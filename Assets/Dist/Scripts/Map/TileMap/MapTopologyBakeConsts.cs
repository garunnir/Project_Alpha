// ============================================================
// MapTopologyBakeConsts — topology defer·국소 bake 상한 SSOT
// ============================================================

namespace IsoTilemap
{
    /// <summary>런타임 dig/chop 등 국소 topology 변경 참고 상한 (증분 경로는 추가/제거와 동일).</summary>
    public static class MapTopologyBakeConsts
    {
        public const int MaxDigIncrementalCells = 16;
    }
}
