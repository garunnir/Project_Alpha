// ============================================================
// SpaceBakeResult — Space bake 산출 (야외/실내 판정용)
// ============================================================
namespace IsoTilemap
{
    public sealed class SpaceBakeResult
    {
        public int SpaceId { get; }
        public int BuildingId { get; }
        public RoomKey SeedRoom { get; }
        public bool IsOutdoor { get; set; }
        public int MinFloorY { get; private set; } = int.MaxValue;
        public int MaxFloorY { get; private set; } = int.MinValue;
        public bool HasFloorBounds => MinFloorY <= MaxFloorY;

        public SpaceBakeResult(int spaceId, int buildingId, RoomKey seedRoom, bool isOutdoor = false)
        {
            SpaceId = spaceId;
            BuildingId = buildingId;
            SeedRoom = seedRoom;
            IsOutdoor = isOutdoor;
        }

        /// <summary>volume 셀(floor·empty) Y band 갱신.</summary>
        public void IncludeCell(UnityEngine.Vector3Int cell)
        {
            if (cell.y < MinFloorY)
                MinFloorY = cell.y;
            if (cell.y > MaxFloorY)
                MaxFloorY = cell.y;
        }

        /// <summary>하위 호환 — <see cref="IncludeCell"/>.</summary>
        public void IncludeFloorCell(UnityEngine.Vector3Int floorCell) => IncludeCell(floorCell);
    }
}
