using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace IsoTilemap
{
    public class CachedTileMapRuntime : IMapModel
    {
        private readonly IMapModel _runtimeData;
        #nullable enable
        private List<TileData>? _cachedtiles = null;
        private HashSet<Vector3Int>? _cachedCurrentRoomID = null;

        public CachedTileMapRuntime(IMapModel runtimeData)
        {
            _runtimeData = runtimeData;
        }

        public IReadOnlyList<TileData> TilesSnapshot => _runtimeData.TilesSnapshot;
#nullable disable


        public event Action<Vector3Int, IReadOnlyList<TileData>> OnRuntimeDataChanged
        {
            add => _runtimeData.OnRuntimeDataChanged += value;
            remove => _runtimeData.OnRuntimeDataChanged -= value;
        }
        public event Action<IReadOnlyCollection<Vector3Int>> OnRuntimeBatchChanged
        {
            add => _runtimeData.OnRuntimeBatchChanged += value;
            remove => _runtimeData.OnRuntimeBatchChanged -= value;
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

        public bool TryGetTiles(Vector3Int pos, out IReadOnlyList<TileData> tileList)
        {
            return _runtimeData.TryGetTiles(pos, out tileList);
        }
        public void Initialize(MapModelDTO prepared)
        {
            _runtimeData.Initialize(prepared);
        }

        public void SetTile(TileData tileData)
        {
            _runtimeData.SetTile(tileData);
        }
        public void ApplyTiles(IReadOnlyList<TileData> tiles)
        {
            _runtimeData.ApplyTiles(tiles);
        }

        public void HideOcclusionTileWall(Vector3Int playerCellPos)
        {
            _runtimeData.HideOcclusionTileWall(playerCellPos);
        }
    }
}
