// ============================================================
// MapTopologyCapsuleMargin — topology 발 위치에 캡슐 반경(미터) 마진 적용
// ============================================================

using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// 그리드 셀 막힘은 발 셀 기준이라, 캡슐 반경만큼 벽 메시에 시각적으로 파고드는 것을 막습니다.
    /// </summary>
    public static class MapTopologyCapsuleMargin
    {
        public static Vector3 ClampHorizontal(
            IMapTopologyQuery query,
            Vector3 feetWorld,
            int gridY,
            Vector3Int footprint,
            float capsuleRadius,
            float skin)
        {
            if (query == null || capsuleRadius <= 0f)
                return feetWorld;

            float cellSize = query.CellSize > 0f ? query.CellSize : 1f;
            float margin = capsuleRadius + Mathf.Max(0f, skin);
            footprint = CharacterGridFootprintDefaults.Clamp(footprint);

            Vector3Int feetCell = TileHelper.ConvertWorldToGrid(feetWorld, cellSize);
            feetCell.y = gridY;
            if (!CharacterOccupiedCellUtil.TryGetAnchorFromFeet(feetCell, footprint, out Vector3Int anchor))
                return feetWorld;

            float minX = float.NegativeInfinity;
            float maxX = float.PositiveInfinity;
            float minZ = float.NegativeInfinity;
            float maxZ = float.PositiveInfinity;

            int sx = footprint.x;
            int sz = footprint.z;
            for (int dx = 0; dx < sx; dx++)
            {
                for (int dz = 0; dz < sz; dz++)
                {
                    var cell = new Vector3Int(anchor.x + dx, gridY, anchor.z + dz);
                    ApplyEastLimit(query, cell, cellSize, margin, ref maxX);
                    ApplyWestLimit(query, cell, cellSize, margin, ref minX);
                    ApplyNorthLimit(query, cell, cellSize, margin, ref maxZ);
                    ApplySouthLimit(query, cell, cellSize, margin, ref minZ);
                }
            }

            float x = feetWorld.x;
            float z = feetWorld.z;
            if (maxX < float.PositiveInfinity)
                x = Mathf.Min(x, maxX);
            if (minX > float.NegativeInfinity)
                x = Mathf.Max(x, minX);
            if (maxZ < float.PositiveInfinity)
                z = Mathf.Min(z, maxZ);
            if (minZ > float.NegativeInfinity)
                z = Mathf.Max(z, minZ);

            return new Vector3(x, feetWorld.y, z);
        }

        static void ApplyEastLimit(
            IMapTopologyQuery query,
            Vector3Int cell,
            float cellSize,
            float margin,
            ref float maxX)
        {
            var east = new Vector3Int(cell.x + 1, cell.y, cell.z);
            if (query.CellHasSolidWall(east.x, east.z, cell.y))
            {
                MergeMin(ref maxX, (cell.x + 1) * cellSize - margin);
                return;
            }

            if (query.EdgeBlocksPassage(cell, east))
            {
                MergeMin(
                    ref maxX,
                    TileHelper.GetAdjacentCellFaceMidpoint(cell, east, cellSize).x - margin);
            }
        }

        static void ApplyWestLimit(
            IMapTopologyQuery query,
            Vector3Int cell,
            float cellSize,
            float margin,
            ref float minX)
        {
            var west = new Vector3Int(cell.x - 1, cell.y, cell.z);
            if (query.CellHasSolidWall(west.x, west.z, cell.y))
            {
                MergeMax(ref minX, cell.x * cellSize + margin);
                return;
            }

            if (query.EdgeBlocksPassage(west, cell))
            {
                MergeMax(
                    ref minX,
                    TileHelper.GetAdjacentCellFaceMidpoint(west, cell, cellSize).x + margin);
            }
        }

        static void ApplyNorthLimit(
            IMapTopologyQuery query,
            Vector3Int cell,
            float cellSize,
            float margin,
            ref float maxZ)
        {
            var north = new Vector3Int(cell.x, cell.y, cell.z + 1);
            if (query.CellHasSolidWall(north.x, north.z, cell.y))
            {
                MergeMin(ref maxZ, (cell.z + 1) * cellSize - margin);
                return;
            }

            if (query.EdgeBlocksPassage(cell, north))
            {
                MergeMin(
                    ref maxZ,
                    TileHelper.GetAdjacentCellFaceMidpoint(cell, north, cellSize).z - margin);
            }
        }

        static void ApplySouthLimit(
            IMapTopologyQuery query,
            Vector3Int cell,
            float cellSize,
            float margin,
            ref float minZ)
        {
            var south = new Vector3Int(cell.x, cell.y, cell.z - 1);
            if (query.CellHasSolidWall(south.x, south.z, cell.y))
            {
                MergeMax(ref minZ, cell.z * cellSize + margin);
                return;
            }

            if (query.EdgeBlocksPassage(south, cell))
            {
                MergeMax(
                    ref minZ,
                    TileHelper.GetAdjacentCellFaceMidpoint(south, cell, cellSize).z + margin);
            }
        }

        static void MergeMin(ref float limit, float candidate)
        {
            limit = limit == float.PositiveInfinity
                ? candidate
                : Mathf.Min(limit, candidate);
        }

        static void MergeMax(ref float limit, float candidate)
        {
            limit = limit == float.NegativeInfinity
                ? candidate
                : Mathf.Max(limit, candidate);
        }
    }
}
