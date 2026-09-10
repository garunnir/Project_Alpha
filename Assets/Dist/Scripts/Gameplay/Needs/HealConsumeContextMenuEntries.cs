// ============================================================
// HealConsumeContextMenuEntries — heal/벗기 컨텍스트 메뉴 항목 SSOT
// ============================================================

using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;

public static class HealConsumeContextMenuEntries
{
    static readonly List<string> EligiblePartsScratch = new(16);
    static readonly List<BodyPartEffect> EffectScratch = new(8);

    /// <summary>인벤: 아이템 1개 → 후보 부위 Leaf.</summary>
    public static void AppendPartLeavesFromItem(
        ItemStack stack,
        InventoryContainer container,
        List<ContextMenuEntry> dest)
    {
        if (dest == null || stack?.Item == null)
            return;

        UseActionData action = stack.Item.use_action;
        if (!ConsumeService.IsHealAction(action))
            return;
        if (!BodyHealApply.TryCollectEligibleParts(PlayerPossessSession.SessionBody, action, EligiblePartsScratch))
            return;

        for (int i = 0; i < EligiblePartsScratch.Count; i++)
        {
            string partId = EligiblePartsScratch[i];
            dest.Add(ContextMenuEntry.Leaf(
                ConsumeMenuIds.UsePartPrefix + partId,
                PlayerStatusLabels.GetPartName(partId),
                new ConsumeContextAction(stack, container, partId)));
        }
    }

    /// <summary>HUD/Status: 부위 1개 → 몸통 인벤+들기 heal 아이템 Leaf.</summary>
    public static void AppendItemLeavesForPart(string partId, List<ContextMenuEntry> dest)
    {
        if (dest == null || string.IsNullOrEmpty(partId))
            return;

        ICharacterBody body = PlayerPossessSession.SessionBody;
        if (body == null)
            return;

        PlayerItemAccess.VisitBodyAndWield((stack, container) =>
            TryAddItemLeaf(stack, container, body, partId, dest));
    }

    public static void AppendUnwrapLeaf(string partId, List<ContextMenuEntry> dest)
    {
        if (dest == null || string.IsNullOrEmpty(partId))
            return;
        if (!BodyHealApply.HasBandagedUnder(PlayerPossessSession.SessionBody, partId, EffectScratch))
            return;

        dest.Add(ContextMenuEntry.Leaf(
            ConsumeMenuIds.UnwrapPrefix + partId,
            ItemContextMenuLabels.Unwrap,
            new UnwrapBandageContextAction(partId)));
    }

    static void TryAddItemLeaf(
        ItemStack stack,
        InventoryContainer container,
        ICharacterBody body,
        string partId,
        List<ContextMenuEntry> dest)
    {
        if (stack?.Item == null || stack.Count < 1)
            return;

        UseActionData action = stack.Item.use_action;
        if (!ConsumeService.IsHealAction(action))
            return;
        if (!BodyHealApply.CanApplyTo(body, action, partId))
            return;
        if (!ConsumeService.CanConsume(stack, container, partId))
            return;

        string itemId = stack.Item.id ?? string.Empty;
        dest.Add(ContextMenuEntry.Leaf(
            ConsumeMenuIds.UsePartPrefix + partId + ":" + itemId,
            UITextPresenter.GetItemName(stack.Item),
            new ConsumeContextAction(stack, container, partId)));
    }
}
