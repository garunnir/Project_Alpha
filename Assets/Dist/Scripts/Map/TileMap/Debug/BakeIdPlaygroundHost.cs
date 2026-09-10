// ============================================================
// [BakeIdPlaygroundHost] — MasterPlayground 실타일 스폰·편집·Bake ID 라벨
// ============================================================

using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IsoTilemap
{
    /// <summary>
    /// 샘플 씬에서 Bake ID(building/room/space)를 Scene·Game 뷰로 확인한다.
    /// <see cref="TilePrefabDB"/> + <see cref="TileObjFactory"/>로 실제 맵 타일 프리팹을 스폰한다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class BakeIdPlaygroundHost : MonoBehaviour
    {
        const string VisualRootName = "BakeIdVisualRoot";
#if UNITY_EDITOR
        const string DefaultPrefabDbPath = "Assets/Dist/SOData/Tile/Tile Prefab DB.asset";
#endif

        [SerializeField] TilePrefabDB prefabDb;
        [SerializeField] float cellSize = 1f;
        [SerializeField] bool showIdLabels = true;
        [SerializeField] bool rebuildOnEnable = true;

        [Title("Edit cells")]
        [SerializeField] Vector3Int editCellA = new(1, 1, 10);
        [SerializeField] Vector3Int editCellB = new(2, 1, 10);

        [ShowInInspector, ReadOnly]
        string Status => _hub == null
            ? "empty — Rebuild MasterPlayground"
            : $"tiles={_model.TilesSnapshot.Count} db={(prefabDb != null ? prefabDb.name : "MISSING")}";

        TileMapModel _model;
        TileMapCacheHub _hub;
        BuildingGroupBuilder _builder;
        TileObjFactory _factory;
        Transform _visualRoot;

        void OnEnable()
        {
            EnsurePrefabDb();
            if (rebuildOnEnable && _hub == null)
                RebuildMasterPlayground();
        }

        void OnDisable()
        {
            ClearVisuals();
            TearDownRuntime();
        }

        void OnValidate()
        {
            EnsurePrefabDb();
        }

        [Button("Rebuild MasterPlayground"), PropertyOrder(-10)]
        public void RebuildMasterPlayground()
        {
            TearDownRuntime();
            if (!EnsurePrefabDb())
            {
                Debug.LogError("[BakeIdPlayground] TilePrefabDB missing — assign Prefab Db or ensure " +
#if UNITY_EDITOR
                    DefaultPrefabDbPath
#else
                    "Tile Prefab DB"
#endif
                );
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

            IReadOnlyList<TileData> tiles = BakeIdPlaygroundLayout.MasterPlayground.Tiles;
            for (int i = 0; i < tiles.Count; i++)
                _model.SetTile(tiles[i]);

            _builder.AssignAll();
            RefreshVisuals();
        }

        [Button("Full Rebake (AssignAll)")]
        public void FullRebake()
        {
            EnsureRuntime();
            _builder.AssignAll();
            RefreshVisuals();
        }

        [Button("Add Floor @ A")]
        public void AddFloorAtA()
        {
            EnsureRuntime();
            _model.SetTile(BakeIdSyntheticTiles.Floor(editCellA));
            RefreshVisuals();
        }

        [Button("Add ThinWall A↔B")]
        public void AddThinWallAB()
        {
            EnsureRuntime();
            _model.SetTile(BakeIdSyntheticTiles.ThinWall(editCellA, editCellB));
            RefreshVisuals();
        }

        [Button("Add Cube @ A")]
        public void AddCubeAtA()
        {
            EnsureRuntime();
            _model.SetTile(BakeIdSyntheticTiles.Cube(editCellA));
            RefreshVisuals();
        }

        [Button("Remove tile @ A (floor preferred)")]
        public void RemoveAtA()
        {
            EnsureRuntime();
            if (!TryFindRemovableAtA(out TileData tile))
            {
                Debug.LogWarning($"[BakeIdPlayground] No tile at editCellA={editCellA}");
                return;
            }

            _model.RemoveTile(tile);
            RefreshVisuals();
        }

        void EnsureRuntime()
        {
            if (_hub != null && _builder != null && _model != null && _factory != null)
                return;
            RebuildMasterPlayground();
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
            int spawned = 0;
            int missed = 0;
            foreach (TileData tile in _model.TilesSnapshot)
            {
                TileView view = _factory.SpawnTile(tile, cs);
                if (view == null)
                {
                    missed++;
                    Debug.LogWarning(
                        $"[BakeIdPlayground] Prefab missing for '{tile.identity.PrefabId}' at {tile.identity.GridPos}");
                    continue;
                }

                spawned++;
            }

            if (missed > 0)
                Debug.LogError($"[BakeIdPlayground] {missed} tiles failed to spawn (check PrefabDB). spawned={spawned}");

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

        bool TryFindRemovableAtA(out TileData tile)
        {
            tile = default;
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
                return true;
            }

            if (hasAny)
            {
                tile = anyMatch;
                return true;
            }

            foreach (TileData candidate in _model.TilesSnapshot)
            {
                if (BakeIdSyntheticTiles.IsThinWallBetween(candidate, editCellA, editCellB))
                {
                    tile = candidate;
                    return true;
                }
            }

            return false;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!showIdLabels || _hub == null)
                return;

            float cs = Mathf.Max(1e-4f, cellSize);
            DrawRegionHint("HouseL", BakeIdPlaygroundLayout.MasterPlayground.OpenPair_A, cs);
            DrawRegionHint("HouseR", BakeIdPlaygroundLayout.MasterPlayground.ThinWall_Right, cs);
            DrawRegionHint("OpenShed", BakeIdPlaygroundLayout.MasterPlayground.OpenShed_A, cs);
            DrawRegionHint("CubeSplit", BakeIdPlaygroundLayout.MasterPlayground.CubeWall_Left, cs);
            DrawRegionHint("Bridge", BakeIdPlaygroundLayout.MasterPlayground.Bridge_B1, cs);
            DrawRegionHint("Column", BakeIdPlaygroundLayout.MasterPlayground.Column_Upper, cs);
            DrawRegionHint("GShape", BakeIdPlaygroundLayout.MasterPlayground.GShape_A, cs);
            DrawRegionHint("Balcony", BakeIdPlaygroundLayout.MasterPlayground.Balcony_Floor, cs);
            DrawRegionHint("Plaza", BakeIdPlaygroundLayout.MasterPlayground.Plaza_Floor, cs);
            DrawRegionHint("Dig", BakeIdPlaygroundLayout.MasterPlayground.Dig_Floor, cs);
        }

        static void DrawRegionHint(string name, Vector3Int walkableCell, float cs)
        {
            Vector3 world = TileHelper.ConvertGridToWorldPos(walkableCell, cs);
            world.y += 1.1f * cs;
            Handles.Label(world, name, EditorStyles.boldLabel);
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
