// ============================================================
// FishCellTargetSession — 낚시 셀 클릭 타겟팅 (커서·취소)
// ============================================================

using IsoTilemap;
using UnityEngine;

public sealed class FishCellTargetSession : IFarmCellTargetSession, ICellTargetSessionTickable
{
    static FishCellTargetSession _active;

    GridCursor _gridCursor;
    CharacterActionHost _actionHost;
    FishCellActionKind _kind;
    ItemStack _stack;
    InventoryContainer _container;

    public static bool IsActive => _active != null;

    public int CancelPriority => UiCancelPriority.FishCellTarget;

    public static bool TryBegin(
        FishCellActionKind kind,
        ItemStack stack,
        InventoryContainer container)
    {
        if (IsActive || UIConstruction.IsOpen || ConstructionCellTargetSession.IsActive)
            return false;

        var session = new FishCellTargetSession();
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
        FishCellActionKind kind,
        ItemStack stack,
        InventoryContainer container)
    {
        _kind = kind;
        _stack = stack;
        _container = container;

        _gridCursor = Object.FindFirstObjectByType<GridCursor>(FindObjectsInactive.Include);
        if (_gridCursor == null)
        {
            Debug.LogError("[FishCellTargetSession] GridCursor not found.");
            return false;
        }

        _actionHost = ResolvePossessedActionHost();
        if (_actionHost == null)
        {
            Debug.LogError("[FishCellTargetSession] CharacterActionHost missing on possessed body.");
            return false;
        }

        BindWorkAnimOnGearHost(PlayerGearHost.Active);

        _gridCursor.BeginTargeting(this);
        return true;
    }

    static CharacterActionHost ResolvePossessedActionHost()
    {
        CharacterActionHost session = CharacterSessionHub.SessionActionHost;
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

    public bool CanApply(Vector3Int cell)
    {
        switch (_kind)
        {
            case FishCellActionKind.Cast:
                return MapFishService.CanCastAt(cell, _stack, _container);
            case FishCellActionKind.DeployTrap:
                return MapFishService.CanDeployTrapAt(cell, _stack, _container);
            case FishCellActionKind.CollectTrap:
                return MapFishService.CanCollectTrapAt(cell);
            default:
                return false;
        }
    }

    public void OnCellHover(Vector3Int cell, bool canApply)
    {
        Color tint = canApply
            ? MapFishConsts.TargetPreviewValid
            : MapFishConsts.TargetPreviewInvalid;
        _gridCursor?.SetTargetTint(tint);
    }

    public bool TryConfirm(Vector3Int cell)
    {
        if (!CanApply(cell))
            return false;

        FishCellActionKind kind = _kind;
        ItemStack stack = _stack;
        InventoryContainer container = _container;
        CharacterActionHost host = _actionHost;

        EndTargeting();
        host.TryRunFish(kind, cell, stack, container);
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
        CellTargetSessionDriver.ClearActive(this);
        if (ReferenceEquals(_active, this))
            _active = null;
    }
}
