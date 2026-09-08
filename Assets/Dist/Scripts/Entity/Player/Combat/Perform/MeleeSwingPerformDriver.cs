// ============================================================
// MeleeSwingPerformDriver — AimClick + TryPerformSelected (MeleeReach)
// ============================================================

using Garunnir.Runtime.Gameplay.Data;

/// <summary>Strike / Pierce 등 ResolveMode.MeleeReach. Raise는 Attacker TickRaiseGuard.</summary>
public sealed class MeleeSwingPerformDriver : ICombatPerformDriver
{
    readonly ICombatAttackInput _input = AimClickAttackInput.Shared;

    public bool MatchesLeaf(CombatLeaf leaf) =>
        CombatLeafUtil.ResolveMode(leaf) == WeaponResolveMode.MeleeReach;

    public bool TryOnAttackPerformed(in CombatPerformContext ctx)
    {
        if (!MatchesLeaf(ctx.Attacker != null ? ctx.Attacker.SelectedLeaf : default))
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
