using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RaycastSightMeshMaker : MonoBehaviour
{
    [Header("시야 설정")]
    public float viewRadius = 10f;
    [Range(0f, 360f)]
    public float viewAngle = 90f;
    public int rayCount = 100;

    [Header("레이어")]
    public LayerMask obstacleMask;

    private Mesh _mesh;
    private MeshFilter _meshFilter;
    private CharacterState _characterState;

    void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _mesh = new Mesh();
        _mesh.name = "FieldOfViewMesh";
        _meshFilter.mesh = _mesh;
        _characterState = GetComponent<CharacterState>();
        if (_characterState == null)
            _characterState = GetComponentInParent<CharacterState>();
    }

    void LateUpdate()
    {
        DrawFieldOfView();
    }

    void DrawFieldOfView()
    {
        float angleStep = viewAngle / rayCount;
        // 캐릭터가 바라보는 방향 기준으로 시야각 시작점
        float startAngle = -viewAngle / 2f;
        float forwardYawDeg = GetSightForwardYawDegrees();

        Vector3[] vertices = new Vector3[rayCount + 2];
        int[] triangles = new int[rayCount * 3];

        // 부채꼴 중심점 (로컬 원점)
        vertices[0] = Vector3.zero;

        for (int i = 0; i <= rayCount; i++)
        {
            float angle = startAngle + angleStep * i;
            Vector3 dir = DirFromAngle(angle, forwardYawDeg);

            RaycastHit hit;
            if (Physics.Raycast(transform.position, dir, out hit, viewRadius, obstacleMask))
            {
                // 벽에 막힘 → 충돌 지점까지만
                vertices[i + 1] = transform.InverseTransformPoint(hit.point);
            }
            else
            {
                // 막힘 없음 → 최대 거리
                vertices[i + 1] = transform.InverseTransformDirection(dir) * viewRadius;
            }
        }

        // 삼각형 구성 (부채꼴을 삼각형으로 분할)
        for (int i = 0; i < rayCount; i++)
        {
            triangles[i * 3 + 0] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        _mesh.Clear();
        _mesh.vertices = vertices;
        _mesh.triangles = triangles;
        _mesh.RecalculateNormals();
    }

    /// <summary>CharacterState.SightDir(XZ) 중심축의 Yaw(도). SightDir이 없으면 트랜스폼 Y.</summary>
    float GetSightForwardYawDegrees()
    {
        if (_characterState == null)
            return transform.eulerAngles.y;
        Vector3 d = _characterState.GetFacingDir();
        d.y = 0f;
        if (d.sqrMagnitude < 1e-8f)
            return transform.eulerAngles.y;
        return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    // 각도 → 방향 벡터 (SightDir 정면 기준 부채꼴)
    Vector3 DirFromAngle(float angleDeg, float forwardYawDeg)
    {
        float globalAngle = angleDeg + forwardYawDeg;
        float rad = globalAngle * Mathf.Deg2Rad;
        // XZ 평면 (iso뷰 수평면) 
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        float angleStep = viewAngle / rayCount;
        float startAngle = -viewAngle / 2f;
        if (_characterState == null)
        {
            _characterState = GetComponent<CharacterState>();
            if (_characterState == null)
                _characterState = GetComponentInParent<CharacterState>();
        }
        float forwardYawDeg = GetSightForwardYawDegrees();

        // 시야 범위 원호
        UnityEditor.Handles.color = new Color(1f, 1f, 0f, 0.1f);
        UnityEditor.Handles.DrawSolidArc(
            transform.position,
            Vector3.up,
            DirFromAngle(-viewAngle / 2f, forwardYawDeg),
            viewAngle,
            viewRadius
        );

        // 시야각 경계선
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + DirFromAngle(-viewAngle / 2f, forwardYawDeg) * viewRadius);
        Gizmos.DrawLine(transform.position, transform.position + DirFromAngle( viewAngle / 2f, forwardYawDeg) * viewRadius);

        // 각 레이
        for (int i = 0; i <= rayCount; i++)
        {
            float angle = startAngle + angleStep * i;
            Vector3 dir = DirFromAngle(angle, forwardYawDeg);

            RaycastHit hit;
            if (Physics.Raycast(transform.position, dir, out hit, viewRadius, obstacleMask))
            {
                // 막힌 레이 → 빨간색, 충돌 지점에 점
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, hit.point);
                Gizmos.DrawSphere(hit.point, 0.05f);
            }
            else
            {
                // 통과 레이 → 초록색
                Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
                Gizmos.DrawLine(transform.position, transform.position + dir * viewRadius);
            }
        }
    }
#endif
}