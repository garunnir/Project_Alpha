// ============================================================
// CombatActionSource — CharacterAttacker 큐/쿨 어댑터
// ============================================================

public sealed class CombatActionSource : ICharacterActionSource
{
    readonly CharacterAttacker _attacker;

    public CombatActionSource(CharacterAttacker attacker) =>
        _attacker = attacker;

    public CharacterActionKind Kind => CharacterActionKind.Combat;

    public bool IsBusy => _attacker != null && _attacker.IsActionBusy;

    public float Progress01 => _attacker != null ? _attacker.CooldownProgress01 : 0f;

    public void CancelSoft()
    {
        // ESC parity: CancelAll skips combat current work.
    }

    public void InterruptHard(CharacterInterruptReason reason)
    {
        if (reason != CharacterInterruptReason.Knockdown || _attacker == null)
            return;

        _attacker.CancelAllPendingCues();
        _attacker.ClearInterruptBusy();
    }

    public void ResetKnockdownLatch() { }
}
