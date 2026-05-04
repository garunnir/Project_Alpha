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
            if (info.tileType == TileView.TileType.EdgeWall)
            {
                byte ef = tileData.identity.edgeFace;
                info.wallEdgeFace = ef == TileIdentity.EdgeFaceNone
                    ? (byte)0
                    : (byte)Mathf.Clamp(ef, 0, 1);
            }
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

            Vector3 worldPos;
            Quaternion rotation = Quaternion.identity;
            if ((TileView.TileType)tileData.identity.tileType == TileView.TileType.EdgeWall)
            {
                var wk = WallEdgeKey.FromEdgeTileIdentity(tileData.identity);
                WallEdgeKey.GetWorldPose(wk, cellSize, out worldPos, out rotation);
            }
            else
            {
                worldPos = TileHelper.ConvertGridToWorldPos(tileData.identity.GridPos, cellSize);
            }

            var tileGo = GameObject.Instantiate(prefab, worldPos, rotation, _targetTransform);
            var view = InitializeTileInfo(tileGo, tileData);
            view.UpdateTile(tileData);
            return view;
        }
    }
}
