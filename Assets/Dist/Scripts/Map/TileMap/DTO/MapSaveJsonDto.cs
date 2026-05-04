using System;
using System.Collections.Generic;
using UnityEngine;
namespace IsoTilemap
{
    [Serializable]
    public class MapSaveJsonDto
    {
        public List<TileSaveData> tiles = new List<TileSaveData>();
        public List<WallEdgeSaveData> wallEdges = new List<WallEdgeSaveData>();
    }
}