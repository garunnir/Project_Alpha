// ============================================================
// ComponentBakeRules — component bake 단계 규칙 (buildingId 할당 전)
// ============================================================
namespace IsoTilemap
{
    public static class ComponentBakeRules
    {
        public static bool CanPropagateComponentFrom(int componentRoot) => componentRoot > 0;

        public static bool IsConflictingComponentRoot(int floorComponentRoot, int propagatingRoot) =>
            CanPropagateComponentFrom(floorComponentRoot) && floorComponentRoot != propagatingRoot;

        /// <summary>
        /// structural 6-dir/footprint 접촉 시 다른 root는 무시하지 않고 Union 대상.
        /// </summary>
        public static bool ShouldUnionOnStructuralContact(int existingRoot, int incomingRoot) =>
            CanPropagateComponentFrom(existingRoot) &&
            CanPropagateComponentFrom(incomingRoot) &&
            existingRoot != incomingRoot;

        /// <summary>
        /// outdoor(-1)·하드 파티션(양수) incident는 component flood traverse·tag·seed 제외.
        /// (AssignAll은 Reset 없이 파티션 보존 — Full Rebake만 양수를 지운 뒤 재묶음.)
        /// </summary>
        public static bool ShouldBlockComponentFloodFromIncidentTile(in TileIdentity id) =>
            BuildingIdBakeRules.IsImmutableOutdoorBuildingId(id.buildingId) ||
            BuildingIdBakeRules.IsHardPartitionBuildingId(id.buildingId);
    }
}

