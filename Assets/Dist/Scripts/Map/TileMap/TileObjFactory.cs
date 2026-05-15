namespace IsoTilemap
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    public class TileObjFactory
    {
        private readonly TilePrefabDB _prefabDB;
        private readonly Transform _targetTransform;
        private readonly TileViewPoolRegistry _pool;

        public bool UsePooling => _pool != null;

        public TileObjFactory(Transform rootTransform, TilePrefabDB prefabDB, TileViewPoolRegistry pool = null)
        {
            _prefabDB = prefabDB;
            _targetTransform = rootTransform;
            _pool = pool;
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
            string prefabId = tileData.identity.PrefabId;
            TileView view = Get(prefabId);
            if (view == null)
                return null;

            view.UpdateTile(tileData, cellSize);
            return view;
        }

        private TileView Get(string prefabId)
        {
            if (_pool != null)
                return _pool.Get(prefabId);

            GameObject prefab = _prefabDB?.GetPrefab(prefabId);
            if (prefab == null)
            {
                Debug.LogWarning($"No prefab for id: {prefabId}");
                return null;
            }

            var tileGo = GameObject.Instantiate(prefab, Vector3.zero, Quaternion.identity, _targetTransform);
            TileView view = tileGo.GetComponent<TileView>();
            if (view == null)
                view = tileGo.AddComponent<TileView>();

            return view;
        }

        public void DespawnTile(TileView view)
        {
            if (view == null)
                return;

            if (_pool != null)
                _pool.Release(view);
            else
                UnityEngine.Object.Destroy(view.gameObject);
        }
    }
}
