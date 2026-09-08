// ============================================================
// WeaponRangedLeafEnsurer — gun Presentation에 Semi/Burst/Auto Leaf 행 Ensure (MCP)
// ============================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using UnityEditor;
using UnityEngine;

public static class WeaponRangedLeafEnsurer
{
    const string CatalogPath = WeaponPresentationCatalog.DefaultAssetPath;

    [MenuItem("Dist/MCP/Ensure Ranged Leaf Entries")]
    public static void EnsureAll()
    {
        var catalog = DistScriptableObjectEnsure.LoadOrCreate<WeaponPresentationCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogError("[WeaponRangedLeafEnsurer] Catalog missing.");
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
                if (EnsureForPresentation(b.presentation, InferGunFromPresentation(b.presentation, catalog)))
                    touched++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[WeaponRangedLeafEnsurer] Updated presentations=" + touched);
    }

    static ItemData InferGunFromPresentation(
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
    /// ranged 행이 있거나 gun이면 Semi+Burst+Auto Ensure. Attack은 기존 ranged 행 복제.
    /// </summary>
    public static bool EnsureForPresentation(WeaponPresentation presentation, ItemData item)
    {
        if (presentation == null)
            return false;

        bool isGun = item?.gun != null;
        if (!isGun && !PresentationHasRangedLeaf(presentation))
            return false;

        WeaponAttack attackTemplate = FindRangedAttackTemplate(presentation);
        var entries = new List<WeaponPresentation.Entry>();
        if (presentation.Entries != null)
        {
            for (int i = 0; i < presentation.Entries.Length; i++)
            {
                if (presentation.Entries[i] != null)
                    entries.Add(presentation.Entries[i]);
            }
        }

        bool changed = false;
        changed |= EnsureLeaf(entries, CombatLeaf.Semi, attackTemplate);
        changed |= EnsureLeaf(entries, CombatLeaf.Burst, attackTemplate);
        changed |= EnsureLeaf(entries, CombatLeaf.Auto, attackTemplate);

        if (!changed)
            return false;

        Undo.RecordObject(presentation, "Ensure ranged leaf entries");
        presentation.SetEntries(entries.ToArray());
        EditorUtility.SetDirty(presentation);
        return true;
    }

    static bool PresentationHasRangedLeaf(WeaponPresentation presentation)
    {
        if (presentation?.Entries == null)
            return false;
        for (int i = 0; i < presentation.Entries.Length; i++)
        {
            WeaponPresentation.Entry e = presentation.Entries[i];
            if (e != null && CombatLeafUtil.IsRanged(e.leaf))
                return true;
        }

        return false;
    }

    static WeaponAttack FindRangedAttackTemplate(WeaponPresentation presentation)
    {
        if (presentation?.Entries == null)
            return null;
        for (int i = 0; i < presentation.Entries.Length; i++)
        {
            WeaponPresentation.Entry e = presentation.Entries[i];
            if (e != null && CombatLeafUtil.IsRanged(e.leaf) && e.attack != null)
                return e.attack;
        }

        for (int i = 0; i < presentation.Entries.Length; i++)
        {
            if (presentation.Entries[i]?.attack != null)
                return presentation.Entries[i].attack;
        }

        return null;
    }

    static bool EnsureLeaf(
        List<WeaponPresentation.Entry> entries,
        CombatLeaf leaf,
        WeaponAttack attack)
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
            effectSeeds = System.Array.Empty<WeaponPresentation.EffectSeed>(),
            vfx = new CombatLeafVfx()
        });
        return true;
    }
}
#endif
