namespace IsoTilemap
{
    /// <summary>맵 JSON <see cref="MapSaveJsonDto.schemaVersion"/> 단계.</summary>
    public static class MapSaveSchema
    {
        public const int Current = 4;

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
    }
}
