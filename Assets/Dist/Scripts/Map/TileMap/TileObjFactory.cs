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

        public Dictionary<Guid, TileView> SpawnTiles(IEnumerable<TileData> tiles, float cellSize = 1f)
        {
            var spawnedTiles = new Dictionary<Guid, TileView>();
            foreach (var tile in tiles)
            {
                var spawnedTile = SpawnTile(tile, cellSize);
                if (spawnedTile != null)
                {
                    spawnedTiles.Add(tile.tileDefId, spawnedTile);
                }
            }
            return spawnedTiles;
        }

        public TileView SpawnTile(TileData tileData, float cellSize = 1f)
        {
            GameObject prefab = _prefabDB?.GetPrefab(tileData.identity.PrefabId);

            if (prefab == null)
            {
                Debug.LogWarning($"No prefab for id: {tileData.identity.PrefabId}");
                return null;
            }

            var tileGo = GameObject.Instantiate(prefab, Vector3.zero, Quaternion.identity, _targetTransform);
            TileView view = tileGo.GetComponent<TileView>();
            if (view == null)
                view = tileGo.AddComponent<TileView>();

            view.UpdateTile(tileData, cellSize);
            return view;
        }
    }
}
