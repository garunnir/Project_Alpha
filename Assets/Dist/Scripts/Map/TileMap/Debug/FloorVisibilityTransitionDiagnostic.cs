// ============================================================
// FloorVisibilityTransitionDiagnostic — 실내→실외 층 hide 전환 진단 (Editor)
// ============================================================
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using IsoTilemap;
using UnityEditor;
using UnityEngine;

namespace IsoTilemap.Diagnostics
{
    public static class FloorVisibilityTransitionDiagnostic
    {
        const string LogTag = "[FloorVisDiag]";

        sealed class DiagRegistry : ITileViewRegistry
        {
            readonly Dictionary<Guid, TileView> _views = new();

            public void Add(Guid id, TileView view) => _views[id] = view;

            public bool TryGetView(Guid tileId, out TileView view) => _views.TryGetValue(tileId, out view);

            public void CollectSpawnedTileIds(List<Guid> into)
            {
                into.Clear();
                foreach (var kv in _views)
                    into.Add(kv.Key);
            }

            public void DestroyAll()
            {
                foreach (var kv in _views)
                {
                    if (kv.Value != null)
                        UnityEngine.Object.DestroyImmediate(kv.Value.gameObject);
                }

                _views.Clear();
            }
        }

        [MenuItem("Tools/Diagnostic/Floor Visibility Channels Indoor-Outdoor Transition")]
        public static void Run()
        {
            var report = new StringBuilder();
            report.AppendLine($"{LogTag} === Floor visibility indoor→outdoor diagnostic ===");

            RunH3IsolatedTest(report);

            Transform root = null;
            DiagRegistry registry = null;
            TileMapModel model = null;
            TileViewPresentationApplier applier = null;

            try
            {
                string mapPath = Path.Combine(Application.dataPath, "..", "map01.json");
                if (!File.Exists(mapPath))
                {
                    Debug.LogError($"{LogTag} map01.json not found at {mapPath}");
                    return;
                }

                var loadResult = new MapLoadPipeline(
                    new TileMapSerializer(),
                    new TileMapModelBuilder(),
                    new TileMapDtoMapper()).Load(mapPath);

                if (loadResult.Model is not TileMapModel loadedModel)
                {
                    Debug.LogError($"{LogTag} Failed to load TileMapModel.");
                    return;
                }

                model = loadedModel;

                float cellSize = 1f;
                var buildingRegistry = new BuildingGroupRegistry();
                var hub = TileMapCacheHub.Create(model, buildingRegistry);
                model.SetMapCacheHub(hub);
                var builder = new BuildingGroupBuilder(model, hub);
                hub.BindRoomBakeBuilder(builder);
                model.SetBuildingGroupBuilder(builder);
                builder.AssignAll();

                var policy = PlayerFloorVisibilityPolicy.Build(hub, cellSize, buildingRegistry);
                registry = new DiagRegistry();
                root = new GameObject("FloorVisDiag_Root").transform;

                foreach (TileData tile in model.TilesSnapshot)
                {
                    var go = new GameObject($"diag_{tile.tileDefId:N}");
                    go.transform.SetParent(root, false);
                    EnsureMinimalViewVisual(go);
                    var view = go.AddComponent<TileView>();
                    view.UpdateTile(tile, cellSize);
                    registry.Add(tile.tileDefId, view);
                }

                applier = new TileViewPresentationApplier(registry, model);
                applier.ConfigureFloorVisibility(
                    policy,
                    buildingRegistry,
                    hub,
                    StructuralHidePresentationMode.DisableGameObject);
                model.OnTileOcclusionPresentationDelta += applier.ApplyOcclusionDelta;

                if (!TryPickIndoorScenario(model, hub, buildingRegistry, out int buildingId, out int playerFloorY, out List<Guid> upperFloorTileIds))
                {
                    Debug.LogWarning($"{LogTag} No multi-floor indoor building found in map01.");
                    return;
                }

                bool hasPlayerWorld = TryResolvePlayerWorld(
                    model, buildingRegistry, buildingId, cellSize, out Vector3 playerWorld);
                FloorVisibilityContext resolvedPlayerCtx = hasPlayerWorld
                    ? policy.ResolveContext(playerWorld.y, playerWorld)
                    : default;

                report.AppendLine($"{LogTag} Scenario: buildingId={buildingId} playerFloorY={playerFloorY} upperFloorTiles={upperFloorTileIds.Count}");

                var indoorCtx = new FloorVisibilityContext(
                    isPlayerOutdoor: false,
                    playerFloorCellY: hasPlayerWorld ? resolvedPlayerCtx.PlayerFloorCellY : playerFloorY,
                    minCellY: policy.MinCellY,
                    playerBuildingId: resolvedPlayerCtx.PlayerBuildingId > 0 ? resolvedPlayerCtx.PlayerBuildingId : buildingId,
                    playerBlockingBuildingIds: new HashSet<int>(),
                    visibleBelowCells: hasPlayerWorld ? resolvedPlayerCtx.VisibleBelowCells : new HashSet<(int x, int z, int y)>(),
                    playerSpaceId: hasPlayerWorld ? resolvedPlayerCtx.PlayerSpaceId : 0,
                    playerSpaceMinY: hasPlayerWorld ? resolvedPlayerCtx.PlayerSpaceMinY : playerFloorY,
                    playerSpaceMaxY: hasPlayerWorld ? resolvedPlayerCtx.PlayerSpaceMaxY : playerFloorY,
                    playerSpaceFloorCells: hasPlayerWorld ? resolvedPlayerCtx.PlayerSpaceFloorCells : null);

                applier.SyncFloorVisibility(in indoorCtx, model);
                AppendPhaseReport(report, "AfterIndoorSync", applier, policy, model, in indoorCtx, upperFloorTileIds, registry);
                AppendPlayerSpaceWallReport(report, "AfterIndoorSync", applier, model, in indoorCtx, buildingId);

                int occlusionSample = Mathf.Min(10, upperFloorTileIds.Count);
                var proximityApply = new List<(Guid tileId, float occlusion01)>(occlusionSample);
                for (int i = 0; i < occlusionSample; i++)
                    proximityApply.Add((upperFloorTileIds[i], 1f));
                applier.ApplyProximityBlendDelta(new TileOcclusionPresentationDelta(
                    proximityApply,
                    Array.Empty<Guid>()));
                AppendOcclusionReport(report, "AfterIndoorProximityInject", applier, upperFloorTileIds, occlusionSample, registry);

                var bfsApply = new List<(Guid tileId, float occlusion01)>();
                for (int i = 0; i < upperFloorTileIds.Count; i++)
                {
                    if (!model.TryGetTileById(upperFloorTileIds[i], out TileData t))
                        continue;
                    if (TileIdentityUtil.IsVerticalFace(t.identity))
                        bfsApply.Add((upperFloorTileIds[i], 1f));
                }

                if (bfsApply.Count > 0)
                {
                    applier.ApplyOcclusionDelta(new TileOcclusionPresentationDelta(
                        bfsApply,
                        Array.Empty<Guid>()));
                    AppendOcclusionReport(report, "AfterIndoorBfsInject", applier, upperFloorTileIds, occlusionSample, registry);
                    AppendStructuralParityReport(report, "AfterIndoorBfsInject", applier, policy, model, in indoorCtx, upperFloorTileIds);
                }

                var outdoorCtx = new FloorVisibilityContext(
                    isPlayerOutdoor: true,
                    playerFloorCellY: playerFloorY,
                    minCellY: policy.MinCellY,
                    playerBuildingId: 0,
                    playerBlockingBuildingIds: new HashSet<int>(),
                    visibleBelowCells: new HashSet<(int x, int z, int y)>());

                applier.SyncFloorVisibility(in outdoorCtx, model);
                AppendPhaseReport(report, "AfterOutdoorSync_NoBlocking", applier, policy, model, in outdoorCtx, upperFloorTileIds, registry);
                AppendOcclusionReport(report, "AfterOutdoorSync_NoBlocking", applier, upperFloorTileIds, occlusionSample, registry);
                AppendStructuralParityReport(report, "AfterOutdoorSync_NoBlocking", applier, policy, model, in outdoorCtx, upperFloorTileIds);

                if (hasPlayerWorld)
                {
                    RunModelOcclusionPathScenario(
                        report,
                        model,
                        applier,
                        policy,
                        in indoorCtx,
                        in outdoorCtx,
                        upperFloorTileIds,
                        occlusionSample,
                        registry,
                        playerWorld,
                        playerFloorY,
                        cellSize);
                }
                else
                {
                    report.AppendLine($"{LogTag} Model path: skipped (no player world for building {buildingId})");
                }

                ClassifyHypotheses(report, applier, policy, model, in outdoorCtx, upperFloorTileIds, registry);

                var blocking = new HashSet<int> { buildingId };
                var outdoorBlockingCtx = new FloorVisibilityContext(
                    isPlayerOutdoor: true,
                    playerFloorCellY: playerFloorY,
                    minCellY: policy.MinCellY,
                    playerBuildingId: 0,
                    playerBlockingBuildingIds: blocking,
                    visibleBelowCells: new HashSet<(int x, int z, int y)>());

                applier.SyncFloorVisibility(in outdoorBlockingCtx, model);
                AppendPhaseReport(report, "AfterOutdoorSync_WithBlocking", applier, policy, model, in outdoorBlockingCtx, upperFloorTileIds, registry);
            }
            finally
            {
                if (model != null && applier != null)
                    model.OnTileOcclusionPresentationDelta -= applier.ApplyOcclusionDelta;

                registry?.DestroyAll();
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root.gameObject);
            }

