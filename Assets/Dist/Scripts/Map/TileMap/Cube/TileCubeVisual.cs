// ============================================================
// TileCubeVisual — bake된 FaceAtlas만 적용 (런타임 패킹 없음)
// ============================================================

using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// Terrain Occupied 큐브용. MeshFilter/MeshRenderer는 프리팹에 미리.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class TileCubeVisual : MonoBehaviour
    {
        [SerializeField] TileCubeFaceAtlas _atlas;

        MeshFilter _filter;
        MeshRenderer _renderer;
        Mesh _ownedMesh;

        public TileCubeFaceAtlas Atlas => _atlas;

        void Awake() => Apply();

        void OnDestroy()
        {
            if (_ownedMesh == null)
                return;
            if (Application.isPlaying)
                Destroy(_ownedMesh);
            else
                DestroyImmediate(_ownedMesh);
            _ownedMesh = null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall -= ApplyDelayed;
            UnityEditor.EditorApplication.delayCall += ApplyDelayed;
        }

        void ApplyDelayed()
        {
            if (this == null)
                return;
            Apply();
        }
#endif

        public void SetAtlas(TileCubeFaceAtlas atlas)
        {
            _atlas = atlas;
            Apply();
        }

        public void Apply()
        {
            if (_atlas == null)
                return;

            _filter ??= GetComponent<MeshFilter>();
            _renderer ??= GetComponent<MeshRenderer>();
            if (_filter == null || _renderer == null)
                return;

            Texture2D atlasTex = _atlas.BakedAtlas;
            Material mat = _atlas.BakedMaterial;
            if (atlasTex == null || mat == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"[TileCubeVisual] '{_atlas.name}' has no bake. Open atlas and wait for OnValidate bake, or ContextMenu Bake Atlas.",
                    _atlas);
#endif
                return;
            }

            Mesh mesh = TileCubeMeshBuilder.Build(_atlas.UvSet);
            if (_ownedMesh != null)
            {
                if (Application.isPlaying)
                    Destroy(_ownedMesh);
                else
                    DestroyImmediate(_ownedMesh);
            }

            _ownedMesh = mesh;
            _filter.sharedMesh = mesh;
            _renderer.sharedMaterial = mat;

            // Play: Shade가 bake shared에서 material 인스턴스를 잡아 EmphasisAdd(Dig 하이라이트)가 먹게 함.
            var shade = GetComponent<ShadeObjectController>();
            if (shade != null)
                shade.RebindMaterial(useSharedMaterial: !Application.isPlaying);
        }
    }
}
