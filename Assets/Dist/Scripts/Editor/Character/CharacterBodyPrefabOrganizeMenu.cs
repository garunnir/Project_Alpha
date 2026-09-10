// ============================================================
// CharacterBodyPrefabOrganizeMenu — NpcSample 본체 자식 GO 역할 분리 Patch
// ============================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public static class CharacterBodyPrefabOrganizeMenu
{
    public const string NpcSamplePrefabPath = "Assets/Dist/Visual/Prefabs/3D/NpcSample.prefab";

    static readonly Type[] RootKeepTypes =
    {
        typeof(Transform),
        typeof(Rigidbody),
        typeof(CapsuleCollider),
        typeof(CharacterBodyRoot),
        typeof(CharacterBodyRefs),
        typeof(CharacterState),
        typeof(CharacterMotor),
        typeof(CharacterDefinitionBinder),
    };

    static readonly Type[] GameplayCoreTypes =
    {
        typeof(CharacterBodyHost),
        typeof(CharacterSkillsHost),
        typeof(CharacterTraitsHost),
        typeof(CharacterFootprintHost),
        typeof(CharacterSessionHub),
        typeof(CharacterActionHost),
        typeof(CharacterSightHost),
        typeof(CharacterArriveHost),
        typeof(PlayerInventoryHost),
        typeof(PlayerGearHost),
        typeof(PlayerEncumbranceHost),
        typeof(InventoryTimedMoveHost),
        typeof(NearbyContainerDetector),
        typeof(CharacterAttacker),
        typeof(BodyEffectTicker),
        typeof(CharacterPainHost),
        typeof(CharacterHitReact),
        typeof(CharacterImbalanceHost),
        typeof(PlayerNeedsHost),
        typeof(CharacterMoodHost),
        typeof(CharacterClimateHost),
        typeof(CharacterFactionHost),
    };

    static readonly Type[] SensesTypes =
    {
        typeof(CharacterPresenceHost),
        typeof(CharacterVision),
        typeof(CharacterHearing),
        typeof(CharacterSenseGizmo),
    };

    static readonly Type[] PresentationTypes =
    {
        typeof(CharacterEmoteHost),
        typeof(CharacterSightFadeHost),
        typeof(CharacterAppearanceHost),
    };

    [MenuItem(DistMcpMenus.CharacterEnsureNpcSampleBodyRefs)]
    public static void EnsureNpcSampleBodyRefs()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(NpcSamplePrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError($"[CharacterBodyPrefabOrganizeMenu] Prefab not found: {NpcSamplePrefabPath}");
            return;
        }

        try
        {
            Undo.RegisterFullObjectHierarchyUndo(prefabRoot, "Ensure NpcSample Body Refs");

            if (prefabRoot.GetComponent<CharacterBodyRoot>() == null)
                prefabRoot.AddComponent<CharacterBodyRoot>();

            CharacterBodyRefs bodyRefs = prefabRoot.GetComponent<CharacterBodyRefs>();
            if (bodyRefs == null)
                bodyRefs = prefabRoot.AddComponent<CharacterBodyRefs>();

            StripBodyMarkersFromChildren(prefabRoot);
            EnsureSensesComponents(prefabRoot);
            EnsureFactionHostOnGameplayCore(prefabRoot);
            EnsureArriveHostOnGameplayCore(prefabRoot);
            EnsureVaultHostOnRoot(prefabRoot);
            EnsureSwimHostOnRoot(prefabRoot);
            EnsureVaultIkHostOnAnimator(prefabRoot);
            bodyRefs.Invalidate();
            bodyRefs.ResolveFromHierarchy();

            if (!ValidateBodyRefsResolution(bodyRefs, out string refsError))
            {
                Debug.LogError(
                    "[CharacterBodyPrefabOrganizeMenu] CharacterBodyRefs validation failed: "
                    + refsError,
                    prefabRoot);
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, NpcSamplePrefabPath);
            Debug.Log(
                "[CharacterBodyPrefabOrganizeMenu] NpcSample BodyRefs ensured on root.",
                prefabRoot);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [MenuItem(DistMcpMenus.CharacterOrganizeNpcSampleBody)]
    public static void OrganizeNpcSamplePrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(NpcSamplePrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError($"[CharacterBodyPrefabOrganizeMenu] Prefab not found: {NpcSamplePrefabPath}");
            return;
        }

        try
        {
            Undo.RegisterFullObjectHierarchyUndo(prefabRoot, "Organize NpcSample Body");

            Transform gameplayCore = EnsureChild(prefabRoot.transform, "GameplayCore");
            Transform senses = EnsureChild(prefabRoot.transform, "Senses");
            Transform presentation = EnsureChild(prefabRoot.transform, "Presentation");

            if (prefabRoot.GetComponent<CharacterBodyRoot>() == null)
                prefabRoot.AddComponent<CharacterBodyRoot>();

            CharacterBodyRefs bodyRefs = prefabRoot.GetComponent<CharacterBodyRefs>();
            if (bodyRefs == null)
                bodyRefs = prefabRoot.AddComponent<CharacterBodyRefs>();

            DeduplicateChildComponents(gameplayCore.gameObject, GameplayCoreTypes);
            DeduplicateChildComponents(senses.gameObject, SensesTypes);
            DeduplicateChildComponents(presentation.gameObject, PresentationTypes);

            MoveTypes(prefabRoot, presentation.gameObject, PresentationTypes);
            MoveTypes(prefabRoot, senses.gameObject, SensesTypes);
            MoveTypes(prefabRoot, gameplayCore.gameObject, GameplayCoreTypes);
            StripForeignComponents(presentation.gameObject, PresentationTypes);
            StripForeignComponents(senses.gameObject, SensesTypes);
            StripForeignComponents(gameplayCore.gameObject, GameplayCoreTypes);
            StripMovedComponentsFromRoot(prefabRoot);
            StripBodyMarkersFromChildren(prefabRoot);
            EnsureSensesComponents(prefabRoot);
            EnsureFactionHostOnGameplayCore(prefabRoot);
            EnsureArriveHostOnGameplayCore(prefabRoot);
            EnsureVaultHostOnRoot(prefabRoot);
            EnsureSwimHostOnRoot(prefabRoot);
            EnsureVaultIkHostOnAnimator(prefabRoot);

            RemoveLegacyMissingScripts(prefabRoot);
            RemoveMissingScripts(prefabRoot);

            if (!ValidateColliderBodyHostResolution(prefabRoot, out string resolveError))
            {
                Debug.LogError(
                    "[CharacterBodyPrefabOrganizeMenu] Collider → CharacterBodyHost validation failed: "
                    + resolveError,
                    prefabRoot);
            }

            bodyRefs.Invalidate();
            bodyRefs.ResolveFromHierarchy();
            if (!ValidateBodyRefsResolution(bodyRefs, out string refsError))
            {
                Debug.LogError(
                    "[CharacterBodyPrefabOrganizeMenu] CharacterBodyRefs validation failed: "
                    + refsError,
                    prefabRoot);
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, NpcSamplePrefabPath);
            Debug.Log(
                "[CharacterBodyPrefabOrganizeMenu] NpcSample organized: root physics + GameplayCore / Senses / Presentation.",
                prefabRoot);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    static Transform EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing;

        GameObject child = new(name);
        Undo.RegisterCreatedObjectUndo(child, "Create body child");
        child.transform.SetParent(parent, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
        child.layer = parent.gameObject.layer;
        return child.transform;
    }

    static void DeduplicateChildComponents(GameObject child, Type[] types)
    {
        for (int i = 0; i < types.Length; i++)
        {
            Type type = types[i];
            if (type == typeof(Transform))
                continue;

            Component[] matches = child.GetComponents(type);
            for (int j = 1; j < matches.Length; j++)
                Undo.DestroyObjectImmediate(matches[j]);
        }
    }

    static void MoveTypes(GameObject fromRoot, GameObject to, Type[] copyOrder)
    {
        for (int i = 0; i < copyOrder.Length; i++)
        {
            Type type = copyOrder[i];
            if (type == typeof(Transform))
                continue;

            Component source = fromRoot.GetComponent(type);
            if (source == null || source.gameObject == to)
                continue;

            if (to.GetComponent(type) != null)
                continue;

            if (!ComponentUtility.CopyComponent(source))
            {
                Debug.LogWarning($"[CharacterBodyPrefabOrganizeMenu] Copy failed: {type.Name}", fromRoot);
                continue;
            }

            if (!ComponentUtility.PasteComponentAsNew(to))
                Debug.LogWarning($"[CharacterBodyPrefabOrganizeMenu] Paste failed: {type.Name}", to);
        }
    }

    static void StripForeignComponents(GameObject child, Type[] allowedTypes)
    {
        HashSet<Type> allowed = new(allowedTypes.Length + 1) { typeof(Transform) };
        for (int i = 0; i < allowedTypes.Length; i++)
            allowed.Add(allowedTypes[i]);

        Type pinnedAllowedType = null;
        MonoBehaviour[] behaviours = child.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || !allowed.Contains(behaviour.GetType()))
                continue;

            if (!PinsForeignOnObject(behaviour.GetType(), child, allowed))
                continue;

            pinnedAllowedType = behaviour.GetType();
            if (!ComponentUtility.CopyComponent(behaviour))
            {
                Debug.LogWarning(
                    $"[CharacterBodyPrefabOrganizeMenu] Could not copy pinned allowed component: {pinnedAllowedType.Name}",
                    child);
                pinnedAllowedType = null;
                break;
            }

            Undo.DestroyObjectImmediate(behaviour);
            break;
        }

        List<MonoBehaviour> foreign = new();
        behaviours = child.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || allowed.Contains(behaviour.GetType()))
                continue;

            foreign.Add(behaviour);
        }

        foreign.Sort((a, b) => CompareDestroyOrder(a.GetType(), b.GetType()));
        for (int i = 0; i < foreign.Count; i++)
        {
            MonoBehaviour behaviour = foreign[i];
            if (behaviour != null)
                Undo.DestroyObjectImmediate(behaviour);
        }

        if (pinnedAllowedType != null && child.GetComponent(pinnedAllowedType) == null)
        {
            if (!ComponentUtility.PasteComponentAsNew(child))
            {
                Debug.LogWarning(
                    $"[CharacterBodyPrefabOrganizeMenu] Could not restore pinned allowed component: {pinnedAllowedType.Name}",
                    child);
            }
        }

        StripForeignComponentsWithoutPin(child, allowed);
    }

    static void StripForeignComponentsWithoutPin(GameObject child, HashSet<Type> allowed)
    {
        for (int pass = 0; pass < 4; pass++)
        {
            List<MonoBehaviour> foreign = new();
            MonoBehaviour[] behaviours = child.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || allowed.Contains(behaviour.GetType()))
                    continue;

                foreign.Add(behaviour);
            }

            if (foreign.Count == 0)
                break;

            foreign.Sort((a, b) => CompareDestroyOrder(a.GetType(), b.GetType()));
            for (int i = 0; i < foreign.Count; i++)
            {
                MonoBehaviour behaviour = foreign[i];
                if (behaviour != null)
                    Undo.DestroyObjectImmediate(behaviour);
            }
        }
    }

    static bool PinsForeignOnObject(Type allowedType, GameObject go, HashSet<Type> allowed)
    {
        MonoBehaviour[] behaviours = go.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || allowed.Contains(behaviour.GetType()))
                continue;

            if (TypeRequires(allowedType, behaviour.GetType()))
                return true;
        }

        return false;
    }

    static int CompareDestroyOrder(Type dependent, Type dependency)
    {
        if (TypeRequires(dependent, dependency))
            return -1;

        if (TypeRequires(dependency, dependent))
            return 1;

        return 0;
    }

    static void StripMovedComponentsFromRoot(GameObject fromRoot)
    {
        List<MonoBehaviour> foreign = new();
        MonoBehaviour[] behaviours = fromRoot.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || ShouldKeepOnRoot(behaviour.GetType()))
                continue;

            foreign.Add(behaviour);
        }

        foreign.Sort((a, b) => CompareDestroyOrder(a.GetType(), b.GetType()));
        for (int i = 0; i < foreign.Count; i++)
        {
            MonoBehaviour behaviour = foreign[i];
            if (behaviour != null)
                Undo.DestroyObjectImmediate(behaviour);
        }
    }

    static bool TypeRequires(Type behaviourType, Type requiredType)
    {
        object[] attributes = behaviourType.GetCustomAttributes(typeof(RequireComponent), true);
        for (int j = 0; j < attributes.Length; j++)
        {
            RequireComponent require = (RequireComponent)attributes[j];
            if (require.m_Type0 == requiredType ||
                require.m_Type1 == requiredType ||
                require.m_Type2 == requiredType)
                return true;
        }

        return false;
    }

    static bool ShouldKeepOnRoot(Type type)
    {
        if (type == typeof(CharacterBodyRoot) ||
            type == typeof(CharacterBodyRefs) ||
            type == typeof(CharacterState) ||
            type == typeof(CharacterMotor) ||
            type == typeof(CharacterDefinitionBinder) ||
            type == typeof(CharacterLocomotionAnim) ||
            type == typeof(CharacterVaultHost) ||
            type == typeof(CharacterSwimHost) ||
            type == typeof(CharacterFootDustVfx))
            return true;

        for (int i = 0; i < RootKeepTypes.Length; i++)
        {
            if (RootKeepTypes[i] == type)
                return true;
        }

        return false;
    }

    static void StripBodyMarkersFromChildren(GameObject prefabRoot)
    {
        Transform[] transforms = prefabRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transform = transforms[i];
            if (transform == null || transform.gameObject == prefabRoot)
                continue;

            GameObject child = transform.gameObject;
            CharacterBodyRoot bodyRoot = child.GetComponent<CharacterBodyRoot>();
            if (bodyRoot != null)
                Undo.DestroyObjectImmediate(bodyRoot);

            CharacterBodyRefs bodyRefs = child.GetComponent<CharacterBodyRefs>();
            if (bodyRefs != null)
                Undo.DestroyObjectImmediate(bodyRefs);
        }
    }

    static void EnsureSensesComponents(GameObject prefabRoot)
    {
        Transform senses = prefabRoot.transform.Find("Senses");
        if (senses == null)
            return;

        GameObject sensesGo = senses.gameObject;
        EnsureComponent<CharacterPresenceHost>(sensesGo);
        EnsureComponent<CharacterVision>(sensesGo);
        EnsureComponent<CharacterHearing>(sensesGo);
        EnsureComponent<CharacterSenseGizmo>(sensesGo);
    }

    static void EnsureFactionHostOnGameplayCore(GameObject prefabRoot)
    {
        Transform gameplayCore = prefabRoot.transform.Find("GameplayCore");
        if (gameplayCore == null)
            return;

        GameObject coreGo = gameplayCore.gameObject;
        CharacterFactionHost onCore = coreGo.GetComponent<CharacterFactionHost>();
        if (onCore != null)
        {
            RemoveDuplicateComponents<CharacterFactionHost>(prefabRoot, coreGo);
            return;
        }

        CharacterFactionHost[] all = prefabRoot.GetComponentsInChildren<CharacterFactionHost>(true);
        for (int i = 0; i < all.Length; i++)
        {
            CharacterFactionHost source = all[i];
            if (source == null || source.gameObject == coreGo)
                continue;

            if (ComponentUtility.CopyComponent(source))
                ComponentUtility.PasteComponentAsNew(coreGo);

            Undo.DestroyObjectImmediate(source);
            break;
        }

        if (coreGo.GetComponent<CharacterFactionHost>() == null)
            EnsureComponent<CharacterFactionHost>(coreGo);

        RemoveDuplicateComponents<CharacterFactionHost>(prefabRoot, coreGo);
    }

    static void EnsureArriveHostOnGameplayCore(GameObject prefabRoot)
    {
        Transform gameplayCore = prefabRoot.transform.Find("GameplayCore");
        if (gameplayCore == null)
            return;

        GameObject coreGo = gameplayCore.gameObject;
        if (coreGo.GetComponent<CharacterArriveHost>() == null)
            EnsureComponent<CharacterArriveHost>(coreGo);

        RemoveDuplicateComponents<CharacterArriveHost>(prefabRoot, coreGo);
    }

    static void EnsureSwimHostOnRoot(GameObject prefabRoot)
    {
        if (prefabRoot.GetComponent<CharacterState>() == null)
            return;

        if (prefabRoot.GetComponent<CharacterSwimHost>() == null)
            EnsureComponent<CharacterSwimHost>(prefabRoot);

        RemoveDuplicateComponents<CharacterSwimHost>(prefabRoot, prefabRoot);
    }

    static void EnsureVaultIkHostOnAnimator(GameObject prefabRoot)
    {
        Animator animator = prefabRoot.GetComponentInChildren<Animator>(true);
        if (animator == null)
            return;

        if (animator.GetComponent<CharacterVaultIkHost>() == null)
            EnsureComponent<CharacterVaultIkHost>(animator.gameObject);

        CharacterVaultIkHost[] all = prefabRoot.GetComponentsInChildren<CharacterVaultIkHost>(true);
        for (int i = 0; i < all.Length; i++)
        {
            CharacterVaultIkHost host = all[i];
            if (host == null || host.gameObject == animator.gameObject)
                continue;
            Undo.DestroyObjectImmediate(host);
        }
    }

    static void EnsureVaultHostOnRoot(GameObject prefabRoot)
    {
        if (prefabRoot.GetComponent<CharacterMotor>() == null)
            return;

        if (prefabRoot.GetComponent<CharacterVaultHost>() == null)
            EnsureComponent<CharacterVaultHost>(prefabRoot);

        RemoveDuplicateComponents<CharacterVaultHost>(prefabRoot, prefabRoot);
    }

    static void RemoveDuplicateComponents<T>(GameObject prefabRoot, GameObject keepOn) where T : Component
    {
        T[] all = prefabRoot.GetComponentsInChildren<T>(true);
        for (int i = 0; i < all.Length; i++)
        {
            T component = all[i];
            if (component == null || component.gameObject == keepOn)
                continue;

            Undo.DestroyObjectImmediate(component);
        }
    }

    static void EnsureComponent<T>(GameObject go) where T : Component
    {
        if (go.GetComponent<T>() == null)
            Undo.AddComponent<T>(go);
    }

    static void RemoveMissingScripts(GameObject root)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transforms[i].gameObject);
    }

    static void RemoveLegacyMissingScripts(GameObject root)
    {
        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = behaviours.Length - 1; i >= 0; i--)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            if (behaviour.GetType().Name == "CharacterActionCancelConsumer")
                Undo.DestroyObjectImmediate(behaviour);
        }
    }

    static bool ValidateColliderBodyHostResolution(GameObject prefabRoot, out string error)
    {
        error = null;
        if (prefabRoot == null)
        {
            error = "prefab root is null";
            return false;
        }

        if (prefabRoot.GetComponentInChildren<CharacterBodyHost>(true) == null)
        {
            error = "no CharacterBodyHost under prefab root";
            return false;
        }

        Collider[] colliders = prefabRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || col.isTrigger)
                continue;

            if (CharacterBodyResolve.TryResolveBodyHost(col, out _))
                continue;

            error = $"Collider on '{GetHierarchyPath(col.transform)}' does not resolve to CharacterBodyHost.";
            return false;
        }

        return true;
    }

    static bool ValidateBodyRefsResolution(CharacterBodyRefs refs, out string error)
    {
        error = null;
        if (refs == null)
        {
            error = "CharacterBodyRefs is null";
            return false;
        }

        if (refs.State == null)
        {
            error = "missing CharacterState";
            return false;
        }

        if (refs.Motor == null)
        {
            error = "missing CharacterMotor";
            return false;
        }

        if (refs.BodyHost == null)
        {
            error = "missing CharacterBodyHost";
            return false;
        }

        if (refs.BodyCollider == null)
        {
            error = "missing body Collider";
            return false;
        }

        if (refs.Vision == null)
        {
            error = "missing CharacterVision";
            return false;
        }

        if (refs.Attacker == null)
        {
            error = "missing CharacterAttacker";
            return false;
        }

        if (refs.FactionHost == null)
        {
            error = "missing CharacterFactionHost";
            return false;
        }

        if (refs.LocomotionAnim == null)
        {
            error = "missing CharacterLocomotionAnim";
            return false;
        }

        if (refs.Motor != null && refs.VaultHost == null)
        {
            error = "missing CharacterVaultHost";
            return false;
        }

        return true;
    }

    static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        string path = transform.name;
        Transform parent = transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }
}
#endif
