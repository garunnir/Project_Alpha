// ============================================================
// ContextMenuQuery — 컨텍스트 메뉴 실행 가능 항목 조회 SSOT
// ============================================================

using System.Collections.Generic;

public static class ContextMenuQuery
{
    public static bool HasExecutableLeaf(IReadOnlyList<ContextMenuEntry> roots) =>
        HasExecutableLeafRecursive(roots);

    public static void CollectExecutableLeaves(
        IReadOnlyList<ContextMenuEntry> entries,
        List<ContextMenuEntry> into)
    {
        if (entries == null || into == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            ContextMenuEntry entry = entries[i];
            if (entry == null)
                continue;

            if (entry.HasChildren)
            {
                CollectExecutableLeaves(entry.Children, into);
                continue;
            }

            if (entry.Action == null)
                continue;

            if (!string.IsNullOrEmpty(entry.Action.GetDisabledReason()))
                continue;

            into.Add(entry);
        }
    }

    static bool HasExecutableLeafRecursive(IReadOnlyList<ContextMenuEntry> entries)
    {
        if (entries == null)
            return false;

        for (int i = 0; i < entries.Count; i++)
        {
            ContextMenuEntry entry = entries[i];
            if (entry == null)
                continue;

            if (entry.HasChildren)
            {
                if (HasExecutableLeafRecursive(entry.Children))
                    return true;
                continue;
            }

            if (entry.Action == null)
                continue;

            if (!string.IsNullOrEmpty(entry.Action.GetDisabledReason()))
                continue;

            return true;
        }

        return false;
    }
}
