// ============================================================
// CharacterHitStopSetupMenu — Dist/MCP 히트스톱 SO·프리팹 Ensure
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CharacterHitStopSetupMenu
{
    const string NpcSamplePath = "Assets/Dist/Visual/Prefabs/3D/NpcSample.prefab";
    const string SettingsPath = CombatHitStopSettings.DefaultAssetPath;

    [MenuItem(DistMcpMenus.CharacterEnsureHitStop)]
    public static void EnsureCombatHitStop()
    {
        CombatHitStopSettings settings = EnsureSettingsAsset();
        int prefabAdded = PatchNpcSamplePrefab(settings);
        int sceneAdded = PatchOpenSceneHosts(settings);
        if (!Application.isPlaying)
            EditorSceneManager.SaveOpenScenes();
        Debug.Log(
            $"[CharacterHitStopSetupMenu] Settings {SettingsPath}. " +
            $"Prefab patched={prefabAdded}, scene patched={sceneAdded}.",
            settings);
    }

    static CombatHitStopSettings EnsureSettingsAsset() =>
        DistScriptableObjectEnsure.LoadOrCreate<CombatHitStopSettings>(SettingsPath);

    static int PatchNpcSamplePrefab(CombatHitStopSettings settings)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(NpcSamplePath);
        if (root == null)
        {
            Debug.LogError($"[CharacterHitStopSetupMenu] Failed to load: {NpcSamplePath}");
            return 0;
        }

        try
        {
            int added = EnsureHitStopOn(root, settings) ? 1 : 0;
            PrefabUtility.SaveAsPrefabAsset(root, NpcSamplePath);
            return added;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static int PatchOpenSceneHosts(CombatHitStopSettings settings)
    {
        int added = 0;
        CharacterBodyRefs[] refsList = Object.FindObjectsByType<CharacterBodyRefs>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < refsList.Length; i++)
        {
            CharacterBodyRefs refs = refsList[i];
            if (refs == null)
                continue;
            if (EnsureHitStopOn(refs.gameObject, settings))
                added++;
        }

        return added;
    }

    static bool EnsureHitStopOn(GameObject go, CombatHitStopSettings settings)
    {
        CharacterBodyRefs refs = go.GetComponent<CharacterBodyRefs>();
        if (refs == null)
            refs = go.GetComponentInParent<CharacterBodyRefs>();
        if (refs == null)
            refs = go.GetComponentInChildren<CharacterBodyRefs>(true);
        if (refs == null)
            return false;

        CharacterBodyHost bodyHost = refs.BodyHost;
        if (bodyHost == null)
        {
            refs.Invalidate();
            refs.ResolveFromHierarchy();
            bodyHost = refs.BodyHost;
        }

        if (bodyHost == null)
            return false;

        // Strip legacy MB if present from prior versions.
        Component[] behaviours = go.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            Component component = behaviours[i];
            if (component == null)
                continue;
            if (component.GetType().Name == "CharacterHitStop")
                Undo.DestroyObjectImmediate(component);
        }

        SerializedObject so = new(refs);
        SerializedProperty settingsProp = so.FindProperty("_hitStopSettings");
        bool changed = false;
        if (settingsProp != null && settingsProp.objectReferenceValue != settings)
        {
            settingsProp.objectReferenceValue = settings;
            so.ApplyModifiedPropertiesWithoutUndo();
            changed = true;
        }

        bodyHost.ConfigureHitStopSettings(settings);
        EditorUtility.SetDirty(refs);
        return changed;
    }
}
#endif
