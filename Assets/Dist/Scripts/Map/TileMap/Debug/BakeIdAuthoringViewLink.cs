// ============================================================
// [BakeIdAuthoringViewLink] — Playground Authoring TileView ↔ Host live sync 브릿지
// ============================================================

using Sirenix.OdinInspector;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// BakeIdPlayground 전용. Authoring <see cref="TileView"/>에 붙어
    /// pose snap / enable / destroy를 <see cref="BakeIdPlaygroundHost"/>에 전달하고 sim entry gather 준비를 담당한다.
    /// <see cref="probeLabel"/>은 버튼으로만 Host <c>simProbes</c>와 양방향 연동한다 (타이핑 중 자동 sync 없음).
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class BakeIdAuthoringViewLink : MonoBehaviour
    {
        BakeIdPlaygroundHost _host;
        TileView _view;
        float _cellSize = 1f;

        [SerializeField]
        [LabelText("Probe Label")]
        [Tooltip("타이핑만으로는 Host에 안 감. Push → Host / Pull ← Host 버튼으로 연동.")]
        string probeLabel;

        public TileView View => _view;
        public BakeIdPlaygroundHost Host => _host;
        public string ProbeLabel => probeLabel;

        [ShowInInspector]
        [ReadOnly]
        [LabelText("Resolved Cell")]
        string ResolvedCellPreview
        {
            get
            {
                if (!TryResolveSimEntry(out BakeIdSimTileEntry entry, out _, prepareGather: false))
                    return "(unresolved)";
                return entry.kind == BakeIdSimTileKind.ThinWall
                    ? $"{entry.cell}↔{entry.cellB}"
                    : entry.cell.ToString();
            }
        }

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
            // AddComponent → OnEnable runs before Configure; bind only after fields exist.
            BindEditorPoseSubscription(notifyHost: true);
        }

        /// <summary>Sim → Authoring 채울 때 notify 없이 라벨만 심음.</summary>
        public void SetProbeLabelWithoutNotify(string label)
        {
            probeLabel = label ?? string.Empty;
        }

        [ButtonGroup("Probe Sync")]
        [Button("Push → Host", ButtonSizes.Medium)]
        [GUIColor(0.55f, 0.95f, 0.65f)]
        public void PushProbeToHost()
        {
            if (!EnsureHostBound())
                return;
            _host.PushProbeFromAuthoringLink(this);
        }

        [ButtonGroup("Probe Sync")]
        [Button("Pull ← Host", ButtonSizes.Medium)]
        [GUIColor(0.65f, 0.85f, 1f)]
        public void PullProbeFromHost()
        {
            if (!EnsureHostBound())
                return;
            _host.PullProbeToAuthoringLink(this);
        }

        [ButtonGroup("Probe Sync")]
        [Button("Clear Probe", ButtonSizes.Medium)]
        public void ClearProbeBinding()
        {
            if (!EnsureHostBound())
                return;
            _host.ClearProbeFromAuthoringLink(this);
        }

        public bool TryResolveSimEntry(out BakeIdSimTileEntry entry, out string error) =>
            TryResolveSimEntry(out entry, out error, prepareGather: true);

        /// <param name="prepareGather">
        /// false면 EnsureEditorPlacementReady(스냅) 생략 — probe upsert가 pose snap 재진입하지 않게.
        /// </param>
        public bool TryResolveSimEntry(out BakeIdSimTileEntry entry, out string error, bool prepareGather)
        {
            entry = default;
            error = null;
            EnsureViewBound();
            if (_view == null)
            {
                error = "view is null";
                return false;
            }

            if (prepareGather)
                PrepareViewForGather();
            else
            {
                _view.gizmoCellSize = _cellSize;
                EnsurePlacementSlot(_view);
            }

            return BakeIdSimTileUtil.TryFromAuthoringView(
                _view,
                _cellSize,
                out entry,
                out error,
                "[BakeIdPlayground]");
        }

        bool EnsureHostBound()
        {
            EnsureViewBound();
            if (_host == null)
                _host = GetComponentInParent<BakeIdPlaygroundHost>();
            if (_host != null)
                return true;

            Debug.LogWarning("[BakeIdPlayground] ViewLink: Host missing (parent BakeIdPlaygroundHost)");
            return false;
        }

        void EnsureViewBound()
        {
            if (_view == null)
                _view = GetComponent<TileView>();
            if (_cellSize <= 1e-4f && _host != null)
                _cellSize = Mathf.Max(1e-4f, _host.CellSize);
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
            BindEditorPoseSubscription(notifyHost: true);
        }

        void OnDisable()
        {
            UnbindEditorPoseSubscription();
        }

        void OnDestroy()
        {
#if UNITY_EDITOR
            UnbindEditorPoseSubscription();
            if (Application.isPlaying || _host == null || _view == null)
                return;
            _host.NotifyAuthoringViewDestroyed(_view);
#endif
        }

        void BindEditorPoseSubscription(bool notifyHost)
        {
#if UNITY_EDITOR
            UnbindEditorPoseSubscription();
            if (Application.isPlaying || !isActiveAndEnabled || _view == null || _host == null)
                return;

            MapPlacedView.EditorPoseSnapped += HandleEditorPoseSnapped;
            if (notifyHost)
                _host.NotifyAuthoringLinkChanged(_view, immediate: false);
#endif
        }

        void UnbindEditorPoseSubscription()
        {
#if UNITY_EDITOR
            MapPlacedView.EditorPoseSnapped -= HandleEditorPoseSnapped;
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
