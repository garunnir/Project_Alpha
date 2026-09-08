// ============================================================
// MeleeBlockAimPreview — Excavate RMB → Dig face SetDigHighlight
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;

/// <summary>
/// ResolveMode.MeleeBlock. AimWorldPoint → DigTileTarget (clamp) + CanBreak → face highlight.
/// UIAimPointer 링 아님.
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

        if (!ctx.Attacker.TryPreviewMeleeBlockTarget(ctx.Host, out DigTileTarget target))
        {
            ClearPreview();
            return;
        }

        System.Guid tileId = target.FaceTile.tileDefId;
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
