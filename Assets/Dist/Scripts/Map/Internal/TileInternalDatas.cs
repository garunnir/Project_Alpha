using System;
using System.Collections.Generic;
using UnityEngine;
namespace IsoTilemap
{
    public readonly struct TileData
    {
        public readonly Guid tileDefId;
        public readonly TileState state;
        public readonly TileIdentity identity;
    }
    public readonly struct TileState
    {
        public readonly bool isHiddenCharacter;
    }
    public readonly struct TileIdentity
    {
        public readonly string PrefabId;
        public readonly Vector3Int GridPos;
        public readonly Vector3Int sizeUnit;
        public readonly byte tileType;
    }

}
