// ============================================================
// BuildingPlayerOcclusionResolver — 카메라↔플레이어 시선상 가리는 buildingId 집합
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>마지막 <see cref="BuildingPlayerOcclusionResolver.ResolveBlockingBuildingIds"/> 시선 샘플 스냅샷.</summary>
    public readonly struct SightLineBuildingDebugSnapshot
    {
        public bool IsValid { get; }
        public Vector3 CameraWorld { get; }
        public Vector3 PlayerWorld { get; }
        public IReadOnlyCollection<Vector3Int> SampledCells { get; }
        public IReadOnlyCollection<Vector3Int> BlockingCells { get; }
        public IReadOnlyCollection<int> BlockingBuildingIds { get; }

        public SightLineBuildingDebugSnapshot(
            bool isValid,
            Vector3 cameraWorld,
            Vector3 playerWorld,
            IReadOnlyCollection<Vector3Int> sampledCells,
            IReadOnlyCollection<Vector3Int> blockingCells,
            IReadOnlyCollection<int> blockingBuildingIds)
        {
            IsValid = isValid;
            CameraWorld = cameraWorld;
            PlayerWorld = playerWorld;
            SampledCells = sampledCells ?? Array.Empty<Vector3Int>();
            BlockingCells = blockingCells ?? Array.Empty<Vector3Int>();
            BlockingBuildingIds = blockingBuildingIds ?? Array.Empty<int>();
        }

        public static SightLineBuildingDebugSnapshot Empty =>
            new(false, Vector3.zero, Vector3.zero,
                Array.Empty<Vector3Int>(), Array.Empty<Vector3Int>(), Array.Empty<int>());
    }

    /// <summary>
    /// 카메라↔플레이어 3D 시선 샘플 경로에
    /// <see cref="TileMapCacheHub.CellHasOccupancy"/> 점유셀(바닥·벽 포함)이 있으면 해당 buildingId를 차단합니다.
    /// 플레이어 점유셀은 차단 판정에서 제외합니다(발밑·인접 면 오탐 방지).
    /// </summary>
    public sealed class BuildingPlayerOcclusionResolver
    {
        static readonly Vector3Int[] CardinalNeighbors =
        {
            Vector3Int.right, Vector3Int.back, Vector3Int.left, Vector3Int.forward
        };

        readonly TileMapCacheHub _hub;
        readonly float _cellSize;
        readonly Func<Camera> _resolveCamera;
        readonly List<TileData> _tilesScratch = new();
        readonly HashSet<Vector3Int> _cellsOnSegment = new();
        readonly HashSet<Vector3Int> _blockingCellsScratch = new();
        readonly HashSet<Vector3Int> _blockingEvaluatedCells = new();
        readonly HashSet<Vector3Int> _playerExcludedCellsScratch = new();

        public BuildingPlayerOcclusionResolver(
            TileMapCacheHub hub,
            float cellSize,
            Func<Camera> resolveCamera)
        {
            _hub = hub;
            _cellSize = cellSize > 0f ? cellSize : 1f;
            _resolveCamera = resolveCamera;
        }

        public SightLineBuildingDebugSnapshot LastDebug { get; private set; } = SightLineBuildingDebugSnapshot.Empty;

        public bool TryGetCameraWorld(out Vector3 cameraWorld)
        {
            Camera cam = _resolveCamera?.Invoke();
            if (cam == null)
            {
                cameraWorld = Vector3.zero;
                return false;
            }

            cameraWorld = cam.transform.position;
            return true;
        }

        public void ResolveBlockingBuildingIds(
            Vector3 playerWorld,
            IReadOnlyCollection<Vector3Int> playerOccupiedCells,
            HashSet<int> output,
            int excludeBuildingId = 0)
        {
            output.Clear();
            _blockingCellsScratch.Clear();

            if (!TryGetCameraWorld(out Vector3 cameraWorld))
            {
                LastDebug = SightLineBuildingDebugSnapshot.Empty;
                return;
            }

            CollectBlockingOnSightSegment(
                cameraWorld, playerWorld, playerOccupiedCells, output, excludeBuildingId);
        }

        void CollectBlockingOnSightSegment(
            Vector3 cameraWorld,
            Vector3 playerWorld,
            IReadOnlyCollection<Vector3Int> playerOccupiedCells,
            HashSet<int> output,
            int excludeBuildingId)
        {
            _cellsOnSegment.Clear();
            _blockingEvaluatedCells.Clear();
            _playerExcludedCellsScratch.Clear();
            if (playerOccupiedCells != null)
            {
                foreach (Vector3Int cell in playerOccupiedCells)
                    _playerExcludedCellsScratch.Add(cell);
            }

            float span = Vector3.Distance(cameraWorld, playerWorld);
            int steps = Mathf.Max(1, Mathf.CeilToInt(span / (_cellSize * 0.5f)));

            for (int i = 0; i <= steps; i++)
            {
                float t = steps == 0 ? 0f : i / (float)steps;
                Vector3 p = Vector3.Lerp(cameraWorld, playerWorld, t);
                Vector3Int sampleCell = OccupiedCellCoord.GridAtSightSampleHeight(p, _cellSize);

                _cellsOnSegment.Add(sampleCell);

                if (!_blockingEvaluatedCells.Add(sampleCell))
                    continue;

                if (_playerExcludedCellsScratch.Contains(sampleCell))
                    continue;

                if (!TryAddBlockingAtOccupiedSampleCell(sampleCell, output, excludeBuildingId))
                    continue;

                _blockingCellsScratch.Add(sampleCell);
            }

            LastDebug = new SightLineBuildingDebugSnapshot(
                true,
                cameraWorld,
                playerWorld,
                _cellsOnSegment,
                _blockingCellsScratch,
                output);
        }

        /// <summary>경로상 점유셀 — 인덱스에 있고 buildingId가 있으면 차단.</summary>
        bool TryAddBlockingAtOccupiedSampleCell(
            Vector3Int sampleCell,
            HashSet<int> output,
            int excludeBuildingId)
        {
            if (!_hub.CellHasOccupancy(sampleCell.x, sampleCell.z, sampleCell.y))
                return false;

            return CollectBuildingIdsAtOccupiedCell(sampleCell, output, excludeBuildingId);
        }

        bool CollectBuildingIdsAtOccupiedCell(
            Vector3Int cell,
            HashSet<int> output,
            int excludeBuildingId)
        {
            bool contributed = false;

            if (_hub.TryGetFloorBuildingRoom(
                    cell.y, cell.x, cell.z, out int floorBuildingId, out _))
            {
                contributed |= TryAddBuildingId(floorBuildingId, output, excludeBuildingId);
            }

            if (_hub.TryGetCellTiles(cell.x, cell.z, cell.y, out var cellTiles))
            {
                for (int i = 0; i < cellTiles.Count; i++)
                {
                    contributed |= TryAddBuildingId(
                        cellTiles[i].identity.buildingId, output, excludeBuildingId);
                }
            }

            _tilesScratch.Clear();
            AppendIncidentFacesAtCell(cell, _tilesScratch);
            for (int i = 0; i < _tilesScratch.Count; i++)
            {
                contributed |= TryAddBuildingId(
                    _tilesScratch[i].identity.buildingId, output, excludeBuildingId);
            }

            return contributed;
        }

        void AppendIncidentFacesAtCell(Vector3Int cell, List<TileData> appendTo)
        {
            if (_hub.TryGetHorizontalFaceBetween(cell + Vector3Int.down, cell, out TileData belowFace))
                appendTo.Add(belowFace);

            if (_hub.TryGetHorizontalFaceBetween(cell, cell + Vector3Int.up, out TileData aboveFace))
                appendTo.Add(aboveFace);

            for (int i = 0; i < CardinalNeighbors.Length; i++)
            {
                Vector3Int neighbor = cell + CardinalNeighbors[i];
                if (_hub.TryGetEdgeSealingLateral(cell, neighbor, out TileData edge))
                    appendTo.Add(edge);
            }
        }

        static bool TryAddBuildingId(int buildingId, HashSet<int> output, int excludeBuildingId)
        {
            if (buildingId <= 0 || buildingId == excludeBuildingId)
                return false;

            output.Add(buildingId);
            return true;
        }
    }
}
