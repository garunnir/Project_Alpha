// ============================================================
// TileDamagePresentation — 맵 HP → 타일 크랙 stage 해석·갱신
// ============================================================

using System;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// Dig/구조물 remaining HP를 presentation stage로 연결.
    /// stage는 저장하지 않음 — <see cref="MapDigColumnHost"/> HP가 SSOT.
    /// </summary>
    public static class TileDamagePresentation
    {
        /// <summary>
        /// 타일 뷰 presentation 합성용. 호스트·정의 없으면 0.
        /// </summary>
        public static int ResolveStage(in TileData tile, MapDigColumnHost host)
        {
            if (host == null)
                return 0;

            if (!TryResolveDefinition(tile.identity.PrefabId, out TileDefinition definition))
                return 0;

            int maxHp = TileDefinitionCombat.BreakDurability(definition);

            if (TileIdentityUtil.IsHorizontalFace(tile.identity))
            {
                Vector3Int walkable = FloorFaceKey.FromFloorTileIdentity(tile.identity).CellAbove;
                if (!host.TryGetFloorFaceRemaining(walkable, out int remaining))
                    return 0;
                return TileDamagePresentationConsts.StageFromRemaining(remaining, maxHp);
            }

            if (TileIdentityUtil.IsOccupiedCell(tile.identity) &&
                MapDigTerrainUtil.IsWalkableStratumBlock(tile.identity, definition))
            {
                Vector3Int anchor = tile.identity.GridPos;
                if (!host.TryGetOccupiedRemaining(anchor, out int stratumRemaining))
                    return 0;

                return TileDamagePresentationConsts.StageFromRemaining(stratumRemaining, maxHp);
            }

            if (!TileIdentityUtil.IsOccupiedCell(tile.identity) ||
                !TileIdentityUtil.IsWallLike(tile.identity))
            {
                return 0;
            }

            Vector3Int wallCell = tile.identity.GridPos;
            if (!host.TryGetOccupiedRemaining(wallCell, out int wallRemaining))
                return 0;

            return TileDamagePresentationConsts.StageFromRemaining(wallRemaining, maxHp);
        }

        public static int ResolveStage(Guid tileId, TileMapModel model, MapDigColumnHost host)
        {
            if (model == null || tileId == Guid.Empty)
                return 0;
            if (!model.TryGetTileById(tileId, out TileData tile))
                return 0;
            return ResolveStage(in tile, host);
        }

        /// <summary>FloorFace HP 변경 후 뷰 재적용.</summary>
        public static void RefreshFloorFace(Vector3Int walkableCell)
        {
            TileMapCacheHub hub = TileMapCacheHub.Runtime;
            if (hub == null)
                return;

            Vector3Int cellBelow = walkableCell + Vector3Int.down;
            if (!hub.TryGetHorizontalFaceBetween(cellBelow, walkableCell, out TileData face))
                return;

            RefreshTile(face.tileDefId);
        }

        /// <summary>Occupied HP 변경 후 뷰 재적용.</summary>
        public static void RefreshOccupied(Vector3Int cell)
        {
            TileMapCacheHub hub = TileMapCacheHub.Runtime;
            if (hub == null ||
                !hub.TryGetCellTiles(cell.x, cell.z, cell.y, out var tiles) ||
                tiles == null)
            {
                return;
            }

            for (int i = 0; i < tiles.Count; i++)
            {
                TileData tile = tiles[i];
                if (TileIdentityUtil.IsWallLike(tile.identity))
                {
                    RefreshTile(tile.tileDefId);
                    return;
                }

                if (!TileIdentityUtil.IsOccupiedCell(tile.identity) ||
                    !TilePrefabDB.TryResolveDefinition(tile.identity.PrefabId, out TileDefinition def) ||
                    !MapDigTerrainUtil.IsWalkableStratumBlock(def))
                {
                    continue;
                }

                RefreshTile(tile.tileDefId);
                return;
            }
        }

        public static void RefreshTile(Guid tileId)
        {
            if (tileId == Guid.Empty)
                return;
            TilePresentationSystem.Instance?.RefreshPresentation(tileId);
        }

        static bool TryResolveDefinition(string prefabId, out TileDefinition definition) =>
            TilePrefabDB.TryResolveDefinition(prefabId, out definition);
    }
}
