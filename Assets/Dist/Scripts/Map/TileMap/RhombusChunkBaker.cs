// 자식 RhombusTileMarker 들을 Largest-Rect-First 알고리즘으로 병합,
// 단일 MeshCollider 로 베이킹. 런타임/에디터 모두 동작.
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshCollider))]
public class RhombusChunkBaker : MonoBehaviour
{
    [Header("Tile size (world units)")]
    public float diagX = 1f;
    public float diagZ = 1f;
    public float thicknessY = 0.05f;

    [Header("Collect")]
    public bool includeInactive = false;

    MeshCollider _mc;
    Mesh         _mesh;

    // ─── 그룹 캐시 (직사각형 1개 = GroupData 1개) ──
    private class GroupData
    {
        public int                     id;
        public List<RhombusTileMarker> markers = new List<RhombusTileMarker>();
        public Vector3[]               verts;
        public int[]                   tris;
    }

    private readonly Dictionary<int, GroupData> _groups = new Dictionary<int, GroupData>();
    private int _nextGroupId;

    // ─────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────

    void Awake()
    {
        _mc = GetComponent<MeshCollider>();
        _mc.convex = false;
        _mc.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning
                           | MeshColliderCookingOptions.WeldColocatedVertices
                           | MeshColliderCookingOptions.UseFastMidphase;
    }

    // ─────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────

    /// <summary>모든 자식 타일을 대상으로 전체 베이킹.</summary>
    [ContextMenu("Bake Chunk")]
    public void BakeChunk()
    {
        var tiles = GetComponentsInChildren<RhombusTileMarker>(includeInactive);
        if (tiles == null || tiles.Length == 0)
        {
            Debug.LogWarning("[RhombusChunkBaker] No RhombusTileMarker found.");
            return;
        }

        _groups.Clear();
        _nextGroupId = 0;

        foreach (var m in tiles)
            m.groupId = -1;

        BakeTilesIntoGroups(tiles);
        RebuildMeshFromGroups();

        int totalVerts = 0, totalTris = 0;
        foreach (var g in _groups.Values) { totalVerts += g.verts.Length; totalTris += g.tris.Length / 3; }
        Debug.Log($"[RhombusChunkBaker] {tiles.Length} tiles → {_groups.Count} groups | verts:{totalVerts}, tris:{totalTris}");
    }

    /// <summary>
    /// 지정 그룹만 재계산. 그룹 내 생존 마커를 재파티셔닝 후 메시 재조합.
    /// BakeChunk 이후에만 유효 (캐시 필요).
    /// </summary>
    public void BakeGroup(int id)
    {
        if (!_groups.TryGetValue(id, out var existing))
        {
            Debug.LogWarning($"[RhombusChunkBaker] Group {id} 가 캐시에 없습니다. BakeChunk 를 먼저 실행하세요.");
            return;
        }

        var liveMarkers = new List<RhombusTileMarker>();
        foreach (var m in existing.markers)
            if (m != null) liveMarkers.Add(m);

        foreach (var m in liveMarkers)
            m.groupId = -1;

        _groups.Remove(id);

        if (liveMarkers.Count > 0)
            BakeTilesIntoGroups(liveMarkers.ToArray());

        RebuildMeshFromGroups();
        Debug.Log($"[RhombusChunkBaker] BakeGroup({id}): {liveMarkers.Count} 타일 재계산 완료");
    }

    [ContextMenu("Clear Baked Mesh")]
    public void ClearBaked()
    {
        if (_mesh != null)
        {
            _mesh.Clear();
            _mc.sharedMesh = null;
        }
        _groups.Clear();
    }

    // ─────────────────────────────────────────────
    // Core: 타일 → GroupData 파티셔닝
    // ─────────────────────────────────────────────

    private void BakeTilesIntoGroups(RhombusTileMarker[] tiles)
    {
        float hy = thicknessY * 0.5f;

        var byLayer = new Dictionary<int, List<(int gx, int gz, float worldY, RhombusTileMarker marker)>>();

        foreach (var tile in tiles)
        {
            var lp = transform.InverseTransformPoint(tile.transform.position);
            // FloorToInt: 반정수 위치 타일에서 뱅커 반올림 충돌 방지
            int gx  = Mathf.FloorToInt(lp.x / diagX);
            int gz  = Mathf.FloorToInt(lp.z / diagZ);
            int key = Mathf.RoundToInt(lp.y / thicknessY);

            if (!byLayer.TryGetValue(key, out var list))
            {
                list = new List<(int, int, float, RhombusTileMarker)>();
                byLayer[key] = list;
            }
            list.Add((gx, gz, lp.y, tile));
        }

        foreach (var kv in byLayer)
        {
            var tileList = kv.Value;
            float worldY = tileList[0].worldY;

            int minX = int.MaxValue, maxX = int.MinValue;
            int minZ = int.MaxValue, maxZ = int.MinValue;
            foreach (var (gx, gz, _, _) in tileList)
            {
                if (gx < minX) minX = gx;
                if (gx > maxX) maxX = gx;
                if (gz < minZ) minZ = gz;
                if (gz > maxZ) maxZ = gz;
            }

            int cols = maxX - minX + 1;
            int rows = maxZ - minZ + 1;
            var grid = new bool[cols, rows];

            foreach (var (gx, gz, _, _) in tileList)
                grid[gx - minX, gz - minZ] = true;

            while (true)
            {
                if (!FindLargestRect(grid, cols, rows, out int rx, out int rz, out int rw, out int rh))
                    break;

                for (int c = rx; c < rx + rw; c++)
                    for (int r = rz; r < rz + rh; r++)
                        grid[c, r] = false;

                var group = new GroupData { id = _nextGroupId++ };

                foreach (var (gx, gz, _, marker) in tileList)
                {
                    int lx = gx - minX;
                    int lz = gz - minZ;
                    if (lx >= rx && lx < rx + rw && lz >= rz && lz < rz + rh)
                    {
                        group.markers.Add(marker);
                        marker.groupId = group.id;
                    }
                }

                float cx = (minX + rx + rw * 0.5f) * diagX;
                float cz = (minZ + rz + rh * 0.5f) * diagZ;
                float hw = rw * diagX * 0.5f;
                float hd = rh * diagZ * 0.5f;

                var gVerts = new List<Vector3>();
                var gTris  = new List<int>();
                AddBox(gVerts, gTris, new Vector3(cx, worldY, cz), hw, hy, hd);
                group.verts = gVerts.ToArray();
                group.tris  = gTris.ToArray();

                _groups[group.id] = group;
            }
        }
    }

