using UnityEngine;
using System.Collections.Generic;

namespace IsoTilemap
{
public sealed class TileRuntimeBuilder : IMapRuntimeBuilder
{
    public IMapRuntime Build(IMapModelReadOnly model)
    {
        var tiles = new Dictionary<Vector3Int, IReadOnlyList<TileData>>();

        foreach (var pos in model.Positions)
        {
            if (model.TryGetTiles(pos, out var tileList))
            {
                // 공유 전제: tileList는 수정되지 않는다는 팀 규율/설계가 필요
                tiles[pos] = tileList;
            }
        }

        return new TileMapRuntime(new MapRuntimeInitData(tiles));
    }
}
public record MapRuntimeInitData
    {
        public IReadOnlyDictionary<Vector3Int, IReadOnlyList<TileData>> tiles;
        public MapRuntimeInitData(IReadOnlyDictionary<Vector3Int, IReadOnlyList<TileData>> tiles)
        {
            this.tiles = tiles;
        }
    }
}