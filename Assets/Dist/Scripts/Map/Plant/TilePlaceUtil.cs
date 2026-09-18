// ============================================================
// TilePlaceUtil — TileDefinition → TileData 조립 (건설·농사 공유)
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    public static class TilePlaceUtil
    {
        /// <summary>
        /// Builds TileData for placement. OccupiedCell·HorizontalFace use <paramref name="occupiedOrCursorCell"/> as walkable grid cell.
        /// </summary>
        public static bool TryBuildTileData(
            TileDefinition def,
            Vector3Int occupiedOrCursorCell,
            out TileData tileData,
            in PlantTileInstance plant = default,
            byte wallFace = 0)
        {
            tileData = default;
            if (def == null || string.IsNullOrEmpty(def.prefabId))
                return false;

            TilePlacementSlot slot = TileIdentityUtil.ResolvePlacementSlot(def, def.prefabId);
            Vector3Int sizeUnit = new Vector3Int(
                Mathf.Max(1, def.size.x),
                Mathf.Max(1, def.size.y),
                Mathf.Max(1, def.size.z));
            Vector3Int gridPos = occupiedOrCursorCell;

            var identity = new TileIdentity
            {
                PrefabId = def.prefabId,
                GridPos = gridPos,
                sizeUnit = sizeUnit,
                placementSlot = (byte)slot,
                wallFace = slot == TilePlacementSlot.VerticalFace
                    ? (byte)(wallFace & 1)
                    : (byte)0,
                floorFace = slot == TilePlacementSlot.HorizontalFace
                    ? (byte)FloorFace.PosY
                    : (byte)0,
                collisionFlags = TileCollisionProfile.FromDefinitionForSlot(slot, def),
            };

            tileData = new TileData
            {
                tileDefId = Guid.NewGuid(),
                state = new TileState(),
                identity = identity,
                plant = plant,
            };
            return true;
        }

        /// <summary>
        /// OccupiedCell install cell: if the support cell has an OccupiedCell with
        /// occupancy size (sizeUnit volume &gt; 0 claiming the cell), install one cell above.
        /// Furniture with no occupancy claim (all size axes treated as non-claiming via
        /// zero collision footprint policy) may share the cell — Dist uses sizeUnit.y &gt; 0
        /// with BlocksOccupiedCells as claiming; Planter has size but no block flags so
        /// same-cell plant is allowed when no BlocksOccupiedCells furniture is present.
        /// </summary>
        public static Vector3Int ResolveOccupiedInstallCell(
            TileMapCacheHub hub,
            Vector3Int targetCell,
            List<TileData> scratch)
        {
            if (hub == null || scratch == null)
                return targetCell;

            scratch.Clear();
            if (!hub.TryCollectTilesAtOccupiedCell(targetCell, scratch))
                return targetCell;

            for (int i = 0; i < scratch.Count; i++)
            {
                TileData tile = scratch[i];
                if (!TileIdentityUtil.IsOccupiedCell(tile.identity))
                    continue;
                if (PlantTileIds.IsPlantPrefabId(tile.identity.PrefabId))
                    continue;
                if (TileCollisionFlagsUtil.Has(
                        tile.identity.collisionFlags,
                        TileCollisionFlags.BlocksOccupiedCells))
                    return targetCell + Vector3Int.up;
            }

            return targetCell;
        }

        /// <summary>
        /// OccupiedCell installables may restrict which floor they can sit on via
        /// <see cref="TileDefinition.requiredBaseFlags"/> (TileFlags SSOT). Floor/Wall
        /// slots and installables with no required flags are unrestricted.
        /// </summary>
        public static bool CanPlaceOnBase(TileDefinition installable, Vector3Int cell)
        {
            if (installable == null)
                return false;
            if (installable.placementSlot != TilePlacementSlot.OccupiedCell)
                return true;
            if (installable.requiredBaseFlags == null || installable.requiredBaseFlags.Count == 0)
                return true;

            MapPlantHost host = MapPlantHost.Runtime;
            TileDefinition baseDef = host != null ? host.GetFloorDefinition(cell) : null;
            if (baseDef == null)
                return false;

            for (int i = 0; i < installable.requiredBaseFlags.Count; i++)
            {
                if (TileFlags.HasFlag(baseDef, installable.requiredBaseFlags[i]))
                    return true;
            }
            return false;
        }
    }
}
