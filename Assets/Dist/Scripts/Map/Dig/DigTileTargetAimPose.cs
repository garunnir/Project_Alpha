// ============================================================
// DigTileTargetAimPose — Dig 잠금 시 Sight/AimWorldPoint 월드 포즈 SSOT
// ============================================================

using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// 잠긴 <see cref="DigTileTarget"/> → 조준 월드점 (face / 바깥면 중심).
    /// SphereCast와 분리 — 홀드 잠금 SightDir용.
    /// </summary>
    public static class DigTileTargetAimPose
    {
        public static bool TryGetSightWorldPoint(
            in DigTileTarget target,
            float cellSize,
            Vector3 lookOriginWorld,
            out Vector3 aimWorld)
        {
            aimWorld = default;
            cellSize = Mathf.Max(1e-4f, cellSize);

            if (target.BreakKind == DigBreakKind.HorizontalFace)
            {
                FloorFaceKey.GetWorldPose(
                    FloorFaceKey.ForWalkableCell(target.WalkableCell),
                    cellSize,
                    out aimWorld,
                    out _);
                return true;
            }

            if (target.BreakKind != DigBreakKind.WalkableStratumBlock)
                return false;

            Vector3Int anchor = MapDigTerrainUtil.SupportBlockAnchor(target.WalkableCell);
            Vector3Int size = target.TargetTile.identity.sizeUnit;
            if (size.x < 1) size.x = 1;
            if (size.y < 1) size.y = 1;
            if (size.z < 1) size.z = 1;

            TileHelper.GetOccupiedCellWireBox(
                anchor,
                cellSize,
                size,
                out Vector3 center,
                out Vector3 extents);
            Vector3 half = extents * 0.5f;

            Vector3 toLook = lookOriginWorld - center;
            float bestFacing = float.NegativeInfinity;
            Vector3 bestFace = center;
            bool found = false;

            for (int i = 0; i < TileCubeFaceIdUtil.Count; i++)
            {
                var face = (TileCubeFaceId)i;
                Vector3 normal = TileCubeFaceIdUtil.WorldNormal(face);
                Vector3 faceCenter = center + new Vector3(
                    normal.x * half.x,
                    normal.y * half.y,
                    normal.z * half.z);

                float facing = 0f;
                if (toLook.sqrMagnitude > 1e-8f)
                    facing = Vector3.Dot(toLook.normalized, normal);

                if (!found || facing > bestFacing)
                {
                    found = true;
                    bestFacing = facing;
                    bestFace = faceCenter;
                }
            }

            aimWorld = bestFace;
            return found;
        }
    }
}
