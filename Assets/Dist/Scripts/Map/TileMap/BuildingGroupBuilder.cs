// ============================================================
// BuildingGroupBuilder — buildingId·roomId bake 오케스트레이션 (partial 루트)
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    public sealed partial class BuildingGroupBuilder
    {
        internal static readonly Vector3Int[] CardinalDirs =
        {
            Vector3Int.right, Vector3Int.back, Vector3Int.left, Vector3Int.forward
        };

        internal static readonly Vector3Int[] OccupiedCellFloodDirs =
        {
            Vector3Int.right, Vector3Int.back, Vector3Int.left, Vector3Int.forward,
            Vector3Int.up, Vector3Int.down
        };

        internal const int OccupiedCellFloodSafetyLimit = 200_000;

        readonly TileMapModel _model;
        readonly TileMapCacheHub _hub;
        readonly TopologyLayer _topology;
        readonly BuildingGroupRegistry _registry;

        int _minCellY;
        int _maxCellY;
        readonly List<TileData> _wallFaceScratch = new List<TileData>();
        readonly List<(int x, int cellY, int z)> _walkableFloorCellScratch = new();
        readonly List<RoomKey> _roomKeyScratch = new();
        readonly Dictionary<int, HashSet<Vector3Int>> _seedsByBuildingScratch = new();
        readonly Dictionary<(int x, int z), int> _columnAscendStartYScratch = new();
        readonly HashSet<Vector3Int> _occupiedCellFloodVisitedScratch = new();
        readonly HashSet<Vector3Int> _occupiedCellAffectedScratch = new();
        readonly HashSet<Guid> _structuralPatchGuidScratch = new();
        readonly List<TileData> _structuralPatchTileScratch = new();
        readonly List<TileData> _occupiedCellCollectScratch = new();
        readonly HashSet<(int x, int z)> _unassignedFootprintScratch = new();
        int _lastStructuralFloodVisited;
        int _lastStructuralFloodPatched;
        int _lastStructuralFloodBridgedFloors;

        readonly Dictionary<Vector3Int, Vector3Int> _floodParentScratch = new();
        readonly Dictionary<Vector3Int, string> _floodViaScratch = new();
        Vector3Int _floodTraceSourceCur;
        string _floodTraceSourceVia;
        bool _floodTraceActive;

        public BuildingGroupBuilder(TileMapModel model, TileMapCacheHub hub)
        {
            _model = model;
            _hub = hub;
            _topology = hub.Topology;
            _registry = hub.Buildings.Registry;
        }

        public BuildingGroupRegistry Registry => _registry;

        /// <summary>바닥 셀에 roomId가 없으면 BFS로 room을 부여·베이크합니다. 이미 roomId가 있으면 true.</summary>
        public bool EnsureRoomAtFloorCell(int cellY, int x, int z)
        {
            int buildingId = GetFloorBuildingId(x, cellY, z);
            if (!BuildingIdBakeRules.CanPropagateBuildingIdFrom(buildingId))
                return false;

            if (!_topology.Index.CellHasFloor(x, cellY, z))
                return false;

            if (GetFloorRoomId(x, cellY, z) > 0)
                return true;

            if (!TryBakeRoomFromSeed(buildingId, cellY, x, z))
                return false;

            TagPerimeterForSlice(buildingId, cellY);
            IndexEdgeWallsForSlice(buildingId, cellY);
            TagAllWallsFromFloorAdjacency(new HashSet<(int buildingId, int cellY)> { (buildingId, cellY) });
            _model.ReindexTilesByIdFromRuntime();
            RebuildRegistryIndices();
            BakeAllSpaces();
            _model.MarkTilesDirty();
            return true;
        }

        void RebuildRegistryIndices() =>
            _registry.RebuildIndicesFromTiles(_model.TilesSnapshot);

        /// <summary>
        /// 맵 bake. <b>양수 buildingId·outdoor(-1)는 하드 파티션 — Reset하지 않음.</b>
        /// 미할당(0)만 component로 새 id. 전체 재묶기는 <see cref="RebakeAllBuildingPartitions"/>.
        /// </summary>
        public void AssignAll()
        {
            _hub.InvalidateAll();
            _registry.Clear();
            _topology.RebuildOccupancy();
            ComputeCellYRange();
            SyncOutdoorLayerFromOutdoorIdTiles();
            // 알고리즘: 저장된 양수 id를 지우지 않음. 지우면 경계를 없애고 재합침 = 버그.
            StampOutdoorLayerFromRegistry();
            BakeBuildingComponentsForMap();
            AssignBuildingIdsFromComponents();
            BakeAllRooms();
            _topology.RebuildOccupancy();
            VerifyOccupancyIndexAfterBake();
            _model.ReindexTilesByIdFromRuntime();
            RebuildRegistryIndices();
            BakeAllSpaces();
            _model.MarkTilesDirty();
            LogCenterMacroLeakTrace();
            LogBakeSummaryIfDebug();
        }

        /// <summary>
        /// 명시적 Full Rebake — 양수 id를 지우고 연결로 재할당.
        /// 로드/Initialize에서는 호출하지 않음.
        /// </summary>
        public void RebakeAllBuildingPartitions()
        {
            _hub.InvalidateAll();
            _registry.Clear();
            _topology.RebuildOccupancy();
            ComputeCellYRange();
            SyncOutdoorLayerFromOutdoorIdTiles();
            ResetStructuralIds();
            StampOutdoorLayerFromRegistry();
            BakeBuildingComponentsForMap();
            AssignBuildingIdsFromComponents();
            BakeAllRooms();
            _topology.RebuildOccupancy();
            VerifyOccupancyIndexAfterBake();
            _model.ReindexTilesByIdFromRuntime();
            RebuildRegistryIndices();
            BakeAllSpaces();
            _model.MarkTilesDirty();
            LogCenterMacroLeakTrace();
            LogBakeSummaryIfDebug();
        }

        public HashSet<RoomKey> CollectAffectedRoomKeys(
            TileData removed,
            IReadOnlyCollection<Vector3Int> changedCells)
        {
            var keys = new HashSet<RoomKey>();
            TryAddRoomKeyFromTile(removed, keys);

            if (changedCells != null)
            {
                foreach (var cell in changedCells)
                    CollectRoomKeysNearCell(cell, keys);
            }

            return keys;
        }

        public void RebuildRooms(HashSet<RoomKey> affectedKeys, HashSet<(int x, int z, int y)> extraSeeds = null)
        {
            if (affectedKeys == null || affectedKeys.Count == 0)
            {
                if (extraSeeds == null || extraSeeds.Count == 0)
                    return;
            }

            var slices = new HashSet<(int buildingId, int cellY)>();
            if (affectedKeys != null)
            {
                foreach (var key in affectedKeys)
                {
                    if (!BuildingIdBakeRules.CanPropagateBuildingIdFrom(key.BuildingId))
                        continue;

                    slices.Add((key.BuildingId, key.CellY));
                }
            }

            if (extraSeeds != null)
            {
                foreach (var (x, z, y) in extraSeeds)
                {
                    int buildingId = GetFloorBuildingId(x, y, z);
                    if (!BuildingIdBakeRules.CanPropagateBuildingIdFrom(buildingId))
                        continue;

                    slices.Add((buildingId, y));
                }
            }

            // outdoor-only / 양수 slice 없음 — room·shell·space 전량 bake 금지.
            if (slices.Count == 0)
                return;

            _hub.InvalidateRooms(affectedKeys);

            foreach (var (buildingId, cellY) in slices)
                ClearRoomIdsOnSlice(buildingId, cellY);

            foreach (var (buildingId, cellY) in slices)
            {
                _registry.ClearEdgeIndexForSlice(buildingId, cellY);
                BakeRoomsForSlice(buildingId, cellY, extraSeeds);
            }

            TagPerimeterForSlices(slices);
            IndexEdgeWallsForSlices(slices);
            TagAllWallsFromFloorAdjacency(slices);
            _model.ReindexTilesByIdFromRuntime();
            RebuildRegistryIndices();
            BakeAllSpaces();
            _model.MarkTilesDirty();
        }

        void VisitWalkableFloorFootprintCells(int x, int cellY, int z, Action<Vector3Int> visit)
        {
            if (!_topology.TryCollectTilesAtOccupiedCell(x, cellY, z, _occupiedCellCollectScratch))
                return;

            for (int i = 0; i < _occupiedCellCollectScratch.Count; i++)
            {
                if (!TileIdentityUtil.IsHorizontalFace(_occupiedCellCollectScratch[i].identity))
                    continue;

                _occupiedCellAffectedScratch.Clear();
                TileIdentityUtil.CollectAffectedCells(
                    _occupiedCellCollectScratch[i].identity, _occupiedCellAffectedScratch);
                foreach (var cell in _occupiedCellAffectedScratch)
                    visit(cell);
            }
        }

        bool AnyCollectedIncidentTileBlocks(Func<TileIdentity, bool> shouldBlock)
        {
            for (int i = 0; i < _occupiedCellCollectScratch.Count; i++)
            {
                if (shouldBlock(_occupiedCellCollectScratch[i].identity))
                    return true;
            }

            return false;
        }
    }
}
