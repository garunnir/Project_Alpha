// ============================================================
// BuildingScenePartitionBake — 에디터 씬 TileView 연결 remesh + Building_* 재부모
// ============================================================
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// 저장과 분리: bake 시점에만 맞닿은 건물을 합치고 뷰 하이어라키를 id에 맞춘다.
    /// </summary>
    public static class BuildingScenePartitionBake
    {
        /// <returns>bake 후 양수 building 개수. 실패 시 -1.</returns>
        public static int BakeAndReparentViews(IReadOnlyList<TileView> views, float cellSize)
        {
            if (views == null || views.Count == 0)
                return 0;

            cellSize = Mathf.Max(1e-4f, cellSize);
            var indoorTiles = new List<TileData>();
            var outdoorFloorCells = new HashSet<Vector3Int>();
            var indoorViews = new List<(TileView view, TileData gathered)>();
            Transform tileContainer = null;

            for (int i = 0; i < views.Count; i++)
            {
                TileView view = views[i];
                if (view == null)
                    continue;

                List<TileData> one = TileViewSceneGather.BuildTileDataSnapshot(new[] { view });
                if (one == null || one.Count == 0)
                    continue;

                TileData data = one[0];
                Transform parent = view.transform.parent;

                if (BuildingViewHierarchy.IsUnderOutdoorRoot(parent))
                {
                    if (TileIdentityUtil.IsFloorTile(data.identity))
                        outdoorFloorCells.Add(data.identity.GridPos);
                    continue;
                }

                if (tileContainer == null)
                    tileContainer = ResolveTileContainer(parent);

                indoorTiles.Add(new TileData
                {
                    tileDefId = data.tileDefId,
                    state = data.state,
                    identity = new TileIdentity
                    {
                        PrefabId = data.identity.PrefabId,
                        GridPos = data.identity.GridPos,
                        sizeUnit = data.identity.sizeUnit,
                        placementSlot = data.identity.placementSlot,
                        wallFace = data.identity.wallFace,
                        floorFace = data.identity.floorFace,
                        buildingId = TileIdentity.BuildingIdUnassigned,
                        roomId = 0,
                        collisionFlags = data.identity.collisionFlags,
                    },
                    plant = data.plant,
                });
                indoorViews.Add((view, data));
            }

            if (indoorTiles.Count == 0)
                return 0;

            if (tileContainer == null)
            {
                Debug.LogError(
                    "[BuildingScenePartitionBake] Tile container (parent of Buildings/) not found.");
                return -1;
            }

            var model = new TileMapModel();
            var registry = new BuildingGroupRegistry();
            var hub = TileMapCacheHub.Create(model, registry);
            model.SetMapCacheHub(hub);
            var builder = new BuildingGroupBuilder(model, hub);
            model.SetBuildingGroupBuilder(builder);

            for (int i = 0; i < indoorTiles.Count; i++)
                model.SetTile(indoorTiles[i]);

            if (outdoorFloorCells.Count > 0)
                registry.ReplaceOutdoorFloorCells(outdoorFloorCells);

            builder.RebakeAllBuildingPartitions();

            var hierarchy = BuildingViewHierarchy.EnsureUnder(tileContainer, cellSize);
            hierarchy.BindRegistry(registry);

            IReadOnlyList<TileData> baked = model.TilesSnapshot;
            int reparented = 0;
            for (int i = 0; i < indoorViews.Count; i++)
            {
                TileView view = indoorViews[i].view;
                TileData gathered = indoorViews[i].gathered;
                if (!TryFindBakedMatch(baked, gathered, out TileData bakedTile))
                {
                    Debug.LogWarning(
                        $"[BuildingScenePartitionBake] No baked match for '{gathered.identity.PrefabId}' " +
                        $"at {gathered.identity.GridPos}",
                        view);
                    continue;
                }

                hierarchy.AttachTile(view, bakedTile);
                reparented++;
            }

            int buildingCount = 0;
            foreach (var kv in registry.TilesByBuildingId)
            {
                if (kv.Key > 0)
                    buildingCount++;
            }

            Debug.Log(
                $"[BuildingScenePartitionBake] remesh buildings={buildingCount}, reparented={reparented}/{indoorViews.Count}");
            return buildingCount;
        }

        static Transform ResolveTileContainer(Transform tileParent)
        {
            Transform t = tileParent;
            while (t != null)
            {
                if (t.name == BuildingViewHierarchy.BuildingsName ||
                    t.name == BuildingViewHierarchy.OutdoorName)
                    return t.parent;
                t = t.parent;
            }

            return null;
        }

        static bool TryFindBakedMatch(
            IReadOnlyList<TileData> baked,
            in TileData gathered,
            out TileData match)
        {
            match = default;
            TilePlacementSlot slot = TileIdentityUtil.GetPlacementSlot(gathered.identity);
            for (int i = 0; i < baked.Count; i++)
            {
                TileData b = baked[i];
                if (TileIdentityUtil.GetPlacementSlot(b.identity) != slot)
                    continue;
                if (b.identity.GridPos != gathered.identity.GridPos)
                    continue;
                if (!string.Equals(b.identity.PrefabId, gathered.identity.PrefabId, System.StringComparison.Ordinal))
                    continue;
                if (slot == TilePlacementSlot.VerticalFace &&
                    b.identity.wallFace != gathered.identity.wallFace)
                    continue;

                match = b;
                return true;
            }

            return false;
        }
    }
}
