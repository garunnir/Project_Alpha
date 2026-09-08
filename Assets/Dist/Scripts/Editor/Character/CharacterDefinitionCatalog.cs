// ============================================================
// CharacterDefinitionCatalog — Dist CharacterDefinition 에셋 폴더 SSOT
// ============================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CharacterDefinitionCatalog
{
    public const string AssetFolder = "Assets/Dist/SOData/Gameplay/Character";

    public static void EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(AssetFolder))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/Dist/SOData/Gameplay"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Dist/SOData"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Dist"))
                    AssetDatabase.CreateFolder("Assets", "Dist");
                AssetDatabase.CreateFolder("Assets/Dist", "SOData");
            }

            AssetDatabase.CreateFolder("Assets/Dist/SOData", "Gameplay");
        }

        AssetDatabase.CreateFolder("Assets/Dist/SOData/Gameplay", "Character");
    }

    public static List<CharacterDefinition> LoadAll()
    {
        var list = new List<CharacterDefinition>();
        if (!AssetDatabase.IsValidFolder(AssetFolder))
            return list;

        string[] guids = AssetDatabase.FindAssets("t:CharacterDefinition", new[] { AssetFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            CharacterDefinition def = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
            if (def != null)
                list.Add(def);
        }

        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return list;
    }

    public static CharacterDefinition CreateNew(string baseName = "New") =>
        DataDefinitionsSoCatalogs.CreateCharacterDefinition(baseName);
}
#endif
