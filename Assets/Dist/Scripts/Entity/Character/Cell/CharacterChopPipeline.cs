// ============================================================
// CharacterChopPipeline — Chop cue 맵 Occupied HP + TryChop
// ============================================================

using IsoTilemap;
using UnityEngine;

public sealed class CharacterChopPipeline : CharacterStructureTargetPipeline<ChopPlantTarget>
{
    protected override TileDefinition ResolveDefinition(in ChopPlantTarget target) =>
        target.Definition;

    protected override bool IsBlocked(in ChopPlantTarget target) =>
        MapPlantService.GetChopBlockedReason(target.Cell) != null;

    protected override bool IsSameTarget(in ChopPlantTarget current, in ChopPlantTarget next) =>
        current.Cell == next.Cell;

    protected override int ResolveMaxHp(in ChopPlantTarget target)
    {
        int maxHp = target.Definition != null
            ? TileDefinitionCombat.BreakDurability(target.Definition)
            : MapPlantConsts.DefaultChopDurability;
        return maxHp > 0 ? maxHp : MapPlantConsts.DefaultChopDurability;
    }

    protected override int InitRemainingHp(in ChopPlantTarget target, int maxHp)
    {
        MapDigColumnHost host = MapDigColumnHost.Runtime;
        return host != null
            ? host.GetOrInitOccupiedHp(target.Cell, maxHp)
            : maxHp;
    }

    protected override bool TryApplyMapDamage(
        MapDigColumnHost host,
        in ChopPlantTarget target,
        int damage,
        out int remaining) =>
        host.ApplyOccupiedDamage(target.Cell, damage, out remaining);

    protected override void BreakTarget(in ChopPlantTarget target)
    {
        Vector3Int cell = target.Cell;
        MapPlantService.TryChop(cell);
        MapDigColumnHost.Runtime?.ClearOccupiedHp(cell);
    }
}
