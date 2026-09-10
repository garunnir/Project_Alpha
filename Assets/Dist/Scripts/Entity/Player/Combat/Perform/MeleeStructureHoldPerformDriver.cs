// ============================================================
// MeleeStructureHoldPerformDriver — AimHold + 구조물 타겟 잠금 (Layer2)
// ============================================================

using Garunnir.Runtime.Gameplay.Data;

/// <summary>
/// Dig/Chop 공용 hold 루프. 조작은 <see cref="AimHoldAttackInput"/>.
/// LMB down/재획득: aim Resolve → 잠금. hold 중 잠금 유지. release: Clear.
/// 파괴로 inactive면 같은 hold에서 aim으로 재고정(A).
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

        if (!TryEnsureLockedTarget(in ctx))
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

    /// <summary>이미 잠금이면 유지. 없으면 aim으로 begin(파괴 후 재고정 포함).</summary>
    protected bool TryEnsureLockedTarget(in CombatPerformContext ctx)
    {
        if (IsPipelineActive(in ctx))
            return true;
        return TryBeginTargetFromAim(in ctx);
    }

    protected abstract bool IsPipelineActive(in CombatPerformContext ctx);

    protected abstract bool TryBeginTargetFromAim(in CombatPerformContext ctx);

    protected abstract void ClearPipeline(in CombatPerformContext ctx);
}
