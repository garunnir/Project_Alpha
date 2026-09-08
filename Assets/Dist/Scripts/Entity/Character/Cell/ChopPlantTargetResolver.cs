// ============================================================
// ChopPlantTargetResolver — AimWorldPoint → 나무 OccupiedCell (DistScript)
// ============================================================

using IsoTilemap;
using UnityEngine;

public static class ChopPlantTargetResolver
{
    static readonly System.Collections.Generic.List<TileData> Scratch = new();

    /// <summary>조준 월드점 → 그리드 셀 → 나무 PlantCell.</summary>
    public static bool TryResolveFromWorldPoint(
        Vector3 worldPoint,
        float cellSize,
        out ChopPlantTarget target)
    {
        target = default;
        MapPlantHost plantHost = MapPlantHost.Runtime;
        if (plantHost == null)
            return false;

        Vector3Int cell = TileHelper.ConvertWorldToGrid(worldPoint, Mathf.Max(1e-4f, cellSize));
        if (MapPlantService.GetChopBlockedReason(cell) != null)
            return false;

        if (!plantHost.TryGetPlant(cell, out PlantCell plant))
            return false;

        TileDefinition definition = null;
        if (TileMapCacheHub.Runtime != null &&
            TileMapCacheHub.Runtime.TryCollectTilesAtOccupiedCell(cell, Scratch) &&
            Scratch.Count > 0)
        {
            for (int i = 0; i < Scratch.Count; i++)
            {
                TileData tile = Scratch[i];
                if (plant.TileDefId != System.Guid.Empty && tile.tileDefId != plant.TileDefId)
                    continue;
                if (TilePrefabDB.TryResolveDefinition(tile.identity.PrefabId, out definition))
                    break;
            }
        }

        target = new ChopPlantTarget(cell, plant, definition);
        return true;
    }
}
