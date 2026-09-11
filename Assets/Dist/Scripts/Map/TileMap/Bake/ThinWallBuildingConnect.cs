// ============================================================
// ThinWallBuildingConnect — VerticalFace(ThinWall) building 연결 규칙 SSOT
// ============================================================
using System;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// ThinWall끼리 building component union에 참여하는 연결 판정.
    /// (a) 동일 WallFace + 같은 Y cardinal 옆칸
    /// (b) 동일 WallFace + 같은 edge 라인 Y±1
    /// (c) 점유셀 1칸 공유 (코너)
    /// </summary>
    public static class ThinWallBuildingConnect
    {
        public static bool AreConnected(in WallEdgeKey a, in WallEdgeKey b)
        {
            if (a.Equals(b))
                return false;

            if (a.Face == b.Face)
            {
                if (AreCardinalNeighborsSameY(a, b))
                    return true;

                if (AreStackedOnSameEdgeLine(a, b))
                    return true;
            }

            return ShareOccupiedCell(a, b);
        }

        public static bool AreConnected(in TileIdentity a, in TileIdentity b)
        {
            if (!TileIdentityUtil.IsVerticalFace(a) || !TileIdentityUtil.IsVerticalFace(b))
                return false;

            return AreConnected(
                WallEdgeKey.FromWallTileIdentity(a),
                WallEdgeKey.FromWallTileIdentity(b));
        }

        static bool AreCardinalNeighborsSameY(in WallEdgeKey a, in WallEdgeKey b)
        {
            if (a.Anchor.y != b.Anchor.y)
                return false;

            Vector3Int d = b.Anchor - a.Anchor;
            if (a.Face == WallFace.PosX)
                return d.x == 0 && Math.Abs(d.z) == 1;

            return d.z == 0 && Math.Abs(d.x) == 1;
        }

        static bool AreStackedOnSameEdgeLine(in WallEdgeKey a, in WallEdgeKey b)
        {
            if (a.Anchor.x != b.Anchor.x || a.Anchor.z != b.Anchor.z)
                return false;

            return Math.Abs(a.Anchor.y - b.Anchor.y) == 1;
        }

        static bool ShareOccupiedCell(in WallEdgeKey a, in WallEdgeKey b)
        {
            Vector3Int a0 = a.Anchor;
            Vector3Int a1 = a.NeighborCell();
            Vector3Int b0 = b.Anchor;
            Vector3Int b1 = b.NeighborCell();

            return a0 == b0 || a0 == b1 || a1 == b0 || a1 == b1;
        }
    }
}
