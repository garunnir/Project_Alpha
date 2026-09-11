// ============================================================
// BuildingGroupBuilder.Incremental — 증분 타일 편집 bake (국소 building 영향)
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    public sealed partial class BuildingGroupBuilder
    {
        /// <summary>
        /// 타일 추가/적용. 전맵 indoor id Reset 금지 —
        /// 변경 셀 근처에서만 0 흡수·양수 merge. dig/remove와 동일 경로.
        /// </summary>
        public void HandleSetOrApply(IReadOnlyCollection<Vector3Int> changedCells)
        {
            ApplyIncrementalTopologyChange(changedCells, seedRoomKeys: null);
        }

        public void HandleRemoveTile(TileData removed, HashSet<Vector3Int> changedCells)
        {
            var removals = new List<TileData> { removed };
            HandleCoalescedTopologyChange(changedCells, removals);
        }

        /// <summary>
        /// 지연·병합된 topology 변경 1회 bake.
        /// dig remove+stratum add = 타일 제거/추가와 동일 증분 경로 (별도 dig bake 없음).
        /// </summary>
        public void HandleCoalescedTopologyChange(
            HashSet<Vector3Int> changedCells,
            IReadOnlyList<TileData> removals)
        {
            if (changedCells == null || changedCells.Count == 0)
                return;

            if (removals != null)
            {
                for (int i = 0; i < removals.Count; i++)
                {
                    TileData removed = removals[i];
                    if (!TileIdentityUtil.IsFloorTile(removed.identity))
                        continue;
                    if (removed.identity.buildingId == TileIdentity.BuildingIdOutdoor ||
                        _registry.IsOutdoorFloorCell(removed.identity.GridPos))
                    {
                        var next = new HashSet<Vector3Int>(_registry.OutdoorFloorCells);
                        next.Remove(removed.identity.GridPos);
                        _registry.ReplaceOutdoorFloorCells(next);
                    }
                }
            }

            HashSet<RoomKey> seedRoomKeys = null;
            if (removals != null)
            {
                for (int i = 0; i < removals.Count; i++)
                {
                    TileData removed = removals[i];
                    int buildingId = removed.identity.buildingId;

                    if (TileIdentityUtil.IsFloorTile(removed.identity) &&
                        (buildingId == TileIdentity.BuildingIdOutdoor ||
                         buildingId == TileIdentity.BuildingIdUnassigned))
                        continue;

                    if (!BuildingIdBakeRules.CanPropagateBuildingIdFrom(buildingId))
                        continue;

                    seedRoomKeys ??= new HashSet<RoomKey>();
                    foreach (RoomKey key in CollectAffectedRoomKeys(removed, changedCells))
                        seedRoomKeys.Add(key);
                }
            }

            ApplyIncrementalTopologyChange(changedCells, seedRoomKeys);
        }

        /// <summary>
        /// 추가·제거·dig/메우기 공통 증분.
        /// indoor room/shell/space는 양수 building slice가 있을 때만.
        /// </summary>
        void ApplyIncrementalTopologyChange(
            IReadOnlyCollection<Vector3Int> changedCells,
            HashSet<RoomKey> seedRoomKeys)
        {
            if (changedCells == null || changedCells.Count == 0)
                return;

            ComputeCellYRange();
            SyncOutdoorLayerFromOutdoorIdTiles();

            var keys = seedRoomKeys ?? new HashSet<RoomKey>();
            var extraSeeds = new HashSet<(int x, int z, int y)>();
            var notifyCells = new HashSet<Vector3Int>();

            foreach (var cell in changedCells)
            {
                CollectRoomKeysNearCell(cell, keys);
                extraSeeds.Add((cell.x, cell.z, cell.y));
                notifyCells.Add(cell);
                foreach (var d in CardinalDirs)
                {
                    var n = cell + d;
                    extraSeeds.Add((n.x, n.z, n.y));
                    notifyCells.Add(n);
                }
            }

            ApplyLocalBuildingConnectivityNearCells(changedCells, notifyCells);

            // connectivity 이후 새 양수 id가 생겼을 수 있으므로 근처 room key 재수집.
            foreach (var cell in changedCells)
                CollectRoomKeysNearCell(cell, keys);

            if (HasIndoorRoomBakeTargets(keys, extraSeeds))
                RebuildRooms(keys, extraSeeds);

            _model.NotifyCellsChanged(notifyCells);
        }

        bool HasIndoorRoomBakeTargets(
            HashSet<RoomKey> keys,
            HashSet<(int x, int z, int y)> extraSeeds)
        {
            if (keys != null)
            {
                foreach (var key in keys)
                {
                    if (BuildingIdBakeRules.CanPropagateBuildingIdFrom(key.BuildingId))
                        return true;
                }
            }

            if (extraSeeds == null)
                return false;

            foreach (var (x, z, y) in extraSeeds)
            {
                int buildingId = GetFloorBuildingId(x, y, z);
                if (BuildingIdBakeRules.CanPropagateBuildingIdFrom(buildingId))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 변경 셀 근처만: 양수←0 흡수, 맞닿은 양수 merge, 고립 0 footprint에 새 id.
        /// </summary>
        void ApplyLocalBuildingConnectivityNearCells(
            IReadOnlyCollection<Vector3Int> changedCells,
            HashSet<Vector3Int> notifyCells)
        {
            if (changedCells == null || changedCells.Count == 0)
                return;

            var sliceYs = new HashSet<int>();
            foreach (Vector3Int cell in changedCells)
            {
                sliceYs.Add(cell.y);
                foreach (var d in CardinalDirs)
                    sliceYs.Add(cell.y + d.y);
            }

            foreach (int cellY in sliceYs)
            {
                if (cellY < _minCellY || cellY > _maxCellY)
                    continue;

                PropagateBuildingIdThroughAdjacentUnassignedFloorsNearChangedOnSlice(
                    cellY, changedCells, notifyCells);
                MergeFloorAdjacencyNearChangedOnSlice(cellY, changedCells, notifyCells);
                AssignOrphanUnassignedFootprintsNearChangedOnSlice(cellY, changedCells, notifyCells);
            }
        }

        void PropagateBuildingIdThroughAdjacentUnassignedFloorsNearChangedOnSlice(
            int cellY,
            IReadOnlyCollection<Vector3Int> changedCells,
            HashSet<Vector3Int> notifyCells)
        {
            var seeds = new HashSet<(int x, int z)>();
            foreach (Vector3Int cell in changedCells)
            {
                if (cell.y != cellY)
                    continue;

                seeds.Add((cell.x, cell.z));
                foreach (var d in CardinalDirs)
                    seeds.Add((cell.x + d.x, cell.z + d.z));
            }

            foreach (var (x, z) in seeds)
            {
                if (IsPlazaOrOutdoorFloor(x, z, cellY))
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

                    var footprint = CollectUnassignedFloorFootprint(cellY, nx, nz);
                    if (footprint.Count == 0)
                        continue;

                    foreach (var (fx, fz) in footprint)
                    {
                        SetFloorBuildingRoom(fx, cellY, fz, buildingId, 0);
                        notifyCells.Add(new Vector3Int(fx, cellY, fz));
                    }
                }
            }
        }

        void MergeFloorAdjacencyNearChangedOnSlice(
            int cellY,
            IReadOnlyCollection<Vector3Int> changedCells,
            HashSet<Vector3Int> notifyCells)
        {
            var seeds = new HashSet<(int x, int z)>();
            foreach (Vector3Int cell in changedCells)
            {
                if (cell.y != cellY)
                    continue;

                seeds.Add((cell.x, cell.z));
                foreach (var d in CardinalDirs)
                    seeds.Add((cell.x + d.x, cell.z + d.z));
            }

            foreach (var (x, z) in seeds)
            {
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
                    if (!BuildingIdBakeRules.CanPropagateBuildingIdFrom(buildingB) ||
                        buildingA == buildingB)
                        continue;

                    int canonical = Math.Min(buildingA, buildingB);
                    int absorbed = Math.Max(buildingA, buildingB);
                    AbsorbBuildingId(absorbed, canonical, notifyCells);
                    buildingA = canonical;
                }
            }
        }

        void AssignOrphanUnassignedFootprintsNearChangedOnSlice(
            int cellY,
            IReadOnlyCollection<Vector3Int> changedCells,
            HashSet<Vector3Int> notifyCells)
        {
            var processed = new HashSet<(int x, int z)>();

            foreach (Vector3Int cell in changedCells)
            {
                if (cell.y != cellY)
                    continue;

                int x = cell.x;
                int z = cell.z;
                if (!IsUnassignedPropagableFloor(x, cellY, z))
                    continue;

                if (processed.Contains((x, z)))
                    continue;

                var footprint = CollectUnassignedFloorFootprint(cellY, x, z);
                if (footprint.Count == 0)
                    continue;

                int buildingId = _registry.AllocateBuildingId();
                foreach (var (fx, fz) in footprint)
                {
                    processed.Add((fx, fz));
                    SetFloorBuildingRoom(fx, cellY, fz, buildingId, 0);
                    notifyCells.Add(new Vector3Int(fx, cellY, fz));
                }
            }
        }
    }
}
