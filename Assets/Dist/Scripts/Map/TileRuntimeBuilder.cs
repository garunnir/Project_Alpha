using UnityEngine;
using System.Collections.Generic;

namespace IsoTilemap
{
public class TileRuntimeBuilder : IMapRuntimeBuilder
{
    public IMapRuntime Build(IMapModelReadOnly prepared)
    {
        TileMapRuntime runtime = new TileMapRuntime(prepared);
        return runtime;
    }
}
public record MapRuntimeInitData
    {
        IReadOnlyDictionary<Vector3Int, List<TileData>> tiles;
    }
}