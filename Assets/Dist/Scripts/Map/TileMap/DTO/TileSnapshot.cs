using System;
using System.Collections.Generic;
using UnityEngine;
namespace IsoTilemap
{
public sealed class TileCellSnapshot
{
    public Vector3Int Position { get; }
    public IReadOnlyList<TileData> Tiles { get; }

    public TileCellSnapshot(Vector3Int position, IReadOnlyList<TileData> tiles)
    {
        Position = position;
        Tiles = tiles;
    }
}
}