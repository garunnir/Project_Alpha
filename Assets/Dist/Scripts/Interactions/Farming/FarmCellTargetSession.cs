// ============================================================
// FarmCellTargetSession — 농사 셀 클릭 타겟팅 (커서·프리뷰·취소)
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;
using UnityEngine;

public sealed class FarmCellTargetSession : IFarmCellTargetSession, ICellTargetSessionTickable
{
    static FarmCellTargetSession _active;

    GridCursor _gridCursor;
    CharacterActionHost _actionHost;
    FarmCellActionKind _kind;
    ItemStack _stack;
    InventoryContainer _container;
    bool _showPlantPreview;
    CellTargetPreview3D _preview;

    public static bool IsActive => _active != null;

    public int CancelPriority => UiCancelPriority.FarmCellTarget;

    public static bool TryBegin(
        FarmCellActionKind kind,
        ItemStack stack,
        InventoryContainer container)
    {
        if (IsActive || UIConstruction.IsOpen || ConstructionCellTargetSession.IsActive)
            return false;

        var session = new FarmCellTargetSession();
        if (!session.BeginInternal(kind, stack, container))
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
        TryHandlePrimaryClick();
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

    bool BeginInternal(
        FarmCellActionKind kind,
        ItemStack stack,
        InventoryContainer container)
    {
        _kind = kind;
        _stack = stack;
        _container = container;
        _showPlantPreview = kind == FarmCellActionKind.Plant;

        _gridCursor = Object.FindFirstObjectByType<GridCursor>(FindObjectsInactive.Include);
        if (_gridCursor == null)
        {
            Debug.LogError("[FarmCellTargetSession] GridCursor not found.");
            return false;
        }

        _actionHost = ResolvePossessedActionHost();
        if (_actionHost == null)
        {
            Debug.LogError("[FarmCellTargetSession] CharacterActionHost missing on possessed body.");
            return false;
        }

        BindWorkAnimOnGearHost(PlayerGearHost.Active);

        if (_showPlantPreview)
        {
            _preview = new CellTargetPreview3D();
            _preview.BeginPlantMode();
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

    static void BindWorkAnimOnGearHost(PlayerGearHost gear)
    {
        if (gear == null)
            return;

        CharacterBodyRoot bodyRoot = gear.GetComponentInParent<CharacterBodyRoot>();
        if (bodyRoot != null)
            CharacterWorkAnimBinder.BindBody(bodyRoot.gameObject);
    }

    public bool CanApply(Vector3Int cell) =>
        MapPlantService.CanApplyAtCell(_kind, cell, _stack, _container);

    public void OnCellHover(Vector3Int cell, bool canApply)
    {
        Color tint = canApply
            ? MapPlantConsts.TargetPreviewValid
            : MapPlantConsts.TargetPreviewInvalid;
        _gridCursor?.SetTargetTint(tint);

        if (!_showPlantPreview || _preview == null)
            return;

        MapPlantHost host = MapPlantHost.Runtime;
        float cellSize = host != null ? host.CellSize : 1f;
        string seedItemId = _stack != null ? _stack.ItemId : null;
        _preview.ShowPlant(
            cell,
            cellSize,
            PlantGrowthStage.Harvestable,
            seedItemId,
            canApply);
    }

    public bool TryConfirm(Vector3Int cell)
    {
        if (!CanApply(cell))
            return false;

        FarmCellActionKind kind = _kind;
        ItemStack stack = _stack;
        InventoryContainer container = _container;
        CharacterActionHost host = _actionHost;

        EndTargeting();
        host.TryRunFarm(kind, cell, stack, container);
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
