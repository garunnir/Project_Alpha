// ============================================================
// TileCubeFaceId — 큐브 6면 슬롯 (그리드 축)
// ============================================================

using UnityEngine;

namespace IsoTilemap
{
    /// <summary>+Y/+Z/+X 기준. 왼쪽만 다르게 = West만 다른 텍스처.</summary>
    public enum TileCubeFaceId : byte
    {
        Up = 0,
        Down = 1,
        North = 2,
        South = 3,
        East = 4,
        West = 5,
    }

    public static class TileCubeFaceIdUtil
    {
        public const int Count = 6;

        /// <summary>월드 축 normal. MeshBuilder·Dig 바깥면 픽 SSOT.</summary>
        public static Vector3 WorldNormal(TileCubeFaceId face) =>
            face switch
            {
                TileCubeFaceId.Up => Vector3.up,
                TileCubeFaceId.Down => Vector3.down,
                TileCubeFaceId.North => Vector3.forward,
                TileCubeFaceId.South => Vector3.back,
                TileCubeFaceId.East => Vector3.right,
                TileCubeFaceId.West => Vector3.left,
                _ => Vector3.up,
            };
    }
}
