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
        /// 점유 셀 → (원본 List, 안정적인 tile ID)들의 목록.
        /// TileData는 struct이므로, 점유 셀 조회 시 매번 원본을 읽어 최신 buildingId 값을 반영합니다.
        /// </summary>
        Dictionary<Vector3Int, OccupiedCellEntry> _occupiedEntries = new();

        readonly HashSet<(int x, int z, int y)> _anyTileAt = new();
        readonly Dictionary<Vector3Int, List<WallEdgeKey>> _wallKeysAtCell = new();
        readonly Dictionary<Vector3Int, List<FloorFaceKey>> _floorKeysAtCell = new();
        /// <summary>(x,z) 컬럼 → walkable floor cellY 오름차순. 파티클 착지 조회용.</summary>
        readonly Dictionary<(int x, int z), List<int>> _walkableFloorYsByColumn = new();
        readonly Dictionary<Vector3Int, int> _walkableFloorContributors = new();
        readonly Dictionary<(int x, int z), SortedSet<int>> _columnSealYs = new();
        readonly HashSet<Vector3Int> _editCells = new();
        readonly HashSet<System.Guid> _collectDedupeScratch = new();

        /// <summary>
        /// (x,z) 컬럼 → 그 컬럼에서 가장 높은 "seal" cellY (walkable floor 또는 structural OccupiedCell).
        /// <see cref="SpaceLeakEvaluator"/> 천장 leak O(1) 조회용 — buildingId 무관 전역 인덱스.
        /// </summary>
        readonly Dictionary<(int x, int z), int> _columnSealTopY = new();

        sealed class TileRef
        {
            public readonly List<TileData> OwnerList;
            public readonly System.Guid TileId;
            int _ownerIndex;

            public TileRef(List<TileData> ownerList, int ownerIndex)
            {
                OwnerList = ownerList;
                TileId = ownerList[ownerIndex].tileDefId;
                _ownerIndex = ownerIndex;
            }

            public bool TryRead(out TileData tile)
            {
                if (_ownerIndex < OwnerList.Count && OwnerList[_ownerIndex].tileDefId == TileId)
                {
                    tile = OwnerList[_ownerIndex];
                    return true;
                }
                // Removing a sibling only shifts this anchor's small list, not the map.
                for (int i = 0; i < OwnerList.Count; i++)
                {
                    if (OwnerList[i].tileDefId != TileId)
                        continue;
                    _ownerIndex = i;
                    tile = OwnerList[i];
                    return true;
                }
                tile = default;
                return false;
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

        /// <summary>Store mutation must precede this call. Only this tile's footprint is updated.</summary>
        public void OnTileAdded(TileData tile) => UpdateTile(tile, adding: true);

        public void OnTileRemoved(TileData tile) => UpdateTile(tile, adding: false);

        void UpdateTile(TileData tile, bool adding)
        {
            _editCells.Clear();
            TileIdentityUtil.CollectAffectedCells(tile.identity, _editCells);
            TileRef tileRef = null;
            if (adding && TileIdentityUtil.IsOccupiedCell(tile.identity) &&
                _tiles.TryGetValue(tile.identity.GridPos, out var owner))
            {
                for (int i = 0; i < owner.Count; i++)
                    if (owner[i].tileDefId == tile.tileDefId)
                    {
                        tileRef = new TileRef(owner, i);
                        break;
                    }
            }

            foreach (var cell in _editCells)
            {
                switch (TileIdentityUtil.GetPlacementSlot(tile.identity))
                {
                    case TilePlacementSlot.VerticalFace:
                        var wallKey = WallEdgeKey.FromWallTileIdentity(tile.identity);
                        if (adding) RegisterWallIncident(cell, wallKey);
                        else RemoveIncident(_wallKeysAtCell, cell, wallKey);
                        break;
                    case TilePlacementSlot.HorizontalFace:
                        var floorKey = FloorFaceKey.FromFloorTileIdentity(tile.identity);
                        if (adding) RegisterFloorIncident(cell, floorKey);
                        else RemoveIncident(_floorKeysAtCell, cell, floorKey);
                        break;
                    default:
                        if (adding && tileRef != null)
                        {
                            if (!_occupiedEntries.TryGetValue(cell, out var entry))
                                _occupiedEntries.Add(cell, entry = new OccupiedCellEntry());
                            entry.Refs.Add(tileRef);
                        }
                        else if (_occupiedEntries.TryGetValue(cell, out var entry))
                        {
                            for (int i = entry.Refs.Count - 1; i >= 0; i--)
                                if (entry.Refs[i].TileId == tile.tileDefId)
                                    entry.Refs.RemoveAt(i);
                            if (entry.Refs.Count == 0)
                                _occupiedEntries.Remove(cell);
                        }
                        break;
                }
                var key = (cell.x, cell.z, cell.y);
                if (_occupiedEntries.ContainsKey(cell) || _wallKeysAtCell.ContainsKey(cell) ||
                    _floorKeysAtCell.ContainsKey(cell))
                    _anyTileAt.Add(key);
                else
                    _anyTileAt.Remove(key);
            }

            int delta = adding ? 1 : -1;
            if (TileIdentityUtil.IsHorizontalFace(tile.identity) &&
                TileCollisionFlagsUtil.Has(tile.identity.collisionFlags, TileCollisionFlags.ProvidesLogicalFloor))
            {
                var above = FloorFaceKey.FromFloorTileIdentity(tile.identity).CellAbove;
                for (int dy = 0; dy < Mathf.Max(1, tile.identity.sizeUnit.y); dy++)
                    UpdateWalkableFloorColumn(above.x, above.z, above.y + dy, delta);
            }
            else if (IsWalkableStratum(tile))
            {
                var anchor = tile.identity.GridPos;
                UpdateWalkableFloorColumn(anchor.x, anchor.z,
                    MapDigTerrainUtil.WalkableCellYFromSupportAnchor(anchor.y, tile.identity.sizeUnit.y), delta);
            }

            foreach (var cell in _editCells)
            {
                RefreshColumnSeal(cell);
                // CellHasFloor also depends on a stratum support in the cell below.
                RefreshColumnSeal(cell + Vector3Int.up);
            }
        }

        static void RemoveIncident<TKey>(Dictionary<Vector3Int, List<TKey>> index, Vector3Int cell, TKey key)
        {
            if (!index.TryGetValue(cell, out var keys))
                return;
            keys.Remove(key);
            if (keys.Count == 0)
                index.Remove(cell);
        }

        public void RebuildOccupancy()
        {
            _anyTileAt.Clear();
            _occupiedEntries.Clear();
            _wallKeysAtCell.Clear();
            _floorKeysAtCell.Clear();
            _walkableFloorYsByColumn.Clear();
            _walkableFloorContributors.Clear();
            _columnSealTopY.Clear();
            _columnSealYs.Clear();

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
                    var tileRef = new TileRef(list, i);
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

                                entry.Refs.Add(tileRef);
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

            RebuildColumnSealHeights();
        }

        /// <summary>
        /// 전역 (x,z) 컬럼별 최고 seal cellY — walkable floor(<see cref="CellHasFloor"/>) 또는
        /// structural OccupiedCell(벽·큐브). buildingId는 보지 않는다 — 다른 building 소속 지붕이라도
        /// 같은 컬럼을 막으면 seal (§대전제 buildingId 동일성 미사용, TILEMAP_BUILDING_BAKE.md).
        /// </summary>
        void RebuildColumnSealHeights()
        {
            foreach (var (x, z, y) in _anyTileAt)
                RefreshColumnSeal(new Vector3Int(x, y, z));
        }

        void RefreshColumnSeal(Vector3Int cell)
        {
            var key = (cell.x, cell.z);
            if (HasAnyTile(cell.x, cell.z, cell.y) &&
                (CellHasFloor(cell.x, cell.y, cell.z) || CellHasStructuralOccupancy(cell.x, cell.y, cell.z)))
            {
                if (!_columnSealYs.TryGetValue(key, out var ys))
                    _columnSealYs.Add(key, ys = new SortedSet<int>());
                ys.Add(cell.y);
                _columnSealTopY[key] = ys.Max;
            }
            else if (_columnSealYs.TryGetValue(key, out var ys))
            {
                ys.Remove(cell.y);
                if (ys.Count == 0)
                {
                    _columnSealYs.Remove(key);
                    _columnSealTopY.Remove(key);
                }
                else
                    _columnSealTopY[key] = ys.Max;
            }
        }

        bool CellHasStructuralOccupancy(int x, int y, int z)
        {
            if (!TryGetCellTiles(x, z, y, out var list) || list == null)
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                if (TileIdentityUtil.IsStructural(list[i].identity))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// (x,z) 컬럼의 최고 seal cellY 조회 — 없으면 false.
        /// <see cref="SpaceLeakEvaluator"/> 천장 leak: <c>topY &gt; floorY</c>면 밀폐.
        /// </summary>
        public bool TryGetColumnSealTopY(int x, int z, out int topY) =>
            _columnSealTopY.TryGetValue((x, z), out topY);

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

        static bool IsWalkableStratum(in TileData tile) =>
            MapDigTerrainUtil.IsWalkableSupportBlock(tile.identity) &&
            TilePrefabDB.TryResolveDefinition(tile.identity.PrefabId, out TileDefinition def) &&
            MapDigTerrainUtil.IsWalkableStratumBlock(def);

        void RegisterWalkableFloorColumn(int x, int z, int cellY) =>
            UpdateWalkableFloorColumn(x, z, cellY, 1);

        void UpdateWalkableFloorColumn(int x, int z, int cellY, int delta)
        {
            var cell = new Vector3Int(x, cellY, z);
            _walkableFloorContributors.TryGetValue(cell, out int count);
            count += delta;
            var key = (x, z);
            if (count <= 0)
            {
                _walkableFloorContributors.Remove(cell);
                if (_walkableFloorYsByColumn.TryGetValue(key, out var existing))
                {
                    existing.Remove(cellY);
                    if (existing.Count == 0)
                        _walkableFloorYsByColumn.Remove(key);
                }
                return;
            }
            _walkableFloorContributors[cell] = count;
            if (!_walkableFloorYsByColumn.TryGetValue(key, out var list))
            {
                list = new List<int>(2);
                _walkableFloorYsByColumn[key] = list;
            }

            int index = list.BinarySearch(cellY);
            if (index < 0)
                list.Insert(~index, cellY);
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
                if (tr.TryRead(out var tile))
                    AppendUniqueTile(into, tile, dedupe);
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
                if (tr.TryRead(out var tile))
                    entry.Scratch.Add(tile);
            }

            list = entry.Scratch;
            return true;
        }

        /// <summary>
        /// 배치 SSOT — 셀 Y와 동일한 <see cref="WallEdgeKey"/>만 조회. size.y 확장은 포함하지 않음.
        /// </summary>
        public bool TryGetEdgeBetween(Vector3Int cellA, Vector3Int cellB, out TileData edgeWall)
        {
            edgeWall = default;
            return WallEdgeKey.TryBetween(cellA, cellB, out var edgeKey) &&
                   _edges.TryGetValue(edgeKey, out edgeWall);
        }

        /// <summary>
        /// 측면 통과/BFS용 — 배치 Y 엣지이거나, 같은 XZ 면 벽이
        /// <c>GridPos.y .. +size.y-1</c>로 이 cellY를 덮으면 히트
        /// (<see cref="RebuildOccupancy"/>가 size만큼 <c>_wallKeysAtCell</c>에 등록).
        /// </summary>
        public bool TryGetEdgeSealingLateral(Vector3Int cellA, Vector3Int cellB, out TileData edgeWall)
        {
            edgeWall = default;
            if (cellA.y != cellB.y)
                return false;

            if (!WallEdgeKey.TryBetween(cellA, cellB, out var queryKey))
                return false;

            if (_edges.TryGetValue(queryKey, out edgeWall))
                return true;

            return TryGetSealingEdgeFromIncidentKeys(cellA, queryKey, out edgeWall) ||
                   TryGetSealingEdgeFromIncidentKeys(cellB, queryKey, out edgeWall);
        }

        bool TryGetSealingEdgeFromIncidentKeys(
            Vector3Int cell,
            in WallEdgeKey queryKey,
            out TileData edgeWall)
        {
            edgeWall = default;
            if (!_wallKeysAtCell.TryGetValue(cell, out var keys) || keys == null)
                return false;

            for (int i = 0; i < keys.Count; i++)
            {
                WallEdgeKey key = keys[i];
                if (!SameHorizontalEdgeFace(key, queryKey))
                    continue;
                if (!_edges.TryGetValue(key, out edgeWall))
                    continue;
                return true;
            }

            return false;
        }

        static bool SameHorizontalEdgeFace(in WallEdgeKey a, in WallEdgeKey b) =>
            a.Face == b.Face && a.Anchor.x == b.Anchor.x && a.Anchor.z == b.Anchor.z;

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
            if (!TryGetEdgeSealingLateral(cellA, cellB, out var edge))
                return false;

            return TileCollisionFlagsUtil.EdgeBlocksPassage(edge);
        }

        public bool EdgeSeparatesRoom(Vector3Int cellA, Vector3Int cellB)
        {
            if (!TryGetEdgeSealingLateral(cellA, cellB, out var edge))
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