            string reportText = report.ToString();
            string reportPath = Path.Combine(Application.dataPath, "..", "Logs", "floor_visibility_channels_diag.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? ".");
            File.WriteAllText(reportPath, reportText);
            Debug.Log(reportText);
            Debug.Log($"{LogTag} Report written: {reportPath}");
        }

        static void EnsureMinimalViewVisual(GameObject go)
        {
            var rendGo = new GameObject("render");
            rendGo.transform.SetParent(go.transform, false);
            rendGo.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            rendGo.AddComponent<MeshRenderer>().sharedMaterial =
                new Material(Shader.Find("Universal Render Pipeline/Lit"));
            rendGo.AddComponent<ShadeObjectController>();
        }

        static void RunH3IsolatedTest(StringBuilder report)
        {
            GameObject go = null;
            try
            {
                go = new GameObject("h3_isolated");
                EnsureMinimalViewVisual(go);
                var view = go.AddComponent<TileView>();

                go.SetActive(false);
                SetPrivateField(view, "_structuralVisibilityHidden", false);

                bool activeBefore = go.activeSelf;
                view.ApplyResolvedPresentation(new TilePresentationResolved(
                    structuralHidden: false,
                    sightLineTrace: false,
                    characterOcclusion: 0f,
                    ghosted: false,
                    selected: false,
                    damageStage: 0));
                bool activeAfter = go.activeSelf;

                report.AppendLine($"{LogTag} H3 isolated: activeBefore={activeBefore} activeAfter={activeAfter}");
                if (!activeBefore && !activeAfter)
                    report.AppendLine($"{LogTag} → H3 CONFIRMED (ApplyStructuralHidden early-return leaves GO inactive)");
                else
                    report.AppendLine($"{LogTag} → H3 REJECTED in isolated test");
            }
            finally
            {
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
            }
        }

