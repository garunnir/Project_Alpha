// ============================================================
// TileDamagePresentation — 맵 HP → 타일 크랙 (모델→뷰)
// ============================================================

using System;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// Dig/구조물 remaining HP → Persistent 크랙 뷰.
    /// stage는 저장하지 않음 — <see cref="MapDigColumnHost"/> HP가 SSOT.
    /// Dig: <see cref="ApplyHpToView"/> 직행. Transient Resolve는 크랙을 덮지 않음.
    /// </summary>
    public static class TileDamagePresentation
    {
        /// <summary>크랙 오버레이를 붙일 수 있는 Dig/구조 타일인지 (Resolve·TileView 공용).</summary>
        public static bool SupportsCrackOverlay(in TileIdentity identity, TileDefinition definition)
        {
            if (definition == null)
                return false;

            if (TileIdentityUtil.IsHorizontalFace(identity))
                return TileFlags.IsDiggableTarget(definition);

            if (!TileIdentityUtil.IsOccupiedCell(identity))
                return false;

            if (MapDigTerrainUtil.IsWalkableStratumBlock(identity, definition))
                return true;

            return TileIdentityUtil.IsWallLike(identity);
        }

        public static bool SupportsCrackOverlay(TilePlacementSlot slot, string prefabId)
        {
            if (slot == TilePlacementSlot.HorizontalFace)
            {
                return TryResolveDefinition(prefabId, out TileDefinition faceDef) &&
                       TileFlags.IsDiggableTarget(faceDef);
            }

            if (slot != TilePlacementSlot.OccupiedCell)
                return false;

            return TryResolveDefinition(prefabId, out TileDefinition occupiedDef) &&
                   (MapDigTerrainUtil.IsWalkableStratumBlock(occupiedDef) ||
                    occupiedDef.occupied.blocksPassageAndOcclusion ||
                    occupiedDef.occupied.occludesOccupiedCells);
        }

        /// <summary>
        /// 청크 스폰·가시성 sync용. Host HP를 stage로 읽어 Resolve에 넣음.
        /// </summary>
        public static int ResolveStage(in TileData tile, MapDigColumnHost host)
        {
            if (host == null)
                return 0;

            if (!TryResolveDefinition(tile.identity.PrefabId, out TileDefinition definition))
                return 0;

            if (!SupportsCrackOverlay(tile.identity, definition))
                return 0;

            int maxHp = TileDefinitionCombat.BreakDurability(definition);

            if (TileIdentityUtil.IsHorizontalFace(tile.identity))
            {
                Vector3Int walkable = FloorFaceKey.FromFloorTileIdentity(tile.identity).CellAbove;
                if (!host.TryGetFloorFaceRemaining(walkable, out int remaining))
                    return 0;
                return TileDamagePresentationConsts.StageFromRemaining(remaining, maxHp);
            }

            Vector3Int anchor = tile.identity.GridPos;
            if (!host.TryGetOccupiedRemaining(anchor, out int occupiedRemaining))
                return 0;

            return TileDamagePresentationConsts.StageFromRemaining(occupiedRemaining, maxHp);
        }

        public static int ResolveStage(Guid tileId, TileMapModel model, MapDigColumnHost host)
        {
            if (model == null || tileId == Guid.Empty)
                return 0;
            if (!model.TryGetTileById(tileId, out TileData tile))
                return 0;
            return ResolveStage(in tile, host);
        }

        /// <summary>
        /// Dig SSOT — remaining/max → Persistent 크랙 (PresentationTileId).
        /// </summary>
        public static void ApplyHpToView(in DigTileTarget target, int remaining, int maxHp)
        {
            Guid id = target.PresentationTileId;
            if (id == Guid.Empty)
                id = TryResolvePresentationId(in target);
            ApplyHpToView(id, remaining, maxHp);
        }

        public static void ApplyHpToView(Guid presentationTileId, int remaining, int maxHp)
        {
            if (presentationTileId == Guid.Empty)
                return;
            TilePresentationSystem.Instance?.ApplyDamageStage(presentationTileId, remaining, maxHp);
        }

        /// <summary>FloorFace HP 변경 후 뷰에 remaining 반영.</summary>
        public static void ApplyFloorFaceHp(Vector3Int walkableCell, int remaining)
        {
            TileMapCacheHub hub = TileMapCacheHub.Runtime;
            if (hub == null)
                return;

            Vector3Int cellBelow = walkableCell + Vector3Int.down;
            if (!hub.TryGetHorizontalFaceBetween(cellBelow, walkableCell, out TileData face))
                return;

            if (!TryResolveDefinition(face.identity.PrefabId, out TileDefinition def))
                return;

            ApplyHpToView(face.tileDefId, remaining, TileDefinitionCombat.BreakDurability(def));
        }

        /// <summary>Occupied HP 변경 후 뷰에 remaining 반영.</summary>
        public static void ApplyOccupiedHp(Vector3Int cell, int remaining)
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
                if (!TryResolveDefinition(tile.identity.PrefabId, out TileDefinition def))
                    continue;
                if (!SupportsCrackOverlay(tile.identity, def))
                    continue;

                ApplyHpToView(tile.tileDefId, remaining, TileDefinitionCombat.BreakDurability(def));
            }
        }

        /// <summary>레거시 — FloorFace remaining을 뷰에 반영.</summary>
        public static void RefreshFloorFace(Vector3Int walkableCell)
        {
            MapDigColumnHost host = MapDigColumnHost.Runtime;
            if (host != null && host.TryGetFloorFaceRemaining(walkableCell, out int remaining))
            {
                ApplyFloorFaceHp(walkableCell, remaining);
                return;
            }

            ApplyFloorFaceHp(walkableCell, remaining: int.MaxValue);
        }

        /// <summary>레거시 — Occupied remaining을 뷰에 반영.</summary>
        public static void RefreshOccupied(Vector3Int cell)
        {
            MapDigColumnHost host = MapDigColumnHost.Runtime;
            if (host != null && host.TryGetOccupiedRemaining(cell, out int remaining))
            {
                ApplyOccupiedHp(cell, remaining);
                return;
            }

            ApplyOccupiedHp(cell, remaining: int.MaxValue);
        }

        public static void RefreshTile(Guid tileId)
        {
            if (tileId == Guid.Empty)
                return;
            TilePresentationSystem.Instance?.RefreshPresentation(tileId);
        }

        static Guid TryResolvePresentationId(in DigTileTarget target)
        {
            TileMapCacheHub hub = TileMapCacheHub.Runtime;
            if (hub == null)
                return Guid.Empty;

            if (target.BreakKind == DigBreakKind.WalkableStratumBlock)
            {
                Vector3Int anchor = target.BlockAnchorCell;
                if (!hub.TryGetCellTiles(anchor.x, anchor.z, anchor.y, out var tiles) || tiles == null)
                    return Guid.Empty;

                for (int i = 0; i < tiles.Count; i++)
                {
                    TileData tile = tiles[i];
                    if (!TryResolveDefinition(tile.identity.PrefabId, out TileDefinition def))
                        continue;
                    if (!SupportsCrackOverlay(tile.identity, def))
                        continue;
                    return tile.tileDefId;
                }

                return Guid.Empty;
            }

            Vector3Int walkable = target.WalkableCell;
            Vector3Int cellBelow = walkable + Vector3Int.down;
            if (!hub.TryGetHorizontalFaceBetween(cellBelow, walkable, out TileData face))
                return Guid.Empty;
            return face.tileDefId;
        }

        static bool TryResolveDefinition(string prefabId, out TileDefinition definition) =>
            TilePrefabDB.TryResolveDefinition(prefabId, out definition);
    }
}
