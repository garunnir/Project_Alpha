namespace IsoTilemap
{
    /// <summary>맵 JSON <see cref="MapSaveJsonDto.schemaVersion"/> 단계.</summary>
    public static class MapSaveSchema
    {
        public const int Current = OutdoorStructureLayersV5;

        /// <summary>1 = placementSlot v1. 0·누락 = 레거시 tiles[].tileType.</summary>
        public const int PlacementSlotV1 = 1;

        /// <summary>floorFaces x,y,z = walkable (TileView.gridPos와 동일).</summary>
        public const int FloorWalkableCoords = 2;

        /// <summary><see cref="MapSaveJsonDto.stratumSeed"/> 필드 도입. 구 JSON은 0으로 로드.</summary>
        public const int StratumSeedV3 = 3;

        /// <summary>
        /// <see cref="MapSaveJsonDto.columnDepths"/> · <see cref="MapSaveJsonDto.tileDurabilities"/>.
        /// 구 JSON은 빈 목록.
        /// </summary>
        public const int TileDurabilityV4 = 4;

        /// <summary>
        /// <see cref="MapSaveJsonDto.outdoorFloorFaces"/> ·
        /// <see cref="MapSaveJsonDto.outdoorWallEdges"/> ·
        /// <see cref="MapSaveJsonDto.outdoorTiles"/> (월드) +
        /// <see cref="MapSaveJsonDto.buildings"/> (건물별 피벗·로컬 좌표, 직렬화 전용).
        /// </summary>
        public const int OutdoorStructureLayersV5 = 5;

        /// <summary>
        /// V4→V5·저장 분류용 야외 바닥 prefabId (마이그레이션 전용 — 영구 outdoor SSOT 아님).
        /// </summary>
        public const string OutdoorMigrationGrassFloorPrefabId = "Floor/GrassFloor";
    }
}
