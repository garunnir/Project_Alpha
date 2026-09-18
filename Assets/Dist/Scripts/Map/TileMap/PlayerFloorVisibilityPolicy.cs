// ============================================================
// PlayerFloorVisibilityPolicy — 플레이어 층·실내/야외 분기 타일 가시성
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    public readonly struct FloorVisibilityContext : IEquatable<FloorVisibilityContext>
    {
        public bool IsPlayerOutdoor { get; }
        public int PlayerFloorCellY { get; }
        public int MinCellY { get; }
        public int PlayerBuildingId { get; }
        public int PlayerSpaceId { get; }
        public int PlayerSpaceMinY { get; }
        public int PlayerSpaceMaxY { get; }
        public HashSet<int> PlayerBlockingBuildingIds { get; }
        public HashSet<(int x, int z, int y)> VisibleBelowCells { get; }
        public HashSet<Vector3Int> PlayerSpaceFloorCells { get; }

        public FloorVisibilityContext(
            bool isPlayerOutdoor,
            int playerFloorCellY,
            int minCellY,
            int playerBuildingId,
            HashSet<int> playerBlockingBuildingIds,
            HashSet<(int x, int z, int y)> visibleBelowCells,
            int playerSpaceId = 0,
            int playerSpaceMinY = int.MinValue,
            int playerSpaceMaxY = int.MinValue,
            HashSet<Vector3Int> playerSpaceFloorCells = null)
        {
            IsPlayerOutdoor = isPlayerOutdoor;
            PlayerFloorCellY = playerFloorCellY;
            MinCellY = minCellY;
            PlayerBuildingId = playerBuildingId;
            PlayerSpaceId = playerSpaceId;
            if (playerSpaceMinY <= playerSpaceMaxY)
            {
                PlayerSpaceMinY = playerSpaceMinY;
                PlayerSpaceMaxY = playerSpaceMaxY;
            }
            else
            {
                PlayerSpaceMinY = playerFloorCellY;
                PlayerSpaceMaxY = playerFloorCellY;
            }
            PlayerBlockingBuildingIds = playerBlockingBuildingIds ?? new HashSet<int>();
            VisibleBelowCells = visibleBelowCells ?? new HashSet<(int x, int z, int y)>();
            PlayerSpaceFloorCells = playerSpaceFloorCells ?? new HashSet<Vector3Int>();
        }

        public bool Equals(FloorVisibilityContext other) =>
            IsPlayerOutdoor == other.IsPlayerOutdoor &&
            PlayerFloorCellY == other.PlayerFloorCellY &&
            MinCellY == other.MinCellY &&
            PlayerBuildingId == other.PlayerBuildingId &&
            PlayerSpaceId == other.PlayerSpaceId &&
            PlayerSpaceMinY == other.PlayerSpaceMinY &&
            PlayerSpaceMaxY == other.PlayerSpaceMaxY &&
            SetEquals(PlayerBlockingBuildingIds, other.PlayerBlockingBuildingIds) &&
            SetEquals(VisibleBelowCells, other.VisibleBelowCells);

        public override bool Equals(object obj) => obj is FloorVisibilityContext other && Equals(other);

        public override int GetHashCode()
        {
            int hash = HashCode.Combine(
                IsPlayerOutdoor,
                PlayerFloorCellY,
                MinCellY,
                PlayerBuildingId,
                PlayerSpaceId,
                PlayerSpaceMinY,
                PlayerSpaceMaxY);
            hash = HashCombineSet(hash, PlayerBlockingBuildingIds);
            return HashCombineBelowCells(hash, VisibleBelowCells);
        }

        public static bool operator ==(FloorVisibilityContext left, FloorVisibilityContext right) =>
            left.Equals(right);

        public static bool operator !=(FloorVisibilityContext left, FloorVisibilityContext right) =>
            !left.Equals(right);

        static bool SetEquals(HashSet<int> a, HashSet<int> b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null || a.Count != b.Count)
                return false;

            foreach (int v in a)
            {
                if (!b.Contains(v))
                    return false;
            }

            return true;
        }

        static bool SetEquals(
            HashSet<(int x, int z, int y)> a,
            HashSet<(int x, int z, int y)> b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null || a.Count != b.Count)
                return false;

            foreach (var v in a)
            {
                if (!b.Contains(v))
                    return false;
            }

            return true;
        }

        static int HashCombineSet(int hash, HashSet<int> set)
        {
            if (set == null)
                return hash;

            foreach (int v in set)
                hash = HashCode.Combine(hash, v);

            return hash;
        }

        static int HashCombineBelowCells(int hash, HashSet<(int x, int z, int y)> set)
        {
            if (set == null)
                return hash;

            foreach (var (x, z, y) in set)
                hash = HashCode.Combine(hash, x, z, y);

            return hash;
        }
    }

    /// <summary>플레이어 월드 높이·그리드 XZ 기준 층(Y) 타일 가시성. XZ 청크 스트리밍과 분리.</summary>
    public sealed class PlayerFloorVisibilityPolicy
    {
        static readonly HashSet<int> EmptyBlocking = new();
        static readonly HashSet<(int x, int z, int y)> EmptyVisibleBelow = new();
        static readonly HashSet<Vector3Int> EmptySpaceFloorCells = new();

        readonly float _cellSize;
        readonly float _cellEpsilonWorld;
        readonly int _minCellY;
        readonly TileMapCacheHub _hub;
        readonly IndoorTileVisibilityPipeline _indoor = new();
        readonly OutdoorTileVisibilityPipeline _outdoor = new();
        readonly BlockingBuildingFullHideLayer _blockingBuildingHide;

        readonly HashSet<int> _proximityBlockingScratch = new();
        readonly HashSet<(int x, int z, int y)> _visibleBelowScratch = new();

        HashSet<int> _blockingForContext = new();
        HashSet<(int x, int z, int y)> _visibleBelowForContext = new();

        SightLineBuildingDebugSnapshot _lastProximityBlockingDebug = SightLineBuildingDebugSnapshot.Empty;

        Vector3Int _cachedPlayerOccupiedCell;
        Vector3Int _cachedPlayerFeetCell;
        Vector3Int _cachedPlayerFootprint = DefaultPlayerFootprint;
        bool _cachedHideEnabled;
        bool _hasStableIdentity;
        bool _cachedIsOutdoor;
        int _cachedPlayerBuildingId;
        int _cachedPlayerSpaceId;
        int _cachedPlayerSpaceMinY;
        int _cachedPlayerSpaceMaxY;
        HashSet<Vector3Int> _cachedPlayerSpaceFloorCells = EmptySpaceFloorCells;
        HashSet<(int x, int z, int y)> _cachedVisibleBelow = new();

        /// <summary>시선상 가림 건물 전층 Hide(실내·야외 공통). false면 차단 집합을 비웁니다.</summary>
        public bool OutdoorSightLineBuildingHideEnabled { get; set; } = true;

        private PlayerFloorVisibilityPolicy(
            float cellSize,
            float cellEpsilonWorld,
            int minCellY,
            TileMapCacheHub hub,
            BuildingGroupRegistry buildingRegistry)
        {
            _cellSize = cellSize;
            _cellEpsilonWorld = cellEpsilonWorld;
            _minCellY = minCellY;
            _hub = hub;
            _blockingBuildingHide = new BlockingBuildingFullHideLayer(buildingRegistry);
        }

        public int MinCellY => _minCellY;

        public TileMapCacheHub MapCache => _hub;

        public float CellSize => _cellSize;

        public SightLineBuildingDebugSnapshot LastSightLineDebug => _lastProximityBlockingDebug;

        /// <summary>근접 Evaluate 에드온이 매 프레임 주입하는 야외 blocking buildingId.</summary>
        public void SetProximityBlockingBuildingIds(
            HashSet<int> blockingBuildingIds,
            SightLineBuildingDebugSnapshot debugSnapshot)
        {
            _proximityBlockingScratch.Clear();
            if (blockingBuildingIds != null)
            {
                foreach (int buildingId in blockingBuildingIds)
                    _proximityBlockingScratch.Add(buildingId);
            }

            _lastProximityBlockingDebug = debugSnapshot;
        }

        public static PlayerFloorVisibilityPolicy Build(
            TileMapCacheHub hub,
            float cellSize,
            BuildingGroupRegistry buildingRegistry,
            float cellEpsilonWorld = 0f,
            float groundPlaneY = 0f)
        {
            if (hub == null)
                throw new ArgumentNullException(nameof(hub));

            if (cellSize <= 0f)
                cellSize = 1f;

            _ = groundPlaneY;
            int minCellY = OccupiedCellCoord.ResolveMinStructuralFloorCellY(hub);

            return new PlayerFloorVisibilityPolicy(
                cellSize,
                cellEpsilonWorld,
                minCellY,
                hub,
                buildingRegistry);
        }

        public Vector3Int ResolvePlayerOccupiedCell(float playerHeightWorldY, Vector3 playerWorld) =>
            OccupiedCellCoord.ResolveFromWorld(
                _hub, playerWorld, _cellSize, playerHeightWorldY, _cellEpsilonWorld, _minCellY);

        public void AppendPlayerOccupiedCells(
            Vector3Int feetCell,
            Vector3Int footprint,
            ICollection<Vector3Int> cells) =>
            AppendPlayerFootprintOccupiedCells(feetCell, footprint, cells);

        public (int minY, int maxY) GetPlayerFootprintVerticalBand(Vector3Int feetCell, Vector3Int footprint) =>
            GetPlayerFootprintVerticalBandInternal(feetCell, footprint);

        public bool IsPlayerOccupiedCell(Vector3Int cell, Vector3Int feetCell, Vector3Int footprint) =>
            PlayerFootprintContains(feetCell, footprint, cell);

        public FloorVisibilityContext ResolveContext(
            float playerHeightWorldY,
            Vector3 playerWorld) =>
            ResolveContext(
                playerHeightWorldY,
                playerWorld,
                ResolvePlayerOccupiedCell(playerHeightWorldY, playerWorld),
                DefaultPlayerFootprint);

        public FloorVisibilityContext ResolveContext(
            float playerHeightWorldY,
            Vector3 playerWorld,
            Vector3Int feetCell,
            Vector3Int footprint)
        {
            footprint = ClampPlayerFootprint(footprint);
            Vector3Int playerOccupiedCell = feetCell;
            int playerFloorCellY = feetCell.y;
            GetPlayerFootprintVerticalBand(feetCell, footprint, out int footprintMinY, out int footprintMaxY);

            bool reuseIdentity = _hasStableIdentity &&
                playerOccupiedCell == _cachedPlayerOccupiedCell &&
                feetCell == _cachedPlayerFeetCell &&
                footprint == _cachedPlayerFootprint &&
                OutdoorSightLineBuildingHideEnabled == _cachedHideEnabled;

            bool isOutdoor;
            int playerBuildingId;
            int playerSpaceId;
            int playerSpaceMinY;
            int playerSpaceMaxY;
            HashSet<Vector3Int> playerSpaceFloorCells;
            HashSet<(int x, int z, int y)> visibleBelow;

            if (reuseIdentity)
            {
                isOutdoor = _cachedIsOutdoor;
                playerBuildingId = _cachedPlayerBuildingId;
                playerSpaceId = _cachedPlayerSpaceId;
                playerSpaceMinY = _cachedPlayerSpaceMinY;
                playerSpaceMaxY = _cachedPlayerSpaceMaxY;
                playerSpaceFloorCells = _cachedPlayerSpaceFloorCells ?? EmptySpaceFloorCells;
                visibleBelow = _cachedVisibleBelow.Count == 0 ? EmptyVisibleBelow : _cachedVisibleBelow;
            }
            else
            {
                isOutdoor = _hub.IsOutdoorEvaluation(
                    playerFloorCellY, playerOccupiedCell.x, playerOccupiedCell.z);
                _hub.TryGetFloorBuildingRoom(
                    playerFloorCellY, playerOccupiedCell.x, playerOccupiedCell.z,
                    out playerBuildingId, out _);
                ResolvePlayerSpace(
                    playerOccupiedCell,
                    playerFloorCellY,
                    footprintMinY,
                    footprintMaxY,
                    out playerSpaceId,
                    out playerSpaceMinY,
                    out playerSpaceMaxY,
                    out playerSpaceFloorCells);

                visibleBelow = isOutdoor
                    ? EmptyVisibleBelow
                    : ResolveVisibleBelowForContext(
                        playerFloorCellY, playerOccupiedCell.x, playerOccupiedCell.z);

                _cachedIsOutdoor = isOutdoor;
                _cachedPlayerBuildingId = playerBuildingId;
                _cachedPlayerSpaceId = playerSpaceId;
                _cachedPlayerSpaceMinY = playerSpaceMinY;
                _cachedPlayerSpaceMaxY = playerSpaceMaxY;
                _cachedPlayerSpaceFloorCells = playerSpaceFloorCells ?? EmptySpaceFloorCells;
                CopyVisibleBelow(visibleBelow, _cachedVisibleBelow);
                _cachedPlayerOccupiedCell = playerOccupiedCell;
                _cachedPlayerFeetCell = feetCell;
                _cachedPlayerFootprint = footprint;
                _cachedHideEnabled = OutdoorSightLineBuildingHideEnabled;
                _hasStableIdentity = true;
            }

            HashSet<int> blocking = ResolveBlockingForContext(isOutdoor);

            return new FloorVisibilityContext(
                isOutdoor,
                playerFloorCellY,
                _minCellY,
                playerBuildingId,
                blocking,
                visibleBelow,
                playerSpaceId,
                playerSpaceMinY,
                playerSpaceMaxY,
                playerSpaceFloorCells);
        }

        void ResolvePlayerSpace(
            Vector3Int playerOccupiedCell,
            int playerFloorCellY,
            int footprintMinY,
            int footprintMaxY,
            out int playerSpaceId,
            out int playerSpaceMinY,
            out int playerSpaceMaxY,
            out HashSet<Vector3Int> playerSpaceFloorCells)
        {
            playerSpaceId = 0;
            playerSpaceMinY = playerFloorCellY;
            playerSpaceMaxY = playerFloorCellY;
            playerSpaceFloorCells = EmptySpaceFloorCells;

            if (!_hub.Spaces.TryGetSpaceAtFloorCell(playerOccupiedCell, out int spaceId) ||
                !_hub.Spaces.TryGetSpace(spaceId, out SpaceBakeResult space) ||
                !space.HasFloorBounds)
            {
                playerSpaceMinY = Mathf.Min(playerSpaceMinY, footprintMinY);
                playerSpaceMaxY = Mathf.Max(playerSpaceMaxY, footprintMaxY);
                return;
            }

            playerSpaceId = spaceId;
            playerSpaceMinY = Mathf.Min(space.MinFloorY, footprintMinY);
            playerSpaceMaxY = Mathf.Max(space.MaxFloorY, footprintMaxY);

            IReadOnlyCollection<Vector3Int> cells = _hub.Spaces.Registry.GetFloorCells(spaceId);
            playerSpaceFloorCells = cells as HashSet<Vector3Int> ?? new HashSet<Vector3Int>(cells);
        }

        static void GetPlayerFootprintVerticalBand(
            Vector3Int feetCell,
            Vector3Int footprint,
            out int minY,
            out int maxY) =>
            (minY, maxY) = GetPlayerFootprintVerticalBandInternal(feetCell, footprint);

        static readonly Vector3Int DefaultPlayerFootprint = new Vector3Int(1, 2, 1);

        static Vector3Int ClampPlayerFootprint(Vector3Int footprint) =>
            new Vector3Int(
                Mathf.Max(1, footprint.x),
                Mathf.Max(1, footprint.y),
                Mathf.Max(1, footprint.z));

        static bool TryGetPlayerFootprintAnchor(
            Vector3Int feetCell,
            Vector3Int footprint,
            out Vector3Int anchor)
        {
            footprint = ClampPlayerFootprint(footprint);
            anchor = new Vector3Int(
                feetCell.x - (footprint.x - 1) / 2,
                feetCell.y,
                feetCell.z - (footprint.z - 1) / 2);
            return true;
        }

        static void AppendPlayerFootprintOccupiedCells(
            Vector3Int feetCell,
            Vector3Int footprint,
            ICollection<Vector3Int> cells)
        {
            if (cells == null || !TryGetPlayerFootprintAnchor(feetCell, footprint, out Vector3Int anchor))
                return;

            TileIdentityUtil.AppendOccupiedCellBox(anchor, ClampPlayerFootprint(footprint), cells);
        }

        static (int minY, int maxY) GetPlayerFootprintVerticalBandInternal(
            Vector3Int feetCell,
            Vector3Int footprint)
        {
            footprint = ClampPlayerFootprint(footprint);
            TryGetPlayerFootprintAnchor(feetCell, footprint, out Vector3Int anchor);
            return (anchor.y, anchor.y + footprint.y - 1);
        }

        static bool PlayerFootprintContains(Vector3Int feetCell, Vector3Int footprint, Vector3Int cell)
        {
            footprint = ClampPlayerFootprint(footprint);
            if (!TryGetPlayerFootprintAnchor(feetCell, footprint, out Vector3Int anchor))
                return false;

            return cell.x >= anchor.x && cell.x < anchor.x + footprint.x
                && cell.y >= anchor.y && cell.y < anchor.y + footprint.y
                && cell.z >= anchor.z && cell.z < anchor.z + footprint.z;
        }

        static void CopyVisibleBelow(
            HashSet<(int x, int z, int y)> source,
            HashSet<(int x, int z, int y)> dest)
        {
            dest.Clear();
            if (source == null || source.Count == 0)
                return;

            foreach (var cell in source)
                dest.Add(cell);
        }

        HashSet<int> ResolveBlockingForContext(bool isPlayerOutdoor)
        {
            if (!OutdoorSightLineBuildingHideEnabled || !isPlayerOutdoor || _proximityBlockingScratch.Count == 0)
            {
                _blockingForContext = EmptyBlocking;
                return _blockingForContext;
            }

            _blockingForContext = new HashSet<int>(_proximityBlockingScratch);
            return _blockingForContext;
        }

        HashSet<(int x, int z, int y)> ResolveVisibleBelowForContext(int playerFloorCellY, int gridX, int gridZ)
        {
            BuildVisibleBelowCells(playerFloorCellY, gridX, gridZ);
            if (SetEqualsBelowCells(_visibleBelowScratch, _visibleBelowForContext))
                return _visibleBelowForContext;

            if (_visibleBelowScratch.Count == 0)
            {
                _visibleBelowForContext = EmptyVisibleBelow;
                return _visibleBelowForContext;
            }

            _visibleBelowForContext = new HashSet<(int x, int z, int y)>(_visibleBelowScratch);
            return _visibleBelowForContext;
        }

        static bool SetEqualsBelowCells(
            HashSet<(int x, int z, int y)> a,
            HashSet<(int x, int z, int y)> b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null || a.Count != b.Count)
                return false;
            foreach (var cell in a)
            {
                if (!b.Contains(cell))
                    return false;
            }
            return true;
        }

        public bool IsTileVisible(TileData tile, in FloorVisibilityContext ctx)
        {
            if (_blockingBuildingHide.Evaluate(tile, ctx) == TileVisibilityVerdict.Hide)
                return false;

            if (ctx.IsPlayerOutdoor)
                return _outdoor.IsVisible(tile, ctx);

            if (ctx.PlayerBuildingId <= 0 || tile.identity.buildingId != ctx.PlayerBuildingId)
                return true;

            return _indoor.IsVisible(tile, ctx);
        }

        /// <summary>플레이어 월드 높이 → 점유 층 cellY (층 가시성·room 조회 공용).</summary>
        public int ResolvePlayerFloorCellY(float playerHeightWorldY, Vector3 playerWorld) =>
            ResolvePlayerOccupiedCell(playerHeightWorldY, playerWorld).y;

        /// <summary>XZ만 있을 때 Y는 <see cref="ResolvePlayerOccupiedCell"/>과 동일 규칙으로 world.y 기준.</summary>
        public int ResolvePlayerFloorCellY(float playerHeightWorldY) =>
            ResolvePlayerFloorCellY(playerHeightWorldY, new Vector3(0f, playerHeightWorldY, 0f));

        void BuildVisibleBelowCells(int playerFloorCellY, int gridX, int gridZ)
        {
            _visibleBelowScratch.Clear();
            if (playerFloorCellY <= _minCellY)
                return;

            FloorBfsResult top = _hub.GetRoomGeometryForCell(
                playerFloorCellY, gridX, gridZ, FloorRoomBfsProfile.Visibility).Result;
            foreach (var (holeX, holeZ) in top.EmptyDiscovered)
                AddVisibleThroughHole(holeX, holeZ, playerFloorCellY);
        }

        private void AddVisibleThroughHole(int holeX, int holeZ, int playerFloorCellY)
        {
            for (int k = playerFloorCellY - 1; k >= _minCellY; k--)
            {
                if (!_hub.CellHasOccupancy(holeX, holeZ, k))
                    continue;

                HashSet<(int x, int z)> room = _hub.GetVisitedForCell(
                    k, holeX, holeZ, FloorRoomBfsProfile.Visibility);
                foreach (var (vx, vz) in room)
                    _visibleBelowScratch.Add((vx, vz, k));
                return;
            }
        }
    }
}
