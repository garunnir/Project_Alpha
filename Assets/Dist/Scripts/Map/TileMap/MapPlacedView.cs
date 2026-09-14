using UnityEngine;

// ============================================================
// MapPlacedView — 그리드에 배치된 씬 오브젝트의 공통 anchor·pose·기즈모
// ============================================================
// TileView(구조 타일)와 LiquidAuthoringView(물 저작 마커)의 공통 부모.
// 프레젠테이션·충돌 계약은 파생 클래스가 각자 가지며, 이 클래스는 어느 쪽도 갖지 않는다.
namespace IsoTilemap
{
    public abstract class MapPlacedView : MonoBehaviour
    {
        [Header("Grid Anchor Position (xyz)")]
        [Tooltip("OccupiedCell=점유 셀. HorizontalFace=walkable(서는 층, pivot과 동일).")]
        public Vector3Int gridPos;

        [Header("Size in Grid Units")]
        public Vector3Int size = Vector3Int.one; // 1x1x1, 2x1x1 등 (x,y,z 방향)

        [Header("Prefab Identity")]
        public string prefabId;             // 어떤 프리팹/타입인지 식별용

        [Header("Gizmo (Grid) Settings")]
        [Tooltip("기즈모에서 사용할 셀 크기: 그리드 단위 1의 월드 길이입니다.")]
        public float gizmoCellSize = 1f;
        [Tooltip("기즈모 그리드 선을 그릴지 여부")]
        public bool drawGizmoGrid = true;
        public Color gizmoGridColor = new Color(0f, 0.7f, 0.9f, 0.6f);

        protected float SafeCellSize => Mathf.Max(0.0001f, gizmoCellSize);

        /// <summary>배치 규칙(점유 셀 / 면 슬롯)에 맞춰 gridPos와 transform을 정렬합니다.</summary>
        protected abstract void ApplyEditorPose();

#if UNITY_EDITOR
        /// <summary>씬 Transform 조작 종료 시 가장 가까운 그리드/면으로 스냅합니다.</summary>
        public void SnapEditorPoseToGrid()
        {
            ApplyEditorPose();
            OnEditorPoseSnapped();
            EditorPoseSnapped?.Invoke(this);
        }

        /// <summary>prefabId·pose를 채운 뒤 그리드 스냅. Authoring live sync용.</summary>
        public void EnsureEditorPlacementReady()
        {
            if (string.IsNullOrEmpty(prefabId))
                TryResolvePrefabIdFromSource();
            SnapEditorPoseToGrid();
        }

        /// <summary>MapPlacedView 그리드 스냅 직후 (씬 편집).</summary>
        public static event System.Action<MapPlacedView> EditorPoseSnapped;

        protected virtual void OnEditorPoseSnapped() { }
#endif

        protected virtual void OnValidate() => ApplyEditorPose();

        protected virtual void Reset()
        {
            gridPos = TileHelper.ConvertWorldToGrid(transform.position, SafeCellSize);
            TryResolvePrefabIdFromSource();
        }

        /// <summary>하이어라키 인스턴스 → 원본 프리팹 에셋 이름으로 prefabId를 채웁니다.</summary>
        protected void TryResolvePrefabIdFromSource()
        {
#if UNITY_EDITOR
            GameObject source = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            if (source == null)
                return;

            prefabId = UnityEditor.Tile.PrefabDBExtensions.GetTilePrefabName(source);
#endif
        }

        // 선택된 오브젝트에서 기즈모로 권장 그리드 라인을 표시합니다.
        // - Anchor(그리드 좌표) 기준으로 X/Z 평면의 셀 경계선을 그리고,
        // - 높이(size.y)에 맞춘 와이어 박스를 함께 표시합니다.
        protected virtual void OnDrawGizmosSelected()
        {
            if (!drawGizmoGrid)
                return;

            TileHelper.DrawOccupiedCellWire(gridPos, SafeCellSize, gizmoGridColor, size);
        }
    }
}
