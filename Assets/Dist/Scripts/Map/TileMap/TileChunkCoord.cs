using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>그리드 셀 ↔ XZ 청크 좌표 변환.</summary>
    public static class TileChunkCoord
    {
        public static Vector2Int FromCell(Vector3Int cell, int chunkSize)
        {
            chunkSize = Mathf.Max(1, chunkSize);
            return new Vector2Int(FloorDiv(cell.x, chunkSize), FloorDiv(cell.z, chunkSize));
        }

        public static void GetCellRange(
            Vector2Int chunk,
            int chunkSize,
            out Vector3Int min,
            out Vector3Int max)
        {
            chunkSize = Mathf.Max(1, chunkSize);
            int minX = chunk.x * chunkSize;
            int minZ = chunk.y * chunkSize;
            min = new Vector3Int(minX, int.MinValue / 2, minZ);
            max = new Vector3Int(minX + chunkSize - 1, int.MaxValue / 2, minZ + chunkSize - 1);
        }

        public static void AppendChunkNeighborhood(
            HashSet<Vector2Int> chunks,
            Vector2Int center,
            int radius)
        {
            radius = Mathf.Max(0, radius);
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                    chunks.Add(new Vector2Int(center.x + dx, center.y + dz));
            }
        }

        private static int FloorDiv(int value, int divisor)
        {
            if (value >= 0)
                return value / divisor;

            return (value - divisor + 1) / divisor;
        }
    }
}
