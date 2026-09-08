// ============================================================
// ArmAnimSlotCatalogBaker — Leaf마다 Catalog 폴백 행 Ensure (MCP)
// ============================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CombatLeafUtil.All(Leaf) / ArmImpactKind 기준으로 Catalog 행만 Ensure한다.
/// 슬롯 .anim 복제 없음 — thin(Hold/Aim/Attack)·Impact thin·Hurt만 유지.
/// </summary>
public static class ArmAnimSlotCatalogBaker
{
    const string SlotDir = "Assets/Dist/Visual/Anim/CharacterAnimator/Slots";
    const string CatalogPath = ArmAnimSlotCatalog.DefaultAssetPath;
    const string PresentationCatalogPath = WeaponPresentationCatalog.DefaultAssetPath;

    static readonly Regex KeepSlotPattern = new Regex(
        @"^(Hold|Aim|Attack)_(Left|Right|TwoHand)_Slot\.anim$|" +
        @"^Impact(Recoil|Blocked)_Slot\.anim$|" +
        @"^Hit(Flinch|Stagger|PainDown|Dead)_Slot\.anim$",
        RegexOptions.CultureInvariant);

    [MenuItem("Dist/MCP/Ensure Arm Anim Pipeline")]
    [MenuItem("Dist/MCP/Ensure Arm Anim Slot Catalog")]
    public static void Bake()
    {
        string slotDir = SlotDir;
        if (!AssetDatabase.IsValidFolder(slotDir))
        {
            Debug.LogError("[ArmAnimSlotCatalogBaker] Slots folder missing.");
            return;
        }

        PruneObsoleteSlotClips(slotDir);
        DeleteOrphanHandlessSlots(slotDir);
        EnsureCatalog();
        WirePresentationCatalog();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "[ArmAnimSlotCatalogBaker] Ensured Leaf fallback verbs=" +
            CombatLeafUtil.All.Length +
            " impacts=" +
            Enum.GetValues(typeof(ArmImpactKind)).Length);
    }

    static void PruneObsoleteSlotClips(string slotDir)
    {
        string absDir = Path.GetFullPath(slotDir);
        if (!Directory.Exists(absDir))
            return;

        int removed = 0;
        foreach (string path in Directory.GetFiles(absDir, "*_Slot.anim", SearchOption.TopDirectoryOnly))
        {
            string fileName = Path.GetFileName(path);
            if (KeepSlotPattern.IsMatch(fileName))
                continue;

            string assetPath = slotDir + "/" + fileName;
            if (AssetDatabase.DeleteAsset(assetPath))
                removed++;
        }

        if (removed > 0)
            Debug.Log("[ArmAnimSlotCatalogBaker] Pruned obsolete slot clips=" + removed);
    }

    static void DeleteOrphanHandlessSlots(string slotDir)
    {
        string[] orphans =
        {
            "AimSwing_Slot",
            "AimTrigger_Slot",
            "AimBashing_Slot",
            "AimGun_Slot",
            "AimCutting_Slot"
        };
        for (int i = 0; i < orphans.Length; i++)
        {
            string path = slotDir + "/" + orphans[i] + ".anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) == null)
                continue;
            AssetDatabase.DeleteAsset(path);
        }
    }

    static void EnsureCatalog()
    {
        ArmAnimSlotCatalog catalog =
            DistScriptableObjectEnsure.LoadOrCreate<ArmAnimSlotCatalog>(CatalogPath);

        catalog.SetHoldThin(LoadHandClips("Hold"));
        catalog.SetAimThin(LoadHandClips("Aim"));
        catalog.SetAttackThin(LoadHandClips("Attack"));
        catalog.SetImpactThin(
            catalog.ImpactRecoilThin != null
                ? catalog.ImpactRecoilThin
                : LoadFlatSlot("ImpactRecoil_Slot"),
            catalog.ImpactBlockedThin != null
                ? catalog.ImpactBlockedThin
                : LoadFlatSlot("ImpactBlocked_Slot"));

        CombatLeafVfx rangedVfxTemplate = FindRangedVfxTemplate(catalog);
        CombatLeafVfx meleeVfxTemplate = FindMeleeVfxTemplate(catalog);

        var verbs = new List<ArmAnimSlotCatalog.ActionLibraryEntry>();
        var seen = new HashSet<int>();
        CombatLeaf[] all = CombatLeafUtil.All;
        for (int i = 0; i < all.Length; i++)
            verbs.Add(BuildVerbEntry(catalog, all[i], seen, rangedVfxTemplate, meleeVfxTemplate));

        if (catalog.Verbs != null)
        {
            for (int i = 0; i < catalog.Verbs.Length; i++)
            {
                ArmAnimSlotCatalog.ActionLibraryEntry orphan = catalog.Verbs[i];
                if (orphan == null)
                    continue;
                CombatLeaf leaf = CombatLeafUtil.Normalize(orphan.leaf);
                if (leaf == CombatLeaf.Trigger)
                    leaf = CombatLeaf.Semi;
                if (seen.Contains((int)leaf))
                    continue;
                verbs.Add(BuildVerbEntry(catalog, leaf, seen, rangedVfxTemplate, meleeVfxTemplate));
            }
        }

        catalog.SetVerbs(verbs.ToArray());

        var impacts = new List<ArmAnimSlotCatalog.ImpactLibraryEntry>();
        var seenKinds = new HashSet<ArmImpactKind>();
        foreach (ArmImpactKind kind in Enum.GetValues(typeof(ArmImpactKind)))
            impacts.Add(BuildImpactEntry(catalog, kind, seenKinds));

        if (catalog.Impacts != null)
        {
            for (int i = 0; i < catalog.Impacts.Length; i++)
            {
                ArmAnimSlotCatalog.ImpactLibraryEntry orphan = catalog.Impacts[i];
                if (orphan == null || seenKinds.Contains(orphan.kind))
                    continue;
                impacts.Add(BuildImpactEntry(catalog, orphan.kind, seenKinds));
            }
        }

        catalog.SetImpacts(impacts.ToArray());
        EditorUtility.SetDirty(catalog);
    }

    static CombatLeafVfx FindRangedVfxTemplate(ArmAnimSlotCatalog catalog)
    {
        if (catalog.Verbs == null)
            return null;
        for (int i = 0; i < catalog.Verbs.Length; i++)
        {
            ArmAnimSlotCatalog.ActionLibraryEntry e = catalog.Verbs[i];
            if (e == null || e.vfx == null || !HasAnyVfx(e.vfx))
                continue;
            if (CombatLeafUtil.IsRanged(e.leaf) || e.leaf == CombatLeaf.Trigger)
                return CloneVfx(e.vfx);
        }

        return null;
    }

    static CombatLeafVfx FindMeleeVfxTemplate(ArmAnimSlotCatalog catalog)
    {
        if (catalog.Verbs == null)
            return null;
        for (int i = 0; i < catalog.Verbs.Length; i++)
        {
            ArmAnimSlotCatalog.ActionLibraryEntry e = catalog.Verbs[i];
            if (e == null || e.vfx == null || !HasAnyVfx(e.vfx))
                continue;
            CombatLeaf leaf = CombatLeafUtil.Normalize(e.leaf);
            if (leaf == CombatLeaf.Strike || leaf == CombatLeaf.Pierce ||
                leaf == CombatLeaf.Excavate || leaf == CombatLeaf.Chop)
                return CloneVfx(e.vfx);
        }

        return null;
    }

    static ArmAnimSlotCatalog.ActionLibraryEntry BuildVerbEntry(
        ArmAnimSlotCatalog catalog,
        CombatLeaf action,
        HashSet<int> seen,
        CombatLeafVfx rangedVfxTemplate,
        CombatLeafVfx meleeVfxTemplate)
    {
        CombatLeaf leaf = CombatLeafUtil.Normalize(action);
        seen.Add((int)leaf);

        ArmAnimSlotCatalog.ActionLibraryEntry existing = FindExact(catalog, leaf);
        CombatLeafVfx vfx = existing?.vfx != null && HasAnyVfx(existing.vfx)
            ? CloneVfx(existing.vfx)
            : new CombatLeafVfx();

        if (!HasAnyVfx(vfx) && CombatLeafUtil.IsRanged(leaf) && rangedVfxTemplate != null)
            vfx = CloneVfx(rangedVfxTemplate);
        if (!HasAnyVfx(vfx) &&
            (leaf == CombatLeaf.Raise ||
             leaf == CombatLeaf.Strike ||
             leaf == CombatLeaf.Pierce ||
             leaf == CombatLeaf.Excavate ||
             leaf == CombatLeaf.Chop) &&
            meleeVfxTemplate != null)
            vfx = CloneVfx(meleeVfxTemplate);

        return new ArmAnimSlotCatalog.ActionLibraryEntry
        {
            leaf = leaf,
            hold = new ArmAnimSlotCatalog.HandClips(),
            aim = new ArmAnimSlotCatalog.HandClips(),
            attack = new ArmAnimSlotCatalog.HandClips(),
            vfx = vfx
        };
    }

    static ArmAnimSlotCatalog.ActionLibraryEntry FindExact(
        ArmAnimSlotCatalog catalog,
        CombatLeaf leaf)
    {
        if (catalog.Verbs == null)
            return null;
        for (int i = 0; i < catalog.Verbs.Length; i++)
        {
            ArmAnimSlotCatalog.ActionLibraryEntry e = catalog.Verbs[i];
            if (e == null)
                continue;
            if (CombatLeafUtil.Normalize(e.leaf) == leaf)
                return e;
            if (leaf == CombatLeaf.Semi && e.leaf == CombatLeaf.Trigger)
                return e;
        }

        return null;
    }

    static ArmAnimSlotCatalog.ImpactLibraryEntry BuildImpactEntry(
        ArmAnimSlotCatalog catalog,
        ArmImpactKind kind,
        HashSet<ArmImpactKind> seen)
    {
        seen.Add(kind);
        ArmAnimSlotCatalog.ImpactLibraryEntry existing = catalog.FindImpact(kind);
        AnimationClip thin = existing != null && existing.thin != null
            ? existing.thin
            : LoadFlatSlot("Impact" + kind + "_Slot");
        return new ArmAnimSlotCatalog.ImpactLibraryEntry
        {
            kind = kind,
            clips = new ArmAnimSlotCatalog.HandClips(),
            thin = thin,
            vfx = existing?.vfx != null ? CloneVfx(existing.vfx) : new CombatLeafVfx()
        };
    }

    static CombatLeafVfx CloneVfx(CombatLeafVfx src)
    {
        if (src == null)
            return new CombatLeafVfx();
        return new CombatLeafVfx
        {
            actionVfx = src.actionVfx,
            tracerVfx = src.tracerVfx,
            hitVfx = src.hitVfx,
            missVfx = src.missVfx
        };
    }

    static bool HasAnyVfx(CombatLeafVfx vfx) =>
        vfx != null &&
        (vfx.actionVfx != null ||
         vfx.tracerVfx != null ||
         vfx.hitVfx != null ||
         vfx.missVfx != null);

    static void WirePresentationCatalog()
    {
        var presentation = AssetDatabase.LoadAssetAtPath<WeaponPresentationCatalog>(
            PresentationCatalogPath);
        var pipeline = AssetDatabase.LoadAssetAtPath<ArmAnimSlotCatalog>(CatalogPath);
        if (presentation == null || pipeline == null)
            return;
        presentation.SetAnimPipeline(pipeline);
        EditorUtility.SetDirty(presentation);
        if (presentation.Fallbacks != null)
            EditorUtility.SetDirty(presentation.Fallbacks);
    }

    static ArmAnimSlotCatalog.HandClips LoadHandClips(string stem) =>
        new ArmAnimSlotCatalog.HandClips
        {
            leftBase = LoadSlot(stem, "Left"),
            rightBase = LoadSlot(stem, "Right"),
            twoHandBase = LoadSlot(stem, "TwoHand")
        };

    static AnimationClip LoadSlot(string stem, string hand) =>
        AssetDatabase.LoadAssetAtPath<AnimationClip>(
            SlotDir + "/" + stem + "_" + hand + "_Slot.anim");

    static AnimationClip LoadFlatSlot(string fileName) =>
        AssetDatabase.LoadAssetAtPath<AnimationClip>(SlotDir + "/" + fileName + ".anim");
}
#endif
