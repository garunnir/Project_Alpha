using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    public interface IMapTopologyQuery
    {
        float CellSize { get; }

        bool TryGetCellTiles(int x, int z, int gridY, out IReadOnlyList<TileData> list);

        bool CellHasSolidWall(int x, int z, int gridY);

        /// <summary>점유 인덱스 — OccupiedCell·VerticalFace·HorizontalFace incident 포함.</summary>
        bool CellHasOccupancy(int x, int z, int gridY);

        bool CellHasFloor(int x, int z, int gridY);

        bool TryGetEdgeBetween(Vector3Int cellA, Vector3Int cellB, out TileData edgeWall);

        /// <summary>
        /// 측면 seal 타일 — 배치 Y 또는 size.y가 덮는 cellY. 통과 판정은 <see cref="EdgeBlocksPassage"/>.
        /// </summary>
        bool TryGetEdgeSealingLateral(Vector3Int cellA, Vector3Int cellB, out TileData edgeWall);

        /// <summary>
        /// 측면 통과 차단 — VerticalFace <c>size.y</c>가 덮는 cellY 포함.
        /// 배치 전용 조회는 <see cref="TryGetEdgeBetween"/>.
        /// </summary>
        bool EdgeBlocksPassage(Vector3Int cellA, Vector3Int cellB);
    }
}
