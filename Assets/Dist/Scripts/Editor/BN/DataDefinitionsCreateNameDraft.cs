// ============================================================
// DataDefinitionsCreateNameDraft — + Create Base name 초안 SSOT
// ============================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;

static class DataDefinitionsCreateNameDraft
{
    static readonly Dictionary<string, string> Drafts = new Dictionary<string, string>();

    public static string GetOrRecommend(string draftKey, Func<string> recommend)
    {
        if (string.IsNullOrEmpty(draftKey))
            throw new ArgumentException("draftKey is required.", nameof(draftKey));
        if (recommend == null)
            throw new ArgumentNullException(nameof(recommend));

        if (Drafts.TryGetValue(draftKey, out string draft) && !string.IsNullOrEmpty(draft))
            return draft;

        draft = recommend() ?? "New";
        Drafts[draftKey] = draft;
        return draft;
    }

    public static void Set(string draftKey, string value)
    {
        if (string.IsNullOrEmpty(draftKey))
            return;
        Drafts[draftKey] = value ?? string.Empty;
    }
}
#endif
