// ============================================================
// SpaceFloodResult — SpaceFloodFill3D AABB volume flood 산출
// ============================================================
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    public readonly struct SpaceFloodResult
    {
        /// <summary>AABB 안 volume 방문 셀 (floor + empty).</summary>
        public HashSet<Vector3Int> VisitedCells { get; }

        /// <summary>하위 호환: <see cref="VisitedCells"/>와 동일.</summary>
        public HashSet<Vector3Int> VisitedFloor => VisitedCells;

        public HashSet<int> BoundarySpaceIds { get; }

        public SpaceFloodResult(
            HashSet<Vector3Int> visitedCells,
            HashSet<int> boundarySpaceIds)
        {
            VisitedCells = visitedCells ?? new HashSet<Vector3Int>();
            BoundarySpaceIds = boundarySpaceIds ?? new HashSet<int>();
        }

        public static SpaceFloodResult Empty { get; } =
            new SpaceFloodResult(new HashSet<Vector3Int>(), new HashSet<int>());
    }
}
