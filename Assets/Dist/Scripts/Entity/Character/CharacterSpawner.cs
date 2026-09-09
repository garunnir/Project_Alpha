// ============================================================
// CharacterSpawner — 셀 SSOT로 본체 프리팹 소환 후 맵·possess·NPC 등록
// ============================================================

using System.Collections.Generic;
using IsoTilemap;
using UnityEngine;

[DefaultExecutionOrder(20)]
[DisallowMultipleComponent]
public sealed class CharacterSpawner : MonoBehaviour
{
    [SerializeField] List<CharacterSpawnEntry> _entries = new();
    [SerializeField] TileMapManager _tileMapManager;
    [SerializeField] MapGameplayBootstrap _mapBootstrap;
    [SerializeField] PlayerManager _playerManager;
    [SerializeField] NpcManager _npcManager;
    [SerializeField, Min(0.0001f)] float _editorGizmoCellSize = 1f;

    readonly List<Vector3Int> _footprintCellsScratch = new();
    readonly List<TileData> _tileScratch = new();

    void OnValidate()
    {
        if (_entries == null)
            return;
        for (int i = 0; i < _entries.Count; i++)
            _entries[i]?.SyncCellFromMarker();
    }

    void Start() => SpawnAll();

    public void SpawnAll()
    {
        if (_tileMapManager == null)
            _tileMapManager = FindFirstObjectByType<TileMapManager>();
        if (_mapBootstrap == null)
            _mapBootstrap = FindFirstObjectByType<MapGameplayBootstrap>();
        if (_playerManager == null)
            _playerManager = FindFirstObjectByType<PlayerManager>();
        if (_npcManager == null)
            _npcManager = FindFirstObjectByType<NpcManager>();

        IWorldGrid worldGrid = _tileMapManager != null ? _tileMapManager.WorldGrid : null;
        Transform parent = CharacterWorldRoot.Resolve();

        if (_entries == null)
            return;

        GameObject possessedBody = null;
        for (int i = 0; i < _entries.Count; i++)
        {
            CharacterSpawnEntry entry = _entries[i];
            if (entry == null || entry.definition == null)
            {
                Debug.LogError($"[CharacterSpawner] Entry {i} is missing definition.", this);
                continue;
            }

            Vector3Int cell = entry.ResolveCell();
            Vector3Int footprint = entry.definition.GridFootprint;
            // entry.cell / marker = 본체(body) 셀. floor·점유 검증은 발 셀 기준.
            ValidateSpawnFootprint(i, BodyCellToFeetCell(cell, footprint), footprint);

            Vector3 world = worldGrid != null
                ? worldGrid.CellToWorld(cell)
                : TileHelper.ConvertGridToWorldPos(cell, _editorGizmoCellSize);

            GameObject instance = CharacterFactory.InstantiateInactive(
                entry.definition,
                world,
                parent,
                useGameplayDataOwner: entry.role == CharacterSpawnRole.Possessed);
            if (instance == null)
                continue;

            AssignBodyInventoryId(instance, entry.role);
            instance.SetActive(true);
            CharacterWorkAnimBinder.BindBody(instance);

            if (_mapBootstrap != null)
                _mapBootstrap.BindSpawnedCharacter(instance);
            else if (worldGrid != null)
            {
                CharacterState state = instance.GetBodyComponent<CharacterState>();
                state?.BindWorldGrid(worldGrid);
            }

            CharacterSpawnGearApplier.Apply(entry.definition, instance);

            if (entry.role == CharacterSpawnRole.Possessed)
            {
                possessedBody = instance;
                continue;
            }

            if (_npcManager == null)
            {
                Debug.LogError("[CharacterSpawner] NpcManager missing for Npc spawn.", this);
                continue;
            }

            CharacterSpawnNpcSettings npc = entry.npc ?? new CharacterSpawnNpcSettings();
            _npcManager.Register(npc.ToAgentEntry(instance.transform));
        }

        if (possessedBody == null)
            return;

        if (_playerManager != null)
            _playerManager.Possess(possessedBody);
        else
            Debug.LogError("[CharacterSpawner] PlayerManager missing for Possessed spawn.", this);

        if (PlayerProgressSaveBridge.TryRestorePossessed(possessedBody))
            SyncPossessedMapPresentation();
    }

