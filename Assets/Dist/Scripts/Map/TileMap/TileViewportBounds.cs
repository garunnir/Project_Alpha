using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>카메라 ortho·화면비 → 지면 XZ AABB → 청크 집합.</summary>
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

            if (!TryGetOrthoFootprintBoundsXZ(
                    camera, orthographicSize, groundPlaneY,
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

        /// <summary>풀 피크 추정용 — ortho AABB가 XZ에 덮는 최대 청크 반경(보수적).</summary>
        public static int ComputeCameraChunkRadius(
            float orthographicSize,
            float aspect,
            float cellSize,
            int chunkSize,
            int marginChunks)
        {
            float chunkWorld = Mathf.Max(1, chunkSize) * Mathf.Max(1e-4f, cellSize);
            float halfW = Mathf.Max(0.01f, orthographicSize) * Mathf.Max(1f, aspect);
            float halfH = Mathf.Max(0.01f, orthographicSize);
            float axisSpan = halfW + halfH;
            int orthoRadius = Mathf.CeilToInt(axisSpan / chunkWorld);
            return Mathf.Max(0, marginChunks) + orthoRadius;
        }

        public static float ResolveOrthographicSize(Camera camera, CinemachineCamera cinemachineCamera)
        {
            if (cinemachineCamera != null)
                return Mathf.Max(0.01f, cinemachineCamera.Lens.OrthographicSize);

            if (camera != null && camera.orthographic)
                return Mathf.Max(0.01f, camera.orthographicSize);

            return 10f;
        }

        private static bool TryGetOrthoFootprintBoundsXZ(
            Camera camera,
            float orthographicSize,
            float groundPlaneY,
            out float minX,
            out float maxX,
            out float minZ,
            out float maxZ)
        {
            minX = minZ = float.PositiveInfinity;
            maxX = maxZ = float.NegativeInfinity;

            float aspect = Mathf.Max(0.01f, camera.aspect);
            float halfW = Mathf.Max(0.01f, orthographicSize) * aspect;
            float halfH = Mathf.Max(0.01f, orthographicSize);

            Vector3 origin = ResolveViewOriginOnGround(camera, groundPlaneY);
            Vector3 right = FlattenToGround(camera.transform.right);
            Vector3 up = FlattenToGround(camera.transform.up);

            if (right.sqrMagnitude < 1e-8f)
                right = FlattenToGround(camera.transform.forward);
            if (up.sqrMagnitude < 1e-8f)
                up = new Vector3(-right.z, 0f, right.x);

            ExpandBounds(ref minX, ref maxX, ref minZ, ref maxZ, groundPlaneY, origin + right * halfW + up * halfH);
            ExpandBounds(ref minX, ref maxX, ref minZ, ref maxZ, groundPlaneY, origin + right * halfW - up * halfH);
            ExpandBounds(ref minX, ref maxX, ref minZ, ref maxZ, groundPlaneY, origin - right * halfW + up * halfH);
            ExpandBounds(ref minX, ref maxX, ref minZ, ref maxZ, groundPlaneY, origin - right * halfW - up * halfH);

            return minX <= maxX && minZ <= maxZ;
        }

        private static void ExpandBounds(
            ref float minX,
            ref float maxX,
            ref float minZ,
            ref float maxZ,
            float groundPlaneY,
            Vector3 point)
        {
            point.y = groundPlaneY;
            minX = Mathf.Min(minX, point.x);
            maxX = Mathf.Max(maxX, point.x);
            minZ = Mathf.Min(minZ, point.z);
            maxZ = Mathf.Max(maxZ, point.z);
        }

        private static Vector3 ResolveViewOriginOnGround(Camera camera, float groundPlaneY)
        {
            Vector3 origin = camera.transform.position;
            if (TryIntersectGroundPlane(new Ray(origin, camera.transform.forward), groundPlaneY, out Vector3 hit))
                return hit;

            return new Vector3(origin.x, groundPlaneY, origin.z);
        }

        private static Vector3 FlattenToGround(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-8f ? v.normalized : Vector3.zero;
        }

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
            marginChunks = Mathf.Max(0, marginChunks);

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
