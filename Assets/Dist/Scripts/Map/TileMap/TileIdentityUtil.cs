// ============================================================
// TileIdentityUtil — placementSlot·면 방향·역할 분류 단일 진실원
// ============================================================
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    public static class TileIdentityUtil
    {
        public static TilePlacementSlot GetPlacementSlot(in TileIdentity id) =>
            (TilePlacementSlot)id.placementSlot;

        public static bool IsOccupiedCell(in TileIdentity id) =>
            GetPlacementSlot(id) == TilePlacementSlot.OccupiedCell;

        public static bool IsVerticalFace(in TileIdentity id) =>
            GetPlacementSlot(id) == TilePlacementSlot.VerticalFace;

        public static bool IsHorizontalFace(in TileIdentity id) =>
            GetPlacementSlot(id) == TilePlacementSlot.HorizontalFace;

        public static bool IsValidHorizontalFaceIdentity(in TileIdentity id) =>
            IsHorizontalFace(id) && id.floorFace == (byte)FloorFace.PosY;

        public static bool IsFaceSlot(in TileIdentity id)
        {
            var slot = GetPlacementSlot(id);
            return slot is TilePlacementSlot.VerticalFace or TilePlacementSlot.HorizontalFace;
        }

        public static WallEdgeKey ToWallEdgeKey(in TileIdentity id) =>
            WallEdgeKey.FromWallTileIdentity(id);

        public static FloorFaceKey ToFloorFaceKey(in TileIdentity id) =>
            FloorFaceKey.FromFloorTileIdentity(id);

        public static Vector3Int GetWalkableCell(in TileIdentity id) =>
            OccupiedCellCoord.PrimaryCellFromIdentity(id);

        public static int GetPresentationCellY(in TileIdentity id) =>
            OccupiedCellCoord.PrimaryCellFromIdentity(id).y;

        public static bool IsFloorTile(in TileIdentity id) =>
            IsHorizontalFace(id);

        public static bool IsWallLike(in TileIdentity id)
        {
            if (IsVerticalFace(id))
                return true;

            if (!IsOccupiedCell(id))
                return false;

            return TileCollisionFlagsUtil.Has(id.collisionFlags, TileCollisionFlags.BlocksOccupiedCells)
                || TileCollisionFlagsUtil.Has(id.collisionFlags, TileCollisionFlags.OccludesOccupiedCells);
        }

        public static bool IsStructural(in TileIdentity id) =>
            IsFloorTile(id) || IsWallLike(id);

        /// <summary>
        /// Building component / shell flood가 영역을 밀어 쓰는 체적 구조 (OccupiedCell box).
        /// HorizontalFace·VerticalFace incident는 조회·면 연결용 — lot 확장 노드 아님.
        /// </summary>
        public static bool IsVolumeStructural(in TileIdentity id) =>
            IsOccupiedCell(id) && IsStructural(id);

        /// <summary>얇은 면(Floor·SlimWall) — 점유 등록은 조회용, building flood 확장 아님.</summary>
        public static bool IsThinFaceStructure(in TileIdentity id) =>
            IsHorizontalFace(id) || IsVerticalFace(id);

        public static void CollectAffectedCells(in TileIdentity id, HashSet<Vector3Int> cells)
        {
            switch (GetPlacementSlot(id))
            {
                case TilePlacementSlot.VerticalFace:
                    AppendWallIncidentCells(ToWallEdgeKey(id), id.sizeUnit.y, cells);
                    break;
                case TilePlacementSlot.HorizontalFace:
                    AppendFloorIncidentCells(ToFloorFaceKey(id), id.sizeUnit.y, cells);
                    break;
                default:
                    AppendOccupiedCellBox(id.GridPos, id.sizeUnit, cells);
                    break;
            }
        }

        public static void AppendOccupiedCellBox(Vector3Int basePos, Vector3Int sizeUnit, ICollection<Vector3Int> cells)
        {
            int sx = Mathf.Max(1, sizeUnit.x);
            int sy = Mathf.Max(1, sizeUnit.y);
            int sz = Mathf.Max(1, sizeUnit.z);

            for (int dx = 0; dx < sx; dx++)
            {
                for (int dy = 0; dy < sy; dy++)
                {
                    for (int dz = 0; dz < sz; dz++)
                    {
                        cells.Add(new Vector3Int(
                            basePos.x + dx,
                            basePos.y + dy,
                            basePos.z + dz));
                    }
                }
            }
        }

        public static void AppendWallIncidentCells(in WallEdgeKey key, int sizeY, ICollection<Vector3Int> cells)
        {
            int sy = Mathf.Max(1, sizeY);
            for (int dy = 0; dy < sy; dy++)
            {
                var yOffset = new Vector3Int(0, dy, 0);
                cells.Add(key.Anchor + yOffset);
                cells.Add(key.NeighborCell() + yOffset);
            }
        }

        public static void AppendFloorIncidentCells(in FloorFaceKey key, int sizeY, ICollection<Vector3Int> cells)
        {
            int sy = Mathf.Max(1, sizeY);
            for (int dy = 0; dy < sy; dy++)
            {
                var yOffset = new Vector3Int(0, dy, 0);
                cells.Add(key.CellBelow + yOffset);
                cells.Add(key.CellAbove + yOffset);
            }
        }

        public static TilePlacementSlot InferSlotFromLegacyTileType(byte legacyTileType)
        {
            switch ((TileView.TileType)legacyTileType)
            {
                case TileView.TileType.Floor:
                    return TilePlacementSlot.HorizontalFace;
                case TileView.TileType.EdgeWall:
                    return TilePlacementSlot.VerticalFace;
                case TileView.TileType.Wall:
                case TileView.TileType.Slope:
                    return TilePlacementSlot.OccupiedCell;
                default:
                    return TilePlacementSlot.None;
            }
        }

        public static TilePlacementSlot InferSlotFromPrefabId(string prefabId)
        {
            if (string.IsNullOrEmpty(prefabId))
                return TilePlacementSlot.None;

            if (prefabId.StartsWith("SlimWall/", System.StringComparison.Ordinal))
                return TilePlacementSlot.VerticalFace;
            if (prefabId.StartsWith("Floor/", System.StringComparison.Ordinal))
                return TilePlacementSlot.HorizontalFace;
            if (prefabId.StartsWith("ThickWall/", System.StringComparison.Ordinal)
                || prefabId.StartsWith("Slope/", System.StringComparison.Ordinal))
                return TilePlacementSlot.OccupiedCell;

            return TilePlacementSlot.None;
        }

        /// <summary>Definition → prefabId prefix → OccupiedCell.</summary>
        public static TilePlacementSlot ResolvePlacementSlot(TileDefinition def, string prefabId)
        {
            if (def != null && def.placementSlot != TilePlacementSlot.None)
                return def.placementSlot;

            var inferred = InferSlotFromPrefabId(prefabId);
            return inferred != TilePlacementSlot.None
                ? inferred
                : TilePlacementSlot.OccupiedCell;
        }
    }
}
