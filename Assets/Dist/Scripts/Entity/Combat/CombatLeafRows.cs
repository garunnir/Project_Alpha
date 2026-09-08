// ============================================================
// CombatLeafRows — Presentation 행 → available / default / instance select
// ============================================================

public static class CombatLeafRows
{
    public static CombatLeafMask Available(WeaponPresentation presentation)
    {
        if (presentation == null)
            return CombatLeafMask.Strike;

        presentation.RebuildSupportedActions();
        CombatLeafMask mask = presentation.SupportedActions;
        return mask == CombatLeafMask.None
            ? CombatLeafMask.Strike
            : mask;
    }

    public static CombatLeaf Default(WeaponPresentation presentation)
    {
        if (presentation == null)
            return CombatLeaf.Strike;

        WeaponPresentation.Entry[] entries = presentation.Entries;
        if (entries == null || entries.Length == 0)
            return CombatLeaf.Strike;

        int index = presentation.DefaultEntryIndex;
        if (index < 0 || index >= entries.Length)
            index = 0;

        WeaponPresentation.Entry entry = entries[index];
        if (entry != null)
            return CombatLeafUtil.Normalize(entry.leaf);

        for (int i = 0; i < entries.Length; i++)
        {
            WeaponPresentation.Entry candidate = entries[i];
            if (candidate == null)
                continue;
            return CombatLeafUtil.Normalize(candidate.leaf);
        }

        return CombatLeaf.Strike;
    }

    public static CombatLeaf ResolveSelected(
        ItemInstance instance,
        WeaponPresentation presentation)
    {
        CombatLeafMask available = Available(presentation);
        CombatLeaf? stored = instance != null ? instance.SelectedLeaf : null;
        if (stored.HasValue &&
            (available & CombatLeafUtil.ToMask(stored.Value)) != 0)
            return CombatLeafUtil.Normalize(stored.Value);

        return Default(presentation);
    }

    public static WeaponPresentation Resolve(
        WeaponPresentationCatalog catalog,
        ItemStack stack)
    {
        if (catalog == null)
            return null;
        return catalog.Resolve(stack?.ItemId, stack?.Item);
    }
}
