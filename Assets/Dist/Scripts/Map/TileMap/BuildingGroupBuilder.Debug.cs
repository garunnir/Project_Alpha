// ============================================================
// BuildingGroupBuilder.Debug — bake 디버그 로그·진단
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    public sealed partial class BuildingGroupBuilder
    {
        void LogCenterMacroLeakTrace()
        {
            const int centerBuildingId = 3;
            const int initSeedX = -4;
            const int initSeedZ = 2;

            if (!_registry.TryGetBuildingExtent(centerBuildingId, out var extent) || !extent.HasBounds)
            {
                Debug.Log($"[CenterLeak] buildingId={centerBuildingId} noExtent");
                return;
            }

            Debug.Log(
                $"[CenterLeak] initSeed=({initSeedX},{_minCellY},{initSeedZ}) buildingId={centerBuildingId} " +
                $"aabb=({extent.MinX},{extent.MinOccupiedY},{extent.MinZ})-({extent.MaxX},{extent.MaxOccupiedY},{extent.MaxZ}) " +
                $"maxStructuralY={extent.MaxStructuralY} floorSlices={extent.FloorFootprintByCellY.Count}");

            var registry = _hub.Spaces.Registry;
            var index = _topology.Index;
            int spaceCount = 0;

            foreach (int spaceId in registry.SpaceIds)
            {
                if (!registry.TryGetSpace(spaceId, out var space) || space.BuildingId != centerBuildingId)
                    continue;

                spaceCount++;
                var floorCells = registry.GetFloorCells(spaceId);
                LogSpaceLeakDiagnostics(
                    "[CenterLeak]",
                    spaceId,
                    space,
                    floorCells,
                    centerBuildingId,
                    extent,
                    index,
                    lateralPrefix: $"[CenterLeak] lateral space={spaceId}",
                    ceilingPrefix: $"[CenterLeak] ceiling space={spaceId}");
            }

            Debug.Log($"[CenterLeak] buildingId={centerBuildingId} spaceCount={spaceCount}");
            LogSpaceLeakDetailAtFloorCell("macro-center-init-seed", initSeedX, _minCellY, initSeedZ);
            LogSpaceLeakDetailAtFloorCell("macro-center-upper", 3, 2, 3);
        }

        void LogBakeSummaryIfDebug()
        {
            if (!Config.DebugMode.FloorAlgorithm)
                return;

            int faceCount = 0;
            int bakedAboveMin = 0;
            int terrainMin = 0;
            foreach (var tile in _model.TilesSnapshot)
            {
                if (!TileIdentityUtil.IsFloorTile(tile.identity))
                    continue;

                faceCount++;
                int walkY = FloorFaceKey.FromFloorTileIdentity(tile.identity).CellAbove.y;
                int bid = tile.identity.buildingId;
                if (walkY == _minCellY && bid == TileIdentity.BuildingIdTerrain)
                    terrainMin++;
                else if (walkY > _minCellY && BuildingIdBakeRules.CanPropagateBuildingIdFrom(bid))
                    bakedAboveMin++;
            }

            Debug.Log(
                $"[BuildingGroupBuilder] bake: minCellY={_minCellY}, floorFaces={faceCount}, " +
                $"terrain@min={terrainMin}, upperWithBuildingId={bakedAboveMin}, buildings={_registry.TilesByBuildingId.Count}, " +
                $"componentRounds={_lastComponentBakeRoundCount}, componentUnions={_lastComponentUnionCount}, " +
                $"componentStructuralUnions={_lastStructuralUnionCount}, initFootprints={_initFootprintCount}, " +
                $"componentNewFloors={_lastComponentNewFloorTags}");

            foreach (var kv in _registry.TilesByBuildingId)
            {
                if (!_registry.TryGetBuildingExtent(kv.Key, out var extent) || !extent.HasBounds)
                    continue;

                int sliceCount = extent.FloorFootprintByCellY.Count;
                Debug.Log(
                    $"[BuildingGroupBuilder] extent id={extent.BuildingId} " +
                    $"aabb=({extent.MinX},{extent.MinOccupiedY},{extent.MinZ})-({extent.MaxX},{extent.MaxOccupiedY},{extent.MaxZ}) " +
                    $"maxStructuralY={extent.MaxStructuralY} floorSlices={sliceCount}");
                break;
            }

            int spaceCount = 0;
            int outdoorSpaces = 0;
            foreach (int spaceId in _hub.Spaces.Registry.SpaceIds)
            {
                spaceCount++;
                if (_hub.Spaces.IsOutdoorSpace(spaceId))
                    outdoorSpaces++;
            }

            if (spaceCount > 0)
            {
                Debug.Log(
                    $"[BuildingGroupBuilder] spaces={spaceCount} outdoor={outdoorSpaces} indoor={spaceCount - outdoorSpaces}");
                LogSpaceLeakDiagnosis();
            }

            int shellTagged = 0;
            int shellUntagged = 0;
            int shellTaggedMaxY = int.MinValue;
            foreach (var tile in _model.TilesSnapshot)
            {
                if (!TileIdentityUtil.IsStructural(tile.identity))
                    continue;

                int bid = tile.identity.buildingId;
                if (BuildingIdBakeRules.CanPropagateBuildingIdFrom(bid))
                {
                    shellTagged++;
                    int y = OccupiedCellCoord.PrimaryCellFromIdentity(tile.identity).y;
                    if (y > shellTaggedMaxY)
                        shellTaggedMaxY = y;
                }
                else
                {
                    shellUntagged++;
                }
            }

            Debug.Log(
                $"[BuildingGroupBuilder] structuralShell flood visited={_lastStructuralFloodVisited} " +
                $"patched={_lastStructuralFloodPatched} bridgedFloors={_lastStructuralFloodBridgedFloors} | " +
                $"afterBake tagged={shellTagged} untagged={shellUntagged} taggedMaxY={shellTaggedMaxY}");

            LogBuildingConnectionDiagnosisFromRegistry();
            LogSpaceLeakDetailForProbeCells();
        }

        readonly List<(Vector3Int floorCell, Vector3Int neighbor, string reason)> _lateralLeakDiagScratch = new();
        readonly List<(int x, int z, int probeY, string reason)> _ceilingLeakDiagScratch = new();

        void LogSpaceLeakDetailForProbeCells()
        {
            LogIndoorSpaceInventory();
            LogSpaceLeakDetailAtFloorCell("edge1F-z3-strip", 3, 0, 3);
            LogSpaceLeakDetailAtFloorCell("macro-center-upper", 3, 2, 3);
            LogSpaceLeakDetailAtFloorCell("east-macro", 12, 0, 2);
        }

        void LogIndoorSpaceInventory()
        {
            var registry = _hub.Spaces.Registry;
            int indoorCount = 0;
            foreach (int spaceId in registry.SpaceIds)
            {
                if (!registry.TryGetSpace(spaceId, out var space) || space.IsOutdoor)
                    continue;

                indoorCount++;
                var cells = registry.GetFloorCells(spaceId);
                Debug.Log(
                    $"[SpaceLeakDetail] indoorSpace id={spaceId} buildingId={space.BuildingId} " +
                    $"floorCells={cells.Count} sample={FirstFloorCellSample(cells)}");
            }

            Debug.Log($"[SpaceLeakDetail] indoorSpaceInventory count={indoorCount}");
        }

        void LogSpaceLeakDetailAtFloorCell(string probeLabel, int x, int cellY, int z)
        {
            var probe = new Vector3Int(x, cellY, z);
            if (!_topology.Index.CellHasFloor(x, cellY, z))
            {
                Debug.Log($"[SpaceLeakDetail] label={probeLabel} probe={probe} noWalkableFloor");
                return;
            }

            if (!_hub.Spaces.TryGetSpaceAtFloorCell(probe, out int spaceId) ||
                !_hub.Spaces.TryGetSpace(spaceId, out var space))
            {
                Debug.Log($"[SpaceLeakDetail] label={probeLabel} probe={probe} noSpace");
                return;
            }

            if (!_registry.TryGetBuildingExtent(space.BuildingId, out var extent))
            {
                Debug.Log(
                    $"[SpaceLeakDetail] label={probeLabel} probe={probe} spaceId={spaceId} buildingId={space.BuildingId} noExtent");
                return;
            }

            var floorCells = _hub.Spaces.Registry.GetFloorCells(spaceId);
            var index = _topology.Index;
            Debug.Log(
                $"[SpaceLeakDetail] label={probeLabel} probe={probe} spaceId={spaceId} buildingId={space.BuildingId} " +
                $"isOutdoor={space.IsOutdoor} maxStructuralY={extent.MaxStructuralY} floorCells={floorCells.Count}");

            LogSpaceLeakDiagnostics(
                "[SpaceLeakDetail]",
                spaceId,
                space,
                floorCells,
                space.BuildingId,
                extent,
                index,
                lateralPrefix: "[SpaceLeakDetail] lateral",
                ceilingPrefix: "[SpaceLeakDetail] ceiling",
                includeSummaryLine: true);
        }

        void LogSpaceLeakDiagnostics(
            string logPrefix,
            int spaceId,
            SpaceBakeResult space,
            IReadOnlyCollection<Vector3Int> floorCells,
            int buildingId,
            BuildingExtent extent,
            FloorMapIndex index,
            string lateralPrefix,
            string ceilingPrefix,
            bool includeSummaryLine = false)
        {
            SpaceLeakEvaluator.EvaluateComponents(
                floorCells, buildingId, extent, index,
                out bool ceilingLeak, out bool lateralLeak);

            if (includeSummaryLine)
            {
                Debug.Log(
                    $"{logPrefix} spaceId={spaceId} buildingId={buildingId} " +
                    $"ceilingLeak={ceilingLeak} lateralLeak={lateralLeak}");
            }
            else
            {
                Debug.Log(
                    $"{logPrefix} space id={spaceId} floorCells={floorCells.Count} sample={FirstFloorCellSample(floorCells)} " +
                    $"isOutdoor={space.IsOutdoor} ceilingLeak={ceilingLeak} lateralLeak={lateralLeak}");
            }

            SpaceLeakEvaluator.DiagnoseLateralLeaks(
                floorCells, buildingId, extent, index, _lateralLeakDiagScratch);
            for (int i = 0; i < _lateralLeakDiagScratch.Count; i++)
            {
                var (floorCell, neighbor, reason) = _lateralLeakDiagScratch[i];
                Debug.Log(
                    $"{lateralPrefix} floor={floorCell} neighbor={neighbor} reason={reason} " +
                    $"{FormatEdgeBetweenNote(index, floorCell, neighbor)} {DescribeOccupiedCellBuildingIds(neighbor)}");
            }

            SpaceLeakEvaluator.DiagnoseCeilingLeaks(
                floorCells, buildingId, extent, index, _ceilingLeakDiagScratch);
            for (int i = 0; i < _ceilingLeakDiagScratch.Count; i++)
            {
                var (cx, cz, probeY, reason) = _ceilingLeakDiagScratch[i];
                Debug.Log($"{ceilingPrefix} column=({cx},{cz}) {reason}");
            }
        }

        static Vector3Int FirstFloorCellSample(IReadOnlyCollection<Vector3Int> floorCells)
        {
            foreach (var cell in floorCells)
                return cell;

            return default;
        }

        static string FormatEdgeBetweenNote(FloorMapIndex index, Vector3Int cellA, Vector3Int cellB) =>
            index.TryGetEdgeBetween(cellA, cellB, out var edge)
                ? $"edgeBid={edge.identity.buildingId}"
                : "edge=none";

        string DescribeOccupiedCellBuildingIds(Vector3Int cell)
        {
            if (!_topology.TryCollectTilesAtOccupiedCell(cell, _occupiedCellCollectScratch))
                return "occ=empty";

            var parts = new List<string>(_occupiedCellCollectScratch.Count);
            for (int i = 0; i < _occupiedCellCollectScratch.Count; i++)
            {
                TileData tile = _occupiedCellCollectScratch[i];
                parts.Add($"{tile.identity.PrefabId}@bid={tile.identity.buildingId}");
            }

            return "occ=[" + string.Join(", ", parts) + "]";
        }

        void LogBuildingConnectionDiagnosisFromRegistry()
        {
            if (!Config.DebugMode.FloorAlgorithm)
                return;

            var ids = new List<int>(_registry.TilesByBuildingId.Keys);
            ids.Sort();
            if (ids.Count < 2)
            {
                Debug.Log(
                    $"[BuildingDiagnosis] bakedBuildingCount={ids.Count} (need 2+ for pair diagnosis)");
                return;
            }

            LogBuildingConnectionDiagnosis(ids[0], ids[1]);
        }

        void LogSpaceLeakDiagnosis()
        {
            var registry = _hub.Spaces.Registry;
            var index = _topology.Index;
            int ceilingOnly = 0;
            int lateralOnly = 0;
            int both = 0;
            int indoor = 0;

            foreach (int spaceId in registry.SpaceIds)
            {
                if (!registry.TryGetSpace(spaceId, out var space))
                    continue;

                if (!_registry.TryGetBuildingExtent(space.BuildingId, out var extent))
                    continue;

                var cells = registry.GetFloorCells(spaceId);
                SpaceLeakEvaluator.EvaluateComponents(
                    cells, space.BuildingId, extent, index, out bool ceiling, out bool lateral);

                if (!ceiling && !lateral)
                    indoor++;
                else if (ceiling && lateral)
                    both++;
                else if (ceiling)
                    ceilingOnly++;
                else
                    lateralOnly++;
            }

            Debug.Log(
                $"[BuildingGroupBuilder] spaceLeak: indoor={indoor} ceilingOnly={ceilingOnly} " +
                $"lateralOnly={lateralOnly} both={both}");
        }

        void LogBuildingConnectionDiagnosis(int idA, int idB)
        {
            if (!Config.DebugMode.FloorAlgorithm)
                return;

            if (!_registry.TilesByBuildingId.ContainsKey(idA) ||
                !_registry.TilesByBuildingId.ContainsKey(idB))
            {
                Debug.Log(
                    $"[BuildingDiagnosis] id={idA}∨{idB} 없음. bakedBuildingCount={_registry.TilesByBuildingId.Count}");
                return;
            }

            if (!_registry.TryGetBuildingExtent(idA, out var extA) || !extA.HasBounds ||
                !_registry.TryGetBuildingExtent(idB, out var extB) || !extB.HasBounds)
            {
                Debug.Log($"[BuildingDiagnosis] id={idA} or {idB} extent 없음.");
                return;
            }

            Debug.Log(
                $"[BuildingDiagnosis] id={idA} aabb=({extA.MinX},{extA.MinOccupiedY},{extA.MinZ})-" +
                $"({extA.MaxX},{extA.MaxOccupiedY},{extA.MaxZ}) maxStructuralY={extA.MaxStructuralY}");
            Debug.Log(
                $"[BuildingDiagnosis] id={idB} aabb=({extB.MinX},{extB.MinOccupiedY},{extB.MinZ})-" +
                $"({extB.MaxX},{extB.MaxOccupiedY},{extB.MaxZ}) maxStructuralY={extB.MaxStructuralY}");

            int cardinalFloorTouches = 0;
            int nearestFloorChebyshev = int.MaxValue;
            (int x, int z, int y) nearestA = default;
            (int x, int z, int y) nearestB = default;

            for (int cellY = _minCellY; cellY <= _maxCellY; cellY++)
            {
                if (!extA.TryGetFloorFootprint(cellY, out var footA) ||
                    !extB.TryGetFloorFootprint(cellY, out var footB))
                    continue;

                foreach (var (ax, az) in footA)
                {
                    foreach (var d in CardinalDirs)
                    {
                        int nx = ax + d.x;
                        int nz = az + d.z;
                        if (!FootprintContains(footB, nx, nz))
                            continue;

                        cardinalFloorTouches++;
                        int bidA = GetFloorBuildingId(ax, cellY, az);
                        int bidB = GetFloorBuildingId(nx, cellY, nz);
                        Debug.Log(
                            $"[BuildingDiagnosis] sliceY={cellY} cardinal floor touch A=({ax},{cellY},{az}) bid={bidA} " +
                            $"B=({nx},{cellY},{nz}) bid={bidB}");
                    }

                    foreach (var (bx, bz) in footB)
                    {
                        int cheb = Math.Max(Math.Abs(ax - bx), Math.Abs(az - bz));
                        if (cheb >= nearestFloorChebyshev)
                            continue;

                        nearestFloorChebyshev = cheb;
                        nearestA = (ax, az, cellY);
                        nearestB = (bx, bz, cellY);
                    }
                }
            }

            if (cardinalFloorTouches == 0)
            {
                Debug.Log(
                    $"[BuildingDiagnosis] 같은 slice에 cardinal 인접 floor **없음** → MergeBuildingsOnFloorAdjacency 대상 아님. " +
                    $"최근 floor chebyshev={nearestFloorChebyshev} A=({nearestA.x},{nearestA.y},{nearestA.z}) B=({nearestB.x},{nearestB.y},{nearestB.z})");
            }

            int shellBetweenWrongId = 0;
            int shellBetweenUntagged = 0;
            if (nearestFloorChebyshev < int.MaxValue && nearestFloorChebyshev <= 3)
            {
                int minX = Math.Min(nearestA.x, nearestB.x) - 1;
                int maxX = Math.Max(nearestA.x, nearestB.x) + 1;
                int minZ = Math.Min(nearestA.z, nearestB.z) - 1;
                int maxZ = Math.Max(nearestA.z, nearestB.z) + 1;
                int minY = Math.Min(nearestA.y, nearestB.y);
                int maxY = Math.Max(nearestA.y, nearestB.y) + 2;

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        for (int z = minZ; z <= maxZ; z++)
                        {
                            if (!_topology.TryCollectTilesAtOccupiedCell(x, y, z, _occupiedCellCollectScratch))
                                continue;

                            for (int i = 0; i < _occupiedCellCollectScratch.Count; i++)
                            {
                                TileData tile = _occupiedCellCollectScratch[i];
                                if (!BuildingIdBakeRules.ShouldPatchBuildingIdAtOccupiedCell(tile.identity))
                                    continue;

                                int bid = tile.identity.buildingId;
                                if (bid == 0)
                                    shellBetweenUntagged++;
                                else if (bid != idA && bid != idB)
                                    shellBetweenWrongId++;
                            }
                        }
                    }
                }

                Debug.Log(
                    $"[BuildingDiagnosis] nearest gap box shell untagged={shellBetweenUntagged} otherBuildingId={shellBetweenWrongId} " +
                    $"mismatchShell3={CountShellWithBuildingIdInBox(minX, maxX, minY, maxY, minZ, maxZ, idA)} " +
                    $"mismatchShell24={CountShellWithBuildingIdInBox(minX, maxX, minY, maxY, minZ, maxZ, idB)}");
            }
        }

        int CountShellWithBuildingIdInBox(
            int minX, int maxX, int minY, int maxY, int minZ, int maxZ, int buildingId)
        {
            int count = 0;
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        if (!_topology.TryCollectTilesAtOccupiedCell(x, y, z, _occupiedCellCollectScratch))
                            continue;

                        for (int i = 0; i < _occupiedCellCollectScratch.Count; i++)
                        {
                            TileData tile = _occupiedCellCollectScratch[i];
                            if (!BuildingIdBakeRules.ShouldPatchBuildingIdAtOccupiedCell(tile.identity))
                                continue;

                            if (tile.identity.buildingId == buildingId)
                                count++;
                        }
                    }
                }
            }

            return count;
        }

        static bool FootprintContains(IReadOnlyCollection<(int x, int z)> foot, int x, int z)
        {
            foreach (var (fx, fz) in foot)
            {
                if (fx == x && fz == z)
                    return true;
            }

            return false;
        }

        void VerifyOccupancyIndexAfterBake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!Config.DebugMode.FloorAlgorithm)
                return;

            foreach (var tile in _model.TilesSnapshot)
            {
                if (!TileIdentityUtil.IsStructural(tile.identity))
                    continue;

                Vector3Int primary = OccupiedCellCoord.PrimaryCellFromIdentity(tile.identity);
                if (_topology.Index.HasAnyTile(primary.x, primary.z, primary.y))
                    continue;

                Debug.LogWarning(
                    $"[BuildingGroupBuilder] 점유 인덱스 미등록: prefab={tile.identity.PrefabId} primary={primary}");
            }
#endif
        }
    }
}
