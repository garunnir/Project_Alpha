// ============================================================
// SpaceLeakEvaluator — Space bake 후 topology 누수 → isOutdoor
// leak seal: footprint·outdoor·structural/floor topology — buildingId 동일성 미사용
// 천장 = walkable 셀에서 위로의 volume 통과가 막혔는가 (SpaceFloodFill3D와 동일)
// collisionFlags leak 금지 — TILEMAP_BUILDING_BAKE.md 대전제 §1·§7.3
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    public static class SpaceLeakEvaluator
    {
        static readonly Vector3Int[] CardinalDirs =
        {
            Vector3Int.right, Vector3Int.back, Vector3Int.left, Vector3Int.forward
        };

        public static bool Evaluate(
            IReadOnlyCollection<Vector3Int> floorCells,
            int buildingId,
            BuildingExtent extent,
            FloorMapIndex index)
        {
            if (!TryPrepareLeakEvaluation(floorCells, buildingId, extent, index))
                return true;

            return EvaluateCeilingLeak(floorCells, extent, index) ||
                   EvaluateLateralLeak(floorCells, extent, index);
        }

        /// <summary>디버그·진단용. leak 분리 (topology만).</summary>
        public static void EvaluateComponents(
            IReadOnlyCollection<Vector3Int> floorCells,
            int buildingId,
            BuildingExtent extent,
            FloorMapIndex index,
            out bool ceilingLeak,
            out bool lateralLeak)
        {
            ceilingLeak = false;
            lateralLeak = false;
            if (!TryPrepareLeakEvaluation(floorCells, buildingId, extent, index))
            {
                ceilingLeak = true;
                lateralLeak = true;
                return;
            }

            ceilingLeak = EvaluateCeilingLeak(floorCells, extent, index);
            lateralLeak = EvaluateLateralLeak(floorCells, extent, index);
        }

        static bool TryPrepareLeakEvaluation(
            IReadOnlyCollection<Vector3Int> floorCells,
            int buildingId,
            BuildingExtent extent,
            FloorMapIndex index) =>
            floorCells != null && floorCells.Count > 0 && index != null && extent.HasBounds &&
            BuildingIdBakeRules.CanPropagateBuildingIdFrom(buildingId);

        static bool EvaluateCeilingLeak(
            IReadOnlyCollection<Vector3Int> floorCells,
            BuildingExtent extent,
            FloorMapIndex index)
        {
            BuildColumnMaxY(floorCells, out var columnMaxY);

            foreach (var kv in columnMaxY)
            {
                if (ColumnHasCeilingLeak(index, kv.Key.x, kv.Key.z, kv.Value))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 이 컬럼(x,z)에서 floorY보다 높은 곳에 seal(walkable floor 또는 structural OccupiedCell)이
        /// 하나도 없으면 leak. <see cref="FloorMapIndex.TryGetColumnSealTopY"/> O(1) 조회 — buildingId 무관.
        /// </summary>
        static bool ColumnHasCeilingLeak(FloorMapIndex index, int x, int z, int floorY) =>
            !index.TryGetColumnSealTopY(x, z, out int topY) || topY <= floorY;

        static bool EvaluateLateralLeak(
            IReadOnlyCollection<Vector3Int> floorCells,
            BuildingExtent extent,
            FloorMapIndex index)
        {
            foreach (var cell in floorCells)
            {
                if (IsLateralLeakAtCell(cell, extent, index))
                    return true;
            }

            return false;
        }

        static bool IsLateralLeakAtCell(Vector3Int cell, BuildingExtent extent, FloorMapIndex index)
        {
            int cellY = cell.y;
            foreach (var d in CardinalDirs)
            {
                int nx = cell.x + d.x;
                int nz = cell.z + d.z;
                // volume space: AABB 밖이 개방 경계 (floor footprint만으로 단정하지 않음)
                if (extent.ContainsAabb(nx, cellY, nz))
                    continue;

                var neighbor = new Vector3Int(nx, cellY, nz);
                if (TryDescribeLateralLeakReason(cell, neighbor, extent, index, out _))
                    return true;
            }

            return false;
        }

        static bool LateralEdgeSeals(FloorMapIndex index, Vector3Int cellA, Vector3Int cellB)
        {
            if (!index.TryGetEdgeSealingLateral(cellA, cellB, out var edge))
                return false;

            return TileIdentityUtil.IsStructural(edge.identity);
        }

        static bool OccupiedCellHasStructural(FloorMapIndex index, int x, int y, int z)
        {
            if (!index.TryGetCellTiles(x, z, y, out var list) || list == null || list.Count == 0)
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                if (TileIdentityUtil.IsStructural(list[i].identity))
                    return true;
            }

            return false;
        }

        static bool TryGetWalkableFloorBuildingId(
            FloorMapIndex index,
            int x,
            int cellY,
            int z,
            out int buildingId)
        {
            buildingId = 0;
            if (!index.TryGetFloorFaceForWalkableCell(x, cellY, z, out var face))
                return false;

            buildingId = face.identity.buildingId;
            return true;
        }

        static void BuildColumnMaxY(
            IReadOnlyCollection<Vector3Int> floorCells,
            out Dictionary<(int x, int z), int> columnMaxY)
        {
            columnMaxY = new Dictionary<(int x, int z), int>();
            foreach (var cell in floorCells)
            {
                var key = (cell.x, cell.z);
                if (!columnMaxY.TryGetValue(key, out int maxY) || cell.y > maxY)
                    columnMaxY[key] = cell.y;
            }
        }

        /// <summary>디버그: 측면 leak 발생 (floorCell, 이웃, 사유). 없으면 빈 목록.</summary>
        public static void DiagnoseLateralLeaks(
            IReadOnlyCollection<Vector3Int> floorCells,
            int buildingId,
            BuildingExtent extent,
            FloorMapIndex index,
            List<(Vector3Int floorCell, Vector3Int neighbor, string reason)> into)
        {
            into.Clear();
            if (!TryPrepareLeakEvaluation(floorCells, buildingId, extent, index))
                return;

            foreach (var cell in floorCells)
            {
                int cellY = cell.y;
                foreach (var d in CardinalDirs)
                {
                    int nx = cell.x + d.x;
                    int nz = cell.z + d.z;
                    if (extent.ContainsAabb(nx, cellY, nz))
                        continue;

                    var neighbor = new Vector3Int(nx, cellY, nz);
                    if (TryDescribeLateralLeakReason(cell, neighbor, extent, index, out string reason))
                        into.Add((cell, neighbor, reason));
                }
            }
        }

        /// <summary>디버그: 천장 leak 발생 column (x,z), floorY, 사유. 없으면 빈 목록.</summary>
        public static void DiagnoseCeilingLeaks(
            IReadOnlyCollection<Vector3Int> floorCells,
            int buildingId,
            BuildingExtent extent,
            FloorMapIndex index,
            List<(int x, int z, int probeY, string reason)> into)
        {
            into.Clear();
            if (!TryPrepareLeakEvaluation(floorCells, buildingId, extent, index))
                return;

            BuildColumnMaxY(floorCells, out var columnMaxY);

            foreach (var kv in columnMaxY)
            {
                int x = kv.Key.x;
                int z = kv.Key.z;
                int floorY = kv.Value;

                if (!index.TryGetColumnSealTopY(x, z, out int topY))
                {
                    into.Add((x, z, floorY, "no seal anywhere in column"));
                    continue;
                }

                if (topY <= floorY)
                    into.Add((x, z, floorY, $"topSealY={topY}<=floorY={floorY}"));
            }
        }

        static bool TryDescribeLateralLeakReason(
            Vector3Int cell,
            Vector3Int neighbor,
            BuildingExtent extent,
            FloorMapIndex index,
            out string reason)
        {
            reason = null;
            if (extent.ContainsAabb(neighbor.x, neighbor.y, neighbor.z))
                return false;

            if (LateralEdgeSeals(index, cell, neighbor))
                return false;

            if (OccupiedCellHasStructural(index, neighbor.x, neighbor.y, neighbor.z))
                return false;

            if (!index.CellHasFloor(neighbor.x, neighbor.y, neighbor.z))
            {
                reason = "neighborNoFloor(open)";
                return true;
            }

            if (!TryGetWalkableFloorBuildingId(index, neighbor.x, neighbor.y, neighbor.z, out int neighborBuildingId))
            {
                reason = "neighborFloorNoBuildingId";
                return true;
            }

            if (neighborBuildingId == TileIdentity.BuildingIdOutdoor)
            {
                reason = $"neighborOutdoor({neighborBuildingId})";
                return true;
            }

            if (neighborBuildingId <= 0)
            {
                reason = $"neighborUnassigned({neighborBuildingId})";
                return true;
            }

            return false;
        }
    }
}
