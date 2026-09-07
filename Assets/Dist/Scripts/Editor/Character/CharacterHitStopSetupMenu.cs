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
        CharacterBodyHost[] hosts = Object.FindObjectsByType<CharacterBodyHost>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < hosts.Length; i++)
        {
            CharacterBodyHost host = hosts[i];
            if (host == null)
                continue;
            if (EnsureHitStopOn(host.gameObject, settings))
                added++;
        }

        return added;
    }

    static bool EnsureHitStopOn(GameObject go, CombatHitStopSettings settings)
    {
        CharacterBodyHost bodyHost = go.GetComponentInChildren<CharacterBodyHost>(true);
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

        SerializedObject so = new(bodyHost);
        SerializedProperty settingsProp = so.FindProperty("_hitStopSettings");
        bool changed = false;
        if (settingsProp != null && settingsProp.objectReferenceValue != settings)
        {
            settingsProp.objectReferenceValue = settings;
            so.ApplyModifiedPropertiesWithoutUndo();
            changed = true;
        }

        bodyHost.ConfigureHitStopSettings(settings);
        EditorUtility.SetDirty(bodyHost);
        return changed;
    }
}
#endif
