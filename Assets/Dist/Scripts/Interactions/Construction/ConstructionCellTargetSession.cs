// ============================================================
// ConstructionCellTargetSession — 건설 셀 타겟팅 (커서·3D 고스트·회전·취소)
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class ConstructionCellTargetSession : IFarmCellTargetSession, ICellTargetSessionTickable
{
    static ConstructionCellTargetSession _active;

    GridCursor _gridCursor;
    CharacterActionHost _actionHost;
    ConstructionData _data;
    CellTargetPreview3D _preview;
    CraftingMaterialPool _pool;
    TileMapManager _mapManager;

    public static bool IsActive => _active != null;

    public int CancelPriority => UiCancelPriority.ConstructionCellTarget;

    public static bool TryBegin(ConstructionData data)
    {
        if (data == null ||
            IsActive ||
            UIConstruction.IsOpen ||
            UIConstructionController.IsGameplayOpen ||
            FarmCellTargetSession.IsActive ||
            FishCellTargetSession.IsActive)
            return false;

        var session = new ConstructionCellTargetSession();
        if (!session.BeginInternal(data))
            return false;

        if (!CellTargetSessionDriver.TrySetActive(session))
        {
            session.EndTargeting();
            return false;
        }

        _active = session;
        return true;
    }

    public static bool TryConsumeRightClick()
    {
        if (_active == null)
            return false;

        _active.Cancel();
        return true;
    }

    public void Tick()
    {
        _gridCursor?.SyncFromPointer();
        TryHandleRotate();
        TryHandlePrimaryClick();
    }

    void TryHandleRotate()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || !kb.rKey.wasPressedThisFrame)
            return;

        _preview?.RotateStep(+1);
        if (_gridCursor != null)
            NotifyHoverForCurrent();
    }

    void TryHandlePrimaryClick()
    {
        InputManager input = InputManager.Instance;
        if (input == null ||
            !input.TryReadPointerPressedThisFrame(out bool pressed) ||
            !pressed)
            return;

        _gridCursor?.TryConfirmTargetingClick();
    }

    bool BeginInternal(ConstructionData data)
    {
        _data = data;
        _pool = ConstructionService.CreatePoolFromActivePlayer();
        _mapManager = Object.FindFirstObjectByType<TileMapManager>();

        _gridCursor = Object.FindFirstObjectByType<GridCursor>(FindObjectsInactive.Include);
        if (_gridCursor == null)
        {
            Debug.LogError("[ConstructionCellTargetSession] GridCursor not found.");
            return false;
        }

        _actionHost = ResolvePossessedActionHost();
        if (_actionHost == null)
        {
            Debug.LogError("[ConstructionCellTargetSession] CharacterActionHost missing on possessed body.");
            return false;
        }

        _preview = new CellTargetPreview3D();
        TilePlacementSlot slot = ConstructionService.ResolvePostSlot(data);
        _preview.BeginTileGhostMode(slot);

        if (_mapManager != null &&
            _mapManager.PrefabDB != null &&
            _mapManager.PrefabDB.TryGetDefinition(data.post_prefab_id, out TileDefinition def) &&
            def != null &&
            def.prefab != null)
        {
            _preview.SetTileGhostPrefab(def.prefab);
        }

        _gridCursor.BeginTargeting(this);
        return true;
    }

    static CharacterActionHost ResolvePossessedActionHost()
    {
        CharacterActionHost session = PlayerPossessSession.SessionActionHost;
        if (session != null)
            return session;

        PlayerGearHost gear = PlayerGearHost.Active;
        if (gear != null)
        {
            CharacterActionHost fromGear = gear.GetBodyComponent<CharacterActionHost>();
            if (fromGear != null)
                return fromGear;
        }

        return PlayerInventoryRuntime.Active?.Host != null
            ? PlayerInventoryRuntime.Active.Host.GetBodyComponent<CharacterActionHost>()
            : null;
    }

    public bool CanApply(Vector3Int cell)
    {
        _pool = ConstructionService.CreatePoolFromActivePlayer();
        return ConstructionService.CanBuild(
            _data,
            cell,
            _pool,
            _mapManager,
            _preview != null ? _preview.FacingQuarters : 0);
    }

    public void OnCellHover(Vector3Int cell, bool canApply)
    {
        Color tint = canApply
            ? ConstructionConsts.TargetPreviewValid
            : ConstructionConsts.TargetPreviewInvalid;
        _gridCursor?.SetTargetTint(tint);

        float cellSize = _mapManager != null && _mapManager.WorldGrid != null
            ? _mapManager.WorldGrid.CellSize
            : 1f;
        _preview?.ShowTileAtCell(cell, cellSize, canApply);
    }

    void NotifyHoverForCurrent()
    {
        _gridCursor?.SyncFromPointer();
    }

    public bool TryConfirm(Vector3Int cell)
    {
        if (!CanApply(cell))
            return false;

        ConstructionData data = _data;
        int facing = _preview != null ? _preview.FacingQuarters : 0;
        CharacterActionHost host = _actionHost;

        EndTargeting();
        host.TryRunConstruction(data, cell, facing);
        return true;
    }

    public void OnCancel() => Cancel();

    public void Cancel()
    {
        if (!IsActive || _active != this)
            return;

        EndTargeting();
    }

    public bool TryHandleCancel()
    {
        if (_active != this)
            return false;

        Cancel();
        return true;
    }

    void EndTargeting()
    {
        _gridCursor?.EndTargeting();
        _preview?.Dispose();
        _preview = null;
        CellTargetSessionDriver.ClearActive(this);
        if (ReferenceEquals(_active, this))
            _active = null;
    }
}
