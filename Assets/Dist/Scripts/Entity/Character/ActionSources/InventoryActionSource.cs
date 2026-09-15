// ============================================================
// InventoryActionSource — InventoryTimedMoveHost 어댑터
// ============================================================

public sealed class InventoryActionSource : ICharacterActionSource
{
    readonly InventoryTimedMoveHost _moveHost;

    public InventoryActionSource(InventoryTimedMoveHost moveHost) =>
        _moveHost = moveHost;

    public CharacterActionKind Kind => CharacterActionKind.Inventory;

    public bool IsBusy => _moveHost != null && _moveHost.IsBusy;

    public float Progress01 => _moveHost != null ? _moveHost.Progress01 : 0f;

    public void CancelSoft() => _moveHost?.Cancel();

    public void InterruptHard(CharacterInterruptReason reason) => CancelSoft();

    public void ResetKnockdownLatch() { }
}
