// ============================================================
// MeleeBlockAimPreview — Excavate RMB → Dig face SetDigHighlight
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;

/// <summary>
/// ResolveMode.MeleeBlock. 홀드 잠금 타겟 우선, 없으면 aim Resolve + CanBreak → highlight.
/// </summary>
public sealed class MeleeBlockAimPreview : ICombatTargetingPreview
{
    public bool MatchesLeaf(CombatLeaf leaf) =>
        CombatLeafUtil.ResolveMode(leaf) == WeaponResolveMode.MeleeBlock;

    public void TickPreview(in CombatPerformContext ctx)
    {
        if (!ctx.InputEnabled ||
            ctx.Attacker == null ||
            ctx.CharacterState == null ||
            ctx.Host == null)
        {
            ClearPreview();
            return;
        }

        if (!MatchesLeaf(ctx.Attacker.SelectedLeaf))
        {
            ClearPreview();
            return;
        }

        if (!ctx.CharacterState.IsAiming ||
            ctx.Host.IsAttackBlockedByUi() ||
            CombatAttackInputGates.IsCellTargetOrMenuSuppressed())
        {
            ClearPreview();
            return;
        }

        DigTileTarget target;
        CharacterDigPipeline dig = ctx.ActionHost != null ? ctx.ActionHost.DigPipeline : null;
        if (dig != null && dig.TryGetActiveTarget(out DigTileTarget locked))
            target = locked;
        else if (!ctx.Attacker.TryPreviewMeleeBlockTarget(ctx.Host, out target))
        {
            ClearPreview();
            return;
        }

        System.Guid tileId = target.PresentationTileId;
        if (tileId == System.Guid.Empty)
        {
            ClearPreview();
            return;
        }

        TilePresentationSystem.Instance?.SetDigHighlight(tileId, true);
    }

    public void ClearPreview() =>
        TilePresentationSystem.Instance?.ClearDigHighlight();
}
