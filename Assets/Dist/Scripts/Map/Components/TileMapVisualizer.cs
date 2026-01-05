
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace IsoTilemap
{
    // 타일맵 시각화 담당 클래스
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TileMapContext))]   
    public class TileMapVisualizer : MonoBehaviour,IMapViewBuilder
    {
        [Header("Prefab DB for loading")]
        public TilePrefabDB prefabDB;

        [Header("Grid / World Settings")]
        public float cellSize = 1f;

        // 타일 정의 인스턴스 매핑
        private Dictionary<Guid,TileInfo> _tileDefInstance = new Dictionary<Guid,TileInfo>();
        // 그리드 셀 월드 크기
        // Start is called once before the first execution of Update after the MonoBehaviour is created

        public void Build(IMapModelReadOnly modelData)
        {
            IEnumerable<TileCellSnapshot> data= modelData.Tiles();
            if(data == null || data.Count() == 0)
            {
                Debug.LogWarning("No tile data to build visual.");
                return;
            }
            // 기존 타일들 정리할지 말지 선택 (여기선 다 지우는 예시)
            ClearExistingTiles();
            foreach (var td in data)
            {
                Vector3Int key = td.Position;
                IReadOnlyList<TileData> tileist = td.Tiles;
                foreach(var ti in tileist)
                {
                    GameObject prefab = prefabDB != null ? prefabDB.GetPrefab(ti.identity.PrefabId) : null;

                    //Debug.Log($"Spawning tile at {key} with prefabId {ti.identity.PrefabId}");
                    if (prefab == null)
                    {
                        Debug.LogWarning($"No prefab for id: {ti.identity.PrefabId}");
                        continue;
                    }

                    // Anchor 기준 월드 좌표
                    Vector3Int gridPos = key;
                    Vector3 worldPos = TileHelper.ConvertGridToWorldPos(gridPos, cellSize);

                    var go = Instantiate(prefab, worldPos, Quaternion.identity, this.transform);

                    var info = go.GetComponent<TileInfo>();
                    if (info == null)
                    {
                        info = go.AddComponent<TileInfo>();
                    }
                    ti.tileDefId = Guid.NewGuid();

                    _tileDefInstance.Add(ti.tileDefId, info);
                    info.gridPos = ti.identity.GridPos;
                    info.size = ti.identity.sizeUnit;
                    info.prefabId = ti.identity.PrefabId;
                    info.tileType = (TileInfo.TileType)ti.identity.tileType;

                    // 필요하면, 멀티타일용으로 콜라이더/메시 사이즈 조정 로직 추가
                    // e.g. info.ApplyGridToWorld(cellSize);
                }

            }
        }
        

    }
    public class TileData
    {
        public Guid tileDefId;
        public TileState state;
        public TileIdentity identity;
    }
    public class TileState
    {
        public bool isHiddenCharacter = false;
    }
    public class TileIdentity
    {
        public string PrefabId;
        public Vector3Int GridPos;
        public Vector3Int sizeUnit;
        public byte tileType;
    }

}
