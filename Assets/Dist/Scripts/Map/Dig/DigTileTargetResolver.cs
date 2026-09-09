// ============================================================
// DigTileTargetResolver — AimWorldPoint / combat clamp → HorizontalFace 굴착 타겟
// ============================================================

using UnityEngine;

namespace IsoTilemap
{
    public static class DigTileTargetResolver
    {
        const int PhysicsHitBufferSize = 16;
        static readonly RaycastHit[] PhysicsHits = new RaycastHit[PhysicsHitBufferSize];

        /// <summary>
        /// 전투 Excavate SSOT. AimWorldPoint → face.
        /// actorFeetWorld(발끝 transform) → 목표 walkable 셀 중심 월드 유클리드
        /// (<see cref="MapDigConsts.ActionRangeWorld"/>) 밖이면 액터→ideal 그리드 스텝 clamp.
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

            float actorFeetY = actorFeetWorld.y;
            Vector3Int actorWalkableCell = TileHelper.ConvertWorldToGrid(actorFeetWorld, cellSize);

            if (!TryResolveFromWorldPoint(
                    aimWorldPoint,
                    hub,
                    cellSize,
                    actorFeetY,
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

            // interactionDirFlat: clamp가 액터에 머물 때 XZ 폴백 (희귀).
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
        /// 조준 월드점(AimWorldPoint) → FloorFace → DigTileTarget.
        /// 사거리 clamp 없음. 카메라 ScreenPointToRay 아님.
        /// </summary>
        public static bool TryResolveFromWorldPoint(
            Vector3 worldPoint,
            TileMapCacheHub hub,
            float cellSize,
            float actorFeetWorldY,
            TilePrefabDB prefabDb,
            out DigTileTarget target)
        {
            target = default;
            if (hub == null)
                return false;

            if (!FloorFacePicker.TryPickFromHub(
                    hub,
                    worldPoint,
                    cellSize,
                    actorFeetWorldY,
                    cellEpsilonWorld: 0f,
                    out FloorFaceKey faceKey) &&
                !FloorFacePicker.TryPickNearest(worldPoint, cellSize, out faceKey))
            {
                return false;
            }

            if (TryBuildTarget(hub, faceKey, prefabDb, out target))
                return true;

            return TryBuildTargetFromWalkable(hub, faceKey.CellAbove, prefabDb, out target);
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
                actorFeetWorldY,
                prefabDb,
                out target);
        }

        /// <summary>
        /// 액터 발끝→ideal walkable. 월드 유클리드 반경 안인 마지막 셀(3축 Sign 스텝).
        /// </summary>
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
