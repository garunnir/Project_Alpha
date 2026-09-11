// ============================================================
// [BakeIdPlaygroundHost] — 씬 직렬화 Sim 배치·probe·rule + 실타일 미리보기
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
            ? $"empty — tiles={simTiles.Count} probes={simProbes.Count} rules={simRules.Count}"
            : $"live model={_model.TilesSnapshot.Count}  simTiles={simTiles.Count}  probes={simProbes.Count}";

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
        [InfoBox("변경 후 씬 저장 필수. VisualRoot는 SSOT가 아님 — simTiles가 진실원.")]
        [ButtonGroup(Tab + "/Setup/Pipeline")]
        [Button("Rebuild From Sim Tiles", ButtonSizes.Medium)]
        public void RebuildFromSimTiles()
        {
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
                return;
            }

            _model = new TileMapModel();
            var registry = new BuildingGroupRegistry();
            _hub = TileMapCacheHub.Create(_model, registry);
            _model.SetMapCacheHub(_hub);
            _builder = new BuildingGroupBuilder(_model, _hub);
            _model.SetBuildingGroupBuilder(_builder);

            EnsureVisualRoot();
            _factory = new TileObjFactory(_visualRoot, prefabDb);

            List<TileData> tiles = BakeIdSimTileUtil.ToTileDataList(simTiles);
            for (int i = 0; i < tiles.Count; i++)
                _model.SetTile(tiles[i]);

            _builder.AssignAll();
            RefreshVisuals();
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
            RebuildFromSimTiles();
        }

        [TabGroup(Tab, "Setup")]
        [ButtonGroup(Tab + "/Setup/Verify")]
        [Button("Full Rebake", ButtonSizes.Medium)]
        public void FullRebake()
        {
            EnsureRuntime();
            _builder.AssignAll();
            RefreshVisuals();
        }

        [TabGroup(Tab, "Setup")]
        [ButtonGroup(Tab + "/Setup/Verify")]
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
            _model.SetTile(entry.ToTileData());
            MarkSimDirty();
            RefreshVisuals();
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
            _model.SetTile(entry.ToTileData());
            MarkSimDirty();
            RefreshVisuals();
        }

        [TabGroup(Tab, "Edit")]
        [BoxGroup(Tab + "/Edit/Place")]
        [ButtonGroup(Tab + "/Edit/Place/Tiles")]
        [Button("ThinWall A↔B")]
        public void AddThinWallAB()
        {
            EnsureRuntime();
            var entry = BakeIdSimTileEntry.ThinWall(editCellA, editCellB);
            simTiles.Add(entry);
            _model.SetTile(entry.ToTileData());
            MarkSimDirty();
            RefreshVisuals();
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
            RefreshVisuals();
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

        TileMapModel _model;
        TileMapCacheHub _hub;
        BuildingGroupBuilder _builder;
        TileObjFactory _factory;
        Transform _visualRoot;

#if UNITY_EDITOR
        bool _sceneGuiSubscribed;
#endif

        void OnEnable()
        {
            EnsurePrefabDb();
            if (rebuildOnEnable && _hub == null)
                RebuildFromSimTiles();
#if UNITY_EDITOR
            SyncSceneMoveSubscription();
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnsubscribeSceneMove();
#endif
            ClearVisuals();
            TearDownRuntime();
        }

        void OnValidate()
        {
            EnsurePrefabDb();
#if UNITY_EDITOR
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
            if (_hub != null && _builder != null && _model != null && _factory != null)
                return;
            RebuildFromSimTiles();
        }

        void TearDownRuntime()
        {
            ClearVisuals();
            _factory = null;
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
            EditorUtility.SetDirty(this);
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

        void EnsureVisualRoot()
        {
            if (_visualRoot != null)
                return;

            Transform existing = transform.Find(VisualRootName);
            if (existing != null)
            {
                _visualRoot = existing;
                return;
            }

            var go = new GameObject(VisualRootName);
            go.transform.SetParent(transform, false);
            _visualRoot = go.transform;
        }

        void ClearVisuals()
        {
            if (_visualRoot == null)
            {
                Transform existing = transform.Find(VisualRootName);
                if (existing != null)
                    DestroyImmediateSafe(existing.gameObject);
                return;
            }

            DestroyImmediateSafe(_visualRoot.gameObject);
            _visualRoot = null;
        }

        void RefreshVisuals()
        {
            ClearVisuals();
            EnsureVisualRoot();
            if (_model == null || !EnsurePrefabDb())
                return;

            _factory = new TileObjFactory(_visualRoot, prefabDb);

            float cs = Mathf.Max(1e-4f, cellSize);
            int missed = 0;
            foreach (TileData tile in _model.TilesSnapshot)
            {
                TileView view = _factory.SpawnTile(tile, cs);
                if (view == null)
                {
                    missed++;
                    Debug.LogWarning(
                        $"[BakeIdPlayground] Prefab missing for '{tile.identity.PrefabId}' at {tile.identity.GridPos}");
                }
            }

            if (missed > 0)
                Debug.LogError($"[BakeIdPlayground] {missed} tiles failed to spawn");

            if (showIdLabels)
                SpawnFloorIdLabels(cs);
        }

        void SpawnFloorIdLabels(float cs)
        {
            if (_hub == null)
                return;

            foreach (var (x, cellY, z) in _hub.Topology.Index.EnumerateWalkableFloorCells())
            {
                var cell = new Vector3Int(x, cellY, z);
                if (!_hub.TryGetFloorFaceForWalkableCell(x, cellY, z, out TileData face))
                    continue;

                int buildingId = face.identity.buildingId;
                int roomId = face.identity.roomId;
                int spaceId = 0;
                if (_hub.Spaces.TryGetSpaceAtFloorCell(cell, out int sid))
                    spaceId = sid;

                Vector3 world = TileWorldPointUtil.GetRepresentativeWorldPoint(face.identity, cs);
                world.y += 0.55f * cs;

                var labelGo = new GameObject($"id_{cell}");
                labelGo.transform.SetParent(_visualRoot, false);
                labelGo.transform.position = world;

                var tm = labelGo.AddComponent<TextMesh>();
                tm.text = $"B:{buildingId} R:{roomId} S:{spaceId}";
                tm.fontSize = 32;
                tm.characterSize = 0.05f * cs;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = buildingId == TileIdentity.BuildingIdOutdoor
                    ? new Color(0.4f, 0.85f, 1f)
                    : Color.white;
            }
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

        static void DestroyImmediateSafe(Object obj)
        {
            if (obj == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }
    }
}
