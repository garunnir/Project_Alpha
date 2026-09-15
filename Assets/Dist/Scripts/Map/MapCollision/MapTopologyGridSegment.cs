using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// 그리드 셀 경로 순회·막힘 검사. Bresenham DDA 대각 스텝은 x-first cardinal 분해로 EdgeWall을 검사합니다.
    /// </summary>
    public static class MapTopologyGridSegment
    {
        /// <summary>
        /// from→to 한 스텝( cardinal 또는 대각 1칸 )의 Wall 셀·EdgeWall 경계를 검사합니다.
        /// </summary>
        public static bool CrossesBlockingBetween(
            IMapTopologyQuery query,
            Vector3Int from,
            Vector3Int to)
        {
            if (from == to)
                return false;

            int dx = to.x - from.x;
            int dz = to.z - from.z;

            if (Mathf.Abs(dx) + Mathf.Abs(dz) == 1)
                return CellOrEdgeBlocks(query, from, to);

            if (Mathf.Abs(dx) == 1 && Mathf.Abs(dz) == 1)
            {
                var mid = new Vector3Int(to.x, from.y, from.z);
                return CellOrEdgeBlocks(query, from, mid)
                    || CellOrEdgeBlocks(query, mid, to);
            }

            return CrossesBlockingSegment(query, from.x, from.z, to.x, to.z, from.y);
        }

        /// <summary>
        /// (x0,z0)→(x1,z1) Bresenham 경로 전체를 순회하며 Wall·EdgeWall을 검사합니다.
        /// </summary>
        public static bool CrossesBlockingSegment(
            IMapTopologyQuery query,
            int x0,
            int z0,
            int x1,
            int z1,
            int gridY)
        {
            if (x0 == x1 && z0 == z1)
                return false;

            int dx = Mathf.Abs(x1 - x0);
            int dz = Mathf.Abs(z1 - z0);
            int sx = x0 < x1 ? 1 : -1;
            int sz = z0 < z1 ? 1 : -1;
            int err = dx - dz;

            int x = x0;
            int z = z0;
            var prev = new Vector3Int(x, gridY, z);

            while (x != x1 || z != z1)
            {
                int stepFromX = prev.x;
                int stepFromZ = prev.z;

                int e2 = 2 * err;
                if (e2 > -dz)
                {
                    err -= dz;
                    x += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    z += sz;
                }

                var stepTo = new Vector3Int(x, gridY, z);

                if (stepTo.x != stepFromX && stepTo.z != stepFromZ)
                {
                    var mid = new Vector3Int(stepTo.x, gridY, stepFromZ);
                    if (CellOrEdgeBlocks(query, prev, mid))
                        return true;
                    if (CellOrEdgeBlocks(query, mid, stepTo))
                        return true;
                }
                else if (stepTo.x != stepFromX || stepTo.z != stepFromZ)
                {
                    if (CellOrEdgeBlocks(query, prev, stepTo))
                        return true;
                }

                prev = stepTo;
            }

            return false;
        }

        static bool CellOrEdgeBlocks(IMapTopologyQuery query, Vector3Int from, Vector3Int to)
        {
            if (query.CellHasSolidWall(to.x, to.z, to.y))
                return true;

            return query.EdgeBlocksPassage(from, to);
        }
    }
}
