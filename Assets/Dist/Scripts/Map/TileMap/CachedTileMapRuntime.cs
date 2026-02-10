using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace IsoTilemap
{
    public class CachedTileMapRuntime : IMapRuntime
    {
        private readonly IMapRuntime _runtimeData;
        #nullable enable
        private List<TileData>? _cachedtiles = null;
        private HashSet<Vector3Int>? _cachedCurrentRoomID = null;
        #nullable disable

        public event Action<Vector3Int, List<TileData>> OnRuntimeDataChanged;

        public CachedTileMapRuntime(IMapRuntime runtime)
        {
            _runtimeData = runtime;
        }

        public IReadOnlyList<TileData> GetOccludingWalls(Vector3Int playerCellPos)
        {
            if (_cachedtiles != null && _cachedCurrentRoomID != null && _cachedCurrentRoomID.Contains(playerCellPos))
            {
                return _cachedtiles;
            }
            IReadOnlyList<TileData> resultTiles = _runtimeData.GetOccludingWalls(playerCellPos);
            IEnumerable<Vector3Int> visited = resultTiles.Select(x => x.identity.GridPos);
            _cachedCurrentRoomID = visited.ToHashSet();
            _cachedtiles = resultTiles.ToList();
            return resultTiles;
        }
        public void ClearCache()
        {
            _cachedtiles = null;
            _cachedCurrentRoomID = null;
        }
    }
}
