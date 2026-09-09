// ============================================================
// StratumGenerator — stratumSeed 기반 결정론적 지층 prefabId 선택
// ============================================================

namespace IsoTilemap
{
    public static class StratumGenerator
    {
        public static string PickStratumPrefabId(
            int stratumSeed,
            int x,
            int z,
            int depth,
            StratumProfile profile)
        {
            if (profile == null)
                return MapDigConsts.DefaultStratumBlockPrefabId;

            return profile.PickPrefabId(stratumSeed, x, z, depth);
        }

        /// <summary>레거시 이름 — <see cref="PickStratumPrefabId"/>.</summary>
        public static string PickFloorPrefabId(
            int stratumSeed,
            int x,
            int z,
            int depth,
            StratumProfile profile) =>
            PickStratumPrefabId(stratumSeed, x, z, depth, profile);

        public static int MixSeed(int stratumSeed, int x, int z, int depth)
        {
            unchecked
            {
                uint hash = (uint)stratumSeed;
                hash = hash * 374761393u + (uint)x;
                hash = hash * 668265263u + (uint)z;
                hash = hash * 2246822519u + (uint)depth;
                hash ^= hash >> 13;
                hash *= 1274126177u;
                hash ^= hash >> 16;
                return (int)(hash & 0x7fffffff);
            }
        }
    }
}
