using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    [Serializable]
    public class TileMapData
    {
        public List<TileData> tiles = new List<TileData>();
    }
    // 한 타일(Anchor 기준)의 순수 데이터 구조
    [Serializable]
    public class TileData
    {
        public int x;
        public int y;
        public int z;

        public int sizeX;
        public int sizeY;
        public int sizeZ;

        public string prefabId;
        public byte tileType;
    }
    public class TileMapRuntimeData
    {
        public Dictionary<Vector3Int, List<TileInfo>> tiles = new Dictionary<Vector3Int, List<TileInfo>>();
    }

}
