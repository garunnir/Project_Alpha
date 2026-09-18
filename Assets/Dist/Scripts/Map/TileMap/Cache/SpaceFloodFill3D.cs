// ============================================================
// SpaceFloodFill3D — building AABB 안 전방향 volume Space flood
// ============================================================
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    public static class SpaceFloodFill3D
    {
        static readonly Vector3Int[] VolumeDirs =
        {
            Vector3Int.right,
            Vector3Int.back,
            Vector3Int.left,
            Vector3Int.forward,
            Vector3Int.up,
            Vector3Int.down
        };

        const int SafetyLimit = 200000;

        /// <summary>
        /// seed에서 AABB(<see cref="BuildingExtent"/>) 안만 6방 volume flood.
        /// 빈 칸 포함; terrain/plaza·구조 solid·타 building floor는 방문하지 않음.
        /// </summary>
        public static SpaceFloodResult Run(
            FloorMapIndex index,
            SpaceRegistry registry,
            BuildingExtent extent,
            BuildingGroupRegistry buildings,
            Vector3Int seedCell,
            int buildingId)
        {
            if (index == null || registry == null || buildings == null ||
                !BuildingIdBakeRules.CanPropagateBuildingIdFrom(buildingId) ||
                !extent.HasBounds ||
                extent.BuildingId != buildingId)
            {
                return SpaceFloodResult.Empty;
            }

            if (!CanVisit(index, buildings, extent, buildingId, seedCell))
                return SpaceFloodResult.Empty;

            if (registry.TryGetSpaceAtFloorCell(seedCell, out _))
                return SpaceFloodResult.Empty;

            var visited = new HashSet<Vector3Int> { seedCell };
            var boundarySpaceIds = new HashSet<int>();
            var q = new Queue<Vector3Int>();
            q.Enqueue(seedCell);

            int steps = 0;
            while (q.Count > 0)
            {
                if (++steps > SafetyLimit)
                    break;

                Vector3Int cur = q.Dequeue();
                for (int i = 0; i < VolumeDirs.Length; i++)
                {
                    Vector3Int neighbor = cur + VolumeDirs[i];
                    TryExpand(
                        index,
                        registry,
                        buildings,
                        extent,
                        buildingId,
                        cur,
                        neighbor,
                        visited,
                        boundarySpaceIds,
                        q);
                }
            }

            return new SpaceFloodResult(visited, boundarySpaceIds);
        }

        static void TryExpand(
            FloorMapIndex index,
            SpaceRegistry registry,
            BuildingGroupRegistry buildings,
            BuildingExtent extent,
            int buildingId,
            Vector3Int cur,
            Vector3Int neighbor,
            HashSet<Vector3Int> visited,
            HashSet<int> boundarySpaceIds,
            Queue<Vector3Int> q)
        {
            if (!extent.ContainsAabb(neighbor.x, neighbor.y, neighbor.z))
                return;

            if (IsTerrainOrPlazaCell(index, buildings, neighbor))
                return;

            if (IsPassageBlocked(index, cur, neighbor))
                return;

            if (IsSolidStructuralCell(index, neighbor))
                return;

            if (HasForeignBuildingFloor(index, neighbor, buildingId))
                return;

            if (registry.TryGetSpaceAtFloorCell(neighbor, out int existingId))
            {
                boundarySpaceIds.Add(existingId);
                return;
            }

            if (!visited.Add(neighbor))
                return;

            q.Enqueue(neighbor);
        }

        static bool CanVisit(
            FloorMapIndex index,
            BuildingGroupRegistry buildings,
            BuildingExtent extent,
            int buildingId,
            Vector3Int cell)
        {
            if (!extent.ContainsAabb(cell.x, cell.y, cell.z))
                return false;

            if (IsTerrainOrPlazaCell(index, buildings, cell))
                return false;

            if (IsSolidStructuralCell(index, cell))
                return false;

            if (HasForeignBuildingFloor(index, cell, buildingId))
                return false;

            return true;
        }

        static bool IsTerrainOrPlazaCell(
            FloorMapIndex index,
            BuildingGroupRegistry buildings,
            Vector3Int cell)
        {
            if (buildings.IsPlazaFloor(cell.y, cell.x, cell.z))
                return true;

            if (!index.TryGetFloorFaceForWalkableCell(cell.x, cell.y, cell.z, out var face))
                return false;

            return face.identity.buildingId == TileIdentity.BuildingIdTerrain;
        }

        static bool HasForeignBuildingFloor(FloorMapIndex index, Vector3Int cell, int buildingId)
        {
            if (!index.TryGetFloorFaceForWalkableCell(cell.x, cell.y, cell.z, out var face))
                return false;

            int other = face.identity.buildingId;
            return BuildingIdBakeRules.CanPropagateBuildingIdFrom(other) && other != buildingId;
        }

        static bool IsSolidStructuralCell(FloorMapIndex index, Vector3Int cell)
        {
            if (!index.TryGetCellTiles(cell.x, cell.z, cell.y, out var list) || list == null || list.Count == 0)
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                var id = list[i].identity;
                // HorizontalFace/VerticalFace는 셀에 incident로 잡힐 수 있으나 volume을 채우지 않음
                if (!TileIdentityUtil.IsOccupiedCell(id))
                    continue;

                if (TileIdentityUtil.IsStructural(id))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 측면은 structural thin wall. 수직(±Y)은 두 셀 사이 logical floor face가 있으면 차단.
        /// walkable Y의 floor = CellBelow↔CellAbove 면 → 아래 칸에서 위 칸으로의 volume 통과를 막음.
        /// </summary>
        static bool IsPassageBlocked(FloorMapIndex index, Vector3Int from, Vector3Int to)
        {
            if (from.y == to.y)
                return LateralStructuralEdgeSeals(index, from, to);

            if (from.x != to.x || from.z != to.z)
                return false;

            int dy = to.y - from.y;
            if (dy == 1)
                return index.CellHasFloor(to.x, to.y, to.z);
            if (dy == -1)
                return index.CellHasFloor(from.x, from.y, from.z);

            return false;
        }

        static bool LateralStructuralEdgeSeals(FloorMapIndex index, Vector3Int cellA, Vector3Int cellB)
        {
            if (!index.TryGetEdgeSealingLateral(cellA, cellB, out var edge))
                return false;

            return TileIdentityUtil.IsStructural(edge.identity);
        }
    }
}
