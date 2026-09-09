using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

// ============================================================
// TileView — 씬 타일 오브젝트의 identity·pose·프레젠테이션 뷰
// ============================================================
// 씬에 실제로 붙어있는 타일 오브젝트용 View.
// Anchor + Size + PrefabId 기반 메타데이터를 유지하고,
// 런타임 데이터 변경을 시각 상태(셰이더 컨트롤)까지 반영합니다.
namespace IsoTilemap
{
    public class TileView : MapPlacedView
    {
        public enum TileType
        {
            none = 0,
            Floor = 1,
            Wall = 2,
            // 3 = legacy Obstacle (JSON 로드 시 Wall로 정규화)
            Slope = 5,
            /// <summary>JSON wallEdges 승격. GridPos=앵커 셀, TileIdentity.wallFace=면.</summary>
            EdgeWall = 4
        }

        // 기본축 시각 상태. 단일 선택이며 우선순위 규칙으로 결정한다.
        // Selected는 별도 오버레이축으로 독립적으로 적용된다.
        public enum TileBaseVisualState
        {
            Visible = 0,
            Ghosted = 1,
            HiddenByCharacter = 2,
        }

        [Header("Placement Slot")]
        public TilePlacementSlot placementSlot = TilePlacementSlot.None;

        [FormerlySerializedAs("tileType")]
        [SerializeField, HideInInspector] TileType _legacyTileType = TileType.none;

        /// <summary>VerticalFace일 때 JSON wallEdges의 face(0=+X, 1=+Z). 에디터 저장 시 사용.</summary>
        [FormerlySerializedAs("wallEdgeFace")]
        [Range(0, 1)] public byte wallFace;

        [Header("Render Controller")]
        [SerializeField] private ShadeObjectController _shadeController;
        [Tooltip("Selected 오버레이용 URP RenderingLayer 비트의 단일 진실원 SO")]
        [SerializeField] private SelectionLayerConfig _selectionLayer;
        [Header("Blocked Trace")]
        [Tooltip("타일이 숨김 상태일 때 표시할 흔적 오브젝트(데칼/메시 등).")]
        [SerializeField] private GameObject _blockedTraceObject;
        private ShadowCastingMode _defaultShadowCastingMode = ShadowCastingMode.On; 

        private TileBaseVisualState _currentBaseState = TileBaseVisualState.Visible;
        private float _characterOcclusion;
        private bool _isGhosted;
        private bool _sightLineBuildingHidden;
        private bool _structuralVisibilityHidden;
        private StructuralHidePresentationMode _structuralHideMode = StructuralHidePresentationMode.DisableGameObject;
        private bool _currentSelected;
        private float _vividEmphasisAmount;
        private bool _baseStateInitialized;
        private bool _selectedInitialized;
        private int _currentDamageStage = -1;
        private TileViewCrackOverlay _crackOverlay;

        private const float OcclusionEpsilon = 1e-4f;
        private const float ShadowOnlyOcclusionThreshold = 0.98f;
        private const float BlockedTraceOcclusionThreshold = 0.5f;
        private const float AdditionalLightFadeStart = 0.25f;
        private const float AdditionalLightFadeEnd = 0.7f;

        readonly List<Renderer> _sortRendererScratch = new();

        private void Awake()
        {
            MigrateLegacyTileType();
            CacheControllers();
            ForceApplyBaseState(TileBaseVisualState.Visible);
            ForceApplySelectedOverlay(false);
            ForceApplyVividEmphasis(0f);
            SetBlockedTraceVisible(false);
        }

        protected override void Reset()
        {
            base.Reset();
            CacheControllers();
        }

        protected override void OnValidate()
        {
            MigrateLegacyTileType();
            if (placementSlot == TilePlacementSlot.None &&
                !string.IsNullOrEmpty(prefabId))
            {
                var inferred = TileIdentityUtil.InferSlotFromPrefabId(prefabId);
                if (inferred != TilePlacementSlot.None)
                    placementSlot = inferred;
            }

            CacheControllers();
            base.OnValidate();
        }

        protected override void ApplyEditorPose()
        {
            float cs = SafeCellSize;
            if (placementSlot == TilePlacementSlot.VerticalFace)
            {
                if (WallEdgePicker.TryPickNearest(transform.position, cs, out var nearest))
                {
                    gridPos = nearest.Anchor;
                    wallFace = (byte)nearest.Face;
                }

                WallEdgeKey key = new WallEdgeKey(gridPos, (WallFace)Mathf.Clamp(wallFace, 0, 1));
                WallEdgeKey.GetWorldPose(key, cs, out Vector3 edgePos, out Quaternion edgeRot);
                transform.SetPositionAndRotation(edgePos, edgeRot);
            }
            else if (placementSlot == TilePlacementSlot.HorizontalFace)
            {
                if (FloorFacePicker.TryPickNearest(transform.position, cs, out var nearest))
                    gridPos = nearest.CellAbove;

                FloorFaceKey key = FloorFaceKey.ForWalkableCell(gridPos);
                FloorFaceKey.GetWorldPose(key, cs, out Vector3 floorPos, out Quaternion floorRot);
                transform.SetPositionAndRotation(floorPos, floorRot);
            }
            else
            {
                gridPos = TileHelper.ConvertWorldToGrid(transform.position, cs);
                transform.position = TileHelper.ConvertGridToWorldPos(gridPos, cs);
            }
        }

