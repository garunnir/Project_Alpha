// ============================================================
// ArmImpactSlotResolver — 동작 줄 Recoil/Blocked → Catalog Impact → thin
// ============================================================

using UnityEngine;

/// <summary>
/// Impact 클립은 무기 Entry(동작 줄) 또는 Catalog Impact 행. SM Impact thin에 투영한다.
/// 무기 Override Impact thin은 동작별이 아니라서 쓰지 않는다.
/// </summary>
public static class ArmImpactSlotResolver
{
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

        AnimationClip fromEntry = PickHand(EntryImpact(presentation, action, kind), hand);
        if (fromEntry != null)
        {
            resolved[thin] = fromEntry;
            return;
        }

        ArmAnimSlotCatalog.ImpactLibraryEntry lib = catalog.FindImpact(kind);
        AnimationClip fromCatalog = PickHand(lib != null ? lib.clips : null, hand);
        resolved[thin] = fromCatalog != null ? fromCatalog : thin;
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
