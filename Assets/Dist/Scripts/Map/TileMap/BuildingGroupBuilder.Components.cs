// ============================================================
// BuildingGroupBuilder.Components — 점유셀 component 배치 union bake·buildingId 지연 할당
// ============================================================
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    public sealed partial class BuildingGroupBuilder
    {
        internal const int MaxComponentBakeRounds = 3;

        readonly ComponentUnionFind _componentUnion = new();
        readonly Dictionary<Vector3Int, int> _occupiedComponent = new();
        readonly List<(int setIdA, int setIdB)> _unionSetCandidateScratch = new();
        readonly Dictionary<int, HashSet<Vector3Int>> _seedsByComponentScratch = new();
        readonly HashSet<int> _componentRootScratch = new();
        readonly HashSet<(int x, int z)> _initFootprintProcessedScratch = new();
        readonly List<(int setId, int seedX, int seedZ, int footprintSize)> _initFootprintMetaScratch = new();
        readonly HashSet<(int x, int cellY, int z)> _zeroFootprintProcessedScratch = new();
        readonly List<int> _thinWallSetIdScratch = new();

        int _lastComponentBakeRoundCount;
        int _lastComponentUnionCount;
        int _lastComponentNewFloorTags;
        int _initFootprintCount;
        int _lastStructuralUnionCount;
        int _roundStructuralUnionScratch;
        bool _loggedFirstStructuralUnion;

        void ClearBuildingComponents()
        {
            _componentUnion.Clear();
            _occupiedComponent.Clear();
            _unionSetCandidateScratch.Clear();
            _seedsByComponentScratch.Clear();
            _componentRootScratch.Clear();
            _initFootprintProcessedScratch.Clear();
            _initFootprintMetaScratch.Clear();
            _zeroFootprintProcessedScratch.Clear();
            _thinWallSetIdScratch.Clear();
            _lastComponentBakeRoundCount = 0;
            _lastComponentUnionCount = 0;
            _lastComponentNewFloorTags = 0;
            _initFootprintCount = 0;
            _lastStructuralUnionCount = 0;
            _roundStructuralUnionScratch = 0;
            _loggedFirstStructuralUnion = false;
        }

        void BakeBuildingComponentsForMap()
        {
            ClearBuildingComponents();
            InitComponentsFromMinCellYFloorOccCells();
            LogComponentBakeInitIfDebug();
            RunComponentBakeRounds();
            AssignOrphanComponents();
            LogComponentBakePhaseIfDebug("afterOrphan");
            RunComponentBakeRounds();
            LogComponentBakePhaseIfDebug("beforeAssign");
        }

        void InitComponentsFromMinCellYFloorOccCells()
        {
            var outdoor = new HashSet<(int x, int z)>(_registry.PlazaFloorXZ);
            _initFootprintProcessedScratch.Clear();

            foreach (var (x, cellY, z) in _topology.Index.EnumerateWalkableFloorCells())
            {
                if (cellY != _minCellY)
                    continue;

                if (_registry.IsPlazaXZ(x, z))
                    continue;

                if (!IsFloorBuildingUnassigned(x, cellY, z))
                    continue;

                if (_initFootprintProcessedScratch.Contains((x, z)))
                    continue;

                var footprint = FloorRoomFloodFill.Run(
                    _topology.Index, _minCellY, x, z,
                    collectEmptyNeighbors: false,
                    excludeCells: outdoor).Visited;

                if (footprint.Count == 0)
                    continue;

                int setId = _componentUnion.MakeSet();
                _initFootprintCount++;
                LogInitFootprintIfDebug(setId, x, z, footprint.Count);
                foreach (var (fx, fz) in footprint)
                {
                    if (outdoor.Contains((fx, fz)))
                        continue;

                    // 이미 하드 파티션(양수 id)인 바닥은 새 component에 넣지 않음.
                    if (BuildingIdBakeRules.IsHardPartitionBuildingId(
                            GetFloorBuildingId(fx, _minCellY, fz)))
                        continue;

                    _initFootprintProcessedScratch.Add((fx, fz));
                    TagOccupiedCellWithComponentSet(WalkableFloorOccupiedCell(fx, _minCellY, fz), setId);
                }
            }
        }

        void RunComponentBakeRounds()
        {
            for (int round = 0; round < MaxComponentBakeRounds; round++)
            {
                _unionSetCandidateScratch.Clear();
                CollectFloorHorizontalUnionCandidates();
                CollectZeroFootprintUnionCandidates();
                CollectThinWallUnionCandidates();

                int unionCount = UnionAllCandidates();
                _roundStructuralUnionScratch = 0;
                int newFloorTags = FloodAllComponentsStructural(round);

                _lastComponentBakeRoundCount = round + 1;
                _lastComponentUnionCount += unionCount;
                _lastStructuralUnionCount += _roundStructuralUnionScratch;
                _lastComponentNewFloorTags += newFloorTags;

                LogComponentBakeRoundIfDebug(round, unionCount, newFloorTags);

                if (unionCount == 0 && newFloorTags == 0)
                    break;

                if (round == MaxComponentBakeRounds - 1 && (unionCount > 0 || newFloorTags > 0))
                    Debug.LogError(
                        $"[ComponentBake] MaxComponentBakeRounds={MaxComponentBakeRounds} reached with " +
                        $"unions={unionCount} newFloors={newFloorTags}");
            }
        }

        void CollectFloorHorizontalUnionCandidates()
        {
            _walkableFloorCellScratch.Clear();
            foreach (var cell in _topology.Index.EnumerateWalkableFloorCells())
                _walkableFloorCellScratch.Add(cell);

            for (int i = 0; i < _walkableFloorCellScratch.Count; i++)
            {
                var (x, cellY, z) = _walkableFloorCellScratch[i];
                if (IsPlazaOrOutdoorFloor(x, z, cellY))
                    continue;

                Vector3Int cellA = WalkableFloorOccupiedCell(x, cellY, z);
                if (!TryGetOccupiedComponentRoot(cellA, out int rootA))
                    continue;

                for (int d = 0; d < CardinalDirs.Length; d++)
                {
                    int nx = x + CardinalDirs[d].x;
                    int nz = z + CardinalDirs[d].z;
                    if (IsPlazaOrOutdoorFloor(nx, nz, cellY))
                        continue;

                    if (!_topology.Index.CellHasFloor(nx, cellY, nz))
                        continue;

                    Vector3Int cellB = WalkableFloorOccupiedCell(nx, cellY, nz);
                    if (!TryGetOccupiedComponentRoot(cellB, out int rootB))
                        continue;

                    if (rootA == rootB)
                        continue;

                    int bidA = GetFloorBuildingId(x, cellY, z);
                    int bidB = GetFloorBuildingId(nx, cellY, nz);
                    if (BuildingIdBakeRules.BlocksComponentUnion(bidA, bidB))
                        continue;

                    if (!CanUnionFloorHorizontalOccupiedCells(cellA, cellB))
                        continue;

                    if (!TryGetOccupiedComponentSetId(cellA, out int setA) ||
                        !TryGetOccupiedComponentSetId(cellB, out int setB))
                        continue;

                    _unionSetCandidateScratch.Add((setA, setB));
                }
            }
        }

        void CollectZeroFootprintUnionCandidates()
        {
            _zeroFootprintProcessedScratch.Clear();

            for (int cellY = _minCellY; cellY <= _maxCellY; cellY++)
            {
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

                    if (!IsFloorOccCellUntaggedForComponent(x, cellY, z))
                        continue;

                    if (_zeroFootprintProcessedScratch.Contains((x, cellY, z)))
                        continue;

                    var footprint = CollectUnassignedFloorFootprint(cellY, x, z);
                    if (footprint.Count == 0)
                        continue;

                    _componentRootScratch.Clear();
                    foreach (var (fx, fz) in footprint)
                    {
                        _zeroFootprintProcessedScratch.Add((fx, cellY, fz));
                        foreach (var dir in CardinalDirs)
                        {
                            int nx = fx + dir.x;
                            int nz = fz + dir.z;
                            Vector3Int neighbor = WalkableFloorOccupiedCell(nx, cellY, nz);
                            if (TryGetOccupiedComponentRoot(neighbor, out int neighborRoot))
                                _componentRootScratch.Add(neighborRoot);
                        }
                    }

                    if (_componentRootScratch.Count == 0)
                        continue;

                    int footprintSetId = _componentUnion.MakeSet();
                    foreach (var (fx, fz) in footprint)
                        TagOccupiedCellWithComponentSet(WalkableFloorOccupiedCell(fx, cellY, fz), footprintSetId);

                    foreach (int adjacentRoot in _componentRootScratch)
                    {
                        if (!TryGetAnySetIdForRoot(adjacentRoot, out int adjacentSetId))
                            continue;

                        _unionSetCandidateScratch.Add((footprintSetId, adjacentSetId));
                    }
                }
            }
        }

        void CollectThinWallUnionCandidates()
        {
            _wallFaceScratch.Clear();
            _model.FaceBinder.CopyWallFacesTo(_wallFaceScratch);

            for (int i = 0; i < _wallFaceScratch.Count; i++)
            {
                TileData wallA = _wallFaceScratch[i];
                if (!TileIdentityUtil.IsVerticalFace(wallA.identity))
                    continue;

                WallEdgeKey keyA = WallEdgeKey.FromWallTileIdentity(wallA.identity);
                for (int j = i + 1; j < _wallFaceScratch.Count; j++)
                {
                    TileData wallB = _wallFaceScratch[j];
                    if (!TileIdentityUtil.IsVerticalFace(wallB.identity))
                        continue;

                    WallEdgeKey keyB = WallEdgeKey.FromWallTileIdentity(wallB.identity);
                    if (!ThinWallBuildingConnect.AreConnected(keyA, keyB))
                        continue;

                    TryAddUnionCandidatesForConnectedWalls(wallA, wallB);
                }
            }
        }

        void TryAddUnionCandidatesForConnectedWalls(in TileData wallA, in TileData wallB)
        {
            _thinWallSetIdScratch.Clear();
            AppendTaggedSetIdsFromWallTile(wallA);
            AppendTaggedSetIdsFromWallTile(wallB);
            if (_thinWallSetIdScratch.Count < 2)
                return;

            for (int i = 0; i < _thinWallSetIdScratch.Count; i++)
            {
                int setA = _thinWallSetIdScratch[i];
                int rootA = _componentUnion.Find(setA);
                for (int j = i + 1; j < _thinWallSetIdScratch.Count; j++)
                {
                    int setB = _thinWallSetIdScratch[j];
                    if (_componentUnion.Find(setB) == rootA)
                        continue;

                    _unionSetCandidateScratch.Add((setA, setB));
                }
            }
        }

        void AppendTaggedSetIdsFromWallTile(in TileData wall)
        {
            _occupiedCellAffectedScratch.Clear();
            TileIdentityUtil.CollectAffectedCells(wall.identity, _occupiedCellAffectedScratch);
            foreach (Vector3Int cell in _occupiedCellAffectedScratch)
            {
                if (!TryGetOccupiedComponentSetId(cell, out int setId))
                    continue;

                bool already = false;
                for (int i = 0; i < _thinWallSetIdScratch.Count; i++)
                {
                    if (_thinWallSetIdScratch[i] == setId)
                    {
                        already = true;
                        break;
                    }
                }

                if (!already)
                    _thinWallSetIdScratch.Add(setId);
            }
        }

        int UnionAllCandidates()
        {
            int unionCount = 0;
            for (int i = 0; i < _unionSetCandidateScratch.Count; i++)
            {
                var (setA, setB) = _unionSetCandidateScratch[i];
                int rootBeforeA = _componentUnion.Find(setA);
                int rootBeforeB = _componentUnion.Find(setB);
                if (rootBeforeA == rootBeforeB)
                    continue;

                _componentUnion.Union(setA, setB);
                unionCount++;
            }

            return unionCount;
        }

        bool TryGetAnySetIdForRoot(int componentRoot, out int setId)
        {
            foreach (var kv in _occupiedComponent)
            {
                if (_componentUnion.Find(kv.Value) == componentRoot)
                {
                    setId = kv.Value;
                    return true;
                }
            }

            setId = 0;
            return false;
        }

        int FloodAllComponentsStructural(int round)
        {
            _componentRootScratch.Clear();
            foreach (var kv in _occupiedComponent)
            {
                int root = _componentUnion.Find(kv.Value);
                _componentRootScratch.Add(root);
            }

            int newFloorTags = 0;
            foreach (int componentRoot in _componentRootScratch)
            {
                if (!ComponentBakeRules.CanPropagateComponentFrom(componentRoot))
                    continue;

                CollectStructuralFloodSeedsForComponent(componentRoot);
                if (!_seedsByComponentScratch.TryGetValue(componentRoot, out var seeds) || seeds.Count == 0)
                    continue;

                LogStructuralFloodSeedsIfDebug(round, componentRoot, seeds);
                newFloorTags += FloodStructuralComponentFromSeeds(componentRoot, seeds);
            }

            return newFloorTags;
        }

        void CollectStructuralFloodSeedsForComponent(int componentRoot)
        {
            if (!_seedsByComponentScratch.TryGetValue(componentRoot, out var seeds))
            {
                seeds = new HashSet<Vector3Int>();
                _seedsByComponentScratch[componentRoot] = seeds;
            }
            else
            {
                seeds.Clear();
            }

            foreach (var kv in _occupiedComponent)
            {
                if (_componentUnion.Find(kv.Value) != componentRoot)
                    continue;

                if (ShouldBlockComponentFloodCell(kv.Key, componentRoot))
                    continue;

                seeds.Add(kv.Key);

                VisitWalkableFloorFootprintCells(kv.Key.x, kv.Key.y, kv.Key.z, cell =>
                {
                    if (!ShouldBlockComponentFloodCell(cell, componentRoot))
                        seeds.Add(cell);
                });
            }
        }

        int FloodStructuralComponentFromSeeds(int componentRoot, HashSet<Vector3Int> seedCells)
        {
            if (!ComponentBakeRules.CanPropagateComponentFrom(componentRoot) || seedCells == null || seedCells.Count == 0)
                return 0;

            _occupiedCellFloodVisitedScratch.Clear();
            var q = new Queue<Vector3Int>();
            int newFloorTags = 0;
            int steps = 0;
            _floodTraceActive = Config.DebugMode.FloorAlgorithm;
            if (_floodTraceActive)
            {
                _floodParentScratch.Clear();
                _floodViaScratch.Clear();
            }

            foreach (var cell in seedCells)
            {
                if (!CanTraverseOccupiedCellForComponentFlood(componentRoot, cell))
                    continue;

                if (!_topology.HasOccupancy(cell.x, cell.z, cell.y))
                    continue;

                RecordFloodParent(cell, cell, "seed");
                if (_occupiedCellFloodVisitedScratch.Add(cell))
                    q.Enqueue(cell);
            }

            while (q.Count > 0)
            {
                if (++steps > OccupiedCellFloodSafetyLimit)
                    break;

                Vector3Int cur = q.Dequeue();
                newFloorTags += TryBridgeFloorComponentFromStructuralFlood(componentRoot, cur, q);
                _floodTraceSourceCur = cur;
                _floodTraceSourceVia = "tagAt";
                TagStructuralOccupiedCellsAt(cur, componentRoot);

                CollectStructuralForPatch(cur, _structuralPatchTileScratch);
                for (int i = 0; i < _structuralPatchTileScratch.Count; i++)
                {
                    _occupiedCellAffectedScratch.Clear();
                    TileIdentityUtil.CollectAffectedCells(
                        _structuralPatchTileScratch[i].identity, _occupiedCellAffectedScratch);
                    foreach (var affected in _occupiedCellAffectedScratch)
                    {
                        if (ShouldBlockComponentFloodCell(affected, componentRoot))
                            continue;

                        _floodTraceSourceCur = cur;
                        _floodTraceSourceVia = "affected";
                        RecordFloodParent(cur, affected, "affected");
                        TagStructuralOccupiedCell(affected, componentRoot);
                        EnqueueComponentFloodCellIfTraversable(componentRoot, affected, q, cur, "affected");
                    }
                }

                for (int d = 0; d < OccupiedCellFloodDirs.Length; d++)
                {
                    Vector3Int dir = OccupiedCellFloodDirs[d];
                    EnqueueComponentFloodCellIfTraversable(
                        componentRoot, cur + dir, q, cur, $"6dir:{dir.x},{dir.y},{dir.z}");
                }
            }

            newFloorTags += PropagateComponentUpVisitedColumns(componentRoot, q);

            _lastStructuralFloodVisited += _occupiedCellFloodVisitedScratch.Count;
            _floodTraceActive = false;
            return newFloorTags;
        }

        int PropagateComponentUpVisitedColumns(int componentRoot, Queue<Vector3Int> q)
        {
            _columnAscendStartYScratch.Clear();
            foreach (var cell in _occupiedCellFloodVisitedScratch)
            {
                var key = (cell.x, cell.z);
                if (!_columnAscendStartYScratch.TryGetValue(key, out int startY) || cell.y < startY)
                    _columnAscendStartYScratch[key] = cell.y;
            }

            int newFloorTags = 0;
            foreach (var kv in _columnAscendStartYScratch)
                newFloorTags += PropagateComponentUpColumn(componentRoot, kv.Key.x, kv.Key.z, kv.Value, q);

            return newFloorTags;
        }

        int PropagateComponentUpColumn(int componentRoot, int x, int z, int startY, Queue<Vector3Int> q)
        {
            int newFloorTags = 0;
            for (int y = startY; y <= _maxCellY; y++)
            {
                var columnCell = new Vector3Int(x, y, z);
                if (!_topology.HasOccupancy(x, z, y))
                    break;

                if (ShouldBlockComponentFloodCell(columnCell, componentRoot))
                    break;

                newFloorTags += TryBridgeWalkableFloorForComponent(componentRoot, x, y, z, q);
                newFloorTags += TryBridgeWalkableFloorAboveCellForComponent(componentRoot, x, y, z, q);
                TagStructuralOccupiedCellsAt(columnCell, componentRoot);
            }

            return newFloorTags;
        }

        int TryBridgeFloorComponentFromStructuralFlood(int componentRoot, Vector3Int cell, Queue<Vector3Int> q)
        {
            int bridged = TryBridgeWalkableFloorForComponent(componentRoot, cell.x, cell.y, cell.z, q);
            bridged += TryBridgeWalkableFloorAboveCellForComponent(componentRoot, cell.x, cell.y, cell.z, q);
            return bridged;
        }

        int TryBridgeWalkableFloorForComponent(int componentRoot, int x, int walkableY, int z, Queue<Vector3Int> q)
        {
            Vector3Int occ = WalkableFloorOccupiedCell(x, walkableY, z);
            if (ShouldBlockComponentFloodCell(occ, componentRoot))
                return 0;

            if (!IsFloorOccCellUntaggedForComponent(x, walkableY, z))
                return 0;

            if (!_topology.Index.CellHasFloor(x, walkableY, z))
                return 0;

            TagOccupiedCellWithComponentRoot(occ, componentRoot);
            EnqueueComponentFloodOccupiedCells(componentRoot, x, walkableY, z, q);
            return 1;
        }

        int TryBridgeWalkableFloorAboveCellForComponent(int componentRoot, int x, int cellY, int z, Queue<Vector3Int> q)
        {
            int aboveY = cellY + 1;
            if (aboveY > _maxCellY)
                return 0;

            if (!_topology.Index.TryGetHorizontalFaceBetween(
                    new Vector3Int(x, cellY, z),
                    new Vector3Int(x, aboveY, z),
                    out _))
                return 0;

            return TryBridgeWalkableFloorForComponent(componentRoot, x, aboveY, z, q);
        }

        void EnqueueComponentFloodOccupiedCells(int componentRoot, int x, int cellY, int z, Queue<Vector3Int> q)
        {
            EnqueueComponentFloodCellIfTraversable(componentRoot, new Vector3Int(x, cellY, z), q);
            VisitWalkableFloorFootprintCells(x, cellY, z, cell =>
                EnqueueComponentFloodCellIfTraversable(componentRoot, cell, q));
        }

        void EnqueueComponentFloodCellIfTraversable(
            int componentRoot,
            Vector3Int cell,
            Queue<Vector3Int> q,
            Vector3Int from,
            string via)
        {
            if (!CanTraverseOccupiedCellForComponentFlood(componentRoot, cell))
                return;

            if (!_topology.HasOccupancy(cell.x, cell.z, cell.y))
                return;

            RecordFloodParent(from, cell, via);
            if (_occupiedCellFloodVisitedScratch.Add(cell))
                q.Enqueue(cell);
        }

        void EnqueueComponentFloodCellIfTraversable(int componentRoot, Vector3Int cell, Queue<Vector3Int> q) =>
            EnqueueComponentFloodCellIfTraversable(componentRoot, cell, q, cell, "enqueue");

        bool CanTraverseOccupiedCellForComponentFlood(int componentRoot, Vector3Int cell) =>
            !ShouldBlockComponentFloodCell(cell, componentRoot);

        bool ShouldBlockComponentFloodCell(Vector3Int cell, int componentRoot)
        {
            if (!_topology.TryCollectTilesAtOccupiedCell(cell, _occupiedCellCollectScratch))
                return false;

            return AnyCollectedIncidentTileBlocks(id =>
                ComponentBakeRules.ShouldBlockComponentFloodFromIncidentTile(id));
        }

        void TagStructuralOccupiedCellsAt(Vector3Int cell, int componentRoot)
        {
            if (ShouldBlockComponentFloodCell(cell, componentRoot))
                return;

            if (!_topology.TryCollectTilesAtOccupiedCell(cell, _occupiedCellCollectScratch))
                return;

            for (int i = 0; i < _occupiedCellCollectScratch.Count; i++)
            {
                TileData tile = _occupiedCellCollectScratch[i];
                if (!BuildingIdBakeRules.ShouldPatchBuildingIdAtOccupiedCell(tile.identity))
                    continue;

                TagStructuralOccupiedCell(cell, componentRoot);
            }
        }

        void TagStructuralOccupiedCell(Vector3Int cell, int componentRoot)
        {
            if (!TryGetOccupiedComponentRoot(cell, out int existingRoot))
            {
                TagOccupiedCellWithComponentRoot(cell, componentRoot);
                return;
            }

            if (existingRoot == componentRoot)
                return;

            if (!ComponentBakeRules.ShouldUnionOnStructuralContact(existingRoot, componentRoot))
                return;

            if (!TryGetOccupiedComponentSetId(cell, out int existingSet) ||
                !TryGetAnySetIdForRoot(componentRoot, out int incomingSet))
                return;

            int rootBeforeA = _componentUnion.Find(existingSet);
            int rootBeforeB = _componentUnion.Find(incomingSet);
            if (rootBeforeA == rootBeforeB)
                return;

            _componentUnion.Union(existingSet, incomingSet);
            _roundStructuralUnionScratch++;
            LogFirstStructuralUnionIfDebug(cell, existingRoot, componentRoot);
            LogFloodReachPathIfDebug(cell, componentRoot, existingRoot);
        }

        void AssignOrphanComponents()
        {
            var outdoor = new HashSet<(int x, int z)>(_registry.PlazaFloorXZ);
            _zeroFootprintProcessedScratch.Clear();

            foreach (var (x, cellY, z) in _topology.Index.EnumerateWalkableFloorCells())
            {
                if (!IsFloorOccCellUntaggedForComponent(x, cellY, z))
                    continue;

                if (_zeroFootprintProcessedScratch.Contains((x, cellY, z)))
                    continue;

                var footprint = FloorRoomFloodFill.Run(
                    _topology.Index, cellY, x, z,
                    collectEmptyNeighbors: false,
                    excludeCells: cellY == _minCellY ? outdoor : null).Visited;

                if (footprint.Count == 0)
                    continue;

                int setId = _componentUnion.MakeSet();
                foreach (var (fx, fz) in footprint)
                {
                    _zeroFootprintProcessedScratch.Add((fx, cellY, fz));
                    TagOccupiedCellWithComponentSet(WalkableFloorOccupiedCell(fx, cellY, fz), setId);
                }
            }
        }

        void AssignBuildingIdsFromComponents()
        {
            LogComponentBakePhaseIfDebug("assignEnter");
            _registry.SyncNextBuildingIdFromTiles(_model.TilesSnapshot);

            var rootToBuildingId = new Dictionary<int, int>();
            var rootHardId = new Dictionary<int, int>();

            foreach (var kv in _occupiedComponent)
            {
                int root = _componentUnion.Find(kv.Value);
                if (!rootHardId.ContainsKey(root))
                    rootHardId[root] = 0;

                if (!_topology.TryCollectTilesAtOccupiedCell(kv.Key, _occupiedCellCollectScratch))
                    continue;

                for (int i = 0; i < _occupiedCellCollectScratch.Count; i++)
                {
                    int bid = _occupiedCellCollectScratch[i].identity.buildingId;
                    if (!BuildingIdBakeRules.IsHardPartitionBuildingId(bid))
                        continue;

                    if (rootHardId[root] == 0)
                        rootHardId[root] = bid;
                    else if (rootHardId[root] != bid)
                    {
                        Debug.LogError(
                            $"[ComponentBake] component root {root} mixes hard partitions " +
                            $"{rootHardId[root]} and {bid} — preserving first.");
                    }
                }
            }

            foreach (var kv in rootHardId)
            {
                rootToBuildingId[kv.Key] = kv.Value > 0
                    ? kv.Value
                    : _registry.AllocateBuildingId();
            }

            foreach (var kv in _occupiedComponent)
            {
                int root = _componentUnion.Find(kv.Value);
                if (!rootToBuildingId.TryGetValue(root, out int buildingId))
                    continue;

                PatchBuildingIdAtOccupiedCell(kv.Key, buildingId);
            }

            _lastStructuralFloodPatched = _occupiedComponent.Count;
            LogInitFootprintToBuildingIdIfDebug(rootToBuildingId);
        }

        void PatchBuildingIdAtOccupiedCell(Vector3Int cell, int buildingId)
        {
            if (!_topology.TryCollectTilesAtOccupiedCell(cell, _occupiedCellCollectScratch))
                return;

            for (int i = 0; i < _occupiedCellCollectScratch.Count; i++)
            {
                TileData tile = _occupiedCellCollectScratch[i];
                int existing = tile.identity.buildingId;
                if (!BuildingIdBakeRules.ShouldOverwriteBuildingIdForPropagation(existing, buildingId))
                    continue;

                if (!BuildingIdBakeRules.ShouldPatchBuildingIdAtOccupiedCell(tile.identity))
                    continue;

                _model.PatchTileIdentity(tile.tileDefId, buildingId, tile.identity.roomId);
            }
        }

        void ResetIndoorBuildingIds()
        {
            _model.ForEachRuntimeTileMutating(tile =>
            {
                if (!TileIdentityUtil.IsStructural(tile.identity))
                    return;

                int existing = tile.identity.buildingId;
                if (BuildingIdBakeRules.IsImmutableOutdoorBuildingId(existing))
                    return;

                if (existing == TileIdentity.BuildingIdUnassigned)
                    return;

                _model.PatchTileIdentity(tile.tileDefId, TileIdentity.BuildingIdUnassigned, tile.identity.roomId);
            });
        }

        void RebakeBuildingIdsFromComponents()
        {
            BakeBuildingComponentsForMap();
            AssignBuildingIdsFromComponents();
        }

        static Vector3Int WalkableFloorOccupiedCell(int x, int cellY, int z) => new Vector3Int(x, cellY, z);

        bool IsFloorOccCellUntaggedForComponent(int x, int cellY, int z)
        {
            if (!_topology.Index.CellHasFloor(x, cellY, z))
                return false;

            if (IsPlazaOrOutdoorFloor(x, z, cellY))
                return false;

            if (!IsFloorBuildingUnassigned(x, cellY, z))
                return false;

            return !TryGetOccupiedComponentSetId(WalkableFloorOccupiedCell(x, cellY, z), out _);
        }

        bool CanUnionFloorHorizontalOccupiedCells(Vector3Int cellA, Vector3Int cellB)
        {
            if (cellA.y != cellB.y)
                return false;

            if (_topology.Index.EdgeSeparatesRoom(cellA, cellB))
                return false;

            if (_topology.Index.TryGetCellTiles(cellB.x, cellB.z, cellB.y, out var list) &&
                FloorMapIndex.CellHasSolidWall(list))
                return false;

            return true;
        }

        bool TryGetOccupiedComponentSetId(Vector3Int cell, out int setId) =>
            _occupiedComponent.TryGetValue(cell, out setId);

        bool TryGetOccupiedComponentRoot(Vector3Int cell, out int root)
        {
            if (!TryGetOccupiedComponentSetId(cell, out int setId))
            {
                root = 0;
                return false;
            }

            root = _componentUnion.Find(setId);
            return ComponentBakeRules.CanPropagateComponentFrom(root);
        }

        void TagOccupiedCellWithComponentSet(Vector3Int cell, int setId) =>
            _occupiedComponent[cell] = setId;

        void TagOccupiedCellWithComponentRoot(Vector3Int cell, int componentRoot)
        {
            foreach (var kv in _occupiedComponent)
            {
                if (_componentUnion.Find(kv.Value) != componentRoot)
                    continue;

                _occupiedComponent[cell] = kv.Value;
                return;
            }

            int setId = _componentUnion.MakeSet();
            _componentUnion.Union(setId, componentRoot);
            _occupiedComponent[cell] = setId;
        }

        int CountDistinctComponentRoots()
        {
            _componentRootScratch.Clear();
            foreach (var kv in _occupiedComponent)
                _componentRootScratch.Add(_componentUnion.Find(kv.Value));

            return _componentRootScratch.Count;
        }

        void LogComponentBakeInitIfDebug()
        {
            if (!Config.DebugMode.FloorAlgorithm)
                return;

            Debug.Log(
                $"[ComponentBake] init footprints={_initFootprintCount} roots={CountDistinctComponentRoots()} " +
                $"taggedCells={_occupiedComponent.Count} plazaCells={_registry.PlazaFloorXZ.Count}");
        }

        void LogComponentBakePhaseIfDebug(string phase)
        {
            if (!Config.DebugMode.FloorAlgorithm)
                return;

            Debug.Log(
                $"[ComponentBake] phase={phase} roots={CountDistinctComponentRoots()} " +
                $"structuralUnionsTotal={_lastStructuralUnionCount} taggedCells={_occupiedComponent.Count}");
        }

        void LogStructuralFloodSeedsIfDebug(int round, int componentRoot, HashSet<Vector3Int> seeds)
        {
            if (!Config.DebugMode.FloorAlgorithm)
                return;

            bool relevant = false;
            for (int i = 0; i < _initFootprintMetaScratch.Count; i++)
            {
                var meta = _initFootprintMetaScratch[i];
                if (_componentUnion.Find(meta.setId) != componentRoot)
                    continue;

                if (meta.footprintSize >= 16 || meta.setId is 1 or 2 or 3)
                {
                    relevant = true;
                    break;
                }
            }

            if (!relevant)
                return;

            var ordered = new List<Vector3Int>(seeds);
            ordered.Sort(static (a, b) =>
            {
                int c = a.y.CompareTo(b.y);
                if (c != 0) return c;
                c = a.x.CompareTo(b.x);
                return c != 0 ? c : a.z.CompareTo(b.z);
            });

            var parts = new List<string>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                Vector3Int c = ordered[i];
                parts.Add($"({c.x},{c.y},{c.z})");
            }

            Debug.Log(
                $"[ComponentBake] floodSeeds round={round + 1} root={componentRoot} count={seeds.Count} " +
                $"cells=[{string.Join(", ", parts)}]");
        }

        void LogInitFootprintIfDebug(int setId, int seedX, int seedZ, int footprintSize)
        {
            _initFootprintMetaScratch.Add((setId, seedX, seedZ, footprintSize));

            if (!Config.DebugMode.FloorAlgorithm)
                return;

            Debug.Log(
                $"[ComponentBake] initFootprint setId={setId} seed=({seedX},{_minCellY},{seedZ}) footprintCells={footprintSize}");
        }

        void LogInitFootprintToBuildingIdIfDebug(Dictionary<int, int> rootToBuildingId)
        {
            for (int i = 0; i < _initFootprintMetaScratch.Count; i++)
            {
                var (setId, seedX, seedZ, footprintSize) = _initFootprintMetaScratch[i];
                int finalRoot = _componentUnion.Find(setId);
                if (!rootToBuildingId.TryGetValue(finalRoot, out int buildingId))
                    continue;

                if (buildingId is not (1 or 2 or 3 or 17))
                    continue;

                string note = seedX == 3 && seedZ == 3 && buildingId != 3
                    ? " (NOT macro-center; z3 edge strip)"
                    : string.Empty;

                Debug.Log(
                    $"[ComponentBake] initToBuilding buildingId={buildingId} setId={setId} finalRoot={finalRoot} " +
                    $"seed=({seedX},{_minCellY},{seedZ}) footprintCells={footprintSize}{note}");
            }
        }

        void LogFirstStructuralUnionIfDebug(Vector3Int cell, int rootA, int rootB)
        {
            if (!Config.DebugMode.FloorAlgorithm || _loggedFirstStructuralUnion)
                return;

            _loggedFirstStructuralUnion = true;
            string tileSummary = DescribeOccupiedCellTiles(cell);
            Debug.Log(
                $"[ComponentBake] firstStructuralUnion cell=({cell.x},{cell.y},{cell.z}) " +
                $"existingRoot={rootA} incomingRoot={rootB} tiles=[{tileSummary}]");
        }

        void RecordFloodParent(Vector3Int from, Vector3Int to, string via)
        {
            if (!_floodTraceActive || _floodParentScratch.ContainsKey(to))
                return;

            _floodParentScratch[to] = from;
            _floodViaScratch[to] = via;
        }

        void LogFloodReachPathIfDebug(Vector3Int cell, int incomingRoot, int existingRoot)
        {
            if (!Config.DebugMode.FloorAlgorithm)
                return;

            string path = BuildFloodPathString(cell);
            string tileSummary = DescribeOccupiedCellTiles(cell);
            Debug.Log(
                $"[ComponentBake] floodReach incomingRoot={incomingRoot} existingRoot={existingRoot} " +
                $"cell=({cell.x},{cell.y},{cell.z}) via={_floodTraceSourceVia} " +
                $"from=({_floodTraceSourceCur.x},{_floodTraceSourceCur.y},{_floodTraceSourceCur.z}) " +
                $"path={path} tiles=[{tileSummary}]");
        }

        string BuildFloodPathString(Vector3Int end)
        {
            if (!_floodParentScratch.TryGetValue(end, out Vector3Int parent))
                return $"({end.x},{end.y},{end.z})";

            var nodes = new List<string>();
            var current = end;
            int guard = 0;
            while (guard++ < 512)
            {
                string via = _floodViaScratch.TryGetValue(current, out string v) ? v : "?";
                nodes.Add($"({current.x},{current.y},{current.z})[{via}]");
                if (!_floodParentScratch.TryGetValue(current, out parent) || parent == current)
                    break;

                current = parent;
            }

            nodes.Reverse();
            return string.Join(" -> ", nodes);
        }

        string DescribeOccupiedCellTiles(Vector3Int cell)
        {
            if (!_topology.TryCollectTilesAtOccupiedCell(cell, _occupiedCellCollectScratch))
                return "none";

            var parts = new List<string>(_occupiedCellCollectScratch.Count);
            for (int i = 0; i < _occupiedCellCollectScratch.Count; i++)
            {
                TileData tile = _occupiedCellCollectScratch[i];
                parts.Add($"{tile.identity.PrefabId}@({tile.identity.GridPos.x},{tile.identity.GridPos.y},{tile.identity.GridPos.z})");
            }

            return string.Join(", ", parts);
        }

        void LogComponentBakeRoundIfDebug(int round, int unionCount, int newFloorTags)
        {
            if (!Config.DebugMode.FloorAlgorithm)
                return;

            Debug.Log(
                $"[ComponentBake] round={round + 1} floorUnions={unionCount} structuralUnions={_roundStructuralUnionScratch} " +
                $"newFloors={newFloorTags} distinctRoots={CountDistinctComponentRoots()} " +
                $"taggedCells={_occupiedComponent.Count}");
        }
    }
}
