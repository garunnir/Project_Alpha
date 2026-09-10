using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    // ============================================================
    // TileViewPresentationApplier — 화면 타일에만 오클루전·고스트·선택 반영
    // ============================================================
    public sealed class TileViewPresentationApplier
    {
        const float DisplayEpsilon = 0.015f;

        private readonly ITileViewRegistry _registry;
        private readonly TileMapModel _model;
        private readonly TilePresentationStore _store = new TilePresentationStore();
        private readonly TilePresentationEntryStore _entries = new TilePresentationEntryStore();
        private readonly Dictionary<Guid, float> _characterOcclusionDisplay = new();
        private readonly List<Guid> _occlusionTickScratch = new();
        private readonly List<Guid> _occlusionRemoveScratch = new();
        private readonly List<Guid> _engagedIdScratch = new();
        /// <summary>현재 구조물 숨김 대상 타일 ID 캐시. 판정 SSOT는 policy+ctx이며 스킵 근거로 쓰지 않는다.</summary>
        private readonly HashSet<Guid> _structuralHidden = new();
        private readonly HashSet<Guid> _appliedSightLineTrace = new();
        private readonly HashSet<Guid> _newHiddenScratch = new();
        private readonly HashSet<Guid> _newTraceScratch = new();
        private readonly HashSet<Guid> _candidateScratch = new();
        private BuildingGroupRegistry _buildingRegistry;
        private PlayerFloorVisibilityPolicy _floorPolicy;
        private FloorVisibilitySyncPlanner _planner;
        private StructuralVisibilityHiddenSetComputer _hiddenComputer;
        private FloorVisibilityContext _floorContext;
        private FloorVisibilityContext _lastSyncedCtx;
        private StructuralHidePresentationMode _structuralHideMode = StructuralHidePresentationMode.DisableGameObject;
        private bool _hasFloorContext;
        private bool _hasLastSyncedCtx;

        public TileViewPresentationApplier(ITileViewRegistry registry, TileMapModel model)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _model = model ?? throw new ArgumentNullException(nameof(model));
        }

        public TilePresentationEntryStore Entries => _entries;

        public bool IsStructuralVisibilityHidden(Guid tileId) => _structuralHidden.Contains(tileId);

        public bool IsSightLineBuildingTrace(Guid tileId) => _appliedSightLineTrace.Contains(tileId);

        public void ConfigureFloorVisibility(
            PlayerFloorVisibilityPolicy policy,
            BuildingGroupRegistry buildingRegistry,
            TileMapCacheHub hub,
            StructuralHidePresentationMode structuralHideMode = StructuralHidePresentationMode.DisableGameObject)
        {
            _floorPolicy = policy;
            _buildingRegistry = buildingRegistry;
            _planner = hub != null && buildingRegistry != null
                ? new FloorVisibilitySyncPlanner(buildingRegistry, hub)
                : null;
            _hiddenComputer = policy != null
                ? new StructuralVisibilityHiddenSetComputer(policy, _model)
                : null;
            SetStructuralHidePresentationMode(structuralHideMode);
        }

        public void SetStructuralHidePresentationMode(StructuralHidePresentationMode mode)
        {
            if (_structuralHideMode == mode)
                return;

            _structuralHideMode = mode;
            ReapplyStructuralHiddenPresentation();
        }

        public void ResetFloorVisibilityState()
        {
            _hasFloorContext = false;
            _hasLastSyncedCtx = false;
            _structuralHidden.Clear();
            _appliedSightLineTrace.Clear();
        }

        /// <summary>층 가시성 컨텍스트 변경 시 후보·diff만 반영합니다.</summary>
        public void SyncFloorVisibility(
            in FloorVisibilityContext ctx,
            IMapModelReadOnly model)
        {
            _floorContext = ctx;
            _hasFloorContext = _floorPolicy != null && model != null &&
                               _planner != null && _hiddenComputer != null;

            if (!_hasFloorContext)
                return;

            bool indoorToOutdoorTransition = _hasLastSyncedCtx &&
                                             !_lastSyncedCtx.IsPlayerOutdoor &&
                                             ctx.IsPlayerOutdoor;
            bool outdoorToIndoorTransition = _hasLastSyncedCtx &&
                                             _lastSyncedCtx.IsPlayerOutdoor &&
                                             !ctx.IsPlayerOutdoor;
            bool visibilityModeTransition = indoorToOutdoorTransition || outdoorToIndoorTransition;

            _planner.BuildCandidateTileIds(
                in ctx,
                in _lastSyncedCtx,
                _hasLastSyncedCtx,
                _structuralHidden,
                _candidateScratch);

            _hiddenComputer.Compute(in ctx, _candidateScratch, _newHiddenScratch);
            ReconcileStructuralVisibilityCandidates(
                _newHiddenScratch,
                _candidateScratch,
                clearShowOcclusion: visibilityModeTransition);
            SyncSightLineTrace(in ctx);

            _lastSyncedCtx = ctx;
            _hasLastSyncedCtx = true;
            IsoVisibleDepthSortRegistry.MarkDirty();
        }

        /// <summary>
        /// 층 가시성 sync 후보를 policy 결과로 일괄 재적용합니다.
        /// - 구조물 숨김 대상 캐시(_structuralHidden) 갱신
        /// - 전환 시 stale occlusion 채널 정리
        /// - 최종 표현 ApplyResolved
        /// </summary>
        void ReconcileStructuralVisibilityCandidates(
            HashSet<Guid> newHidden,
            HashSet<Guid> candidates,
            bool clearShowOcclusion)
        {
            if (candidates == null || candidates.Count == 0)
                return;

            foreach (Guid tileId in candidates)
            {
                bool shouldHide = newHidden.Contains(tileId);
                bool forceOcclusionClear = clearShowOcclusion && !shouldHide;
                ReconcileTilePresentation(tileId, shouldHide, forceOcclusionClear);
            }
        }

        /// <summary>
        /// policy structural hide + occlusion 정리 + 최종 표현. sync·스폰 공통 진입점.
        /// </summary>
        void ReconcileTilePresentation(Guid tileId, bool shouldStructuralHide, bool forceClearOcclusion)
        {
            bool wasHidden = _structuralHidden.Contains(tileId);

            if (shouldStructuralHide)
                _structuralHidden.Add(tileId);
            else
                _structuralHidden.Remove(tileId);

            if (shouldStructuralHide || wasHidden != shouldStructuralHide || forceClearOcclusion)
                ClearCharacterOcclusionState(tileId);

            SyncOcclusionDisplayCacheForTile(tileId);
            ApplyResolved(tileId);
        }

        /// <summary>
        /// display 캐시는 즉시 표시값이 아니라 target 페이드 시작점을 나타냅니다.
        /// 보일 타일의 target occlusion이 남아 있으면 0f에서 다시 보간하도록 캐시를 유지합니다.
        /// </summary>
        void SyncOcclusionDisplayCacheForTile(Guid tileId)
        {
            TilePresentationResolved resolved = Resolve(tileId);
            if (!resolved.StructuralHidden && resolved.CharacterOcclusion > DisplayEpsilon)
                _characterOcclusionDisplay[tileId] = 0f;
            else
                _characterOcclusionDisplay.Remove(tileId);
        }

        void SyncSightLineTrace(in FloorVisibilityContext ctx)
        {
            _newTraceScratch.Clear();

            if (ctx.IsPlayerOutdoor && _buildingRegistry != null)
            {
                foreach (int buildingId in ctx.PlayerBlockingBuildingIds)
                {
                    IReadOnlyCollection<Guid> traceTiles =
                        _buildingRegistry.GetMinCellYFloorTilesForBuilding(buildingId);
                    foreach (Guid tileId in traceTiles)
                        _newTraceScratch.Add(tileId);
                }
            }

            ApplyTraceDiff(_newTraceScratch);
        }

        void ApplyTraceDiff(HashSet<Guid> newTrace)
        {
            _occlusionRemoveScratch.Clear();
            foreach (Guid tileId in _appliedSightLineTrace)
            {
                if (!newTrace.Contains(tileId))
                    _occlusionRemoveScratch.Add(tileId);
            }

            for (int i = 0; i < _occlusionRemoveScratch.Count; i++)
            {
                Guid tileId = _occlusionRemoveScratch[i];
                _appliedSightLineTrace.Remove(tileId);
                ApplyResolved(tileId);
            }

            foreach (Guid tileId in newTrace)
            {
                if (_appliedSightLineTrace.Contains(tileId))
                    continue;

                _appliedSightLineTrace.Add(tileId);
                ApplyResolved(tileId);
            }
        }

        /// <summary>
        /// 비저장(Transient) 합성 — 카메라/층·Ghost·Selected·Vivid.
        /// 저장 축(크랙)은 포함하지 않음.
        /// </summary>
        public TilePresentationResolved Resolve(Guid tileId)
        {
            bool structuralHidden = _structuralHidden.Contains(tileId);
            bool trace = _appliedSightLineTrace.Contains(tileId);
            float occlusion = structuralHidden
                ? 0f
                : PresentationEntryQueries.ResolveCharacterOcclusion(tileId, _entries);
            bool ghosted = PresentationEntryQueries.ResolveGhosted(tileId, _entries);
            bool selected = _store.IsSelected(tileId);
            float vividEmphasis = _store.GetVividEmphasis(tileId);
            return new TilePresentationResolved(
                structuralHidden,
                trace,
                occlusion,
                ghosted,
                selected,
                vividEmphasis);
        }

        /// <summary>비저장 축을 뷰에 적용. Persistent(크랙)는 건드리지 않음.</summary>
        public void ApplyResolved(Guid tileId)
        {
            if (!_registry.TryGetView(tileId, out TileView view))
                return;

            view.ConfigureStructuralHidePresentationMode(_structuralHideMode);
            view.ApplyTransientPresentation(Resolve(tileId));
        }

        /// <summary>
        /// 저장 축 — Dig/구조 remaining → 크랙. Transient Resolve와 독립.
        /// </summary>
        public void ApplyDamageStage(Guid tileId, int remaining, int maxHp)
        {
            if (!_registry.TryGetView(tileId, out TileView view))
                return;

            int stage = TileDamagePresentationConsts.StageFromRemaining(remaining, maxHp);
            view.ApplyPersistentPresentation(stage);
        }

        /// <summary>청크 스폰·재로드 — 저장 HP → Persistent 크랙만 복원.</summary>
        public void SyncDamageStageFromHost(Guid tileId)
        {
            if (!_registry.TryGetView(tileId, out TileView view))
                return;

            int stage = TileDamagePresentation.ResolveStage(
                tileId,
                _model,
                MapDigColumnHost.Runtime);
            view.ApplyPersistentPresentation(stage);
        }

        void ReapplyStructuralHiddenPresentation()
        {
            foreach (Guid tileId in _structuralHidden)
                ApplyResolved(tileId);
        }

        /// <summary>BFS 아래벽 블렌드 채널.</summary>
        public void ApplyOcclusionDelta(TileOcclusionPresentationDelta delta) =>
            ApplyCharacterOcclusionDelta(PresentationSource.BfsWallOcclusion, delta);

        /// <summary>카메라↔플레이어 시선 근접 블렌드 채널.</summary>
        public void ApplyProximityBlendDelta(TileOcclusionPresentationDelta delta) =>
            ApplyCharacterOcclusionDelta(PresentationSource.ProximitySightLine, delta);

        void ApplyCharacterOcclusionDelta(PresentationSource source, TileOcclusionPresentationDelta delta)
        {
            if (delta.IsEmpty)
                return;

            ApplyPresentationDelta(delta, source, PresentationConcern.CharacterOcclusion);
        }

        void ApplyPresentationDelta(
            TileOcclusionPresentationDelta delta,
            PresentationSource source,
            PresentationConcern concern)
        {
            _entries.ApplyOcclusionDelta(source, concern, in delta);
        }

        /// <summary>
        /// engaged·페이드 중 타일의 display를 resolved target 쪽으로 보간해 뷰에 반영합니다.
        /// <see cref="CharacterOcclusionDisplayDriver"/>가 매 프레임 호출합니다.
        /// </summary>
        public void TickCharacterOcclusionDisplay(float smoothSpeed, float deltaTime)
        {
            CollectCharacterOcclusionTickTargets(_occlusionTickScratch);
            if (_occlusionTickScratch.Count == 0)
                return;

            float factor = OcclusionBlendMath.ExpSmoothFactor(smoothSpeed, deltaTime);
            _occlusionRemoveScratch.Clear();

            for (int i = 0; i < _occlusionTickScratch.Count; i++)
            {
                Guid tileId = _occlusionTickScratch[i];
                if (!_registry.TryGetView(tileId, out TileView view))
                {
                    _occlusionRemoveScratch.Add(tileId);
                    continue;
                }

                if (IsStructuralVisibilityHidden(tileId))
                {
                    FadeOcclusionDisplayTowards(tileId, view, 0f, factor);
                    continue;
                }

                float target = PresentationEntryQueries.ResolveCharacterOcclusion(tileId, _entries);
                _characterOcclusionDisplay.TryGetValue(tileId, out float display);
                float newDisplay = OcclusionBlendMath.SmoothTowards(display, target, factor);
                _characterOcclusionDisplay[tileId] = newDisplay;

                if (target <= DisplayEpsilon && newDisplay <= DisplayEpsilon)
                {
                    _occlusionRemoveScratch.Add(tileId);
                    continue;
                }

                view.SetCharacterOcclusion(newDisplay);
            }

            for (int i = 0; i < _occlusionRemoveScratch.Count; i++)
            {
                Guid tileId = _occlusionRemoveScratch[i];
                _characterOcclusionDisplay.Remove(tileId);

                if (_registry.TryGetView(tileId, out TileView view) &&
                    PresentationEntryQueries.ResolveCharacterOcclusion(tileId, _entries) <= DisplayEpsilon)
                {
                    view.SetCharacterOcclusion(0f);
                }
            }
        }

        /// <summary>셧다운·맵 전환 시 display 상태와 뷰를 초기화합니다.</summary>
        public void ResetCharacterOcclusionDisplay()
        {
            foreach (KeyValuePair<Guid, float> kv in _characterOcclusionDisplay)
            {
                if (_registry.TryGetView(kv.Key, out TileView view))
                    view.SetCharacterOcclusion(0f);
            }

            _characterOcclusionDisplay.Clear();
            _occlusionTickScratch.Clear();
            _occlusionRemoveScratch.Clear();
        }

        void CollectCharacterOcclusionTickTargets(List<Guid> into)
        {
            into.Clear();
            AppendEngagedOcclusionTiles(PresentationSource.ProximitySightLine, into);
            AppendEngagedOcclusionTiles(PresentationSource.BfsWallOcclusion, into);

            foreach (KeyValuePair<Guid, float> kv in _characterOcclusionDisplay)
            {
                if (kv.Value > DisplayEpsilon && !into.Contains(kv.Key))
                    into.Add(kv.Key);
            }
        }

        void AppendEngagedOcclusionTiles(PresentationSource source, List<Guid> into)
        {
            _entries.CollectEngagedTileIds(source, _engagedIdScratch);
            for (int i = 0; i < _engagedIdScratch.Count; i++)
            {
                Guid tileId = _engagedIdScratch[i];
                if (!into.Contains(tileId))
                    into.Add(tileId);
            }
        }

        public void SetGhosted(Guid tileId, bool ghosted)
        {
            if (ghosted)
                _entries.Set(PresentationConcern.GhostAmount, PresentationSource.Ghost, tileId, 1f);
            else
                _entries.Remove(PresentationConcern.GhostAmount, PresentationSource.Ghost, tileId);

            ApplyResolved(tileId);
        }

        public void SetSelected(Guid tileId, bool selected)
        {
            _store.SetSelected(tileId, selected);
            ApplyResolved(tileId);
        }

        /// <summary>
        /// 비저장 · Dig 조준 Add 강조만. Transient 전체 Resolve·Persistent 크랙 미포함.
        /// </summary>
        public void SetVividEmphasis(Guid tileId, float amount)
        {
            _store.SetVividEmphasis(tileId, amount);
            if (!_registry.TryGetView(tileId, out TileView view))
                return;
            if (IsStructuralVisibilityHidden(tileId))
                return;

            view.SetVividEmphasis(amount);
        }

        /// <summary>디버그·오버레이: 타일에 관여 중인 entry만 (기본 Query).</summary>
        public IReadOnlyList<TilePresentationEntry> QueryEntriesForTile(Guid tileId) =>
            _entries.Query(PresentationQuery.ForTile(tileId));

        /// <summary>청크 로드·스폰 직후 entry store·선택 상태와 뷰를 맞춥니다.</summary>
        public void SyncPresentationForTile(Guid tileId)
        {
            if (!_registry.TryGetView(tileId, out _))
                return;

            if (_hasFloorContext && _model.TryGetTileById(tileId, out TileData tile))
            {
                bool shouldHide = !_floorPolicy.IsTileVisible(tile, in _floorContext);
                bool trace = ShouldShowSightLineBuildingTrace(tile, in _floorContext, _buildingRegistry);

                if (trace)
                    _appliedSightLineTrace.Add(tileId);
                else
                    _appliedSightLineTrace.Remove(tileId);

                ReconcileTilePresentation(tileId, shouldHide, forceClearOcclusion: false);
                SyncDamageStageFromHost(tileId);
                return;
            }

            SyncOcclusionDisplayCacheForTile(tileId);
            ApplyResolved(tileId);
            SyncDamageStageFromHost(tileId);
        }

        static bool ShouldShowSightLineBuildingTrace(
            in TileData tile,
            in FloorVisibilityContext ctx,
            BuildingGroupRegistry buildingRegistry)
        {
            if (!ctx.IsPlayerOutdoor || buildingRegistry == null)
                return false;

            int buildingId = tile.identity.buildingId;
            if (buildingId <= 0 || !ctx.PlayerBlockingBuildingIds.Contains(buildingId))
                return false;

            return TileIdentityUtil.IsFloorTile(tile.identity) &&
                   buildingRegistry.IsBottomFloorTile(buildingId, tile.tileDefId);
        }

        /// <summary>타일의 캐릭터 가림 관련 모든 채널(proximity/BFS/display)을 초기화합니다.</summary>
        void ClearCharacterOcclusionState(Guid tileId)
        {
            _entries.Remove(
                PresentationConcern.CharacterOcclusion,
                PresentationSource.ProximitySightLine,
                tileId);
            _entries.Remove(
                PresentationConcern.CharacterOcclusion,
                PresentationSource.BfsWallOcclusion,
                tileId);
            _characterOcclusionDisplay.Remove(tileId);
        }

        void FadeOcclusionDisplayTowards(Guid tileId, TileView view, float target, float factor)
        {
            _characterOcclusionDisplay.TryGetValue(tileId, out float display);
            float newDisplay = OcclusionBlendMath.SmoothTowards(display, target, factor);

            if (newDisplay <= DisplayEpsilon)
            {
                _characterOcclusionDisplay.Remove(tileId);
                view.SetCharacterOcclusion(0f);
                return;
            }

            _characterOcclusionDisplay[tileId] = newDisplay;
            view.SetCharacterOcclusion(newDisplay);
        }
    }
}
