// ============================================================
// GearActionSource — Wear/Wield/도구 세션 소스 어댑터
// ============================================================

public sealed class GearActionSource : ICharacterActionSource
{
    readonly PlayerGearHost _gearHost;

    bool _knockdownDropLatched;

    public GearActionSource(PlayerGearHost gearHost) =>
        _gearHost = gearHost;

    public CharacterActionKind Kind => CharacterActionKind.Gear;

    CharacterGearService Service => _gearHost != null ? _gearHost.Service : null;

    public bool IsBusy => Service != null && Service.IsBusy;

    public float Progress01 =>
        _gearHost?.Timed != null ? _gearHost.Timed.Progress01 : 0f;

    public void CancelSoft() => _gearHost?.Timed?.Cancel();

    public void InterruptHard(CharacterInterruptReason reason)
    {
        CharacterGearService service = Service;
        service?.TryEndToolUse();
        _gearHost?.Timed?.Cancel();

        if (reason != CharacterInterruptReason.Knockdown || _knockdownDropLatched)
            return;

        _knockdownDropLatched = true;
        _gearHost?.DropAllWieldedToWorld();
    }

    public void ResetKnockdownLatch() => _knockdownDropLatched = false;
}
