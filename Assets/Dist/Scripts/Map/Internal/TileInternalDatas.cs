using System;
using System.Collections.Generic;
using UnityEngine;
namespace IsoTilemap
{
    public readonly struct TileData
    {
        public Guid tileDefId{init; get;}
        public TileState state{init; get;}
        public TileIdentity identity{init; get;}
    }
    public readonly struct TileState
    {
        public readonly bool isHiddenCharacter;
    }
    public readonly struct TileIdentity
    {
        public string PrefabId{init; get;}
        public Vector3Int GridPos{init; get;}
        public Vector3Int sizeUnit{init; get;}
        public byte tileType{init; get;}
    }

}
