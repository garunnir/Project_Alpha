// ============================================================
// CharacterEmotePatchMenu — Dist/MCP 캐릭터 이모트 SO·프리팹·NpcSample Ensure
// ============================================================

#if UNITY_EDITOR
using System;
using Garunnir.Runtime.Gameplay.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CharacterEmotePatchMenu
{
    const string NpcSamplePath = "Assets/Dist/Visual/Prefabs/3D/NpcSample.prefab";
    const string SpriteFolder = "Assets/Dist/Visual/Sprites/UI/Character/Emote";
    const string PrefabFolder = "Assets/Dist/Visual/Prefabs/UIComponents/Character";

    static readonly (EmoteId Id, MoodIconId MoodIcon, bool ObserverOnly)[] MoodEntries =
    {
        (EmoteId.MoodDepressed, MoodIconId.Depressed, false),
        (EmoteId.MoodVerySad, MoodIconId.VerySad, false),
        (EmoteId.MoodSad, MoodIconId.Sad, false),
        (EmoteId.MoodSlightlySad, MoodIconId.SlightlySad, false),
        (EmoteId.MoodNeutral, MoodIconId.Neutral, false),
        (EmoteId.MoodSlightlyHappy, MoodIconId.SlightlyHappy, false),
        (EmoteId.MoodHappy, MoodIconId.Happy, false),
        (EmoteId.MoodVeryHappy, MoodIconId.VeryHappy, false),
        (EmoteId.AlertSuspicious, MoodIconId.Hunger, true),
        (EmoteId.AlertSpotted, MoodIconId.Hunger, true),
    };

    [MenuItem(DistMcpMenus.CharacterEnsureEmote)]
    public static void EnsureCharacterEmote()
    {
        CharacterEmoteCatalog catalog = EnsureEmoteAssets();
        GameObject prefab = EnsurePrefab();
        int prefabPatched = PatchNpcSamplePrefab(catalog, prefab);
        int scenePatched = PatchOpenSceneHosts(catalog, prefab);
        if (!Application.isPlaying)
            EditorSceneManager.SaveOpenScenes();
        Debug.Log(
            $"[CharacterEmotePatchMenu] Catalog {CharacterEmoteCatalog.DefaultAssetPath}. " +
            $"Prefab patched={prefabPatched}, scene hosts={scenePatched}.",
            catalog);
    }

    static CharacterEmoteCatalog EnsureEmoteAssets()
    {
        EnsureSoFolder();
        EnsureSpriteFolder();

        EnsureExclamationSprite(
            SpriteFolder + "/Emote_AlertSuspicious.png",
            new Color(1f, 0.86f, 0.15f, 1f));
        EnsureExclamationSprite(
            SpriteFolder + "/Emote_AlertSpotted.png",
            new Color(0.95f, 0.22f, 0.18f, 1f));

        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();

        PlayerStatusMoodIconCatalog moodCatalog =
            AssetDatabase.LoadAssetAtPath<PlayerStatusMoodIconCatalog>(
                PlayerStatusMoodIconCatalog.DefaultAssetPath);

        CharacterEmoteCatalog catalog =
            DistScriptableObjectEnsure.LoadOrCreate<CharacterEmoteCatalog>(
                CharacterEmoteCatalog.DefaultAssetPath);

        SerializedObject catalogSo = new(catalog);
        SerializedProperty entries = catalogSo.FindProperty("_entries");
        entries.arraySize = MoodEntries.Length;

        for (int i = 0; i < MoodEntries.Length; i++)
        {
            (EmoteId id, MoodIconId moodIcon, bool observerOnly) = MoodEntries[i];
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            SerializedProperty idProp = entry.FindPropertyRelative("Id");
            idProp.intValue = (int)id;
            entry.FindPropertyRelative("ObserverOnly").boolValue = observerOnly;

            Sprite sprite = ResolveSprite(id, moodIcon, moodCatalog);
            entry.FindPropertyRelative("Sprite").objectReferenceValue = sprite;
            entry.FindPropertyRelative("Tint").colorValue = ResolveTint(id);
        }

        catalogSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        return catalog;
    }

    static Sprite ResolveSprite(
        EmoteId id,
        MoodIconId moodIcon,
        PlayerStatusMoodIconCatalog moodCatalog)
    {
        if (id == EmoteId.AlertSuspicious)
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "/Emote_AlertSuspicious.png");
        if (id == EmoteId.AlertSpotted)
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "/Emote_AlertSpotted.png");

        if (moodCatalog != null &&
            moodIcon != MoodIconId.Hunger &&
            moodCatalog.TryGetFront(moodIcon, out Sprite moodSprite) &&
            moodSprite != null)
        {
            return moodSprite;
        }

        string path = SpriteFolder + "/Emote_" + id + ".png";
        EnsureCircleSprite(path, 48, Color.white, filled: false);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Color ResolveTint(EmoteId id) =>
        id switch
        {
            EmoteId.AlertSuspicious => new Color(1f, 0.86f, 0.15f, 1f),
            EmoteId.AlertSpotted => new Color(0.95f, 0.22f, 0.18f, 1f),
            _ => CharacterEmoteLayout.DefaultIconColor,
        };

    static GameObject EnsurePrefab()
    {
        EnsurePrefabFolder();
        GameObject existing =
            AssetDatabase.LoadAssetAtPath<GameObject>(CharacterEmoteLayout.PrefabPath);
        if (existing != null)
            return existing;

        GameObject root = new(CharacterEmoteLayout.RootName);
        RectTransform rootRt = root.AddComponent<RectTransform>();
        rootRt.sizeDelta = CharacterEmoteLayout.Size;
        root.transform.localPosition = new Vector3(0f, CharacterEmoteLayout.LocalY, 0f);
        root.transform.localScale = Vector3.one * CharacterEmoteLayout.WorldScale;

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 21;

        WorldBillboard billboard = root.AddComponent<WorldBillboard>();
        SerializedObject billboardSo = new(billboard);
        billboardSo.FindProperty("_billboardEnabled").boolValue = true;
        billboardSo.FindProperty("_timeChannel").enumValueIndex = (int)TimeScaleChannel.Realtime;
        billboardSo.ApplyModifiedPropertiesWithoutUndo();

        UICharacterEmote emote = root.AddComponent<UICharacterEmote>();

        GameObject iconGo = new(CharacterEmoteLayout.IconName);
        iconGo.transform.SetParent(root.transform, false);
        RectTransform iconRt = iconGo.AddComponent<RectTransform>();
        iconRt.anchorMin = Vector2.zero;
        iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = Vector2.zero;
        iconRt.offsetMax = Vector2.zero;
        iconGo.AddComponent<CanvasRenderer>();
        Image icon = iconGo.AddComponent<Image>();
        icon.raycastTarget = false;
        icon.color = CharacterEmoteLayout.DefaultIconColor;
        icon.preserveAspect = true;

        SerializedObject so = new(emote);
        so.FindProperty("_icon").objectReferenceValue = icon;
        so.FindProperty("_canvas").objectReferenceValue = canvas;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CharacterEmoteLayout.PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return prefab;
    }

    static int PatchNpcSamplePrefab(CharacterEmoteCatalog catalog, GameObject prefab)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(NpcSamplePath);
        if (root == null)
        {
            Debug.LogError($"[CharacterEmotePatchMenu] Failed to load: {NpcSamplePath}");
            return 0;
        }

        try
        {
            bool changed = EnsureEmoteOn(root, catalog, prefab, recordUndo: false);
            PrefabUtility.SaveAsPrefabAsset(root, NpcSamplePath);
            return changed ? 1 : 0;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static int PatchOpenSceneHosts(CharacterEmoteCatalog catalog, GameObject prefab)
    {
        int patched = 0;
        CharacterBodyHost[] hosts = UnityEngine.Object.FindObjectsByType<CharacterBodyHost>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < hosts.Length; i++)
        {
            CharacterBodyHost host = hosts[i];
            if (host == null)
                continue;
            if (EnsureEmoteOn(host.gameObject, catalog, prefab, recordUndo: true))
                patched++;
        }

        return patched;
    }

    static bool EnsureEmoteOn(
        GameObject go,
        CharacterEmoteCatalog catalog,
        GameObject prefab,
        bool recordUndo)
    {
        bool changed = false;

        CharacterEmoteHost emoteHost = go.GetComponent<CharacterEmoteHost>();
        if (emoteHost == null)
        {
            emoteHost = recordUndo
                ? Undo.AddComponent<CharacterEmoteHost>(go)
                : go.AddComponent<CharacterEmoteHost>();
            changed = true;
        }

        // Strip legacy glue MBs folded into CharacterEmoteHost.
        Component mood = go.GetComponent("CharacterMoodEmoteSource");
        if (mood != null)
        {
            if (recordUndo)
                Undo.DestroyObjectImmediate(mood);
            else
                UnityEngine.Object.DestroyImmediate(mood);
            changed = true;
        }

        Component combat = go.GetComponent("CharacterCombatEmoteBridge");
        if (combat != null)
        {
            if (recordUndo)
                Undo.DestroyObjectImmediate(combat);
            else
                UnityEngine.Object.DestroyImmediate(combat);
            changed = true;
        }

        SerializedObject hostSo = new(emoteHost);
        SerializedProperty catalogProp = hostSo.FindProperty("_catalog");
        if (catalogProp != null && catalogProp.objectReferenceValue != catalog)
        {
            catalogProp.objectReferenceValue = catalog;
            hostSo.ApplyModifiedPropertiesWithoutUndo();
            changed = true;
        }

        Transform existing = go.transform.Find(CharacterEmoteLayout.RootName);
        if (existing == null && prefab != null)
        {
            GameObject instance = recordUndo
                ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.transform)
                : (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.transform);
            instance.name = CharacterEmoteLayout.RootName;
            if (recordUndo)
                Undo.RegisterCreatedObjectUndo(instance, "Patch Character Emote Instance");
            changed = true;
        }

        EditorUtility.SetDirty(go);
        return changed;
    }

    static void EnsureSoFolder()
    {
        if (AssetDatabase.IsValidFolder("Assets/Dist/SOData/Gameplay/Character"))
            return;
        if (!AssetDatabase.IsValidFolder("Assets/Dist/SOData/Gameplay"))
            AssetDatabase.CreateFolder("Assets/Dist/SOData", "Gameplay");
        AssetDatabase.CreateFolder("Assets/Dist/SOData/Gameplay", "Character");
    }

    static void EnsureSpriteFolder()
    {
        if (AssetDatabase.IsValidFolder(SpriteFolder))
            return;
        if (!AssetDatabase.IsValidFolder("Assets/Dist/Visual/Sprites/UI/Character"))
            AssetDatabase.CreateFolder("Assets/Dist/Visual/Sprites/UI", "Character");
        AssetDatabase.CreateFolder("Assets/Dist/Visual/Sprites/UI/Character", "Emote");
    }

    static void EnsurePrefabFolder()
    {
        if (AssetDatabase.IsValidFolder(PrefabFolder))
            return;
        if (!AssetDatabase.IsValidFolder("Assets/Dist/Visual/Prefabs/UIComponents"))
            return;
        AssetDatabase.CreateFolder("Assets/Dist/Visual/Prefabs/UIComponents", "Character");
    }

    static void EnsureExclamationSprite(string assetPath, Color color)
    {
        if (System.IO.File.Exists(assetPath))
        {
            EnsureSpriteImportSettings(assetPath);
            if (AssetDatabase.LoadAssetAtPath<Sprite>(assetPath) != null)
                return;
        }

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        Color32 c = color;
        Color32 clear = new(0, 0, 0, 0);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, clear);
        }

        DrawExclamation(tex, size, c);
        tex.Apply();
        System.IO.File.WriteAllBytes(assetPath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        EnsureSpriteImportSettings(assetPath);
    }

    static void DrawExclamation(Texture2D tex, int size, Color32 color)
    {
        int centerX = size / 2;
        int stemLeft = centerX - 3;
        int stemRight = centerX + 2;
        int stemTop = size - 14;
        int stemBottom = 18;
        for (int y = stemBottom; y <= stemTop; y++)
        {
            for (int x = stemLeft; x <= stemRight; x++)
                tex.SetPixel(x, y, color);
        }

        for (int y = 8; y <= 14; y++)
        {
            for (int x = stemLeft; x <= stemRight; x++)
                tex.SetPixel(x, y, color);
        }
    }

    static void EnsureCircleSprite(string assetPath, int size, Color color, bool filled)
    {
        if (System.IO.File.Exists(assetPath))
        {
            EnsureSpriteImportSettings(assetPath);
            if (AssetDatabase.LoadAssetAtPath<Sprite>(assetPath) != null)
                return;
        }

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        float center = (size - 1) * 0.5f;
        float outer = size * 0.45f;
        float inner = size * 0.28f;
        float outerSq = outer * outer;
        float innerSq = inner * inner;
        Color32 c = color;
        Color32 clear = new(0, 0, 0, 0);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distSq = dx * dx + dy * dy;
                bool draw = filled ? distSq <= outerSq : distSq <= outerSq && distSq >= innerSq;
                tex.SetPixel(x, y, draw ? c : clear);
            }
        }

        tex.Apply();
        System.IO.File.WriteAllBytes(assetPath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        EnsureSpriteImportSettings(assetPath);
    }

    static void EnsureSpriteImportSettings(string assetPath)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }
}
#endif
