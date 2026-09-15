// ============================================================
// InventoryDragDrop — 활성 드래그 페이로드를 대상 컨테이너로 적용
// ============================================================

using System;
using System.Collections.Generic;

public static class InventoryDragDrop
{
    public static bool TryApplyTo(InventorySession session, InventoryContainer target)
    {
        if (session == null || target == null)
            return false;

        if (LootAggregateHost.IsAggregateContainer(target))
            return false;

        if (!InventoryDragState.TryGetActive(out InventoryDragPayload payload))
            return false;

        if (payload.Kind == InventoryDragKind.AggregateDisplayGroup)
            return TryApplyAggregateGroup(session, target, payload);

        if (payload.SourceContainer == null || payload.Stacks == null || payload.Stacks.Count == 0)
            return false;

        if (LootAggregateHost.IsAggregateContainer(payload.SourceContainer))
            return false;

        // ContainerTab SourceContainer is the parent holding the bag stack — same as the open
        // player-body list target is a valid no-op (MoveStacks from==to). Do not early-out only
        // for Item / ContainerContents where source==target means "drop onto self".
        if (payload.Kind != InventoryDragKind.ContainerTab &&
            payload.SourceContainer == target)
            return false;

        if (payload.Kind == InventoryDragKind.ContainerContents)
        {
            InventoryTimedMoveHost timed = InventoryTimedMoveHost.Active;
            if (timed != null)
            {
                return timed.TryBeginSequentialUntilFull(
                    session,
                    payload.SourceContainer,
                    target,
                    payload.Stacks);
            }

            return session.MoveStacksSequentiallyUntilFull(
                payload.SourceContainer, target, payload.Stacks) > 0;
        }

        if (payload.Kind == InventoryDragKind.Item)
        {
            return TryMoveItemStacks(
                session,
                payload.SourceContainer,
                target,
                payload.Stacks,
                payload.ClearSelection);
        }

        return TryMoveItemStacks(
            session,
            payload.SourceContainer,
            target,
            payload.Stacks,
            null);
    }

    public static bool TryApplyAggregateGroup(
        InventorySession session,
        InventoryContainer target,
        InventoryDragPayload payload)
    {
        if (session == null || target == null || payload == null)
            return false;

        if (LootAggregateHost.IsAggregateContainer(target))
            return false;

        IReadOnlyList<(InventoryContainer owner, ItemStack stack)> sources = payload.Sources;
        if (sources == null || sources.Count == 0)
            return false;

        var moves = new List<(InventoryContainer from, ItemStack stack)>(sources.Count);
        for (int i = 0; i < sources.Count; i++)
        {
            (InventoryContainer owner, ItemStack stack) = sources[i];
            if (owner == null || stack == null)
                continue;

            if (LootAggregateHost.IsAggregateContainer(owner) || owner == target)
                continue;

            moves.Add((owner, stack));
        }

        if (moves.Count == 0)
            return false;

        Action clearSelection = payload.ClearSelection;
        InventoryTimedMoveHost timed = InventoryTimedMoveHost.Active;
        if (timed != null)
        {
            return timed.TryBeginMultiSourceSequentialUntilFull(
                session,
                target,
                moves,
                () => clearSelection?.Invoke());
        }

        int moved = MoveStacksSequentiallyUntilFull(session, moves, target);
        if (moved > 0)
            clearSelection?.Invoke();

        return moved > 0;
    }

    static int MoveStacksSequentiallyUntilFull(
        InventorySession session,
        IReadOnlyList<(InventoryContainer from, ItemStack stack)> moves,
        InventoryContainer target)
    {
        if (session == null || target == null || moves == null || moves.Count == 0)
            return 0;

        int moved = 0;
        for (int i = 0; i < moves.Count; i++)
        {
            (InventoryContainer from, ItemStack stack) = moves[i];
            if (from == null || stack == null || from == target)
                continue;

            if (InventorySession.MustTransferStackWhole(stack))
            {
                if (session.MoveStackCount(from, target, stack, stack.Count))
                    moved++;
                continue;
            }

            int units = stack.Count;
            for (int unit = 0; unit < units; unit++)
            {
                if (!session.MoveStackCount(from, target, stack, 1))
                    break;

                moved++;
            }
        }

        return moved;
    }

    public static bool TryMoveItemStacks(
        InventorySession session,
        InventoryContainer from,
        InventoryContainer to,
        IReadOnlyList<ItemStack> stacks,
        Action clearSelection = null)
    {
        if (session == null || from == null || to == null || stacks == null || stacks.Count == 0)
            return false;

        if (from == to)
            return false;

        InventoryTimedMoveHost timed = InventoryTimedMoveHost.Active;
        if (timed != null)
        {
            return timed.TryBeginMove(
                session,
                from,
                to,
                stacks,
                () => clearSelection?.Invoke());
        }

        if (!session.MoveStacks(from, to, stacks))
            return false;

        clearSelection?.Invoke();
        return true;
    }

    public static bool TryQuickTransferBetweenWindows(
        InventorySession session,
        UIInventoryListWindow primaryWindow,
        UIInventoryListWindow lootWindow,
        UIItemListView sourceListView,
        ItemStack stack,
        InventoryContainer sourceContainer)
    {
        if (session == null || stack == null || sourceContainer == null || sourceListView == null)
            return false;

        if (LootAggregateHost.IsAggregateContainer(sourceContainer))
            return false;

        if (primaryWindow == null || lootWindow == null)
            return false;

        if (!primaryWindow.IsVisible || !lootWindow.IsVisible)
            return false;

        UIInventoryListWindow peerWindow;
        if (sourceListView == primaryWindow.ListView)
            peerWindow = lootWindow;
        else if (sourceListView == lootWindow.ListView)
            peerWindow = primaryWindow;
        else
            return false;

        InventoryContainer target = peerWindow.SelectedContainer;
        if (target == null || sourceContainer == target)
            return false;

        if (LootAggregateHost.IsAggregateContainer(target))
            return false;

        InventoryListSelection selection = sourceListView.Selection;
        if (selection == null)
            return false;

        if (!selection.IsSelected(stack))
            selection.SetSingle(stack);

        IReadOnlyList<ItemStack> stacks = selection.GetSelectedStacks();
        return TryMoveItemStacks(session, sourceContainer, target, stacks, () => selection.Clear());
    }
}