        static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }

        static bool TryPickIndoorScenario(
            TileMapModel model,
            TileMapCacheHub hub,
            BuildingGroupRegistry buildingRegistry,
            out int buildingId,
            out int playerFloorY,
            out List<Guid> upperFloorTileIds)
        {
            buildingId = 0;
            playerFloorY = 0;
            upperFloorTileIds = new List<Guid>();
            int bestUpperCount = 0;

            var seen = new HashSet<int>();
            foreach (TileData tile in model.TilesSnapshot)
            {
                int bid = tile.identity.buildingId;
                if (bid <= 0 || !seen.Add(bid))
                    continue;

                if (!hub.Buildings.TryGetBuildingExtent(bid, out BuildingExtent extent) || !extent.HasBounds)
                    continue;

                if (extent.MaxOccupiedY <= extent.MinOccupiedY)
                    continue;

                var upper = new List<Guid>();
                buildingRegistry.EnumerateTilesForBuilding(bid, tileId =>
                {
                    if (!model.TryGetTileById(tileId, out TileData t))
                        return;

                    if (TileVisibilityCellUtil.GetCellY(t) > extent.MinOccupiedY)
                        upper.Add(tileId);
                });

                if (upper.Count <= bestUpperCount)
                    continue;

                bestUpperCount = upper.Count;
                buildingId = bid;
                playerFloorY = extent.MinOccupiedY;
                upperFloorTileIds = upper;
            }

            return buildingId > 0 && upperFloorTileIds.Count > 0;
        }