        static TilePlacementSlot MapLegacyTileType(TileType legacy) =>
            legacy switch
            {
                TileType.Floor => TilePlacementSlot.HorizontalFace,
                TileType.EdgeWall => TilePlacementSlot.VerticalFace,
                TileType.Wall or TileType.Slope => TilePlacementSlot.OccupiedCell,
                _ => TilePlacementSlot.None,
            };

        void MigrateLegacyTileType()
        {
            if (_legacyTileType == TileType.none)
                return;

            if (placementSlot == TilePlacementSlot.None)
                placementSlot = MapLegacyTileType(_legacyTileType);

            _legacyTileType = TileType.none;
        }

        private void CacheControllers()
        {
            _shadeController ??= GetComponentInChildren<ShadeObjectController>();
            Renderer renderer = _shadeController?.CachedRenderer;
            if (renderer != null)
                _defaultShadowCastingMode = renderer.shadowCastingMode;

            RefreshIsoDepthSortRegistration();
        }

        void OnEnable()
        {
            if (Application.isPlaying)
                RefreshIsoDepthSortRegistration();
        }

        void OnDisable()
        {
            if (Application.isPlaying)
                IsoVisibleDepthSortRegistry.UnregisterOwner(this);
        }

        void RefreshIsoDepthSortRegistration()
        {
            if (!Application.isPlaying)
                return;

            IsoVisibleDepthSortRegistry.UnregisterOwner(this);

            if (!ShouldParticipateInIsoDepthSort())
                return;

            IsoDepthSortKey key = IsoDepthSortKey.FromTileView(this);
            CollectSortRenderers(_sortRendererScratch);
            for (int i = 0; i < _sortRendererScratch.Count; i++)
                IsoVisibleDepthSortRegistry.Register(_sortRendererScratch[i], key, this);
        }

        bool ShouldParticipateInIsoDepthSort() => false;

        void CollectSortRenderers(List<Renderer> into)
        {
            into.Clear();
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            Transform blockedTraceRoot = _blockedTraceObject != null ? _blockedTraceObject.transform : null;
            Transform crackRoot = _crackOverlay != null ? _crackOverlay.Root : null;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (blockedTraceRoot != null &&
                    (renderer.transform == blockedTraceRoot || renderer.transform.IsChildOf(blockedTraceRoot)))
                    continue;

                if (crackRoot != null &&
                    (renderer.transform == crackRoot || renderer.transform.IsChildOf(crackRoot)))
                    continue;

                into.Add(renderer);
            }
        }

        internal void UpdateTile(TileData tileData, float cellSize)
        {
            ApplyWorldPose(tileData, cellSize);

            placementSlot = TileIdentityUtil.GetPlacementSlot(tileData.identity);
            prefabId = tileData.identity.PrefabId;
            size = tileData.identity.sizeUnit;
            gridPos = tileData.identity.GridPos;

            if (placementSlot == TilePlacementSlot.VerticalFace)
                wallFace = (byte)Mathf.Clamp(tileData.identity.wallFace, 0, 1);

            TileCollisionPolicy.Apply(this, tileData.identity.collisionFlags);
            RefreshIsoDepthSortRegistration();
        }

        /// <summary>화면에 그릴 캐릭터 오클루전 display(0~1). target 보간은 Applier가 담당합니다.</summary>
        public void SetCharacterOcclusion(float occlusion01)
        {
            _characterOcclusion = Mathf.Clamp01(occlusion01);
            RefreshBaseVisualState();
        }

        public void SetGhosted(bool ghosted)
        {
            _isGhosted = ghosted;
            RefreshBaseVisualState();
        }

        public void SetSelected(bool selected) => ForceApplySelectedOverlay(selected);

        /// <summary>Add 강조 오버레이. Selected(외곽선)와 독립 축. 0 = off.</summary>
        public void SetVividEmphasis(float amount) => ForceApplyVividEmphasis(amount);

