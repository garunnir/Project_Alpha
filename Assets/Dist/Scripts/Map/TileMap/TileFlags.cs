// ============================================================
// TileFlags — TileDefinition gameplay string-flag SSOT (BN-style)
// ============================================================
using System;
using System.Collections.Generic;

namespace IsoTilemap
{
    public static class TileFlags
    {
        public const string Plantable = "PLANTABLE";
        public const string Plowable = "PLOWABLE";
        public const string Diggable = "DIGGABLE";
        public const string Mineable = "MINEABLE";
        public const string Plant = "PLANT";
        public const string GrowthSeed = "GROWTH_SEED";
        public const string GrowthSeedling = "GROWTH_SEEDLING";
        public const string GrowthMature = "GROWTH_MATURE";
        public const string GrowthHarvest = "GROWTH_HARVEST";
        public const string GrowthWithered = "GROWTH_WITHERED";
        public const string Water = "WATER";
        public const string ShallowWater = "SHALLOW_WATER";
        public const string DeepWater = "DEEP_WATER";
        public const string Fishable = "FISHABLE";

        /// <summary>저작·bake 대상 액체 타일 — <see cref="Water"/> 또는 레거시 shallow/deep.</summary>
        public static bool IsLiquidAuthoring(TileDefinition definition) =>
            HasFlag(definition, Water)
            || HasFlag(definition, ShallowWater)
            || HasFlag(definition, DeepWater);

        /// <summary>MINEABLE 또는 DIGGABLE — 채굴·굴착 대상 바닥재.</summary>
        public static bool IsDiggableTarget(TileDefinition definition) =>
            HasFlag(definition, Mineable) || HasFlag(definition, Diggable);

        public static bool HasFlag(TileDefinition definition, string flag)
        {
            if (definition == null || definition.flags == null || string.IsNullOrEmpty(flag))
                return false;

            List<string> flags = definition.flags;
            for (int i = 0; i < flags.Count; i++)
            {
                string value = flags[i];
                if (!string.IsNullOrEmpty(value) &&
                    value.Equals(flag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
