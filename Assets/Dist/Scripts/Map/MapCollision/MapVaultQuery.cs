// ============================================================
// MapVaultQuery — 전방 vault 후보 (Mantle 착지 우선 → thin CrossOver)
// ============================================================

using UnityEngine;

namespace IsoTilemap
{
    public enum VaultHeightClass : byte
    {
        Low = 0,
        High = 1,
    }

    public enum VaultCrossStyle : byte
    {
        CrossOver = 0,
        Mantle = 1,
    }

    public readonly struct VaultCandidate
    {
        public readonly VaultHeightClass Height;
        public readonly VaultCrossStyle Style;
        public readonly Vector3Int FeetCell;
        public readonly Vector3Int LandingFeetCell;
        public readonly Vector3 ApproachDirXZ;
        public readonly int WallSpanCells;

        public VaultCandidate(
            VaultHeightClass height,
            VaultCrossStyle style,
            Vector3Int feetCell,
            Vector3Int landingFeetCell,
            Vector3 approachDirXZ,
            int wallSpanCells)
        {
            Height = height;
            Style = style;
            FeetCell = feetCell;
            LandingFeetCell = landingFeetCell;
            ApproachDirXZ = approachDirXZ;
            WallSpanCells = wallSpanCells;
        }
    }

    /// <summary>
    /// 발밑·이동 방향으로 Mantle(착지)을 먼저 보고, 실패 시 얇은 edge CrossOver만 본다.
    /// 한 장애 = Low 또는 High 하나만 (상호 배타). 계약: docs/locomotion/VAULT.md
    /// </summary>
    public static class MapVaultQuery
    {
        const int MaxProbeY = 8;

        public static bool TryFindCandidate(
            IMapTopologyQuery query,
            Vector3 feetWorld,
            Vector3Int footprint,
            Vector3 moveDirXZ,
            out VaultCandidate candidate)
        {
            candidate = default;
            if (query == null)
                return false;

            float cellSize = query.CellSize > 0f ? query.CellSize : 1f;
            footprint = MapTopologyCollisionResolver.ClampFootprint(footprint);
            Vector3Int feetCell = TileHelper.ConvertWorldToGrid(feetWorld, cellSize);
            return TryFindCandidate(query, feetCell, footprint, moveDirXZ, out candidate);
        }

        /// <summary>
        /// <paramref name="feetCell"/>는 locomotion·<see cref="CharacterState.GridPos"/>와 동일 계약(walkable 발밑).
        /// </summary>
        public static bool TryFindCandidate(
            IMapTopologyQuery query,
            Vector3Int feetCell,
            Vector3Int footprint,
            Vector3 moveDirXZ,
            out VaultCandidate candidate)
        {
            candidate = default;
            if (query == null)
                return false;

            footprint = MapTopologyCollisionResolver.ClampFootprint(footprint);

            if (!TryCardinalStep(moveDirXZ, out Vector3Int step, out Vector3 approach))
                return false;

            Vector3Int ahead = feetCell + step;

            if (TryMantle(query, feetCell, ahead, footprint, approach, out candidate))
                return true;

            if (TryCrossOver(query, feetCell, ahead, footprint, approach, out candidate))
                return true;

            return false;
        }

        /// <summary>
        /// Mantle 높이: deltaY &lt; footprint.y → Low, deltaY ≥ footprint.y → High.
        /// CrossOver는 sizeUnit.y 1/2로만 분류한다.
        /// </summary>
        public static VaultHeightClass ClassifyMantleHeight(int deltaY, int footprintY)
        {
            int sy = Mathf.Max(1, footprintY);
            return deltaY >= sy ? VaultHeightClass.High : VaultHeightClass.Low;
        }

        /// <summary>
        /// 달리기 자동 vault: 장애 높이 ≤ 플레이어 footprint.y 절반.
        /// Mantle=<see cref="VaultCandidate.WallSpanCells"/> (deltaY), CrossOver=edge sizeUnit.y.
        /// </summary>
        public static bool IsAutoSprintEligible(in VaultCandidate candidate, Vector3Int footprint)
        {
            footprint = MapTopologyCollisionResolver.ClampFootprint(footprint);
            int maxSpanCells = Mathf.Max(1, footprint.y / 2);
            return candidate.WallSpanCells > 0 && candidate.WallSpanCells <= maxSpanCells;
        }

