// ============================================================
// ExcavateHoldPerformDriver — MeleeBlock hold → Dig pipeline + TryPerform(Excavate)
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;

/// <summary>
/// ResolveMode.MeleeBlock. 타겟은 AimWorldPoint → DigTileTargetResolver.
/// </summary>
public sealed class ExcavateHoldPerformDriver : MeleeStructureHoldPerformDriver
{
    protected override WeaponResolveMode Mode => WeaponResolveMode.MeleeBlock;
    protected override CombatLeaf PerformLeaf => CombatLeaf.Excavate;

    protected override bool TryBeginTargetFromAim(in CombatPerformContext ctx)
    {
        if (ctx.Host == null || ctx.CharacterState == null || !ctx.CharacterState.IsAiming)
            return false;
        if (!ctx.Host.TryResolveDigTargetFromAim(out DigTileTarget target))
            return false;
        return ctx.ActionHost.DigPipeline.TryBeginTarget(target);
    }

    protected override void ClearPipeline(in CombatPerformContext ctx) =>
        ctx.ActionHost?.DigPipeline?.Clear();
}