        static bool TryResolvePlayerWorld(
            TileMapModel model,
            BuildingGroupRegistry registry,
            int buildingId,
            float cellSize,
            out Vector3 playerWorld)
        {
            playerWorld = default;
            int bestSliceY = int.MaxValue;
            Vector3Int bestWalkable = default;
            bool found = false;

            registry.EnumerateTilesForBuilding(buildingId, tileId =>
            {
                if (!model.TryGetTileById(tileId, out TileData tile))
                    return;
                if (!TileIdentityUtil.IsHorizontalFace(tile.identity))
                    return;

                int sliceY = TileVisibilityCellUtil.GetCellY(tile);
                var walkable = FloorFaceKey.FromFloorTileIdentity(tile.identity).CellAbove;
                if (sliceY >= bestSliceY)
                    return;

                bestSliceY = sliceY;
                bestWalkable = walkable;
                found = true;
            });

            if (!found)
                return false;

            playerWorld = TileHelper.ConvertGridToWorldPos(bestWalkable, cellSize);
            playerWorld.y += cellSize * 0.5f;
            return true;
        }

        static void RunModelOcclusionPathScenario(
            StringBuilder report,
            TileMapModel model,
            TileViewPresentationApplier applier,
            PlayerFloorVisibilityPolicy policy,
            in FloorVisibilityContext indoorCtx,
            in FloorVisibilityContext outdoorCtx,
            List<Guid> upperFloorTileIds,
            int occlusionSample,
            DiagRegistry registry,
            Vector3 playerWorld,
            int playerFloorY,
            float cellSize)
        {
            report.AppendLine($"{LogTag} --- Model path (BFS evaluate + policy filter) playerWorld={playerWorld} ---");

            var settings = OcclusionProximitySettings.DefaultUnity;
            settings.CellSize = cellSize;

            applier.SyncFloorVisibility(in indoorCtx, model);
            FloorVisibilityContext indoorCtxCopy = indoorCtx;
            model.UpdateOcclusionFromPlayerWorld(
                playerWorld,
                playerFloorY,
                settings,
                tile => policy.IsTileVisible(tile, in indoorCtxCopy));
            AppendOcclusionReport(report, "ModelPath_IndoorBfs", applier, upperFloorTileIds, occlusionSample, registry);
            AppendStructuralParityReport(report, "ModelPath_IndoorBfs", applier, policy, model, in indoorCtx, upperFloorTileIds);

            applier.SyncFloorVisibility(in outdoorCtx, model);
            FloorVisibilityContext outdoorCtxCopy = outdoorCtx;
            model.UpdateOcclusionFromPlayerWorld(
                playerWorld,
                playerFloorY,
                settings,
                tile => policy.IsTileVisible(tile, in outdoorCtxCopy));
            AppendOcclusionReport(report, "ModelPath_OutdoorSamePos", applier, upperFloorTileIds, occlusionSample, registry);
            AppendStructuralParityReport(report, "ModelPath_OutdoorSamePos", applier, policy, model, in outdoorCtx, upperFloorTileIds);

            int structuralParityMismatch = CountStructuralParityMismatch(
                applier, policy, model, in outdoorCtx, upperFloorTileIds);
            report.AppendLine($"{LogTag} ModelPath_OutdoorSamePos structural parity mismatch={structuralParityMismatch}");
        }

        static int CountStructuralParityMismatch(
            TileViewPresentationApplier applier,
            PlayerFloorVisibilityPolicy policy,
            TileMapModel model,
            in FloorVisibilityContext ctx,
            List<Guid> tileIds)
        {
            int mismatch = 0;
            for (int i = 0; i < tileIds.Count; i++)
            {
                Guid tileId = tileIds[i];
                if (!model.TryGetTileById(tileId, out TileData tile))
                    continue;

                bool hidden = applier.IsStructuralVisibilityHidden(tileId);
                bool visible = policy.IsTileVisible(tile, in ctx);
                if (hidden != !visible)
                    mismatch++;
            }

            return mismatch;
        }

