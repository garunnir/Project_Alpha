// ============================================================
// [BakeIdPlaygroundHost] — 씬 직렬화 Sim 배치·probe·rule + Authoring 표시 SSOT
// ============================================================

using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace IsoTilemap
{
    /// <summary>
    /// BakeId 시뮬레이션 씬 SSOT. 타일·probe·rule은 SerializeField로 씬에 저장된다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class BakeIdPlaygroundHost : MonoBehaviour
    {
        public const string ScenePath = "Assets/Dist/Scenes/BakeIdPlayground.unity";

        const string VisualRootName = "BakeIdVisualRoot";
        const string AuthoringRootName = "BakeIdAuthoring";
        const string LabelsRootName = "BakeIdLabels";
        const float IdLabelYOffsetCells = 0.55f;
#if UNITY_EDITOR
        const string DefaultPrefabDbPath = "Assets/Dist/SOData/Tile/Tile Prefab DB.asset";
        const float EditCellGizmoSize = 0.1f;
        const float EditCellGizmoSizeActive = 0.12f;
#endif


        const string Tab = "BakeId";

        [PropertyOrder(-100)]
        [ShowInInspector, ReadOnly, DisplayAsString]
        [LabelText("Status")]
        string Status => _hub == null
            ? $"empty — tiles={simTiles.Count} probes={simProbes.Count} rules={simRules.Count} steps={simSteps.Count}"
            : $"live model={_model.TilesSnapshot.Count}  simTiles={simTiles.Count}  probes={simProbes.Count}  steps={simSteps.Count}";

        // ── Setup ─────────────────────────────────────────────

        [TabGroup(Tab, "Setup")]
        [Required]
        [SerializeField] TilePrefabDB prefabDb;

        [TabGroup(Tab, "Setup")]
        [MinValue(1e-4f)]
        [SerializeField] float cellSize = 1f;

        [TabGroup(Tab, "Setup")]
        [LabelText("Show ID Labels")]
        [SerializeField] bool showIdLabels = true;

        [TabGroup(Tab, "Setup")]
        [LabelText("Rebuild On Enable")]
        [SerializeField] bool rebuildOnEnable = true;

        [TabGroup(Tab, "Setup")]
        [PropertySpace(8)]
        [InfoBox("Live Sync ON: Authoring=편집 SSOT → simTiles 자동 미러. Rebuild From Sim Tiles 버튼만 Inspector simTiles 직접 반영.")]
        [ButtonGroup(Tab + "/Setup/Pipeline")]
        [Button("Rebuild From Sim Tiles", ButtonSizes.Medium)]
        public void RebuildFromSimTiles() => RebuildFromSimTilesInternal(preferAuthoringSync: false);

        void RebuildFromSimTilesInternal(bool preferAuthoringSync, bool skipAuthoringRepopulate = false)
        {
            bool syncedFromAuthoring = false;
#if UNITY_EDITOR
            if (preferAuthoringSync && authoringLiveSync && !Application.isPlaying)
                syncedFromAuthoring = TrySyncSimTilesFromAuthoringScene();
#endif
            TearDownRuntime();
            if (!EnsurePrefabDb())
            {
                Debug.LogError("[BakeIdPlayground] TilePrefabDB missing");
                return;
            }

            if (simTiles == null || simTiles.Count == 0)
            {
                Debug.LogWarning(
                    "[BakeIdPlayground] simTiles empty — Inspector: Import From Seed Layout, then save scene");
                ClearLeftoverVisualRoot();
                ClearIdLabels();
                return;
            }

            _model = new TileMapModel();
            var registry = new BuildingGroupRegistry();
            _hub = TileMapCacheHub.Create(_model, registry);
            _model.SetMapCacheHub(_hub);
            _builder = new BuildingGroupBuilder(_model, _hub);
            _model.SetBuildingGroupBuilder(_builder);

            List<TileData> tiles = BakeIdSimTileUtil.ToTileDataList(simTiles, out int skipped, "[BakeIdPlayground]");
            if (tiles.Count == 0)
            {
                Debug.LogError(
                    skipped > 0
                        ? "[BakeIdPlayground] All simTiles invalid — fix Sim list or Import Seed Layout"
                        : "[BakeIdPlayground] No valid tiles to load");
                return;
            }

            for (int i = 0; i < tiles.Count; i++)
                _model.SetTile(tiles[i]);

            registry.ReplaceTerrainFloorCells(BakeIdPlaygroundLayout.MasterPlayground.TerrainFloorCells);
            _builder.RebakeAllBuildingPartitions();
            RefreshBakeIdDisplay();

            // Display SSOT = Authoring. Repopulate when rebuild source was simTiles, not live Authoring.
            if (!skipAuthoringRepopulate && !syncedFromAuthoring)
                PopulateAuthoringFromSimTilesInternal(logSummary: false);
        }

        [TabGroup(Tab, "Setup")]
        [ButtonGroup(Tab + "/Setup/Pipeline")]
        [Button("Import Seed Layout", ButtonSizes.Medium)]
        [GUIColor(1f, 0.85f, 0.4f)]
        public void ImportFromUnitLayout()
        {
            BakeIdSimDefaults.FillFromUnitMasterPlayground(simTiles, simProbes, simRules);
            simSteps.Clear();
            MarkSimDirty();
            RebuildFromSimTilesInternal(preferAuthoringSync: false);
        }

        [TabGroup(Tab, "Setup")]
        [BoxGroup(Tab + "/Setup/Scene Authoring")]
        [LabelText("Live Sync (Add/Delete on edit)")]
        [SerializeField] bool authoringLiveSync = true;

        [TabGroup(Tab, "Setup")]
        [BoxGroup(Tab + "/Setup/Scene Authoring")]
        [InfoBox(
            "BakeIdAuthoring 아래(또는 씬에 드롭한) Floor/SlimWall/ThickWall TileView 배치·복사·이동·삭제 → Edit Add/Remove와 동일 반영. " +
            "복사 직후 같은 칸이면 이동 후 Add.")]
        [ButtonGroup(Tab + "/Setup/Scene Authoring/Row1")]
        [Button("Sim → Authoring", ButtonSizes.Medium)]
        public void PopulateAuthoringFromSimTiles() =>
            PopulateAuthoringFromSimTilesInternal(logSummary: true);

        [TabGroup(Tab, "Setup")]
        [ButtonGroup(Tab + "/Setup/Scene Authoring/Row1")]
        [Button("Replace Sim From Authoring", ButtonSizes.Medium)]
        [GUIColor(1f, 0.85f, 0.55f)]
        [Tooltip("Live Sync drift 복구용 — Authoring 전체로 simTiles를 덮어씁니다.")]
        public void ReplaceSimFromAuthoringScene()
        {
            if (!EnsurePrefabDb())
            {
                Debug.LogError("[BakeIdPlayground] TilePrefabDB missing");
                return;
            }

            EnsureAuthoringRoot();
            TileView[] views = CollectAuthoringTileViews();
            List<BakeIdSimTileEntry> gathered = BakeIdSimTileUtil.FromAuthoringViews(
                views,
                cellSize,
                out int skipped,
                "[BakeIdPlayground]");

            if (gathered.Count == 0)
            {
                Debug.LogWarning(
                    "[BakeIdPlayground] Sync: no valid tiles under BakeIdAuthoring — place TileView prefabs first");
                return;
            }

            if (simTiles == null)
                simTiles = new List<BakeIdSimTileEntry>();
            else
                simTiles.Clear();

            using (SuspendAuthoringLiveSync())
            {
                simTiles.AddRange(gathered);
                MarkSimDirty();
                RebuildAuthoringSnapshotFromScene();
            }

            RebuildFromSimTilesInternal(preferAuthoringSync: false, skipAuthoringRepopulate: true);
            Debug.Log(
                $"[BakeIdPlayground] Replace Sim From Authoring OK — simTiles={simTiles.Count} (skipped {skipped})");
        }

#if UNITY_EDITOR
        [TabGroup(Tab, "Setup")]
        [ButtonGroup(Tab + "/Setup/Scene Authoring/Row1")]
        [Button("Select Authoring Root", ButtonSizes.Medium)]
        public void SelectAuthoringRootInHierarchy()
        {
            EnsureAuthoringRoot();
            Selection.activeGameObject = _authoringRoot.gameObject;
        }
#endif

        void PopulateAuthoringFromSimTilesInternal(bool logSummary)
        {
            if (!EnsurePrefabDb())
            {
                Debug.LogError("[BakeIdPlayground] TilePrefabDB missing");
                return;
            }

            if (simTiles == null || simTiles.Count == 0)
            {
                Debug.LogWarning("[BakeIdPlayground] simTiles empty — Import Seed Layout first");
                return;
            }

            List<TileData> tiles = BakeIdSimTileUtil.ToTileDataList(simTiles, out int skipped, "[BakeIdPlayground]");
            if (tiles.Count == 0)
            {
                Debug.LogError("[BakeIdPlayground] No valid simTiles to populate authoring");
                return;
            }

            using (SuspendAuthoringLiveSync())
            {
                EnsureAuthoringRoot();
                ClearAuthoringTileViews();

                float cs = Mathf.Max(1e-4f, cellSize);
                int spawned = 0;
                int missed = 0;
                for (int i = 0; i < tiles.Count; i++)
                {
                    if (TrySpawnAuthoringView(tiles[i], cs))
                        spawned++;
                    else
                        missed++;
                }

                RebuildAuthoringSnapshotFromScene();
                ApplyProbeLabelsFromSimProbesToAuthoring();
                MarkSimDirty();
                if (logSummary)
                {
                    Debug.Log(
                        $"[BakeIdPlayground] Sim → Authoring: spawned {spawned} views under {AuthoringRootName} " +
                        $"(simTiles skipped {skipped}, spawn missed {missed})");
                }
            }
        }

        [TabGroup(Tab, "Setup")]
        [ButtonGroup(Tab + "/Setup/Verify/Bake")]
        [Button("Full Rebake", ButtonSizes.Medium)]
        public void FullRebake()
        {
            EnsureRuntime();
            _builder.RebakeAllBuildingPartitions();
            RefreshBakeIdDisplay();
        }

        [TabGroup(Tab, "Setup")]
        [ButtonGroup(Tab + "/Setup/Verify/Bake")]
        [Button("Run Sim Rules", ButtonSizes.Medium)]
        [GUIColor(0.55f, 0.95f, 0.65f)]
        public void RunSimRulesPreview()
        {
            BakeIdSimRunner.Result result = BakeIdSimRunner.Run(simTiles, simProbes, simRules, simSteps);
            if (result.Ok)
            {
                Debug.Log($"[BakeIdPlayground] Rules OK ({simRules.Count} rules, {simTiles.Count} tiles)");
                return;
            }

            for (int i = 0; i < result.Failures.Count; i++)
                Debug.LogError($"[BakeIdPlayground] Rule fail: {result.Failures[i]}");
        }

        [TabGroup(Tab, "Setup")]
        [PropertySpace(4)]
        [ButtonGroup(Tab + "/Setup/Verify/Steps")]
        [Button("Preview Sim Steps", ButtonSizes.Medium)]
        [GUIColor(0.65f, 0.85f, 1f)]
        public void PreviewSimSteps()
        {
            RebuildFromSimTilesInternal(preferAuthoringSync: authoringLiveSync);
            if (_model == null)
                return;

            if (simSteps == null || simSteps.Count == 0)
            {
                Debug.Log("[BakeIdPlayground] Preview: no simSteps — showing initial layout only");
                return;
            }

            if (!BakeIdSimRunner.TryApplySteps(_model, simProbes, simSteps, out string error))
            {
                Debug.LogError($"[BakeIdPlayground] Preview step failed: {error}");
                return;
            }

            RefreshBakeIdDisplay();
            Debug.Log($"[BakeIdPlayground] Preview OK — applied {simSteps.Count} step(s)");
        }

        [TabGroup(Tab, "Setup")]
        [ButtonGroup(Tab + "/Setup/Verify/Steps")]
        [Button("Preview + Run Rules", ButtonSizes.Medium)]
        public void PreviewSimStepsAndRunRules()
        {
            PreviewSimSteps();
            RunSimRulesPreview();
        }

        // ── Edit ──────────────────────────────────────────────

        [TabGroup(Tab, "Edit")]
        [BoxGroup(Tab + "/Edit/Cells", ShowLabel = false)]
        [HorizontalGroup(Tab + "/Edit/Cells/AB")]
        [LabelText("A"), LabelWidth(14)]
        [SerializeField] Vector3Int editCellA = new(0, 1, 10);

        [TabGroup(Tab, "Edit")]
        [HorizontalGroup(Tab + "/Edit/Cells/AB")]
        [LabelText("B"), LabelWidth(14)]
        [SerializeField] Vector3Int editCellB = new(1, 1, 10);

        [TabGroup(Tab, "Edit")]
        [BoxGroup(Tab + "/Edit/Scene Move")]
        [LabelText("Enable (QWEASD)")]
        [SerializeField] bool sceneMoveEditCell;

        [TabGroup(Tab, "Edit")]
        [BoxGroup(Tab + "/Edit/Scene Move")]
        [EnumToggleButtons]
        [LabelText("Target")]
        [EnableIf(nameof(sceneMoveEditCell))]
        [SerializeField] EditCellTarget moveTarget = EditCellTarget.A;

        [TabGroup(Tab, "Edit")]
        [BoxGroup(Tab + "/Edit/Scene Move")]
        [ShowInInspector, ReadOnly, DisplayAsString]
        [HideLabel]
        string MoveHint => sceneMoveEditCell
            ? $"ON → {moveTarget}  |  W/S±Z  A/D±X  Q/E±Y  |  Tab A↔B"
            : "OFF — Scene 뷰에서 키 이동 안 함";

        [TabGroup(Tab, "Edit")]
        [BoxGroup(Tab + "/Edit/Place")]
        [ButtonGroup(Tab + "/Edit/Place/Tiles")]
        [Button("Floor @ A")]
        public void AddFloorAtA()
        {
            EnsureRuntime();
            var entry = BakeIdSimTileEntry.Floor(editCellA);
            simTiles.Add(entry);
            TileData tile = entry.ToTileData();
            _model.SetTile(tile);
            MarkSimDirty();
            SyncAuthoringViewForEditAdd(tile);
            RefreshBakeIdDisplay();
        }

        [TabGroup(Tab, "Edit")]
        [BoxGroup(Tab + "/Edit/Place")]
        [ButtonGroup(Tab + "/Edit/Place/Tiles")]
        [Button("Cube @ A")]
        public void AddCubeAtA()
        {
            EnsureRuntime();
            var entry = BakeIdSimTileEntry.Cube(editCellA);
            simTiles.Add(entry);
            TileData tile = entry.ToTileData();
            _model.SetTile(tile);
            MarkSimDirty();
            SyncAuthoringViewForEditAdd(tile);
            RefreshBakeIdDisplay();
        }

        [TabGroup(Tab, "Edit")]
        [BoxGroup(Tab + "/Edit/Place")]
        [ButtonGroup(Tab + "/Edit/Place/Tiles")]
        [Button("ThinWall A↔B")]
        public void AddThinWallAB()
        {
            EnsureRuntime();
            var entry = BakeIdSimTileEntry.ThinWall(editCellA, editCellB);
            if (!entry.TryToTileData(out TileData tile, out string error))
            {
                Debug.LogWarning($"[BakeIdPlayground] ThinWall rejected: {error}");
                return;
            }

            simTiles.Add(entry);
            _model.SetTile(tile);
            MarkSimDirty();
            SyncAuthoringViewForEditAdd(tile);
            RefreshBakeIdDisplay();
        }

        [TabGroup(Tab, "Edit")]
        [BoxGroup(Tab + "/Edit/Place")]
        [ButtonGroup(Tab + "/Edit/Place/Tiles")]
        [Button("Remove @ A")]
        [GUIColor(1f, 0.45f, 0.45f)]
        public void RemoveAtA()
        {
            EnsureRuntime();
            if (!TryFindRemovableAtA(out TileData tile, out int simIndex))
            {
                Debug.LogWarning($"[BakeIdPlayground] No tile at editCellA={editCellA}");
                return;
            }

            _model.RemoveTile(tile);
            if (simIndex >= 0 && simIndex < simTiles.Count)
                simTiles.RemoveAt(simIndex);
            else
                RemoveMatchingSimEntry(tile);

            MarkSimDirty();
            SyncAuthoringViewForEditRemove(tile);
            RefreshBakeIdDisplay();
        }

        [TabGroup(Tab, "Edit")]
        [BoxGroup(Tab + "/Edit/Place")]
        [ButtonGroup(Tab + "/Edit/Place/WithProbe")]
        [Button("Floor @ A + Probe")]
        public void AddFloorAtAWithProbe()
        {
            AddFloorAtA();
            TryAddProbeAt(editCellA, preferredName: null);
        }

        [TabGroup(Tab, "Edit")]
        [BoxGroup(Tab + "/Edit/Place")]
        [ButtonGroup(Tab + "/Edit/Place/WithProbe")]
        [Button("Cube @ A + Probe")]
        public void AddCubeAtAWithProbe()
        {
            AddCubeAtA();
            TryAddProbeAt(editCellA, preferredName: null);
        }

        [TabGroup(Tab, "Edit")]
        [BoxGroup(Tab + "/Edit/Probe")]
        [LabelText("Name (optional)")]
        [Tooltip("비우면 P_{x}_{y}_{z}")]
        [SerializeField] string probeNameToAdd;

        [TabGroup(Tab, "Edit")]
        [BoxGroup(Tab + "/Edit/Probe")]
        [Button("Add Probe @ A", ButtonSizes.Medium)]
        public void AddProbeAtA()
        {
            TryAddProbeAt(editCellA, preferredName: null);
        }

        [TabGroup(Tab, "Edit")]
        [BoxGroup(Tab + "/Edit/Record Step")]
        [InfoBox(
            "simTiles=초기 레이아웃 SSOT. Record Step은 simSteps에 append — Run Sim Rules / Preview / 씬 테스트가 steps 후 상태를 검증.")]
        [ButtonGroup(Tab + "/Edit/Record Step/Row1")]
        [Button("AddFloor", ButtonSizes.Small)]
        public void RecordAddFloorStepAtA() =>
            RecordStep(BakeIdSimStepKind.AddFloorAtProbe, editCellA);

        [TabGroup(Tab, "Edit")]
        [BoxGroup(Tab + "/Edit/Record Step")]
        [ButtonGroup(Tab + "/Edit/Record Step/Row1")]
        [Button("RemoveFloor", ButtonSizes.Small)]
        public void RecordRemoveFloorStepAtA() =>
            RecordStep(BakeIdSimStepKind.RemoveFloorAtProbe, editCellA);

        [TabGroup(Tab, "Edit")]
        [BoxGroup(Tab + "/Edit/Record Step")]
        [ButtonGroup(Tab + "/Edit/Record Step/Row1")]
        [Button("AddCube", ButtonSizes.Small)]
        public void RecordAddCubeStepAtA() =>
            RecordStep(BakeIdSimStepKind.AddCubeAtProbe, editCellA);

        [TabGroup(Tab, "Edit")]
        [BoxGroup(Tab + "/Edit/Record Step")]
        [ButtonGroup(Tab + "/Edit/Record Step/Row1")]
        [Button("RemoveCube", ButtonSizes.Small)]
        public void RecordRemoveCubeStepAtA() =>
            RecordStep(BakeIdSimStepKind.RemoveCubeAtProbe, editCellA);

        [TabGroup(Tab, "Edit")]
        [BoxGroup(Tab + "/Edit/Record Step")]
        [ButtonGroup(Tab + "/Edit/Record Step/Row2")]
        [Button("RemoveThinWall A↔B", ButtonSizes.Small)]
        public void RecordRemoveThinWallStepAtAB()
        {
            string probeA = ResolveOrCreateProbeNameAt(editCellA);
            string probeB = ResolveOrCreateProbeNameAt(editCellB);
            AppendSimStep(BakeIdSimStep.RemoveThinWall(probeA, probeB));
        }

        [TabGroup(Tab, "Edit")]
        [BoxGroup(Tab + "/Edit/Record Step")]
        [ButtonGroup(Tab + "/Edit/Record Step/Row2")]
        [Button("Clear Steps", ButtonSizes.Small)]
        [GUIColor(1f, 0.55f, 0.55f)]
        public void ClearSimSteps()
        {
            if (simSteps == null || simSteps.Count == 0)
                return;

            simSteps.Clear();
            MarkSimDirty();
            Debug.Log("[BakeIdPlayground] simSteps cleared");
        }

        // ── Sim SSOT ──────────────────────────────────────────

        [TabGroup(Tab, "Sim")]
        [InfoBox("씬 SerializeField = SSOT. Import Seed는 시드 덮어쓰기.")]
        [ListDrawerSettings(
            ShowPaging = true,
            NumberOfItemsPerPage = 20,
            DraggableItems = true,
            ShowIndexLabels = true,
            ListElementLabelName = "kind")]
        [LabelText("Tiles")]
        [SerializeField] List<BakeIdSimTileEntry> simTiles = new();

        [TabGroup(Tab, "Sim")]
        [ListDrawerSettings(
            ShowPaging = true,
            NumberOfItemsPerPage = 20,
            DraggableItems = true,
            ShowIndexLabels = true,
            ListElementLabelName = "name")]
        [LabelText("Probes")]
        [SerializeField] List<BakeIdSimProbe> simProbes = new();

        [TabGroup(Tab, "Sim")]
        [ListDrawerSettings(
            ShowPaging = true,
            NumberOfItemsPerPage = 20,
            DraggableItems = true,
            ShowIndexLabels = true,
            ListElementLabelName = "kind")]
        [LabelText("Rules")]
        [SerializeField] List<BakeIdSimRule> simRules = new();

        [TabGroup(Tab, "Sim")]
        [ListDrawerSettings(
            ShowPaging = true,
            NumberOfItemsPerPage = 12,
            DraggableItems = true,
            ShowIndexLabels = true,
            ListElementLabelName = "kind")]
        [LabelText("Steps (incremental)")]
        [SerializeField] List<BakeIdSimStep> simSteps = new();

        public enum EditCellTarget
        {
            A = 0,
            B = 1,
        }

        public IReadOnlyList<BakeIdSimTileEntry> SimTiles => simTiles;
        public IReadOnlyList<BakeIdSimProbe> SimProbes => simProbes;
        public IReadOnlyList<BakeIdSimRule> SimRules => simRules;
        public IReadOnlyList<BakeIdSimStep> SimSteps => simSteps;
        public float CellSize => cellSize;

        TileMapModel _model;
        TileMapCacheHub _hub;
        BuildingGroupBuilder _builder;
        Transform _authoringRoot;
        Transform _labelsRoot;

#if UNITY_EDITOR
        bool _sceneGuiSubscribed;
        bool _authoringSyncSubscribed;
        bool _authoringSyncPending;
        double _authoringSyncDueTime;
        int _authoringSyncSuspendDepth;
        const double AuthoringSyncDebounceSec = 0.05;
        readonly Dictionary<int, BakeIdSimTileEntry> _authoringSnapshot = new();
        bool _authoringEverUsed;
#endif

        void OnEnable()
        {
            EnsurePrefabDb();
            EnsureAuthoringRoot();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                RebuildAuthoringSnapshotFromScene();
                SyncAuthoringLiveSubscription();
            }

            SyncSceneMoveSubscription();
#endif
            if (rebuildOnEnable && _hub == null)
                RebuildFromSimTilesInternal(preferAuthoringSync: authoringLiveSync);
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnsubscribeAuthoringLive();
            UnsubscribeSceneMove();
            if (!Application.isPlaying)
                return;
#endif
            TearDownRuntime();
        }

        void OnValidate()
        {
            EnsurePrefabDb();
#if UNITY_EDITOR
            SyncAuthoringLiveSubscription();
            SyncSceneMoveSubscription();
#endif
        }

        bool TryAddProbeAt(Vector3Int cell, string preferredName)
        {
            if (simProbes == null)
                simProbes = new List<BakeIdSimProbe>();

            for (int i = 0; i < simProbes.Count; i++)
            {
                if (simProbes[i].cell != cell)
                    continue;
                Debug.LogWarning(
                    $"[BakeIdPlayground] Probe already at {cell} ('{simProbes[i].name}') — skip");
                return false;
            }

            string name = !string.IsNullOrWhiteSpace(preferredName)
                ? preferredName.Trim()
                : (!string.IsNullOrWhiteSpace(probeNameToAdd)
                    ? probeNameToAdd.Trim()
                    : $"P_{cell.x}_{cell.y}_{cell.z}");

            name = EnsureUniqueProbeName(name);
            simProbes.Add(new BakeIdSimProbe(name, cell));
            probeNameToAdd = string.Empty;
            MarkSimDirty();
            Debug.Log($"[BakeIdPlayground] Probe added '{name}' @ {cell}");
            return true;
        }

        /// <summary>ViewLink Push 버튼 — Authoring label → simProbes (빈 라벨은 Clear Probe 사용).</summary>
        public void PushProbeFromAuthoringLink(BakeIdAuthoringViewLink link)
        {
            if (link == null)
            {
                Debug.LogWarning("[BakeIdPlayground] Push probe failed: link null");
                return;
            }

            if (!link.TryResolveSimEntry(out BakeIdSimTileEntry entry, out string error, prepareGather: false))
            {
                Debug.LogWarning($"[BakeIdPlayground] Push probe failed: {error}");
                return;
            }

            Vector3Int cell = entry.cell;
            string label = link.ProbeLabel != null ? link.ProbeLabel.Trim() : string.Empty;
            if (string.IsNullOrEmpty(label))
            {
                Debug.LogWarning("[BakeIdPlayground] Push probe: label empty — use Clear Probe, or type a name first");
                return;
            }

            if (simProbes == null)
                simProbes = new List<BakeIdSimProbe>();

            int byName = IndexOfProbeName(label);
            int byCell = IndexOfProbeCell(cell);

            if (byName >= 0)
            {
                if (simProbes[byName].cell != cell)
                {
                    simProbes[byName] = new BakeIdSimProbe(label, cell);
                    MarkSimDirty();
                    Debug.Log($"[BakeIdPlayground] Probe '{label}' → {cell} (Push)");
                }
                else
                {
                    Debug.Log($"[BakeIdPlayground] Probe '{label}' @ {cell} already synced");
                }

                if (byCell >= 0 && byCell != byName)
                {
                    simProbes.RemoveAt(byCell);
                    MarkSimDirty();
                }

                return;
            }

            if (byCell >= 0)
            {
                simProbes[byCell] = new BakeIdSimProbe(label, cell);
                MarkSimDirty();
                Debug.Log($"[BakeIdPlayground] Probe @ {cell} renamed → '{label}' (Push)");
                return;
            }

            simProbes.Add(new BakeIdSimProbe(label, cell));
            MarkSimDirty();
            Debug.Log($"[BakeIdPlayground] Probe '{label}' @ {cell} (Push)");
        }

        /// <summary>ViewLink Pull 버튼 — simProbes @ cell → Authoring label.</summary>
        public void PullProbeToAuthoringLink(BakeIdAuthoringViewLink link)
        {
            if (link == null)
            {
                Debug.LogWarning("[BakeIdPlayground] Pull probe failed: link null");
                return;
            }

            if (!link.TryResolveSimEntry(out BakeIdSimTileEntry entry, out string error, prepareGather: false))
            {
                Debug.LogWarning($"[BakeIdPlayground] Pull probe failed: {error}");
                return;
            }

            int byCell = IndexOfProbeCell(entry.cell);
            if (byCell < 0)
            {
                Debug.LogWarning($"[BakeIdPlayground] Pull probe: no simProbe @ {entry.cell}");
                return;
            }

            string name = simProbes[byCell].name;
            link.SetProbeLabelWithoutNotify(name);
#if UNITY_EDITOR
            EditorUtility.SetDirty(link);
#endif
            Debug.Log($"[BakeIdPlayground] Probe '{name}' @ {entry.cell} → Authoring label (Pull)");
        }

        /// <summary>ViewLink Clear 버튼 — label 비우고 해당 name/cell 프로브 제거.</summary>
        public void ClearProbeFromAuthoringLink(BakeIdAuthoringViewLink link)
        {
            if (link == null)
                return;

            RemoveProbeOwnedByAuthoringLink(link, logReason: "Clear", onlyIfBoundToThisCell: false);
            link.SetProbeLabelWithoutNotify(string.Empty);
#if UNITY_EDITOR
            EditorUtility.SetDirty(link);
#endif
        }

        /// <param name="onlyIfBoundToThisCell">
        /// true(destroy): label이 이 타일 cell의 프로브와 일치할 때만 제거 — 미Push 타이핑으로 다른 프로브 지우지 않음.
        /// </param>
        void RemoveProbeOwnedByAuthoringLink(
            BakeIdAuthoringViewLink link,
            string logReason,
            bool onlyIfBoundToThisCell)
        {
            if (link == null || simProbes == null || simProbes.Count == 0)
                return;

            string label = link.ProbeLabel != null ? link.ProbeLabel.Trim() : string.Empty;
            bool resolved = link.TryResolveSimEntry(out BakeIdSimTileEntry entry, out _, prepareGather: false);

            if (!string.IsNullOrEmpty(label))
            {
                int byName = IndexOfProbeName(label);
                if (byName < 0)
                    return;

                if (onlyIfBoundToThisCell && (!resolved || simProbes[byName].cell != entry.cell))
                    return;

                simProbes.RemoveAt(byName);
                MarkSimDirty();
                Debug.Log($"[BakeIdPlayground] Probe '{label}' removed ({logReason})");
                return;
            }

            if (onlyIfBoundToThisCell || !resolved)
                return;

            int byCell = IndexOfProbeCell(entry.cell);
            if (byCell < 0)
                return;

            string removed = simProbes[byCell].name;
            simProbes.RemoveAt(byCell);
            MarkSimDirty();
            Debug.Log($"[BakeIdPlayground] Probe '{removed}' @ {entry.cell} removed ({logReason})");
        }

        int IndexOfProbeName(string name)
        {
            if (simProbes == null || string.IsNullOrEmpty(name))
                return -1;
            for (int i = 0; i < simProbes.Count; i++)
            {
                if (simProbes[i].name == name)
                    return i;
            }

            return -1;
        }

        int IndexOfProbeCell(Vector3Int cell)
        {
            if (simProbes == null)
                return -1;
            for (int i = 0; i < simProbes.Count; i++)
            {
                if (simProbes[i].cell == cell)
                    return i;
            }

            return -1;
        }

        void ApplyProbeLabelsFromSimProbesToAuthoring()
        {
            if (simProbes == null || simProbes.Count == 0 || _authoringRoot == null)
                return;

            TileView[] views = CollectAuthoringTileViews();
            for (int i = 0; i < views.Length; i++)
            {
                TileView view = views[i];
                if (view == null)
                    continue;

                BakeIdAuthoringViewLink link = BakeIdAuthoringViewLink.Ensure(view, this, cellSize);
                if (link == null || !link.TryResolveSimEntry(out BakeIdSimTileEntry entry, out _))
                    continue;

                int probeIdx = IndexOfProbeCell(entry.cell);
                if (probeIdx < 0)
                    continue;

                string name = simProbes[probeIdx].name;
                if (string.IsNullOrEmpty(name))
                    continue;

                link.SetProbeLabelWithoutNotify(name);
#if UNITY_EDITOR
                EditorUtility.SetDirty(link);
#endif
            }
        }

        void RecordStep(BakeIdSimStepKind kind, Vector3Int cell)
        {
            string probe = ResolveOrCreateProbeNameAt(cell);
            BakeIdSimStep step = kind switch
            {
                BakeIdSimStepKind.AddFloorAtProbe => BakeIdSimStep.AddFloor(probe),
                BakeIdSimStepKind.RemoveFloorAtProbe => BakeIdSimStep.RemoveFloor(probe),
                BakeIdSimStepKind.AddCubeAtProbe => BakeIdSimStep.AddCube(probe),
                BakeIdSimStepKind.RemoveCubeAtProbe => BakeIdSimStep.RemoveCube(probe),
                _ => throw new System.InvalidOperationException($"RecordStep unsupported kind {kind}"),
            };
            AppendSimStep(step);
        }

        string ResolveOrCreateProbeNameAt(Vector3Int cell)
        {
            if (simProbes != null)
            {
                for (int i = 0; i < simProbes.Count; i++)
                {
                    BakeIdSimProbe p = simProbes[i];
                    if (p.cell != cell || string.IsNullOrEmpty(p.name))
                        continue;
                    return p.name;
                }
            }

            TryAddProbeAt(cell, preferredName: null);
            return simProbes[simProbes.Count - 1].name;
        }

        void AppendSimStep(BakeIdSimStep step)
        {
            if (simSteps == null)
                simSteps = new List<BakeIdSimStep>();

            simSteps.Add(step);
            MarkSimDirty();
            Debug.Log(
                $"[BakeIdPlayground] Step[{simSteps.Count - 1}] {step.kind} probeA='{step.probeA}' probeB='{step.probeB}'");
        }

        string EnsureUniqueProbeName(string name)
        {
            if (!ProbeNameExists(name))
                return name;

            for (int i = 2; i < 1000; i++)
            {
                string candidate = $"{name}_{i}";
                if (!ProbeNameExists(candidate))
                    return candidate;
            }

            return $"{name}_{System.Guid.NewGuid():N}";
        }

        bool ProbeNameExists(string name)
        {
            for (int i = 0; i < simProbes.Count; i++)
            {
                if (simProbes[i].name == name)
                    return true;
            }

            return false;
        }

        void EnsureRuntime()
        {
            if (_hub != null && _builder != null && _model != null)
                return;
            RebuildFromSimTilesInternal(preferAuthoringSync: authoringLiveSync);
        }

        void TearDownRuntime()
        {
            ClearLeftoverVisualRoot();
            ClearIdLabels();
            _builder = null;
            _hub = null;
            _model = null;
        }

        bool EnsurePrefabDb()
        {
            if (prefabDb != null)
            {
                TilePrefabDB.RegisterRuntime(prefabDb);
                return true;
            }

#if UNITY_EDITOR
            prefabDb = AssetDatabase.LoadAssetAtPath<TilePrefabDB>(DefaultPrefabDbPath);
            if (prefabDb != null)
            {
                TilePrefabDB.RegisterRuntime(prefabDb);
                return true;
            }
#endif
            return false;
        }

        void MarkSimDirty()
        {
#if UNITY_EDITOR
            Undo.RecordObject(this, "BakeId Sim");
            EditorUtility.SetDirty(this);
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

#if UNITY_EDITOR
        bool TrySyncSimTilesFromAuthoringScene()
        {
            if (!authoringLiveSync || Application.isPlaying)
                return false;

            EnsureAuthoringRoot();
            TileView[] views = CollectAuthoringTileViews();
            if (views.Length == 0)
            {
                if (!_authoringEverUsed)
                    return false;

                using (SuspendAuthoringLiveSync())
                {
                    simTiles?.Clear();
                    _authoringSnapshot.Clear();
                }

                MarkSimDirty();
                return true;
            }

            List<BakeIdSimTileEntry> gathered = BakeIdSimTileUtil.FromAuthoringViews(
                views,
                cellSize,
                out _,
                "[BakeIdPlayground]");
            using (SuspendAuthoringLiveSync())
            {
                if (simTiles == null)
                    simTiles = new List<BakeIdSimTileEntry>();
                else
                    simTiles.Clear();
                simTiles.AddRange(gathered);
                RebuildAuthoringSnapshotFromScene();
            }

            _authoringEverUsed = true;
            MarkSimDirty();
            return true;
        }
#endif

        void EnsureAuthoringRoot()
        {
            if (_authoringRoot != null)
                return;

            Transform existing = transform.Find(AuthoringRootName);
            if (existing != null)
            {
                _authoringRoot = existing;
                return;
            }

            var go = new GameObject(AuthoringRootName);
            go.transform.SetParent(transform, false);
            _authoringRoot = go.transform;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorUtility.SetDirty(gameObject);
#endif
        }

        TileView[] CollectAuthoringTileViews()
        {
            EnsureAuthoringRoot();
            TileView[] all = _authoringRoot.GetComponentsInChildren<TileView>(includeInactive: true);
            if (all == null || all.Length == 0)
                return System.Array.Empty<TileView>();

            var filtered = new List<TileView>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                TileView view = all[i];
                if (view == null)
                    continue;
                if (view.transform == _authoringRoot)
                    continue;
                filtered.Add(view);
            }

            return filtered.ToArray();
        }

        void ClearAuthoringTileViews()
        {
            EnsureAuthoringRoot();
            TileView[] views = CollectAuthoringTileViews();
            for (int i = views.Length - 1; i >= 0; i--)
                DestroyImmediateSafe(views[i].gameObject);
        }

        bool TrySpawnAuthoringView(in TileData tile, float cs)
        {
            string prefabId = tile.identity.PrefabId;
            GameObject prefab = prefabDb.GetPrefab(prefabId);
            if (prefab == null)
            {
                Debug.LogWarning(
                    $"[BakeIdPlayground] Authoring spawn missing prefab '{prefabId}' @ {tile.identity.GridPos}");
                return false;
            }

            GameObject go = TilePrefabSpawnUtil.Instantiate(
                prefab,
                _authoringRoot,
                Vector3.zero,
                Quaternion.identity);
            if (go == null)
                return false;

            if (!go.TryGetComponent(out TileView view))
            {
                DestroyImmediateSafe(go);
                Debug.LogWarning($"[BakeIdPlayground] Authoring prefab has no TileView: '{prefabId}'");
                return false;
            }

            view.UpdateTile(tile, cs);
#if UNITY_EDITOR
            _authoringEverUsed = true;
#endif
            BakeIdAuthoringViewLink.Ensure(view, this, cs);
            return true;
        }

        void ClearLeftoverVisualRoot()
        {
            Transform existing = transform.Find(VisualRootName);
            if (existing != null)
                DestroyImmediateSafe(existing.gameObject);
        }

        void EnsureLabelsRoot()
        {
            if (_labelsRoot != null)
                return;

            EnsureAuthoringRoot();
            Transform existing = _authoringRoot.Find(LabelsRootName);
            if (existing != null)
            {
                _labelsRoot = existing;
                return;
            }

            var go = new GameObject(LabelsRootName);
            go.transform.SetParent(_authoringRoot, false);
            _labelsRoot = go.transform;
        }

        void ClearIdLabels()
        {
            if (_labelsRoot == null)
            {
                EnsureAuthoringRoot();
                if (_authoringRoot != null)
                {
                    Transform existing = _authoringRoot.Find(LabelsRootName);
                    if (existing != null)
                        DestroyImmediateSafe(existing.gameObject);
                }

                return;
            }

            DestroyImmediateSafe(_labelsRoot.gameObject);
            _labelsRoot = null;
        }

        /// <summary>
        /// Display SSOT = Authoring tiles. Refresh only bake ID labels (no VisualRoot tile meshes).
        /// </summary>
        void RefreshBakeIdDisplay()
        {
            ClearLeftoverVisualRoot();
            ClearIdLabels();
            if (_model == null || _hub == null || !EnsurePrefabDb())
                return;

            if (!showIdLabels)
                return;

            float cs = Mathf.Max(1e-4f, cellSize);
            SpawnFloorIdLabels(cs);
        }

        void SpawnFloorIdLabels(float cs)
        {
            if (_hub == null)
                return;

            EnsureLabelsRoot();

            foreach (var (x, cellY, z) in _hub.Topology.Index.EnumerateWalkableFloorCells())
            {
                var cell = new Vector3Int(x, cellY, z);
                if (!_hub.TryGetFloorFaceForWalkableCell(x, cellY, z, out TileData face))
                    continue;

                int buildingId = face.identity.buildingId;
                int roomId = face.identity.roomId;
                int spaceId = 0;
                bool isOutdoor = false;
                if (_hub.Spaces.TryGetSpaceAtFloorCell(cell, out int sid))
                {
                    spaceId = sid;
                    isOutdoor = _hub.Spaces.IsOutdoorSpace(sid);
                }
                else if (buildingId == TileIdentity.BuildingIdTerrain)
                {
                    isOutdoor = true;
                }
                else
                {
                    isOutdoor = _hub.IsOutdoorEvaluation(cellY, x, z);
                }

                Vector3 world = TileWorldPointUtil.GetRepresentativeWorldPoint(face.identity, cs);
                world.y += IdLabelYOffsetCells * cs;

                var labelGo = new GameObject($"id_{cell}");
                labelGo.transform.SetParent(_labelsRoot, worldPositionStays: true);
                labelGo.transform.position = world;

                var tm = labelGo.AddComponent<TextMesh>();
                tm.text = $"B:{buildingId} R:{roomId} S:{spaceId} O:{(isOutdoor ? 1 : 0)}";
                tm.fontSize = 32;
                tm.characterSize = 0.05f * cs;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = isOutdoor
                    ? new Color(0.4f, 0.85f, 1f)
                    : Color.white;
            }
        }

        void SyncAuthoringViewForEditAdd(in TileData tile)
        {
#if UNITY_EDITOR
            using (SuspendAuthoringLiveSync())
            {
#endif
                float cs = Mathf.Max(1e-4f, cellSize);
                TrySpawnAuthoringView(tile, cs);
#if UNITY_EDITOR
                RebuildAuthoringSnapshotFromScene();
            }
#endif
        }

        void SyncAuthoringViewForEditRemove(in TileData tile)
        {
            if (!BakeIdSimTileEntry.TryFromTileData(tile, out BakeIdSimTileEntry entry))
                return;

#if UNITY_EDITOR
            using (SuspendAuthoringLiveSync())
            {
#endif
                TileView[] views = CollectAuthoringTileViews();
                for (int i = views.Length - 1; i >= 0; i--)
                {
                    TileView view = views[i];
                    if (view == null)
                        continue;
                    if (!TryResolveAuthoringEntry(view, out BakeIdSimTileEntry got, out _))
                        continue;
                    if (!BakeIdSimTileEntry.EntriesEqual(got, entry))
                        continue;
                    DestroyImmediateSafe(view.gameObject);
                    break;
                }

#if UNITY_EDITOR
                RebuildAuthoringSnapshotFromScene();
            }
#endif
        }

        bool TryFindRemovableAtA(out TileData tile, out int simIndex)
        {
            tile = default;
            simIndex = -1;
            TileData floorMatch = default;
            bool hasFloor = false;
            TileData anyMatch = default;
            bool hasAny = false;

            foreach (TileData candidate in _model.TilesSnapshot)
            {
                if (TileIdentityUtil.IsHorizontalFace(candidate.identity) &&
                    candidate.identity.GridPos == editCellA)
                {
                    floorMatch = candidate;
                    hasFloor = true;
                    break;
                }

                if (candidate.identity.GridPos == editCellA)
                {
                    anyMatch = candidate;
                    hasAny = true;
                }
            }

            if (hasFloor)
            {
                tile = floorMatch;
                simIndex = FindSimIndex(tile);
                return true;
            }

            if (hasAny)
            {
                tile = anyMatch;
                simIndex = FindSimIndex(tile);
                return true;
            }

            foreach (TileData candidate in _model.TilesSnapshot)
            {
                if (!BakeIdSyntheticTiles.IsThinWallBetween(candidate, editCellA, editCellB))
                    continue;
                tile = candidate;
                simIndex = FindSimIndex(tile);
                return true;
            }

            return false;
        }

        int FindSimIndex(in TileData tile)
        {
            if (!BakeIdSimTileEntry.TryFromTileData(tile, out BakeIdSimTileEntry want))
                return -1;

            for (int i = 0; i < simTiles.Count; i++)
            {
                BakeIdSimTileEntry e = simTiles[i];
                if (e.kind != want.kind)
                    continue;
                if (e.cell != want.cell)
                    continue;
                if (e.kind == BakeIdSimTileKind.ThinWall && e.cellB != want.cellB)
                    continue;
                return i;
            }

            return -1;
        }

        void RemoveMatchingSimEntry(in TileData tile)
        {
            int idx = FindSimIndex(tile);
            if (idx >= 0)
                simTiles.RemoveAt(idx);
        }

        bool SimContainsEntry(in BakeIdSimTileEntry entry)
        {
            if (simTiles == null)
                return false;

            for (int i = 0; i < simTiles.Count; i++)
            {
                if (BakeIdSimTileEntry.EntriesEqual(simTiles[i], entry))
                    return true;
            }

            return false;
        }

        int FindSimIndex(in BakeIdSimTileEntry entry)
        {
            if (simTiles == null)
                return -1;

            for (int i = 0; i < simTiles.Count; i++)
            {
                if (BakeIdSimTileEntry.EntriesEqual(simTiles[i], entry))
                    return i;
            }

            return -1;
        }

        bool TryFindModelTile(in BakeIdSimTileEntry entry, out TileData tile)
        {
            tile = default;
            EnsureRuntime();
            if (_model == null)
                return false;

            foreach (TileData candidate in _model.TilesSnapshot)
            {
                if (!BakeIdSimTileEntry.TryFromTileData(candidate, out BakeIdSimTileEntry got))
                    continue;
                if (!BakeIdSimTileEntry.EntriesEqual(got, entry))
                    continue;
                tile = candidate;
                return true;
            }

            return false;
        }

        bool ApplyAuthoringAdd(in BakeIdSimTileEntry entry, bool commit = true)
        {
            if (!entry.TryToTileData(out TileData tile, out string error))
            {
                Debug.LogWarning($"[BakeIdPlayground] Authoring add rejected: {error}");
                return false;
            }

            if (SimContainsEntry(entry))
            {
                Debug.LogWarning(
                    $"[BakeIdPlayground] Authoring add skipped — duplicate {entry.kind} @ {entry.cell}");
                return false;
            }

            EnsureRuntime();
            if (simTiles == null)
                simTiles = new List<BakeIdSimTileEntry>();
            simTiles.Add(entry);
            _model.SetTile(tile);
            MarkSimDirty();
            if (commit)
                CommitAuthoringModelChange();
            Debug.Log($"[BakeIdPlayground] Authoring + {entry.kind} @ {FormatEntryCell(entry)}");
            return true;
        }

        bool ApplyAuthoringRemove(in BakeIdSimTileEntry entry, bool commit = true)
        {
            int simIndex = FindSimIndex(entry);
            if (simIndex < 0 && _model == null)
                return false;

            EnsureRuntime();
            if (TryFindModelTile(entry, out TileData modelTile))
                _model.RemoveTile(modelTile);

            if (simIndex >= 0)
                simTiles.RemoveAt(simIndex);

            MarkSimDirty();
            if (commit)
                CommitAuthoringModelChange();
            Debug.Log($"[BakeIdPlayground] Authoring - {entry.kind} @ {FormatEntryCell(entry)}");
            return true;
        }

        void CommitAuthoringModelChange()
        {
            EnsureRuntime();
            if (_builder != null)
                _builder.RebakeAllBuildingPartitions();
            RefreshBakeIdDisplay();
        }

        public void NotifyAuthoringViewDestroyed(TileView view)
        {
#if UNITY_EDITOR
            if (!authoringLiveSync || view == null || Application.isPlaying || _authoringSyncSuspendDepth > 0)
                return;

            if (view.TryGetComponent(out BakeIdAuthoringViewLink dyingLink))
                RemoveProbeOwnedByAuthoringLink(dyingLink, logReason: "Authoring destroy", onlyIfBoundToThisCell: true);

            int id = view.GetInstanceID();
            if (!_authoringSnapshot.TryGetValue(id, out BakeIdSimTileEntry entry) &&
                !TryResolveAuthoringEntry(view, out entry, out _))
            {
                return;
            }

            using (SuspendAuthoringLiveSync())
            {
                _authoringSnapshot.Remove(id);
                if (CountLiveAuthoringViewsAt(entry, exceptViewId: id) == 0)
                    ApplyAuthoringRemove(entry, commit: false);
            }

            CommitAuthoringModelChange();
            Debug.Log($"[BakeIdPlayground] Authoring destroyed - {entry.kind} @ {FormatEntryCell(entry)}");
#endif
        }

        internal void NotifyAuthoringLinkChanged(TileView view, bool immediate)
        {
#if UNITY_EDITOR
            if (!authoringLiveSync || view == null || Application.isPlaying || _authoringSyncSuspendDepth > 0)
                return;
            if (view.gameObject.scene != gameObject.scene)
                return;

            if (!IsUnderAuthoringRoot(view.transform))
                TryAdoptTileView(view);

            ScheduleAuthoringLiveSync(immediate);
#endif
        }

        bool TryResolveAuthoringEntry(TileView view, out BakeIdSimTileEntry entry, out string error)
        {
            entry = default;
            error = null;
            if (view == null)
            {
                error = "view is null";
                return false;
            }

            // Always Ensure: duplicated / domain-reload links keep the component but lose
            // non-serialized _host/_view until Configure runs.
            BakeIdAuthoringViewLink link = BakeIdAuthoringViewLink.Ensure(view, this, cellSize);
            return link != null && link.TryResolveSimEntry(out entry, out error);
        }

        static string FormatEntryCell(in BakeIdSimTileEntry entry) =>
            entry.kind == BakeIdSimTileKind.ThinWall
                ? $"{entry.cell}↔{entry.cellB}"
                : entry.cell.ToString();

#if UNITY_EDITOR
        readonly struct AuthoringLiveSyncSuspendScope : System.IDisposable
        {
            readonly BakeIdPlaygroundHost _host;

            public AuthoringLiveSyncSuspendScope(BakeIdPlaygroundHost host)
            {
                _host = host;
                if (_host != null)
                    _host._authoringSyncSuspendDepth++;
            }

            public void Dispose()
            {
                if (_host == null || _host._authoringSyncSuspendDepth <= 0)
                    return;
                _host._authoringSyncSuspendDepth--;
            }
        }

        AuthoringLiveSyncSuspendScope SuspendAuthoringLiveSync() => new(this);

        void SyncAuthoringLiveSubscription()
        {
            if (!authoringLiveSync || Application.isPlaying)
            {
                UnsubscribeAuthoringLive();
                return;
            }

            if (_authoringSyncSubscribed)
                return;

            EditorApplication.hierarchyChanged += OnAuthoringHierarchyChanged;
            EditorApplication.update += OnAuthoringEditorUpdate;
            _authoringSyncSubscribed = true;
        }

        void UnsubscribeAuthoringLive()
        {
            if (!_authoringSyncSubscribed)
                return;

            EditorApplication.hierarchyChanged -= OnAuthoringHierarchyChanged;
            EditorApplication.update -= OnAuthoringEditorUpdate;
            _authoringSyncSubscribed = false;
            _authoringSyncPending = false;
        }

        void OnAuthoringHierarchyChanged()
        {
            if (!authoringLiveSync || _authoringSyncSuspendDepth > 0 || Application.isPlaying)
                return;

            TryAdoptOrphanAuthoringViews();
            ScheduleAuthoringLiveSync();
        }

        void ScheduleAuthoringLiveSync(bool immediate = false)
        {
            _authoringSyncPending = true;
            _authoringSyncDueTime = immediate
                ? EditorApplication.timeSinceStartup
                : EditorApplication.timeSinceStartup + AuthoringSyncDebounceSec;
        }

        void OnAuthoringEditorUpdate()
        {
            if (!authoringLiveSync || _authoringSyncSuspendDepth > 0 || Application.isPlaying)
                return;

            if (_authoringSyncPending)
            {
                if (EditorApplication.timeSinceStartup < _authoringSyncDueTime)
                    return;
                _authoringSyncPending = false;
                FlushAuthoringLiveSync();
                return;
            }

            if (HasAuthoringPoseDrift())
                ScheduleAuthoringLiveSync();
        }

        bool IsUnderAuthoringRoot(Transform t)
        {
            EnsureAuthoringRoot();
            return t != null && _authoringRoot != null && t.IsChildOf(_authoringRoot);
        }

        bool IsUnderVisualRoot(Transform t)
        {
            if (t == null)
                return false;
            Transform existing = transform.Find(VisualRootName);
            return existing != null && t.IsChildOf(existing);
        }

        void TryAdoptOrphanAuthoringViews()
        {
            EnsureAuthoringRoot();
            TileView[] all = UnityEngine.Object.FindObjectsByType<TileView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                TileView view = all[i];
                if (view == null)
                    continue;
                if (view.gameObject.scene != gameObject.scene)
                    continue;
                if (IsUnderAuthoringRoot(view.transform))
                    continue;
                if (IsUnderVisualRoot(view.transform))
                    continue;
                TryAdoptTileView(view);
            }
        }

        void TryAdoptTileView(TileView view)
        {
            if (view == null)
                return;

            EnsureAuthoringRoot();
            _authoringEverUsed = true;
            Undo.RecordObject(view.transform, "Adopt BakeId Authoring");
            view.transform.SetParent(_authoringRoot, worldPositionStays: true);
            BakeIdAuthoringViewLink.Ensure(view, this, cellSize);
            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(view.transform);
        }

        bool HasAuthoringPoseDrift()
        {
            if (_authoringSnapshot.Count == 0)
                return false;

            EnsureAuthoringRoot();
            TileView[] views = CollectAuthoringTileViews();
            for (int i = 0; i < views.Length; i++)
            {
                TileView view = views[i];
                if (view == null)
                    continue;

                int id = view.GetInstanceID();
                if (!_authoringSnapshot.TryGetValue(id, out BakeIdSimTileEntry prev))
                    continue;

                if (!TryResolveAuthoringEntry(view, out BakeIdSimTileEntry now, out _))
                    continue;

                if (!BakeIdSimTileEntry.EntriesEqual(prev, now))
                    return true;
            }

            return false;
        }

        void RebuildAuthoringSnapshotFromScene()
        {
            _authoringSnapshot.Clear();
            if (_authoringRoot == null)
                return;

            TileView[] views = CollectAuthoringTileViews();
            for (int i = 0; i < views.Length; i++)
            {
                TileView view = views[i];
                if (view == null)
                    continue;
                if (!TryResolveAuthoringEntry(view, out BakeIdSimTileEntry entry, out _))
                    continue;
                BakeIdAuthoringViewLink.Ensure(view, this, cellSize);
                _authoringSnapshot[view.GetInstanceID()] = entry;
            }
        }

        void FlushAuthoringLiveSync()
        {
            if (!authoringLiveSync || _authoringSyncSuspendDepth > 0 || Application.isPlaying)
                return;

            EnsureAuthoringRoot();
            if (!EnsurePrefabDb())
                return;

            using (SuspendAuthoringLiveSync())
            {
                TileView[] views = CollectAuthoringTileViews();
                var currentIds = new HashSet<int>();
                var staleIds = new List<int>();

                for (int i = 0; i < views.Length; i++)
                {
                    TileView view = views[i];
                    if (view == null)
                        continue;

                    int id = view.GetInstanceID();
                    if (!TryResolveAuthoringEntry(view, out BakeIdSimTileEntry entry, out string error))
                    {
                        if (_authoringSnapshot.ContainsKey(id))
                            staleIds.Add(id);
                        else if (!string.IsNullOrEmpty(error))
                        {
                            Debug.LogWarning(
                                $"[BakeIdPlayground] Authoring view ignored on '{view.name}': {error}",
                                view);
                        }

                        continue;
                    }

                    currentIds.Add(id);
                    if (!_authoringSnapshot.TryGetValue(id, out BakeIdSimTileEntry prev))
                    {
                        if (ApplyAuthoringAdd(entry))
                            _authoringSnapshot[id] = entry;
                        continue;
                    }

                    if (BakeIdSimTileEntry.EntriesEqual(prev, entry))
                        continue;

                    ApplyAuthoringMove(id, prev, entry);
                }

                foreach (KeyValuePair<int, BakeIdSimTileEntry> kv in _authoringSnapshot)
                {
                    if (currentIds.Contains(kv.Key))
                        continue;
                    staleIds.Add(kv.Key);
                }

                for (int i = 0; i < staleIds.Count; i++)
                {
                    int id = staleIds[i];
                    if (!_authoringSnapshot.TryGetValue(id, out BakeIdSimTileEntry removed))
                        continue;
                    ApplyAuthoringRemove(removed);
                    _authoringSnapshot.Remove(id);
                }
            }
        }

        bool ApplyAuthoringMove(int viewInstanceId, in BakeIdSimTileEntry prev, in BakeIdSimTileEntry next)
        {
            if (CountLiveAuthoringViewsAt(prev, exceptViewId: viewInstanceId) == 0)
                ApplyAuthoringRemove(prev, commit: false);

            bool added = ApplyAuthoringAdd(next, commit: false);
            _authoringSnapshot[viewInstanceId] = next;
            CommitAuthoringModelChange();

            if (!BakeIdSimTileEntry.EntriesEqual(prev, next))
            {
                Debug.Log(
                    $"[BakeIdPlayground] Authoring move {prev.kind} {FormatEntryCell(prev)} → {FormatEntryCell(next)}");
            }

            return added;
        }

        int CountLiveAuthoringViewsAt(in BakeIdSimTileEntry entry, int exceptViewId)
        {
            EnsureAuthoringRoot();
            TileView[] views = CollectAuthoringTileViews();
            int count = 0;
            for (int i = 0; i < views.Length; i++)
            {
                TileView view = views[i];
                if (view == null || view.GetInstanceID() == exceptViewId)
                    continue;
                if (!TryResolveAuthoringEntry(view, out BakeIdSimTileEntry got, out _))
                    continue;
                if (BakeIdSimTileEntry.EntriesEqual(got, entry))
                    count++;
            }

            return count;
        }
