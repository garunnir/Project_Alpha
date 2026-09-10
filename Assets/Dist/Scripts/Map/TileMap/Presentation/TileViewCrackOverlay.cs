// ============================================================
// TileViewCrackOverlay — Dig 타겟 bounds 큐브 크랙 (1 mesh · 동일 UV×6)
// ============================================================

using UnityEngine;
using UnityEngine.Rendering;

namespace IsoTilemap
{
    /// <summary>
    /// destroy_stage식: stage 텍스처 1장을 큐브 6면에. GO/Renderer는 1개.
    /// </summary>
    public sealed class TileViewCrackOverlay
    {
        const string RootName = "CrackOverlay";

        static Mesh _sharedCube;
        static Texture2D[] _stageTextures;
        static Material[] _stageMaterials;

        readonly Transform _host;
        GameObject _root;
        MeshFilter _filter;
        MeshRenderer _renderer;
        int _appliedStage = -1;

        public TileViewCrackOverlay(Transform host) =>
            _host = host;

        public Transform Root => _root != null ? _root.transform : null;

        public void Apply(int stage, Renderer boundsSource)
        {
            stage = Mathf.Clamp(stage, 0, TileDamagePresentationConsts.MaxCrackStage);
            if (stage <= 0)
            {
                SetVisible(false);
                _appliedStage = 0;
                return;
            }

            EnsureRoot();
            if (_root == null || _renderer == null)
                return;

            FitToBounds(boundsSource);

            if (_appliedStage != stage)
            {
                Material mat = GetStageMaterial(stage);
                if (mat != null)
                    _renderer.sharedMaterial = mat;
                _appliedStage = stage;
            }

            SetVisible(true);
        }

        public void Dispose()
        {
            if (_root == null)
                return;
            Object.Destroy(_root);
            _root = null;
            _filter = null;
            _renderer = null;
            _appliedStage = -1;
        }

        void SetVisible(bool visible)
        {
            if (_root != null && _root.activeSelf != visible)
                _root.SetActive(visible);
        }

        void EnsureRoot()
        {
            if (_root != null)
                return;
            if (_host == null)
                return;

            _root = new GameObject(RootName);
            _root.transform.SetParent(_host, false);
            _root.layer = _host.gameObject.layer;

            _filter = _root.AddComponent<MeshFilter>();
            _filter.sharedMesh = EnsureSharedCube();

            _renderer = _root.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _renderer.allowOcclusionWhenDynamic = false;
        }

        void FitToBounds(Renderer boundsSource)
        {
            if (_root == null || _host == null)
                return;

            float inflate = TileDamagePresentationConsts.CrackBoundsInflate;
            Vector3 localCenter;
            Vector3 localSize;

            if (boundsSource != null)
            {
                Bounds wb = boundsSource.bounds;
                Vector3 min = _host.InverseTransformPoint(wb.min);
                Vector3 max = _host.InverseTransformPoint(wb.max);
                localCenter = (min + max) * 0.5f;
                localSize = new Vector3(
                    Mathf.Abs(max.x - min.x),
                    Mathf.Abs(max.y - min.y),
                    Mathf.Abs(max.z - min.z));
            }
            else
            {
                float cell = MapDigColumnHost.Runtime != null
                    ? MapDigColumnHost.Runtime.CellSize
                    : 1f;
                localCenter = new Vector3(0f, cell * 0.5f, 0f);
                localSize = Vector3.one * cell;
            }

            localSize += Vector3.one * inflate;
            localSize.x = Mathf.Max(1e-3f, localSize.x);
            localSize.y = Mathf.Max(1e-3f, localSize.y);
            localSize.z = Mathf.Max(1e-3f, localSize.z);

            Transform t = _root.transform;
            t.localPosition = localCenter;
            t.localRotation = Quaternion.identity;
            t.localScale = localSize;
        }

        static Mesh EnsureSharedCube()
        {
            if (_sharedCube != null)
                return _sharedCube;

            var full = new Rect(0f, 0f, 1f, 1f);
            _sharedCube = TileCubeMeshBuilder.BuildUniformUvCube(full);
            _sharedCube.name = "TileCrackCube";
            return _sharedCube;
        }

        static Material GetStageMaterial(int stage)
        {
            EnsureStageAssets();
            int index = stage - 1;
            if (index < 0 || index >= _stageMaterials.Length)
                return null;
            return _stageMaterials[index];
        }

        static void EnsureStageAssets()
        {
            if (_stageMaterials != null)
                return;

            int count = TileDamagePresentationConsts.MaxCrackStage;
            _stageTextures = new Texture2D[count];
            _stageMaterials = new Material[count];

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            for (int i = 0; i < count; i++)
            {
                int stage = i + 1;
                Texture2D tex = BuildCrackTexture(stage);
                _stageTextures[i] = tex;
                if (shader == null)
                    continue;

                var mat = new Material(shader)
                {
                    name = $"TileCrackStage{stage}",
                    mainTexture = tex,
                    color = Color.white,
                    renderQueue = (int)RenderQueue.Transparent,
                };
                if (mat.HasProperty("_BaseMap"))
                    mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", Color.white);
                if (mat.HasProperty("_Surface"))
                    mat.SetFloat("_Surface", 1f);
                if (mat.HasProperty("_Blend"))
                    mat.SetFloat("_Blend", 0f);
                if (mat.HasProperty("_Cull"))
                    mat.SetFloat("_Cull", (float)CullMode.Off);
                if (mat.HasProperty("_ZWrite"))
                    mat.SetFloat("_ZWrite", 0f);
                if (mat.HasProperty("_SrcBlend"))
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (mat.HasProperty("_DstBlend"))
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_Cull", (int)CullMode.Off);
                _stageMaterials[i] = mat;
            }
        }

        static Texture2D BuildCrackTexture(int stage)
        {
            int size = TileDamagePresentationConsts.CrackTextureSize;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = $"TileCrackTex{stage}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            Color clear = new Color(0f, 0f, 0f, 0f);
            // 어두운 크랙 — 밝은 dirt 위에서도 대비
            Color crack = new Color(0.08f, 0.07f, 0.06f, 0.92f);
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;

            int branchCount = 2 + stage;
            int seed = 17 + stage * 97;
            for (int b = 0; b < branchCount; b++)
            {
                int x = size / 2;
                int y = size / 2;
                int steps = size / 2 + stage * 8;
                for (int s = 0; s < steps; s++)
                {
                    PlotDisk(pixels, size, x, y, stage >= 2 ? 1 : 0, crack);
                    seed = unchecked(seed * 1103515245 + 12345);
                    int dir = (seed >> 16) & 3;
                    if (dir == 0) x++;
                    else if (dir == 1) x--;
                    else if (dir == 2) y++;
                    else y--;
                    x = Mathf.Clamp(x, 1, size - 2);
                    y = Mathf.Clamp(y, 1, size - 2);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }

        static void PlotDisk(Color[] pixels, int size, int cx, int cy, int radius, Color color)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy > radius * radius)
                        continue;
                    int x = cx + dx;
                    int y = cy + dy;
                    if ((uint)x >= (uint)size || (uint)y >= (uint)size)
                        continue;
                    pixels[y * size + x] = color;
                }
            }
        }
    }
}