        public void SetDamageStage(int stage)
        {
            stage = Mathf.Clamp(stage, 0, TileDamagePresentationConsts.MaxCrackStage);
            bool supportsCracks = placementSlot == TilePlacementSlot.HorizontalFace ||
                                  (placementSlot == TilePlacementSlot.OccupiedCell &&
                                   TilePrefabDB.TryResolveDefinition(prefabId, out TileDefinition def) &&
                                   MapDigTerrainUtil.IsWalkableStratumBlock(def));
            if (!supportsCracks)
                stage = 0;

            if (_currentDamageStage == stage)
                return;

            _currentDamageStage = stage;
            float cellSize = MapDigColumnHost.Runtime != null
                ? MapDigColumnHost.Runtime.CellSize
                : SafeCellSize;

            if (stage <= 0)
            {
                _crackOverlay?.Apply(0, cellSize);
                return;
            }

            _crackOverlay ??= new TileViewCrackOverlay(transform);
            _crackOverlay.Apply(stage, cellSize);
        }

        public void ConfigureStructuralHidePresentationMode(StructuralHidePresentationMode mode) =>
            _structuralHideMode = mode;

        /// <summary>Applier SSOT — 합성된 표현을 한 경로로 적용합니다.</summary>
        public void ApplyResolvedPresentation(in TilePresentationResolved resolved)
        {
            ApplyStructuralHidden(resolved.StructuralHidden);
            SetSightLineBuildingHidden(resolved.SightLineTrace);

            if (resolved.StructuralHidden)
            {
                SetDamageStage(0);
                return;
            }

            SetGhosted(resolved.Ghosted);
            SetSelected(resolved.Selected);
            SetVividEmphasis(resolved.VividEmphasis);
            SetDamageStage(resolved.DamageStage);
            SetCharacterOcclusion(resolved.CharacterOcclusion);
            RefreshIsoDepthSortRegistration();
        }

        /// <summary>야외 시선 차단 building MinCellY Floor 어둡게 표시.</summary>
        public void SetSightLineBuildingHidden(bool hidden)
        {
            _sightLineBuildingHidden = hidden;
            ApplySightLineBuildingOverlay();
        }

        /// <summary>구조물 가시성 정책에 의한 완전 숨김(스트리밍 despawn 없음).</summary>
        public void SetStructuralVisibilityHidden(bool hidden) => ApplyStructuralHidden(hidden);

        void ApplyStructuralHidden(bool hidden)
        {
            if (_structuralVisibilityHidden == hidden)
                return;

            _structuralVisibilityHidden = hidden;

            if (_structuralHideMode == StructuralHidePresentationMode.DisableGameObject)
            {
                if (hidden)
                {
                    SetBlockedTraceVisible(false);
                    gameObject.SetActive(false);
                    RefreshIsoDepthSortRegistration();
                    return;
                }

                gameObject.SetActive(true);
                _characterOcclusion = 0f;
                ForceApplyBaseState(TileBaseVisualState.Visible);
                ApplySightLineBuildingOverlay();
                RefreshIsoDepthSortRegistration();
                return;
            }

            if (hidden)
            {
                Renderer renderer = _shadeController?.CachedRenderer;
                if (renderer != null)
                    renderer.enabled = false;
                SetBlockedTraceVisible(false);
                RefreshIsoDepthSortRegistration();
                return;
            }

            _characterOcclusion = 0f;
            ForceApplyBaseState(TileBaseVisualState.Visible);
            ApplySightLineBuildingOverlay();
            RefreshIsoDepthSortRegistration();
        }

        private void RefreshBaseVisualState()
        {
            if (_structuralVisibilityHidden)
            {
                if (_structuralHideMode == StructuralHidePresentationMode.DisableGameObject)
                    return;

                Renderer renderer = _shadeController?.CachedRenderer;
                if (renderer != null)
                    renderer.enabled = false;
                SetBlockedTraceVisible(false);
                return;
            }

            TileBaseVisualState next = ResolveBaseState(_characterOcclusion, _isGhosted);

            if (!_baseStateInitialized || _currentBaseState != next)
                ForceApplyBaseState(next);

            if (next == TileBaseVisualState.HiddenByCharacter)
                ApplyCharacterOcclusionDerived();

            ApplySightLineBuildingOverlay();
        }

        private void ApplyWorldPose(in TileData tileData, float cellSize)
        {
            cellSize = Mathf.Max(1e-4f, cellSize);
            if (TileIdentityUtil.IsVerticalFace(tileData.identity))
            {
                WallEdgeKey key = WallEdgeKey.FromWallTileIdentity(tileData.identity);
                WallEdgeKey.GetWorldPose(key, cellSize, out Vector3 pos, out Quaternion rot);
                transform.SetPositionAndRotation(pos, rot);
                return;
            }

            if (TileIdentityUtil.IsHorizontalFace(tileData.identity))
            {
                FloorFaceKey key = FloorFaceKey.FromFloorTileIdentity(tileData.identity);
                FloorFaceKey.GetWorldPose(key, cellSize, out Vector3 pos, out Quaternion rot);
                transform.SetPositionAndRotation(pos, rot);
                return;
            }

            transform.SetPositionAndRotation(
                TileHelper.ConvertGridToWorldPos(tileData.identity.GridPos, cellSize),
                Quaternion.identity);
        }

