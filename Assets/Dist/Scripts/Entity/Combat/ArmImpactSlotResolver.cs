// ============================================================
// ArmImpactSlotResolver — 동작 줄 Recoil/Blocked → Catalog Impact → thin
// ============================================================

using UnityEngine;

/// <summary>
/// Impact 클립은 무기 Entry(동작 줄) 또는 Catalog Impact 행. SM Impact thin에 투영한다.
/// 무기 Override Impact thin은 동작별이 아니라서 쓰지 않는다.
/// Animator-독립 재생은 <see cref="ResolveImpactClip"/> 결과 클립 +
/// <see cref="WeaponAnimClipSpeeds.GetSpeed"/> 배속을 쓰면 된다.
/// </summary>
public static class ArmImpactSlotResolver
{
    /// <summary>
    /// Entry → Catalog Impact → thin. Animator 불필요.
    /// 배속은 <see cref="WeaponAnimClipSpeeds.GetSpeed"/>(clip).
    /// </summary>
    public static AnimationClip ResolveImpactClip(
        ArmAnimSlotCatalog catalog,
        WeaponPresentation presentation,
        CombatLeaf action,
        ArmImpactKind kind,
        WieldHand hand)
    {
        if (catalog == null)
            return null;

        AnimationClip thin = catalog.ImpactThinClip(kind);
        AnimationClip fromEntry = PickHand(EntryImpact(presentation, action, kind), hand);
        if (fromEntry != null)
            return fromEntry;

        ArmAnimSlotCatalog.ImpactLibraryEntry lib = catalog.FindImpact(kind);
        AnimationClip fromCatalog = PickHand(lib != null ? lib.clips : null, hand);
        return fromCatalog != null ? fromCatalog : thin;
    }

    public static void ProjectImpact(
        AnimatorOverrideController resolved,
        ArmAnimSlotCatalog catalog,
        WeaponPresentation presentation,
        CombatLeaf action,
        ArmImpactKind kind,
        WieldHand hand)
    {
        if (resolved == null || catalog == null)
            return;

        AnimationClip thin = catalog.ImpactThinClip(kind);
        if (thin == null)
            return;

        resolved[thin] = ResolveImpactClip(catalog, presentation, action, kind, hand);
    }

    static ArmAnimSlotCatalog.HandClips EntryImpact(
        WeaponPresentation presentation,
        CombatLeaf action,
        ArmImpactKind kind)
    {
        if (presentation == null ||
            !presentation.TryGetEntry(action, out WeaponPresentation.Entry entry) ||
            entry == null)
            return null;
        return kind == ArmImpactKind.Blocked ? entry.blockedClips : entry.recoilClips;
    }

    static AnimationClip PickHand(ArmAnimSlotCatalog.HandClips clips, WieldHand hand)
    {
        if (clips == null)
            return null;
        if (hand == WieldHand.Left)
            return clips.leftBase;
        if (hand == WieldHand.TwoHand)
            return clips.twoHandBase;
        return clips.rightBase;
    }
}
