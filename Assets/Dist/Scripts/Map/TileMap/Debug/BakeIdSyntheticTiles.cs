// ============================================================
// [BakeIdSyntheticTiles] — Bake ID 플레이그라운드/테스트용 Floor·SlimWall·ThickWall
// ============================================================

using System;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// 실제 TileDefinition prefabId로 <see cref="TileData"/>를 만든다 (시각 = PrefabDB 프리팹).
    /// collisionFlags는 bake 검증용으로 정의 프로파일과 맞춰 직접 넣는다 (EditMode에서 SO 로드 불필요).
    /// </summary>
    public static class BakeIdSyntheticTiles
    {
        public const string FloorPrefabId = "Floor/GrassFloor";
        public const string ThinWallPrefabIdPosX = "SlimWall/Wall_NE";
        public const string ThinWallPrefabIdPosZ = "SlimWall/Wall_WN";
        public const string CubePrefabId = "ThickWall/Wall_NE";

        /// <summary>레거시 별칭 — ThinWall 기본(PosX) id.</summary>
        public const string ThinWallPrefabId = ThinWallPrefabIdPosX;

        public static TileData Floor(int x, int y, int z) =>
            Floor(new Vector3Int(x, y, z));

        public static TileData Floor(Vector3Int walkableCell)
        {
            return new TileData
            {
                tileDefId = Guid.NewGuid(),
                state = default,
                identity = new TileIdentity
                {
                    PrefabId = FloorPrefabId,
                    GridPos = walkableCell,
                    sizeUnit = Vector3Int.one,
                    placementSlot = (byte)TilePlacementSlot.HorizontalFace,
                    floorFace = (byte)FloorFace.PosY,
                    wallFace = 0,
                    buildingId = TileIdentity.BuildingIdUnassigned,
                    roomId = 0,
                    collisionFlags = (byte)TileCollisionFlags.ProvidesLogicalFloor,
                },
            };
        }

        /// <summary>두 walkable 셀 사이 얇은 벽 (SeparatesRoom + BlocksEdge). SlimWall 프리팹.</summary>
        public static TileData ThinWall(Vector3Int cellA, Vector3Int cellB)
        {
            if (!WallEdgeKey.TryBetween(cellA, cellB, out WallEdgeKey key))
            {
                throw new ArgumentException(
                    $"ThinWall requires cardinal neighbors at same Y: {cellA} ↔ {cellB}");
            }

            string prefabId = key.Face == WallFace.PosX
                ? ThinWallPrefabIdPosX
                : ThinWallPrefabIdPosZ;

            return new TileData
            {
                tileDefId = Guid.NewGuid(),
                state = default,
                identity = new TileIdentity
                {
                    PrefabId = prefabId,
                    GridPos = key.Anchor,
                    sizeUnit = Vector3Int.one,
                    placementSlot = (byte)TilePlacementSlot.VerticalFace,
                    wallFace = (byte)key.Face,
                    floorFace = 0,
                    buildingId = TileIdentity.BuildingIdUnassigned,
                    roomId = 0,
                    collisionFlags = (byte)(TileCollisionFlags.SeparatesRoom | TileCollisionFlags.BlocksEdge),
                },
            };
        }

        public static TileData ThinWall(int ax, int ay, int az, int bx, int by, int bz) =>
            ThinWall(new Vector3Int(ax, ay, az), new Vector3Int(bx, by, bz));

        public static TileData Cube(int x, int y, int z) =>
            Cube(new Vector3Int(x, y, z));

        /// <summary>점유 셀 블록 벽 (ThickWall). BlocksOccupiedCells + OccludesOccupiedCells.</summary>
        public static TileData Cube(Vector3Int cell)
        {
            return new TileData
            {
                tileDefId = Guid.NewGuid(),
                state = default,
                identity = new TileIdentity
                {
                    PrefabId = CubePrefabId,
                    GridPos = cell,
                    sizeUnit = Vector3Int.one,
                    placementSlot = (byte)TilePlacementSlot.OccupiedCell,
                    wallFace = 0,
                    floorFace = 0,
                    buildingId = TileIdentity.BuildingIdUnassigned,
                    roomId = 0,
                    collisionFlags = (byte)(TileCollisionFlags.BlocksOccupiedCells |
                                           TileCollisionFlags.OccludesOccupiedCells),
                },
            };
        }

        public static bool IsThinWallBetween(in TileData tile, Vector3Int cellA, Vector3Int cellB)
        {
            if (!TileIdentityUtil.IsVerticalFace(tile.identity))
                return false;
            if (!WallEdgeKey.TryBetween(cellA, cellB, out WallEdgeKey expected))
                return false;

            var key = WallEdgeKey.FromWallTileIdentity(tile.identity);
            return key.Equals(expected);
        }
    }
}
