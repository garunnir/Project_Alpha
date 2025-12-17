using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
namespace IsoTilemap
{
    // 타일맵 시각화 담당 클래스
    [RequireComponent(typeof(TileMapRuntime))]
    public class TileMapVisualizer : MonoBehaviour
    {
        [Header("Prefab DB for loading")]
        public TilePrefabDB prefabDB;

        [Header("Grid / World Settings")]
        public float cellSize = 1f;

        private TileMapRuntime _tileMapRuntime;
        // 그리드 셀 월드 크기
        // Start is called once before the first execution of Update after the MonoBehaviour is created

        public event Action<Dictionary<Vector3Int, List<TileData>>> TileMapBuilded;
        void Awake()
        {
            _tileMapRuntime = GetComponent<TileMapRuntime>();
        }

        public void BuildVisualFromData(TileMapData data)
        {
            // 기존 타일들 정리할지 말지 선택 (여기선 다 지우는 예시)
            ClearExistingTiles();
            Dictionary<Vector3Int, List<TileData>> runtimeInfos = new Dictionary<Vector3Int, List<TileData>>();
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

                if (runtimeInfos.ContainsKey(gridPos))
                {
                    runtimeInfos[gridPos].Add(new TileData { tileInfo = info });
                }
                else
                {
                    runtimeInfos.Add(gridPos, new List<TileData>() { new TileData { tileInfo = info } });
                }
                // 필요하면, 멀티타일용으로 콜라이더/메시 사이즈 조정 로직 추가
                // e.g. info.ApplyGridToWorld(cellSize);
            }
            TileMapBuilded?.Invoke(runtimeInfos);
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

        Dictionary<Vector3Int, TileState> states = new();
        HashSet<Vector3Int> dirty = new();

        //public ref TileState GetOrCreate(Vector3Int cell) { /* ... */ }

        public void MarkDirty(Vector3Int cell) => dirty.Add(cell);

        public void FlushDirty(TileViewUpdater view)
        {
            foreach (var cell in dirty)
                RefreshCell(cell, states[cell]); // 그 셀만 갱신
            dirty.Clear();
        }
        void Update()
        {
            
        }
        private void RefreshCell(Vector3Int cellPos, TileState state)
        {
            // 해당 셀만 갱신
        }
        public void UpdateCell(Vector3Int cellPos, TileState state)
        {
            states[cellPos] = state;
            MarkDirty(cellPos);
        }
    }
    public class TileData
    {
        public TileInfo tileInfo;
        public TileState state;
    }
    public class TileState
    {
        public bool isHiddenCharacter = false;
    }

}
