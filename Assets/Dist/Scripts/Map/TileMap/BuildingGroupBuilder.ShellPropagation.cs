// ============================================================
// BuildingGroupBuilder.ShellPropagation — §4 occupied-cell flood·열 상향 shell 전파
// ============================================================
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    public sealed partial class BuildingGroupBuilder
    {
        void TagAllWallsFromFloorAdjacency(HashSet<(int buildingId, int cellY)> sliceFilter)
        {
            _seedsByBuildingScratch.Clear();
            _lastStructuralFloodVisited = 0;
            _lastStructuralFloodPatched = 0;
            _lastStructuralFloodBridgedFloors = 0;

            foreach (var (x, floorCellY, z) in _topology.Index.EnumerateWalkableFloorCells())
            {
                if (IsPlazaOrOutdoorFloor(x, z, floorCellY))
                    continue;

                int buildingId = GetFloorBuildingId(x, floorCellY, z);
                if (!BuildingIdBakeRules.CanPropagateBuildingIdFrom(buildingId))
                    continue;

                if (sliceFilter != null &&
                    !SliceFilterAllows(sliceFilter, buildingId, floorCellY))
                    continue;

                AddStructuralFloodSeedsForWalkableFloor(buildingId, x, floorCellY, z);
            }

            foreach (var kv in _seedsByBuildingScratch)
                TagStructuralFromOccupiedCellFlood(kv.Key, kv.Value);
        }
        void TagWallsFromFloorAdjacencyOnSlice(int cellY)
        {
            // BuildingIdOutdoor = Y-wide wildcard (해당 cellY의 모든 양수 building).
            TagAllWallsFromFloorAdjacency(
                new HashSet<(int buildingId, int cellY)>
                {
                    (TileIdentity.BuildingIdOutdoor, cellY)
                });
        }

        static bool SliceFilterAllows(
            HashSet<(int buildingId, int cellY)> sliceFilter,
            int buildingId,
            int cellY)
        {
            if (sliceFilter.Contains((buildingId, cellY)))
                return true;

            // Y-wide: outdoor sentinel — TagWallsFromFloorAdjacencyOnSlice 전용.
            return sliceFilter.Contains((TileIdentity.BuildingIdOutdoor, cellY));
        }
        void TagWallsFromFloorAdjacencyNearCells(IReadOnlyCollection<Vector3Int> changedCells)
        {
            if (changedCells == null || changedCells.Count == 0)
                return;

            var sliceYs = new HashSet<int>();
            foreach (var cell in changedCells)
            {
                sliceYs.Add(cell.y);
                foreach (var d in CardinalDirs)
                    sliceYs.Add(cell.y + d.y);
            }

            foreach (int cellY in sliceYs)
            {
                if (cellY < _minCellY || cellY > _maxCellY)
                    continue;

                TagWallsFromFloorAdjacencyOnSlice(cellY);
            }
        }
        void AddStructuralFloodSeedsForWalkableFloor(int buildingId, int x, int cellY, int z)
        {
            if (!_seedsByBuildingScratch.TryGetValue(buildingId, out var seeds))
            {
                seeds = new HashSet<Vector3Int>();
                _seedsByBuildingScratch[buildingId] = seeds;
            }

            var occ = new Vector3Int(x, cellY, z);
            if (ShouldBlockBuildingFloodCell(occ, buildingId))
                return;

            seeds.Add(occ);

            VisitWalkableFloorFootprintCells(x, cellY, z, cell =>
            {
                if (!ShouldBlockBuildingFloodCell(cell, buildingId))
                    seeds.Add(cell);
            });
        }
        void TagStructuralFromOccupiedCellFlood(int buildingId, HashSet<Vector3Int> seedCells)
        {
            if (!BuildingIdBakeRules.CanPropagateBuildingIdFrom(buildingId) || seedCells == null || seedCells.Count == 0)
                return;

            _occupiedCellFloodVisitedScratch.Clear();
            var q = new Queue<Vector3Int>();
            int patchedThisFlood = 0;
            int bridgedFloorsThisFlood = 0;

            foreach (var cell in seedCells)
            {
                if (!CanTraverseOccupiedCellForStructuralFlood(buildingId, cell))
                    continue;

                if (!_topology.HasOccupancy(cell.x, cell.z, cell.y))
                    continue;

                if (_occupiedCellFloodVisitedScratch.Add(cell))
                    q.Enqueue(cell);
            }

            int steps = 0;
            while (q.Count > 0)
            {
                if (++steps > OccupiedCellFloodSafetyLimit)
                    break;

                Vector3Int cur = q.Dequeue();
                bridgedFloorsThisFlood += TryBridgeFloorBuildingFromStructuralFlood(buildingId, cur, q);
                CollectStructuralForPatch(cur, _structuralPatchTileScratch);

                for (int i = 0; i < _structuralPatchTileScratch.Count; i++)
                {
                    TileData tile = _structuralPatchTileScratch[i];
                    int existing = tile.identity.buildingId;
                    if (!BuildingIdBakeRules.ShouldOverwriteBuildingIdForPropagation(existing, buildingId))
                        continue;

                    patchedThisFlood++;
                    _model.PatchTileIdentity(tile.tileDefId, buildingId, tile.identity.roomId);

                    _occupiedCellAffectedScratch.Clear();
                    TileIdentityUtil.CollectAffectedCells(tile.identity, _occupiedCellAffectedScratch);
                    foreach (var affected in _occupiedCellAffectedScratch)
                    {
                        if (ShouldBlockBuildingFloodCell(affected, buildingId))
                            continue;

                        EnqueueStructuralFloodCellIfTraversable(buildingId, affected, q);
                    }
                }

                for (int d = 0; d < OccupiedCellFloodDirs.Length; d++)
                    EnqueueStructuralFloodCellIfTraversable(buildingId, cur + OccupiedCellFloodDirs[d], q);
            }

            PropagateBuildingIdUpVisitedColumns(buildingId, q, ref patchedThisFlood, ref bridgedFloorsThisFlood);

            _lastStructuralFloodVisited += _occupiedCellFloodVisitedScratch.Count;
            _lastStructuralFloodPatched += patchedThisFlood;
            _lastStructuralFloodBridgedFloors += bridgedFloorsThisFlood;
        }
        void PropagateBuildingIdUpVisitedColumns(
            int buildingId,
            Queue<Vector3Int> q,
            ref int patched,
            ref int bridgedFloors)
        {
            _columnAscendStartYScratch.Clear();
            foreach (var cell in _occupiedCellFloodVisitedScratch)
            {
                var key = (cell.x, cell.z);
                if (!_columnAscendStartYScratch.TryGetValue(key, out int startY) || cell.y < startY)
                    _columnAscendStartYScratch[key] = cell.y;
            }

            foreach (var kv in _columnAscendStartYScratch)
                PropagateBuildingIdUpColumn(buildingId, kv.Key.x, kv.Key.z, kv.Value, q, ref patched, ref bridgedFloors);
        }
        void PropagateBuildingIdUpColumn(
            int buildingId,
            int x,
            int z,
            int startY,
            Queue<Vector3Int> q,
            ref int patched,
            ref int bridgedFloors)
        {
            for (int y = startY; y <= _maxCellY; y++)
            {
                var columnCell = new Vector3Int(x, y, z);
                if (!_topology.HasOccupancy(x, z, y))
                    break;

                if (ShouldBlockBuildingFloodCell(columnCell, buildingId))
                    break;

                bridgedFloors += TryBridgeWalkableFloorAt(buildingId, x, y, z, q);
                bridgedFloors += TryBridgeWalkableFloorAboveCell(buildingId, x, y, z, q);
                patched += PatchStructuralShellAtOccupiedCell(buildingId, x, y, z, q);
            }
        }
        int TryBridgeWalkableFloorAt(int buildingId, int x, int walkableY, int z, Queue<Vector3Int> q)
        {
            var occ = WalkableFloorOccupiedCell(x, walkableY, z);
            if (ShouldBlockBuildingFloodCell(occ, buildingId))
                return 0;

            if (!IsFloorBuildingUnassigned(x, walkableY, z))
                return 0;

            if (!_topology.Index.CellHasFloor(x, walkableY, z))
                return 0;

            SetFloorBuildingRoom(x, walkableY, z, buildingId, 0);
            EnqueueStructuralFloodOccupiedCells(buildingId, x, walkableY, z, q);
            return 1;
        }
        int TryBridgeWalkableFloorAboveCell(int buildingId, int x, int cellY, int z, Queue<Vector3Int> q)
        {
            int aboveY = cellY + 1;
            if (aboveY > _maxCellY)
                return 0;

            if (!_topology.Index.TryGetHorizontalFaceBetween(
                    new Vector3Int(x, cellY, z),
                    new Vector3Int(x, aboveY, z),
                    out _))
                return 0;

            return TryBridgeWalkableFloorAt(buildingId, x, aboveY, z, q);
        }
        int PatchStructuralShellAtOccupiedCell(int buildingId, int x, int cellY, int z, Queue<Vector3Int> q)
        {
            CollectStructuralForPatch(new Vector3Int(x, cellY, z), _structuralPatchTileScratch);
            int patchedHere = 0;

            for (int i = 0; i < _structuralPatchTileScratch.Count; i++)
            {
                TileData tile = _structuralPatchTileScratch[i];
                int existing = tile.identity.buildingId;
                if (!BuildingIdBakeRules.ShouldOverwriteBuildingIdForPropagation(existing, buildingId))
                    continue;

                patchedHere++;
                _model.PatchTileIdentity(tile.tileDefId, buildingId, tile.identity.roomId);

                _occupiedCellAffectedScratch.Clear();
                TileIdentityUtil.CollectAffectedCells(tile.identity, _occupiedCellAffectedScratch);
                foreach (var affected in _occupiedCellAffectedScratch)
                {
                    if (ShouldBlockBuildingFloodCell(affected, buildingId))
                        continue;

                    EnqueueStructuralFloodCellIfTraversable(buildingId, affected, q);
                }
            }

            return patchedHere;
        }
        int TryBridgeFloorBuildingFromStructuralFlood(int buildingId, Vector3Int cell, Queue<Vector3Int> q)
        {
            int bridged = TryBridgeWalkableFloorAt(buildingId, cell.x, cell.y, cell.z, q);
            bridged += TryBridgeWalkableFloorAboveCell(buildingId, cell.x, cell.y, cell.z, q);
            return bridged;
        }
        void EnqueueStructuralFloodOccupiedCells(
            int buildingId,
            int x,
            int cellY,
            int z,
            Queue<Vector3Int> q)
        {
            EnqueueStructuralFloodCellIfTraversable(buildingId, new Vector3Int(x, cellY, z), q);
            VisitWalkableFloorFootprintCells(x, cellY, z, cell =>
                EnqueueStructuralFloodCellIfTraversable(buildingId, cell, q));
        }
        void EnqueueStructuralFloodCellIfTraversable(int buildingId, Vector3Int cell, Queue<Vector3Int> q)
        {
            if (!CanTraverseOccupiedCellForStructuralFlood(buildingId, cell))
                return;

            if (!_topology.HasOccupancy(cell.x, cell.z, cell.y))
                return;

            if (_occupiedCellFloodVisitedScratch.Add(cell))
                q.Enqueue(cell);
        }
        bool CanTraverseOccupiedCellForStructuralFlood(int buildingId, Vector3Int cell) =>
            !ShouldBlockBuildingFloodCell(cell, buildingId);

        bool ShouldBlockBuildingFloodCell(Vector3Int cell, int buildingId)
        {
            if (!_topology.TryCollectTilesAtOccupiedCell(cell, _occupiedCellCollectScratch))
                return false;

            return AnyCollectedIncidentTileBlocks(id =>
                BuildingIdBakeRules.ShouldBlockBuildingFloodFromIncidentTile(id, buildingId));
        }
        void CollectStructuralForPatch(Vector3Int cell, List<TileData> into)
        {
            into.Clear();
            _structuralPatchGuidScratch.Clear();

            if (!_topology.TryCollectTilesAtOccupiedCell(cell, _occupiedCellCollectScratch))
                return;

            for (int i = 0; i < _occupiedCellCollectScratch.Count; i++)
            {
                TileData tile = _occupiedCellCollectScratch[i];
                if (!BuildingIdBakeRules.ShouldPatchBuildingIdAtOccupiedCell(tile.identity))
                    continue;

                if (_structuralPatchGuidScratch.Add(tile.tileDefId))
                    into.Add(tile);
            }
        }
    }
}
