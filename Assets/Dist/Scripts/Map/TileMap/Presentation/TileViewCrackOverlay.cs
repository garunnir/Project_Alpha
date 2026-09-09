// ============================================================
// TileViewCrackOverlay — TileView 자식 크랙 쿼드 (단계형)
// ============================================================

using UnityEngine;
using UnityEngine.Rendering;

namespace IsoTilemap
{
    /// <summary>
    /// 프리팹 배선 없이 런타임 팩토리로 크랙 오버레이를 붙인다.
    /// 텍스처는 stage별 프로시저럴(공통 캐시).
    /// </summary>
    public sealed class TileViewCrackOverlay
    {
        const string RootName = "CrackOverlay";

        static Mesh _sharedQuad;
        static Texture2D[] _stageTextures;
        static Material[] _stageMaterials;

        readonly Transform _host;
        GameObject _root;
        MeshFilter _filter;
        MeshRenderer _renderer;
        int _appliedStage = -1;

        public TileViewCrackOverlay(Transform host) =>
            _host = host;

        public void Apply(int stage, float cellSize)
        {
            stage = Mathf.Clamp(stage, 0, TileDamagePresentationConsts.MaxCrackStage);
            if (stage <= 0)
            {
                SetVisible(false);
                _appliedStage = 0;
                return;
            }

            EnsureRoot(cellSize);
            if (_root == null || _renderer == null)
                return;

            if (_appliedStage != stage)
            {
                Material mat = GetStageMaterial(stage);
                if (mat != null)
                    _renderer.sharedMaterial = mat;
                _appliedStage = stage;
            }

            SetVisible(true);
        }

        public Transform Root => _root != null ? _root.transform : null;

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

        void EnsureRoot(float cellSize)
        {
            if (_root != null)
                return;

            if (_host == null)
                return;

            _root = new GameObject(RootName);
            _root.transform.SetParent(_host, false);
            _root.layer = _host.gameObject.layer;

            float scale = Mathf.Max(1e-4f, cellSize) * TileDamagePresentationConsts.CrackPlaneScale;
            _root.transform.localPosition = new Vector3(0f, TileDamagePresentationConsts.CrackYOffset, 0f);
            _root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _root.transform.localScale = new Vector3(scale, scale, 1f);

            _filter = _root.AddComponent<MeshFilter>();
            _filter.sharedMesh = EnsureSharedQuad();

            _renderer = _root.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _renderer.allowOcclusionWhenDynamic = false;
        }

        static Mesh EnsureSharedQuad()
        {
            if (_sharedQuad != null)
                return _sharedQuad;

            var mesh = new Mesh { name = "TileCrackUnitQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _sharedQuad = mesh;
            return _sharedQuad;
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

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Transparent");

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
                };
                if (mat.HasProperty("_BaseMap"))
                    mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", Color.white);
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
            Color crack = new Color(0.08f, 0.08f, 0.08f, 0.85f);
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;

            // stage↑ → 가지 수·두께↑ (결정론적 의사 랜덤)
            int branchCount = 2 + stage;
            int seed = 17 + stage * 97;
            for (int b = 0; b < branchCount; b++)
            {
                int x = size / 2;
                int y = size / 2;
                int steps = size / 2 + stage * 6;
                for (int s = 0; s < steps; s++)
                {
                    PlotDisk(pixels, size, x, y, stage >= 3 ? 1 : 0, crack);
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
