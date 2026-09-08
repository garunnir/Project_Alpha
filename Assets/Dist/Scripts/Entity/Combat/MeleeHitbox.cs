// ============================================================
// MeleeHitbox — 근접 cue 시점 Overlap 조회 + 무기축 접촉(치명타 훅)
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;
using UnityEngine.Rendering;

public readonly struct MeleeHitContact
{
    public readonly Vector3 WorldPoint;

    /// <summary>무기 축 0=손/자루 ~ 1=끝. 치명타는 아직 미사용.</summary>
    public readonly float WeaponReach01;

    public MeleeHitContact(Vector3 worldPoint, float weaponReach01)
    {
        WorldPoint = worldPoint;
        WeaponReach01 = Mathf.Clamp01(weaponReach01);
    }
}

public readonly struct MeleeHitboxPose
{
    public readonly Vector3 Origin;
    public readonly Vector3 Axis;
    public readonly Vector3 Center;
    public readonly Vector3 HalfExtents;
    public readonly Quaternion Rotation;
    public readonly float Range;

    public MeleeHitboxPose(
        Vector3 origin,
        Vector3 axis,
        Vector3 center,
        Vector3 halfExtents,
        Quaternion rotation,
        float range)
    {
        Origin = origin;
        Axis = axis;
        Center = center;
        HalfExtents = halfExtents;
        Rotation = rotation;
        Range = range;
    }

    public bool IsValid => Range > CharacterAttacker.MinRayDistance;
}

public static class MeleeHitbox
{
    public const int BufferSize = 16;
    public const float DebugCueHoldSeconds = 0.8f;
    public const float DebugContactRadius = 0.06f;

    public static readonly Color PreviewWire = new Color(1f, 0.85f, 0.2f, 1f);
    public static readonly Color CueMissWire = new Color(1f, 0.4f, 0.12f, 1f);
    public static readonly Color CueHitWire = new Color(0.25f, 1f, 0.35f, 1f);
    public static readonly Color ContactMark = new Color(1f, 0.2f, 0.85f, 1f);

    public static bool TryGetPose(
        CharacterAttacker attacker,
        ItemData item,
        CombatLeaf action,
        WeaponAttack attack,
        out MeleeHitboxPose pose)
    {
        pose = default;
        if (attacker == null)
            return false;

        Vector3 origin = attacker.ResolveOrigin();
        Vector3 axis = attacker.ResolveSwingAxis();
        float range = CombatMath.RangeMeters(item, action);
        if (range <= CharacterAttacker.MinRayDistance)
            return false;

        float halfW = WeaponAttack.HitboxHalfWidthOf(attack);
        float halfH = WeaponAttack.HitboxHalfHeightOf(attack);
        Vector3 center = origin + axis * (range * 0.5f);
        Vector3 halfExtents = new Vector3(halfW, halfH, range * 0.5f);
        Quaternion rotation = Quaternion.LookRotation(axis, Vector3.up);
        pose = new MeleeHitboxPose(origin, axis, center, halfExtents, rotation, range);
        return true;
    }

    public static int Collect(
        CharacterAttacker attacker,
        ItemData item,
        CombatLeaf action,
        WeaponAttack attack,
        Collider[] colliderBuffer,
        CharacterBodyHost[] hosts,
        MeleeHitContact[] contacts)
    {
        if (attacker == null ||
            colliderBuffer == null ||
            hosts == null ||
            contacts == null)
            return 0;

        if (!TryGetPose(attacker, item, action, attack, out MeleeHitboxPose pose))
            return 0;

        int hitCount = Physics.OverlapBoxNonAlloc(
            pose.Center,
            pose.HalfExtents,
            colliderBuffer,
            pose.Rotation,
            ~0,
            QueryTriggerInteraction.Ignore);

        int cap = hosts.Length < contacts.Length ? hosts.Length : contacts.Length;
        int written = 0;
        for (int i = 0; i < hitCount && written < cap; i++)
        {
            Collider col = colliderBuffer[i];
            if (!TryResolveBody(col, attacker, out CharacterBodyHost host))
                continue;
            if (Contains(hosts, written, host))
                continue;

            Vector3 targetCenter = CharacterAttacker.ResolveBodyCenter(host.transform, col);
            contacts[written] = ComputeContact(
                pose.Origin, pose.Axis, pose.Range, col, targetCenter);
            hosts[written] = host;
            written++;
        }

        return written;
    }

    public static void DrawDebugWire(in MeleeHitboxPose pose, Color color, float duration)
    {
        if (!pose.IsValid)
            return;

        GetCorners(pose, out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3,
            out Vector3 p4, out Vector3 p5, out Vector3 p6, out Vector3 p7);
        DrawEdge(p0, p1, color, duration);
        DrawEdge(p1, p2, color, duration);
        DrawEdge(p2, p3, color, duration);
        DrawEdge(p3, p0, color, duration);
        DrawEdge(p4, p5, color, duration);
        DrawEdge(p5, p6, color, duration);
        DrawEdge(p6, p7, color, duration);
        DrawEdge(p7, p4, color, duration);
        DrawEdge(p0, p4, color, duration);
        DrawEdge(p1, p5, color, duration);
        DrawEdge(p2, p6, color, duration);
        DrawEdge(p3, p7, color, duration);
        Debug.DrawLine(pose.Origin, pose.Origin + pose.Axis * pose.Range, color, duration, false);
    }

    public static void DrawGizmoWire(in MeleeHitboxPose pose, Color color)
    {
        if (!pose.IsValid)
            return;

        Color previous = Gizmos.color;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.color = color;
        Gizmos.matrix = Matrix4x4.TRS(pose.Center, pose.Rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, pose.HalfExtents * 2f);
        Gizmos.matrix = previousMatrix;
        Gizmos.DrawLine(pose.Origin, pose.Origin + pose.Axis * pose.Range);
        Gizmos.color = previous;
    }

