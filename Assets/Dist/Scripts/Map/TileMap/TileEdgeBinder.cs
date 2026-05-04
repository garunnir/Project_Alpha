using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// 그리드 셀 데이터와 분리된 면 벽(EdgeWall) 레지스트리.
    /// 물리적으로 같은 변은 하나의 키(<see cref="WallEdgeKey"/>)로만 보관되며, 인접 두 셀이 동일 타일 데이터를 참조합니다.
    /// </summary>
    public sealed class TileEdgeBinder : ITileEdgeBinderReadOnly
    {
        private readonly Dictionary<WallEdgeKey, TileData> _edges = new Dictionary<WallEdgeKey, TileData>();
        private readonly Dictionary<Vector3Int, List<WallEdgeKey>> _cellToEdgeKeys = new Dictionary<Vector3Int, List<WallEdgeKey>>();

        public IReadOnlyDictionary<WallEdgeKey, TileData> EdgeIndex => _edges;

        public void Clear()
        {
            _edges.Clear();
            _cellToEdgeKeys.Clear();
        }

        /// <summary>동일 키가 있으면 교체합니다. 인접 셀 인덱스를 유지합니다.</summary>
        public void Register(in TileData tile)
        {
            var key = WallEdgeKey.FromEdgeTileIdentity(tile.identity);
            if (_edges.ContainsKey(key))
                RemoveInternal(key);

            _edges[key] = tile;
            AddIncident(key);
        }

        public bool TryReplaceTileData(in TileData tile)
        {
            WallEdgeKey? found = null;
            foreach (var kv in _edges)
            {
                if (kv.Value.tileDefId != tile.tileDefId)
                    continue;
                found = kv.Key;
                break;
            }

            if (found == null)
                return false;

            RemoveInternal(found.Value);
            Register(tile);
            return true;
        }

        public void AppendIncidentEdges(Vector3Int cell, List<TileData> appendTo)
        {
            if (!_cellToEdgeKeys.TryGetValue(cell, out var keys))
                return;

            for (int i = 0; i < keys.Count; i++)
            {
                if (_edges.TryGetValue(keys[i], out var td))
                    appendTo.Add(td);
            }
        }

        public IEnumerable<TileData> EnumerateTiles()
        {
            foreach (var kv in _edges)
                yield return kv.Value;
        }

        private void AddIncident(in WallEdgeKey key)
        {
            TouchCell(key.Anchor, key);
            TouchCell(key.NeighborCell(), key);
        }

        private void TouchCell(Vector3Int cell, in WallEdgeKey key)
        {
            if (!_cellToEdgeKeys.TryGetValue(cell, out var list))
            {
                list = new List<WallEdgeKey>(2);
                _cellToEdgeKeys[cell] = list;
            }

            if (!list.Contains(key))
                list.Add(key);
        }

        private void RemoveInternal(in WallEdgeKey key)
        {
            DetachCell(key.Anchor, key);
            DetachCell(key.NeighborCell(), key);
            _edges.Remove(key);
        }

        private void DetachCell(Vector3Int cell, in WallEdgeKey key)
        {
            if (!_cellToEdgeKeys.TryGetValue(cell, out var list))
                return;
            list.Remove(key);
            if (list.Count == 0)
                _cellToEdgeKeys.Remove(cell);
        }
    }
}
