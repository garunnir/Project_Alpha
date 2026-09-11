// ============================================================
// BuildingGroupBuilder.Merge — 레거시 floor merge (AssignAll 미사용)
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    public sealed partial class BuildingGroupBuilder
    {
        [Obsolete("Replaced by component batch union in BuildingGroupBuilder.Components.cs")]
        void MergeBuildingsOnFloorAdjacency()
        {
            bool merged;
            do
            {
                merged = false;
                for (int cellY = _minCellY; cellY <= _maxCellY; cellY++)
                    merged |= MergeFloorAdjacencyOnSlice(cellY);
            }
            while (merged);
        }
        bool MergeFloorAdjacencyOnSlice(int cellY)
        {
            bool merged = false;

            _walkableFloorCellScratch.Clear();
            foreach (var cell in _topology.Index.EnumerateWalkableFloorCells())
            {
                if (cell.cellY != cellY)
                    continue;

                _walkableFloorCellScratch.Add(cell);
            }

            for (int i = 0; i < _walkableFloorCellScratch.Count; i++)
            {
                var (x, _, z) = _walkableFloorCellScratch[i];

                if (IsPlazaOrOutdoorFloor(x, z, cellY))
                    continue;

                int buildingA = GetFloorBuildingId(x, cellY, z);
                if (!BuildingIdBakeRules.CanPropagateBuildingIdFrom(buildingA))
                    continue;

                foreach (var d in CardinalDirs)
                {
                    int nx = x + d.x;
                    int nz = z + d.z;

                    if (IsPlazaOrOutdoorFloor(nx, nz, cellY))
                        continue;

                    if (!_topology.Index.CellHasFloor(nx, cellY, nz))
                        continue;

                    int buildingB = GetFloorBuildingId(nx, cellY, nz);
                    if (!BuildingIdBakeRules.CanPropagateBuildingIdFrom(buildingB) || buildingA == buildingB)
                        continue;

                    int canonical = Math.Min(buildingA, buildingB);
                    int absorbed = Math.Max(buildingA, buildingB);
                    AbsorbBuildingId(absorbed, canonical);
                    merged = true;
                    buildingA = canonical;
                }
            }

            return merged;
        }
        void AbsorbBuildingId(int absorbedId, int canonicalId) =>
            AbsorbBuildingId(absorbedId, canonicalId, notifyCells: null);

        void AbsorbBuildingId(int absorbedId, int canonicalId, HashSet<Vector3Int> notifyCells)
        {
            if (!BuildingIdBakeRules.CanPropagateBuildingIdFrom(absorbedId) || !BuildingIdBakeRules.CanPropagateBuildingIdFrom(canonicalId) ||
                absorbedId == canonicalId)
                return;

            _model.ForEachRuntimeTileMutating(tile =>
            {
                if (!TileIdentityUtil.IsStructural(tile.identity))
                    return;

                if (tile.identity.buildingId != absorbedId)
                    return;

                if (notifyCells != null)
                {
                    notifyCells.Add(tile.identity.GridPos);
                    notifyCells.Add(OccupiedCellCoord.PrimaryCellFromIdentity(tile.identity));
                }

                _model.PatchTileIdentity(tile.tileDefId, canonicalId, tile.identity.roomId);
            });
        }
    }
}
