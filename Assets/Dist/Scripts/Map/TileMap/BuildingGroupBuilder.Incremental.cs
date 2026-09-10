// ============================================================
// BuildingGroupBuilder.Incremental — 증분 타일 편집 bake (HandleSetOrApply/Remove)
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    public sealed partial class BuildingGroupBuilder
    {
        public void HandleSetOrApply(IReadOnlyCollection<Vector3Int> changedCells)
        {
            if (changedCells == null || changedCells.Count == 0)
                return;

            if (IsMinCellYFloorChange(changedCells))
            {
                RecomputeOutdoorFromMinAndRebuildLost(changedCells);
                TagWallsFromFloorAdjacencyNearCells(changedCells);
                _model.ReindexTilesByIdFromRuntime();
                RebuildRegistryIndices();
                BakeAllSpaces();
                _model.MarkTilesDirty();
                return;
            }

            var keys = new HashSet<RoomKey>();
            var extraSeeds = new HashSet<(int x, int z, int y)>();

            foreach (var cell in changedCells)
            {
                CollectRoomKeysNearCell(cell, keys);
                extraSeeds.Add((cell.x, cell.z, cell.y));
                foreach (var d in CardinalDirs)
                {
                    var n = cell + d;
                    extraSeeds.Add((n.x, n.z, n.y));
                }
            }

            ResetIndoorBuildingIds();
            RebakeBuildingIdsFromComponents();
            RebuildRooms(keys, extraSeeds);
            TagWallsFromFloorAdjacencyNearCells(changedCells);
            _model.ReindexTilesByIdFromRuntime();
            RebuildRegistryIndices();
            BakeAllSpaces();
            _model.MarkTilesDirty();
        }

        public void HandleRemoveTile(TileData removed, HashSet<Vector3Int> changedCells)
        {
            var removals = new List<TileData> { removed };
            HandleCoalescedTopologyChange(changedCells, removals);
        }

        /// <summary>
        /// 지연·병합된 topology 변경 1회 bake. dig remove+stratum add를 한 패스로 처리합니다.
        /// </summary>
        public void HandleCoalescedTopologyChange(
            HashSet<Vector3Int> changedCells,
            IReadOnlyList<TileData> removals)
        {
            if (changedCells == null || changedCells.Count == 0)
                return;

            bool outdoorRecompute = IsMinCellYFloorChange(changedCells);
            var roomKeys = new HashSet<RoomKey>();

            if (removals != null)
            {
                for (int i = 0; i < removals.Count; i++)
                {
                    TileData removed = removals[i];
                    int buildingId = removed.identity.buildingId;

                    if (TileIdentityUtil.IsFloorTile(removed.identity) &&
                        (buildingId == TileIdentity.BuildingIdOutdoor ||
                         buildingId == TileIdentity.BuildingIdUnassigned))
                    {
                        outdoorRecompute = true;
                        continue;
                    }

                    if (BuildingIdBakeRules.CanPropagateBuildingIdFrom(buildingId))
                    {
                        foreach (RoomKey key in CollectAffectedRoomKeys(removed, changedCells))
                            roomKeys.Add(key);
                    }
                }
            }

            if (!outdoorRecompute &&
                MapTopologyBakeConsts.CanUseDigIncrementalBake(changedCells.Count, removals))
            {
                ApplyDigIncrementalBake(changedCells, roomKeys);
                return;
            }

            if (outdoorRecompute)
            {
                RecomputeOutdoorFromMinAndRebuildLost(changedCells);
                TagWallsFromFloorAdjacencyNearCells(changedCells);
                _model.ReindexTilesByIdFromRuntime();
                RebuildRegistryIndices();
                BakeAllSpaces();
                _model.MarkTilesDirty();
                return;
            }

            if (roomKeys.Count > 0)
            {
                RebuildRooms(roomKeys, null);
                TagWallsFromFloorAdjacencyNearCells(changedCells);
                _model.MarkTilesDirty();
                return;
            }

            HandleSetOrApply(changedCells);
        }

        void ApplyDigIncrementalBake(HashSet<Vector3Int> changedCells, HashSet<RoomKey> roomKeys)
        {
            var keys = roomKeys ?? new HashSet<RoomKey>();
            var extraSeeds = new HashSet<(int x, int z, int y)>();

            foreach (Vector3Int cell in changedCells)
            {
                CollectRoomKeysNearCell(cell, keys);
                extraSeeds.Add((cell.x, cell.z, cell.y));
                foreach (Vector3Int d in CardinalDirs)
                {
                    Vector3Int n = cell + d;
                    extraSeeds.Add((n.x, n.z, n.y));
                }
            }

            RebuildRooms(keys, extraSeeds);
            TagWallsFromFloorAdjacencyNearCells(changedCells);
            _model.MarkTilesDirty();
        }
        void RecomputeOutdoorFromMinAndRebuildLost(IReadOnlyCollection<Vector3Int> changedCells)
        {
            var oldOutdoor = new HashSet<(int x, int z)>(_registry.PlazaFloorXZ);
            RecomputeOutdoorFromMin();

            var lost = new HashSet<(int x, int z)>();
            foreach (var (x, z) in oldOutdoor)
            {
                if (!_registry.IsPlazaXZ(x, z))
                    lost.Add((x, z));
            }

            ResetIndoorBuildingIds();
            RebakeBuildingIdsFromComponents();

            var keys = new HashSet<RoomKey>();
            if (changedCells != null)
            {
                foreach (var cell in changedCells)
                    CollectRoomKeysNearCell(cell, keys);
            }

            var extraSeeds = new HashSet<(int x, int z, int y)>();
            foreach (var (x, z) in lost)
                extraSeeds.Add((x, z, _minCellY));

            RebuildRooms(keys, extraSeeds);
        }
        bool IsMinCellYFloorChange(IReadOnlyCollection<Vector3Int> cells)
        {
            if (cells == null)
                return false;

            foreach (var c in cells)
            {
                if (c.y == _minCellY)
                    return true;
            }

            return false;
        }
    }
}
