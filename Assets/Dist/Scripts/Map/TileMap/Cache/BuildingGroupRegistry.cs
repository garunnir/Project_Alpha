// ============================================================
// BuildingGroupRegistry — buildingId 역인덱스·광장 바닥 집합·room별 EdgeWall
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    public sealed class BuildingGroupRegistry
    {
        static readonly Guid[] EmptyEdgeGuids = Array.Empty<Guid>();

        readonly Dictionary<int, HashSet<Guid>> _tilesByBuildingId = new();
        readonly Dictionary<int, HashSet<Guid>> _minCellYFloorTilesByBuildingId = new();
        readonly Dictionary<int, BuildingExtent> _extentsByBuildingId = new();
        readonly Dictionary<RoomKey, HashSet<Guid>> _edgeIdsByRoom = new();
        /// <summary>야외 레이어 walkable floor 칸 (Y 제한 없음). minCellY plaza BFS 아님.</summary>
        readonly HashSet<Vector3Int> _outdoorFloorCells = new();

        public int NextBuildingId { get; private set; } = 1;

        public IReadOnlyDictionary<int, HashSet<Guid>> TilesByBuildingId => _tilesByBuildingId;

        /// <summary>레거시 이름 — outdoor 레이어에 칸이 있으면 그 중 임의 Y(없으면 MinValue).</summary>
        public int PlazaCellY
        {
            get
            {
                int y = int.MaxValue;
                foreach (Vector3Int c in _outdoorFloorCells)
                {
                    if (c.y < y)
                        y = c.y;
                }

                return y == int.MaxValue ? int.MinValue : y;
            }
        }

        public IReadOnlyCollection<Vector3Int> OutdoorFloorCells => _outdoorFloorCells;

        /// <summary>레거시: outdoor 레이어 (x,z) 투영. 같은 XZ에 outdoor 칸이 있으면 포함.</summary>
        public IReadOnlyCollection<(int x, int z)> PlazaFloorXZ
        {
            get
            {
                var set = new HashSet<(int x, int z)>();
                foreach (Vector3Int c in _outdoorFloorCells)
                    set.Add((c.x, c.z));
                return set;
            }
        }

        public void Clear()
        {
            _tilesByBuildingId.Clear();
            _minCellYFloorTilesByBuildingId.Clear();
            _extentsByBuildingId.Clear();
            _edgeIdsByRoom.Clear();
            // outdoor 레이어는 AssignAll Clear 동안 유지 — Replace/ClearOutdoor로만 교체
            NextBuildingId = 1;
        }

        public void ClearOutdoorFloorCells() => _outdoorFloorCells.Clear();

        public void ReplaceOutdoorFloorCells(IEnumerable<Vector3Int> cells)
        {
            _outdoorFloorCells.Clear();
            if (cells == null)
                return;
            foreach (Vector3Int c in cells)
                _outdoorFloorCells.Add(c);
        }

        public void AddOutdoorFloorCell(Vector3Int cell) => _outdoorFloorCells.Add(cell);

        public bool IsOutdoorFloorCell(Vector3Int cell) => _outdoorFloorCells.Contains(cell);

        public bool IsOutdoorFloorCell(int cellY, int x, int z) =>
            _outdoorFloorCells.Contains(new Vector3Int(x, cellY, z));

        /// <summary>야외 레이어 칸이면 true (구 plaza API).</summary>
        public bool IsPlazaFloor(int cellY, int x, int z) =>
            IsOutdoorFloorCell(cellY, x, z);

        public bool IsPlazaXZ(int x, int z)
        {
            foreach (Vector3Int c in _outdoorFloorCells)
            {
                if (c.x == x && c.z == z)
                    return true;
            }

            return false;
        }

        [System.Obsolete("Use ReplaceOutdoorFloorCells — minCellY plaza BFS removed.")]
        public void SetPlazaOutdoor(int plazaCellY, HashSet<(int x, int z)> plazaFloor)
        {
            _ = plazaCellY;
            _outdoorFloorCells.Clear();
            if (plazaFloor == null)
                return;
            foreach (var (x, z) in plazaFloor)
                _outdoorFloorCells.Add(new Vector3Int(x, 0, z));
        }

        public int AllocateBuildingId() => NextBuildingId++;

        /// <summary>
        /// 저장된 양수 buildingId와 Allocate 충돌 방지.
        /// AssignAll이 Reset 없이 돌 때 Clear로 Next=1이 된 뒤 호출.
        /// </summary>
        public void SyncNextBuildingIdFromTiles(IEnumerable<TileData> tiles)
        {
            if (tiles == null)
                return;

            int maxBuildingId = 0;
            foreach (TileData tile in tiles)
            {
                if (tile.identity.buildingId > maxBuildingId)
                    maxBuildingId = tile.identity.buildingId;
            }

            if (maxBuildingId >= NextBuildingId)
                NextBuildingId = maxBuildingId + 1;
        }

        public void RegisterTile(Guid tileId, int buildingId)
        {
            if (buildingId <= 0)
                return;

            if (!_tilesByBuildingId.TryGetValue(buildingId, out var set))
            {
                set = new HashSet<Guid>();
                _tilesByBuildingId[buildingId] = set;
            }

            set.Add(tileId);
        }

        public void UnregisterTile(Guid tileId, int buildingId)
        {
            if (buildingId <= 0)
                return;

            if (_tilesByBuildingId.TryGetValue(buildingId, out var set))
            {
                set.Remove(tileId);
                if (set.Count == 0)
                    _tilesByBuildingId.Remove(buildingId);
            }
        }

        public void RegisterEdgeForRoom(RoomKey roomKey, Guid edgeTileId)
        {
            if (roomKey.BuildingId <= 0 || roomKey.RoomId <= 0)
                return;

            if (!_edgeIdsByRoom.TryGetValue(roomKey, out var set))
            {
                set = new HashSet<Guid>();
                _edgeIdsByRoom[roomKey] = set;
            }

            set.Add(edgeTileId);
        }

        public void ClearEdgeIndexForSlice(int buildingId, int cellY)
        {
            if (buildingId <= 0)
                return;

            var toRemove = new List<RoomKey>();
            foreach (var kv in _edgeIdsByRoom)
            {
                if (kv.Key.BuildingId == buildingId && kv.Key.CellY == cellY)
                    toRemove.Add(kv.Key);
            }

            for (int i = 0; i < toRemove.Count; i++)
                _edgeIdsByRoom.Remove(toRemove[i]);
        }

        public bool TryGetEdgeWallIds(RoomKey roomKey, out IReadOnlyCollection<Guid> edgeIds)
        {
            if (_edgeIdsByRoom.TryGetValue(roomKey, out var set))
            {
                edgeIds = set;
                return true;
            }

            edgeIds = EmptyEdgeGuids;
            return false;
        }

        public void RebuildFromTiles(IEnumerable<TileData> tiles) =>
            RebuildIndicesFromTiles(tiles);

        /// <summary>tile guid 역인덱스·최하층 floor·<see cref="BuildingExtent"/>를 한 패스로 재구성합니다.</summary>
        public void RebuildIndicesFromTiles(IEnumerable<TileData> tiles)
        {
            _tilesByBuildingId.Clear();
            _minCellYFloorTilesByBuildingId.Clear();
            _extentsByBuildingId.Clear();

            if (tiles == null)
                return;

            var extentBuilders = new Dictionary<int, BuildingExtent.Builder>();
            int maxBuildingId = 0;

            foreach (var tile in tiles)
            {
                int buildingId = tile.identity.buildingId;
                if (buildingId <= 0)
                    continue;

                if (buildingId > maxBuildingId)
                    maxBuildingId = buildingId;

                RegisterTile(tile.tileDefId, buildingId);

                if (!extentBuilders.TryGetValue(buildingId, out var builder))
                {
                    builder = new BuildingExtent.Builder(buildingId);
                    extentBuilders[buildingId] = builder;
                }

                builder.IncludeTile(tile);
            }

            foreach (var kv in extentBuilders)
            {
                var extent = kv.Value.Build();
                if (extent.HasBounds)
                    _extentsByBuildingId[kv.Key] = extent;

                foreach (Guid tileId in kv.Value.MinFloorTileIds)
                    RegisterMinCellYFloorTile(kv.Key, tileId);
            }

            if (maxBuildingId >= NextBuildingId)
                NextBuildingId = maxBuildingId + 1;
        }

        public bool TryGetBuildingExtent(int buildingId, out BuildingExtent extent)
        {
            if (buildingId > 0 && _extentsByBuildingId.TryGetValue(buildingId, out extent))
                return true;

            extent = BuildingExtent.Empty;
            return false;
        }

        [Obsolete("Use RebuildIndicesFromTiles — min floor index is included.")]
        public void RebuildMinCellYFloorIndex(IEnumerable<TileData> tiles, int MinCellY)
        {
            _ = MinCellY;
            RebuildIndicesFromTiles(tiles);
        }

        public HashSet<Guid> GetTilesForBuilding(int buildingId)
        {
            if (buildingId <= 0 || !_tilesByBuildingId.TryGetValue(buildingId, out var set))
                return new HashSet<Guid>();

            return new HashSet<Guid>(set);
        }

        public void EnumerateTilesForBuilding(int buildingId, Action<Guid> visitor)
        {
            if (buildingId <= 0 || visitor == null ||
                !_tilesByBuildingId.TryGetValue(buildingId, out var set))
                return;

            foreach (Guid tileId in set)
                visitor(tileId);
        }

        public IReadOnlyCollection<Guid> GetTileIdsReadOnly(int buildingId)
        {
            if (buildingId <= 0 || !_tilesByBuildingId.TryGetValue(buildingId, out var set))
                return Array.Empty<Guid>();

            return set;
        }

        public bool IsBottomFloorTile(int buildingId, Guid tileId)
        {
            if (buildingId <= 0 ||
                !_minCellYFloorTilesByBuildingId.TryGetValue(buildingId, out var set))
                return false;

            return set.Contains(tileId);
        }

        public void RegisterMinCellYFloorTile(int buildingId, Guid tileId)
        {
            if (buildingId <= 0)
                return;

            if (!_minCellYFloorTilesByBuildingId.TryGetValue(buildingId, out var set))
            {
                set = new HashSet<Guid>();
                _minCellYFloorTilesByBuildingId[buildingId] = set;
            }

            set.Add(tileId);
        }

        public IReadOnlyCollection<Guid> GetMinCellYFloorTilesForBuilding(int buildingId)
        {
            if (buildingId <= 0 ||
                !_minCellYFloorTilesByBuildingId.TryGetValue(buildingId, out var set))
                return Array.Empty<Guid>();

            return set;
        }
    }
}
