// ============================================================
// TileDefinitionCombat — TileDefinition 파괴·재질 combat 필드 SSOT
// ============================================================

namespace IsoTilemap
{
    public static class TileDefinitionCombat
    {
        public static int BreakDurability(TileDefinition definition) =>
            definition != null && definition.breakDurability > 0
                ? definition.breakDurability
                : MapDigConsts.DefaultDigDurability;

        public static int MaterialThickness(TileDefinition definition) =>
            definition != null ? definition.materialThickness : 0;
    }
}