        // 기본축 상태 결정: 우선순위 Hidden > Ghosted > Visible.
        private static TileBaseVisualState ResolveBaseState(float characterOcclusion, bool isGhosted)
        {
            if (characterOcclusion > OcclusionEpsilon) return TileBaseVisualState.HiddenByCharacter;
            if (isGhosted) return TileBaseVisualState.Ghosted;
            return TileBaseVisualState.Visible;
        }

        private void ForceApplyBaseState(TileBaseVisualState next)
        {
            Renderer renderer = _shadeController?.CachedRenderer;
            switch (next)
            {
                case TileBaseVisualState.Visible:
                    if (renderer != null)
                    {
                        renderer.enabled = true;
                        renderer.shadowCastingMode = _defaultShadowCastingMode;
                    }
                    _shadeController?.SetAdditionalLightEnabled(true);
                    _shadeController?.SetGhost(false);
                    _shadeController?.SetCharacterOcclusion(0f);
                    SetBlockedTraceVisible(false);
                    break;

                case TileBaseVisualState.HiddenByCharacter:
                    if (renderer != null)
                        renderer.enabled = true;
                    _shadeController?.SetGhost(false);
                    break;

                case TileBaseVisualState.Ghosted:
                    if (renderer != null)
                    {
                        renderer.enabled = true;
                        renderer.shadowCastingMode = _defaultShadowCastingMode;
                    }
                    _shadeController?.SetAdditionalLightEnabled(true);
                    _shadeController?.SetGhost(true);
                    _shadeController?.SetCharacterOcclusion(0f);
                    SetBlockedTraceVisible(false);
                    break;
            }

            _currentBaseState = next;
            _baseStateInitialized = true;
            ApplySightLineBuildingOverlay();
            RefreshIsoDepthSortRegistration();
        }

        private void ApplySightLineBuildingOverlay() =>
            _shadeController?.SetSightLineBuildingHidden(_sightLineBuildingHidden);

        private void ForceApplySelectedOverlay(bool next)
        {
            Renderer renderer = _shadeController?.CachedRenderer;
            if (renderer != null && _selectionLayer != null)
            {
                uint mask = renderer.renderingLayerMask;
                uint bit = _selectionLayer.RenderingLayerMask;
                if (next) mask |= bit;
                else mask &= ~bit;
                renderer.renderingLayerMask = mask;
            }

            _currentSelected = next;
            _selectedInitialized = true;
        }

        private void ForceApplyVividEmphasis(float amount)
        {
            _vividEmphasisAmount = Mathf.Clamp01(amount);
            _shadeController?.SetEmphasisAdd(_vividEmphasisAmount);
        }

        private void ApplyCharacterOcclusionDerived()
        {
            Renderer renderer = _shadeController?.CachedRenderer;
            float displayOcclusion = OcclusionBlendMath.PerceptualOcclusion01(_characterOcclusion);
            _shadeController?.SetCharacterOcclusion(displayOcclusion);

            if (renderer != null)
            {
                renderer.shadowCastingMode = displayOcclusion >= ShadowOnlyOcclusionThreshold
                    ? ShadowCastingMode.ShadowsOnly
                    : _defaultShadowCastingMode;
            }

            float additionalLight = 1f - Mathf.SmoothStep(
                AdditionalLightFadeStart, AdditionalLightFadeEnd, _characterOcclusion);
            _shadeController?.SetAdditionalLightBlend(additionalLight);

            SetBlockedTraceVisible(_characterOcclusion >= BlockedTraceOcclusionThreshold);
        }

        private void SetBlockedTraceVisible(bool visible)
        {
            if (_blockedTraceObject == null) return;
            if (_blockedTraceObject.activeSelf == visible) return;
            _blockedTraceObject.SetActive(visible);
        }

        /// <summary>풀 반납 전 시각·오클루전·선택 상태 초기화. Awake는 재호출되지 않음.</summary>
        internal void ResetForPool()
        {
            CacheControllers();
            _characterOcclusion = 0f;
            _isGhosted = false;
            _sightLineBuildingHidden = false;
            _structuralVisibilityHidden = false;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            ForceApplyBaseState(TileBaseVisualState.Visible);
            ForceApplySelectedOverlay(false);
            ForceApplyVividEmphasis(0f);
            SetBlockedTraceVisible(false);
        }
    }
}
