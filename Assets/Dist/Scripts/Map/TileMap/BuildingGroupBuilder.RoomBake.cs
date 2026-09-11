// ============================================================
// BuildingGroupBuilder.RoomBake — room BFS·perimeter·edge wall 인덱스
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    public sealed partial class BuildingGroupBuilder
    {
        void BakeAllRooms()
        {
            // buildingId는 유지한 채 room만 재계산 (하드 파티션 AssignAll·로드 경로).
            ClearAllStructuralRoomIds();

            var slices = new HashSet<(int buildingId, int cellY)>();

            foreach (var tile in _model.TilesSnapshot)
            {
                if (!TileIdentityUtil.IsFloorTile(tile.identity))
                    continue;

                int buildingId = tile.identity.buildingId;
                if (!BuildingIdBakeRules.CanPropagateBuildingIdFrom(buildingId))
                    continue;

                slices.Add((buildingId, FloorFaceKey.FromFloorTileIdentity(tile.identity).CellAbove.y));
            }

            foreach (var (buildingId, cellY) in slices)
                BakeRoomsForSlice(buildingId, cellY, null);

            TagPerimeterForSlices(slices);
            IndexEdgeWallsForSlices(slices);
        }

        void ClearAllStructuralRoomIds()
        {
            _model.ForEachRuntimeTileMutating(tile =>
            {
                if (!TileIdentityUtil.IsStructural(tile.identity))
                    return;

                if (BuildingIdBakeRules.IsImmutableOutdoorBuildingId(tile.identity.buildingId))
                    return;

                if (tile.identity.roomId == 0)
                    return;

                _model.PatchTileIdentity(tile.tileDefId, tile.identity.buildingId, 0);
            });
        }
        void IndexEdgeWallsForSlices(HashSet<(int buildingId, int cellY)> slices)
        {
            foreach (var (buildingId, cellY) in slices)
                IndexEdgeWallsForSlice(buildingId, cellY);
        }
        void IndexEdgeWallsForSlice(int buildingId, int cellY)
        {
            _model.FaceBinder.CopyWallFacesTo(_wallFaceScratch);
            for (int i = 0; i < _wallFaceScratch.Count; i++)
            {
                var edge = _wallFaceScratch[i];
                var edgeKey = WallEdgeKey.FromWallTileIdentity(edge.identity);
                if (edgeKey.Anchor.y != cellY)
                    continue;

                var roomKeys = new HashSet<RoomKey>();
                TryAddRoomKeyAtFloorCell(edgeKey.CellA, cellY, roomKeys);
                TryAddRoomKeyAtFloorCell(edgeKey.CellB, cellY, roomKeys);

                foreach (var roomKey in roomKeys)
                {
                    if (roomKey.BuildingId != buildingId)
                        continue;

                    _registry.RegisterEdgeForRoom(roomKey, edge.tileDefId);
                }
            }
        }
        void TryAddRoomKeyAtFloorCell(Vector3Int cell, int cellY, HashSet<RoomKey> roomKeys)
        {
            if (cell.y != cellY)
                return;

            int b = GetFloorBuildingId(cell.x, cellY, cell.z);
            int r = GetFloorRoomId(cell.x, cellY, cell.z);
            if (b > 0 && r > 0)
                roomKeys.Add(new RoomKey(b, cellY, r));
        }
        void BakeRoomsForSlice(
            int buildingId,
            int cellY,
            HashSet<(int x, int z, int y)> extraSeeds)
        {
            var visitedAnchors = new HashSet<(int x, int z)>();
            int nextRoomId = 1;

            void TrySeed(int x, int z)
            {
                if (visitedAnchors.Contains((x, z)))
                    return;

                if (GetFloorBuildingId(x, cellY, z) != buildingId)
                    return;

                if (!_topology.Index.CellHasFloor(x, cellY, z))
                    return;

                if (GetFloorRoomId(x, cellY, z) != 0)
                    return;

                var (occlusion, visibility) = RunRoomBfsAt(cellY, x, z, buildingId);

                if (occlusion.Visited.Count == 0)
                    return;

                int roomId = nextRoomId++;
                var key = new RoomKey(buildingId, cellY, roomId);

                foreach (var (vx, vz) in occlusion.Visited)
                {
                    visitedAnchors.Add((vx, vz));
                    SetFloorBuildingRoom(vx, cellY, vz, buildingId, roomId);
                }

                StoreRoomBfsProfiles(cellY, key, occlusion, visibility);
            }

            foreach (var (x, z, b) in _model.EnumerateOccupiedCells())
            {
                if (b != cellY)
                    continue;

                TrySeed(x, z);
            }

            if (extraSeeds != null)
            {
                foreach (var (x, z, b) in extraSeeds)
                {
                    if (b != cellY)
                        continue;

                    TrySeed(x, z);
                }
            }
        }
        void ClearRoomIdsOnSlice(int buildingId, int cellY)
        {
            _model.ForEachRuntimeTileMutating(tile =>
            {
                if (!TileIdentityUtil.IsFloorTile(tile.identity))
                    return;

                var key = FloorFaceKey.FromFloorTileIdentity(tile.identity);
                if (key.CellAbove.y != cellY || tile.identity.buildingId != buildingId)
                    return;

                _model.PatchTileIdentity(tile.tileDefId, buildingId, 0);
            });
        }
        void TagPerimeterForSlices(HashSet<(int buildingId, int cellY)> slices)
        {
            foreach (var (buildingId, cellY) in slices)
                TagPerimeterForSlice(buildingId, cellY);
        }
        void TagPerimeterForSlice(int buildingId, int cellY)
        {
            _model.ForEachRuntimeTileMutating(tile =>
            {
                if (!TileIdentityUtil.IsWallLike(tile.identity))
                    return;

                int tileCellY = OccupiedCellCoord.PrimaryCellFromIdentity(tile.identity).y;
                if (tileCellY != cellY)
                    return;

                var pos = tile.identity.GridPos;

                if (!TryGetAdjacentFloorRoom(pos, out int adjBuilding, out int adjRoom))
                    return;

                if (adjBuilding != buildingId)
                    return;

                _model.PatchTileIdentity(tile.tileDefId, buildingId, adjRoom);
            });
        }
        bool TryGetAdjacentFloorRoom(Vector3Int cell, out int buildingId, out int roomId)
        {
            buildingId = 0;
            roomId = 0;

            foreach (var d in CardinalDirs)
            {
                int nx = cell.x + d.x;
                int nz = cell.z + d.z;
                int cellY = cell.y;

                if (!_topology.Index.CellHasFloor(nx, cellY, nz))
                    continue;

                int r = GetFloorRoomId(nx, cellY, nz);
                int b = GetFloorBuildingId(nx, cellY, nz);
                if (b > 0 && r > 0)
                {
                    buildingId = b;
                    roomId = r;
                    return true;
                }
            }

            return false;
        }
        void CollectRoomKeysNearCell(Vector3Int cell, HashSet<RoomKey> keys)
        {
            CollectRoomKeysAt(cell.x, cell.y, cell.z, keys);

            foreach (var d in CardinalDirs)
            {
                var n = cell + d;
                CollectRoomKeysAt(n.x, n.y, n.z, keys);
            }

            int below = cell.y - 1;
            int above = cell.y + 1;
            if (below >= _minCellY)
                CollectRoomKeysAt(cell.x, below, cell.z, keys);
            if (above <= _maxCellY)
                CollectRoomKeysAt(cell.x, above, cell.z, keys);
        }
        void CollectRoomKeysAt(int x, int cellY, int z, HashSet<RoomKey> keys)
        {
            int buildingId = GetFloorBuildingId(x, cellY, z);
            int roomId = GetFloorRoomId(x, cellY, z);
            if (buildingId > 0 && roomId > 0)
                keys.Add(new RoomKey(buildingId, cellY, roomId));
        }
        void TryAddRoomKeyFromTile(TileData tile, HashSet<RoomKey> keys)
        {
            if (TileIdentityUtil.IsFloorTile(tile.identity) && tile.identity.buildingId > 0 && tile.identity.roomId > 0)
            {
                var key = FloorFaceKey.FromFloorTileIdentity(tile.identity);
                keys.Add(new RoomKey(tile.identity.buildingId, key.CellAbove.y, tile.identity.roomId));
                return;
            }

            if (TileIdentityUtil.IsWallLike(tile.identity))
            {
                var pos = tile.identity.GridPos;
                CollectRoomKeysNearCell(pos, keys);
            }
        }
        bool TryBakeRoomFromSeed(int buildingId, int cellY, int seedX, int seedZ)
        {
            var (occlusion, visibility) = RunRoomBfsAt(cellY, seedX, seedZ, buildingId);

            if (occlusion.Visited == null || occlusion.Visited.Count == 0)
                return false;

            int roomId = FindExistingRoomIdInVisited(buildingId, cellY, occlusion.Visited);
            bool isNewRoom = roomId == 0;
            if (isNewRoom)
                roomId = GetNextRoomIdForSlice(buildingId, cellY);

            var key = new RoomKey(buildingId, cellY, roomId);

            foreach (var (vx, vz) in occlusion.Visited)
            {
                if (GetFloorRoomId(vx, cellY, vz) == 0)
                    SetFloorBuildingRoom(vx, cellY, vz, buildingId, roomId);
            }

            if (isNewRoom || !_hub.Rooms.TryGet(key, FloorRoomBfsProfile.Occlusion, out _))
                StoreRoomBfsProfiles(cellY, key, occlusion, visibility);

            return true;
        }
        (FloorBfsResult occlusion, FloorBfsResult visibility) RunRoomBfsAt(
            int cellY, int seedX, int seedZ, int buildingId)
        {
            var occlusion = FloorRoomFloodFill.Run(
                _topology.Index, cellY, seedX, seedZ, collectEmptyNeighbors: false, buildingId);
            var visibility = FloorRoomFloodFill.Run(
                _topology.Index, cellY, seedX, seedZ, collectEmptyNeighbors: true, buildingId);
            return (occlusion, visibility);
        }
        void StoreRoomBfsProfiles(
            int cellY,
            RoomKey key,
            FloorBfsResult occlusion,
            FloorBfsResult visibility)
        {
            _hub.Rooms.Store(key, FloorRoomBfsProfile.Occlusion, occlusion);
            _hub.CellYGeometry.RegisterFootprint(cellY, FloorRoomBfsProfile.Occlusion, occlusion);
            _hub.Rooms.Store(key, FloorRoomBfsProfile.Visibility, visibility);
            _hub.CellYGeometry.RegisterFootprint(cellY, FloorRoomBfsProfile.Visibility, visibility);
        }
        int FindExistingRoomIdInVisited(int buildingId, int cellY, HashSet<(int x, int z)> visited)
        {
            foreach (var (vx, vz) in visited)
            {
                if (GetFloorBuildingId(vx, cellY, vz) != buildingId)
                    continue;

                int roomId = GetFloorRoomId(vx, cellY, vz);
                if (roomId > 0)
                    return roomId;
            }

            return 0;
        }
        int GetNextRoomIdForSlice(int buildingId, int cellY)
        {
            int max = 0;
            foreach (var tile in _model.TilesSnapshot)
            {
                if (!TileIdentityUtil.IsFloorTile(tile.identity))
                    continue;

                var key = FloorFaceKey.FromFloorTileIdentity(tile.identity);
                if (key.CellAbove.y != cellY || tile.identity.buildingId != buildingId)
                    continue;

                max = Math.Max(max, tile.identity.roomId);
            }

            return max + 1;
        }
    }
}
