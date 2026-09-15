// ============================================================
// CharacterBodyPrefabOrganizeMenu — NpcSample 본체 자식 GO 역할 분리 Patch
// ============================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
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
        typeof(CharacterActionHost),
        typeof(CharacterSightHost),
        typeof(CharacterArriveHost),
    };

    static readonly Type[] SensesTypes = { };

    static readonly Type[] PresentationTypes = { };

    const string SelectionLayerConfigPath = "Assets/Settings/Outline/SelectionLayerConfig.asset";

    [MenuItem(DistMcpMenus.CharacterEnsureNpcSampleBodyRefs)]
    public static void EnsureNpcSampleBodyRefs()
    {
        StripConvertedInventoryModuleYaml(NpcSamplePrefabPath);

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
            StripSensesLegacyComponents(prefabRoot);
            EnsurePresentationFolder(prefabRoot);
            StripPresentationLegacyComponents(prefabRoot);
            EnsurePresentationWiring(prefabRoot);
            StripStatsVitalityLegacyComponents(prefabRoot);
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

            SaveNpcSamplePrefab(prefabRoot);
            Debug.Log(
                "[CharacterBodyPrefabOrganizeMenu] NpcSample BodyRefs ensured on root.",
                prefabRoot);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        StripConvertedInventoryModuleYaml(NpcSamplePrefabPath);
    }

    [MenuItem(DistMcpMenus.CharacterOrganizeNpcSampleBody)]
    public static void OrganizeNpcSamplePrefab()
    {
        StripConvertedInventoryModuleYaml(NpcSamplePrefabPath);

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
            StripSensesLegacyComponents(prefabRoot);
            EnsurePresentationFolder(prefabRoot);
            StripPresentationLegacyComponents(prefabRoot);
            EnsurePresentationWiring(prefabRoot);
            StripStatsVitalityLegacyComponents(prefabRoot);
            EnsureArriveHostOnGameplayCore(prefabRoot);
            EnsureVaultHostOnRoot(prefabRoot);
            EnsureSwimHostOnRoot(prefabRoot);
            EnsureVaultIkHostOnAnimator(prefabRoot);

            RemoveLegacyMissingScripts(prefabRoot);
            RemoveMissingScripts(prefabRoot);
            StripSensesLegacyComponents(prefabRoot);
            StripPresentationLegacyComponents(prefabRoot);
            StripStatsVitalityLegacyComponents(prefabRoot);

            bodyRefs.Invalidate();
            bodyRefs.ResolveFromHierarchy();
            if (!ValidateColliderBodyHostResolution(prefabRoot, out string resolveError))
            {
                Debug.LogError(
                    "[CharacterBodyPrefabOrganizeMenu] Collider → CharacterBodyHost validation failed: "
                    + resolveError,
                    prefabRoot);
            }

            if (!ValidateBodyRefsResolution(bodyRefs, out string refsError))
            {
                Debug.LogError(
                    "[CharacterBodyPrefabOrganizeMenu] CharacterBodyRefs validation failed: "
                    + refsError,
                    prefabRoot);
            }

            SaveNpcSamplePrefab(prefabRoot);
            Debug.Log(
                "[CharacterBodyPrefabOrganizeMenu] NpcSample organized: root physics + GameplayCore / Senses / Presentation.",
                prefabRoot);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        StripConvertedInventoryModuleYaml(NpcSamplePrefabPath);
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
        EnsureChild(prefabRoot.transform, "Senses");
    }

    static void EnsurePresentationFolder(GameObject prefabRoot)
    {
        EnsureChild(prefabRoot.transform, "Presentation");
    }

    static void StripSensesLegacyComponents(GameObject prefabRoot)
    {
        StripFolderLegacyComponents(prefabRoot, "Senses");
    }

    static void StripPresentationLegacyComponents(GameObject prefabRoot)
    {
        StripFolderLegacyComponents(prefabRoot, "Presentation");
    }

    static void StripFolderLegacyComponents(GameObject prefabRoot, string childName)
    {
        Transform folder = prefabRoot.transform.Find(childName);
        if (folder == null)
            return;

        GameObject folderGo = folder.gameObject;
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(folderGo);

        Component[] components = folderGo.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null || component is Transform)
                continue;

            Undo.DestroyObjectImmediate(component);
        }

        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(folderGo);
    }

    static void EnsurePresentationWiring(GameObject prefabRoot)
    {
        CharacterBodyRefs refs = prefabRoot.GetComponent<CharacterBodyRefs>();
        if (refs == null)
            return;

        SerializedObject so = new(refs);

        SerializedProperty renderRoot = so.FindProperty("_renderRoot");
        if (renderRoot != null && renderRoot.objectReferenceValue == null)
        {
            Transform pivot = prefabRoot.transform.Find(CharacterBodyRefs.RenderPivotChildName);
            if (pivot != null)
                renderRoot.objectReferenceValue = pivot;
        }

        SerializedProperty selectionLayer = so.FindProperty("_selectionLayer");
        if (selectionLayer != null && selectionLayer.objectReferenceValue == null)
        {
            SelectionLayerConfig config =
                AssetDatabase.LoadAssetAtPath<SelectionLayerConfig>(SelectionLayerConfigPath);
            if (config != null)
                selectionLayer.objectReferenceValue = config;
        }

        SerializedProperty catalog = so.FindProperty("_emoteCatalog");
        if (catalog != null && catalog.objectReferenceValue == null)
        {
            CharacterEmoteCatalog emoteCatalog =
                AssetDatabase.LoadAssetAtPath<CharacterEmoteCatalog>(
                    CharacterEmoteCatalog.DefaultAssetPath);
            if (emoteCatalog != null)
                catalog.objectReferenceValue = emoteCatalog;
        }

        SerializedProperty needsSettings = so.FindProperty("_needsSettings");
        if (needsSettings != null && needsSettings.objectReferenceValue == null)
        {
            PlayerNeedsSettings needs =
                AssetDatabase.LoadAssetAtPath<PlayerNeedsSettings>(PlayerNeedsSettings.DefaultAssetPath);
            if (needs != null)
                needsSettings.objectReferenceValue = needs;
        }

        SerializedProperty moodSettings = so.FindProperty("_moodSettings");
        if (moodSettings != null && moodSettings.objectReferenceValue == null)
        {
            MoodSettings mood = AssetDatabase.LoadAssetAtPath<MoodSettings>(MoodSettings.DefaultAssetPath);
            if (mood != null)
                moodSettings.objectReferenceValue = mood;
        }

        SerializedProperty hitStop = so.FindProperty("_hitStopSettings");
        if (hitStop != null && hitStop.objectReferenceValue == null)
        {
            CombatHitStopSettings settings =
                AssetDatabase.LoadAssetAtPath<CombatHitStopSettings>(CombatHitStopSettings.DefaultAssetPath);
            if (settings != null)
                hitStop.objectReferenceValue = settings;
        }

        SerializedProperty weaponCatalog = so.FindProperty("_weaponPresentationCatalog");
        if (weaponCatalog != null && weaponCatalog.objectReferenceValue == null)
        {
            WeaponPresentationCatalog weapons =
                AssetDatabase.LoadAssetAtPath<WeaponPresentationCatalog>(
                    WeaponPresentationCatalog.DefaultAssetPath);
            if (weapons != null)
                weaponCatalog.objectReferenceValue = weapons;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void StripStatsVitalityLegacyComponents(GameObject prefabRoot)
    {
        if (prefabRoot == null)
            return;

        StripConvertedModuleComponentsByName(prefabRoot, ConvertedStatsVitalityTypeNames);
        Transform gameplayCore = prefabRoot.transform.Find("GameplayCore");
        if (gameplayCore != null)
            StripConvertedModuleComponentsByName(gameplayCore.gameObject, ConvertedStatsVitalityTypeNames);
    }

    static void StripConvertedModuleComponentsByName(GameObject go, string[] typeNames)
    {
        if (go == null || typeNames == null || typeNames.Length == 0)
            return;

        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
                continue;

            string typeName = component.GetType().Name;
            for (int j = 0; j < typeNames.Length; j++)
            {
                if (typeName != typeNames[j])
                    continue;
                Undo.DestroyObjectImmediate(component);
                break;
            }
        }

        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
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

        if (prefabRoot.GetComponent<CharacterBodyRefs>() == null)
        {
            error = "no CharacterBodyRefs on prefab root";
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
            error = "missing CharacterVision module";
            return false;
        }

        if (refs.Hearing == null)
        {
            error = "missing CharacterHearing module";
            return false;
        }

        if (refs.Presence == null)
        {
            error = "missing CharacterPresenceHost module";
            return false;
        }

        if (refs.Appearance == null)
        {
            error = "missing CharacterAppearanceHost module";
            return false;
        }

        if (refs.SightFade == null)
        {
            error = "missing CharacterSightFadeHost module";
            return false;
        }

        if (refs.SelectionOutline == null)
        {
            error = "missing CharacterSelectionOutlineHost module";
            return false;
        }

        if (refs.Emote == null)
        {
            error = "missing CharacterEmoteHost module";
            return false;
        }

        if (refs.InventoryHost == null)
        {
            error = "missing PlayerInventoryHost module";
            return false;
        }

        if (refs.GearHost == null)
        {
            error = "missing PlayerGearHost module";
            return false;
        }

        if (refs.EncumbranceHost == null)
        {
            error = "missing PlayerEncumbranceHost module";
            return false;
        }

        if (refs.TimedMoveHost == null)
        {
            error = "missing InventoryTimedMoveHost module";
            return false;
        }

        if (refs.NearbyDetector == null)
        {
            error = "missing NearbyContainerDetector module";
            return false;
        }

        if (refs.FootprintHost == null)
        {
            error = "missing CharacterFootprintHost module";
            return false;
        }

        if (refs.NeedsHost == null)
        {
            error = "missing PlayerNeedsHost module";
            return false;
        }

        if (refs.MoodHost == null)
        {
            error = "missing CharacterMoodHost module";
            return false;
        }

        if (refs.ClimateHost == null)
        {
            error = "missing CharacterClimateHost module";
            return false;
        }

        if (refs.SkillsHost == null)
        {
            error = "missing CharacterSkillsHost module";
            return false;
        }

        if (refs.TraitsHost == null)
        {
            error = "missing CharacterTraitsHost module";
            return false;
        }

        if (refs.Attacker == null)
        {
            error = "missing CharacterAttacker module";
            return false;
        }

        if (refs.PainHost == null)
        {
            error = "missing CharacterPainHost module";
            return false;
        }

        if (refs.ImbalanceHost == null)
        {
            error = "missing CharacterImbalanceHost module";
            return false;
        }

        if (refs.HitReact == null)
        {
            error = "missing CharacterHitReact module";
            return false;
        }

        if (refs.BodyEffectTicker == null)
        {
            error = "missing BodyEffectTicker module";
            return false;
        }

        if (refs.FactionHost == null)
        {
            error = "missing CharacterFactionHost module";
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

    static void SaveNpcSamplePrefab(GameObject prefabRoot)
    {
        bool saved = false;
        try
        {
            saved = PrefabUtility.SaveAsPrefabAsset(prefabRoot, NpcSamplePrefabPath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                "[CharacterBodyPrefabOrganizeMenu] SaveAsPrefabAsset failed (non-MB leftover): "
                + ex.Message);
        }

        if (!saved)
        {
            Debug.LogWarning(
                "[CharacterBodyPrefabOrganizeMenu] Prefab save returned false; YAML-stripping converted inventory modules.");
        }
    }

    static readonly string[] ConvertedInventoryModuleGuids =
    {
        "c3f81d322b541c44bb9bc28bd6cc756f",
        "eb66a306ab71d494fa692517ca17868f",
        "40a213a98d9432148ae56f8665372397",
        "774d2807e2a86604394e7caa89abe288",
        "343725b234bf87446abdc32bad955f82",
        "173bf695b91ad7842975bff0459f7a99",
        "d48c0eca271ad70409cacb0d075bd78f",
        "a7c3e1f42b8d4e5096f2a1c8d3b7e4f0",
        "82d3f4a5b6c7489dab0c1d2e3f445566",
        "a7c3e1f94b2d4a6e9f0c8d5b3a1e7f42",
        "b31707533045dc6479ffd15cf6928ab9",
        "46d9a88e7fefefa48b455e4e609cf4f0",
        "7c4e2a91b0d84f6e9a3c5d8e1f2b4a60",
        "2d73ef868a1f0064fbc932cd62762a6f",
        "29bca86be602f6e4688d3478970c2e0f",
        "ebde6a9e2f207874691e335bc49fa6ac",
        "49bd65d988e0726488607cc42bee200f",
        "d818f78d88b48464cb03c278ab5ab789",
    };

    static readonly string[] ConvertedInventoryModuleTypeNames =
    {
        "DistScript::PlayerInventoryHost",
        "DistScript::PlayerGearHost",
        "DistScript::PlayerEncumbranceHost",
        "DistScript::InventoryTimedMoveHost",
        "DistScript::NearbyContainerDetector",
        "DistScript::CharacterBodyHost",
        "DistScript::CharacterSkillsHost",
        "DistScript::CharacterTraitsHost",
        "DistScript::CharacterFactionHost",
        "DistScript::CharacterFootprintHost",
        "DistScript::PlayerNeedsHost",
        "DistScript::CharacterMoodHost",
        "DistScript::CharacterClimateHost",
        "DistScript::CharacterAttacker",
        "DistScript::BodyEffectTicker",
        "DistScript::CharacterPainHost",
        "DistScript::CharacterImbalanceHost",
        "DistScript::CharacterHitReact",
    };

    static readonly string[] ConvertedStatsVitalityTypeNames =
    {
        "CharacterBodyHost",
        "CharacterSkillsHost",
        "CharacterTraitsHost",
        "CharacterFactionHost",
        "CharacterFootprintHost",
        "PlayerNeedsHost",
        "CharacterMoodHost",
        "CharacterClimateHost",
        "CharacterAttacker",
        "BodyEffectTicker",
        "CharacterPainHost",
        "CharacterImbalanceHost",
        "CharacterHitReact",
    };

    static void StripConvertedInventoryModuleYaml(string prefabPath)
    {
        string fullPath = ResolveAssetFullPath(prefabPath);
        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            return;

        string yaml = File.ReadAllText(fullPath);
        string stripped = StripConvertedModuleBlocks(yaml);
        if (stripped == yaml)
            return;

        File.WriteAllText(fullPath, stripped);
        AssetDatabase.ImportAsset(prefabPath);
        Debug.Log("[CharacterBodyPrefabOrganizeMenu] YAML-stripped leftover converted module MB slots.");
    }

    static string ResolveAssetFullPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return null;

        string dataPath = Application.dataPath;
        if (string.IsNullOrEmpty(dataPath))
            return assetPath;

        DirectoryInfo projectRoot = Directory.GetParent(dataPath);
        if (projectRoot == null)
            return assetPath;

        return Path.Combine(projectRoot.FullName, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    static string StripConvertedModuleBlocks(string yaml)
    {
        if (string.IsNullOrEmpty(yaml))
            return yaml;

        var fileIds = new HashSet<string>();
        var kept = new System.Text.StringBuilder(yaml.Length);
        int cursor = 0;
        while (cursor < yaml.Length)
        {
            int nextDoc = yaml.IndexOf("--- !u!", cursor, StringComparison.Ordinal);
            if (nextDoc < 0)
            {
                kept.Append(yaml, cursor, yaml.Length - cursor);
                break;
            }

            if (nextDoc > cursor)
                kept.Append(yaml, cursor, nextDoc - cursor);

            int after = yaml.IndexOf("--- !u!", nextDoc + 7, StringComparison.Ordinal);
            if (after < 0)
                after = yaml.Length;

            string part = yaml.Substring(nextDoc, after - nextDoc);
            if (part.StartsWith("--- !u!114", StringComparison.Ordinal) &&
                IsConvertedInventoryModuleBlock(part))
            {
                int amp = part.IndexOf('&');
                if (amp >= 0)
                {
                    int idStart = amp + 1;
                    int idLen = 0;
                    while (idStart + idLen < part.Length && char.IsDigit(part[idStart + idLen]))
                        idLen++;
                    if (idLen > 0)
                        fileIds.Add(part.Substring(idStart, idLen));
                }
            }
            else
            {
                kept.Append(part);
            }

            cursor = after;
        }

        string next = kept.ToString();
        foreach (string fileId in fileIds)
        {
            string needle = "  - component: {fileID: " + fileId + "}";
            next = next.Replace("\r\n" + needle, string.Empty);
            next = next.Replace("\n" + needle, string.Empty);
        }

        return next;
    }

    static bool IsConvertedInventoryModuleBlock(string block)
    {
        for (int i = 0; i < ConvertedInventoryModuleGuids.Length; i++)
        {
            if (block.IndexOf(ConvertedInventoryModuleGuids[i], StringComparison.Ordinal) >= 0)
                return true;
        }

        for (int i = 0; i < ConvertedInventoryModuleTypeNames.Length; i++)
        {
            if (block.IndexOf(ConvertedInventoryModuleTypeNames[i], StringComparison.Ordinal) >= 0)
                return true;
        }

        return false;
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