        static bool TryMantle(
            IMapTopologyQuery query,
            Vector3Int feetCell,
            Vector3Int ahead,
            Vector3Int footprint,
            Vector3 approach,
            out VaultCandidate candidate)
        {
            candidate = default;
            int sy = footprint.y;

            if (!TryPickBestMantleLanding(
                    query,
                    feetCell,
                    footprint,
                    out int landingY,
                    out int landX,
                    out int landZ,
                    ahead.x,
                    ahead.z))
                return false;

            int deltaY = landingY - feetCell.y;
            Vector3Int landing = new Vector3Int(landX, landingY, landZ);
            candidate = new VaultCandidate(
                ClassifyMantleHeight(deltaY, sy),
                VaultCrossStyle.Mantle,
                feetCell,
                landing,
                approach,
                deltaY);
            return true;
        }

        /// <summary>ahead(1칸) → feet(벽 밀착) 순. 전방 스캔 깊이: <see cref="VaultConsts.MantleProbeMaxAheadCells"/>.</summary>
        static bool TryPickBestMantleLanding(
            IMapTopologyQuery query,
            Vector3Int feetCell,
            Vector3Int footprint,
            out int landingY,
            out int landX,
            out int landZ,
            int aheadX,
            int aheadZ)
        {
            landingY = 0;
            landX = 0;
            landZ = 0;
            int bestPriority = int.MaxValue;
            bool found = false;

            ConsiderMantleColumn(
                query,
                feetCell,
                footprint,
                aheadX,
                aheadZ,
                0,
                ref found,
                ref landingY,
                ref landX,
                ref landZ,
                ref bestPriority);
            if (aheadX != feetCell.x || aheadZ != feetCell.z)
            {
                ConsiderMantleColumn(
                    query,
                    feetCell,
                    footprint,
                    feetCell.x,
                    feetCell.z,
                    1,
                    ref found,
                    ref landingY,
                    ref landX,
                    ref landZ,
                    ref bestPriority);
            }

            return found;
        }

        static void ConsiderMantleColumn(
            IMapTopologyQuery query,
            Vector3Int feetCell,
            Vector3Int footprint,
            int x,
            int z,
            int priority,
            ref bool found,
            ref int landingY,
            ref int landX,
            ref int landZ,
            ref int bestPriority)
        {
            if (!TryFindLowestMantleLanding(query, feetCell, x, z, footprint, out int y, out int lx, out int lz))
                return;
            if (!found || y < landingY || (y == landingY && priority < bestPriority))
            {
                found = true;
                landingY = y;
                landX = lx;
                landZ = lz;
                bestPriority = priority;
            }
        }

        static bool TryFindLowestMantleLanding(
            IMapTopologyQuery query,
            Vector3Int feetCell,
            int x,
            int z,
            Vector3Int footprint,
            out int landingY,
            out int landX,
            out int landZ)
        {
            landingY = 0;
            landX = x;
            landZ = z;
            for (int dy = 1; dy <= MaxProbeY; dy++)
            {
                int y = feetCell.y + dy;
                if (!IsValidMantleLanding(query, feetCell, x, z, y, footprint))
                    continue;

                landingY = y;
                return true;
            }

            return false;
        }

        static bool IsValidMantleLanding(
            IMapTopologyQuery query,
            Vector3Int feetCell,
            int x,
            int z,
            int landingY,
            Vector3Int footprint)
        {
            int sy = footprint.y;
            int deltaY = landingY - feetCell.y;
            // ThickWall(size.y=1)+상단 floor = deltaY 1. 양수 단차만 요구.
            if (deltaY < 1)
                return false;

            if (!query.CellHasFloor(x, z, landingY))
                return false;

            if (LandingSurfaceOccupied(query, x, z, landingY))
                return false;

            // 착지 높이에서 approach 열→착지 열 경로(SlimWall 등 blocking edge).
            if (MapTopologyGridSegment.CrossesBlockingSegment(
                    query,
                    feetCell.x,
                    feetCell.z,
                    x,
                    z,
                    landingY))
                return false;

            // 몸 footprint 높이만큼만 헤드룸 (FootprintVolumeBlocks와 패리티 + 점유·edge incident).
            if (CountClearSpanUp(query, x, z, landingY) < sy)
                return false;

            var landingFeet = new Vector3Int(x, landingY, z);
            if (MapTopologyCollisionResolver.FootprintVolumeBlocks(query, landingFeet, footprint))
                return false;

            if (FootprintVaultHeadroomBlocks(query, landingFeet, footprint))
                return false;

            return true;
        }

