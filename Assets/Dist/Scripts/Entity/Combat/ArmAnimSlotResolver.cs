// ============================================================
// ArmAnimSlotResolver — 동작 줄 → Catalog 폴백 → thin 투영 (컨트롤러는 동작 모름)
// ============================================================

using UnityEngine;

/// <summary>
/// AnimVerb 클립은 무기 Entry(동작 줄) 또는 Pipeline(Catalog)에 있다. SM thin에 투영한다.
/// Recoil/Blocked도 동작 줄 → Catalog Impact. 무기 Override 클립 맵은 쓰지 않는다.
/// </summary>
public static class ArmAnimSlotResolver
{
    enum PoseKind
    {
        Hold = 0,
        Aim = 1,
        Attack = 2
    }

    public static AnimatorOverrideController BuildResolvedOverride(
        RuntimeAnimatorController baseOrWeapon,
        ArmAnimSlotCatalog catalog,
        WeaponPresentation presentationL,
        WeaponPresentation presentationR,
        WeaponPresentation presentation2H,
        CombatLeaf actionL,
        CombatLeaf actionR,
        CombatLeaf action2H)
    {
        if (baseOrWeapon == null || catalog == null)
            return null;

        RuntimeAnimatorController root = baseOrWeapon;
        AnimatorOverrideController weaponOvr = baseOrWeapon as AnimatorOverrideController;
        if (weaponOvr != null && weaponOvr.runtimeAnimatorController != null)
            root = weaponOvr.runtimeAnimatorController;

        var resolved = new AnimatorOverrideController(root);
        ProjectThinKeys(
            resolved,
            catalog,
            presentationL,
            presentationR,
            presentation2H,
            actionL,
            actionR,
            action2H);
        return resolved;
    }

    public static void RemapThinKeys(
        AnimatorOverrideController resolved,
        ArmAnimSlotCatalog catalog,
        WeaponPresentation presentationL,
        WeaponPresentation presentationR,
        WeaponPresentation presentation2H,
        CombatLeaf actionL,
        CombatLeaf actionR,
        CombatLeaf action2H,
        bool surpriseAttackL = false,
        bool surpriseAttackR = false,
        bool surpriseAttack2H = false)
    {
        if (resolved == null || catalog == null)
            return;

        ProjectThinKeys(
            resolved,
            catalog,
            presentationL,
            presentationR,
            presentation2H,
            actionL,
            actionR,
            action2H,
            surpriseAttackL,
            surpriseAttackR,
            surpriseAttack2H);
    }

    static void ProjectThinKeys(
        AnimatorOverrideController resolved,
        ArmAnimSlotCatalog catalog,
        WeaponPresentation presentationL,
        WeaponPresentation presentationR,
        WeaponPresentation presentation2H,
        CombatLeaf actionL,
        CombatLeaf actionR,
        CombatLeaf action2H,
        bool surpriseAttackL = false,
        bool surpriseAttackR = false,
        bool surpriseAttack2H = false)
    {
        ProjectPose(
            resolved,
            catalog,
            catalog.HoldThin,
            presentationL,
            presentationR,
            presentation2H,
            actionL,
            actionR,
            action2H,
            PoseKind.Hold,
            null,
            false,
            false,
            false);
        ProjectPose(
            resolved,
            catalog,
            catalog.AimThin,
            presentationL,
            presentationR,
            presentation2H,
            actionL,
            actionR,
            action2H,
            PoseKind.Aim,
            catalog.HoldThin,
            false,
            false,
            false);
        ProjectPose(
            resolved,
            catalog,
            catalog.AttackThin,
            presentationL,
            presentationR,
            presentation2H,
            actionL,
            actionR,
            action2H,
            PoseKind.Attack,
            catalog.HoldThin,
            surpriseAttackL,
            surpriseAttackR,
            surpriseAttack2H);
    }

