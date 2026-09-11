// ============================================================
// [BakeIdPlaygroundLayout] — Sim 씬 Import용 시드 배치 (코드 fixture)
// ============================================================
// Bake ID 회귀 SSOT는 BakeIdPlayground 씬 Host 직렬화. 이 파일은 빈 씬 시드용.
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// Host <c>Import From Seed Layout</c>용 기본 배치·probe 좌표.
    /// </summary>
    public static class BakeIdPlaygroundLayout
    {
        public static class MasterPlayground
        {
            // --- Enclosed two-room house (x=0..3, z=10..12, floorY=1, roofY=2) ---
            // Left room floors (same building/space when sealed)
            public static readonly Vector3Int OpenPair_A = new Vector3Int(0, 1, 10);
            public static readonly Vector3Int OpenPair_B = new Vector3Int(1, 1, 10);
            // Across internal SlimWall (x=1|2)
            public static readonly Vector3Int ThinWall_Left = new Vector3Int(1, 1, 11);
            public static readonly Vector3Int ThinWall_Right = new Vector3Int(2, 1, 11);

            // --- Open shed (walls, no roof → outdoor space) ---
            public static readonly Vector3Int OpenShed_A = new Vector3Int(6, 1, 10);
            public static readonly Vector3Int OpenShed_B = new Vector3Int(7, 1, 10);

            // CubeWallSplit — floors with cube gap, isolated at z=20
            public static readonly Vector3Int CubeWall_Left = new Vector3Int(0, 1, 20);
            public static readonly Vector3Int CubeWall_Right = new Vector3Int(2, 1, 20);
            public static readonly Vector3Int CubeWall_Cube = new Vector3Int(1, 1, 20);

            // BridgeGap — z=20 x=4..8
            public static readonly Vector3Int Bridge_B1 = new Vector3Int(4, 1, 20);
            public static readonly Vector3Int Bridge_B1b = new Vector3Int(5, 1, 20);
            public static readonly Vector3Int Bridge_GapCell = new Vector3Int(6, 1, 20);
            public static readonly Vector3Int Bridge_B2 = new Vector3Int(7, 1, 20);
            public static readonly Vector3Int Bridge_B2b = new Vector3Int(8, 1, 20);

            // ColumnStack — isolated pad (space column +Y probe), z=25
            public static readonly Vector3Int Column_HFloor_Lower = new Vector3Int(0, 1, 25);
            public static readonly Vector3Int Column_HFloor_Upper = new Vector3Int(0, 2, 25);

            // GShape ㄱ + balcony — east of house, z=10..12
            public static readonly Vector3Int GShape_A = new Vector3Int(4, 1, 10);
            public static readonly Vector3Int GShape_B = new Vector3Int(5, 1, 10);
            public static readonly Vector3Int GShape_C = new Vector3Int(5, 1, 11);
            public static readonly Vector3Int Balcony_Floor = new Vector3Int(5, 1, 12);

            public static readonly Vector3Int Plaza_Floor = new Vector3Int(0, 0, 0);
            public static readonly Vector3Int Dig_Floor = new Vector3Int(9, 1, 20);

            // Indoor probe (left room center) / outdoor shed probe
            public static readonly Vector3Int IndoorProbe = new Vector3Int(0, 1, 11);
            public static readonly Vector3Int OutdoorShedProbe = OpenShed_A;

            static readonly IReadOnlyList<TileData> s_tiles = BuildTiles();

            public static IReadOnlyList<TileData> Tiles => s_tiles;

            public static TileData MakeBridgeFloorTile() => BakeIdSyntheticTiles.Floor(Bridge_GapCell);

            public static TileData MakeOpenPairSeparatorWall() =>
                BakeIdSyntheticTiles.ThinWall(OpenPair_A, OpenPair_B);

            public static bool IsThinWallSplitTile(TileData tile) =>
                BakeIdSyntheticTiles.IsThinWallBetween(tile, ThinWall_Left, ThinWall_Right);

            public static bool IsCubeWallSplitTile(TileData tile) =>
                TileIdentityUtil.IsOccupiedCell(tile.identity) &&
                tile.identity.GridPos == CubeWall_Cube &&
                tile.identity.PrefabId == BakeIdSyntheticTiles.CubePrefabId;

            public static bool IsDigFloorTile(TileData tile) =>
                TileIdentityUtil.IsHorizontalFace(tile.identity) &&
                tile.identity.GridPos == Dig_Floor;

            static IReadOnlyList<TileData> BuildTiles()
            {
                var tiles = new List<TileData>(128);

                // Plaza — y=0 outdoor ground
                AddFloorRect(tiles, x0: 0, x1: 10, z0: 0, z1: 2, y: 0);

                // Enclosed two-room house
                AddFloorRect(tiles, x0: 0, x1: 3, z0: 10, z1: 12, y: 1);
                AddSlimPerimeter(tiles, x0: 0, x1: 3, z0: 10, z1: 12, y: 1);
                // Internal divider (full depth) — splits left/right rooms
                for (int z = 10; z <= 12; z++)
                    tiles.Add(BakeIdSyntheticTiles.ThinWall(
                        new Vector3Int(1, 1, z), new Vector3Int(2, 1, z)));
                // Roof shell (structural, not walkable) — ceiling seal for indoor space
                AddCubeRect(tiles, x0: 0, x1: 3, z0: 10, z1: 12, y: 2);

                // Open shed — perimeter walls, no roof → outdoor space
                AddFloorRect(tiles, x0: 6, x1: 7, z0: 10, z1: 11, y: 1);
                AddSlimPerimeter(tiles, x0: 6, x1: 7, z0: 10, z1: 11, y: 1);

                // CubeWallSplit
                tiles.Add(BakeIdSyntheticTiles.Floor(CubeWall_Left));
                tiles.Add(BakeIdSyntheticTiles.Floor(CubeWall_Right));
                tiles.Add(BakeIdSyntheticTiles.Cube(CubeWall_Cube));

                // BridgeGap
                tiles.Add(BakeIdSyntheticTiles.Floor(Bridge_B1));
                tiles.Add(BakeIdSyntheticTiles.Floor(Bridge_B1b));
                tiles.Add(BakeIdSyntheticTiles.Floor(Bridge_B2));
                tiles.Add(BakeIdSyntheticTiles.Floor(Bridge_B2b));

                // ColumnStack
                tiles.Add(BakeIdSyntheticTiles.Floor(Column_HFloor_Lower));
                tiles.Add(BakeIdSyntheticTiles.Floor(Column_HFloor_Upper));

                // G-shape + balcony (no enclosure — outdoor)
                tiles.Add(BakeIdSyntheticTiles.Floor(GShape_A));
                tiles.Add(BakeIdSyntheticTiles.Floor(GShape_B));
                tiles.Add(BakeIdSyntheticTiles.Floor(GShape_C));
                tiles.Add(BakeIdSyntheticTiles.Floor(Balcony_Floor));

                // DigTarget
                tiles.Add(BakeIdSyntheticTiles.Floor(Dig_Floor));

                return tiles;
            }

            static void AddFloorRect(List<TileData> tiles, int x0, int x1, int z0, int z1, int y)
            {
                for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                    tiles.Add(BakeIdSyntheticTiles.Floor(x, y, z));
            }

            static void AddCubeRect(List<TileData> tiles, int x0, int x1, int z0, int z1, int y)
            {
                for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                    tiles.Add(BakeIdSyntheticTiles.Cube(x, y, z));
            }

            static void AddSlimPerimeter(List<TileData> tiles, int x0, int x1, int z0, int z1, int y)
            {
                for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    var cell = new Vector3Int(x, y, z);
                    TryBoundaryWall(tiles, cell, cell + Vector3Int.left, x0, x1, z0, z1);
                    TryBoundaryWall(tiles, cell, cell + Vector3Int.right, x0, x1, z0, z1);
                    TryBoundaryWall(tiles, cell, cell + Vector3Int.back, x0, x1, z0, z1);
                    TryBoundaryWall(tiles, cell, cell + Vector3Int.forward, x0, x1, z0, z1);
                }
            }

            static void TryBoundaryWall(
                List<TileData> tiles,
                Vector3Int cell,
                Vector3Int neighbor,
                int x0, int x1, int z0, int z1)
            {
                if (neighbor.x >= x0 && neighbor.x <= x1 &&
                    neighbor.z >= z0 && neighbor.z <= z1)
                    return;

                tiles.Add(BakeIdSyntheticTiles.ThinWall(cell, neighbor));
            }
        }
    }
}
