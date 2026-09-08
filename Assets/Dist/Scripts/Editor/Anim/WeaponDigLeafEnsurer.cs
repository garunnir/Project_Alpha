// ============================================================
// WeaponDigLeafEnsurer — DIG quality Presentation에 Dig Leaf 행 Ensure (MCP)
// ============================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;
using UnityEditor;
using UnityEngine;

public static class WeaponDigLeafEnsurer
{
    const string CatalogPath = WeaponPresentationCatalog.DefaultAssetPath;

    [MenuItem("Dist/MCP/Ensure Dig Leaf Entries")]
    public static void EnsureAll()
    {
        GameDataLoader.Load();

        var catalog = DistScriptableObjectEnsure.LoadOrCreate<WeaponPresentationCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogError("[WeaponDigLeafEnsurer] Catalog missing.");
            return;
        }

        int touched = 0;
        HashSet<WeaponPresentation> seen = new HashSet<WeaponPresentation>();

        WeaponPresentationCatalog.Binding[] byItem = catalog.ByItemId;
        if (byItem != null)
        {
            for (int i = 0; i < byItem.Length; i++)
            {
                WeaponPresentationCatalog.Binding b = byItem[i];
                if (b == null || b.presentation == null || !seen.Add(b.presentation))
                    continue;
                ItemData item = string.IsNullOrEmpty(b.id) ? null : GameplayData.GetItem(b.id);
                if (EnsureForPresentation(b.presentation, item))
                    touched++;
            }
        }

        WeaponPresentationCatalog.Binding[] byCat = catalog.ByCategoryId;
        if (byCat != null)
        {
            for (int i = 0; i < byCat.Length; i++)
            {
                WeaponPresentationCatalog.Binding b = byCat[i];
                if (b == null || b.presentation == null || !seen.Add(b.presentation))
                    continue;
                if (EnsureForPresentation(b.presentation, InferDigItem(b.presentation, catalog)))
                    touched++;
            }
        }

        // DIG 도구가 카탈로그 바인딩에 없어도 Resolve 폴백 Presentation에 Dig Leaf Ensure.
        touched += EnsureResolvedDigTools(catalog, seen);

