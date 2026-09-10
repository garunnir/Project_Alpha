// ============================================================
// BakeIdReader — TileMapCacheHub public read API 위임 (유일 구현)
// ============================================================
using System.Collections.Generic;
using IsoTilemap;
using UnityEngine;

namespace IsoTilemap.Tests
{
    sealed class BakeIdReader : IBakeIdReader
    {
        readonly TileMapCacheHub _hub;

        public BakeIdReader(TileMapCacheHub hub)
        {
            _hub = hub;
        }

        public bool TryRead(Vector3Int walkableCell, out FloorIdSnapshot snapshot)
        {
            snapshot = default;
            if (_hub == null)
                return false;

            if (!_hub.TryGetFloorFaceForWalkableCell(
                    walkableCell.x, walkableCell.y, walkableCell.z, out TileData face))
            {
                return false;
            }

            int buildingId = face.identity.buildingId;
            int roomId = face.identity.roomId;
            int spaceId = 0;
            bool isOutdoor = false;

            if (_hub.Spaces.TryGetSpaceAtFloorCell(walkableCell, out int sid))
            {
                spaceId = sid;
                isOutdoor = _hub.Spaces.IsOutdoorSpace(sid);
            }
            else if (buildingId == TileIdentity.BuildingIdOutdoor)
            {
                isOutdoor = true;
            }

            snapshot = new FloorIdSnapshot(buildingId, roomId, spaceId, isOutdoor);
            return true;
        }

        public IReadOnlyDictionary<Vector3Int, FloorIdSnapshot> CaptureAllFloors()
        {
            var result = new Dictionary<Vector3Int, FloorIdSnapshot>();
            if (_hub == null)
                return result;

            foreach (var (x, cellY, z) in _hub.Topology.Index.EnumerateWalkableFloorCells())
            {
                var cell = new Vector3Int(x, cellY, z);
                if (TryRead(cell, out FloorIdSnapshot snap))
                    result[cell] = snap;
            }

            return result;
        }
    }
}
