using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace IsoTilemap
{

    public class TileMapRuntimeData
    {
        public Dictionary<Vector3Int, List<TileData>> tiles = new Dictionary<Vector3Int, List<TileData>>();
    }
    public static class TileHelper
    {
        public static Vector3Int ConvertWorldToGrid(Vector3 worldPos)
        {
            // worldPos에서 그리드 좌표로 변환합니다. 현재 셀 크기 1:1 가정을 사용합니다.
            // 필요하면 cellSize를 파라미터로 추가하여 비율을 적용하세요.
            Vector3 p = worldPos;
            return new Vector3Int(
                Mathf.RoundToInt(p.x - 0.5f),
                Mathf.RoundToInt(p.y),
                Mathf.RoundToInt(p.z - 0.5f)
            );
        }
        public static Vector3 ConvertGridToWorldPos(Vector3Int gridPos, float cellSize = 1f)
        {
            return ConvertGridToWorldPos((Vector3)gridPos, cellSize);
        }
        public static Vector3 ConvertGridToWorldPos(Vector3 worldPos, float cellSize = 1f)
        {
            Vector3 wPos = new Vector3(
            worldPos.x * cellSize + 0.5f * cellSize,
            worldPos.y * cellSize + 0.5f * cellSize,
                worldPos.z * cellSize + 0.5f * cellSize
);
            return wPos;
        }
    }

}
