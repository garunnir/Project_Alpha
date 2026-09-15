// ============================================================
// TimeScaleHudSetupMenu — Dist/MCP 배속 HUD 프리팹·씬 Setup
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

static class TimeScaleHudSetupMenu
{
    const string PrefabFolder = "Assets/Dist/Visual/Prefabs/UIComponents/Time";
    const string HudPrefabPath = PrefabFolder + "/Hud_TimeScale.prefab";

    [MenuItem(DistMcpMenus.TimeScaleCreateHudPrefabIfMissing)]
    static void CreateHudPrefabIfMissing()
    {
        EnsureFolder();
        UITimeScaleHudPanel existing =
            AssetDatabase.LoadAssetAtPath<UITimeScaleHudPanel>(HudPrefabPath);
        if (existing != null)
        {
            Debug.Log($"[TimeScaleHudSetupMenu] Prefab already exists: {HudPrefabPath}", existing);
            Selection.activeObject = existing;
            return;
        }

        UITimeScaleHudPanel panel = TimeScaleUIFactory.CreateHudRoot();
        GameObject root = panel.gameObject;
        UIWindowChromeBarPrefabPatch.Apply(
            root,
            createHeaderIfMissing: false,
            addFoldedTitle: false,
            foldedTitleText: null);
        PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[TimeScaleHudSetupMenu] Created prefab: {HudPrefabPath}");
        Selection.activeObject =
            AssetDatabase.LoadAssetAtPath<UITimeScaleHudPanel>(HudPrefabPath);
    }

    [MenuItem(DistMcpMenus.TimeScaleSetupHudInOpenScene)]
    static void SetupHudInOpenScene()
    {
        Canvas canvas = ResolveUiCanvas();
        if (canvas == null)
        {
            Debug.LogError("[TimeScaleHudSetupMenu] Canvas not found.");
            return;
        }

        UICanvasLayerHost layerHost = canvas.GetComponent<UICanvasLayerHost>();
        if (layerHost == null)
            layerHost = Undo.AddComponent<UICanvasLayerHost>(canvas.gameObject);
        layerHost.EditorSetupLayerHierarchy();

        Transform systemRoot = SystemHierarchySetup.ResolveSystemRoot();
        if (systemRoot == null)
        {
            Debug.LogError("[TimeScaleHudSetupMenu] System root not found.");
            return;
        }

        Transform timeRoot = SystemHierarchySetup.EnsureCategory(
            systemRoot,
            SystemHierarchySetup.Time);

        GameplayTimeScale timeScale = Object.FindAnyObjectByType<GameplayTimeScale>();
        if (timeScale == null)
            timeScale = EnsureComponentOnChild<GameplayTimeScale>(timeRoot, "GameplayTimeScale");

        if (timeScale.GetComponent<PossessedActionReservedWorkSource>() == null)
            Undo.AddComponent<PossessedActionReservedWorkSource>(timeScale.gameObject);

        EnsureReservedWorkSources();

        UITimeScaleHudPanel prefab =
            AssetDatabase.LoadAssetAtPath<UITimeScaleHudPanel>(HudPrefabPath);
        if (prefab == null)
        {
            Debug.LogError(
                $"[TimeScaleHudSetupMenu] Prefab missing: {HudPrefabPath}. " +
                "Run " + DistMcpMenus.TimeScaleCreateHudPrefabIfMissing + " first.");
            return;
        }

        UITimeScaleHudController controller =
            Object.FindAnyObjectByType<UITimeScaleHudController>();
        if (controller == null)
        {
            GameObject go = new("TimeScaleHudController");
            Undo.RegisterCreatedObjectUndo(go, "Create TimeScaleHudController");
            go.transform.SetParent(timeRoot, false);
            controller = Undo.AddComponent<UITimeScaleHudController>(go);
        }
        else
        {
            SystemHierarchySetup.EnsureChildUnder(
                timeRoot,
                controller.transform,
                "Move TimeScaleHudController Under System/Time");
        }

        UITimeScaleHudPanel panel = EnsureHudInstance(layerHost, prefab, "Hud_TimeScale");
        if (panel == null)
            return;

        SerializedObject so = new(controller);
        so.FindProperty("_panel").objectReferenceValue = panel;
        so.FindProperty("_uiCanvas").objectReferenceValue = canvas;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[TimeScaleHudSetupMenu] GameplayTimeScale + HUD wired.", panel);
        _ = timeScale;
    }

    static void EnsureReservedWorkSources()
    {
        CharacterBodyRefs refs = Object.FindAnyObjectByType<CharacterBodyRefs>();
        if (refs != null)
        {
            if (refs.GetComponent<GearReservedWorkSource>() == null)
                Undo.AddComponent<GearReservedWorkSource>(refs.gameObject);
            if (refs.GetComponent<InventoryReservedWorkSource>() == null)
                Undo.AddComponent<InventoryReservedWorkSource>(refs.gameObject);
        }

        UICraftingController crafting = Object.FindAnyObjectByType<UICraftingController>();
        if (crafting != null && crafting.GetComponent<CraftingReservedWorkSource>() == null)
            Undo.AddComponent<CraftingReservedWorkSource>(crafting.gameObject);
    }

    static UITimeScaleHudPanel EnsureHudInstance(
        UICanvasLayerHost layerHost,
        UITimeScaleHudPanel prefab,
        string instanceName)
    {
        Transform hud = layerHost.GetLayerRoot(UICanvasLayer.HUD);
        Transform existing = hud.Find(instanceName);
        if (existing != null)
        {
            UITimeScaleHudPanel panel = existing.GetComponent<UITimeScaleHudPanel>();
            if (panel != null)
                return panel;

            Debug.LogError(
                $"[TimeScaleHudSetupMenu] '{instanceName}' lacks UITimeScaleHudPanel.",
                existing);
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, hud);
        if (instance == null)
            return null;

        Undo.RegisterCreatedObjectUndo(instance, "Instantiate TimeScale HUD");
        instance.name = instanceName;
        return instance.GetComponent<UITimeScaleHudPanel>();
    }

    static T EnsureComponentOnChild<T>(Transform parent, string childName) where T : Component
    {
        Transform child = parent.Find(childName);
        GameObject go;
        if (child != null)
        {
            go = child.gameObject;
        }
        else
        {
            go = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(go, $"Create {childName}");
            go.transform.SetParent(parent, false);
        }

        T component = go.GetComponent<T>();
        if (component == null)
            component = Undo.AddComponent<T>(go);
        return component;
    }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Dist/Visual/Prefabs/UIComponents"))
                AssetDatabase.CreateFolder("Assets/Dist/Visual/Prefabs", "UIComponents");
            AssetDatabase.CreateFolder("Assets/Dist/Visual/Prefabs/UIComponents", "Time");
        }
    }

    static Canvas ResolveUiCanvas()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas c = canvases[i];
            if (c != null && c.GetComponent<UICanvasLayerHost>() != null)
                return c;
        }

        return Object.FindAnyObjectByType<Canvas>();
    }
}
#endif
