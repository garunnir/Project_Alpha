// ============================================================
// BuildingGroupBuilder.AdjacentZeroPropagate — building 인접 미할당(0) footprint 흡수
// ============================================================
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    public sealed partial class BuildingGroupBuilder
    {
        /// <summary>
        /// buildingId&gt;0 floor에 cardinal 인접한 미할당(0) footprint에 동일 id 부여. 편집·full bake 공통.
        /// </summary>
        void PropagateBuildingIdThroughAdjacentUnassignedFloorsUntilFixed()
        {
            bool changed;
            do
            {
                changed = false;
                for (int cellY = _minCellY; cellY <= _maxCellY; cellY++)
                    changed |= PropagateBuildingIdThroughAdjacentUnassignedFloorsOnSlice(cellY);
            }
            while (changed);
        }
        bool PropagateBuildingIdThroughAdjacentUnassignedFloorsOnSlice(int cellY)
        {
            bool changed = false;
            _walkableFloorCellScratch.Clear();
            foreach (var cell in _topology.Index.EnumerateWalkableFloorCells())
            {
                if (cell.cellY != cellY)
                    continue;

                _walkableFloorCellScratch.Add(cell);
            }

            _unassignedFootprintScratch.Clear();

            for (int i = 0; i < _walkableFloorCellScratch.Count; i++)
            {
                var (x, _, z) = _walkableFloorCellScratch[i];

                if (IsPlazaOrTerrainFloor(x, z, cellY))
                    continue;

                int buildingId = GetFloorBuildingId(x, cellY, z);
                if (!BuildingIdBakeRules.CanPropagateBuildingIdFrom(buildingId))
                    continue;

                foreach (var d in CardinalDirs)
                {
                    int nx = x + d.x;
                    int nz = z + d.z;

                    if (!IsUnassignedPropagableFloor(nx, cellY, nz))
                        continue;

                    if (_unassignedFootprintScratch.Contains((nx, nz)))
                        continue;

                    var footprint = CollectUnassignedFloorFootprint(cellY, nx, nz);
                    if (footprint.Count == 0)
                        continue;

                    foreach (var (fx, fz) in footprint)
                    {
                        SetFloorBuildingRoom(fx, cellY, fz, buildingId, 0);
                        _unassignedFootprintScratch.Add((fx, fz));
                    }

                    changed = true;
                }
            }

            return changed;
        }
        bool IsUnassignedPropagableFloor(int x, int cellY, int z) =>
            IsFloorBuildingUnassigned(x, cellY, z) &&
            !IsPlazaOrTerrainFloor(x, z, cellY) &&
            _topology.Index.CellHasFloor(x, cellY, z);

        HashSet<(int x, int z)> CollectUnassignedFloorFootprint(int cellY, int startX, int startZ)
        {
            var footprint = new HashSet<(int x, int z)>();
            if (!IsUnassignedPropagableFloor(startX, cellY, startZ))
                return footprint;

            var index = _topology.Index;
            Vector3Int start = index.ResolveFloorBfsStart(cellY, startX, startZ);
            if (!IsUnassignedPropagableFloor(start.x, cellY, start.z))
                return footprint;

            var visitedCells = new HashSet<Vector3Int> { start };
            var q = new Queue<Vector3Int>();
            q.Enqueue(start);
            footprint.Add((start.x, start.z));

            int steps = 0;
            while (q.Count > 0)
            {
                if (++steps > OccupiedCellFloodSafetyLimit)
                    break;

                Vector3Int cur = q.Dequeue();
                foreach (var d in CardinalDirs)
                {
                    int nx = cur.x + d.x;
                    int nz = cur.z + d.z;
                    var neighbor = new Vector3Int(nx, cellY, nz);

                    if (index.EdgeSeparatesRoom(cur, neighbor))
                        continue;

                    if (visitedCells.Contains(neighbor))
                        continue;

                    if (!IsUnassignedPropagableFloor(nx, cellY, nz))
                        continue;

                    if (index.TryGetCellTiles(nx, nz, cellY, out var list) &&
                        FloorMapIndex.CellHasSolidWall(list))
                        continue;

                    visitedCells.Add(neighbor);
                    footprint.Add((nx, nz));
                    q.Enqueue(neighbor);
                }
            }

            return footprint;
        }
    }
}
