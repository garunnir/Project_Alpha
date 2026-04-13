// 자식 ColliderTileMarker 들을 Largest-Rect-First 알고리즘으로 병합,
// 단일 MeshCollider 로 베이킹. 런타임/에디터 모두 동작.
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshCollider))]
public class ChunkColliderBaker : MonoBehaviour
{
    public static ChunkColliderBaker Instance { get; private set; }

    [Header("Tile size (world units)")]
    public float diagX = 1f;
    public float diagZ = 1f;
    public float thicknessY = 0.05f;

    MeshCollider _mc;
    Mesh         _mesh;

    // ─── 등록된 마커 목록 ──
    private readonly HashSet<ColliderTileMarker> _registeredMarkers = new HashSet<ColliderTileMarker>();
    private bool _dirty;

    // ─── 그룹 캐시 (직사각형 1개 = GroupData 1개) ──
    private class GroupData
    {
        public int                     id;
        public List<ColliderTileMarker> markers = new List<ColliderTileMarker>();
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
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[ChunkColliderBaker] 인스턴스가 이미 존재합니다.");
            Destroy(this);
            return;
        }
        Instance = this;

        _mc = GetComponent<MeshCollider>();
        _mc.convex = false;
        _mc.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning
                           | MeshColliderCookingOptions.WeldColocatedVertices
                           | MeshColliderCookingOptions.UseFastMidphase;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void LateUpdate()
    {
        if (!_dirty) return;
        _dirty = false;
        BakeChunk();
    }

    // ─────────────────────────────────────────────
    // 등록 API
    // ─────────────────────────────────────────────

    public void Register(ColliderTileMarker marker)
    {
        _registeredMarkers.Add(marker);
        _dirty = true;
    }

    public void Unregister(ColliderTileMarker marker)
    {
        _registeredMarkers.Remove(marker);
        _dirty = true;
    }

    // ─────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────

    /// <summary>등록된 마커 전체를 베이킹 (런타임).</summary>
    public void BakeChunk()
    {
        if (_registeredMarkers.Count == 0)
        {
            Debug.LogWarning("[ChunkColliderBaker] 등록된 ColliderTileMarker 가 없습니다.");
            return;
        }

        _groups.Clear();
        _nextGroupId = 0;

        foreach (var m in _registeredMarkers)
            m.groupId = -1;

        var arr = new ColliderTileMarker[_registeredMarkers.Count];
        _registeredMarkers.CopyTo(arr);
        BakeTilesIntoGroups(arr);
        RebuildMeshFromGroups();

        int totalVerts = 0, totalTris = 0;
        foreach (var g in _groups.Values) { totalVerts += g.verts.Length; totalTris += g.tris.Length / 3; }
        Debug.Log($"[ChunkColliderBaker] {_registeredMarkers.Count} tiles → {_groups.Count} groups | verts:{totalVerts}, tris:{totalTris}");
    }

#if UNITY_EDITOR
    /// <summary>에디터 전용. 자식 계층에서 마커를 직접 수집해 베이킹.</summary>
    [ContextMenu("Bake Chunk (Editor)")]
    void BakeChunkEditor()
    {
        var tiles = GetComponentsInChildren<ColliderTileMarker>(includeInactive: true);
        if (tiles == null || tiles.Length == 0)
        {
            Debug.LogWarning("[ChunkColliderBaker] No ColliderTileMarker found in children.");
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
        Debug.Log($"[ChunkColliderBaker][Editor] {tiles.Length} tiles → {_groups.Count} groups | verts:{totalVerts}, tris:{totalTris}");
    }
#endif

    /// <summary>
    /// 지정 그룹만 재계산. 그룹 내 생존 마커를 재파티셔닝 후 메시 재조합.
    /// BakeChunk 이후에만 유효 (캐시 필요).
    /// </summary>
    public void BakeGroup(int id)
    {
        if (!_groups.TryGetValue(id, out var existing))
        {
            Debug.LogWarning($"[ChunkColliderBaker] Group {id} 가 캐시에 없습니다. BakeChunk 를 먼저 실행하세요.");
            return;
        }

        var liveMarkers = new List<ColliderTileMarker>();
        foreach (var m in existing.markers)
            if (m != null) liveMarkers.Add(m);

        foreach (var m in liveMarkers)
            m.groupId = -1;

        _groups.Remove(id);

        if (liveMarkers.Count > 0)
            BakeTilesIntoGroups(liveMarkers.ToArray());

        RebuildMeshFromGroups();
        Debug.Log($"[ChunkColliderBaker] BakeGroup({id}): {liveMarkers.Count} 타일 재계산 완료");
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

    private void BakeTilesIntoGroups(ColliderTileMarker[] tiles)
    {
        float hy = thicknessY * 0.5f;

        var byLayer = new Dictionary<int, List<(int gx, int gz, float worldY, ColliderTileMarker marker)>>();

        foreach (var tile in tiles)
        {
            var lp = transform.InverseTransformPoint(tile.transform.position);
            // FloorToInt: 반정수 위치 타일에서 뱅커 반올림 충돌 방지
            int gx  = Mathf.FloorToInt(lp.x / diagX);
            int gz  = Mathf.FloorToInt(lp.z / diagZ);
            int key = Mathf.RoundToInt(lp.y / thicknessY);

            if (!byLayer.TryGetValue(key, out var list))
            {
                list = new List<(int, int, float, ColliderTileMarker)>();
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

        if (_mesh == null) _mesh = new Mesh { name = "ChunkColliderMesh" };
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
