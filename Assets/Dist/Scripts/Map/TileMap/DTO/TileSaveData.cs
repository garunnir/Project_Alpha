using System;
namespace IsoTilemap
{
    // 한 타일(Anchor 기준)의 순수 데이터 구조
    [Serializable]
    public class TileSaveData
    {
        public int x;
        public int y;
        public int z;

        public int sizeX;
        public int sizeY;
        public int sizeZ;

        public string prefabId;
        public byte tileType;
    }
}