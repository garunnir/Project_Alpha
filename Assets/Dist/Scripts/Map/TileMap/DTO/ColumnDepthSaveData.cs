// ============================================================
// ColumnDepthSaveData — (x,z) dig-break 횟수 직렬화
// ============================================================

using System;

namespace IsoTilemap
{
    [Serializable]
    public class ColumnDepthSaveData
    {
        public int x;
        public int z;
        public int depth;
    }
}
