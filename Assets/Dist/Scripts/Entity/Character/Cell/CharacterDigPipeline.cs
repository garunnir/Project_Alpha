// ============================================================
// CharacterDigPipeline — Dig cue 맵 FloorFace HP + highlight + TryBreak
// ============================================================

using System;
using IsoTilemap;
using UnityEngine;

public sealed class CharacterDigPipeline : CharacterStructureTargetPipeline<DigTileTarget>
{
    Guid _highlightTileId = Guid.Empty;

    protected override TileDefinition ResolveDefinition(in DigTileTarget target) =>
        target.Definition;

    protected override bool IsBlocked(in DigTileTarget target) =>
        MapDigService.GetBlockedReason(target) != null;

    protected override bool IsSameTarget(in DigTileTarget current, in DigTileTarget next) =>
        current.WalkableCell == next.WalkableCell;

    protected override int ResolveMaxHp(in DigTileTarget target) =>
        TileDefinitionCombat.BreakDurability(target.Definition);

    protected override int InitRemainingHp(in DigTileTarget target, int maxHp)
    {
        MapDigColumnHost host = MapDigColumnHost.Runtime;
        return host != null
            ? host.GetOrInitFloorFaceHp(target.WalkableCell, maxHp)
            : maxHp;
    }

    protected override bool TryApplyMapDamage(
        MapDigColumnHost host,
        in DigTileTarget target,
        int damage,
        out int remaining) =>
        host.ApplyFloorFaceDamage(target.WalkableCell, damage, out remaining);

    protected override void BreakTarget(in DigTileTarget target) =>
        MapDigService.TryBreak(target);

    protected override void OnTargetBegun(in DigTileTarget target) =>
        SetHighlight(target.FaceTile.tileDefId);

    protected override void OnCleared() =>
        ClearHighlight();

    void SetHighlight(Guid presentationTileId)
    {
        if (presentationTileId == Guid.Empty)
        {
            ClearHighlight();
            return;
        }

        if (_highlightTileId == presentationTileId)
            return;

        ClearHighlight();
        _highlightTileId = presentationTileId;
        TilePresentationSystem.Instance?.SetDigHighlight(presentationTileId, true);
    }

    void ClearHighlight()
    {
        if (_highlightTileId == Guid.Empty)
            return;

        TilePresentationSystem.Instance?.SetDigHighlight(_highlightTileId, false);
        _highlightTileId = Guid.Empty;
    }
}
