// ============================================================
// CharacterDigPipeline — Dig cue 맵 HP + TryBreak (face / stratum block)
// ============================================================

using IsoTilemap;

public sealed class CharacterDigPipeline : CharacterStructureTargetPipeline<DigTileTarget>
{
    protected override TileDefinition ResolveDefinition(in DigTileTarget target) =>
        target.Definition;

    protected override bool IsBlocked(in DigTileTarget target) =>
        MapDigService.GetBlockedReason(target) != null;

    protected override bool IsSameTarget(in DigTileTarget current, in DigTileTarget next) =>
        current.WalkableCell == next.WalkableCell &&
        current.BreakKind == next.BreakKind;

    protected override int ResolveMaxHp(in DigTileTarget target) =>
        TileDefinitionCombat.BreakDurability(target.Definition);

    protected override int InitRemainingHp(in DigTileTarget target, int maxHp)
    {
        MapDigColumnHost host = MapDigColumnHost.Runtime;
        if (host == null)
            return maxHp;

        return target.BreakKind == DigBreakKind.WalkableStratumBlock
            ? host.GetOrInitOccupiedHp(target.BlockAnchorCell, maxHp)
            : host.GetOrInitFloorFaceHp(target.WalkableCell, maxHp);
    }

    protected override bool TryApplyMapDamage(
        MapDigColumnHost host,
        in DigTileTarget target,
        int damage,
        out int remaining)
    {
        if (target.BreakKind == DigBreakKind.WalkableStratumBlock)
            return host.ApplyOccupiedDamage(target.BlockAnchorCell, damage, out remaining);

        return host.ApplyFloorFaceDamage(target.WalkableCell, damage, out remaining);
    }

    /// <summary>
    /// remaining/max → stage → TileView 직행 (Resolve 합성·맵 리빌드 없음).
    /// </summary>
    protected override void OnMapHpChanged(in DigTileTarget target, int remaining, int maxHp) =>
        TileDamagePresentation.ApplyHpToView(in target, remaining, maxHp);

    protected override void BreakTarget(in DigTileTarget target) =>
        MapDigService.TryBreak(target);
}
