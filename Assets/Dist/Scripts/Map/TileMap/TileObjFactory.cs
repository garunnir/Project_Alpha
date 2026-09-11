namespace IsoTilemap
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    // ============================================================
    // TileObjFactory — 타일 프리팹 스폰·디스폰 + Building/Outdoor 부모
    // ============================================================
    public class TileObjFactory
    {
        private readonly TilePrefabDB _prefabDB;
        private readonly Transform _targetTransform;
        private readonly TileViewPoolRegistry _pool;
        private readonly BuildingViewHierarchy _hierarchy;

        public bool UsePooling => _pool != null;

        public BuildingViewHierarchy Hierarchy => _hierarchy;

        public TileObjFactory(
            Transform rootTransform,
            TilePrefabDB prefabDB,
            TileViewPoolRegistry pool = null,
            BuildingViewHierarchy hierarchy = null)
        {
            _prefabDB = prefabDB;
            _targetTransform = rootTransform;
            _pool = pool;
            _hierarchy = hierarchy ?? BuildingViewHierarchy.EnsureUnder(rootTransform, 1f);
        }

        public Dictionary<Guid, TileView> SpawnTiles(IEnumerable<TileData> tiles, float cellSize = 1f)
        {
            _hierarchy?.SetCellSize(cellSize);
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

            _hierarchy?.SetCellSize(cellSize);
            view.UpdateTile(tileData, cellSize);
            SyncTileParent(view, tileData);
            return view;
        }

        /// <summary>rebake/merge 후 buildingId 변경 시 Outdoor/Building 재부모.</summary>
        public void SyncTileParent(TileView view, in TileData tileData)
        {
            _hierarchy?.AttachTile(view, tileData);
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

            var tileGo = TilePrefabSpawnUtil.Instantiate(
                prefab,
                _targetTransform,
                Vector3.zero,
                Quaternion.identity);
            if (tileGo == null)
                return null;

            TileView view = tileGo.GetComponent<TileView>();
            if (view == null)
                view = tileGo.AddComponent<TileView>();

            return view;
        }

        public void DespawnTile(TileView view)
        {
            if (view == null)
                return;

            // 풀/Destroy 전 Building 부모에서 분리 — 청크 GO 부모 불필요, 타일 단위 despawn.
            _hierarchy?.DetachTile(view);

            if (_pool != null)
                _pool.Release(view);
            else
                UnityEngine.Object.Destroy(view.gameObject);
        }
    }
}
