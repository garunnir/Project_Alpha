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

        private readonly TileEdgeBinder _edgeBinder = new TileEdgeBinder();

        private List<TileData> _cachedList = new List<TileData>();
        private bool _isDirty = false;
        private WallOcclusionFinder _occlusionFinder;

        public ITileEdgeBinderReadOnly EdgeBinder => _edgeBinder;

        public void GatherRenderableTiles(Vector3Int cellPos, List<TileData> buffer)
        {
            buffer.Clear();
            if (tiles.TryGetValue(cellPos, out var list))
                buffer.AddRange(list);
            _edgeBinder.AppendIncidentEdges(cellPos, buffer);
        }

        private void NotifyCell(Vector3Int cell)
        {
            var snapshot = new List<TileData>();
            GatherRenderableTiles(cell, snapshot);
            OnRuntimeDataChanged?.Invoke(cell, snapshot);
        }

        public void AddTile(Vector3Int pos, TileData tile)
        {
            SetTile(tile);
        }

        public IReadOnlyList<TileData> TilesSnapshot
        {
            get
            {
                if (_isDirty)
                {
                    _cachedList.Clear();
                    foreach (var list in tiles.Values)
                        _cachedList.AddRange(list);
                    foreach (var edgeTile in _edgeBinder.EnumerateTiles())
                        _cachedList.Add(edgeTile);
                    _isDirty = false;
                }
                return _cachedList;
            }
        }

        public void SetTile(TileData tileData)
        {
            if ((TileView.TileType)tileData.identity.tileType == TileView.TileType.EdgeWall)
                SetEdgeTile(tileData);
            else
                SetCellTile(tileData);
        }

        private void SetEdgeTile(TileData tileData)
        {
            _edgeBinder.Register(tileData);
            _isDirty = true;

            var key = WallEdgeKey.FromEdgeTileIdentity(tileData.identity);
            NotifyCell(key.Anchor);
            NotifyCell(key.NeighborCell());
        }

        private void SetCellTile(TileData tileData)
        {
            Vector3Int pos = tileData.identity.GridPos;
            if (!tiles.ContainsKey(pos))
                tiles[pos] = new List<TileData>();
            tiles[pos].Add(tileData);
            _isDirty = true;
            NotifyCell(pos);
        }

        public void Initialize(MapModelDTO prepared)
        {
            tiles.Clear();
            _edgeBinder.Clear();

            foreach (var kv in prepared.TilesData)
            {
                if ((TileView.TileType)kv.identity.tileType == TileView.TileType.EdgeWall)
                    _edgeBinder.Register(kv);
                else
                {
                    if (!tiles.ContainsKey(kv.identity.GridPos))
                        tiles[kv.identity.GridPos] = new List<TileData>();
                    tiles[kv.identity.GridPos].Add(kv);
                }
            }

            _isDirty = true;
            _occlusionFinder = new WallOcclusionFinder(tiles, _edgeBinder.EdgeIndex);
        }

        public IReadOnlyList<TileData> GetOccludingWalls(Vector3Int playerCellPos)
        {
            _occlusionFinder ??= new WallOcclusionFinder(tiles, _edgeBinder.EdgeIndex);
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
            _occlusionFinder ??= new WallOcclusionFinder(tiles, _edgeBinder.EdgeIndex);
            OcclusionSelection batch = _occlusionFinder.FindOcclusion(playerCellPos);
            var list = batch.Occluding;
            for (int i = 0; i < list.Count; i++)
            {
                TileData wall = list[i];
                TileState state = wall.state;
                state.isHiddenCharacter = true;
                wall.state = state;
                list[i] = wall;
            }
            ApplyTiles(list);
        }

        public void ApplyTiles(IReadOnlyList<TileData> tileList)
        {
            HashSet<Vector3Int> changedCells = new HashSet<Vector3Int>();
            foreach (var tile in tileList)
            {
                if ((TileView.TileType)tile.identity.tileType == TileView.TileType.EdgeWall)
                {
                    if (!_edgeBinder.TryReplaceTileData(tile))
                        continue;
                    var key = WallEdgeKey.FromEdgeTileIdentity(tile.identity);
                    changedCells.Add(key.Anchor);
                    changedCells.Add(key.NeighborCell());
                    continue;
                }

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