#endif

#if UNITY_EDITOR
        void SyncSceneMoveSubscription()
        {
            if (sceneMoveEditCell)
                SubscribeSceneMove();
            else
                UnsubscribeSceneMove();
        }

        void SubscribeSceneMove()
        {
            if (_sceneGuiSubscribed)
                return;
            SceneView.duringSceneGui += OnSceneGuiMoveEditCell;
            _sceneGuiSubscribed = true;
        }

        void UnsubscribeSceneMove()
        {
            if (!_sceneGuiSubscribed)
                return;
            SceneView.duringSceneGui -= OnSceneGuiMoveEditCell;
            _sceneGuiSubscribed = false;
        }

        void OnSceneGuiMoveEditCell(SceneView view)
        {
            if (!sceneMoveEditCell || this == null)
                return;

            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown || e.control || e.alt || e.command)
                return;

            Vector3Int delta = Vector3Int.zero;
            switch (e.keyCode)
            {
                case KeyCode.W: delta = new Vector3Int(0, 0, 1); break;
                case KeyCode.S: delta = new Vector3Int(0, 0, -1); break;
                case KeyCode.A: delta = new Vector3Int(-1, 0, 0); break;
                case KeyCode.D: delta = new Vector3Int(1, 0, 0); break;
                case KeyCode.Q: delta = new Vector3Int(0, -1, 0); break;
                case KeyCode.E: delta = new Vector3Int(0, 1, 0); break;
                case KeyCode.Tab:
                    moveTarget = moveTarget == EditCellTarget.A
                        ? EditCellTarget.B
                        : EditCellTarget.A;
                    e.Use();
                    EditorUtility.SetDirty(this);
                    SceneView.RepaintAll();
                    return;
                default:
                    return;
            }

            if (moveTarget == EditCellTarget.A)
                editCellA += delta;
            else
                editCellB += delta;

            e.Use();
            EditorUtility.SetDirty(this);
            SceneView.RepaintAll();
        }

        void OnDrawGizmos()
        {
            float cs = Mathf.Max(1e-4f, cellSize);
            DrawEditCellGizmo(editCellA, "A", new Color(0.2f, 0.85f, 1f, 1f),
                moveTarget == EditCellTarget.A && sceneMoveEditCell, cs);
            DrawEditCellGizmo(editCellB, "B", new Color(1f, 0.85f, 0.2f, 1f),
                moveTarget == EditCellTarget.B && sceneMoveEditCell, cs);

            if (simProbes == null)
                return;

            for (int i = 0; i < simProbes.Count; i++)
            {
                BakeIdSimProbe p = simProbes[i];
                if (string.IsNullOrEmpty(p.name))
                    continue;
                Vector3 world = TileHelper.ConvertGridToWorldPos(p.cell, cs);
                world.y += 1.05f * cs;
                Handles.Label(world, p.name, EditorStyles.boldLabel);
            }
        }

        void DrawEditCellGizmo(Vector3Int cell, string label, Color color, bool active, float cs)
        {
            Vector3 world = TileHelper.ConvertGridToWorldPos(cell, cs);
            float size = cs * (active ? EditCellGizmoSizeActive : EditCellGizmoSize);
            Gizmos.color = color;
            if (active)
                Gizmos.DrawCube(world, Vector3.one * size);
            else
                Gizmos.DrawWireCube(world, Vector3.one * size);

            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = active ? 14 : 11,
            };
            style.normal.textColor = color;
            string suffix = active ? " ★" : string.Empty;
            Handles.Label(world + Vector3.up * (0.65f * cs), $"{label} {cell}{suffix}", style);
        }
#endif

        static void DestroyImmediateSafe(UnityEngine.Object obj)
        {
            if (obj == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(obj);
            else
                UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
