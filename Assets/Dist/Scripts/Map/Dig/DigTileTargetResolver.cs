// ============================================================
// DigTileTargetResolver — 카메라 레이 → HorizontalFace 굴착 타겟 (MVP)
// ============================================================

using UnityEngine;

namespace IsoTilemap
{
    public static class DigTileTargetResolver
    {
        const int PhysicsHitBufferSize = 16;
        static readonly RaycastHit[] PhysicsHits = new RaycastHit[PhysicsHitBufferSize];

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

            if (!FloorFacePicker.TryPickFromHub(
                    hub,
                    sampleWorld,
                    cellSize,
                    actorFeetWorldY,
                    cellEpsilonWorld: 0f,
                    out FloorFaceKey faceKey) &&
                !FloorFacePicker.TryPickNearest(sampleWorld, cellSize, out faceKey))
            {
                return false;
            }

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
