// Rhombus(였던) 타일들을 하나의 MeshCollider로 베이크
// → 지금은 XZ축 정사각(직사각) 타일 프리즘으로 생성
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[ExecuteAlways]
[RequireComponent(typeof(MeshCollider))]
public class RhombusChunkBaker : MonoBehaviour
{
    [Header("Tile size (world units)")]
    public float diagX = 1f;        // 가로 길이(이제는 대각선이 아니라 폭)
    public float diagZ = 1f;        // 세로 길이(이제는 대각선이 아니라 깊이)
    public float thicknessY = 0.05f;// 프리즘 높이

    [Header("Collect")]
    public bool includeInactive = false; // 비활성 자식도 포함

    MeshCollider _mc;
    Mesh _mesh; // 청크 통합 메시

    void OnEnable() {
        _mc = GetComponent<MeshCollider>();
        if (_mc.sharedMesh == null) {
            if (_mesh == null) _mesh = new Mesh(){ name = "RhombusChunkMesh" };
            _mc.sharedMesh = _mesh;
        } else {
            _mesh = _mc.sharedMesh;
        }
        _mc.convex = false; // 정적 환경용

        _mc.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning
                           | MeshColliderCookingOptions.WeldColocatedVertices
                           | MeshColliderCookingOptions.UseFastMidphase;
    }

    [ContextMenu("Bake Chunk")]
    public void BakeChunk() {
        var tiles = GetComponentsInChildren<RhombusTileMarker>(includeInactive);
        if (tiles == null || tiles.Length == 0) {
            Debug.LogWarning("[RhombusChunkBaker] No RhombusTileMarker found under this chunk.");
            return;
        }

        // 이제 diagX/diagZ는 '변 길이' 취급 (가로/세로)
        float hx = diagX * 0.5f;   // half width
        float hz = diagZ * 0.5f;   // half depth
        float hy = thicknessY * 0.5f;

        // 한 타일 = 8버텍스(윗4+아랫4), 12트라잉글(36인덱스)
        int tileCount = tiles.Length;
        var verts = new List<Vector3>(tileCount * 8);
        var tris  = new List<int>(tileCount * 36);

        for (int t = 0; t < tileCount; t++) {
            var pos = transform.InverseTransformPoint(tiles[t].transform.position);
            int baseIndex = verts.Count;

            // ★ 정사각(직사각) 윗면 4개 (축 정렬)
            //   (-hx, +hy, +hz) ─ (hx, +hy, +hz)
            //          |                |
            //   (-hx, +hy, -hz) ─ (hx, +hy, -hz)
            Vector3 v0 = pos + new Vector3(-hx,  hy,  hz); // 앞-좌
            Vector3 v1 = pos + new Vector3( hx,  hy,  hz); // 앞-우
            Vector3 v2 = pos + new Vector3( hx,  hy, -hz); // 뒤-우
            Vector3 v3 = pos + new Vector3(-hx,  hy, -hz); // 뒤-좌

            // 아랫면 4개 (윗면과 동일 xz, y만 -hy)
            Vector3 v4 = pos + new Vector3(-hx, -hy,  hz);
            Vector3 v5 = pos + new Vector3( hx, -hy,  hz);
            Vector3 v6 = pos + new Vector3( hx, -hy, -hz);
            Vector3 v7 = pos + new Vector3(-hx, -hy, -hz);

            // 윗면 4개
            verts.Add(v0); // baseIndex+0
            verts.Add(v1); // baseIndex+1
            verts.Add(v2); // baseIndex+2
            verts.Add(v3); // baseIndex+3
            // 아랫면 4개
            verts.Add(v4); // baseIndex+4
            verts.Add(v5); // baseIndex+5
            verts.Add(v6); // baseIndex+6
            verts.Add(v7); // baseIndex+7

            // 윗면
            tris.Add(baseIndex+0); tris.Add(baseIndex+1); tris.Add(baseIndex+2);
            tris.Add(baseIndex+0); tris.Add(baseIndex+2); tris.Add(baseIndex+3);

            // 아랫면 (시계 반대가 되도록 순서 주의)
            tris.Add(baseIndex+6); tris.Add(baseIndex+5); tris.Add(baseIndex+4);
            tris.Add(baseIndex+7); tris.Add(baseIndex+6); tris.Add(baseIndex+4);

            // 옆면 4개
            // 앞면 (v0,v1,v5,v4)
            tris.Add(baseIndex+0); tris.Add(baseIndex+4); tris.Add(baseIndex+5);
            tris.Add(baseIndex+0); tris.Add(baseIndex+5); tris.Add(baseIndex+1);

            // 오른쪽 (v1,v2,v6,v5)
            tris.Add(baseIndex+1); tris.Add(baseIndex+5); tris.Add(baseIndex+6);
            tris.Add(baseIndex+1); tris.Add(baseIndex+6); tris.Add(baseIndex+2);

            // 뒤쪽 (v2,v3,v7,v6)
            tris.Add(baseIndex+2); tris.Add(baseIndex+6); tris.Add(baseIndex+7);
            tris.Add(baseIndex+2); tris.Add(baseIndex+7); tris.Add(baseIndex+3);

            // 왼쪽 (v3,v0,v4,v7)
            tris.Add(baseIndex+3); tris.Add(baseIndex+7); tris.Add(baseIndex+4);
            tris.Add(baseIndex+3); tris.Add(baseIndex+4); tris.Add(baseIndex+0);
        }

        if (_mesh == null) _mesh = new Mesh(){ name = "RhombusChunkMesh" };
        _mesh.Clear();
        _mesh.indexFormat = (verts.Count > 65535) 
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        _mesh.SetVertices(verts);
        _mesh.SetTriangles(tris, 0, true);
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        // 갱신 트릭
        _mc.sharedMesh = null;
        _mc.sharedMesh = _mesh;

        Debug.Log($"[RhombusChunkBaker] Baked {tileCount} tiles → verts:{verts.Count}, tris:{tris.Count/3}");
    }

    [ContextMenu("Clear Baked Mesh")]
    public void ClearBaked() {
        if (_mesh != null) {
            _mesh.Clear();
            _mc.sharedMesh = null;
        }
    }
}
#endif
