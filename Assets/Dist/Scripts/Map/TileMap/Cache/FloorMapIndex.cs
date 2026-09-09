// ============================================================
// FloorMapIndex — 셀 Y별 타일·바닥 face·벽·엣지 조회 스냅샷
// ============================================================
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    public sealed class FloorMapIndex
    {
        private static readonly Vector3Int[] CardinalNeighbors =
        {
            Vector3Int.right, Vector3Int.back, Vector3Int.left, Vector3Int.forward
        };

        /// <summary>
        /// 앵커 셀 → 타일 리스트(런타임에서도 동일 List 인스턴스를 유지). Floor는 포함하지 않습니다.
        /// </summary>
        readonly Dictionary<Vector3Int, List<TileData>> _tiles;
        readonly IReadOnlyDictionary<WallEdgeKey, TileData> _edges;
        readonly IReadOnlyDictionary<FloorFaceKey, TileData> _faces;

        /// <summary>
        /// 점유 셀 → (원본 List, 인덱스)들의 목록.
        /// TileData는 struct이므로, 점유 셀 조회 시 매번 원본을 읽어 최신 buildingId 값을 반영합니다.
        /// </summary>
        Dictionary<Vector3Int, OccupiedCellEntry> _occupiedEntries = new();

        readonly HashSet<(int x, int z, int y)> _anyTileAt = new();
        readonly Dictionary<Vector3Int, List<WallEdgeKey>> _wallKeysAtCell = new();
        readonly Dictionary<Vector3Int, List<FloorFaceKey>> _floorKeysAtCell = new();
        /// <summary>(x,z) 컬럼 → walkable floor cellY 오름차순. 파티클 착지 조회용.</summary>
        readonly Dictionary<(int x, int z), List<int>> _walkableFloorYsByColumn = new();
        readonly HashSet<System.Guid> _collectDedupeScratch = new();

        readonly struct TileRef
        {
            public readonly List<TileData> OwnerList;
            public readonly int OwnerIndex;

            public TileRef(List<TileData> ownerList, int ownerIndex)
            {
                OwnerList = ownerList;
                OwnerIndex = ownerIndex;
            }
        }

        sealed class OccupiedCellEntry
        {
            public readonly List<TileRef> Refs = new();
            public readonly List<TileData> Scratch = new();
        }

        public FloorMapIndex(
            Dictionary<Vector3Int, List<TileData>> tiles,
            IReadOnlyDictionary<WallEdgeKey, TileData> edges,
            IReadOnlyDictionary<FloorFaceKey, TileData> faces)
        {
            _tiles = tiles;
            _edges = edges ?? new Dictionary<WallEdgeKey, TileData>();
            _faces = faces ?? new Dictionary<FloorFaceKey, TileData>();
            RebuildOccupancy();
        }

        public static FloorMapIndex FromModel(TileMapModel model) =>
            new FloorMapIndex(model.tiles, model.FaceBinder.WallFaceIndex, model.FaceBinder.FloorFaceIndex);

        public bool HasAnyTile(int x, int z, int y) => _anyTileAt.Contains((x, z, y));

        public IEnumerable<(int x, int z, int y)> EnumerateOccupiedCells() => _anyTileAt;

        /// <summary>런타임 topology 변경 후 (x,z,y) 점유 집합을 <see cref="_tiles"/>와 맞춥니다.</summary>
        public void SyncOccupancyForCell(int x, int z, int y) => RebuildOccupancy();

        public void SyncOccupancyFromChangedCells(IEnumerable<Vector3Int> changedCells) => RebuildOccupancy();

        public void RebuildOccupancy()
        {
            _anyTileAt.Clear();
            _occupiedEntries.Clear();
            _wallKeysAtCell.Clear();
            _floorKeysAtCell.Clear();
            _walkableFloorYsByColumn.Clear();

            foreach (var kv in _tiles)
            {
                var list = kv.Value;
                if (list == null || list.Count == 0)
                    continue;

                for (int i = 0; i < list.Count; i++)
                {
                    TileData tile = list[i];
                    if (TileIdentityUtil.IsHorizontalFace(tile.identity))
                        continue;

                    int sx = tile.identity.sizeUnit.x;
                    int sy = tile.identity.sizeUnit.y;
                    int sz = tile.identity.sizeUnit.z;

                    if (sx < 1) sx = 1;
                    if (sy < 1) sy = 1;
                    if (sz < 1) sz = 1;

                    Vector3Int basePos = tile.identity.GridPos;
                    for (int dx = 0; dx < sx; dx++)
                    {
                        for (int dy = 0; dy < sy; dy++)
                        {
                            for (int dz = 0; dz < sz; dz++)
                            {
                                var cell = new Vector3Int(basePos.x + dx, basePos.y + dy, basePos.z + dz);
                                if (!_occupiedEntries.TryGetValue(cell, out var entry))
                                {
                                    entry = new OccupiedCellEntry();
                                    _occupiedEntries[cell] = entry;
                                }

                                entry.Refs.Add(new TileRef(list, i));
                            }
                        }
                    }
                }
            }

            foreach (var kv in _occupiedEntries)
            {
                if (kv.Value.Refs.Count > 0)
                    _anyTileAt.Add((kv.Key.x, kv.Key.z, kv.Key.y));
            }

            foreach (var kv in _edges)
            {
                var edgeKey = kv.Key;
                int sy = kv.Value.identity.sizeUnit.y;
                if (sy < 1) sy = 1;

                for (int dy = 0; dy < sy; dy++)
                {
                    var yOffset = new Vector3Int(0, dy, 0);
                    var cellA = edgeKey.CellA + yOffset;
                    var cellB = edgeKey.CellB + yOffset;
                    _anyTileAt.Add((cellA.x, cellA.z, cellA.y));
                    _anyTileAt.Add((cellB.x, cellB.z, cellB.y));
                    RegisterWallIncident(cellA, edgeKey);
                    RegisterWallIncident(cellB, edgeKey);
                }
            }

            foreach (var kv in _faces)
            {
                var faceKey = kv.Key;
                int sy = kv.Value.identity.sizeUnit.y;
                if (sy < 1) sy = 1;

                bool providesLogicalFloor = TileCollisionFlagsUtil.Has(
                    kv.Value.identity.collisionFlags,
                    TileCollisionFlags.ProvidesLogicalFloor);

                for (int dy = 0; dy < sy; dy++)
                {
                    var yOffset = new Vector3Int(0, dy, 0);
                    var below = faceKey.CellBelow + yOffset;
                    var above = faceKey.CellAbove + yOffset;
                    _anyTileAt.Add((below.x, below.z, below.y));
                    _anyTileAt.Add((above.x, above.z, above.y));
                    RegisterFloorIncident(below, faceKey);
                    RegisterFloorIncident(above, faceKey);

                    if (providesLogicalFloor)
                        RegisterWalkableFloorColumn(above.x, above.z, above.y);
                }
            }

            RegisterWalkableStratumBlockColumns();

            foreach (var kv in _walkableFloorYsByColumn)
                kv.Value.Sort();
        }

        /// <summary>
        /// (x,z) 컬럼에서 <paramref name="maxCellY"/> 이하 최상단 walkable floor cellY.
        /// 없으면 false (void — minCellY fallback 금지).
        /// </summary>
        public bool TryGetHighestWalkableFloorAtOrBelow(
            int x,
            int z,
            int maxCellY,
            out int floorCellY)
        {
            floorCellY = 0;
            if (!_walkableFloorYsByColumn.TryGetValue((x, z), out var ys) || ys == null || ys.Count == 0)
                return false;

            int lo = 0;
            int hi = ys.Count - 1;
            int best = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int y = ys[mid];
                if (y <= maxCellY)
                {
                    best = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            if (best < 0)
                return false;

            floorCellY = ys[best];
            return true;
        }

        void RegisterWalkableStratumBlockColumns()
        {
            foreach (var kv in _tiles)
            {
                var list = kv.Value;
                if (list == null || list.Count == 0)
                    continue;

                for (int i = 0; i < list.Count; i++)
                {
                    TileData tile = list[i];
                    if (!MapDigTerrainUtil.IsWalkableSupportBlock(tile.identity))
                        continue;

                    if (!TilePrefabDB.TryResolveDefinition(tile.identity.PrefabId, out TileDefinition def) ||
                        !MapDigTerrainUtil.IsWalkableStratumBlock(def))
                    {
                        continue;
                    }

                    Vector3Int anchor = tile.identity.GridPos;
                    int sy = tile.identity.sizeUnit.y;
                    if (sy < 1) sy = 1;
                    int walkableY = MapDigTerrainUtil.WalkableCellYFromSupportAnchor(anchor.y, sy);
                    RegisterWalkableFloorColumn(anchor.x, anchor.z, walkableY);
                }
            }
        }

        bool HasWalkableStratumSupportAtAnchor(Vector3Int anchorCell)
        {
            if (!TryCollectTilesAtOccupiedCell(anchorCell, _stratumSupportScratch))
                return false;

            for (int i = 0; i < _stratumSupportScratch.Count; i++)
            {
                TileData tile = _stratumSupportScratch[i];
                if (!MapDigTerrainUtil.IsWalkableSupportBlock(tile.identity))
                    continue;

                if (!TilePrefabDB.TryResolveDefinition(tile.identity.PrefabId, out TileDefinition def) ||
                    !MapDigTerrainUtil.IsWalkableStratumBlock(def))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        readonly List<TileData> _stratumSupportScratch = new();

        void RegisterWalkableFloorColumn(int x, int z, int cellY)
        {
            var key = (x, z);
            if (!_walkableFloorYsByColumn.TryGetValue(key, out var list))
            {
                list = new List<int>(2);
                _walkableFloorYsByColumn[key] = list;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == cellY)
                    return;
            }

            list.Add(cellY);
        }

        /// <summary>
        /// 점유셀에 incident한 OccupiedCell·VerticalFace·HorizontalFace 타일을 모읍니다.
        /// <see cref="HasAnyTile"/> false이면 into를 비우고 false. 중복 tileDefId는 한 번만 넣습니다.
        /// </summary>
        public bool TryCollectTilesAtOccupiedCell(Vector3Int cell, List<TileData> into)
        {
            into.Clear();
            if (!HasAnyTile(cell.x, cell.z, cell.y))
                return false;

            _collectDedupeScratch.Clear();

            if (_occupiedEntries.TryGetValue(cell, out var entry) && entry.Refs.Count > 0)
                AppendOccupiedRefs(entry, into, _collectDedupeScratch);

            if (_wallKeysAtCell.TryGetValue(cell, out var wallKeys))
            {
                for (int i = 0; i < wallKeys.Count; i++)
                {
                    if (!_edges.TryGetValue(wallKeys[i], out var edge))
                        continue;

                    AppendUniqueTile(into, edge, _collectDedupeScratch);
                }
            }

            if (_floorKeysAtCell.TryGetValue(cell, out var floorKeys))
            {
                for (int i = 0; i < floorKeys.Count; i++)
                {
                    if (!_faces.TryGetValue(floorKeys[i], out var face))
                        continue;

                    AppendUniqueTile(into, face, _collectDedupeScratch);
                }
            }

            return into.Count > 0;
        }

        public bool TryCollectTilesAtOccupiedCell(int x, int z, int cellY, List<TileData> into) =>
            TryCollectTilesAtOccupiedCell(new Vector3Int(x, cellY, z), into);

        void RegisterWallIncident(Vector3Int cell, WallEdgeKey key)
        {
            if (!_wallKeysAtCell.TryGetValue(cell, out var keys))
            {
                keys = new List<WallEdgeKey>(2);
                _wallKeysAtCell[cell] = keys;
            }

            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i].Equals(key))
                    return;
            }

            keys.Add(key);
        }

        void RegisterFloorIncident(Vector3Int cell, FloorFaceKey key)
        {
            if (!_floorKeysAtCell.TryGetValue(cell, out var keys))
            {
                keys = new List<FloorFaceKey>(2);
                _floorKeysAtCell[cell] = keys;
            }

            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i].Equals(key))
                    return;
            }

            keys.Add(key);
        }

        static void AppendOccupiedRefs(
            OccupiedCellEntry entry,
            List<TileData> into,
            HashSet<System.Guid> dedupe)
        {
            for (int i = 0; i < entry.Refs.Count; i++)
            {
                var tr = entry.Refs[i];
                AppendUniqueTile(into, tr.OwnerList[tr.OwnerIndex], dedupe);
            }
        }

        static void AppendUniqueTile(List<TileData> into, TileData tile, HashSet<System.Guid> dedupe)
        {
            if (dedupe != null)
            {
                if (!dedupe.Add(tile.tileDefId))
                    return;
            }

            into.Add(tile);
        }

        public bool TryGetCellTiles(int x, int z, int cellY, out List<TileData> list) =>
            TryGetCellTiles(new Vector3Int(x, cellY, z), out list);

        bool TryGetCellTiles(Vector3Int cellPos, out List<TileData> list)
        {
            if (!_occupiedEntries.TryGetValue(cellPos, out var entry) || entry.Refs.Count == 0)
            {
                list = null;
                return false;
            }

            entry.Scratch.Clear();
            for (int i = 0; i < entry.Refs.Count; i++)
            {
                var tr = entry.Refs[i];
                entry.Scratch.Add(tr.OwnerList[tr.OwnerIndex]);
            }

            list = entry.Scratch;
            return true;
        }

        public bool TryGetEdgeBetween(Vector3Int cellA, Vector3Int cellB, out TileData edgeWall)
        {
            edgeWall = default;
            return WallEdgeKey.TryBetween(cellA, cellB, out var edgeKey) &&
                   _edges.TryGetValue(edgeKey, out edgeWall);
        }

        public bool TryGetHorizontalFaceBetween(Vector3Int cellBelow, Vector3Int cellAbove, out TileData face)
        {
            face = default;
            return FloorFaceKey.TryBetween(cellBelow, cellAbove, out var faceKey) &&
                   _faces.TryGetValue(faceKey, out face);
        }

        public bool TryGetFloorFaceForWalkableCell(int x, int cellY, int z, out TileData face) =>
            TryGetHorizontalFaceBetween(
                new Vector3Int(x, cellY - 1, z),
                new Vector3Int(x, cellY, z),
                out face);

        public bool CellHasFloor(int x, int cellY, int z)
        {
            if (TryGetFloorFaceForWalkableCell(x, cellY, z, out var face) &&
                TileCollisionFlagsUtil.Has(
                    face.identity.collisionFlags,
                    TileCollisionFlags.ProvidesLogicalFloor))
            {
                return true;
            }

            return HasWalkableStratumSupportAtAnchor(new Vector3Int(x, cellY - 1, z));
        }

        public IEnumerable<TileData> EnumerateEdgeTiles()
        {
            foreach (var kv in _edges)
                yield return kv.Value;
        }

        public IEnumerable<TileData> EnumerateFaceTiles()
        {
            foreach (var kv in _faces)
                yield return kv.Value;
        }

        /// <summary>등록된 Floor face의 walkable 셀(CellAbove)을 순회합니다.</summary>
        public IEnumerable<(int x, int cellY, int z)> EnumerateWalkableFloorCells()
        {
            foreach (var kv in _faces)
            {
                if (!TileCollisionFlagsUtil.Has(
                        kv.Value.identity.collisionFlags,
                        TileCollisionFlags.ProvidesLogicalFloor))
                    continue;

                var key = kv.Key;
                int sy = kv.Value.identity.sizeUnit.y;
                if (sy < 1) sy = 1;

                for (int dy = 0; dy < sy; dy++)
                {
                    var above = key.CellAbove + new Vector3Int(0, dy, 0);
                    yield return (above.x, above.y, above.z);
                }
            }
        }

        /// <summary>점유 셀 타일 리스트에 Floor collision이 있는지(레거시 호환).</summary>
        public static bool CellHasFloor(IReadOnlyList<TileData> list) =>
            TileCollisionFlagsUtil.CellProvidesLogicalFloor(list);

        public static bool CellHasSolidWall(IReadOnlyList<TileData> list) =>
            TileCollisionFlagsUtil.CellBlocksOccupied(list);

        public bool EdgeBlocksPassage(Vector3Int cellA, Vector3Int cellB)
        {
            if (!TryGetEdgeBetween(cellA, cellB, out var edge))
                return false;

            return TileCollisionFlagsUtil.EdgeBlocksPassage(edge);
        }

        public bool EdgeSeparatesRoom(Vector3Int cellA, Vector3Int cellB)
        {
            if (!TryGetEdgeBetween(cellA, cellB, out var edge))
                return false;

            return TileCollisionFlagsUtil.EdgeSeparatesRoom(edge);
        }

        public Vector3Int ResolveFloorBfsStart(int cellY, int startX, int startZ)
        {
            var start = new Vector3Int(startX, cellY, startZ);
            if (!TryGetCellTiles(startX, startZ, cellY, out var startList) ||
                !CellHasSolidWall(startList))
                return start;

            foreach (var d in CardinalNeighbors)
            {
                int nx = startX + d.x;
                int nz = startZ + d.z;
                if (!TryGetCellTiles(nx, nz, cellY, out var nList))
                    continue;

                if (!CellHasSolidWall(nList))
                    return new Vector3Int(nx, cellY, nz);
            }

            return start;
        }
    }
}
