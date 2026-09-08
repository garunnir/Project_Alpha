// ============================================================
// SetHandAction / WieldGrip / UnwieldSlot — 들기 슬롯 RMB
// ============================================================

public sealed class SetHandActionContextAction : IContextMenuAction
{
    readonly WieldSlotContextRequest _request;
    readonly CombatLeaf? _action;

    public SetHandActionContextAction(WieldSlotContextRequest request, CombatLeaf? action)
    {
        _request = request;
        _action = action;
    }

    public string GetDisabledReason()
    {
        if (_request?.Gear == null)
            return "missing";
        if (_action == null)
            return null;

        ItemStack stack = _request.Gear.Wield?.Get(_request.Slot);
        if (stack?.Item == null)
            return "missing";

        WeaponPresentation presentation = CombatLeafRows.Resolve(
            _request.Gear.PresentationCatalog,
            stack);
        CombatLeafMask mask = CombatLeafRows.Available(presentation);
        return (mask & CombatLeafUtil.ToMask(_action.Value)) == 0
            ? CharacterGearLabels.BlockedInvalid
            : null;
    }

    public void Execute()
    {
        if (_request?.Gear == null || !string.IsNullOrEmpty(GetDisabledReason()))
            return;

        ItemStack stack = _request.Gear.Wield?.Get(_request.Slot);
        _request.Gear.TrySetHandAction(stack, _action);
        _request.OnChanged?.Invoke();
    }
}

public sealed class WieldGripContextAction : IContextMenuAction
{
    readonly WieldSlotContextRequest _request;
    readonly WieldHand _hand;

    public WieldGripContextAction(WieldSlotContextRequest request, WieldHand hand)
    {
        _request = request;
        _hand = hand;
    }

    public string GetDisabledReason()
    {
        if (_request?.Gear == null)
            return CharacterGearLabels.BlockedInvalid;

        ItemStack stack = _request.Gear.Wield?.Get(_request.Slot);
        return _request.Gear.GetWieldGripBlockedReason(stack, _hand);
    }

    public void Execute()
    {
        if (_request?.Gear == null || !string.IsNullOrEmpty(GetDisabledReason()))
            return;

        ItemStack stack = _request.Gear.Wield?.Get(_request.Slot);
        _request.Gear.TryBeginWieldGrip(stack, _hand);
        _request.OnChanged?.Invoke();
    }
}

public sealed class UnwieldSlotContextAction : IContextMenuAction
{
    readonly WieldSlotContextRequest _request;
    readonly bool _toFloor;

    public UnwieldSlotContextAction(WieldSlotContextRequest request, bool toFloor)
    {
        _request = request;
        _toFloor = toFloor;
    }

    public string GetDisabledReason() => _request?.Gear == null ? "missing" : null;

    public void Execute()
    {
        if (_request?.Gear == null)
            return;

        _request.Gear.TryBeginUnwieldSlot(_request.Slot, _toFloor);
        _request.OnChanged?.Invoke();
    }
}
