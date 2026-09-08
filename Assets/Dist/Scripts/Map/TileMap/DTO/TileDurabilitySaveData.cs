// ============================================================
// TileDurabilitySaveData — 맵 타일 remaining HP (풀 HP는 생략)
// ============================================================

using System;

namespace IsoTilemap
{
    /// <summary>0 = HorizontalFace(walkable), 1 = OccupiedCell/Wall.</summary>
    public static class TileDurabilityKind
    {
        public const byte FloorFace = 0;
        public const byte Occupied = 1;
    }

    [Serializable]
    public class TileDurabilitySaveData
    {
        public int x;
        public int y;
        public int z;
        public byte kind;
        public int remainingHp;
    }
}
