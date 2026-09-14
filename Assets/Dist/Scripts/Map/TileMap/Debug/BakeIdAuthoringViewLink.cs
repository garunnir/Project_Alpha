// ============================================================
// [BakeIdAuthoringViewLink] — Playground Authoring TileView ↔ Host live sync 브릿지
// ============================================================

using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// BakeIdPlayground 전용. Authoring <see cref="TileView"/>에 붙어
    /// pose snap / enable / destroy를 <see cref="BakeIdPlaygroundHost"/>에 전달하고 sim entry gather 준비를 담당한다.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class BakeIdAuthoringViewLink : MonoBehaviour
    {
        BakeIdPlaygroundHost _host;
        TileView _view;
        float _cellSize = 1f;

        public TileView View => _view;
        public BakeIdPlaygroundHost Host => _host;

        public static BakeIdAuthoringViewLink Ensure(
            TileView view,
            BakeIdPlaygroundHost host,
            float cellSize)
        {
            if (view == null || host == null)
                return null;

            if (!view.TryGetComponent(out BakeIdAuthoringViewLink link))
                link = view.gameObject.AddComponent<BakeIdAuthoringViewLink>();
            link.Configure(host, view, cellSize);
            return link;
        }

        public void Configure(BakeIdPlaygroundHost host, TileView view, float cellSize)
        {
            _host = host;
            _view = view;
            _cellSize = Mathf.Max(1e-4f, cellSize);
            PrepareViewForGather();
        }

        public bool TryResolveSimEntry(out BakeIdSimTileEntry entry, out string error)
        {
            entry = default;
            error = null;
            if (_view == null)
            {
                error = "view is null";
                return false;
            }

            PrepareViewForGather();
            return BakeIdSimTileUtil.TryFromAuthoringView(
                _view,
                _cellSize,
                out entry,
                out error,
                "[BakeIdPlayground]");
        }

        void PrepareViewForGather()
        {
            if (_view == null)
                return;

            _view.gizmoCellSize = _cellSize;
            EnsurePlacementSlot(_view);
#if UNITY_EDITOR
            if (!Application.isPlaying)
                _view.EnsureEditorPlacementReady();
#endif
        }

        static void EnsurePlacementSlot(TileView view)
        {
            if (view.placementSlot != TilePlacementSlot.None || string.IsNullOrEmpty(view.prefabId))
                return;

            TilePlacementSlot inferred = TileIdentityUtil.InferSlotFromPrefabId(view.prefabId);
            if (inferred != TilePlacementSlot.None)
                view.placementSlot = inferred;
        }

        void OnEnable()
        {
#if UNITY_EDITOR
            if (Application.isPlaying || _view == null || _host == null)
                return;

            MapPlacedView.EditorPoseSnapped += HandleEditorPoseSnapped;
            _host.NotifyAuthoringLinkChanged(_view, immediate: false);
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            MapPlacedView.EditorPoseSnapped -= HandleEditorPoseSnapped;
#endif
        }

        void OnDestroy()
        {
#if UNITY_EDITOR
            if (Application.isPlaying || _host == null || _view == null)
                return;
            _host.NotifyAuthoringViewDestroyed(_view);
#endif
        }

#if UNITY_EDITOR
        void HandleEditorPoseSnapped(MapPlacedView view)
        {
            if (view != _view || _host == null)
                return;
            _host.NotifyAuthoringLinkChanged(_view, immediate: true);
        }
#endif
    }
}
