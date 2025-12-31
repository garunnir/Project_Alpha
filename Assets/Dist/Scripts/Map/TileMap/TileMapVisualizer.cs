
using System;
using System.Collections.Generic;
using UnityEngine;
namespace IsoTilemap
{
    // 타일맵 시각화 담당 클래스
    [RequireComponent(typeof(TileMapDomainData))]
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

        public event Action<Dictionary<Vector3Int, List<TileData>>> TileMapBuilded;

        public void BuildVisualFromData(IMapDomainReadOnly domainData)
        {
            var data= domainData.GetRuntimeData();
            if(data.tiles == null || data.tiles.Count == 0)
            {
                Debug.LogWarning("No tile data to build visual.");
                return;
            }
            // 기존 타일들 정리할지 말지 선택 (여기선 다 지우는 예시)
            ClearExistingTiles();
            foreach (var td in data.tiles)
            {
                Vector3Int key = td.Key;
                List<TileData> tileist = td.Value;
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
            TileMapBuilded?.Invoke(data.tiles);
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

        HashSet<Vector3Int> dirty = new();

        //public ref TileState GetOrCreate(Vector3Int cell) { /* ... */ }

        public void MarkDirty(Vector3Int cell) => dirty.Add(cell);

        public void FlushDirty()
        {
            foreach (var cell in dirty)
                RefreshCell(cell); // 그 셀만 갱신
            dirty.Clear();
        }
        void Update()
        {
            FlushDirty();
        }
        private void RefreshCell(Vector3Int cellPos)
        {
            // 해당 셀만 갱신
            List<TileData> datas=_do.GetRuntimeData().tiles[cellPos];
            foreach (var data in datas)
            {
                RefreshObj(data);
            }
        }
        private void RefreshObj(TileData obj)
        {
            TileInfo info = _tileDefInstance[obj.tileDefId];
            info.gameObject.SetActive(!obj.state.isHiddenCharacter);
        }
        public void UpdateCell(Vector3Int cellPos, TileData state)
        {
            MarkDirty(cellPos);
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
