using UnityEngine;

namespace IsoTilemap
{
    /// <summary>맵·타일·스트리밍이 공유하는 월드↔그리드 규칙.</summary>
    public interface IWorldGrid
    {
        float CellSize { get; }

        Vector3Int WorldToCell(Vector3 world);
        Vector3 CellToWorld(Vector3Int cell);
        Vector3 CellToWorld(Vector3 cell);
    }
}
