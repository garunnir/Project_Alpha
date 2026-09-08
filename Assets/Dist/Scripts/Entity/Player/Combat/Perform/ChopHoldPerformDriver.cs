// ============================================================
// ChopHoldPerformDriver — MeleePlant hold → Chop pipeline + TryPerform(Chop)
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;

/// <summary>
/// ResolveMode.MeleePlant. 타겟은 AimWorldPoint → ChopPlantTargetResolver.
/// </summary>
public sealed class ChopHoldPerformDriver : MeleeStructureHoldPerformDriver
{
    protected override WeaponResolveMode Mode => WeaponResolveMode.MeleePlant;
    protected override CombatLeaf PerformLeaf => CombatLeaf.Chop;

    protected override bool TryBeginTargetFromAim(in CombatPerformContext ctx)
    {
        if (ctx.Host == null || ctx.CharacterState == null || !ctx.CharacterState.IsAiming)
            return false;
        if (!ctx.Host.TryResolveChopTargetFromAim(out ChopPlantTarget target))
            return false;
        return ctx.ActionHost.ChopPipeline.TryBeginTarget(target);
    }

    protected override void ClearPipeline(in CombatPerformContext ctx) =>
        ctx.ActionHost?.ChopPipeline?.Clear();
}