        static bool TryCrossOver(
            IMapTopologyQuery query,
            Vector3Int feetCell,
            Vector3Int ahead,
            Vector3Int footprint,
            Vector3 approach,
            out VaultCandidate candidate)
        {
            candidate = default;
            if (!query.TryGetEdgeSealingLateral(feetCell, ahead, out TileData edge) ||
                !TileCollisionFlagsUtil.EdgeBlocksPassage(edge))
                return false;

            int span = edge.identity.sizeUnit.y;
            if (span != 1 && span != 2)
                return false;

            Vector3Int landing = new Vector3Int(ahead.x, feetCell.y, ahead.z);
            if (!query.CellHasFloor(landing.x, landing.z, landing.y))
                return false;

            if (!IsValidCrossOverLanding(query, landing, footprint))
                return false;

            VaultHeightClass height = span == 1 ? VaultHeightClass.Low : VaultHeightClass.High;
            candidate = new VaultCandidate(
                height,
                VaultCrossStyle.CrossOver,
                feetCell,
                landing,
                approach,
                span);
            return true;
        }

        static bool IsValidCrossOverLanding(
            IMapTopologyQuery query,
            Vector3Int landingFeet,
            Vector3Int footprint)
        {
            int sy = footprint.y;

            if (LandingSurfaceOccupied(query, landingFeet.x, landingFeet.z, landingFeet.y))
                return false;

            if (CountClearSpanUp(query, landingFeet.x, landingFeet.z, landingFeet.y) < sy)
                return false;

            if (MapTopologyCollisionResolver.FootprintVolumeBlocks(query, landingFeet, footprint))
                return false;

            return !FootprintVaultHeadroomBlocks(query, landingFeet, footprint);
        }

        /// <summary>walkable 셀 위 OccupiedCell(가구·상자 등). 바닥 HorizontalFace는 <see cref="TryGetCellTiles"/>에 없음.</summary>
        static bool LandingSurfaceOccupied(IMapTopologyQuery query, int x, int z, int landingY) =>
            query.TryGetCellTiles(x, z, landingY, out var list) && list != null && list.Count > 0;

        /// <summary>발 높이 위(dy≥1) footprint XZ 밴드 — SlimWall incident·비차단 점유 포함.</summary>
        static bool FootprintVaultHeadroomBlocks(
            IMapTopologyQuery query,
            Vector3Int feetCell,
            Vector3Int footprint)
        {
            footprint = MapTopologyCollisionResolver.ClampFootprint(footprint);
            int sx = footprint.x;
            int sy = footprint.y;
            int sz = footprint.z;
            if (sy <= 1)
                return false;

            int ax = feetCell.x - (sx - 1) / 2;
            int az = feetCell.z - (sz - 1) / 2;
            for (int dy = 1; dy < sy; dy++)
            {
                int y = feetCell.y + dy;
                for (int dx = 0; dx < sx; dx++)
                {
                    for (int dz = 0; dz < sz; dz++)
                    {
                        int x = ax + dx;
                        int z = az + dz;
                        if (query.CellHasSolidWall(x, z, y) || query.CellHasOccupancy(x, z, y))
                            return true;
                    }
                }
            }

            return false;
        }

        static int CountClearSpanUp(IMapTopologyQuery query, int x, int z, int startY)
        {
            int span = 0;
            for (int i = 0; i < MaxProbeY; i++)
            {
                int y = startY + i;
                if (query.CellHasSolidWall(x, z, y))
                    break;

                // 착지 바닥 셀(i==0)은 walkable face occupancy 허용. 그 위는 점유·edge incident 차단.
                if (i > 0 && query.CellHasOccupancy(x, z, y))
                    break;

                span++;
            }

            return span;
        }

        static bool TryCardinalStep(Vector3 moveDirXZ, out Vector3Int step, out Vector3 approach)
        {
            step = default;
            approach = default;
            moveDirXZ.y = 0f;
            if (moveDirXZ.sqrMagnitude < 1e-6f)
                return false;

            if (Mathf.Abs(moveDirXZ.x) >= Mathf.Abs(moveDirXZ.z))
            {
                int sx = moveDirXZ.x >= 0f ? 1 : -1;
                step = new Vector3Int(sx, 0, 0);
                approach = new Vector3(sx, 0f, 0f);
            }
            else
            {
                int sz = moveDirXZ.z >= 0f ? 1 : -1;
                step = new Vector3Int(0, 0, sz);
                approach = new Vector3(0f, 0f, sz);
            }

            return true;
        }
    }
}