        AssetDatabase.SaveAssets();
        Debug.Log("[WeaponDigLeafEnsurer] Updated presentations=" + touched);
    }

    [MenuItem("Dist/MCP/Ensure Chop Leaf Entries")]
    public static void EnsureChopAll()
    {
        GameDataLoader.Load();

        var catalog = DistScriptableObjectEnsure.LoadOrCreate<WeaponPresentationCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogError("[WeaponDigLeafEnsurer] Catalog missing (Chop).");
            return;
        }

        int touched = 0;
        HashSet<WeaponPresentation> seen = new HashSet<WeaponPresentation>();

        WeaponPresentationCatalog.Binding[] byItem = catalog.ByItemId;
        if (byItem != null)
        {
            for (int i = 0; i < byItem.Length; i++)
            {
                WeaponPresentationCatalog.Binding b = byItem[i];
                if (b == null || b.presentation == null || !seen.Add(b.presentation))
                    continue;
                ItemData item = string.IsNullOrEmpty(b.id) ? null : GameplayData.GetItem(b.id);
                if (EnsureChopForPresentation(b.presentation, item))
                    touched++;
            }
        }

        touched += EnsureResolvedAxeTools(catalog, seen);

        AssetDatabase.SaveAssets();
        Debug.Log("[WeaponDigLeafEnsurer] Chop Updated presentations=" + touched);
    }

    static int EnsureResolvedAxeTools(
        WeaponPresentationCatalog catalog,
        HashSet<WeaponPresentation> seen)
    {
        if (catalog == null)
            return 0;

        int touched = 0;
        GameDatabase db = GameplayData.RefData;
        if (db?.Items == null)
            return 0;

        for (int i = 0; i < db.Items.Count; i++)
        {
            ItemData item = db.Items[i];
            if (item == null || !MapPlantService.HasAxeQuality(item))
                continue;

            WeaponPresentation presentation = catalog.Resolve(item.id, item);
            if (presentation == null || !seen.Add(presentation))
                continue;
            if (EnsureChopForPresentation(presentation, item))
                touched++;
        }

        return touched;
    }

    public static bool EnsureChopForPresentation(WeaponPresentation presentation, ItemData item)
    {
        if (presentation == null)
            return false;

        bool axeTool = MapPlantService.HasAxeQuality(item);
        if (!axeTool && !PresentationHasChopLeaf(presentation))
            return false;

        WeaponAttack attackTemplate = FindMeleeAttackTemplate(presentation);
        float cooldownSeconds = FindMeleeCooldownSeconds(presentation);

        var entries = new List<WeaponPresentation.Entry>();
        if (presentation.Entries != null)
        {
            for (int i = 0; i < presentation.Entries.Length; i++)
            {
                if (presentation.Entries[i] != null)
                    entries.Add(presentation.Entries[i]);
            }
        }

        WeaponAttack chopAttack = EnsureChopAttackAsset(presentation, attackTemplate);
        if (!EnsureLeaf(entries, CombatLeaf.Chop, chopAttack, cooldownSeconds))
            return false;

        Undo.RecordObject(presentation, "Ensure Chop leaf entry");
        presentation.SetEntries(entries.ToArray());
        EditorUtility.SetDirty(presentation);
        return true;
    }

    static bool PresentationHasChopLeaf(WeaponPresentation presentation)
    {
        if (presentation?.Entries == null)
            return false;
        for (int i = 0; i < presentation.Entries.Length; i++)
        {
            WeaponPresentation.Entry e = presentation.Entries[i];
            if (e != null && CombatLeafUtil.Normalize(e.leaf) == CombatLeaf.Chop)
                return true;
        }

        return false;
    }

    static WeaponAttack EnsureChopAttackAsset(
        WeaponPresentation presentation,
        WeaponAttack template)
    {
        string dir = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(presentation));
        if (string.IsNullOrEmpty(dir))
            dir = "Assets/Dist/SOData/Combat";

        string path = dir.Replace('\\', '/') + "/" + presentation.name + "_ChopAttack.asset";
        WeaponAttack existing = AssetDatabase.LoadAssetAtPath<WeaponAttack>(path);
        if (existing != null)
        {
            EnsureChopLogicId(existing);
            return existing;
        }

        WeaponAttack created = ScriptableObject.CreateInstance<WeaponAttack>();
        if (template != null)
            EditorUtility.CopySerialized(template, created);
        EnsureChopLogicId(created);
        AssetDatabase.CreateAsset(created, path);
        EditorUtility.SetDirty(created);
        return created;
    }

    static void EnsureChopLogicId(WeaponAttack attack)
    {
        if (attack == null)
            return;
        SerializedObject so = new SerializedObject(attack);
        SerializedProperty logic = so.FindProperty("_logicId");
        if (logic != null && logic.stringValue != ActionHandlerIds.MeleePlantTarget)
        {
            logic.stringValue = ActionHandlerIds.MeleePlantTarget;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(attack);
        }
    }

    static int EnsureResolvedDigTools(
        WeaponPresentationCatalog catalog,
        HashSet<WeaponPresentation> seen)
    {
        if (catalog == null)
            return 0;

        int touched = 0;
        GameDatabase db = GameplayData.RefData;
        if (db?.Items == null)
            return 0;

        for (int i = 0; i < db.Items.Count; i++)
        {
            ItemData item = db.Items[i];
            if (item == null || !MapPlantService.HasDigQuality(item))
                continue;

            WeaponPresentation presentation = catalog.Resolve(item.id, item);
            if (presentation == null || !seen.Add(presentation))
                continue;
            if (EnsureForPresentation(presentation, item))
                touched++;
        }

        return touched;
    }

    static ItemData InferDigItem(
        WeaponPresentation presentation,
        WeaponPresentationCatalog catalog)
    {
        WeaponPresentationCatalog.Binding[] byItem = catalog.ByItemId;
        if (byItem == null)
            return null;
        for (int i = 0; i < byItem.Length; i++)
        {
            if (byItem[i]?.presentation != presentation)
                continue;
            return GameplayData.GetItem(byItem[i].id);
        }

        return null;
    }

    /// <summary>
    /// DIG 품질 아이템이거나 Dig 행이 있으면 Dig Leaf Ensure. Attack은 melee 템플릿 복제 + melee_block_target.
    /// </summary>
    public static bool EnsureForPresentation(WeaponPresentation presentation, ItemData item)
    {
        if (presentation == null)
            return false;

        bool digTool = MapPlantService.HasDigQuality(item);
        if (!digTool && !PresentationHasDigLeaf(presentation))
            return false;

        WeaponAttack attackTemplate = FindMeleeAttackTemplate(presentation);
        float cooldownSeconds = FindMeleeCooldownSeconds(presentation);

        var entries = new List<WeaponPresentation.Entry>();
        if (presentation.Entries != null)
        {
            for (int i = 0; i < presentation.Entries.Length; i++)
            {
                if (presentation.Entries[i] != null)
                    entries.Add(presentation.Entries[i]);
            }
        }

        WeaponAttack digAttack = EnsureDigAttackAsset(presentation, attackTemplate);
        if (!EnsureLeaf(entries, CombatLeaf.Excavate, digAttack, cooldownSeconds))
            return false;

        Undo.RecordObject(presentation, "Ensure Dig leaf entry");
        presentation.SetEntries(entries.ToArray());
        EditorUtility.SetDirty(presentation);
        return true;
    }

    static float FindMeleeCooldownSeconds(WeaponPresentation presentation)
    {
        if (presentation?.Entries == null)
            return 0f;
        for (int i = 0; i < presentation.Entries.Length; i++)
        {
            WeaponPresentation.Entry e = presentation.Entries[i];
            if (e == null)
                continue;
            CombatLeaf leaf = CombatLeafUtil.Normalize(e.leaf);
            if (leaf == CombatLeaf.Strike || leaf == CombatLeaf.Pierce)
                return e.actionCooldownSeconds;
        }

        return 0f;
    }

    static bool PresentationHasDigLeaf(WeaponPresentation presentation)
    {
        if (presentation?.Entries == null)
            return false;
        for (int i = 0; i < presentation.Entries.Length; i++)
        {
            WeaponPresentation.Entry e = presentation.Entries[i];
            if (e != null && CombatLeafUtil.Normalize(e.leaf) == CombatLeaf.Excavate)
                return true;
        }

        return false;
    }

    static WeaponAttack FindMeleeAttackTemplate(WeaponPresentation presentation)
    {
        if (presentation?.Entries == null)
            return null;
        for (int i = 0; i < presentation.Entries.Length; i++)
        {
            WeaponPresentation.Entry e = presentation.Entries[i];
            if (e == null || e.attack == null)
                continue;
            CombatLeaf leaf = CombatLeafUtil.Normalize(e.leaf);
            if (leaf == CombatLeaf.Strike || leaf == CombatLeaf.Pierce)
                return e.attack;
        }

        for (int i = 0; i < presentation.Entries.Length; i++)
        {
            if (presentation.Entries[i]?.attack != null)
                return presentation.Entries[i].attack;
        }

        return null;
    }

    static WeaponAttack EnsureDigAttackAsset(
        WeaponPresentation presentation,
        WeaponAttack template)
    {
        string dir = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(presentation));
        if (string.IsNullOrEmpty(dir))
            dir = "Assets/Dist/SOData/Combat";

        string path = dir.Replace('\\', '/') + "/" + presentation.name + "_DigAttack.asset";
        WeaponAttack existing = AssetDatabase.LoadAssetAtPath<WeaponAttack>(path);
        if (existing != null)
        {
            EnsureDigLogicId(existing);
            return existing;
        }

        WeaponAttack created = ScriptableObject.CreateInstance<WeaponAttack>();
        if (template != null)
            EditorUtility.CopySerialized(template, created);
        EnsureDigLogicId(created);
        AssetDatabase.CreateAsset(created, path);
        EditorUtility.SetDirty(created);
        return created;
    }

    static void EnsureDigLogicId(WeaponAttack attack)
    {
        if (attack == null)
            return;
        SerializedObject so = new SerializedObject(attack);
        SerializedProperty logic = so.FindProperty("_logicId");
        if (logic != null
            && logic.stringValue != ActionHandlerIds.MeleeBlockTarget
            && logic.stringValue != ActionHandlerIds.MapDigBreak)
        {
            logic.stringValue = ActionHandlerIds.MeleeBlockTarget;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(attack);
        }
    }

    static bool EnsureLeaf(
        List<WeaponPresentation.Entry> entries,
        CombatLeaf leaf,
        WeaponAttack attack,
        float actionCooldownSeconds)
    {
        CombatLeaf want = CombatLeafUtil.Normalize(leaf);
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null &&
                CombatLeafUtil.Normalize(entries[i].leaf) == want)
                return false;
        }

        entries.Add(new WeaponPresentation.Entry
        {
            leaf = want,
            attack = attack,
            actionCooldownSeconds = actionCooldownSeconds,
            effectSeeds = System.Array.Empty<WeaponPresentation.EffectSeed>(),
            vfx = new CombatLeafVfx()
        });
        return true;
    }
}
#endif
