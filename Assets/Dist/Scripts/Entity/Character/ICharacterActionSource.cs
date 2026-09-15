// ============================================================
// ICharacterActionSource — 행동 큐 소스별 busy/progress/cancel 계약
// ============================================================

public enum CharacterInterruptReason
{
    UserCancel,
    Knockdown,
}

public interface ICharacterActionSource
{
    CharacterActionKind Kind { get; }
    bool IsBusy { get; }
    float Progress01 { get; }
    void CancelSoft();
    void InterruptHard(CharacterInterruptReason reason);
    void ResetKnockdownLatch();
}