        static void AppendPlayerSpaceWallReport(
            StringBuilder report,
            string phase,
            TileViewPresentationApplier applier,
            TileMapModel model,
            in FloorVisibilityContext ctx,
            int buildingId)
        {
            int spaceWalls = 0;
            int hiddenSpaceWalls = 0;
            int spaceFloors = 0;
            int hiddenSpaceFloors = 0;

            foreach (TileData tile in model.TilesSnapshot)
            {
                if (tile.identity.buildingId != buildingId)
                    continue;

                bool touchesSpace = SpaceVisibilityUtil.TouchesPlayerSpace(tile.identity, in ctx);
                if (!touchesSpace)
                    continue;

                bool hidden = applier.IsStructuralVisibilityHidden(tile.tileDefId);
                if (TileIdentityUtil.IsVerticalFace(tile.identity))
                {
                    spaceWalls++;
                    if (hidden) hiddenSpaceWalls++;
                }
                else if (TileIdentityUtil.IsHorizontalFace(tile.identity))
                {
                    spaceFloors++;
                    if (hidden) hiddenSpaceFloors++;
                }
            }

            report.AppendLine(
                $"{LogTag} {phase} playerSpace id={ctx.PlayerSpaceId} band={ctx.PlayerSpaceMinY}..{ctx.PlayerSpaceMaxY} " +
                $"spaceEdgeWalls hidden/total={hiddenSpaceWalls}/{spaceWalls} " +
                $"spaceFloors hidden/total={hiddenSpaceFloors}/{spaceFloors}");
        }

        static void AppendPhaseReport(
            StringBuilder report,
            string phase,
            TileViewPresentationApplier applier,
            PlayerFloorVisibilityPolicy policy,
            TileMapModel model,
            in FloorVisibilityContext ctx,
            List<Guid> upperFloorTileIds,
            DiagRegistry registry)
        {
            int stuckCount = 0;
            int hiddenApplied = 0;
            int policyHide = 0;

            report.AppendLine($"{LogTag} --- {phase} ctx: outdoor={ctx.IsPlayerOutdoor} buildingId={ctx.PlayerBuildingId} blocking={ctx.PlayerBlockingBuildingIds?.Count ?? 0} ---");

            for (int i = 0; i < upperFloorTileIds.Count; i++)
            {
                Guid tileId = upperFloorTileIds[i];
                if (!registry.TryGetView(tileId, out TileView view))
                    continue;

                if (!model.TryGetTileById(tileId, out TileData tile))
                    continue;

                bool structuralHidden = applier.IsStructuralVisibilityHidden(tileId);
                bool visible = policy.IsTileVisible(tile, in ctx);
                bool active = view.gameObject.activeSelf;

                if (structuralHidden) hiddenApplied++;
                if (!visible) policyHide++;

                if (visible && !structuralHidden && !active)
                {
                    stuckCount++;
                    report.AppendLine($"{LogTag} STUCK(H3?) tile={tileId} IsTileVisible=true structuralHidden=false activeSelf=false");
                }
                else if (visible && structuralHidden)
                {
                    stuckCount++;
                    report.AppendLine($"{LogTag} STUCK(H4?) tile={tileId} IsTileVisible=true structuralHidden=true activeSelf={active}");
                }
            }

            report.AppendLine($"{LogTag} {phase} summary: upper={upperFloorTileIds.Count} structuralHidden={hiddenApplied} policyHide={policyHide} stuck={stuckCount}");
        }

        static void AppendStructuralParityReport(
            StringBuilder report,
            string phase,
            TileViewPresentationApplier applier,
            PlayerFloorVisibilityPolicy policy,
            TileMapModel model,
            in FloorVisibilityContext ctx,
            List<Guid> upperFloorTileIds)
        {
            int floorHidden = 0;
            int floorVisible = 0;
            int wallHidden = 0;
            int wallVisible = 0;
            int floorOcc = 0;
            int wallOcc = 0;

            for (int i = 0; i < upperFloorTileIds.Count; i++)
            {
                Guid tileId = upperFloorTileIds[i];
                if (!model.TryGetTileById(tileId, out TileData tile))
                    continue;

                bool hidden = applier.IsStructuralVisibilityHidden(tileId);
                bool visible = policy.IsTileVisible(tile, in ctx);
                float occ = applier.Resolve(tileId).CharacterOcclusion;
                bool isWall = TileIdentityUtil.IsVerticalFace(tile.identity);

                if (isWall)
                {
                    if (hidden) wallHidden++;
                    else wallVisible++;
                    if (occ > 0.015f) wallOcc++;
                }
                else if (TileIdentityUtil.IsHorizontalFace(tile.identity))
                {
                    if (hidden) floorHidden++;
                    else floorVisible++;
                    if (occ > 0.015f) floorOcc++;
                }
            }

            report.AppendLine(
                $"{LogTag} {phase} parity: floor hidden/visible={floorHidden}/{floorVisible} occ={floorOcc} | " +
                $"edgewall hidden/visible={wallHidden}/{wallVisible} occ={wallOcc}");
        }

