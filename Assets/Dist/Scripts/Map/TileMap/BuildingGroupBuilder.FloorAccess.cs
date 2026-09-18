// ============================================================
// BuildingGroupBuilder.FloorAccess — floor buildingId·roomId 읽기/쓰기
// ============================================================
using System.Collections.Generic;

namespace IsoTilemap
{
    public sealed partial class BuildingGroupBuilder
    {
        bool IsFloorBuildingUnassigned(int x, int cellY, int z) =>
            GetFloorBuildingId(x, cellY, z) == TileIdentity.BuildingIdUnassigned;

        int GetFloorBuildingId(int x, int cellY, int z)
        {
            if (!_topology.Index.TryGetFloorFaceForWalkableCell(x, cellY, z, out var face))
                return TileIdentity.BuildingIdUnassigned;

            return face.identity.buildingId;
        }
        int GetFloorRoomId(int x, int cellY, int z)
        {
            if (!_topology.Index.TryGetFloorFaceForWalkableCell(x, cellY, z, out var face))
                return 0;

            return face.identity.roomId;
        }
        void SetFloorBuildingRoom(int x, int cellY, int z, int buildingId, int roomId)
        {
            if (!_topology.Index.TryGetFloorFaceForWalkableCell(x, cellY, z, out var face))
                return;

            _model.PatchTileIdentity(face.tileDefId, buildingId, roomId);
        }
        /// <summary>Init footprint·floor horizontal union·orphan 전용. structural flood traverse에 사용 금지.</summary>
        bool IsPlazaOrTerrainFloor(int x, int z, int cellY)
        {
            if (cellY == _minCellY && _registry.IsPlazaXZ(x, z))
                return true;

            if (!_topology.Index.CellHasFloor(x, cellY, z))
                return false;

            return GetFloorBuildingId(x, cellY, z) == TileIdentity.BuildingIdTerrain;
        }
    }
}
