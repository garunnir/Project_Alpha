// ============================================================
// StripWearContextAction — 쓰러진 NPC 착용 아이템 벗겨주기 (PC timed)
// ============================================================

public sealed class StripWearContextAction : IContextMenuAction
{
    readonly CharacterGearService _victimGear;
    readonly ItemStack _stack;

    public StripWearContextAction(CharacterGearService victimGear, ItemStack stack)
    {
        _victimGear = victimGear;
        _stack = stack;
    }

    public string GetDisabledReason()
    {
        if (_victimGear == null || _stack == null)
            return CharacterGearLabels.BlockedInvalid;
        if (_victimGear.ToolSession.IsActive)
            return CharacterGearLabels.BlockedToolSession;
        if (!_victimGear.Wear.Contains(_stack))
            return CharacterGearLabels.BlockedInvalid;

        CharacterGearService interactor = PlayerGearHost.Active?.Service;
        CharacterActionHost host = PlayerPossessSession.SessionActionHost;
        if (interactor == null || host == null)
            return CharacterGearLabels.BlockedInvalid;
        if (interactor.ToolSession.IsActive)
            return CharacterGearLabels.BlockedToolSession;

        return null;
    }

    public void Execute()
    {
        CharacterGearService interactor = PlayerGearHost.Active?.Service;
        interactor?.TryBeginAssistedStripWear(_victimGear, _stack);
    }
}