        static void AppendOcclusionReport(
            StringBuilder report,
            string phase,
            TileViewPresentationApplier applier,
            List<Guid> upperFloorTileIds,
            int sampleCount,
            DiagRegistry registry)
        {
            int proxEngaged = 0;
            int bfsEngaged = 0;
            int resolvedOcc = 0;
            int displayCache = 0;
            int viewOcc = 0;
            int stuckOcc = 0;
            var entries = applier.Entries;

            for (int i = 0; i < sampleCount && i < upperFloorTileIds.Count; i++)
            {
                Guid tileId = upperFloorTileIds[i];
                bool prox = entries.IsSourceEngagedForTile(tileId, PresentationSource.ProximitySightLine);
                bool bfs = entries.IsSourceEngagedForTile(tileId, PresentationSource.BfsWallOcclusion);
                float occ = applier.Resolve(tileId).CharacterOcclusion;
                float display = TryGetApplierDisplay(applier, tileId);
                float viewOcclusion = 0f;
                if (registry.TryGetView(tileId, out TileView view))
                    viewOcclusion = TryGetViewOcclusion(view);

                if (prox) proxEngaged++;
                if (bfs) bfsEngaged++;
                if (occ > 0.015f) resolvedOcc++;
                if (display > 0.015f) displayCache++;
                if (viewOcclusion > 0.015f) viewOcc++;

                bool stuck = prox || bfs || occ > 0.015f || display > 0.015f || viewOcclusion > 0.015f;
                if (stuck && phase.StartsWith("AfterOutdoor"))
                {
                    stuckOcc++;
                    report.AppendLine(
                        $"{LogTag} STUCK(CH) tile={tileId} prox={prox} bfs={bfs} " +
                        $"resolvedOcc={occ:F3} display={display:F3} viewOcc={viewOcclusion:F3} " +
                        $"structuralHidden={applier.IsStructuralVisibilityHidden(tileId)}");
                }
            }

            report.AppendLine(
                $"{LogTag} {phase} channels sample={sampleCount} " +
                $"prox={proxEngaged} bfs={bfsEngaged} resolvedOcc={resolvedOcc} " +
                $"display={displayCache} viewOcc={viewOcc} stuckAll={stuckOcc}");
        }

        static float TryGetApplierDisplay(TileViewPresentationApplier applier, Guid tileId)
        {
            FieldInfo field = typeof(TileViewPresentationApplier).GetField(
                "_characterOcclusionDisplay",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field?.GetValue(applier) is not Dictionary<Guid, float> dict)
                return 0f;

            return dict.TryGetValue(tileId, out float v) ? v : 0f;
        }

        static float TryGetViewOcclusion(TileView view)
        {
            FieldInfo field = typeof(TileView).GetField(
                "_characterOcclusion",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null ? (float)field.GetValue(view) : 0f;
        }

        static void ClassifyHypotheses(
            StringBuilder report,
            TileViewPresentationApplier applier,
            PlayerFloorVisibilityPolicy policy,
            TileMapModel model,
            in FloorVisibilityContext outdoorCtx,
            List<Guid> upperFloorTileIds,
            DiagRegistry registry)
        {
            int h3 = 0;
            int h4 = 0;

            for (int i = 0; i < upperFloorTileIds.Count; i++)
            {
                Guid tileId = upperFloorTileIds[i];
                if (!registry.TryGetView(tileId, out TileView view))
                    continue;
                if (!model.TryGetTileById(tileId, out TileData tile))
                    continue;

                bool structuralHidden = applier.IsStructuralVisibilityHidden(tileId);
                bool visible = policy.IsTileVisible(tile, in outdoorCtx);
                bool active = view.gameObject.activeSelf;

                if (visible && !structuralHidden && !active)
                    h3++;
                if (visible && structuralHidden)
                    h4++;
            }

            report.AppendLine($"{LogTag} === CONCLUSION (outdoor, no blocking) ===");
            report.AppendLine($"{LogTag} 확실: H3 desync count={h3} (visible && !structuralHidden && !activeSelf)");
            report.AppendLine($"{LogTag} 확실: H4 mismatch count={h4} (visible && structuralHidden)");
            if (h3 > 0)
                report.AppendLine($"{LogTag} → H3 CONFIRMED: ApplyStructuralHidden desync");
            else if (h4 > 0)
                report.AppendLine($"{LogTag} → H4 CONFIRMED: sync/_structuralHidden mismatch");
            else
                report.AppendLine($"{LogTag} → No H3/H4 in simulation; check H1(blocking) or H2(IsOutdoorEvaluation) in Play mode");
        }
    }
}
#endif
