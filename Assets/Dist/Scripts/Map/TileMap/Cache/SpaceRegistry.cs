// ============================================================
// SpaceRegistry — volume cell → SpaceId 역인덱스 및 bake 결과
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    public sealed class SpaceRegistry
    {
        readonly Dictionary<Vector3Int, int> _cellToSpaceId = new();
        readonly Dictionary<int, SpaceBakeResult> _spacesById = new();
        readonly Dictionary<int, HashSet<Vector3Int>> _volumeCellsBySpaceId = new();
        readonly Dictionary<int, HashSet<Vector3Int>> _floorCellsBySpaceId = new();
        int _nextSpaceId = 1;

        public void Clear()
        {
            _cellToSpaceId.Clear();
            _spacesById.Clear();
            _volumeCellsBySpaceId.Clear();
            _floorCellsBySpaceId.Clear();
            _nextSpaceId = 1;
        }

        public int AllocateSpaceId() => _nextSpaceId++;

        public bool TryGetSpaceAtFloorCell(Vector3Int cell, out int spaceId) =>
            _cellToSpaceId.TryGetValue(cell, out spaceId);

        public bool TryGetSpaceAtFloorCell(int cellY, int x, int z, out int spaceId) =>
            TryGetSpaceAtFloorCell(new Vector3Int(x, cellY, z), out spaceId);

        public bool TryGetSpace(int spaceId, out SpaceBakeResult result) =>
            _spacesById.TryGetValue(spaceId, out result);

        public bool IsOutdoorSpace(int spaceId) =>
            _spacesById.TryGetValue(spaceId, out var result) && result.IsOutdoor;

        public IReadOnlyCollection<int> SpaceIds => _spacesById.Keys;

        /// <summary>leak·가시성 band용 walkable floor 셀.</summary>
        public IReadOnlyCollection<Vector3Int> GetFloorCells(int spaceId)
        {
            if (_floorCellsBySpaceId.TryGetValue(spaceId, out var set))
                return set;

            return Array.Empty<Vector3Int>();
        }

        /// <summary>AABB volume 전체 (floor + empty).</summary>
        public IReadOnlyCollection<Vector3Int> GetVolumeCells(int spaceId)
        {
            if (_volumeCellsBySpaceId.TryGetValue(spaceId, out var set))
                return set;

            return Array.Empty<Vector3Int>();
        }

        public void AssignNew(
            int spaceId,
            int buildingId,
            RoomKey seedRoom,
            IEnumerable<Vector3Int> volumeCells,
            FloorMapIndex index)
        {
            if (spaceId <= 0)
                return;

            var result = new SpaceBakeResult(spaceId, buildingId, seedRoom);
            _spacesById[spaceId] = result;

            var volumeSet = new HashSet<Vector3Int>();
            var floorSet = new HashSet<Vector3Int>();
            AbsorbInto(spaceId, result, volumeCells, index, volumeSet, floorSet);
            _volumeCellsBySpaceId[spaceId] = volumeSet;
            _floorCellsBySpaceId[spaceId] = floorSet;
        }

        public void Absorb(
            int canonicalSpaceId,
            IEnumerable<Vector3Int> volumeCells,
            FloorMapIndex index)
        {
            if (canonicalSpaceId <= 0 || volumeCells == null)
                return;

            if (!_volumeCellsBySpaceId.TryGetValue(canonicalSpaceId, out var volumeSet))
            {
                volumeSet = new HashSet<Vector3Int>();
                _volumeCellsBySpaceId[canonicalSpaceId] = volumeSet;
            }

            if (!_floorCellsBySpaceId.TryGetValue(canonicalSpaceId, out var floorSet))
            {
                floorSet = new HashSet<Vector3Int>();
                _floorCellsBySpaceId[canonicalSpaceId] = floorSet;
            }

            _spacesById.TryGetValue(canonicalSpaceId, out var result);
            AbsorbInto(canonicalSpaceId, result, volumeCells, index, volumeSet, floorSet);
        }

        public void SetOutdoor(int spaceId, bool isOutdoor)
        {
            if (_spacesById.TryGetValue(spaceId, out var result))
                result.IsOutdoor = isOutdoor;
        }

        void AbsorbInto(
            int spaceId,
            SpaceBakeResult result,
            IEnumerable<Vector3Int> volumeCells,
            FloorMapIndex index,
            HashSet<Vector3Int> volumeSet,
            HashSet<Vector3Int> floorSet)
        {
            if (volumeCells == null)
                return;

            foreach (var cell in volumeCells)
            {
                volumeSet.Add(cell);
                _cellToSpaceId[cell] = spaceId;
                result?.IncludeCell(cell);

                if (index != null && index.CellHasFloor(cell.x, cell.y, cell.z))
                    floorSet.Add(cell);
            }
        }
    }
}
