// ============================================================
// DigTileTargetResolver — AimWorldPoint / 카메라 레이 → HorizontalFace 굴착 타겟
// ============================================================

using UnityEngine;

namespace IsoTilemap
{
    public static class DigTileTargetResolver
    {
        const int PhysicsHitBufferSize = 16;
        static readonly RaycastHit[] PhysicsHits = new RaycastHit[PhysicsHitBufferSize];

        /// <summary>
        /// 조준 월드점(AimWorldPoint) → FloorFace → DigTileTarget.
        /// Excavate 기본 경로. 카메라 ScreenPointToRay 아님.
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

            return TryBuildTarget(hub, faceKey, prefabDb, out target);
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

            target = new DigTileTarget(walkableCell, faceTile, definition);
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
