// ============================================================
// SpaceLeakEvaluator — Space bake 후 topology 누수 → isOutdoor
// leak seal: footprint·outdoor·structural/floor topology — buildingId 동일성 미사용
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

        const int CeilingProbeMaxSteps = 64;

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
            int capY = extent.MaxStructuralY;

            foreach (var kv in columnMaxY)
            {
                if (ColumnHasCeilingLeak(index, kv.Key.x, kv.Key.z, kv.Value + 1, capY))
                    return true;
            }

            return false;
        }

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

        static bool ColumnHasCeilingLeak(FloorMapIndex index, int x, int z, int startY, int capY)
        {
            for (int step = 0; step < CeilingProbeMaxSteps; step++)
            {
                int y = startY + step;
                if (CeilingSealsAt(index, x, y, z))
                    break;

                if (y > capY)
                    return true;
            }

            return false;
        }

        static bool CeilingSealsAt(FloorMapIndex index, int x, int y, int z)
        {
            if (OccupiedCellHasStructural(index, x, y, z))
                return true;

            return index.CellHasFloor(x, y, z) &&
                   TryGetWalkableFloorBuildingId(index, x, y, z, out int floorBid) &&
                   IsIndoorFloorBid(floorBid);
        }

        static bool LateralEdgeSeals(FloorMapIndex index, Vector3Int cellA, Vector3Int cellB)
        {
            if (!index.TryGetEdgeBetween(cellA, cellB, out var edge))
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

        static bool IsIndoorFloorBid(int buildingId) => buildingId > 0;

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

        /// <summary>디버그: 천장 leak 발생 column (x,z), probeY, 사유. 없으면 빈 목록.</summary>
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
            int capY = extent.MaxStructuralY;

            foreach (var kv in columnMaxY)
            {
                int x = kv.Key.x;
                int z = kv.Key.z;
                int startY = kv.Value + 1;

                for (int step = 0; step < CeilingProbeMaxSteps; step++)
                {
                    int y = startY + step;
                    if (CeilingSealsAt(index, x, y, z))
                        break;

                    if (y > capY)
                    {
                        into.Add((x, z, y, $"probeY={y}>maxStructuralY={capY}"));
                        break;
                    }
                }
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

