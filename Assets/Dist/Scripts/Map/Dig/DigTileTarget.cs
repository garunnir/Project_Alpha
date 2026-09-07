// ============================================================
// DigTileTarget — 굴착 대상 HorizontalFace (walkable + face tile)
// ============================================================

using UnityEngine;

namespace IsoTilemap
{
    public readonly struct DigTileTarget
    {
        public readonly Vector3Int WalkableCell;
        public readonly TileData FaceTile;
        public readonly TileDefinition Definition;

        public DigTileTarget(Vector3Int walkableCell, TileData faceTile, TileDefinition definition)
        {
            WalkableCell = walkableCell;
            FaceTile = faceTile;
            Definition = definition;
        }
    }
}
