using System;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// 맵 그리드의 단일 출처. <see cref="TileMapManager"/>가 로드·DTO 기준으로 구성합니다.
    /// </summary>
    public sealed class IsoWorldGrid : IWorldGrid
    {
        public float CellSize { get; private set; } = 1f;

        public event Action CellSizeChanged;

        public void ApplyCellSize(float cellSize)
        {
            float next = Mathf.Max(1e-4f, cellSize);
            if (Mathf.Approximately(CellSize, next))
                return;

            CellSize = next;
            CellSizeChanged?.Invoke();
        }

        public void ApplyFromMap(MapSaveJsonDto dto, float fallbackCellSize)
        {
            float fromDto = dto != null ? dto.gridCellSize : 0f;
            float resolved = fromDto > 1e-4f ? fromDto : fallbackCellSize;
            ApplyCellSize(resolved);
        }

        public Vector3Int WorldToCell(Vector3 world) =>
            TileHelper.ConvertWorldToGrid(world, CellSize);

        public Vector3 CellToWorld(Vector3Int cell) =>
            TileHelper.ConvertGridToWorldPos(cell, CellSize);

        public Vector3 CellToWorld(Vector3 cell) =>
            TileHelper.ConvertGridToWorldPos(cell, CellSize);
    }
}
