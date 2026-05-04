using System;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// 인접 두 칸 사이의 수직 면. 앵커는 해당 축에서 좌표가 작은 쪽 셀, Face는 +X 또는 +Z 방향 이웃을 향합니다.
    /// </summary>
    public enum WallFace : byte
    {
        PosX = 0,
        PosZ = 1,
    }

    public readonly struct WallEdgeKey : IEquatable<WallEdgeKey>
    {
        public Vector3Int Anchor { get; }
        public WallFace Face { get; }

        public WallEdgeKey(Vector3Int anchor, WallFace face)
        {
            Anchor = anchor;
            Face = face;
        }

        public static WallEdgeKey FromEdgeTileIdentity(in TileIdentity id) =>
            new WallEdgeKey(id.GridPos, (WallFace)id.edgeFace);

        public bool Equals(WallEdgeKey other) => Anchor.Equals(other.Anchor) && Face == other.Face;

        public override bool Equals(object obj) => obj is WallEdgeKey other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Anchor.x, Anchor.y, Anchor.z, (byte)Face);

        /// <summary>
        /// a→b가 카드널 이웃일 때 공유 면 키. (y는 같아야 함.)
        /// </summary>
        public static bool TryBetween(Vector3Int a, Vector3Int b, out WallEdgeKey key)
        {
            key = default;
            if (a.y != b.y) return false;
            var d = b - a;
            if (d == Vector3Int.right) { key = new WallEdgeKey(a, WallFace.PosX); return true; }
            if (d == Vector3Int.left) { key = new WallEdgeKey(b, WallFace.PosX); return true; }
            if (d == Vector3Int.forward) { key = new WallEdgeKey(a, WallFace.PosZ); return true; }
            if (d == Vector3Int.back) { key = new WallEdgeKey(b, WallFace.PosZ); return true; }
            return false;
        }

        public Vector3Int NeighborCell() => Face == WallFace.PosX
            ? Anchor + Vector3Int.right
            : Anchor + Vector3Int.forward;

        public static Vector3Int StepTowardNeighbor(WallFace face) =>
            face == WallFace.PosX ? Vector3Int.right : Vector3Int.forward;

        public static void GetWorldPose(in WallEdgeKey key, float cellSize, out Vector3 position, out Quaternion rotation)
        {
            Vector3Int n = key.NeighborCell();
            Vector3 c0 = TileHelper.ConvertGridToWorldPos(key.Anchor, cellSize);
            Vector3 c1 = TileHelper.ConvertGridToWorldPos(n, cellSize);
            position = (c0 + c1) * 0.5f;
            Vector3 outward = new Vector3(n.x - key.Anchor.x, 0f, n.z - key.Anchor.z);
            if (outward.sqrMagnitude < 0.0001f)
                outward = Vector3.forward;
            else
                outward.Normalize();
            rotation = Quaternion.LookRotation(outward, Vector3.up);
        }
    }

    /// <summary>
    /// 월드 좌표에서 가장 가까운 카드널 벽 에지(<see cref="WallEdgeKey.GetWorldPose"/> 기준 중점)를 고릅니다.
    /// </summary>
    public static class WallEdgePicker
    {
        public static bool TryPickNearest(Vector3 world, float cellSize, out WallEdgeKey key)
        {
            key = default;
            cellSize = Mathf.Max(1e-4f, cellSize);

            int gy = Mathf.RoundToInt(world.y / cellSize);

            Vector3Int rough = TileHelper.ConvertWorldToGrid(world, cellSize);
            int cx = rough.x;
            int cz = rough.z;

            float bestSq = float.MaxValue;
            WallEdgeKey best = default;
            bool found = false;

            for (int dz = -3; dz <= 3; dz++)
            for (int dx = -3; dx <= 3; dx++)
            {
                Vector3Int ac = new Vector3Int(cx + dx, gy, cz + dz);
                ConsiderFace(ac, WallFace.PosX);
                ConsiderFace(ac, WallFace.PosZ);
            }

            if (!found) return false;
            key = best;
            return true;

            void ConsiderFace(Vector3Int ac, WallFace face)
            {
                WallEdgeKey k = new WallEdgeKey(ac, face);
                WallEdgeKey.GetWorldPose(k, cellSize, out Vector3 pose, out _);
                float sq = (world - pose).sqrMagnitude;
                if (sq >= bestSq) return;

                bestSq = sq;
                best = k;
                found = true;
            }
        }
    }
}
