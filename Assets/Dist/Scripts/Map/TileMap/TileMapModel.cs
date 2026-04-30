using UnityEngine;
using System.Collections.Generic;
using System;
namespace IsoTilemap
{
    // ============================================================
    // TileMapModel — 런타임 타일맵 데이터를 보관하고 변경 이벤트를 발행
    // ============================================================
    public class TileMapModel : IMapModel
    {
        public event Action<Vector3Int, IReadOnlyList<TileData>> OnRuntimeDataChanged;
        public event Action<IReadOnlyCollection<Vector3Int>> OnRuntimeBatchChanged;
        public Dictionary<Vector3Int, List<TileData>> tiles = new Dictionary<Vector3Int, List<TileData>>();

        private List<TileData> _cachedList = new List<TileData>();
        private bool _isDirty = false;
        private WallOcclusionFinder _occlusionFinder;

        public void AddTile(Vector3Int pos, TileData tile)
        {
            if (!tiles.ContainsKey(pos))
                tiles[pos] = new List<TileData>();
            tiles[pos].Add(tile);
            _isDirty = true;
        }

        public IReadOnlyList<TileData> TilesSnapshot
        {
            get
            {
                // 데이터가 바뀐 적이 있으면 그때 새로 만듦 (Lazy Update)
                if (_isDirty)
                {
                    _cachedList.Clear();
                    foreach (var list in tiles.Values)
                    {
                        _cachedList.AddRange(list);
                    }
                    _isDirty = false;
                }
                return _cachedList;
            }
        }
        public void SetTile(TileData tileData)
        {
            SetTile(tileData.identity.GridPos, tileData);
        }

        private void SetTile(Vector3Int pos, TileData tileDatas)
        {
            if (!tiles.ContainsKey(pos))
            {
                tiles[pos] = new List<TileData>();
            }
            tiles[pos].Add(tileDatas);
            OnRuntimeDataChanged?.Invoke(pos, tiles[pos]);
        }

        public void Initialize(MapModelDTO prepared)
        {
            foreach (var kv in prepared.TilesData)
            {
                if (!tiles.ContainsKey(kv.identity.GridPos))
                    tiles[kv.identity.GridPos] = new List<TileData>();
                tiles[kv.identity.GridPos].Add(kv);
            }
            _isDirty = true;
            _occlusionFinder = new WallOcclusionFinder(tiles);
        }

        public IReadOnlyList<TileData> GetOccludingWalls(Vector3Int playerCellPos)
        {
            return _occlusionFinder.Find(playerCellPos);
        }

        public bool TryGetTiles(Vector3Int pos, out IReadOnlyList<TileData> tileList)
        {
            if (tiles.TryGetValue(pos, out var list)) { tileList = list; return true; }
            tileList = null;
            return false;
        }

        public void HideOcclusionTileWall(Vector3Int playerCellPos)
        {
            List<TileData> wallsList = _occlusionFinder.Find(playerCellPos);
            for (int i = 0; i < wallsList.Count; i++)
            {
                TileData wall = wallsList[i];
                TileState state = wall.state;
                state.isHiddenCharacter = true;
                TileState tileState = state;
                wall.state = tileState;
                wallsList[i] = wall;
            }
            ApplyTiles(wallsList);
        }

        public void ApplyTiles(IReadOnlyList<TileData> tileList)
        {
            HashSet<Vector3Int> changedCells = new HashSet<Vector3Int>();
            foreach (var tile in tileList)
            {
                var pos = tile.identity.GridPos;
                if (!tiles.TryGetValue(pos, out var existingList)) continue;
                for (int i = 0; i < existingList.Count; i++)
                {
                    if (existingList[i].tileDefId == tile.tileDefId)
                    { existingList[i] = tile; break; }
                }
                changedCells.Add(pos);
            }
            _isDirty = true;
            if (changedCells.Count > 0)
            {
                OnRuntimeBatchChanged?.Invoke(changedCells);
            }
        }
    }

}
