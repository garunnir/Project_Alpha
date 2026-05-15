using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>모델 스냅샷·런타임 증분 갱신용 청크↔셀·타일 Guid 인덱스.</summary>
    public sealed class TileMapChunkIndex
    {
        private readonly Dictionary<Vector2Int, List<Vector3Int>> _cellsByChunk = new();
        private readonly Dictionary<Guid, Vector2Int> _primaryChunkByTile = new();

        public void Build(IMapModelReadOnly model, int chunkSize)
        {
            _cellsByChunk.Clear();
            _primaryChunkByTile.Clear();
            chunkSize = Mathf.Max(1, chunkSize);

            IReadOnlyList<TileData> snapshot = model.TilesSnapshot;
            if (snapshot == null)
                return;

            for (int i = 0; i < snapshot.Count; i++)
                RegisterTile(snapshot[i], chunkSize);
        }

        public void RegisterTile(TileData tile, int chunkSize)
        {
            chunkSize = Mathf.Max(1, chunkSize);
            var type = (TileView.TileType)tile.identity.tileType;
            if (type == TileView.TileType.EdgeWall)
            {
                WallEdgeKey key = WallEdgeKey.FromEdgeTileIdentity(tile.identity);
                Vector2Int anchorChunk = TileChunkCoord.FromCell(key.Anchor, chunkSize);
                _primaryChunkByTile[tile.tileDefId] = anchorChunk;
                AddCell(anchorChunk, key.Anchor);
                AddCell(TileChunkCoord.FromCell(key.NeighborCell(), chunkSize), key.NeighborCell());
                return;
            }

            Vector3Int cell = tile.identity.GridPos;
            Vector2Int chunk = TileChunkCoord.FromCell(cell, chunkSize);
            _primaryChunkByTile[tile.tileDefId] = chunk;
            AddCell(chunk, cell);
        }

        public void UnregisterTile(TileData tile, int chunkSize)
        {
            chunkSize = Mathf.Max(1, chunkSize);
            if (!_primaryChunkByTile.Remove(tile.tileDefId))
                return;

            var type = (TileView.TileType)tile.identity.tileType;
            if (type == TileView.TileType.EdgeWall)
            {
                WallEdgeKey key = WallEdgeKey.FromEdgeTileIdentity(tile.identity);
                RemoveCell(TileChunkCoord.FromCell(key.Anchor, chunkSize), key.Anchor);
                RemoveCell(TileChunkCoord.FromCell(key.NeighborCell(), chunkSize), key.NeighborCell());
                return;
            }

            Vector3Int cell = tile.identity.GridPos;
            RemoveCell(TileChunkCoord.FromCell(cell, chunkSize), cell);
        }

        public IReadOnlyList<Vector3Int> GetCellsInChunk(Vector2Int chunk)
        {
            if (_cellsByChunk.TryGetValue(chunk, out var cells))
                return cells;

            return Array.Empty<Vector3Int>();
        }

        public bool TryGetChunkForTile(Guid tileId, out Vector2Int chunk) =>
            _primaryChunkByTile.TryGetValue(tileId, out chunk);

        private void AddCell(Vector2Int chunk, Vector3Int cell)
        {
            if (!_cellsByChunk.TryGetValue(chunk, out var cells))
            {
                cells = new List<Vector3Int>();
                _cellsByChunk[chunk] = cells;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] == cell)
                    return;
            }

            cells.Add(cell);
        }

        private void RemoveCell(Vector2Int chunk, Vector3Int cell)
        {
            if (!_cellsByChunk.TryGetValue(chunk, out var cells))
                return;

            for (int i = cells.Count - 1; i >= 0; i--)
            {
                if (cells[i] != cell)
                    continue;

                cells.RemoveAt(i);
                break;
            }

            if (cells.Count == 0)
                _cellsByChunk.Remove(chunk);
        }
    }
}
