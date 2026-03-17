// Rhombus(였던) 타일들을 하나의 MeshCollider로 베이크
// → Largest-Rect-First 알고리즘으로 인접 타일을 최대 직사각형으로 병합
#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
[RequireComponent(typeof(MeshCollider))]
public class RhombusChunkBaker : MonoBehaviour
{
    [Header("Tile size (world units)")]
    public float diagX = 1f;
    public float diagZ = 1f;
    public float thicknessY = 0.05f;

    [Header("Collect")]
    public bool includeInactive = false;

    // ─── 내부 접근용 alias ───────────────────────
    float _diagX => diagX;
    float _diagZ => diagZ;
    float _thicknessY => thicknessY;
    bool  _includeInactive => includeInactive;

    MeshCollider _mc;
    Mesh _mesh;

    void OnEnable()
    {
        _mc = GetComponent<MeshCollider>();
        if (_mc.sharedMesh == null)
        {
            if (_mesh == null) _mesh = new Mesh { name = "RhombusChunkMesh" };
            _mc.sharedMesh = _mesh;
        }
        else
        {
            _mesh = _mc.sharedMesh;
        }

        _mc.convex = false;
        _mc.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning
                           | MeshColliderCookingOptions.WeldColocatedVertices
                           | MeshColliderCookingOptions.UseFastMidphase;
    }

    // ─────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────

    [ContextMenu("Bake Chunk")]
    public void BakeChunk()
    {
        var tiles = GetComponentsInChildren<RhombusTileMarker>(_includeInactive);
        if (tiles == null || tiles.Length == 0)
        {
            Debug.LogWarning("[RhombusChunkBaker] No RhombusTileMarker found under this chunk.");
            return;
        }

        var verts = new List<Vector3>();
        var tris  = new List<int>();

        // Y 레이어별로 타일 그룹화 후 Largest-Rect-First 실행
        int rectCount = BakeByLayer(tiles, verts, tris);

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

        Debug.Log($"[RhombusChunkBaker] Baked {tiles.Length} tiles → {rectCount} rects | verts:{verts.Count}, tris:{tris.Count / 3}");
    }

    [ContextMenu("Debug: Print Tile Grid")]
    public void DebugPrintGrid()
    {
        var tiles = GetComponentsInChildren<RhombusTileMarker>(_includeInactive);
        if (tiles == null || tiles.Length == 0) { Debug.LogWarning("No tiles found."); return; }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[RhombusChunkBaker] diagX={_diagX} diagZ={_diagZ} thicknessY={_thicknessY}");
        sb.AppendLine("tile local pos → (gx, gz, layerKey)");

        foreach (var tile in tiles)
        {
            var lp = transform.InverseTransformPoint(tile.transform.position);
            int gx  = Mathf.FloorToInt(lp.x / _diagX);
            int gz  = Mathf.FloorToInt(lp.z / _diagZ);
            int key = Mathf.RoundToInt(lp.y / _thicknessY);
            sb.AppendLine($"  {tile.name}: ({lp.x:F3}, {lp.y:F3}, {lp.z:F3}) → gx={gx} gz={gz} layer={key}");
        }
        Debug.Log(sb.ToString());
    }

    [ContextMenu("Clear Baked Mesh")]
    public void ClearBaked()
    {
        if (_mesh != null)
        {
            _mesh.Clear();
            _mc.sharedMesh = null;
        }
    }

    // ─────────────────────────────────────────────
    // Core: 레이어별 병합
    // ─────────────────────────────────────────────

