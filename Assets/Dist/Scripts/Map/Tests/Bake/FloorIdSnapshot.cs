// ============================================================
// FloorIdSnapshot — walkable floor 칸의 bake ID 스냅샷 (읽기 전용 값)
// ============================================================
using System;
using UnityEngine;

namespace IsoTilemap.Tests
{
    public readonly struct FloorIdSnapshot : IEquatable<FloorIdSnapshot>
    {
        public int BuildingId { get; }
        public int RoomId { get; }
        public int SpaceId { get; }
        public bool IsOutdoor { get; }
        public bool HasSpace => SpaceId > 0;

        public FloorIdSnapshot(int buildingId, int roomId, int spaceId, bool isOutdoor)
        {
            BuildingId = buildingId;
            RoomId = roomId;
            SpaceId = spaceId;
            IsOutdoor = isOutdoor;
        }

        public bool Equals(FloorIdSnapshot other) =>
            BuildingId == other.BuildingId &&
            RoomId == other.RoomId &&
            SpaceId == other.SpaceId &&
            IsOutdoor == other.IsOutdoor;

        public override bool Equals(object obj) => obj is FloorIdSnapshot other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(BuildingId, RoomId, SpaceId, IsOutdoor);

        public override string ToString() =>
            $"b={BuildingId} r={RoomId} s={SpaceId} outdoor={IsOutdoor}";
    }

    public enum FloorIdField
    {
        BuildingId,
        RoomId,
        SpaceId,
        IsOutdoor,
    }
}
