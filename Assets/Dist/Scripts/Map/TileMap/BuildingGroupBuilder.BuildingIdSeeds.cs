// ============================================================
// BuildingGroupBuilder.BuildingIdSeeds — minCellY 시드·orphan·slice footprint buildingId
// ============================================================
using System.Collections.Generic;

namespace IsoTilemap
{
    public sealed partial class BuildingGroupBuilder
    {
        HashSet<(int x, int z)> CollectMinCellYBuildingSeeds()
        {
            var seeds = new HashSet<(int x, int z)>();

            foreach (var (x, cellY, z) in _topology.Index.EnumerateWalkableFloorCells())
            {
                if (cellY != _minCellY)
                    continue;

                if (_registry.IsPlazaXZ(x, z))
                    continue;

                if (!IsFloorBuildingUnassigned(x, cellY, z))
                    continue;

                seeds.Add((x, z));
            }

            return seeds;
        }
        void AssignBuildingsFromSeeds(IEnumerable<(int x, int z)> seeds)
        {
            if (seeds == null)
                return;

            var terrain = new HashSet<(int x, int z)>(_registry.PlazaFloorXZ);

            foreach (var (seedX, seedZ) in seeds)
            {
                if (!IsFloorBuildingUnassigned(seedX, _minCellY, seedZ))
                    continue;

                if (terrain.Contains((seedX, seedZ)))
                    continue;

                var footprint = FloorRoomFloodFill.Run(
                    _topology.Index, _minCellY, seedX, seedZ,
                    collectEmptyNeighbors: false,
                    excludeCells: terrain).Visited;

                if (footprint.Count == 0)
                    continue;

                int buildingId = _registry.AllocateBuildingId();
                AssignBuildingFootprintOnSlice(buildingId, _minCellY, footprint, terrain);
            }
        }
        void AssignBuildingFootprintOnSlice(
            int buildingId,
            int cellY,
            HashSet<(int x, int z)> footprint,
            HashSet<(int x, int z)> terrainExclude)
        {
            foreach (var (x, z) in footprint)
            {
                if (terrainExclude != null && cellY == _minCellY && terrainExclude.Contains((x, z)))
                    continue;

                if (!IsFloorBuildingUnassigned(x, cellY, z))
                    continue;

                SetFloorBuildingRoom(x, cellY, z, buildingId, 0);
            }
        }
        /// <summary>
        /// <see cref="TileIdentity.BuildingIdUnassigned"/>(0) floor만 대상.
        /// 이미 id가 있는 building에 cardinal로 닿은 0 구역은 propagate가 먼저 흡수 — orphan은 고립 0만.
        /// </summary>
        void AssignOrphanFloorBuildings()
        {
            var seeds = new HashSet<(int x, int z, int y)>();

            foreach (var (x, cellY, z) in _topology.Index.EnumerateWalkableFloorCells())
            {
                if (!IsFloorBuildingUnassigned(x, cellY, z))
                    continue;

                seeds.Add((x, cellY, z));
            }

            foreach (var (seedX, seedZ, seedCellY) in seeds)
            {
                if (!IsFloorBuildingUnassigned(seedX, seedCellY, seedZ))
                    continue;

                var terrain = new HashSet<(int x, int z)>(_registry.PlazaFloorXZ);
                var footprint = FloorRoomFloodFill.Run(
                    _topology.Index, seedCellY, seedX, seedZ,
                    collectEmptyNeighbors: false,
                    excludeCells: seedCellY == _minCellY ? terrain : null).Visited;

                if (footprint.Count == 0)
                    continue;

                int buildingId = _registry.AllocateBuildingId();
                HashSet<(int x, int z)> terrainExclude = seedCellY == _minCellY ? terrain : null;
                AssignBuildingFootprintOnSlice(buildingId, seedCellY, footprint, terrainExclude);
            }
        }
        void TryAssignLocalBuildingSeeds(HashSet<(int x, int z, int y)> cells)
        {
            if (cells == null || cells.Count == 0)
                return;

            var seeds = new HashSet<(int x, int z)>();
            foreach (var (x, z, y) in cells)
            {
                if (y != _minCellY)
                    continue;

                if (_registry.IsPlazaXZ(x, z))
                    continue;

                if (!IsFloorBuildingUnassigned(x, y, z))
                    continue;

                if (_topology.Index.CellHasFloor(x, y, z))
                    seeds.Add((x, z));
            }

            if (seeds.Count > 0)
                AssignBuildingsFromSeeds(seeds);
        }
    }
}
