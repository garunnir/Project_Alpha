// ============================================================
// MapDigService — 채굴·굴착 CanBreak / TryBreak 오케스트레이션
// ============================================================

using System;
using UnityEngine;

namespace IsoTilemap
{
    public static class MapDigService
    {
        static MapDigRuntimeHooks _hooks = MapDigRuntimeHooks.Default;

        public static void Configure(MapDigRuntimeHooks hooks) =>
            _hooks = hooks.PlayerHasDigTool == null ? MapDigRuntimeHooks.Default : hooks;

        public static float BaseBreakSeconds => MapDigConsts.BaseBreakSeconds;

        public static bool CanBreak(DigTileTarget target) =>
            GetBlockedReason(target) == null;

        public static string GetBlockedReason(DigTileTarget target)
        {
            if (_hooks.IsMoodBlocked != null && _hooks.IsMoodBlocked())
                return BlockedLabel();

            if (_hooks.PlayerHasDigTool == null || !_hooks.PlayerHasDigTool())
                return BlockedLabel();

            if (MapDigColumnHost.Runtime == null)
                return BlockedLabel();

            if (target.Definition == null || !TileFlags.IsDiggableTarget(target.Definition))
                return BlockedLabel();

            if (!IsWithinDigRange(target.WalkableCell))
                return BlockedLabel();

            return null;
        }

        public static bool TryBreak(DigTileTarget target)
        {
            if (GetBlockedReason(target) != null)
                return false;

            MapDigColumnHost host = MapDigColumnHost.Runtime;
            if (host == null)
                return false;

            if (!host.TryBreakDigTarget(target, out string brokenPrefabId))
                return false;

            GrantBreakDrop(brokenPrefabId, host.CellWorld(target.WalkableCell));
            return true;
        }

        public static bool TryBreakAt(Vector3Int walkableCell)
        {
            MapDigColumnHost host = MapDigColumnHost.Runtime;
            if (host == null || MapPlantHost.Runtime == null)
                return false;

            TileDefinition definition = MapPlantHost.Runtime.GetFloorDefinition(walkableCell);
            if (definition == null || !TileFlags.IsDiggableTarget(definition))
                return false;

            if (!_hubTryGetFace(host, walkableCell, out TileData faceTile))
                return false;

            return TryBreak(new DigTileTarget(
                walkableCell,
                DigBreakKind.HorizontalFace,
                faceTile,
                definition));
        }

        static bool _hubTryGetFace(MapDigColumnHost host, Vector3Int walkableCell, out TileData faceTile)
        {
            faceTile = default;
            TileMapCacheHub hub = TileMapCacheHub.Runtime;
            if (hub == null)
                return false;

            Vector3Int cellBelow = walkableCell + Vector3Int.down;
            return hub.TryGetHorizontalFaceBetween(cellBelow, walkableCell, out faceTile);
        }

        static bool IsWithinDigRange(Vector3Int targetCell)
        {
            if (_hooks.TryResolveActorWorld == null ||
                !_hooks.TryResolveActorWorld(out Vector3 actorWorld))
            {
                return false;
            }

            MapDigColumnHost host = MapDigColumnHost.Runtime;
            float cellSize = host != null ? host.CellSize : 1f;
            return MapDigConsts.IsWithinActionRangeWorld(actorWorld, targetCell, cellSize);
        }

        static void GrantBreakDrop(string prefabId, Vector3 world)
        {
            if (_hooks.GrantItem == null ||
                !MapDigConsts.TryGetDropItemId(prefabId, out string itemId))
            {
                return;
            }

            _hooks.GrantItem(itemId, 1, world);
        }

        static string BlockedLabel() =>
            _hooks.DigBlockedLabel != null ? _hooks.DigBlockedLabel() : "채굴할 수 없음";
    }
}
