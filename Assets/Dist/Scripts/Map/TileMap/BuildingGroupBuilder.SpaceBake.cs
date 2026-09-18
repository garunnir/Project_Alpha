// ============================================================
// BuildingGroupBuilder.SpaceBake — AABB volume Space flood·isOutdoor bake
// ============================================================
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    public sealed partial class BuildingGroupBuilder
    {
        /// <summary>전체 맵 space bake (load·Full Rebake 전용). 증분 편집은 <see cref="BakeSpacesForSlices"/>.</summary>
        void BakeAllSpaces() => BakeSpacesForSlices(null);

        /// <summary>
        /// <paramref name="slices"/> == null이면 전체 맵(<see cref="SpaceRegistry.Clear"/>).
        /// 아니면 그 (buildingId, cellY) slice와 겹치는 space만 지우고(<see cref="SpaceRegistry.RemoveSpacesInSlices"/>),
        /// 그 slice의 room만 시드로 재flood — 나머지 space는 그대로 둔다.
        /// 새/재flood된 space만 leak 재평가(<see cref="SpaceLeakEvaluator"/>) — 손 안 댄 space는 스킵.
        /// </summary>
        void BakeSpacesForSlices(HashSet<(int buildingId, int cellY)> slices)
        {
            var registry = _hub.Spaces.Registry;
            var index = _topology.Index;

            if (slices == null)
                registry.Clear();
            else
                registry.RemoveSpacesInSlices(slices);

            var touchedSpaceIds = slices != null ? new HashSet<int>() : null;

            _roomKeyScratch.Clear();
            _hub.Rooms.CollectRoomKeys(FloorRoomBfsProfile.Occlusion, _roomKeyScratch);
            _roomKeyScratch.Sort(CompareRoomKeys);

            foreach (var roomKey in _roomKeyScratch)
            {
                if (!BuildingIdBakeRules.CanPropagateBuildingIdFrom(roomKey.BuildingId))
                    continue;

                if (slices != null && !slices.Contains((roomKey.BuildingId, roomKey.CellY)))
                    continue;

                if (!_hub.Rooms.TryGet(roomKey, FloorRoomBfsProfile.Occlusion, out var occlusion) ||
                    occlusion.Visited == null)
                    continue;

                if (!_registry.TryGetBuildingExtent(roomKey.BuildingId, out var extent) || !extent.HasBounds)
                    continue;

                foreach (var (x, z) in occlusion.Visited)
                {
                    var cell = new Vector3Int(x, roomKey.CellY, z);
                    if (_registry.IsPlazaFloor(cell.y, cell.x, cell.z))
                        continue;

                    if (registry.TryGetSpaceAtFloorCell(cell, out _))
                        continue;

                    SpaceFloodResult flood = SpaceFloodFill3D.Run(
                        index, registry, extent, _registry, cell, roomKey.BuildingId);

                    if (flood.VisitedCells.Count == 0)
                        continue;

                    int touchedId;
                    if (flood.BoundarySpaceIds.Count > 0)
                    {
                        int canonical = int.MaxValue;
                        foreach (int boundaryId in flood.BoundarySpaceIds)
                        {
                            if (boundaryId < canonical)
                                canonical = boundaryId;
                        }

                        registry.Absorb(canonical, flood.VisitedCells, index);
                        touchedId = canonical;
                    }
                    else
                    {
                        int spaceId = registry.AllocateSpaceId();
                        registry.AssignNew(spaceId, roomKey.BuildingId, roomKey, flood.VisitedCells, index);
                        touchedId = spaceId;
                    }

                    touchedSpaceIds?.Add(touchedId);
                }
            }

            IEnumerable<int> idsToEvaluate = slices == null ? registry.SpaceIds : touchedSpaceIds;
            foreach (int spaceId in idsToEvaluate)
            {
                if (!registry.TryGetSpace(spaceId, out var space))
                    continue;

                if (!_registry.TryGetBuildingExtent(space.BuildingId, out var extent))
                {
                    registry.SetOutdoor(spaceId, true);
                    continue;
                }

                bool outdoor = SpaceLeakEvaluator.Evaluate(
                    registry.GetFloorCells(spaceId),
                    space.BuildingId,
                    extent,
                    index);
                registry.SetOutdoor(spaceId, outdoor);
            }
        }

        static int CompareRoomKeys(RoomKey a, RoomKey b)
        {
            int c = a.BuildingId.CompareTo(b.BuildingId);
            if (c != 0) return c;
            c = a.CellY.CompareTo(b.CellY);
            if (c != 0) return c;
            return a.RoomId.CompareTo(b.RoomId);
        }
    }
}
