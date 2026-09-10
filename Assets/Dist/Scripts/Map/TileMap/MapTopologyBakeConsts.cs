// ============================================================
// MapTopologyBakeConsts — topology defer·dig 증분 bake SSOT
// ============================================================

using System.Collections.Generic;

namespace IsoTilemap
{
    /// <summary>런타임 dig/chop 등 국소 topology 변경 bake 상한.</summary>
    public static class MapTopologyBakeConsts
    {
        public const int MaxDigIncrementalCells = 16;

        public static bool CanUseDigIncrementalBake(int changedCellCount, IReadOnlyList<TileData> removals)
        {
            if (changedCellCount <= 0 || changedCellCount > MaxDigIncrementalCells)
                return false;
            if (removals == null || removals.Count == 0)
                return false;

            for (int i = 0; i < removals.Count; i++)
            {
                TilePlacementSlot slot = TileIdentityUtil.GetPlacementSlot(removals[i].identity);
                if (slot != TilePlacementSlot.HorizontalFace &&
                    slot != TilePlacementSlot.OccupiedCell)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
