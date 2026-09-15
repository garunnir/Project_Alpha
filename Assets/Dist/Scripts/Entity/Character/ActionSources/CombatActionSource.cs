// ============================================================
// CombatActionSource — CharacterAttacker 동작 busy 어댑터 (무기 쿨 제외)
// ============================================================

public sealed class CombatActionSource : ICharacterActionSource
{
    readonly CharacterAttacker _attacker;

    public CombatActionSource(CharacterAttacker attacker) =>
        _attacker = attacker;

    public CharacterActionKind Kind => CharacterActionKind.Combat;

    public bool IsBusy => _attacker != null && _attacker.IsActionBusy;

    public float Progress01 => _attacker != null ? _attacker.ActionPerformProgress01 : 0f;

    /// <summary>TryRunImmediate 선점. ESC CancelAll은 Combat을 건너뛰어 호출하지 않음.</summary>
    public void CancelSoft() => _attacker?.ClearActionBusy();

    public void InterruptHard(CharacterInterruptReason reason)
    {
        if (reason != CharacterInterruptReason.Knockdown || _attacker == null)
            return;

        _attacker.CancelAllPendingCues();
        _attacker.ClearInterruptBusy();
    }

    public void ResetKnockdownLatch() { }
}
