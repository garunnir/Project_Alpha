// ============================================================
// BakeTileFactory — Dist.Map BakeIdSyntheticTiles 위임 (테스트 호환)
// ============================================================
using IsoTilemap;
using UnityEngine;

namespace IsoTilemap.Tests
{
    public static class BakeTileFactory
    {
        public const string FloorPrefabId = BakeIdSyntheticTiles.FloorPrefabId;
        public const string ThinWallPrefabId = BakeIdSyntheticTiles.ThinWallPrefabIdPosX;
        public const string CubePrefabId = BakeIdSyntheticTiles.CubePrefabId;

        public static TileData Floor(int x, int y, int z) => BakeIdSyntheticTiles.Floor(x, y, z);
        public static TileData Floor(Vector3Int walkableCell) => BakeIdSyntheticTiles.Floor(walkableCell);
        public static TileData ThinWall(Vector3Int cellA, Vector3Int cellB) =>
            BakeIdSyntheticTiles.ThinWall(cellA, cellB);
        public static TileData ThinWall(int ax, int ay, int az, int bx, int by, int bz) =>
            BakeIdSyntheticTiles.ThinWall(ax, ay, az, bx, by, bz);
        public static TileData Cube(int x, int y, int z) => BakeIdSyntheticTiles.Cube(x, y, z);
        public static TileData Cube(Vector3Int cell) => BakeIdSyntheticTiles.Cube(cell);
        public static bool IsThinWallBetween(in TileData tile, Vector3Int cellA, Vector3Int cellB) =>
            BakeIdSyntheticTiles.IsThinWallBetween(tile, cellA, cellB);
    }
}
