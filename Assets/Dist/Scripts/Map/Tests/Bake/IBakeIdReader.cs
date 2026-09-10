// ============================================================
// IBakeIdReader — bake 결과 ID 읽기 계약 (구현 교체 가능)
// ============================================================
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap.Tests
{
    public interface IBakeIdReader
    {
        bool TryRead(Vector3Int walkableCell, out FloorIdSnapshot snapshot);

        /// <summary>모든 walkable floor 칸 → 스냅샷. Golden 비교용.</summary>
        IReadOnlyDictionary<Vector3Int, FloorIdSnapshot> CaptureAllFloors();
    }
}
