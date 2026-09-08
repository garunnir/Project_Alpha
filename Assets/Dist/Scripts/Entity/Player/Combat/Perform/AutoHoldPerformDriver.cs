// ============================================================
// AutoHoldPerformDriver — AimHold + TryPerform(Auto) 연사 (Layer2)
// ============================================================

using Garunnir.Runtime.Gameplay.Data;

/// <summary>
/// Trigger Family Leaf지만 입력은 Dig/Chop과 같은 hold.
/// 쿨/pending 비는 동안 Auto 재시전 (클릭 볼리 아님).
/// </summary>
public sealed class AutoHoldPerformDriver : ICombatPerformDriver
{
    readonly ICombatAttackInput _input = AimHoldAttackInput.Shared;

    public bool MatchesLeaf(CombatLeaf leaf) =>
        CombatLeafUtil.Normalize(leaf) == CombatLeaf.Auto;

    public bool TryOnAttackPerformed(in CombatPerformContext ctx) => false;

    public void Tick(in CombatPerformContext ctx)
    {
        if (ctx.Attacker == null)
            return;

        if (!MatchesLeaf(ctx.Attacker.SelectedLeaf))
            return;

        if (!_input.ShouldFire(in ctx))
            return;

        if (ctx.Attacker.IsActionBusy || !ctx.Attacker.CanPerform(CombatLeaf.Auto))
            return;

        ctx.Attacker.TryPerform(CombatLeaf.Auto, null);
    }

    public void Clear(in CombatPerformContext ctx) { }
}