    static void SyncPossessedMapPresentation()
    {
        PlayerPossessedInputHost input =
            Object.FindFirstObjectByType<PlayerPossessedInputHost>();
        input?.SyncMapPresentationDrivers();
    }

    void OnDrawGizmos()
    {
        if (_entries == null)
            return;

        if (!Application.isPlaying)
        {
            for (int i = 0; i < _entries.Count; i++)
                _entries[i]?.SyncCellFromMarker();
        }

        float cellSize = ResolveEditorCellSize();
        for (int i = 0; i < _entries.Count; i++)
        {
            CharacterSpawnEntry entry = _entries[i];
            if (entry == null)
                continue;

            if (entry.marker != null)
                continue;

            Vector3Int cell = entry.ResolveCell();
            Vector3Int footprint = entry.definition != null
                ? entry.definition.GridFootprint
                : CharacterGridFootprintDefaults.Default;
            DrawFootprintGizmo(
                BodyCellToFeetCell(cell, footprint),
                footprint,
                cellSize,
                CharacterSpawnGizmoColors.ForRole(entry.role));
        }
    }

    void DrawFootprintGizmo(Vector3Int feetCell, Vector3Int footprint, float cellSize, Color color)
    {
        if (!CharacterOccupiedCellUtil.TryGetAnchorFromFeet(feetCell, footprint, out Vector3Int anchor))
            return;

        TileHelper.DrawOccupiedCellWire(anchor, cellSize, color, footprint);
    }

    /// <summary>
    /// 스폰 셀은 본체 중심(Capsule/transform). 발 Y = bodyY − footprint.y/2
    /// (높이 2셀·중심 정렬 캡슐과 패리티).
    /// </summary>
    public static Vector3Int BodyCellToFeetCell(Vector3Int bodyCell, Vector3Int footprint)
    {
        footprint = CharacterGridFootprintDefaults.Clamp(footprint);
        return new Vector3Int(bodyCell.x, bodyCell.y - footprint.y / 2, bodyCell.z);
    }

    void ValidateSpawnFootprint(int entryIndex, Vector3Int feetCell, Vector3Int footprint)
    {
        TileMapCacheHub hub = _tileMapManager != null ? _tileMapManager.MapCacheHub : null;
        if (hub == null)
            return;

        _footprintCellsScratch.Clear();
        CharacterOccupiedCellUtil.AppendOccupiedCells(feetCell, footprint, _footprintCellsScratch);

        for (int i = 0; i < _footprintCellsScratch.Count; i++)
        {
            Vector3Int occupiedCell = _footprintCellsScratch[i];
            _tileScratch.Clear();
            if (!hub.TryCollectTilesAtOccupiedCell(occupiedCell, _tileScratch))
                continue;

            if (TileCollisionFlagsUtil.CellBlocksOccupied(_tileScratch))
            {
                Debug.LogError(
                    $"[CharacterSpawner] Entry {entryIndex}: solid wall blocks footprint at {occupiedCell}.",
                    this);
            }
        }

        if (!CharacterOccupiedCellUtil.TryGetAnchorFromFeet(feetCell, footprint, out Vector3Int anchor))
            return;

        int sx = footprint.x;
        int sz = footprint.z;
        for (int x = anchor.x; x < anchor.x + sx; x++)
        {
            for (int z = anchor.z; z < anchor.z + sz; z++)
            {
                if (hub.CellHasFloor(x, feetCell.y, z))
                    continue;

                Debug.LogError(
                    $"[CharacterSpawner] Entry {entryIndex}: missing floor under footprint at ({x},{feetCell.y},{z}).",
                    this);
            }
        }
    }

    static void AssignBodyInventoryId(GameObject instance, CharacterSpawnRole role)
    {
        if (!instance.TryGetBodyComponent(out PlayerInventoryHost host))
            return;

        if (role == CharacterSpawnRole.Possessed)
            host.AssignInstanceId(PlayerInventoryHost.DefaultInstanceId);
        else
            host.AssignInstanceId(PlayerInventoryHost.CreateUniqueBodyInstanceId());
    }

    float ResolveEditorCellSize()
    {
        if (_tileMapManager != null && _tileMapManager.WorldGrid != null)
            return Mathf.Max(0.0001f, _tileMapManager.WorldGrid.CellSize);
        return Mathf.Max(0.0001f, _editorGizmoCellSize);
    }
}
