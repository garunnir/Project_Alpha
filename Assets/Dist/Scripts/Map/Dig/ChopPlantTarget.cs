// ============================================================
// ChopPlantTarget — 벌목 대상 나무 OccupiedCell
// ============================================================

using UnityEngine;

namespace IsoTilemap
{
    public readonly struct ChopPlantTarget
    {
        public readonly Vector3Int Cell;
        public readonly PlantCell Plant;
        public readonly TileDefinition Definition;

        public ChopPlantTarget(Vector3Int cell, PlantCell plant, TileDefinition definition)
        {
            Cell = cell;
            Plant = plant;
            Definition = definition;
        }
    }
}
