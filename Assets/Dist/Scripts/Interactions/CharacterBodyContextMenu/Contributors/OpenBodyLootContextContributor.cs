// ============================================================
// OpenBodyLootContextContributor — 몸 컨테이너 루팅 액션
// ============================================================

using System.Collections.Generic;

public sealed class OpenBodyLootContextContributor : ICharacterBodyContextMenuContributor
{
    public void Contribute(CharacterBodyContextRequest request, List<ContextMenuEntry> roots)
    {
        if (request?.Victim?.Container == null || roots == null)
            return;

        roots.Add(ContextMenuEntry.Leaf(
            "open-body-loot",
            InteractionLabels.OpenContainer,
            new OpenBodyLootContextAction(request.Victim.Container)));
    }
}
