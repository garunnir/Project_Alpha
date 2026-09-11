// ============================================================
// [BakeIdPlaygroundLayout] — Sim 씬 Import용 시드 배치 (코드 fixture)
// ============================================================
// Bake ID 회귀 SSOT는 BakeIdPlayground 씬 Host 직렬화. 이 파일은 빈 씬 시드용.
// 클러스터를 서로 비인접으로 두어 structural floor union이 indoor 밀폐 집을 오염하지 않게 한다.
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
            // --- Enclosed two-room house (x=0..3, z=10..12, floorY=1, roofY=2) — isolated ---
            public static readonly Vector3Int OpenPair_A = new Vector3Int(0, 1, 10);
            public static readonly Vector3Int OpenPair_B = new Vector3Int(1, 1, 10);
            public static readonly Vector3Int ThinWall_Left = new Vector3Int(1, 1, 11);
            public static readonly Vector3Int ThinWall_Right = new Vector3Int(2, 1, 11);

            // --- Open shed (walls, no roof → outdoor space), far from house ---
            public static readonly Vector3Int OpenShed_A = new Vector3Int(20, 1, 10);
            public static readonly Vector3Int OpenShed_B = new Vector3Int(21, 1, 10);

            // CubeWallSplit — floors with cube gap
            public static readonly Vector3Int CubeWall_Left = new Vector3Int(0, 1, 20);
            public static readonly Vector3Int CubeWall_Right = new Vector3Int(2, 1, 20);
            public static readonly Vector3Int CubeWall_Cube = new Vector3Int(1, 1, 20);

            // BridgeGap — disconnected pads (gap at x=6)
            public static readonly Vector3Int Bridge_B1 = new Vector3Int(4, 1, 20);
            public static readonly Vector3Int Bridge_B1b = new Vector3Int(5, 1, 20);
            public static readonly Vector3Int Bridge_GapCell = new Vector3Int(6, 1, 20);
            public static readonly Vector3Int Bridge_B2 = new Vector3Int(7, 1, 20);
            public static readonly Vector3Int Bridge_B2b = new Vector3Int(8, 1, 20);

            // ColumnStack — AABB volume same Space across Y
            public static readonly Vector3Int Column_HFloor_Lower = new Vector3Int(0, 1, 25);
            public static readonly Vector3Int Column_HFloor_Upper = new Vector3Int(0, 2, 25);

            // GShape ㄱ + balcony — isolated outdoor structure
            public static readonly Vector3Int GShape_A = new Vector3Int(30, 1, 10);
            public static readonly Vector3Int GShape_B = new Vector3Int(31, 1, 10);
            public static readonly Vector3Int GShape_C = new Vector3Int(31, 1, 11);
            public static readonly Vector3Int Balcony_Floor = new Vector3Int(31, 1, 12);

            public static readonly Vector3Int Plaza_Floor = new Vector3Int(0, 0, 0);
            public static readonly Vector3Int Dig_Floor = new Vector3Int(15, 1, 20);

            /// <summary>floor 없는 셀 — IsOutdoorEvaluation 선택 A (empty→true).</summary>
            public static readonly Vector3Int EmptyOutdoorProbe = new Vector3Int(50, 1, 50);

            public static readonly Vector3Int IndoorProbe = new Vector3Int(0, 1, 11);
            public static readonly Vector3Int OutdoorShedProbe = OpenShed_A;

            /// <summary>outdoor 레이어 시드 (min plaza BFS 대체).</summary>
            public static readonly Vector3Int[] OutdoorFloorCells =
            {
                Plaza_Floor,
            };

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

                // Plaza — y=0 outdoor ground (does not touch house z=10)
                AddFloorRect(tiles, x0: 0, x1: 10, z0: 0, z1: 2, y: 0);

                // Enclosed two-room house (isolated)
                AddFloorRect(tiles, x0: 0, x1: 3, z0: 10, z1: 12, y: 1);
                AddSlimPerimeter(tiles, x0: 0, x1: 3, z0: 10, z1: 12, y: 1);
                for (int z = 10; z <= 12; z++)
                    tiles.Add(BakeIdSyntheticTiles.ThinWall(
                        new Vector3Int(1, 1, z), new Vector3Int(2, 1, z)));
                AddCubeRect(tiles, x0: 0, x1: 3, z0: 10, z1: 12, y: 2);

                // Open shed — perimeter walls, no roof → outdoor space
                AddFloorRect(tiles, x0: 20, x1: 21, z0: 10, z1: 11, y: 1);
                AddSlimPerimeter(tiles, x0: 20, x1: 21, z0: 10, z1: 11, y: 1);

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

                // G-shape + balcony (no enclosure — outdoor), isolated
                tiles.Add(BakeIdSyntheticTiles.Floor(GShape_A));
                tiles.Add(BakeIdSyntheticTiles.Floor(GShape_B));
                tiles.Add(BakeIdSyntheticTiles.Floor(GShape_C));
                tiles.Add(BakeIdSyntheticTiles.Floor(Balcony_Floor));

                // DigTarget — not adjacent to bridge pads
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
