// ============================================================
// CraftActionSource — UICraftingController 어댑터
// ============================================================

public sealed class CraftActionSource : ICharacterActionSource
{
    readonly UICraftingController _crafting;

    public CraftActionSource(UICraftingController crafting) =>
        _crafting = crafting;

    public CharacterActionKind Kind => CharacterActionKind.Craft;

    public bool IsBusy => _crafting != null && _crafting.IsCraftRunning;

    public float Progress01 => _crafting != null ? _crafting.CraftProgress01 : 0f;

    public void CancelSoft() => _crafting?.CancelRunningCraft();

    public void InterruptHard(CharacterInterruptReason reason) => CancelSoft();

    public void ResetKnockdownLatch() { }
}
