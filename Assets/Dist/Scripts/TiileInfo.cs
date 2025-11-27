using UnityEngine;

// 씬에 실제로 붙어있는 타일 오브젝트용
// Anchor + Size + PrefabId 기반
namespace IsoTilemap
{
    public class TileInfo : MonoBehaviour
    {
        public enum TileType
        {
            none=0,
            Floor=1,
            Wall=2,
            Obstacle=3
        }
        [Header("Grid Anchor Position (xyz)")]
        public Vector3 gridPos;          // gx, gy, gz

        [Header("Tile Size in Grid Units")]
        public Vector3Int size = Vector3Int.one; // 1x1x1, 2x1x1 등 (x,y,z 방향)

        [Header("Prefab Identity")]
        public string prefabId;             // 어떤 프리팹/타입인지 식별용

        [Header("Tile Type")]
        public TileType tileType = TileType.none;

        private void Reset()
        {
            gridPos = transform.position;
            // 하이어라키의 인스턴스 → 원본 프리팹 오브젝트
#if UNITY_EDITOR
            var source = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            Debug.Log(UnityEditor.Tile.PrefabDBExtensions.GetTilePrefabName(source));
            prefabId = UnityEditor.Tile.PrefabDBExtensions.GetTilePrefabName(source);
#endif
        }
        // 그리드 → 월드 변환 (지금은 1:1이라 가정, 필요하면 수정)

        public void ApplyGridToWorld(float cellSize = 1f)
        {
            var p = new Vector3(
                gridPos.x * cellSize,
                gridPos.y * cellSize,
                gridPos.z * cellSize
            );
            transform.position = p;
        }

        // 이미 배치된 오브젝트에서 월드→그리드 역변환 (원하면 사용)
        public void CaptureGridFromWorld(float cellSize = 1f)
        {
            var p = transform.position / cellSize;
            gridPos = new Vector3Int(
                Mathf.RoundToInt(p.x),
                Mathf.RoundToInt(p.y),
                Mathf.RoundToInt(p.z)
            );
        }
    }
}