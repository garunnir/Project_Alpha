using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary><see cref="TileMapCacheHub"/> topology 조회. 다유닛 안전을 위해 즉시 판정만 제공합니다.</summary>
    public sealed class MapTopologyQuery : IMapTopologyQuery
    {
        readonly TileMapCacheHub _hub;
        readonly float _cellSize;

        public MapTopologyQuery(TileMapCacheHub hub, float cellSize)
        {
            _hub = hub;
            _cellSize = cellSize > 0f ? cellSize : 1f;
        }

        public float CellSize => _cellSize;

        public bool TryGetCellTiles(int x, int z, int gridY, out IReadOnlyList<TileData> list)
        {
            if (_hub.TryGetCellTiles(x, z, gridY, out var hubList))
            {
                list = hubList;
                return true;
            }

            list = null;
            return false;
        }

        public bool CellHasSolidWall(int x, int z, int gridY)
        {
            if (!TryGetCellTiles(x, z, gridY, out var list))
                return false;

            return FloorMapIndex.CellHasSolidWall(list);
        }

        public bool CellHasOccupancy(int x, int z, int gridY) =>
            _hub.Topology.HasOccupancy(x, z, gridY);

        /// <summary>
        /// 타일 바닥 또는 **아래 셀의 얼어붙은 액체**. 얼음 지지는 여기서만 합성한다 —
        /// <see cref="FloorMapIndex"/>에 넣으면 building·space bake와 가려짐 입력까지 오염되고,
        /// 점유 인덱스는 셀 변경 시 전체 리빌드라 상변화마다 부를 수 없다.
        /// </summary>
        public bool CellHasFloor(int x, int z, int gridY) =>
            _hub.Topology.CellHasFloor(x, gridY, z)
            || MapLiquidQuery.ProvidesSolidSupport(new Vector3Int(x, gridY - 1, z));

        public bool TryGetEdgeBetween(Vector3Int cellA, Vector3Int cellB, out TileData edgeWall) =>
            _hub.TryGetEdgeBetween(cellA, cellB, out edgeWall);

        public bool TryGetEdgeSealingLateral(Vector3Int cellA, Vector3Int cellB, out TileData edgeWall) =>
            _hub.Topology.Index.TryGetEdgeSealingLateral(cellA, cellB, out edgeWall);

        public bool EdgeBlocksPassage(Vector3Int cellA, Vector3Int cellB) =>
            _hub.Topology.Index.EdgeBlocksPassage(cellA, cellB);
    }
}
