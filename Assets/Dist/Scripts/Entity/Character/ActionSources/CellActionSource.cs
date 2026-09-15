// ============================================================
// CellActionSource — 셀/도착/vault/dig/chop 소스 어댑터
// ============================================================

public sealed class CellActionSource : ICharacterActionSource
{
    readonly CharacterCellFarmPipeline _farm;
    readonly CharacterCellFishPipeline _fish;
    readonly CharacterCellConstructionPipeline _construction;
    readonly CharacterArriveHost _arriveHost;
    readonly CharacterVaultHost _vaultHost;
    readonly CharacterDigPipeline _dig;
    readonly CharacterChopPipeline _chop;

    public CellActionSource(
        CharacterCellFarmPipeline farm,
        CharacterCellFishPipeline fish,
        CharacterCellConstructionPipeline construction,
        CharacterArriveHost arriveHost,
        CharacterVaultHost vaultHost,
        CharacterDigPipeline dig,
        CharacterChopPipeline chop)
    {
        _farm = farm;
        _fish = fish;
        _construction = construction;
        _arriveHost = arriveHost;
        _vaultHost = vaultHost;
        _dig = dig;
        _chop = chop;
    }

    public CharacterActionKind Kind => CharacterActionKind.Cell;

    public bool IsBusy =>
        (_farm != null && _farm.IsBusy) ||
        (_fish != null && _fish.IsBusy) ||
        (_construction != null && _construction.IsBusy) ||
        (_arriveHost != null && _arriveHost.IsBusy) ||
        (_vaultHost != null && _vaultHost.IsBusy);

    public float Progress01
    {
        get
        {
            if (_vaultHost != null && _vaultHost.IsBusy)
                return _vaultHost.Progress01;
            if (_construction != null && _construction.IsBusy)
                return _construction.WorkProgress01;
            if (_fish != null && _fish.IsBusy)
                return _fish.WorkProgress01;
            return _farm != null ? _farm.WorkProgress01 : 0f;
        }
    }

    public void CancelSoft()
    {
        _farm?.Cancel();
        _fish?.Cancel();
        _construction?.Cancel();
        _arriveHost?.Cancel();
        _vaultHost?.Cancel();
    }

    public void InterruptHard(CharacterInterruptReason reason)
    {
        CancelSoft();
        if (reason != CharacterInterruptReason.Knockdown)
            return;

        _dig?.Clear();
        _chop?.Clear();
    }

    public void ResetKnockdownLatch() { }
}
