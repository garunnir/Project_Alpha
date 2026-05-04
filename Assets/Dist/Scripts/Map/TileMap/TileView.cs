using UnityEngine;

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

        private void Awake()
        {
            CacheControllers();
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

        internal void UpdateTile(TileData tileData)
        {
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
            _shadeController?.SetAdditionalLightEnabled(!tileData.state.isHiddenCharacter);
        }
    }
}
