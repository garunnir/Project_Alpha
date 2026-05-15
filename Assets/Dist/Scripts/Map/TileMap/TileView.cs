using UnityEngine;
using UnityEngine.Rendering;

// 씬에 실제로 붙어있는 타일 오브젝트용 View.
// Anchor + Size + PrefabId 기반 메타데이터를 유지하고,
// 런타임 데이터 변경을 시각 상태(셰이더 컨트롤)까지 반영합니다.
namespace IsoTilemap
{
    public class TileView : MonoBehaviour
    {
        public enum TileType
        {
            none = 0,
            Floor = 1,
            Wall = 2,
            Obstacle = 3,
            /// <summary>JSON wallEdges 승격. GridPos=앵커 셀, TileIdentity.edgeFace=면.</summary>
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
        [Header("Grid Anchor Position (xyz)")]
        public Vector3Int gridPos;          // gx, gy, gz

        [Header("Tile Size in Grid Units")]
        public Vector3Int size = Vector3Int.one; // 1x1x1, 2x1x1 등 (x,y,z 방향)

        [Header("Prefab Identity")]
        public string prefabId;             // 어떤 프리팹/타입인지 식별용

        [Header("Tile Type")]
        public TileType tileType = TileType.none;

        /// <summary>EdgeWall일 때 JSON wallEdges의 face(0=+X, 1=+Z). 에디터 저장 시 사용.</summary>
        [Range(0, 1)] public byte wallEdgeFace;

        [Header("Gizmo (Grid) Settings")]
        [Tooltip("기즈모에서 사용할 셀 크기: 그리드 단위 1의 월드 길이입니다.")]
        public float gizmoCellSize = 1f;
        [Tooltip("기즈모 그리드 선을 그릴지 여부")]
        public bool drawGizmoGrid = true;
        public Color gizmoGridColor = new Color(0f, 0.7f, 0.9f, 0.6f);
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
        private bool _currentSelected;
        private bool _baseStateInitialized;
        private bool _selectedInitialized;

        private const float OcclusionEpsilon = 1e-4f;
        private const float ShadowOnlyOcclusionThreshold = 0.99f;
        private const float BlockedTraceOcclusionThreshold = 0.4f;
        private const float AdditionalLightCutoffOcclusionThreshold = 0.55f;

        private void Awake()
        {
            CacheControllers();
            ForceApplyBaseState(TileBaseVisualState.Visible);
            ForceApplySelectedOverlay(false);
            SetBlockedTraceVisible(false);
        }

        private void Reset()
        {
            float cs = Mathf.Max(0.0001f, gizmoCellSize);
            gridPos = TileHelper.ConvertWorldToGrid(transform.position, cs);
            CacheControllers();
            // 하이어라키의 인스턴스 → 원본 프리팹 오브젝트
#if UNITY_EDITOR
            var source = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            Debug.Log(UnityEditor.Tile.PrefabDBExtensions.GetTilePrefabName(source));
            prefabId = UnityEditor.Tile.PrefabDBExtensions.GetTilePrefabName(source);
#endif
        }

        private void OnValidate()
        {
            CacheControllers();
            float cs = Mathf.Max(0.0001f, gizmoCellSize);
            if (tileType == TileType.EdgeWall)
            {
                if (WallEdgePicker.TryPickNearest(transform.position, cs, out var nearest))
                {
                    gridPos = nearest.Anchor;
                    wallEdgeFace = (byte)nearest.Face;
                }

                WallEdgeKey key = new WallEdgeKey(gridPos, (WallFace)Mathf.Clamp(wallEdgeFace, 0, 1));
                WallEdgeKey.GetWorldPose(key, cs, out Vector3 edgePos, out Quaternion edgeRot);
                transform.SetPositionAndRotation(edgePos, edgeRot);
            }
            else
            {
                gridPos = TileHelper.ConvertWorldToGrid(transform.position, cs);
                transform.position = TileHelper.ConvertGridToWorldPos(gridPos, cs);
            }
        }

