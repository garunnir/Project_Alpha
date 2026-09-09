// ============================================================
// DigTileTargetResolver — AimWorldPoint → 셀 중심·바깥면 기준 Dig 타겟
// ============================================================

using UnityEngine;

namespace IsoTilemap
{
    public static class DigTileTargetResolver
    {
        const int PhysicsHitBufferSize = 16;
        static readonly RaycastHit[] PhysicsHits = new RaycastHit[PhysicsHitBufferSize];

        /// <summary>
        /// 전투 Excavate SSOT. AimWorldPoint → diggable (face / stratum 블록).
        /// 픽은 셀·큐브 바깥면 중심 근접; 사거리 밖이면 액터→ideal 그리드 스텝 clamp.
        /// </summary>
        public static bool TryResolveFromCombatAim(
            Vector3 aimWorldPoint,
            Vector3 interactionDirFlat,
            Vector3 actorFeetWorld,
            TileMapCacheHub hub,
            float cellSize,
            TilePrefabDB prefabDb,
            out DigTileTarget target)
        {
            target = default;
            if (hub == null)
                return false;

            Vector3Int actorWalkableCell = TileHelper.ConvertWorldToGrid(actorFeetWorld, cellSize);

            if (!TryResolveFromWorldPoint(
                    aimWorldPoint,
                    hub,
                    cellSize,
                    actorFeetWorld,
                    prefabDb,
                    out DigTileTarget ideal))
            {
                return false;
            }

            if (MapDigConsts.IsWithinActionRangeWorld(actorFeetWorld, ideal.WalkableCell, cellSize))
            {
                target = ideal;
                return true;
            }

            Vector3Int clamped = ClampWalkableTowardIdeal(
                actorFeetWorld,
                actorWalkableCell,
                ideal.WalkableCell,
                cellSize);

            if (clamped == actorWalkableCell &&
                interactionDirFlat.sqrMagnitude > 1e-6f)
            {
                Vector3 flat = interactionDirFlat;
                flat.y = 0f;
                if (flat.sqrMagnitude > 1e-6f)
                {
                    flat.Normalize();
                    float span = MapDigConsts.ActionRangeWorld(cellSize);
                    Vector3 offsetWorld = actorFeetWorld + flat * span;
                    Vector3Int offsetCell = TileHelper.ConvertWorldToGrid(offsetWorld, cellSize);
                    if (offsetCell != actorWalkableCell)
                    {
                        clamped = ClampWalkableTowardIdeal(
                            actorFeetWorld,
                            actorWalkableCell,
                            offsetCell,
                            cellSize);
                    }
                }
            }

            return TryBuildTargetFromWalkable(hub, clamped, prefabDb, out target);
        }

        /// <summary>
        /// 조준 월드점 → 주변 diggable 중 셀/바깥면 중심이 가장 가까운 타겟.
        /// 사거리 clamp 없음. 카메라 ScreenPointToRay 아님.
        /// </summary>
        public static bool TryResolveFromWorldPoint(
            Vector3 worldPoint,
            TileMapCacheHub hub,
            float cellSize,
            float actorFeetWorldY,
            TilePrefabDB prefabDb,
            out DigTileTarget target) =>
            TryResolveFromWorldPoint(
                worldPoint,
                hub,
                cellSize,
                new Vector3(worldPoint.x, actorFeetWorldY, worldPoint.z),
                prefabDb,
                out target);

