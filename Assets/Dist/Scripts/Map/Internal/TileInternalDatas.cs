using System;
using System.Collections.Generic;
using UnityEngine;
namespace IsoTilemap
{
    public struct TileData
    {
        public Guid tileDefId{init; get;}
        public TileState state;
        public TileIdentity identity{init; get;}
    }
    public struct TileState
    {
        public bool isHiddenCharacter;
    }
    public readonly struct TileIdentity
    {
        /// <summary>255이면 칸 타일. 0=+X 면, 1=+Z 면(앵커 GridPos 기준, WallFace와 동일).</summary>
        public const byte EdgeFaceNone = 255;

        public string PrefabId{init; get;}
        public Vector3Int GridPos{init; get;}
        public Vector3Int sizeUnit{init; get;}
        public byte tileType{init; get;}
        public byte edgeFace{init; get;}
    }

}
