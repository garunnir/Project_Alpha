
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// 타일맵 시각화 담당 클래스
    /// 모델 데이터를 게임 월드에 3D 타일로 인스턴스화합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TileMapContext))]
    public class TileMapVisualizer : MonoBehaviour, IMapViewBuilder
    {
        [Header("Prefab DB for loading")]
        public TilePrefabDB prefabDB;

        [Header("Grid / World Settings")]
        public float cellSize = 1f;

        private Dictionary<Guid, TileInfo> _tileInstances = new Dictionary<Guid, TileInfo>();

        /// <summary>
        /// 모델 데이터를 기반으로 타일맵을 구축합니다.
        /// </summary>
        public void Build(IMapModelReadOnly modelData)
        {
            var tiles = modelData.Tiles();
            if (tiles == null || tiles.Count() == 0)
            {
                Debug.LogWarning("No tile data to build visual.");
                return;
            }

            ClearExistingTiles();
            SpawnTiles(tiles);
        }

        /// <summary>
        /// 타일 데이터에서 게임 오브젝트를 생성하고 인스턴스화합니다.
        /// </summary>
        private void SpawnTiles(IEnumerable<TileData> tiles)
        {
            foreach (var tile in tiles)
            {
                GameObject prefab = prefabDB?.GetPrefab(tile.identity.PrefabId);

                if (prefab == null)
                {
                    Debug.LogWarning($"No prefab for id: {tile.identity.PrefabId}");
                    continue;
                }

                Vector3 worldPos = TileHelper.ConvertGridToWorldPos(tile.identity.GridPos, cellSize);
                var tileGo = Instantiate(prefab, worldPos, Quaternion.identity, transform);

                InitializeTileInfo(tileGo, tile);
            }
        }

        /// <summary>
        /// 타일 게임 오브젝트의 TileInfo 컴포넌트를 초기화하고 추적 사전에 등록합니다.
        /// </summary>
        private void InitializeTileInfo(GameObject tileGo, TileData tileData)
        {
            var info = tileGo.GetComponent<TileInfo>();
            if (info == null)
            {
                info = tileGo.AddComponent<TileInfo>();
            }

            info.gridPos = tileData.identity.GridPos;
            info.size = tileData.identity.sizeUnit;
            info.prefabId = tileData.identity.PrefabId;
            info.tileType = (TileInfo.TileType)tileData.identity.tileType;

            _tileInstances.Add(tileData.tileDefId, info);
        }

        /// <summary>
        /// 기존 타일들을 정리합니다.
        /// </summary>
        private void ClearExistingTiles()
        {
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            _tileInstances.Clear();
        }

        /// <summary>
        /// ID로 타일 인스턴스를 조회합니다.
        /// </summary>
        public bool TryGetTile(Guid tileId, out TileInfo tileInfo)
        {
            return _tileInstances.TryGetValue(tileId, out tileInfo);
        }
    }
 
}
