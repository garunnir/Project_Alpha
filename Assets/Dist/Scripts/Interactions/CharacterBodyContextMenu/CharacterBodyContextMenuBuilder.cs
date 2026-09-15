// ============================================================
// CharacterBodyContextMenuBuilder — Catalog Contributor 순회 → Model
// ============================================================

using System.Collections.Generic;
using UnityEngine;

public static class CharacterBodyContextMenuBuilder
{
    public static ContextMenuModel Build(
        PlayerInventoryHost victim,
        IReadOnlyList<ICharacterBodyContextMenuContributor> contributors)
    {
        var roots = new List<ContextMenuEntry>();
        if (victim == null || contributors == null)
            return new ContextMenuModel(roots);

        CharacterBodyContextRequest request = CreateRequest(victim);
        if (request == null)
            return new ContextMenuModel(roots);

        for (int i = 0; i < contributors.Count; i++)
            contributors[i]?.Contribute(request, roots);

        return new ContextMenuModel(roots);
    }

    public static bool HasHoverOutlineContext(PlayerInventoryHost victim)
    {
        if (!CharacterBodyLootContextGate.Passes(victim))
            return false;

        ContextMenuModel model = Build(victim, CharacterBodyContextMenuCatalog.All);
        return ContextMenuQuery.HasExecutableLeaf(model.Roots);
    }

    public static bool TryShow(PlayerInventoryHost victim, Vector2 screenPosition)
    {
        if (!HasHoverOutlineContext(victim))
            return false;

        ContextMenuModel model = Build(victim, CharacterBodyContextMenuCatalog.All);
        if (model.IsEmpty)
            return false;

        if (!UIContextMenuHost.TryShow(model, screenPosition))
        {
            Debug.LogError("[CharacterBodyContextMenuBuilder] UIContextMenuHost failed to show.");
            return false;
        }

        return true;
    }

    static CharacterBodyContextRequest CreateRequest(PlayerInventoryHost victim)
    {
        if (victim == null)
            return null;

        PlayerGearHost gearHost = victim.BodyRefs != null
            ? victim.BodyRefs.GearHost
            : null;
        gearHost?.BindDomainIfNeeded();

        return new CharacterBodyContextRequest
        {
            Victim = victim,
            VictimGearHost = gearHost,
            VictimGear = gearHost != null ? gearHost.Service : null,
        };
    }
}
