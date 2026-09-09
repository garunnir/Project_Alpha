// ============================================================
// TileCubeMeshBuilder — 6면 각각 아틀라스 UV인 단위 큐브 (1 mesh)
// ============================================================

using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// 면마다 버텍스 분리·UV 칸만 다름. 서브메쉬/인스턴스 6 아님.
    /// </summary>
    public static class TileCubeMeshBuilder
    {
        public static Mesh Build(in TileCubeFaceUvSet uvs)
        {
            var mesh = new Mesh { name = "TileCubeFaceMesh" };

            Vector3[] corners =
            {
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f),
            };

            var verts = new Vector3[24];
            var norms = new Vector3[24];
            var meshUvs = new Vector2[24];
            var tris = new int[36];

            int v = 0;
            int t = 0;

            // +Y Up
            EmitFace(
                corners[3], corners[7], corners[6], corners[2],
                Vector3.up, uvs.Up, verts, norms, meshUvs, tris, ref v, ref t);
            // -Y Down
            EmitFace(
                corners[0], corners[1], corners[5], corners[4],
                Vector3.down, uvs.Down, verts, norms, meshUvs, tris, ref v, ref t);
            // +Z North
            EmitFace(
                corners[4], corners[5], corners[6], corners[7],
                Vector3.forward, uvs.North, verts, norms, meshUvs, tris, ref v, ref t);
            // -Z South
            EmitFace(
                corners[1], corners[0], corners[3], corners[2],
                Vector3.back, uvs.South, verts, norms, meshUvs, tris, ref v, ref t);
            // +X East
            EmitFace(
                corners[5], corners[1], corners[2], corners[6],
                Vector3.right, uvs.East, verts, norms, meshUvs, tris, ref v, ref t);
            // -X West
            EmitFace(
                corners[0], corners[4], corners[7], corners[3],
                Vector3.left, uvs.West, verts, norms, meshUvs, tris, ref v, ref t);

            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = meshUvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>크랙 오버레이용 — 전 면 동일 UV.</summary>
        public static Mesh BuildUniformUvCube(in Rect uv) =>
            Build(TileCubeFaceUvSet.Uniform(uv));

        static void EmitFace(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            Vector3 normal,
            in Rect uv,
            Vector3[] verts,
            Vector3[] norms,
            Vector2[] uvs,
            int[] tris,
            ref int v,
            ref int t)
        {
            int i0 = v;
            verts[v] = a;
            norms[v] = normal;
            uvs[v] = new Vector2(uv.xMin, uv.yMin);
            v++;
            verts[v] = b;
            norms[v] = normal;
            uvs[v] = new Vector2(uv.xMax, uv.yMin);
            v++;
            verts[v] = c;
            norms[v] = normal;
            uvs[v] = new Vector2(uv.xMax, uv.yMax);
            v++;
            verts[v] = d;
            norms[v] = normal;
            uvs[v] = new Vector2(uv.xMin, uv.yMax);
            v++;

            tris[t++] = i0;
            tris[t++] = i0 + 1;
            tris[t++] = i0 + 2;
            tris[t++] = i0;
            tris[t++] = i0 + 2;
            tris[t++] = i0 + 3;
        }
    }
}
