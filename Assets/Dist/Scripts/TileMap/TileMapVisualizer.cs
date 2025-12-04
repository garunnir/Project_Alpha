using UnityEngine;
namespace IsoTilemap
{
    public class TileMapVisualizer : MonoBehaviour
    {
        [Header("Prefab DB for loading")]
        public TilePrefabDB prefabDB;

        [Header("Grid / World Settings")]
        public float cellSize = 1f;                 
        // 그리드 셀 월드 크기
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {

        }


        public void BuildVisualFromData(TileMapData data)
        {
            // 기존 타일들 정리할지 말지 선택 (여기선 다 지우는 예시)
            ClearExistingTiles();

            foreach (var td in data.tiles)
            {
                GameObject prefab = prefabDB != null ? prefabDB.GetPrefab(td.prefabId) : null;

                if (prefab == null)
                {
                    Debug.LogWarning($"No prefab for id: {td.prefabId}");
                    continue;
                }

                // Anchor 기준 월드 좌표
                Vector3Int gridPos = new Vector3Int(td.x, td.y, td.z);
                Vector3 worldPos = TileHelper.ConvertGridToWorldPos(gridPos, cellSize);

                var go = Instantiate(prefab, worldPos, Quaternion.identity, this.transform);

                var info = go.GetComponent<TileInfo>();
                if (info == null)
                {
                    info = go.AddComponent<TileInfo>();
                }

                info.gridPos = gridPos;
                info.size = new Vector3Int(td.sizeX, td.sizeY, td.sizeZ);
                info.prefabId = td.prefabId;
                info.tileType = (TileInfo.TileType)td.tileType;

                // 필요하면, 멀티타일용으로 콜라이더/메시 사이즈 조정 로직 추가
                // e.g. info.ApplyGridToWorld(cellSize);
            }
        }
        void ClearExistingTiles()
        {
            // FindObjectsByType: include inactive so we match the previous behavior of FindObjectsOfType(true).
            var tileInfos = UnityEngine.Object.FindObjectsByType<TileInfo>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);

            // 타일만 날린다고 가정 (이 스크립트가 붙은 오브젝트는 남김)
            foreach (var info in tileInfos)
            {
                if (info != null)
                {
                    // 본인 자신(TileMapSerializer의 GameObject) 밑에 있는지만 보고 날릴 수도 있음
                    DestroyImmediate(info.gameObject);
                }
            }

        }
            void OnCellChanged(Vector3Int cellPos)
    {
        // 해당 셀만 갱신
    }
    }

}