        private void CacheControllers()
        {
            _shadeController ??= GetComponentInChildren<ShadeObjectController>();
            Renderer renderer = _shadeController?.CachedRenderer;
            if (renderer != null)
            {
                _defaultShadowCastingMode = renderer.shadowCastingMode;
            }
        }
        // 선택된 오브젝트에서 기즈모로 권장 그리드 라인을 표시합니다.
        // - Anchor(그리드 좌표) 기준으로 X/Z 평면의 셀 경계선을 그리고,
        // - 높이(size.y)에 맞춘 와이어 박스를 함께 표시합니다.
        // 각 타일의 권장규격을 표현하는 기즈모입니다.
        private void OnDrawGizmosSelected()
        {
            if (!drawGizmoGrid) return;

            // 셀 크기
            float cs = Mathf.Max(0.0001f, gizmoCellSize);

            Vector3 anchor = transform.position - new Vector3(0.5f, 0f, 0.5f);

            int sx = Mathf.Max(1, size.x);
            int sy = Mathf.Max(1, size.y);
            int sz = Mathf.Max(1, size.z);

            Gizmos.color = gizmoGridColor;

            // 높이에 따른 수직선 및 와이어 박스
            Vector3 boxCenter = anchor + new Vector3((sx * cs) * 0.5f, (sy * cs) * 0.5f, (sz * cs) * 0.5f);
            Vector3 boxSize = new Vector3(sx * cs, sy * cs, sz * cs);
            Gizmos.DrawWireCube(boxCenter, boxSize);

            // 모서리에서 위로 올라가는 수직선 (시각적 강조)
            Vector3[] corners = new Vector3[4]
            {
                anchor + new Vector3(0f, 0f, 0f),
                anchor + new Vector3(sx * cs, 0f, 0f),
                anchor + new Vector3(sx * cs, 0f, sz * cs),
                anchor + new Vector3(0f, 0f, sz * cs)
            };
        }

        internal void UpdateTile(TileData tileData, float cellSize)
        {
            ApplyWorldPose(tileData, cellSize);

            tileType = (TileType)tileData.identity.tileType;
            prefabId = tileData.identity.PrefabId;
            gridPos = tileData.identity.GridPos;
            size = tileData.identity.sizeUnit;
            if (tileType == TileType.EdgeWall)
            {
                byte ef = tileData.identity.edgeFace;
                wallEdgeFace = ef == TileIdentity.EdgeFaceNone
                    ? (byte)0
                    : (byte)Mathf.Clamp(ef, 0, 1);
            }

            _characterOcclusion = tileData.state.characterOcclusion;
            TileBaseVisualState resolved = ResolveBaseState(tileData.state);

            if (!_baseStateInitialized || _currentBaseState != resolved)
                ForceApplyBaseState(resolved);

            ApplyCharacterOcclusionPresentation(resolved);

            ApplySelectedOverlay(tileData.state.isSelected);
        }

        private void ApplyWorldPose(in TileData tileData, float cellSize)
        {
            cellSize = Mathf.Max(1e-4f, cellSize);
            var type = (TileType)tileData.identity.tileType;
            if (type == TileType.EdgeWall)
            {
                WallEdgeKey key = WallEdgeKey.FromEdgeTileIdentity(tileData.identity);
                WallEdgeKey.GetWorldPose(key, cellSize, out Vector3 pos, out Quaternion rot);
                transform.SetPositionAndRotation(pos, rot);
                return;
            }

            transform.SetPositionAndRotation(
                TileHelper.ConvertGridToWorldPos(tileData.identity.GridPos, cellSize),
                Quaternion.identity);
        }

        // 기본축 상태 결정: 우선순위 Hidden > Ghosted > Visible.
        private static TileBaseVisualState ResolveBaseState(TileState state)
        {
            if (state.characterOcclusion > OcclusionEpsilon) return TileBaseVisualState.HiddenByCharacter;
            if (state.isGhosted) return TileBaseVisualState.Ghosted;
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
        }

        private void ApplySelectedOverlay(bool next)
        {
            if (_selectedInitialized && _currentSelected == next) return;
            ForceApplySelectedOverlay(next);
        }

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

        private void ApplyCharacterOcclusionPresentation(TileBaseVisualState visualState)
        {
            if (visualState != TileBaseVisualState.HiddenByCharacter)
                return;

            Renderer renderer = _shadeController?.CachedRenderer;
            _shadeController?.SetCharacterOcclusion(_characterOcclusion);

            if (renderer != null)
            {
                renderer.shadowCastingMode = _characterOcclusion >= ShadowOnlyOcclusionThreshold
                    ? ShadowCastingMode.ShadowsOnly
                    : _defaultShadowCastingMode;
            }

            _shadeController?.SetAdditionalLightEnabled(
                _characterOcclusion < AdditionalLightCutoffOcclusionThreshold);

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
            ForceApplyBaseState(TileBaseVisualState.Visible);
            ForceApplySelectedOverlay(false);
            SetBlockedTraceVisible(false);
        }
    }
}
