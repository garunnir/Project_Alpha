// ============================================================
// TileMapCacheHub — 맵 topology·건물·cellY·room geometry 통합 조회·무효화
// ============================================================
// 계약:
// - 캐시 히트 = FloorRoomFloodFill 재실행만 생략. 오클루전 delta·TileViewPresentationApplier 반영은 매번 수행.
// - 프레젠테이션(오클루전·고스트·선택)은 모델 TileState에 미기록. ApplyTiles/SetTile/RemoveTile = 구조·토폴로지.
// - InvalidateAll = topology 변경 시. 플레이어 셀 이동만으로는 무효화하지 않음.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>BFS 옵션 프로필. 캐시 키에 포함되며 소비자별 계약을 분리합니다.</summary>
    public enum FloorRoomBfsProfile
    {
        /// <summary>벽 오클루전: <c>collectEmptyNeighbors: false</c></summary>
        Occlusion,

        /// <summary>층 가시성(구멍·아래층): <c>collectEmptyNeighbors: true</c></summary>
        Visibility,
    }

    public readonly struct GeometryQuery
    {
        public FloorBfsResult Result { get; }
        public RoomKey? RoomKey { get; }

        public GeometryQuery(FloorBfsResult result, RoomKey? roomKey)
        {
            Result = result;
            RoomKey = roomKey;
        }

        public HashSet<(int x, int z)> Visited => Result.Visited;
    }

    /// <summary>셀·엣지 topology 조회 및 점유 (x,z,y) 동기화.</summary>
    public sealed class TopologyLayer
    {
        readonly FloorMapIndex _index;

        internal TopologyLayer(FloorMapIndex index) => _index = index;

        public FloorMapIndex Index => _index;

        public bool HasOccupancy(int x, int z, int y) => _index.HasAnyTile(x, z, y);

        public IEnumerable<(int x, int z, int y)> EnumerateOccupiedCells() =>
            _index.EnumerateOccupiedCells();

        public bool TryGetCellTiles(int x, int z, int cellY, out List<TileData> list) =>
            _index.TryGetCellTiles(x, z, cellY, out list);

        public bool TryCollectTilesAtOccupiedCell(Vector3Int cell, List<TileData> into) =>
            _index.TryCollectTilesAtOccupiedCell(cell, into);

        public bool TryCollectTilesAtOccupiedCell(int x, int z, int cellY, List<TileData> into) =>
            _index.TryCollectTilesAtOccupiedCell(x, z, cellY, into);

        public bool TryGetEdgeBetween(Vector3Int cellA, Vector3Int cellB, out TileData edgeWall) =>
            _index.TryGetEdgeBetween(cellA, cellB, out edgeWall);

        public bool TryGetHorizontalFaceBetween(Vector3Int cellBelow, Vector3Int cellAbove, out TileData face) =>
            _index.TryGetHorizontalFaceBetween(cellBelow, cellAbove, out face);

        public bool TryGetFloorFaceForWalkableCell(int x, int cellY, int z, out TileData face) =>
            _index.TryGetFloorFaceForWalkableCell(x, cellY, z, out face);

        public bool CellHasFloor(int x, int cellY, int z) =>
            _index.CellHasFloor(x, cellY, z);

        public Vector3Int ResolveFloorBfsStart(int cellY, int startX, int startZ) =>
            _index.ResolveFloorBfsStart(cellY, startX, startZ);

        public void SyncOccupancyFromChangedCells(IEnumerable<Vector3Int> changedCells) =>
            _index.SyncOccupancyFromChangedCells(changedCells);

        public void RebuildOccupancy() => _index.RebuildOccupancy();
    }

    /// <summary>buildingId·outdoor·room edge wall 역인덱스 (읽기 + Builder 쓰기).</summary>
    public sealed class BuildingLayer
    {
        readonly BuildingGroupRegistry _registry;

        internal BuildingLayer(BuildingGroupRegistry registry) => _registry = registry;

        public BuildingGroupRegistry Registry => _registry;

        public bool IsPlazaFloor(int cellY, int x, int z) => _registry.IsPlazaFloor(cellY, x, z);

        public bool TryGetEdgeWalls(RoomKey roomKey, out IReadOnlyCollection<Guid> edgeIds) =>
            _registry.TryGetEdgeWallIds(roomKey, out edgeIds);

        public bool TryGetFloorBuildingRoom(int cellY, int x, int z, TopologyLayer topology, out int buildingId, out int roomId)
        {
            buildingId = 0;
            roomId = 0;

            if (!topology.TryGetFloorFaceForWalkableCell(x, cellY, z, out var face))
                return false;

            buildingId = face.identity.buildingId;
            roomId = face.identity.roomId;
            return true;
        }

        public int TryGetBuildingIdAtCell(int cellY, int x, int z, TopologyLayer topology) =>
            TryGetFloorBuildingRoom(cellY, x, z, topology, out int buildingId, out _) ? buildingId : 0;

        public bool TryGetBuildingExtent(int buildingId, out BuildingExtent extent) =>
            _registry.TryGetBuildingExtent(buildingId, out extent);

        public bool IsInBuildingFloorFootprint(int buildingId, int cellY, int x, int z) =>
            _registry.TryGetBuildingExtent(buildingId, out var extent) &&
            extent.ContainsFloorFootprint(cellY, x, z);
    }

    /// <summary>RoomKey + profile 단위 bake된 BFS geometry.</summary>
    public sealed class RoomGeometryLayer
    {
        readonly Dictionary<(RoomKey key, FloorRoomBfsProfile profile), FloorBfsResult> _byRoomProfile = new();

        public void InvalidateAll() => _byRoomProfile.Clear();

        public void InvalidateRooms(IEnumerable<RoomKey> keys, CellYGeometryLayer cellYGeometry)
        {
            if (keys == null)
                return;

            foreach (var key in keys)
            {
                if (TryGet(key, FloorRoomBfsProfile.Occlusion, out var occ))
                    cellYGeometry.UnregisterFootprint(key.CellY, FloorRoomBfsProfile.Occlusion, occ);
                if (TryGet(key, FloorRoomBfsProfile.Visibility, out var vis))
                    cellYGeometry.UnregisterFootprint(key.CellY, FloorRoomBfsProfile.Visibility, vis);

                _byRoomProfile.Remove((key, FloorRoomBfsProfile.Occlusion));
                _byRoomProfile.Remove((key, FloorRoomBfsProfile.Visibility));
            }
        }

        public void Store(RoomKey key, FloorRoomBfsProfile profile, FloorBfsResult result) =>
            _byRoomProfile[(key, profile)] = result;

        public bool TryGet(RoomKey key, FloorRoomBfsProfile profile, out FloorBfsResult result) =>
            _byRoomProfile.TryGetValue((key, profile), out result);

        public void CollectRoomKeys(FloorRoomBfsProfile profile, List<RoomKey> into)
        {
            if (into == null)
                return;

            foreach (var kv in _byRoomProfile)
            {
                if (kv.Key.profile != profile)
                    continue;

                into.Add(kv.Key.key);
            }
        }
    }

    /// <summary>(cellY, profile)별 (x,z) → BFS 결과 역인덱스. lazy 선형 탐색 대체.</summary>
    public sealed class CellYGeometryLayer
    {
        readonly TopologyLayer _topology;
        readonly RoomGeometryLayer _rooms;

        readonly Dictionary<(int cellY, FloorRoomBfsProfile profile), Dictionary<(int x, int z), FloorBfsResult>>
            _cellToResult = new();

        internal CellYGeometryLayer(TopologyLayer topology, RoomGeometryLayer rooms)
        {
            _topology = topology;
            _rooms = rooms;
        }

        public void InvalidateAll() => _cellToResult.Clear();

        public void RegisterFootprint(int cellY, FloorRoomBfsProfile profile, FloorBfsResult result)
        {
            if (result.Visited == null || result.Visited.Count == 0)
                return;

            var dict = GetOrCreateCellMap(cellY, profile);
            foreach (var (x, z) in result.Visited)
                dict[(x, z)] = result;
        }

        public void UnregisterFootprint(int cellY, FloorRoomBfsProfile profile, FloorBfsResult result)
        {
            if (result.Visited == null)
                return;

            var key = (cellY, profile);
            if (!_cellToResult.TryGetValue(key, out var dict))
                return;

            foreach (var (x, z) in result.Visited)
                dict.Remove((x, z));

            if (dict.Count == 0)
                _cellToResult.Remove(key);
        }

        public FloorBfsResult GetForCell(int cellY, int x, int z, FloorRoomBfsProfile profile)
        {
            if (TryResolveRoomKey(cellY, x, z, out var roomKey) &&
                _rooms.TryGet(roomKey, profile, out var baked))
                return baked;

            var cellYKey = (cellY, profile);
            if (_cellToResult.TryGetValue(cellYKey, out var dict) &&
                dict.TryGetValue((x, z), out var cached))
                return cached;

            bool collectEmpty = profile == FloorRoomBfsProfile.Visibility;
            FloorBfsResult result = FloorRoomFloodFill.Run(_topology.Index, cellY, x, z, collectEmpty);
            RegisterFootprint(cellY, profile, result);
            return result;
        }

        public bool TryResolveRoomKey(int cellY, int x, int z, out RoomKey key)
        {
            key = default;

            if (!_topology.TryGetFloorFaceForWalkableCell(x, cellY, z, out var face))
                return false;

            int buildingId = face.identity.buildingId;
            int roomId = face.identity.roomId;
            if (buildingId > 0 && roomId > 0)
            {
                key = new RoomKey(buildingId, cellY, roomId);
                return true;
            }

            return false;
        }

        Dictionary<(int x, int z), FloorBfsResult> GetOrCreateCellMap(int cellY, FloorRoomBfsProfile profile)
        {
            var key = (cellY, profile);
            if (!_cellToResult.TryGetValue(key, out var dict))
            {
                dict = new Dictionary<(int x, int z), FloorBfsResult>();
                _cellToResult[key] = dict;
            }

            return dict;
        }
    }

    /// <summary>SpaceId·야외/실내 Space bake 결과 (판정 전용).</summary>
    public sealed class SpaceLayer
    {
        readonly SpaceRegistry _registry;

        internal SpaceLayer(SpaceRegistry registry) => _registry = registry;

        public SpaceRegistry Registry => _registry;

        public void Clear() => _registry.Clear();

        public bool TryGetSpaceAtFloorCell(int cellY, int x, int z, out int spaceId) =>
            _registry.TryGetSpaceAtFloorCell(cellY, x, z, out spaceId);

        public bool TryGetSpaceAtFloorCell(Vector3Int floorCell, out int spaceId) =>
            _registry.TryGetSpaceAtFloorCell(floorCell, out spaceId);

        public bool TryGetSpace(int spaceId, out SpaceBakeResult result) =>
            _registry.TryGetSpace(spaceId, out result);

        public bool IsOutdoorSpace(int spaceId) => _registry.IsOutdoorSpace(spaceId);
    }

    /// <summary>맵 캐시 단일 진입점 — 소비자는 읽기 API, Builder는 계층 쓰기.</summary>
    public sealed class TileMapCacheHub
    {
        public TopologyLayer Topology { get; }
        public BuildingLayer Buildings { get; }
        public RoomGeometryLayer Rooms { get; }
        public CellYGeometryLayer CellYGeometry { get; }
        public SpaceLayer Spaces { get; }

        MapBounds _mapBounds = MapBounds.Unbounded;

        /// <summary>저장 mapBounds 또는 로드 fallback. Unbounded면 InMapBounds가 전부 true.</summary>
        public MapBounds MapBounds => _mapBounds;

        BuildingGroupBuilder _roomBakeBuilder;
        readonly List<TileData> _occupiedCellCollectScratch = new();

        /// <summary>
        /// 씬 런타임 허브. DistScript가 층 셀로 <see cref="IsOutdoorEvaluation"/>을 호출할 때 사용.
        /// <see cref="Create"/>가 마지막 생성 인스턴스를 등록합니다.
        /// </summary>
        public static TileMapCacheHub Runtime { get; private set; }

        TileMapCacheHub(
            TopologyLayer topology,
            BuildingLayer buildings,
            RoomGeometryLayer rooms,
            CellYGeometryLayer cellYGeometry,
            SpaceLayer spaces)
        {
            Topology = topology;
            Buildings = buildings;
            Rooms = rooms;
            CellYGeometry = cellYGeometry;
            Spaces = spaces;
        }

        public static TileMapCacheHub Create(TileMapModel model, BuildingGroupRegistry registry)
        {
            var index = new FloorMapIndex(
                model.tiles,
                model.FaceBinder.WallFaceIndex,
                model.FaceBinder.FloorFaceIndex);
            var topology = new TopologyLayer(index);
            var buildings = new BuildingLayer(registry);
            var rooms = new RoomGeometryLayer();
            var cellYGeometry = new CellYGeometryLayer(topology, rooms);
            var spaces = new SpaceLayer(new SpaceRegistry());
            var hub = new TileMapCacheHub(topology, buildings, rooms, cellYGeometry, spaces);
            Runtime = hub;
            return hub;
        }

        internal void BindRoomBakeBuilder(BuildingGroupBuilder builder) => _roomBakeBuilder = builder;

        bool TryEnsureRoomKeyAtCell(int cellY, int x, int z, out RoomKey roomKey)
        {
            if (CellYGeometry.TryResolveRoomKey(cellY, x, z, out roomKey))
                return true;

            if (_roomBakeBuilder == null ||
                !_roomBakeBuilder.EnsureRoomAtFloorCell(cellY, x, z))
                return false;

            return CellYGeometry.TryResolveRoomKey(cellY, x, z, out roomKey);
        }

        public bool CellHasOccupancy(int x, int z, int y) => Topology.HasOccupancy(x, z, y);

        public void BindMapBounds(MapBounds bounds) => _mapBounds = bounds;

        public bool IsInMapBounds(int x, int y, int z) => _mapBounds.IsInMapBounds(x, y, z);

        public bool IsInMapBoundsXZ(int x, int z) => _mapBounds.IsInMapBoundsXZ(x, z);

        public bool CellHasFloor(int x, int cellY, int z) =>
            Topology.CellHasFloor(x, cellY, z);

        /// <summary>점유 셀에 <see cref="TileCollisionFlags.BlocksOccupiedCells"/> 타일이 있는지 — 이동·액체 차단 SSOT.</summary>
        public bool CellHasSolidWall(int x, int z, int cellY)
        {
            if (!TryGetCellTiles(x, z, cellY, out List<TileData> list))
                return false;

            return FloorMapIndex.CellHasSolidWall(list);
        }

        public void CollectStructuralOccludersAtOccupiedCell(
            Vector3Int occupiedCell,
            List<TileData> cellTilesOut,
            List<TileData> wallFacesOut)
        {
            cellTilesOut?.Clear();
            wallFacesOut?.Clear();

            if (!TryCollectTilesAtOccupiedCell(occupiedCell, _occupiedCellCollectScratch))
                return;

            for (int i = 0; i < _occupiedCellCollectScratch.Count; i++)
            {
                TileData tile = _occupiedCellCollectScratch[i];
                if (TileIdentityUtil.IsWallLike(tile.identity))
                {
                    wallFacesOut?.Add(tile);
                    continue;
                }

                if (TileIdentityUtil.IsHorizontalFace(tile.identity))
                    continue;

                cellTilesOut?.Add(tile);
            }
        }

        public bool TryCollectTilesAtOccupiedCell(Vector3Int cell, List<TileData> into) =>
            Topology.TryCollectTilesAtOccupiedCell(cell, into);

        public bool TryCollectTilesAtOccupiedCell(int x, int z, int cellY, List<TileData> into) =>
            Topology.TryCollectTilesAtOccupiedCell(x, z, cellY, into);

        public bool TryGetCellTiles(int x, int z, int cellY, out List<TileData> list) =>
            Topology.TryGetCellTiles(x, z, cellY, out list);

        public bool TryGetEdgeBetween(Vector3Int cellA, Vector3Int cellB, out TileData edgeWall) =>
            Topology.TryGetEdgeBetween(cellA, cellB, out edgeWall);

        public bool TryGetHorizontalFaceBetween(Vector3Int cellBelow, Vector3Int cellAbove, out TileData face) =>
            Topology.TryGetHorizontalFaceBetween(cellBelow, cellAbove, out face);

        public bool TryGetFloorFaceForWalkableCell(int x, int cellY, int z, out TileData face) =>
            Topology.TryGetFloorFaceForWalkableCell(x, cellY, z, out face);

        public IEnumerable<(int x, int z, int y)> EnumerateOccupiedCells() =>
            Topology.EnumerateOccupiedCells();

        public GeometryQuery GetRoomGeometryForCell(int cellY, int x, int z, FloorRoomBfsProfile profile)
        {
            RoomKey? roomKey = CellYGeometry.TryResolveRoomKey(cellY, x, z, out var key) ? key : (RoomKey?)null;
            var result = CellYGeometry.GetForCell(cellY, x, z, profile);
            return new GeometryQuery(result, roomKey);
        }

        public HashSet<(int x, int z)> GetVisitedForCell(
            int cellY, int x, int z, FloorRoomBfsProfile profile) =>
            CellYGeometry.GetForCell(cellY, x, z, profile).Visited;

        /// <summary>
        /// 월드 XZ 스냅 셀의 roomId로 bake된 <see cref="FloorBfsResult.Visited"/>를 읽습니다.
        /// <b>타일 가시성·오클루전 경로에서는 사용하지 않습니다</b> — <see cref="GetVisitedForCell"/> + evaluate 셀 SSOT.
        /// </summary>
        public HashSet<(int x, int z)> GetVisitedForWorld(
            int floorCellY,
            Vector3 worldRef,
            float cellSize,
            FloorRoomBfsProfile profile)
        {
            Vector3Int snap = TileHelper.ConvertWorldToGrid(worldRef, Mathf.Max(1e-4f, cellSize));
            if (!TryEnsureRoomKeyAtCell(floorCellY, snap.x, snap.z, out var roomKey))
                return null;

            if (!Rooms.TryGet(roomKey, profile, out var baked))
                return null;

            return baked.Visited;
        }

        /// <summary>
        /// 야외/실내 분기 판정 단일 API.
        /// plaza → true; empty/no-floor(TryGetFloorBuildingRoom 실패) → true (선택 A);
        /// Space 있으면 isOutdoor; 없으면 true.
        /// buildingId==0만으로 야외를 추론하지 않습니다.
        /// </summary>
        public bool IsOutdoorEvaluation(int cellY, int x, int z)
        {
            if (Buildings.IsPlazaFloor(cellY, x, z))
                return true;

            if (!TryGetFloorBuildingRoom(cellY, x, z, out _, out _))
                return true;

            if (Spaces.TryGetSpaceAtFloorCell(cellY, x, z, out int spaceId) &&
                Spaces.TryGetSpace(spaceId, out var space))
                return space.IsOutdoor;

            return true;
        }

        public bool TryGetFloorBuildingRoom(int cellY, int x, int z, out int buildingId, out int roomId) =>
            Buildings.TryGetFloorBuildingRoom(cellY, x, z, Topology, out buildingId, out roomId);

        public bool TryGetBuildingExtent(int buildingId, out BuildingExtent extent) =>
            Buildings.TryGetBuildingExtent(buildingId, out extent);

        public bool IsInBuildingFloorFootprint(int buildingId, int cellY, int x, int z) =>
            Buildings.IsInBuildingFloorFootprint(buildingId, cellY, x, z);

        public void InvalidateAll()
        {
            Rooms.InvalidateAll();
            CellYGeometry.InvalidateAll();
            Spaces.Clear();
        }

        public void InvalidateRooms(IEnumerable<RoomKey> keys) =>
            Rooms.InvalidateRooms(keys, CellYGeometry);

        public void NotifyTopologyChanged(
            IReadOnlyCollection<Vector3Int> changedCells,
            BuildingGroupBuilder builder,
            bool isRemoval = false,
            TileData removedTile = default,
            bool immediate = false)
        {
            if (!immediate && MapTopologyBakeDeferral.ShouldDefer)
            {
                MapTopologyBakeDeferral.Enqueue(changedCells, isRemoval, in removedTile);
                return;
            }

            ApplyTopologyChangedImmediate(changedCells, builder, isRemoval, removedTile);
        }

        public void FlushDeferredTopologyBakes(BuildingGroupBuilder builder)
        {
            if (!MapTopologyBakeDeferral.TryTakePending(
                    out HashSet<Vector3Int> changedCells,
                    out List<TileData> removals))
            {
                return;
            }

            if (changedCells != null && changedCells.Count > 0)
                Topology.SyncOccupancyFromChangedCells(changedCells);

            if (builder != null)
            {
                builder.HandleCoalescedTopologyChange(changedCells, removals);
                return;
            }

            InvalidateAll();
        }

        void ApplyTopologyChangedImmediate(
            IReadOnlyCollection<Vector3Int> changedCells,
            BuildingGroupBuilder builder,
            bool isRemoval,
            TileData removedTile)
        {
            if (changedCells != null && changedCells.Count > 0)
                Topology.SyncOccupancyFromChangedCells(changedCells);

            if (builder != null)
            {
                if (isRemoval)
                {
                    builder.HandleCoalescedTopologyChange(
                        ToMutableHashSet(changedCells),
                        new List<TileData> { removedTile });
                }
                else
                {
                    builder.HandleSetOrApply(changedCells);
                }

                return;
            }

            InvalidateAll();
        }

        static HashSet<Vector3Int> ToMutableHashSet(IReadOnlyCollection<Vector3Int> cells)
        {
            if (cells is HashSet<Vector3Int> set)
                return set;

            var result = new HashSet<Vector3Int>();
            if (cells == null)
                return result;

            foreach (var c in cells)
                result.Add(c);
            return result;
        }
    }

}
