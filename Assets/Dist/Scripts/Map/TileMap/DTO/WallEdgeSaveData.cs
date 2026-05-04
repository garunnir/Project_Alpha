using System;

namespace IsoTilemap
{
    [Serializable]
    public class WallEdgeSaveData
    {
        public int x;
        public int y;
        public int z;
        /// <summary>0 = +X 면, 1 = +Z 면 (앵커 셀 기준).</summary>
        public byte face;
        public string prefabId;
    }
}
