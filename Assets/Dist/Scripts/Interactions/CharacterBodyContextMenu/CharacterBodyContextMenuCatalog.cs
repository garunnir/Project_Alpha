// ============================================================
// CharacterBodyContextMenuCatalog — 몸 RMB Contributor 등록 SSOT
// ============================================================

using System.Collections.Generic;

public static class CharacterBodyContextMenuCatalog
{
    static readonly ICharacterBodyContextMenuContributor[] Contributors =
    {
        new OpenBodyLootContextContributor(),
        new StripWearContextContributor(),
    };

    public static IReadOnlyList<ICharacterBodyContextMenuContributor> All => Contributors;
}
