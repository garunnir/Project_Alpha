// ============================================================
// RangedTriggerPerformDriver — AimClick + TryPerformSelected (Semi/Burst)
// ============================================================

using Garunnir.Runtime.Gameplay.Data;

/// <summary>Semi / Burst — LMB click. Auto는 <see cref="AutoHoldPerformDriver"/>.</summary>
public sealed class RangedTriggerPerformDriver : ICombatPerformDriver
{
    readonly ICombatAttackInput _input = AimClickAttackInput.Shared;

    public bool MatchesLeaf(CombatLeaf leaf)
    {
        CombatLeaf normalized = CombatLeafUtil.Normalize(leaf);
        return normalized == CombatLeaf.Semi || normalized == CombatLeaf.Burst;
    }

    public bool TryOnAttackPerformed(in CombatPerformContext ctx)
    {
        if (ctx.Attacker == null || !MatchesLeaf(ctx.Attacker.SelectedLeaf))
            return false;
        if (!_input.ShouldFire(in ctx))
            return false;

        CharacterAttacker attacker = ctx.Attacker;
        if (ctx.ActionHost != null)
        {
            ctx.ActionHost.TryRunOrEnqueue(CharacterActionKind.Combat, () => Execute(attacker));
            return true;
        }

        Execute(attacker);
        return true;
    }

    public void Tick(in CombatPerformContext ctx) { }

    public void Clear(in CombatPerformContext ctx) { }

    static bool Execute(CharacterAttacker attacker)
    {
        if (attacker == null)
            return false;
        attacker.TryPerformSelected(null);
        return attacker.IsActionBusy;
    }
}
