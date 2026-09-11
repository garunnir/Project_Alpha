// ============================================================
// BuildingIdBakeRules — buildingId bake 값·전파 원점 규칙 (SSOT)
// ============================================================
namespace IsoTilemap
{
    public static class BuildingIdBakeRules
    {
        /// <summary>bake 전파·merge·flood 시드 원점. 0·-1은 확장 원점 아님 — 0은 수신만.</summary>
        public static bool CanPropagateBuildingIdFrom(int buildingId) => buildingId > 0;

        /// <summary>
        /// 저장된/저작된 양수 buildingId — union·flood·Assign이 경계를 넘거나 덮지 않음.
        /// (로드 후 id를 지우고 재묶는 것은 알고리즘 위반.)
        /// </summary>
        public static bool IsHardPartitionBuildingId(int buildingId) => buildingId > 0;

        /// <summary>다른 양수 building floor — flood 통과·patch 덮어쓰기 차단.</summary>
        public static bool IsConflictingPropagableBuildingId(int floorBuildingId, int propagatingBuildingId) =>
            CanPropagateBuildingIdFrom(floorBuildingId) && floorBuildingId != propagatingBuildingId;

        public static bool ShouldPatchBuildingIdAtOccupiedCell(in TileIdentity id) =>
            TileIdentityUtil.IsStructural(id);

        /// <summary>
        /// outdoor(-1)·다른 하드 파티션(양수 id)은 merge·flood·증분이 덮어쓰지 않음.
        /// </summary>
        public static bool ShouldOverwriteBuildingIdForPropagation(int existing, int targetBuildingId)
        {
            if (IsImmutableOutdoorBuildingId(existing) || IsImmutableOutdoorBuildingId(targetBuildingId))
                return false;

            // 이미 파티션된 양수 id는 다른 id로 덮지 않음 (같은 id 재스탬프만 허용).
            if (IsHardPartitionBuildingId(existing) && existing != targetBuildingId)
                return false;

            return existing != targetBuildingId;
        }

        /// <summary>incident 타일 buildingId 기준 structural flood traverse 차단 (walkable 조회 없음).</summary>
        public static bool ShouldBlockBuildingFloodFromIncidentTile(in TileIdentity id, int propagatingBuildingId)
        {
            if (IsImmutableOutdoorBuildingId(id.buildingId) ||
                IsImmutableOutdoorBuildingId(propagatingBuildingId))
                return true;

            if (IsConflictingPropagableBuildingId(id.buildingId, propagatingBuildingId))
                return true;

            return false;
        }

        /// <summary>outdoor(-1) 타일·칸은 building component 시드·흡수 대상이 아님. 불변.</summary>
        public static bool IsImmutableOutdoorBuildingId(int buildingId) =>
            buildingId == TileIdentity.BuildingIdOutdoor;

        /// <summary>
        /// 하드 파티션(양수)·outdoor는 같은 id끼리만 union.
        /// 0↔양수 합치면 다른 파티션이 미할당 복도로 간접 병합됨.
        /// </summary>
        public static bool BlocksComponentUnion(int buildingIdA, int buildingIdB)
        {
            if (IsImmutableOutdoorBuildingId(buildingIdA) || IsImmutableOutdoorBuildingId(buildingIdB))
                return true;

            if (IsHardPartitionBuildingId(buildingIdA) || IsHardPartitionBuildingId(buildingIdB))
                return buildingIdA != buildingIdB;

            return false;
        }
    }
}
