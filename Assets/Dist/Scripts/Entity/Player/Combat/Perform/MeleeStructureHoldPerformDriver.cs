// ============================================================
// MeleeStructureHoldPerformDriver — AimHold + 구조물 타겟 (Layer2)
// ============================================================

using Garunnir.Runtime.Gameplay.Data;

/// <summary>
/// Dig/Chop 공용 hold 루프. 조작은 <see cref="AimHoldAttackInput"/>,
/// Leaf·ResolveMode·타겟 begin은 파생.
/// </summary>
public abstract class MeleeStructureHoldPerformDriver : ICombatPerformDriver
{
    readonly ICombatAttackInput _input = AimHoldAttackInput.Shared;

    protected abstract WeaponResolveMode Mode { get; }
    protected abstract CombatLeaf PerformLeaf { get; }

    public bool MatchesLeaf(CombatLeaf leaf) =>
        CombatLeafUtil.ResolveMode(leaf) == Mode;

    public bool TryOnAttackPerformed(in CombatPerformContext ctx) => false;

    public void Tick(in CombatPerformContext ctx)
    {
        if (!ctx.InputEnabled || ctx.Attacker == null || ctx.ActionHost == null)
            return;

        if (!MatchesLeaf(ctx.Attacker.SelectedLeaf))
        {
            Clear(ctx);
            return;
        }

        if (!_input.ShouldFire(in ctx))
        {
            Clear(ctx);
            return;
        }

        if (!TryBeginTargetFromAim(ctx))
        {
            Clear(ctx);
            return;
        }

        if (ctx.Attacker.IsActionBusy || !ctx.Attacker.CanPerform(PerformLeaf))
            return;

        ctx.Attacker.TryPerform(PerformLeaf, null);
    }

    public void Clear(in CombatPerformContext ctx) =>
        ClearPipeline(ctx);

    protected abstract bool TryBeginTargetFromAim(in CombatPerformContext ctx);

    protected abstract void ClearPipeline(in CombatPerformContext ctx);
}
