// ============================================================
// CharacterBodyResolve — 본체 루트·자식 간 GetComponent SSOT
// ============================================================

using UnityEngine;

public static class CharacterBodyResolve
{
    public static GameObject GetBodyRoot(GameObject go)
    {
        if (go == null)
            return null;

        if (go.TryGetComponent(out CharacterBodyRoot _))
            return go;

        CharacterBodyRoot marker = go.GetComponentInParent<CharacterBodyRoot>();
        if (marker != null)
            return marker.gameObject;

        // Prefab OnValidate 등에서 BodyRoot 조회가 실패할 때 BodyRefs가 루트 마커.
        CharacterBodyRefs refs = go.GetComponentInParent<CharacterBodyRefs>();
        if (refs != null)
            return refs.gameObject;

        return go;
    }

    /// <summary>프리팹에 미리 있는 <see cref="CharacterBodyRefs"/> (런타임 AddComponent 금지).</summary>
    public static CharacterBodyRefs GetRefs(Component bodyMember)
    {
        if (bodyMember == null)
            return null;

        CharacterBodyRefs refs = bodyMember.GetComponentInParent<CharacterBodyRefs>();
        if (refs == null)
        {
            GameObject root = GetBodyRoot(bodyMember.gameObject);
            if (root != null)
                root.TryGetComponent(out refs);
        }

        if (refs == null)
            return null;

        refs.ResolveFromHierarchy();
        return refs;
    }

    public static T GetInBody<T>(Component component) where T : Component
    {
        if (component == null)
            return null;

        if (component.TryGetComponent(out T self))
            return self;

        CharacterBodyRefs refs = GetRefs(component);
        if (refs != null && refs.TryGet(out T cached))
            return cached;

        GameObject root = GetBodyRoot(component.gameObject);
        return root != null ? root.GetComponentInChildren<T>(true) : null;
    }

    public static bool TryGetInBody<T>(Component component, out T result) where T : Component
    {
        result = GetInBody<T>(component);
        return result != null;
    }

    /// <summary>전투·히트스캔 SSOT. Collider는 루트, Host는 자식일 수 있다.</summary>
    public static bool TryResolveBodyHost(Collider collider, out CharacterBodyHost host)
    {
        host = GetInBody<CharacterBodyHost>(collider);
        if (host == null || host.Body == null || host.Body.IsDeadState)
        {
            host = null;
            return false;
        }

        return true;
    }

    public static bool IsSameBodyRoot(Component a, Component b)
    {
        if (a == null || b == null)
            return false;

        GameObject rootA = GetBodyRoot(a.gameObject);
        GameObject rootB = GetBodyRoot(b.gameObject);
        return rootA != null && rootA == rootB;
    }

    /// <summary>콜라이더가 본체 루트 트리(루트·자식)에 속하는지.</summary>
    public static bool IsColliderOnBody(Component bodyMember, Collider collider)
    {
        if (bodyMember == null || collider == null)
            return false;

        GameObject root = GetBodyRoot(bodyMember.gameObject);
        if (root == null)
            return false;

        Transform colliderTransform = collider.transform;
        return colliderTransform == root.transform || colliderTransform.IsChildOf(root.transform);
    }

    /// <summary>본체 루트 콜라이더(보통 CapsuleCollider). Host가 자식에 있을 때 SSOT.</summary>
    public static Collider GetBodyCollider(Component bodyMember)
    {
        if (bodyMember == null)
            return null;

        CharacterBodyRefs refs = GetRefs(bodyMember);
        if (refs != null)
        {
            Collider collider = refs.BodyCollider;
            if (collider != null)
                return collider;
        }

        GameObject root = GetBodyRoot(bodyMember.gameObject);
        if (root != null && root.TryGetComponent(out Collider rootCollider))
            return rootCollider;

        return GetInBody<CapsuleCollider>(bodyMember);
    }

    public static bool TryGetBodyCollider(Component bodyMember, out Collider collider)
    {
        collider = GetBodyCollider(bodyMember);
        return collider != null;
    }
}

public static class CharacterBodyComponentExtensions
{
    public static bool TryGetBodyComponent<T>(this Component body, out T component) where T : Component
    {
        component = null;
        if (body == null)
            return false;

        if (body.TryGetComponent(out component))
            return true;

        component = CharacterBodyResolve.GetInBody<T>(body);
        return component != null;
    }

    public static T GetBodyComponent<T>(this Component body) where T : Component
    {
        body.TryGetBodyComponent(out T component);
        return component;
    }

    public static CharacterBodyRefs GetBodyRefs(this Component body) =>
        CharacterBodyResolve.GetRefs(body);
}

public static class CharacterBodyGameObjectExtensions
{
    public static bool TryGetBodyComponent<T>(this GameObject body, out T component) where T : Component
    {
        if (body == null)
        {
            component = null;
            return false;
        }

        return body.transform.TryGetBodyComponent(out component);
    }

    public static T GetBodyComponent<T>(this GameObject body) where T : Component
    {
        body.TryGetBodyComponent(out T component);
        return component;
    }

    public static CharacterBodyRefs GetBodyRefs(this GameObject body) =>
        body != null ? CharacterBodyResolve.GetRefs(body.transform) : null;
}
