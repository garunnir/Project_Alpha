using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace IsoTilemap
{
    public static class TileHelper
    {
        /// <summary>
        /// <see cref="ConvertGridToWorldPos(Vector3,float)"/>의 역변환과 맞춘 월드→그리드 변환입니다.
        /// </summary>
        public static Vector3Int ConvertWorldToGrid(Vector3 worldPos, float cellSize)
        {
            if (cellSize <= 0f) cellSize = 1f;
            return new Vector3Int(
                Mathf.RoundToInt(worldPos.x / cellSize - 0.5f),
                Mathf.RoundToInt(worldPos.y / cellSize),
                Mathf.RoundToInt(worldPos.z / cellSize - 0.5f)
            );
        }

        public static Vector3Int ConvertWorldToGrid(Vector3 worldPos) =>
            ConvertWorldToGrid(worldPos, 1f);
        public static Vector3 ConvertGridToWorldPos(Vector3Int gridPos, float cellSize = 1f)
        {
            return ConvertGridToWorldPos((Vector3)gridPos, cellSize);
        }
        public static Vector3 ConvertGridToWorldPos(Vector3 worldPos, float cellSize = 1f)
        {
            Vector3 wPos = new Vector3(
            worldPos.x * cellSize + 0.5f * cellSize,
            worldPos.y * cellSize,
                worldPos.z * cellSize + 0.5f * cellSize
);
            return wPos;
        }
    }

}
