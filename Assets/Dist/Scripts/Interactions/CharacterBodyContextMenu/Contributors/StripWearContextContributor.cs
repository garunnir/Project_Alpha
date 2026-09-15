// ============================================================
// StripWearContextContributor — 착용 행별 벗기기 액션
// ============================================================

using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;

public sealed class StripWearContextContributor : ICharacterBodyContextMenuContributor
{
    public void Contribute(CharacterBodyContextRequest request, List<ContextMenuEntry> roots)
    {
        if (request?.VictimGear == null || roots == null)
            return;

        IReadOnlyList<ItemStack> worn = request.VictimGear.Wear.Worn;
        for (int i = 0; i < worn.Count; i++)
        {
            ItemStack stack = worn[i];
            if (stack?.Item == null)
                continue;

            string label = CharacterGearLabels.StripWear + " — " + UITextPresenter.GetItemName(stack.Item);
            roots.Add(ContextMenuEntry.Leaf(
                "strip-wear-" + stack.ItemId,
                label,
                new StripWearContextAction(request.VictimGear, stack)));
        }
    }
}
