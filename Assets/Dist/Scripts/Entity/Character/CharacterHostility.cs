// ============================================================
// CharacterHostility — 세력 관계표 기반 적대 판정
// ============================================================

using System.Collections.Generic;
using UnityEngine;

public static class CharacterHostility
{
    static CharacterFactionCatalog s_catalog;
    static readonly HashSet<int> WarnedMissingFaction = new();

    public static void BindCatalog(CharacterFactionCatalog catalog) => s_catalog = catalog;

    public static bool IsHostile(CharacterFactionHost self, CharacterFactionHost other)
    {
        if (self == null || other == null)
            return false;

        CharacterFaction selfFaction = self.Faction;
        CharacterFaction otherFaction = other.Faction;
        if (selfFaction == null || otherFaction == null)
        {
            WarnMissingFactionOnce(self, other, selfFaction, otherFaction);
            return false;
        }

        if (ReferenceEquals(selfFaction, otherFaction))
            return false;

        if (s_catalog != null && s_catalog.TryGetStance(selfFaction, otherFaction, out FactionStance stance))
            return stance == FactionStance.Hostile;

        return false;
    }

    static void WarnMissingFactionOnce(
        CharacterFactionHost self,
        CharacterFactionHost other,
        CharacterFaction selfFaction,
        CharacterFaction otherFaction)
    {
        if (self == null || other == null)
            return;

        int key = (self.GetInstanceID() * 397) ^ other.GetInstanceID();
        if (!WarnedMissingFaction.Add(key))
            return;

        string selfName = self.name;
        string otherName = other.name;
        string selfId = selfFaction != null ? selfFaction.Id : "(null)";
        string otherId = otherFaction != null ? otherFaction.Id : "(null)";
        Debug.LogError(
            $"[CharacterHostility] Missing faction assignment: {selfName}={selfId}, {otherName}={otherId}.",
            self.LogContext);
    }
}
