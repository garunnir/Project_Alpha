// Rhombus 타일들을 하나의 MeshCollider로 베이크
// - 정적 환경용(비컨벡스). 동적 Rigidbody와의 충돌은 OK.
// - 루트~콜라이더 경로의 스케일은 (1,1,1) 유지 권장.
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[ExecuteAlways]
[RequireComponent(typeof(MeshCollider))]
public class RhombusChunkBaker : MonoBehaviour
{
    [Header("Rhombus size (world units)")]
    public float diagX = 1f;        // 가로 대각 길이
    public float diagZ = 0.5f;      // 세로 대각 길이
    public float thicknessY = 0.05f;// 프리즘 높이(얇게)

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

        float hx = diagX * 0.5f;
        float hz = diagZ * 0.5f;
        float hy = thicknessY * 0.5f;

        // 한 타일 = 8버텍스(윗4+아랫4), 12트라잉글(36인덱스)
        int tileCount = tiles.Length;
        var verts = new List<Vector3>(tileCount * 8);
        var tris  = new List<int>(tileCount * 36);

        for (int t = 0; t < tileCount; t++) {
            var pos = transform.InverseTransformPoint(tiles[t].transform.position);
            int baseIndex = verts.Count;
            // 로컬 마름모(월드 XZ)에 맞춘 버텍스(타일 중앙 기준)
            Vector3 n = new Vector3( 0,  hy,  hz);
            Vector3 e = new Vector3( hx, hy,   0);
            Vector3 s = new Vector3( 0,  hy, -hz);
            Vector3 w = new Vector3(-hx, hy,   0);

            // 윗면 4개
            verts.Add(pos + n); // 0
            verts.Add(pos + e); // 1
            verts.Add(pos + s); // 2
            verts.Add(pos + w); // 3
            // 아랫면 4개
            verts.Add(pos + new Vector3( 0,-hy,  hz)); // 4
            verts.Add(pos + new Vector3( hx,-hy,  0)); // 5
            verts.Add(pos + new Vector3( 0,-hy, -hz)); // 6
            verts.Add(pos + new Vector3(-hx,-hy,  0)); // 7

            // 윗면
            tris.Add(baseIndex+0); tris.Add(baseIndex+1); tris.Add(baseIndex+2);
            tris.Add(baseIndex+0); tris.Add(baseIndex+2); tris.Add(baseIndex+3);
            // 아랫면
            tris.Add(baseIndex+6); tris.Add(baseIndex+5); tris.Add(baseIndex+4);
            tris.Add(baseIndex+7); tris.Add(baseIndex+6); tris.Add(baseIndex+4);
            // 옆면 4개
            tris.Add(baseIndex+0); tris.Add(baseIndex+4); tris.Add(baseIndex+5);
            tris.Add(baseIndex+0); tris.Add(baseIndex+5); tris.Add(baseIndex+1);

            tris.Add(baseIndex+1); tris.Add(baseIndex+5); tris.Add(baseIndex+6);
            tris.Add(baseIndex+1); tris.Add(baseIndex+6); tris.Add(baseIndex+2);

            tris.Add(baseIndex+2); tris.Add(baseIndex+6); tris.Add(baseIndex+7);
            tris.Add(baseIndex+2); tris.Add(baseIndex+7); tris.Add(baseIndex+3);

            tris.Add(baseIndex+3); tris.Add(baseIndex+7); tris.Add(baseIndex+4);
            tris.Add(baseIndex+3); tris.Add(baseIndex+4); tris.Add(baseIndex+0);
        }

        if (_mesh == null) _mesh = new Mesh(){ name = "RhombusChunkMesh" };
        _mesh.Clear();
        _mesh.indexFormat = (verts.Count > 65535) ? UnityEngine.Rendering.IndexFormat.UInt32
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