    public static void DrawDebugContact(Vector3 worldPoint, float duration)
    {
        Color color = ContactMark;
        Vector3 up = Vector3.up * DebugContactRadius;
        Vector3 right = Vector3.right * DebugContactRadius;
        Vector3 fwd = Vector3.forward * DebugContactRadius;
        Debug.DrawLine(worldPoint - up, worldPoint + up, color, duration, false);
        Debug.DrawLine(worldPoint - right, worldPoint + right, color, duration, false);
        Debug.DrawLine(worldPoint - fwd, worldPoint + fwd, color, duration, false);
    }

    public static void DrawGizmoContact(Vector3 worldPoint)
    {
        Color previous = Gizmos.color;
        Gizmos.color = ContactMark;
        Gizmos.DrawWireSphere(worldPoint, DebugContactRadius);
        Gizmos.color = previous;
    }

    static void GetCorners(
        in MeleeHitboxPose pose,
        out Vector3 p0,
        out Vector3 p1,
        out Vector3 p2,
        out Vector3 p3,
        out Vector3 p4,
        out Vector3 p5,
        out Vector3 p6,
        out Vector3 p7)
    {
        Vector3 c = pose.Center;
        Vector3 x = pose.Rotation * new Vector3(pose.HalfExtents.x, 0f, 0f);
        Vector3 y = pose.Rotation * new Vector3(0f, pose.HalfExtents.y, 0f);
        Vector3 z = pose.Rotation * new Vector3(0f, 0f, pose.HalfExtents.z);
        p0 = c - x - y - z;
        p1 = c + x - y - z;
        p2 = c + x - y + z;
        p3 = c - x - y + z;
        p4 = c - x + y - z;
        p5 = c + x + y - z;
        p6 = c + x + y + z;
        p7 = c - x + y + z;
    }

    static Material _glMaterial;

    public static void DrawGl(
        in MeleeHitboxPose pose,
        Color color,
        Vector3[] contacts,
        int contactCount,
        Camera cam)
    {
        if (!pose.IsValid || cam == null || !TryEnsureGlMaterial())
            return;

        _glMaterial.SetPass(0);
        GL.PushMatrix();
        GL.LoadProjectionMatrix(GL.GetGPUProjectionMatrix(cam.projectionMatrix, true));
        GL.modelview = cam.worldToCameraMatrix;
        GL.Begin(GL.LINES);
        GL.Color(color);
        GetCorners(pose, out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3,
            out Vector3 p4, out Vector3 p5, out Vector3 p6, out Vector3 p7);
        GlEdge(p0, p1);
        GlEdge(p1, p2);
        GlEdge(p2, p3);
        GlEdge(p3, p0);
        GlEdge(p4, p5);
        GlEdge(p5, p6);
        GlEdge(p6, p7);
        GlEdge(p7, p4);
        GlEdge(p0, p4);
        GlEdge(p1, p5);
        GlEdge(p2, p6);
        GlEdge(p3, p7);
        GlEdge(pose.Origin, pose.Origin + pose.Axis * pose.Range);

        if (contacts != null)
        {
            GL.Color(ContactMark);
            int n = contactCount < contacts.Length ? contactCount : contacts.Length;
            Vector3 up = Vector3.up * DebugContactRadius;
            Vector3 right = Vector3.right * DebugContactRadius;
            Vector3 fwd = Vector3.forward * DebugContactRadius;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = contacts[i];
                GlEdge(p - up, p + up);
                GlEdge(p - right, p + right);
                GlEdge(p - fwd, p + fwd);
            }
        }

        GL.End();
        GL.PopMatrix();
    }

    static bool TryEnsureGlMaterial()
    {
        if (_glMaterial != null)
            return true;

        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null)
        {
            Debug.LogWarning("[MeleeHitbox] Hidden/Internal-Colored shader missing; GL debug off.");
            return false;
        }

        _glMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave,
            name = "MeleeHitboxDebug"
        };
        _glMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        _glMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        _glMaterial.SetInt("_Cull", (int)CullMode.Off);
        _glMaterial.SetInt("_ZWrite", 0);
        _glMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
        return true;
    }

    static void GlEdge(Vector3 a, Vector3 b)
    {
        GL.Vertex(a);
        GL.Vertex(b);
    }

    static void DrawEdge(Vector3 a, Vector3 b, Color color, float duration) =>
        Debug.DrawLine(a, b, color, duration, false);

    public static MeleeHitContact ComputeContact(
        Vector3 origin,
        Vector3 axis,
        float range,
        Collider targetCollider,
        Vector3 targetCenter)
    {
        float t = Mathf.Clamp(Vector3.Dot(targetCenter - origin, axis), 0f, range);
        Vector3 axisPoint = origin + axis * t;
        Vector3 worldPoint = targetCollider != null
            ? targetCollider.ClosestPoint(axisPoint)
            : targetCenter;
        float along = Vector3.Dot(worldPoint - origin, axis);
        float reach01 = range > 0f ? Mathf.Clamp01(along / range) : 0f;
        return new MeleeHitContact(worldPoint, reach01);
    }

    static bool TryResolveBody(
        Collider collider,
        CharacterAttacker attacker,
        out CharacterBodyHost host)
    {
        host = null;
        if (collider == null || attacker.IsOwnCollider(collider))
            return false;
        return CharacterBodyResolve.TryResolveBodyHost(collider, out host);
    }

    static bool Contains(CharacterBodyHost[] hosts, int count, CharacterBodyHost host)
    {
        for (int i = 0; i < count; i++)
        {
            if (ReferenceEquals(hosts[i], host))
                return true;
        }

        return false;
    }
}
