// ============================================================
// MapDigTerrainUtil — walkable 지형 블록(OccupiedCell) 앵커·판정 SSOT
// ============================================================

using UnityEngine;

namespace IsoTilemap
{
    public static class MapDigTerrainUtil
    {
        /// <summary>발밑 walkable을 지지하는 블록 앵커 — ThickWall OccupiedCell과 동일 (walkable + down).</summary>
        public static Vector3Int SupportBlockAnchor(Vector3Int walkableCell) =>
            walkableCell + Vector3Int.down;

        public static bool IsWalkableStratumBlock(TileDefinition definition)
        {
            if (definition == null || !TileFlags.IsDiggableTarget(definition))
                return false;

            if (TileIdentityUtil.ResolvePlacementSlot(definition, definition.prefabId) !=
                TilePlacementSlot.OccupiedCell)
            {
                return false;
            }

            return definition.occupied.providesLogicalFloor &&
                   !definition.occupied.blocksPassageAndOcclusion;
        }

        public static bool IsWalkableStratumBlock(in TileIdentity identity, TileDefinition definition) =>
            TileIdentityUtil.IsOccupiedCell(identity) && IsWalkableStratumBlock(definition);

        /// <summary>OccupiedCell + ProvidesLogicalFloor + 비벽 — walkable 등록·CellHasFloor 폴백.</summary>
        public static bool IsWalkableSupportBlock(in TileIdentity identity)
        {
            if (!TileIdentityUtil.IsOccupiedCell(identity))
                return false;

            if (!TileCollisionFlagsUtil.Has(
                    identity.collisionFlags,
                    TileCollisionFlags.ProvidesLogicalFloor))
            {
                return false;
            }

            return !TileCollisionFlagsUtil.Has(
                       identity.collisionFlags,
                       TileCollisionFlags.BlocksOccupiedCells) &&
                   !TileIdentityUtil.IsWallLike(identity);
        }

        public static int WalkableCellYFromSupportAnchor(int anchorCellY, int sizeY)
        {
            int sy = sizeY < 1 ? 1 : sizeY;
            return anchorCellY + sy;
        }

        public static bool TryGetSupportBlockAtAnchor(
            TileMapCacheHub hub,
            Vector3Int anchorCell,
            out TileData blockTile,
            out TileDefinition definition)
        {
            blockTile = default;
            definition = null;
            if (hub == null)
                return false;

            if (!hub.TryGetCellTiles(anchorCell.x, anchorCell.z, anchorCell.y, out var tiles) ||
                tiles == null)
            {
                return false;
            }

            for (int i = 0; i < tiles.Count; i++)
            {
                TileData tile = tiles[i];
                if (!IsWalkableSupportBlock(tile.identity))
                    continue;

                if (!TilePrefabDB.TryResolveDefinition(tile.identity.PrefabId, out definition) ||
                    !IsWalkableStratumBlock(definition))
                {
                    continue;
                }

                blockTile = tile;
                return true;
            }

            return false;
        }

        public static bool TryGetSupportBlockForWalkable(
            TileMapCacheHub hub,
            Vector3Int walkableCell,
            out TileData blockTile,
            out TileDefinition definition) =>
            TryGetSupportBlockAtAnchor(hub, SupportBlockAnchor(walkableCell), out blockTile, out definition);
    }
}
