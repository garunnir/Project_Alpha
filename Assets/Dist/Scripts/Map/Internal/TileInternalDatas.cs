using System;
using System.Collections.Generic;
using UnityEngine;
namespace IsoTilemap
{
       public struct TileData
    {
        public Guid tileDefId;
        public TileState state;
        public TileIdentity identity;
    }
    public struct TileState
    {
        public bool isHiddenCharacter;
    }
    public struct TileIdentity
    {
        public string PrefabId;
        public Vector3Int GridPos;
        public Vector3Int sizeUnit;
        public byte tileType;
    }

}