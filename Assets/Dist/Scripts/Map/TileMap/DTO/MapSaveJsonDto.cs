using System;
using System.Collections.Generic;
using UnityEngine;
namespace IsoTilemap
{
    [Serializable]
    public class MapSaveJsonDto
    {
        /// <summary>그리드 1칸 월드 길이. 0 이하·누락(구 JSON)이면 로더 fallback 사용.</summary>
        public float gridCellSize = 1f;

        public List<TileSaveData> tiles = new List<TileSaveData>();
        public List<WallEdgeSaveData> wallEdges = new List<WallEdgeSaveData>();
    }
}