    static void ProjectPose(
        AnimatorOverrideController resolved,
        ArmAnimSlotCatalog catalog,
        ArmAnimSlotCatalog.HandClips thin,
        WeaponPresentation presentationL,
        WeaponPresentation presentationR,
        WeaponPresentation presentation2H,
        CombatLeaf actionL,
        CombatLeaf actionR,
        CombatLeaf action2H,
        PoseKind pose,
        ArmAnimSlotCatalog.HandClips poseFallback,
        bool surpriseAttackL,
        bool surpriseAttackR,
        bool surpriseAttack2H)
    {
        if (thin == null)
            return;

        if (thin.leftBase != null)
            resolved[thin.leftBase] = PoseClip(
                presentationL,
                actionL,
                catalog,
                pose,
                WieldHand.Left,
                poseFallback,
                thin.leftBase,
                surpriseAttackL);
        if (thin.rightBase != null)
            resolved[thin.rightBase] = PoseClip(
                presentationR,
                actionR,
                catalog,
                pose,
                WieldHand.Right,
                poseFallback,
                thin.rightBase,
                surpriseAttackR);
        if (thin.twoHandBase != null)
            resolved[thin.twoHandBase] = PoseClip(
                presentation2H,
                action2H,
                catalog,
                pose,
                WieldHand.TwoHand,
                poseFallback,
                thin.twoHandBase,
                surpriseAttack2H);
    }

    static AnimationClip PoseClip(
        WeaponPresentation presentation,
        CombatLeaf action,
        ArmAnimSlotCatalog catalog,
        PoseKind pose,
        WieldHand hand,
        ArmAnimSlotCatalog.HandClips poseFallback,
        AnimationClip thinClip,
        bool useSurpriseAttack)
    {
        AnimationClip fromEntry = LibHand(
            EntryPose(presentation, action, pose, useSurpriseAttack),
            hand);
        if (fromEntry != null)
            return fromEntry;

        ArmAnimSlotCatalog.ActionLibraryEntry lib =
            catalog != null ? catalog.FindAction(action) : null;
        AnimationClip fromCatalog = LibHand(CatalogPose(lib, pose), hand);
        if (fromCatalog != null)
            return fromCatalog;

        AnimationClip fallback = LibHand(poseFallback, hand);
        return fallback != null ? fallback : thinClip;
    }

    static ArmAnimSlotCatalog.HandClips EntryPose(
        WeaponPresentation presentation,
        CombatLeaf action,
        PoseKind pose,
        bool useSurpriseAttack)
    {
        if (presentation == null ||
            !presentation.TryGetEntry(action, out WeaponPresentation.Entry entry) ||
            entry == null)
            return null;
        if (pose == PoseKind.Hold)
            return entry.holdClips;
        if (pose == PoseKind.Aim)
            return entry.aimClips;
        if (useSurpriseAttack && HasAnyClip(entry.surpriseAttackClips))
            return entry.surpriseAttackClips;
        return entry.attackClips;
    }

    static bool HasAnyClip(ArmAnimSlotCatalog.HandClips clips) =>
        clips != null &&
        (clips.leftBase != null || clips.rightBase != null || clips.twoHandBase != null);

    static ArmAnimSlotCatalog.HandClips CatalogPose(
        ArmAnimSlotCatalog.ActionLibraryEntry lib,
        PoseKind pose)
    {
        if (lib == null)
            return null;
        if (pose == PoseKind.Hold)
            return lib.hold;
        if (pose == PoseKind.Aim)
            return lib.aim;
        return lib.attack;
    }

    static AnimationClip LibHand(ArmAnimSlotCatalog.HandClips lib, WieldHand hand)
    {
        if (lib == null)
            return null;
        if (hand == WieldHand.Left)
            return lib.leftBase;
        if (hand == WieldHand.Right)
            return lib.rightBase;
        return lib.twoHandBase;
    }

    public static AnimationClip EffectiveClip(AnimationClip baseClip, AnimatorOverrideController overrideController)
    {
        if (baseClip == null)
            return null;
        if (overrideController == null)
            return baseClip;
        AnimationClip mapped = overrideController[baseClip];
        return mapped != null ? mapped : baseClip;
    }

    /// <summary>무기 Entry → Catalog → thin Attack 클립 (dig·연출 타이밍용).</summary>
    public static AnimationClip ResolvePresentationAttackClip(
        ArmAnimSlotCatalog catalog,
        WeaponPresentation presentation,
        CombatLeaf action,
        WieldHand hand)
    {
        if (catalog == null)
            return null;

        ArmAnimSlotCatalog.HandClips attackThin = catalog.AttackThin;
        AnimationClip thinClip = LibHand(attackThin, hand);
        return PoseClip(
            presentation,
            CombatLeafUtil.Normalize(action),
            catalog,
            PoseKind.Attack,
            hand,
            catalog.HoldThin,
            thinClip,
            useSurpriseAttack: false);
    }
}