    private int BakeByLayer(RhombusTileMarker[] tiles, List<Vector3> verts, List<int> tris)
    {
        // 타일 → 로컬 좌표 → (gridX, gridZ, layerY) 변환
        var byLayer = new Dictionary<int, List<(int gx, int gz, float worldY)>>();

        foreach (var tile in tiles)
        {
            var lp = transform.InverseTransformPoint(tile.transform.position);
            // FloorToInt: 타일이 반정수 위치(N+0.5)에 있을 때 RoundToInt는 뱅커 반올림으로
            // 인접 타일이 같은 gx에 충돌함. Floor로 [N, N+1) 구간을 하나의 셀로 취급.
            int gx  = Mathf.FloorToInt(lp.x / _diagX);
            int gz  = Mathf.FloorToInt(lp.z / _diagZ);
            int key = Mathf.RoundToInt(lp.y / _thicknessY); // Y 레이어 키

            if (!byLayer.TryGetValue(key, out var list))
            {
                list = new List<(int, int, float)>();
                byLayer[key] = list;
            }
            list.Add((gx, gz, lp.y));
        }

        int totalRects = 0;
        float hy = _thicknessY * 0.5f;

        foreach (var kv in byLayer)
        {
            var tileList = kv.Value;
            float worldY = tileList[0].worldY;

            // 그리드 범위
            int minX = int.MaxValue, maxX = int.MinValue;
            int minZ = int.MaxValue, maxZ = int.MinValue;
            foreach (var (gx, gz, _) in tileList)
            {
                if (gx < minX) minX = gx;
                if (gx > maxX) maxX = gx;
                if (gz < minZ) minZ = gz;
                if (gz > maxZ) maxZ = gz;
            }

            int cols = maxX - minX + 1;
            int rows = maxZ - minZ + 1;
            var grid = new bool[cols, rows];

            foreach (var (gx, gz, _) in tileList)
                grid[gx - minX, gz - minZ] = true;

            // Largest-Rect-First
            while (true)
            {
                if (!FindLargestRect(grid, cols, rows, out int rx, out int rz, out int rw, out int rh))
                    break;

                // 해당 영역 제거
                for (int c = rx; c < rx + rw; c++)
                    for (int r = rz; r < rz + rh; r++)
                        grid[c, r] = false;

                // 직사각형 월드 중심
                // FloorToInt 기준: gx=N → 월드 [N*diagX, (N+1)*diagX], 중심 = (N+0.5)*diagX
                // rect 중심 = (gx_start + rw/2) * diagX
                float cx = (minX + rx + rw * 0.5f) * _diagX;
                float cz = (minZ + rz + rh * 0.5f) * _diagZ;
                float hw  = rw * _diagX * 0.5f;
                float hd  = rh * _diagZ * 0.5f;

                AddBox(verts, tris, new Vector3(cx, worldY, cz), hw, hy, hd);
                totalRects++;
            }
        }

        return totalRects;
    }

    // ─────────────────────────────────────────────
    // Largest Rectangle in Histogram (스택, O(cols) per row)
    // ─────────────────────────────────────────────

    private static readonly Stack<int> _histStack = new Stack<int>();

    /// <summary>
    /// bool 그리드에서 타일이 채워진 가장 큰 직사각형을 찾는다.
    /// 반환값: 직사각형이 있으면 true, 없으면 false.
    /// </summary>
    private static bool FindLargestRect(bool[,] grid, int cols, int rows,
        out int rx, out int rz, out int rw, out int rh)
    {
        var heights = new int[cols];
        int bestArea = 0;
        rx = rz = rw = rh = 0;

        for (int row = 0; row < rows; row++)
        {
            // 각 열의 연속 타일 높이 갱신
            for (int col = 0; col < cols; col++)
                heights[col] = grid[col, row] ? heights[col] + 1 : 0;

            // 이 row의 histogram에서 최대 직사각형 탐색
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
    // Box mesh 추가 헬퍼
    // ─────────────────────────────────────────────

    private static void AddBox(List<Vector3> verts, List<int> tris,
        Vector3 center, float hx, float hy, float hz)
    {
        int b = verts.Count;

        // 윗면 4개
        verts.Add(center + new Vector3(-hx,  hy,  hz)); // b+0 앞-좌
        verts.Add(center + new Vector3( hx,  hy,  hz)); // b+1 앞-우
        verts.Add(center + new Vector3( hx,  hy, -hz)); // b+2 뒤-우
        verts.Add(center + new Vector3(-hx,  hy, -hz)); // b+3 뒤-좌
        // 아랫면 4개
        verts.Add(center + new Vector3(-hx, -hy,  hz)); // b+4
        verts.Add(center + new Vector3( hx, -hy,  hz)); // b+5
        verts.Add(center + new Vector3( hx, -hy, -hz)); // b+6
        verts.Add(center + new Vector3(-hx, -hy, -hz)); // b+7

        // 윗면
        tris.Add(b+0); tris.Add(b+1); tris.Add(b+2);
        tris.Add(b+0); tris.Add(b+2); tris.Add(b+3);
        // 아랫면
        tris.Add(b+6); tris.Add(b+5); tris.Add(b+4);
        tris.Add(b+7); tris.Add(b+6); tris.Add(b+4);
        // 앞면
        tris.Add(b+0); tris.Add(b+4); tris.Add(b+5);
        tris.Add(b+0); tris.Add(b+5); tris.Add(b+1);
        // 오른쪽
        tris.Add(b+1); tris.Add(b+5); tris.Add(b+6);
        tris.Add(b+1); tris.Add(b+6); tris.Add(b+2);
        // 뒤쪽
        tris.Add(b+2); tris.Add(b+6); tris.Add(b+7);
        tris.Add(b+2); tris.Add(b+7); tris.Add(b+3);
        // 왼쪽
        tris.Add(b+3); tris.Add(b+7); tris.Add(b+4);
        tris.Add(b+3); tris.Add(b+4); tris.Add(b+0);
    }
}
#endif
