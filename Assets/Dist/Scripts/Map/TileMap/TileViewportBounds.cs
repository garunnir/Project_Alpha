using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>카메라 ortho 뷰포트 → 월드 XZ → 청크 집합.</summary>
    public static class TileViewportBounds
    {
        public static void AppendCameraChunks(
            HashSet<Vector2Int> chunks,
            Camera camera,
            float orthographicSize,
            float cellSize,
            int chunkSize,
            int marginChunks,
            float groundPlaneY = 0f)
        {
            if (camera == null || chunks == null)
                return;

            if (!TryGetViewportWorldBoundsXZ(camera, orthographicSize, groundPlaneY,
                    out float minX, out float maxX, out float minZ, out float maxZ))
                return;

            AppendChunksForWorldBounds(chunks, minX, maxX, minZ, maxZ, cellSize, chunkSize, marginChunks);
        }

        public static void AppendCameraChunks(
            HashSet<Vector2Int> chunks,
            Camera camera,
            CinemachineCamera cinemachineCamera,
            float cellSize,
            int chunkSize,
            int marginChunks,
            float groundPlaneY = 0f)
        {
            float orthoSize = ResolveOrthographicSize(camera, cinemachineCamera);
            AppendCameraChunks(chunks, camera, orthoSize, cellSize, chunkSize, marginChunks, groundPlaneY);
        }

        public static void AppendPlayerChunks(
            HashSet<Vector2Int> chunks,
            Vector3Int playerCell,
            int chunkSize,
            int playerChunkRadius)
        {
            if (chunks == null)
                return;

            TileChunkCoord.AppendChunkNeighborhood(
                chunks,
                TileChunkCoord.FromCell(playerCell, chunkSize),
                playerChunkRadius);
        }

        public static float ResolveOrthographicSize(Camera camera, CinemachineCamera cinemachineCamera)
        {
            if (cinemachineCamera != null)
                return Mathf.Max(0.01f, cinemachineCamera.Lens.OrthographicSize);

            if (camera != null && camera.orthographic)
                return Mathf.Max(0.01f, camera.orthographicSize);

            return 10f;
        }

        private static bool TryGetViewportWorldBoundsXZ(
            Camera camera,
            float orthographicSize,
            Vector3[] cornerBuffer,
            float groundPlaneY,
            out float minX,
            out float maxX,
            out float minZ,
            out float maxZ)
        {
            minX = minZ = float.PositiveInfinity;
            maxX = maxZ = float.NegativeInfinity;

            Vector3[] corners = cornerBuffer ?? new Vector3[4];
            corners[0] = new Vector3(0f, 0f, 0f);
            corners[1] = new Vector3(1f, 0f, 0f);
            corners[2] = new Vector3(0f, 1f, 0f);
            corners[3] = new Vector3(1f, 1f, 0f);

            bool hitAny = false;
            for (int i = 0; i < corners.Length; i++)
            {
                Ray ray = camera.ViewportPointToRay(corners[i]);
                if (!TryIntersectGroundPlane(ray, groundPlaneY, out Vector3 hit))
                    continue;

                hitAny = true;
                minX = Mathf.Min(minX, hit.x);
                maxX = Mathf.Max(maxX, hit.x);
                minZ = Mathf.Min(minZ, hit.z);
                maxZ = Mathf.Max(maxZ, hit.z);
            }

            if (!hitAny)
            {
                float aspect = Mathf.Max(0.01f, camera.aspect);
                float halfHeight = Mathf.Max(0.01f, orthographicSize);
                float halfWidth = halfHeight * aspect;
                Vector3 pos = camera.transform.position;
                minX = pos.x - halfWidth;
                maxX = pos.x + halfWidth;
                minZ = pos.z - halfHeight;
                maxZ = pos.z + halfHeight;
                hitAny = true;
            }

            return hitAny;
        }

        private static bool TryGetViewportWorldBoundsXZ(
            Camera camera,
            float orthographicSize,
            float groundPlaneY,
            out float minX,
            out float maxX,
            out float minZ,
            out float maxZ) =>
            TryGetViewportWorldBoundsXZ(camera, orthographicSize, null, groundPlaneY, out minX, out maxX, out minZ, out maxZ);

        private static void AppendChunksForWorldBounds(
            HashSet<Vector2Int> chunks,
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            float cellSize,
            int chunkSize,
            int marginChunks)
        {
            cellSize = Mathf.Max(1e-4f, cellSize);
            chunkSize = Mathf.Max(1, chunkSize);

            Vector3Int minCell = TileHelper.ConvertWorldToGrid(new Vector3(minX, 0f, minZ), cellSize);
            Vector3Int maxCell = TileHelper.ConvertWorldToGrid(new Vector3(maxX, 0f, maxZ), cellSize);

            int minCx = TileChunkCoord.FromCell(minCell, chunkSize).x - marginChunks;
            int maxCx = TileChunkCoord.FromCell(maxCell, chunkSize).x + marginChunks;
            int minCz = TileChunkCoord.FromCell(minCell, chunkSize).y - marginChunks;
            int maxCz = TileChunkCoord.FromCell(maxCell, chunkSize).y + marginChunks;

            if (minCx > maxCx)
                (minCx, maxCx) = (maxCx, minCx);
            if (minCz > maxCz)
                (minCz, maxCz) = (maxCz, minCz);

            for (int cx = minCx; cx <= maxCx; cx++)
            {
                for (int cz = minCz; cz <= maxCz; cz++)
                    chunks.Add(new Vector2Int(cx, cz));
            }
        }

        private static bool TryIntersectGroundPlane(Ray ray, float planeY, out Vector3 hit)
        {
            hit = default;
            if (Mathf.Abs(ray.direction.y) < 1e-5f)
                return false;

            float t = (planeY - ray.origin.y) / ray.direction.y;
            if (t < 0f)
                return false;

            hit = ray.origin + ray.direction * t;
            return true;
        }
    }
}
