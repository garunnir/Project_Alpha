using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
namespace IsoTilemap
{
    public sealed class TileMapModel : IMapModel
    {
        private readonly IMapTilesReadOnly _prepared;
        public TileMapModel(IMapTilesReadOnly prepared) => _prepared = prepared;

        public IEnumerable<Vector3Int> Positions => _prepared.Positions;

        public IReadOnlyList<TileData> Tiles()=>_prepared.Tiles();

        public bool TryGetTiles(Vector3Int pos, out IReadOnlyList<TileData> tiles)
            => _prepared.TryGetTiles(pos, out tiles);
    }
}