// ============================================================
// DataDefinitionsSoCatalogs — Data Definitions SO 생성·삭제·폴더 SSOT
// ============================================================

#if UNITY_EDITOR
using IsoTilemap;
using UnityEditor;
using UnityEngine;

static class DataDefinitionsSoCatalogs
{
    public const string TileFolder = "Assets/Dist/SOData/Tile";
    public const string AttacksFolder = "Assets/Dist/SOData/Combat/Attacks";

    public static string RecommendTileBaseName() =>
        RecommendBaseName(stem => TileAssetPath(stem));

    public static string RecommendFactionBaseName() =>
        RecommendBaseName(stem => FactionAssetPath(stem));

    public static string RecommendCharacterDefinitionBaseName() =>
        RecommendBaseName(stem => CharacterDefinitionAssetPath(stem));

    public static string RecommendPresentationBaseName() =>
        RecommendBaseName(stem => PresentationAssetPath(stem));

    public static string RecommendAttackBaseName() =>
        RecommendBaseName(stem => AttackAssetPath(stem));

    public static TileDefinition CreateTileDefinition(string baseName = "New")
    {
        EnsureFolder(TileFolder);
        if (!TryResolveCreatePath(TileAssetPath(baseName), out string path))
            return null;

        var def = ScriptableObject.CreateInstance<TileDefinition>();
        AssetDatabase.CreateAsset(def, path);
        AssetDatabase.SaveAssets();
        return def;
    }

    public static CharacterDefinition CreateCharacterDefinition(string baseName = "New")
    {
        CharacterDefinitionCatalog.EnsureFolder();
        if (!TryResolveCreatePath(CharacterDefinitionAssetPath(baseName), out string path))
            return null;

        var def = ScriptableObject.CreateInstance<CharacterDefinition>();
        AssetDatabase.CreateAsset(def, path);
        AssetDatabase.SaveAssets();
        return def;
    }

    public static CharacterFaction CreateFaction(string baseName = "New")
    {
        CharacterDefinitionCatalog.EnsureFolder();
        if (!TryResolveCreatePath(FactionAssetPath(baseName), out string path))
            return null;

        var faction = ScriptableObject.CreateInstance<CharacterFaction>();
        AssetDatabase.CreateAsset(faction, path);
        AssetDatabase.SaveAssets();
        return faction;
    }

    public static WeaponPresentation CreatePresentation(string baseName = "New")
    {
        string folder = GameDataWeaponPresentationEditor.PresentationsFolder;
        DistScriptableObjectEnsure.EnsureParentFoldersForAsset(folder + "/_.asset");
        if (!TryResolveCreatePath(PresentationAssetPath(baseName), out string path))
            return null;

        var presentation = ScriptableObject.CreateInstance<WeaponPresentation>();
        AssetDatabase.CreateAsset(presentation, path);
        AssetDatabase.SaveAssets();
        return presentation;
    }

    public static WeaponAttack CreateAttack(string baseName = "New")
    {
        DistScriptableObjectEnsure.EnsureParentFoldersForAsset(AttacksFolder + "/_.asset");
        if (!TryResolveCreatePath(AttackAssetPath(baseName), out string path))
            return null;

        var attack = ScriptableObject.CreateInstance<WeaponAttack>();
        AssetDatabase.CreateAsset(attack, path);
        AssetDatabase.SaveAssets();
        return attack;
    }

    public static bool CanDelete(UnityEngine.Object asset)
    {
        if (asset == null)
            return false;

        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path))
            return false;

        switch (asset)
        {
            case TileDefinition _:
                return IsUnderFolder(path, TileFolder);
            case CharacterDefinition _:
            case CharacterFaction _:
                return IsUnderFolder(path, CharacterDefinitionCatalog.AssetFolder);
            case WeaponPresentation _:
                return IsUnderFolder(path, GameDataWeaponPresentationEditor.PresentationsFolder);
            case WeaponAttack _:
                return IsUnderFolder(path, AttacksFolder);
            default:
                return false;
        }
    }

    public static bool TryDelete(UnityEngine.Object asset)
    {
        if (!CanDelete(asset))
            return false;

        string path = AssetDatabase.GetAssetPath(asset);
        if (!EditorUtility.DisplayDialog(
                "Delete ScriptableObject",
                $"에셋을 삭제합니다.\n\n{path}\n\n되돌릴 수 없습니다.",
                "Delete",
                "Cancel"))
            return false;

        if (!AssetDatabase.DeleteAsset(path))
        {
            EditorUtility.DisplayDialog(
                "Delete failed",
                $"에셋을 삭제하지 못했습니다.\n\n{path}",
                "OK");
            return false;
        }

        AssetDatabase.SaveAssets();
        FinishDelete();
        return true;
    }

    public static void FinishCreate(UnityEngine.Object asset)
    {
        if (asset == null)
            return;

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        if (EditorWindow.HasOpenInstances<DataDefinitionsWindow>())
            EditorWindow.GetWindow<DataDefinitionsWindow>().ForceMenuTreeRebuild();
    }

    public static void FinishDelete()
    {
        Selection.activeObject = null;
        if (EditorWindow.HasOpenInstances<DataDefinitionsWindow>())
            EditorWindow.GetWindow<DataDefinitionsWindow>().ForceMenuTreeRebuild();
    }

    static string RecommendBaseName(System.Func<string, string> pathForStem)
    {
        for (int i = 0; i < 1000; i++)
        {
            string stem = i == 0 ? "New" : $"New{i}";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(pathForStem(stem)) == null)
                return stem;
        }

        return "New";
    }

    static bool TryResolveCreatePath(string path, out string resolvedPath)
    {
        resolvedPath = path;
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
        {
            EditorUtility.DisplayDialog(
                "Already exists",
                $"같은 이름의 에셋이 이미 있습니다.\n\n{path}\n\nBase name을 바꾸거나 기존 에셋을 삭제하세요.",
                "OK");
            return false;
        }

        return true;
    }

    static string TileAssetPath(string baseName) =>
        $"{TileFolder}/TileDefinition.{SanitizeFileName(baseName)}.asset";

    static string CharacterDefinitionAssetPath(string baseName) =>
        $"{CharacterDefinitionCatalog.AssetFolder}/CharacterDefinition.{SanitizeFileName(baseName)}.asset";

    static string FactionAssetPath(string baseName) =>
        $"{CharacterDefinitionCatalog.AssetFolder}/CharacterFaction.{SanitizeFileName(baseName)}.asset";

    static string PresentationAssetPath(string baseName)
    {
        string folder = GameDataWeaponPresentationEditor.PresentationsFolder;
        return $"{folder}/Weapon_{SanitizeFileName(baseName)}.asset";
    }

    static string AttackAssetPath(string baseName) =>
        $"{AttacksFolder}/Attack_{SanitizeFileName(baseName)}.asset";

    static bool IsUnderFolder(string assetPath, string folder)
    {
        if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(folder))
            return false;
        return assetPath.StartsWith(folder + "/", System.StringComparison.Ordinal)
               || string.Equals(assetPath, folder, System.StringComparison.Ordinal);
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        DistScriptableObjectEnsure.EnsureParentFoldersForAsset(folder + "/_.asset");
    }

    static string SanitizeFileName(string id)
    {
        if (string.IsNullOrEmpty(id))
            return "New";
        return id.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
    }
}
#endif