        /// <summary>
        /// 조준 월드점 + lookOrigin(발/몸, 바깥면 facing용) → DigTileTarget.
        /// </summary>
        public static bool TryResolveFromWorldPoint(
            Vector3 worldPoint,
            TileMapCacheHub hub,
            float cellSize,
            Vector3 lookOriginWorld,
            TilePrefabDB prefabDb,
            out DigTileTarget target)
        {
            target = default;
            if (hub == null)
                return false;

            cellSize = Mathf.Max(1e-4f, cellSize);
            Vector3Int seed = TileHelper.ConvertWorldToGrid(worldPoint, cellSize);
            int radius = MapDigConsts.DigPickSearchRadiusCells;

            float bestScore = float.MaxValue;
            DigTileTarget best = default;
            bool found = false;

            for (int dy = -radius; dy <= radius; dy++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                Vector3Int walkable = new Vector3Int(seed.x + dx, seed.y + dy, seed.z + dz);
                if (!TryBuildTargetFromWalkable(hub, walkable, prefabDb, out DigTileTarget candidate))
                    continue;

                float score = ScoreCandidate(worldPoint, lookOriginWorld, candidate, cellSize);
                if (score >= bestScore)
                    continue;

                bestScore = score;
                best = candidate;
                found = true;
            }

            if (!found)
                return false;

            target = best;
            return true;
        }

        /// <summary>레거시/유틸: 카메라 스크린 레이 → 샘플 월드점 → <see cref="TryResolveFromWorldPoint"/>.</summary>
        public static bool TryResolve(
            Camera camera,
            Vector2 screenPos,
            TileMapCacheHub hub,
            float cellSize,
            float maxRayDistance,
            LayerMask hitMask,
            float actorFeetWorldY,
            TilePrefabDB prefabDb,
            out DigTileTarget target)
        {
            target = default;
            if (camera == null || hub == null)
                return false;

            Ray ray = camera.ScreenPointToRay(screenPos);
            Vector3 sampleWorld = ray.origin + ray.direction * maxRayDistance;

            int hitCount = Physics.RaycastNonAlloc(
                ray,
                PhysicsHits,
                maxRayDistance,
                hitMask,
                QueryTriggerInteraction.Collide);

            float bestDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = PhysicsHits[i];
                if (hit.collider == null || hit.distance >= bestDistance)
                    continue;

                bestDistance = hit.distance;
                sampleWorld = hit.point;
            }

            return TryResolveFromWorldPoint(
                sampleWorld,
                hub,
                cellSize,
                new Vector3(sampleWorld.x, actorFeetWorldY, sampleWorld.z),
                prefabDb,
                out target);
        }

        static float ScoreCandidate(
            Vector3 aimWorld,
            Vector3 lookOriginWorld,
            in DigTileTarget candidate,
            float cellSize)
        {
            if (candidate.BreakKind == DigBreakKind.HorizontalFace)
            {
                FloorFaceKey.GetWorldPose(
                    FloorFaceKey.ForWalkableCell(candidate.WalkableCell),
                    cellSize,
                    out Vector3 facePose,
                    out _);
                return (aimWorld - facePose).sqrMagnitude;
            }

            Vector3Int anchor = MapDigTerrainUtil.SupportBlockAnchor(candidate.WalkableCell);
            Vector3Int size = candidate.TargetTile.identity.sizeUnit;
            if (size.x < 1) size.x = 1;
            if (size.y < 1) size.y = 1;
            if (size.z < 1) size.z = 1;

            TileHelper.GetOccupiedCellWireBox(anchor, cellSize, size, out Vector3 center, out Vector3 extents);
            Vector3 half = extents * 0.5f;

            float best = float.MaxValue;
            for (int i = 0; i < TileCubeFaceIdUtil.Count; i++)
            {
                var face = (TileCubeFaceId)i;
                Vector3 normal = TileCubeFaceIdUtil.WorldNormal(face);
                Vector3 faceCenter = center + new Vector3(
                    normal.x * half.x,
                    normal.y * half.y,
                    normal.z * half.z);

                float distSq = (aimWorld - faceCenter).sqrMagnitude;

                // 조준점이 면 바깥쪽이면 가점(안쪽·뒷면 페널티)
                if (Vector3.Dot(aimWorld - center, normal) < 0f)
                    distSq += cellSize * cellSize;

                // 플레이어를 향한 바깥면 선호
                Vector3 toLook = lookOriginWorld - center;
                if (toLook.sqrMagnitude > 1e-8f)
                {
                    float facing = Vector3.Dot(toLook.normalized, normal);
                    distSq -= facing * cellSize * cellSize * MapDigConsts.DigOuterFaceFacingWeight;
                }

                if (distSq < best)
                    best = distSq;
            }

            return best;
        }

