// ============================================================
// TileCubeFaceAtlas — 6면 슬롯 SSOT + 에디터 bake 아틀라스
// ============================================================

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IsoTilemap
{
    /// <summary>
    /// Up/Down/N/S/E/W 슬롯이 바리에이션 SSOT.
    /// 아틀라스·머티리얼은 에디터에서 bake된 에셋만 런타임 사용 (런타임 Blit/패킹 없음).
    /// </summary>
    [CreateAssetMenu(fileName = "TileCubeFaceAtlas", menuName = "Iso/Map/Tile Cube Face Atlas")]
    public sealed class TileCubeFaceAtlas : ScriptableObject
    {
        public const int DefaultCellPixels = 16;
        public const int FaceCount = TileCubeFaceIdUtil.Count;
        public const int AtlasCols = 3;
        public const int AtlasRows = 2;

        [Header("Face slots (variation SSOT)")]
        [SerializeField] Texture2D _upTexture;
        [SerializeField] Texture2D _downTexture;
        [SerializeField] Texture2D _northTexture;
        [SerializeField] Texture2D _southTexture;
        [SerializeField] Texture2D _eastTexture;
        [SerializeField] Texture2D _westTexture;

        [SerializeField] Color _upColor = new Color(0.45f, 0.70f, 0.28f, 1f);
        [SerializeField] Color _downColor = new Color(0.35f, 0.25f, 0.15f, 1f);
        [SerializeField] Color _northColor = new Color(0.55f, 0.38f, 0.22f, 1f);
        [SerializeField] Color _southColor = new Color(0.55f, 0.38f, 0.22f, 1f);
        [SerializeField] Color _eastColor = new Color(0.55f, 0.38f, 0.22f, 1f);
        [SerializeField] Color _westColor = new Color(0.55f, 0.38f, 0.22f, 1f);

        [SerializeField, Min(4)] int _cellPixels = DefaultCellPixels;

        [Header("Bake material")]
        [Tooltip("베이크 결과 머티리얼의 템플릿. 비우면 URP Lit→Unlit→Standard 폴백.")]
        [SerializeField] Material _sourceMaterial;

        [Header("Baked (output)")]
        [SerializeField] Texture2D _bakedAtlas;
        [SerializeField] Material _bakedMaterial;
        [SerializeField, HideInInspector] int _bakedFromSourceId;

        public Texture2D BakedAtlas => _bakedAtlas;
        public Material BakedMaterial => _bakedMaterial;
        public Material SourceMaterial => _sourceMaterial;

        /// <summary>레이아웃 고정 3×2 — bake와 동일 UV.</summary>
        public TileCubeFaceUvSet UvSet => BuildFixedUvSet(Mathf.Max(4, _cellPixels));

        public static TileCubeFaceUvSet BuildFixedUvSet(int cellPixels)
        {
            int cell = Mathf.Max(4, cellPixels);
            int sizeX = cell * AtlasCols;
            int sizeY = cell * AtlasRows;
            float insetU = 0.5f / sizeX;
            float insetV = 0.5f / sizeY;
            float wu = 1f / AtlasCols;
            float hv = 1f / AtlasRows;

            return new TileCubeFaceUvSet(
                FaceRect(0, 1, wu, hv, insetU, insetV),
                FaceRect(1, 1, wu, hv, insetU, insetV),
                FaceRect(2, 1, wu, hv, insetU, insetV),
                FaceRect(0, 0, wu, hv, insetU, insetV),
                FaceRect(1, 0, wu, hv, insetU, insetV),
                FaceRect(2, 0, wu, hv, insetU, insetV));
        }

        static Rect FaceRect(int col, int row, float wu, float hv, float insetU, float insetV) =>
            new Rect(col * wu + insetU, row * hv + insetV, wu - insetU * 2f, hv - insetV * 2f);

#if UNITY_EDITOR
        void OnValidate()
        {
            EditorApplication.delayCall -= BakeAndRefreshVisuals;
            EditorApplication.delayCall += BakeAndRefreshVisuals;
        }

        void BakeAndRefreshVisuals()
        {
            if (this == null)
                return;
            Bake();
            RefreshDependentVisuals();
        }

        [ContextMenu("Bake Atlas")]
        public void Bake()
        {
            int cell = Mathf.Max(4, _cellPixels);
            int sizeX = cell * AtlasCols;
            int sizeY = cell * AtlasRows;

            Texture2D atlas = EnsureBakedAtlasTexture(sizeX, sizeY);
            WriteSlot(atlas, 0, 1, cell, _upTexture, _upColor);
            WriteSlot(atlas, 1, 1, cell, _downTexture, _downColor);
            WriteSlot(atlas, 2, 1, cell, _northTexture, _northColor);
            WriteSlot(atlas, 0, 0, cell, _southTexture, _southColor);
            WriteSlot(atlas, 1, 0, cell, _eastTexture, _eastColor);
            WriteSlot(atlas, 2, 0, cell, _westTexture, _westColor);
            atlas.Apply(false, false);
            EditorUtility.SetDirty(atlas);

            EnsureBakedMaterial(atlas);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        Texture2D EnsureBakedAtlasTexture(int sizeX, int sizeY)
        {
            string assetPath = AssetDatabase.GetAssetPath(this);
            if (_bakedAtlas != null &&
                _bakedAtlas.width == sizeX &&
                _bakedAtlas.height == sizeY)
            {
                return _bakedAtlas;
            }

            if (_bakedAtlas != null)
            {
                DestroyImmediate(_bakedAtlas, true);
                _bakedAtlas = null;
            }

            var atlas = new Texture2D(sizeX, sizeY, TextureFormat.RGBA32, false, false)
            {
                name = name + "_Baked",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            if (!string.IsNullOrEmpty(assetPath))
                AssetDatabase.AddObjectToAsset(atlas, this);
            _bakedAtlas = atlas;
            return atlas;
        }

        void EnsureBakedMaterial(Texture2D atlas)
        {
            int sourceId = _sourceMaterial != null ? _sourceMaterial.GetInstanceID() : 0;
            bool recreate = _bakedMaterial == null || _bakedFromSourceId != sourceId;

            if (recreate)
            {
                if (_bakedMaterial != null)
                {
                    DestroyImmediate(_bakedMaterial, true);
                    _bakedMaterial = null;
                }

                if (_sourceMaterial != null)
                {
                    _bakedMaterial = new Material(_sourceMaterial) { name = name + "_Mat" };
                }
                else
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null)
                        shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (shader == null)
                        shader = Shader.Find("Standard");
                    if (shader == null)
                        return;
                    _bakedMaterial = new Material(shader) { name = name + "_Mat" };
                    if (_bakedMaterial.HasProperty("_BaseColor"))
                        _bakedMaterial.SetColor("_BaseColor", Color.white);
                    if (_bakedMaterial.HasProperty("_Color"))
                        _bakedMaterial.SetColor("_Color", Color.white);
                }

                string assetPath = AssetDatabase.GetAssetPath(this);
                if (!string.IsNullOrEmpty(assetPath))
                    AssetDatabase.AddObjectToAsset(_bakedMaterial, this);
                _bakedFromSourceId = sourceId;
            }
            else if (_sourceMaterial != null)
            {
                _bakedMaterial.CopyPropertiesFromMaterial(_sourceMaterial);
                _bakedMaterial.name = name + "_Mat";
            }

            ApplyAtlasToMaterial(_bakedMaterial, atlas);
            EditorUtility.SetDirty(_bakedMaterial);
        }

        static void ApplyAtlasToMaterial(Material mat, Texture2D atlas)
        {
            if (mat == null || atlas == null)
                return;

            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", atlas);
            if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", atlas);
            mat.mainTexture = atlas;
        }

        void RefreshDependentVisuals()
        {
            TileCubeVisual[] visuals = Resources.FindObjectsOfTypeAll<TileCubeVisual>();
            for (int i = 0; i < visuals.Length; i++)
            {
                TileCubeVisual visual = visuals[i];
                if (visual == null || visual.Atlas != this)
                    continue;
                visual.Apply();
                EditorUtility.SetDirty(visual);
            }
        }

        static void WriteSlot(
            Texture2D atlas,
            int col,
            int row,
            int cell,
            Texture2D source,
            Color fallback)
        {
            int ox = col * cell;
            int oy = row * cell;
            if (source != null)
            {
                if (TryCopyTexture(atlas, ox, oy, cell, source))
                    return;
                Debug.LogWarning(
                    $"[TileCubeFaceAtlas] Bake blit failed for '{source.name}', using color.",
                    source);
            }

            FillSolid(atlas, ox, oy, cell, fallback);
        }

        static bool TryCopyTexture(Texture2D atlas, int ox, int oy, int cell, Texture source)
        {
            RenderTexture rt = null;
            Texture2D readable = null;
            RenderTexture prev = RenderTexture.active;
            try
            {
                rt = RenderTexture.GetTemporary(cell, cell, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                readable = new Texture2D(cell, cell, TextureFormat.RGBA32, false, false);
                readable.ReadPixels(new Rect(0, 0, cell, cell), 0, 0);
                readable.Apply(false, false);
                atlas.SetPixels(ox, oy, cell, cell, readable.GetPixels());
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[TileCubeFaceAtlas] {ex.Message}", source);
                return false;
            }
            finally
            {
                RenderTexture.active = prev;
                if (rt != null)
                    RenderTexture.ReleaseTemporary(rt);
                if (readable != null)
                    DestroyImmediate(readable);
            }
        }

        static void FillSolid(Texture2D atlas, int ox, int oy, int cell, Color fallback)
        {
            for (int y = 0; y < cell; y++)
            {
                for (int x = 0; x < cell; x++)
                {
                    Color c = fallback;
                    if (((x + y) & 3) == 0)
                    {
                        c.r = Mathf.Clamp01(c.r * 0.92f);
                        c.g = Mathf.Clamp01(c.g * 0.92f);
                        c.b = Mathf.Clamp01(c.b * 0.92f);
                    }

                    atlas.SetPixel(ox + x, oy + y, c);
                }
            }
        }
#endif
    }

    public readonly struct TileCubeFaceUvSet
    {
        public readonly Rect Up;
        public readonly Rect Down;
        public readonly Rect North;
        public readonly Rect South;
        public readonly Rect East;
        public readonly Rect West;

        public TileCubeFaceUvSet(Rect up, Rect down, Rect north, Rect south, Rect east, Rect west)
        {
            Up = up;
            Down = down;
            North = north;
            South = south;
            East = east;
            West = west;
        }

        public static TileCubeFaceUvSet Uniform(Rect uv) =>
            new TileCubeFaceUvSet(uv, uv, uv, uv, uv, uv);
    }
}
