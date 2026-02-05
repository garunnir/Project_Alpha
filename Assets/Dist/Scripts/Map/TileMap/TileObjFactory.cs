namespace IsoTilemap
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    public class TileObjFactory
    {
        private readonly TilePrefabDB _prefabDB;
        private readonly Transform _targetTransform;

        public TileObjFactory(Transform rootTransform, TilePrefabDB prefabDB)
        {
            _prefabDB = prefabDB;
            _targetTransform = rootTransform;
        }

        /// <summary>
        /// 타일 데이터에서 게임 오브젝트를 생성하고 인스턴스화합니다.
        /// </summary>
        public Dictionary<Guid, TileView> SpawnTiles(IEnumerable<TileData> tiles)
        {
            var spawnedTiles = new Dictionary<Guid, TileView>();
            foreach (var tile in tiles)
            {
                var spawnedTile = SpawnTile(tile);
                if (spawnedTile != null)
                {
                    spawnedTiles.Add(tile.tileDefId, spawnedTile);
                }
            }
            return spawnedTiles;
        }

        /// <summary>
        /// 타일 게임 오브젝트의 TileView 컴포넌트를 초기화하고 추적 사전에 등록합니다.
        /// </summary>
        private TileView InitializeTileInfo(GameObject tileGo, TileData tileData)
        {

            var info = tileGo.GetComponent<TileView>();
            if (info == null)
            {
                info = tileGo.AddComponent<TileView>();
            }

            info.gridPos = tileData.identity.GridPos;
            info.size = tileData.identity.sizeUnit;
            info.prefabId = tileData.identity.PrefabId;
            info.tileType = (TileView.TileType)tileData.identity.tileType;
            return info;
        }

        public TileView SpawnTile(TileData tileData, float cellSize = 1f)
        {
            GameObject prefab = _prefabDB?.GetPrefab(tileData.identity.PrefabId);

            if (prefab == null)
            {
                Debug.LogWarning($"No prefab for id: {tileData.identity.PrefabId}");
                return null;
            }

            Vector3 worldPos = TileHelper.ConvertGridToWorldPos(tileData.identity.GridPos, cellSize);
            var tileGo = GameObject.Instantiate(prefab, worldPos, Quaternion.identity, _targetTransform);

            return InitializeTileInfo(tileGo, tileData);
        }
    }
}