        static Vector3Int ClampWalkableTowardIdeal(
            Vector3 actorFeetWorld,
            Vector3Int actorWalkableCell,
            Vector3Int ideal,
            float cellSize)
        {
            if (MapDigConsts.IsWithinActionRangeWorld(actorFeetWorld, ideal, cellSize))
                return ideal;

            Vector3Int lastInRange = actorWalkableCell;
            Vector3Int cur = actorWalkableCell;
            int maxSteps = MapDigConsts.DigActionRangeCells * 4 + 8;
            for (int step = 0; step < maxSteps; step++)
            {
                int rdx = ideal.x - cur.x;
                int rdy = ideal.y - cur.y;
                int rdz = ideal.z - cur.z;
                if (rdx == 0 && rdy == 0 && rdz == 0)
                    break;

                cur = new Vector3Int(
                    cur.x + MathSign(rdx),
                    cur.y + MathSign(rdy),
                    cur.z + MathSign(rdz));

                if (!MapDigConsts.IsWithinActionRangeWorld(actorFeetWorld, cur, cellSize))
                    break;

                lastInRange = cur;
                if (cur.x == ideal.x && cur.y == ideal.y && cur.z == ideal.z)
                    break;
            }

            return lastInRange;
        }

        static int MathSign(int v) =>
            v > 0 ? 1 : v < 0 ? -1 : 0;

        static bool TryBuildTargetFromWalkable(
            TileMapCacheHub hub,
            Vector3Int walkableCell,
            TilePrefabDB prefabDb,
            out DigTileTarget target)
        {
            if (TryBuildFaceTarget(hub, walkableCell, prefabDb, out target))
                return true;

            return TryBuildStratumBlockTarget(hub, walkableCell, prefabDb, out target);
        }

        static bool TryBuildFaceTarget(
            TileMapCacheHub hub,
            Vector3Int walkableCell,
            TilePrefabDB prefabDb,
            out DigTileTarget target) =>
            TryBuildTarget(
                hub,
                FloorFaceKey.ForWalkableCell(walkableCell),
                prefabDb,
                out target);

        static bool TryBuildStratumBlockTarget(
            TileMapCacheHub hub,
            Vector3Int walkableCell,
            TilePrefabDB prefabDb,
            out DigTileTarget target)
        {
            target = default;
            if (!MapDigTerrainUtil.TryGetSupportBlockForWalkable(
                    hub,
                    walkableCell,
                    out TileData blockTile,
                    out TileDefinition definition))
            {
                return false;
            }

            target = new DigTileTarget(
                walkableCell,
                DigBreakKind.WalkableStratumBlock,
                blockTile,
                definition);
            return true;
        }

        static bool TryBuildTarget(
            TileMapCacheHub hub,
            FloorFaceKey faceKey,
            TilePrefabDB prefabDb,
            out DigTileTarget target)
        {
            target = default;
            Vector3Int walkableCell = faceKey.CellAbove;
            if (!hub.TryGetHorizontalFaceBetween(faceKey.CellBelow, walkableCell, out TileData faceTile))
                return false;

            TileDefinition definition = ResolveDefinition(prefabDb, faceTile.identity.PrefabId);
            if (!TileFlags.IsDiggableTarget(definition))
                return false;

            target = new DigTileTarget(
                walkableCell,
                DigBreakKind.HorizontalFace,
                faceTile,
                definition);
            return true;
        }

        static TileDefinition ResolveDefinition(TilePrefabDB prefabDb, string prefabId)
        {
            if (prefabDb != null && prefabDb.TryGetDefinition(prefabId, out TileDefinition fromDb))
                return fromDb;

            TilePrefabDB.TryResolveDefinition(prefabId, out TileDefinition resolved);
            return resolved;
        }
    }
}
