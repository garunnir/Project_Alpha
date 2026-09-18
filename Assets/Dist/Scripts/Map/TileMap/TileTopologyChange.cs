using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// One model edit (or a coalesced batch): original removals, final additions, and
    /// every affected cell. Replacements retain both the old and new footprint.
    /// </summary>
    public sealed class TileTopologyChange
    {
        readonly Dictionary<Guid, TileData> _added = new();
        readonly Dictionary<Guid, TileData> _removed = new();
        public HashSet<Vector3Int> ChangedCells { get; } = new();
        public IEnumerable<TileData> AddedTiles => _added.Values;
        public IEnumerable<TileData> RemovedTiles => _removed.Values;

        internal bool TryGetRemovedTile(Guid id, out TileData tile) => _removed.TryGetValue(id, out tile);

        public void Add(in TileData tile)
        {
            _added[tile.tileDefId] = tile;
            TileIdentityUtil.CollectAffectedCells(tile.identity, ChangedCells);
        }

        public void Remove(in TileData tile)
        {
            // An addition removed again within this batch never entered its input state.
            if (!_added.Remove(tile.tileDefId) && !_removed.ContainsKey(tile.tileDefId))
                _removed.Add(tile.tileDefId, tile);
            TileIdentityUtil.CollectAffectedCells(tile.identity, ChangedCells);
        }

        public void Merge(TileTopologyChange next)
        {
            foreach (var tile in next.RemovedTiles)
                Remove(tile);
            foreach (var tile in next.AddedTiles)
                Add(tile);
            ChangedCells.UnionWith(next.ChangedCells);
        }
    }
}
