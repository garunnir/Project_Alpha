// ============================================================
// ConstructionService — 맵 건설 CanBuild / TryBuildAt (재료 소비 + 타일 설치)
// ============================================================

using System;
using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;
using UnityEngine;

public static class ConstructionService
{
    static readonly List<TileData> ScratchTiles = new(8);

    public static TilePlacementSlot ResolvePostSlot(ConstructionData data)
    {
        if (data == null || string.IsNullOrEmpty(data.post_slot))
            return TilePlacementSlot.OccupiedCell;

        if (string.Equals(
                data.post_slot,
                ConstructionConsts.SlotHorizontalFace,
                StringComparison.OrdinalIgnoreCase))
            return TilePlacementSlot.HorizontalFace;

        if (string.Equals(
                data.post_slot,
                ConstructionConsts.SlotVerticalFace,
                StringComparison.OrdinalIgnoreCase))
            return TilePlacementSlot.VerticalFace;

        return TilePlacementSlot.OccupiedCell;
    }

    public static RecipeData ToMaterialRecipe(ConstructionData data)
    {
        if (data == null)
            return null;

        return new RecipeData
        {
            id = data.id,
            result = string.IsNullOrEmpty(data.post_prefab_id) ? data.id : data.post_prefab_id,
            category = data.category,
            skill_used = data.skill_used,
            skills_required = data.skills_required,
            difficulty = data.difficulty,
            time_minutes = data.time_minutes,
            qualities_required = data.qualities_required,
            tools = data.tools,
            components = data.components,
            result_count = 1,
        };
    }

    public static bool CanBuild(
        ConstructionData data,
        Vector3Int cell,
        CraftingMaterialPool pool,
        TileMapManager mapManager,
        int facingQuarters = 0)
    {
        if (data == null || pool == null || mapManager == null)
            return false;

        if (string.IsNullOrEmpty(data.post_prefab_id))
            return false;

        RecipeData recipe = ToMaterialRecipe(data);
        if (!CraftingService.CanCraft(recipe, pool))
            return false;

        if (!mapManager.PrefabDB.TryGetDefinition(data.post_prefab_id, out TileDefinition def) ||
            def == null)
            return false;

        if (!TilePlaceUtil.CanPlaceOnBase(def, cell))
            return false;

        return true;
    }

    public static bool TryBuildAt(
        ConstructionData data,
        Vector3Int cell,
        CraftingMaterialPool pool,
        TileMapManager mapManager,
        TileMapController controller,
        int facingQuarters = 0,
        InventorySession session = null)
    {
        if (!CanBuild(data, cell, pool, mapManager, facingQuarters))
            return false;

        if (controller == null)
            return false;

        if (!mapManager.PrefabDB.TryGetDefinition(data.post_prefab_id, out TileDefinition def) ||
            def == null)
            return false;

        RecipeData recipe = ToMaterialRecipe(data);
        if (!TryConsumeMaterials(recipe, pool))
            return false;

        TilePlacementSlot slot = ResolvePostSlot(data);
        byte wallFace = (byte)(facingQuarters & 1);

        if (slot == TilePlacementSlot.HorizontalFace)
        {
            if (!controller.TryReplaceFloorMaterial(cell, def))
                return false;
        }
        else
        {
            Vector3Int installCell = cell;
            if (slot == TilePlacementSlot.OccupiedCell &&
                mapManager.MapCacheHub != null)
            {
                installCell = TilePlaceUtil.ResolveOccupiedInstallCell(
                    mapManager.MapCacheHub,
                    cell,
                    ScratchTiles);
            }

            if (!TilePlaceUtil.TryBuildTileData(def, installCell, out TileData tileData, default, wallFace))
                return false;

            controller.AddAndFlush(tileData);
            // OccupiedCell yaw: preview only in v1 (TileIdentity has no facing field).
        }

        if (session != null)
            NotifyPoolChanged(session, pool);

        if (!string.IsNullOrEmpty(data.skill_used) && GameplayData.Stats != null)
            GameplayData.Stats.AddPractice(data.skill_used, Mathf.Max(1, data.difficulty));

        return true;
    }

    public static float ResolveWorkDurationSeconds(ConstructionData data)
    {
        if (data == null)
            return ConstructionConsts.DefaultWorkDurationSeconds;

        float minutes = Mathf.Max(0f, data.time_minutes);
        if (minutes <= 0f)
            return ConstructionConsts.DefaultWorkDurationSeconds;

        return minutes * ConstructionConsts.MinutesToRealtimeSeconds;
    }

    public static Vector3 CellArriveWorld(Vector3Int cell)
    {
        MapPlantHost host = MapPlantHost.Runtime;
        float cellSize = host != null ? host.CellSize : 1f;
        return TileHelper.ConvertGridToWorldPos(cell, cellSize);
    }

    public static float CellArriveStoppingDistance()
    {
        MapPlantHost host = MapPlantHost.Runtime;
        float cellSize = host != null ? host.CellSize : 1f;
        return cellSize * ConstructionConsts.CellArriveStoppingCellFraction;
    }

    public static CraftingMaterialPool CreatePoolFromActivePlayer()
    {
        PlayerInventoryRuntime runtime = PlayerInventoryRuntime.Active;
        if (runtime?.Session == null)
            return null;

        return new CraftingMaterialPool(
            runtime.Session.GetSidebarContainers(),
            runtime.IsWorldLootContainer,
            PlayerInventoryHost.DefaultInstanceId);
    }

    static bool TryConsumeMaterials(RecipeData recipe, CraftingMaterialPool pool)
    {
        if (recipe.components != null)
        {
            for (int i = 0; i < recipe.components.Count; i++)
            {
                ComponentSlot slot = recipe.components[i];
                if (slot?.alternatives == null || slot.alternatives.Count == 0)
                    return false;

                bool removed = false;
                for (int a = 0; a < slot.alternatives.Count; a++)
                {
                    ComponentAlt alt = slot.alternatives[a];
                    if (alt == null || string.IsNullOrEmpty(alt.item))
                        continue;
                    if (alt.count <= 0)
                    {
                        removed = true;
                        break;
                    }

                    if (pool.CountItem(alt.item) >= alt.count &&
                        pool.TryRemoveItem(alt.item, alt.count))
                    {
                        removed = true;
                        break;
                    }
                }

                if (!removed)
                    return false;
            }
        }

        if (recipe.tools != null)
        {
            for (int i = 0; i < recipe.tools.Count; i++)
            {
                ToolSlot slot = recipe.tools[i];
                if (slot?.alternatives == null)
                    continue;

                for (int a = 0; a < slot.alternatives.Count; a++)
                {
                    ToolAlt alt = slot.alternatives[a];
                    if (alt == null || alt.charges <= 0)
                        continue;
                    if (pool.CountToolCharges(alt.tool) >= alt.charges)
                    {
                        pool.TryConsumeToolCharges(alt.tool, alt.charges);
                        break;
                    }
                }
            }
        }

        return true;
    }

    static void NotifyPoolChanged(InventorySession session, CraftingMaterialPool pool)
    {
        if (session == null || pool?.Sources == null)
            return;

        for (int i = 0; i < pool.Sources.Count; i++)
            session.NotifyExternalStacksChanged(pool.Sources[i]);
    }
}
