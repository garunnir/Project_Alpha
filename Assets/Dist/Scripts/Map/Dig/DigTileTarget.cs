// ============================================================
// DigTileTarget — 굴착 대상 (HorizontalFace 또는 walkable 지형 블록)
// ============================================================

using System;
using UnityEngine;

namespace IsoTilemap
{
    public readonly struct DigTileTarget
    {
        public readonly Vector3Int WalkableCell;
        public readonly DigBreakKind BreakKind;
        public readonly TileData TargetTile;
        public readonly TileDefinition Definition;

        public DigTileTarget(
            Vector3Int walkableCell,
            DigBreakKind breakKind,
            TileData targetTile,
            TileDefinition definition)
        {
            WalkableCell = walkableCell;
            BreakKind = breakKind;
            TargetTile = targetTile;
            Definition = definition;
        }

        /// <summary>하이라이트·크랙용 타일 id.</summary>
        public Guid PresentationTileId => TargetTile.tileDefId;

        /// <summary><see cref="DigBreakKind.WalkableStratumBlock"/> 앵커 셀 (walkable + down).</summary>
        public Vector3Int BlockAnchorCell =>
            BreakKind == DigBreakKind.WalkableStratumBlock
                ? MapDigTerrainUtil.SupportBlockAnchor(WalkableCell)
                : default;

        /// <summary>레거시 호환 — face 타겟만 유효.</summary>
        public TileData FaceTile => TargetTile;
    }
}