    // ─────────────────────────────────────────────
    // 메시 재조합
    // ─────────────────────────────────────────────

    private void RebuildMeshFromGroups()
    {
        var verts = new List<Vector3>();
        var tris  = new List<int>();

        foreach (var group in _groups.Values)
        {
            int offset = verts.Count;
            verts.AddRange(group.verts);
            foreach (var t in group.tris)
                tris.Add(t + offset);
        }

        if (_mesh == null) _mesh = new Mesh { name = "RhombusChunkMesh" };
        _mesh.Clear();
        _mesh.indexFormat = verts.Count > 65535
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        _mesh.SetVertices(verts);
        _mesh.SetTriangles(tris, 0, true);
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        _mc.sharedMesh = null;
        _mc.sharedMesh = _mesh;
    }

    // ─────────────────────────────────────────────
    // Largest Rectangle in Histogram
    // ─────────────────────────────────────────────

    private static readonly Stack<int> _histStack = new Stack<int>();

    private static bool FindLargestRect(bool[,] grid, int cols, int rows,
        out int rx, out int rz, out int rw, out int rh)
    {
        var heights = new int[cols];
        int bestArea = 0;
        rx = rz = rw = rh = 0;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
                heights[col] = grid[col, row] ? heights[col] + 1 : 0;

            _histStack.Clear();

            for (int col = 0; col <= cols; col++)
            {
                int h = (col == cols) ? 0 : heights[col];

                while (_histStack.Count > 0 && heights[_histStack.Peek()] > h)
                {
                    int height   = heights[_histStack.Pop()];
                    int startCol = _histStack.Count == 0 ? 0 : _histStack.Peek() + 1;
                    int width    = col - startCol;
                    int area     = height * width;

                    if (area > bestArea)
                    {
                        bestArea = area;
                        rx = startCol;
                        rz = row - height + 1;
                        rw = width;
                        rh = height;
                    }
                }
                _histStack.Push(col);
            }
        }

        return bestArea > 0;
    }

    // ─────────────────────────────────────────────
    // Box mesh 헬퍼
    // ─────────────────────────────────────────────

    private static void AddBox(List<Vector3> verts, List<int> tris,
        Vector3 center, float hx, float hy, float hz)
    {
        int b = verts.Count;

        verts.Add(center + new Vector3(-hx,  hy,  hz));
        verts.Add(center + new Vector3( hx,  hy,  hz));
        verts.Add(center + new Vector3( hx,  hy, -hz));
        verts.Add(center + new Vector3(-hx,  hy, -hz));
        verts.Add(center + new Vector3(-hx, -hy,  hz));
        verts.Add(center + new Vector3( hx, -hy,  hz));
        verts.Add(center + new Vector3( hx, -hy, -hz));
        verts.Add(center + new Vector3(-hx, -hy, -hz));

        tris.Add(b+0); tris.Add(b+1); tris.Add(b+2);
        tris.Add(b+0); tris.Add(b+2); tris.Add(b+3);
        tris.Add(b+6); tris.Add(b+5); tris.Add(b+4);
        tris.Add(b+7); tris.Add(b+6); tris.Add(b+4);
        tris.Add(b+0); tris.Add(b+4); tris.Add(b+5);
        tris.Add(b+0); tris.Add(b+5); tris.Add(b+1);
        tris.Add(b+1); tris.Add(b+5); tris.Add(b+6);
        tris.Add(b+1); tris.Add(b+6); tris.Add(b+2);
        tris.Add(b+2); tris.Add(b+6); tris.Add(b+7);
        tris.Add(b+2); tris.Add(b+7); tris.Add(b+3);
        tris.Add(b+3); tris.Add(b+7); tris.Add(b+4);
        tris.Add(b+3); tris.Add(b+4); tris.Add(b+0);
    }
}
