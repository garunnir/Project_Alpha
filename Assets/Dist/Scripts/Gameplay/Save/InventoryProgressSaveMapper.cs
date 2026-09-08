// ============================================================
// InventoryProgressSaveMapper — InventoryGearSaveDto ↔ 런타임 스택
// ============================================================

using System;
using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

public static class InventoryProgressSaveMapper
{
    public static InventoryGearSaveDto Capture(PlayerInventoryHost inventoryHost, CharacterGearService gear)
    {
        if (inventoryHost?.Container == null)
            return null;

        var unique = new HashSet<ItemStack>();
        InventoryContainer body = inventoryHost.Container;

        for (int i = 0; i < body.Stacks.Count; i++)
            CollectStacks(body.Stacks[i], unique);

        if (gear != null)
        {
            IReadOnlyList<ItemStack> worn = gear.Wear.Worn;
            for (int i = 0; i < worn.Count; i++)
                CollectStacks(worn[i], unique);

            gear.Wield.Snapshot(out ItemStack left, out ItemStack right, out _);
            CollectStacks(left, unique);
            if (right != left)
                CollectStacks(right, unique);
        }

        if (unique.Count == 0)
        {
            return new InventoryGearSaveDto
            {
                stacks = Array.Empty<ItemStackSaveDto>(),
                bodyStackUids = Array.Empty<string>(),
                wornStackUids = Array.Empty<string>()
            };
        }

        var stackList = new List<ItemStack>(unique);
        stackList.Sort(CompareStacksByUid);

        var uidToDto = new Dictionary<string, ItemStackSaveDto>(stackList.Count, StringComparer.Ordinal);
        var stacks = new ItemStackSaveDto[stackList.Count];
        for (int i = 0; i < stackList.Count; i++)
        {
            ItemStackSaveDto row = ToStackDto(stackList[i]);
            stacks[i] = row;
            uidToDto[row.uid] = row;
        }

        return new InventoryGearSaveDto
        {
            stacks = stacks,
            bodyStackUids = CollectRootUids(body.Stacks),
            wornStackUids = gear != null ? CollectRootUids(gear.Wear.Worn) : Array.Empty<string>(),
            wieldLeftUid = gear?.Wield.Left != null ? FormatUid(gear.Wield.Left) : null,
            wieldRightUid = gear?.Wield.Right != null ? FormatUid(gear.Wield.Right) : null,
            wieldTwoHand = gear != null && gear.Wield.IsTwoHand
        };
    }

    public static bool TryApply(
        InventoryGearSaveDto dto,
        PlayerInventoryHost inventoryHost,
        CharacterGearService gear)
    {
        if (dto == null || inventoryHost?.Container == null)
            return false;

        gear?.Wear.Clear();
        if (gear != null)
            gear.Wield.Restore(null, null, false);

        InventoryContainer body = inventoryHost.Container;
        body.ClearStackReferences();

        if (dto.stacks == null || dto.stacks.Length == 0)
            return true;

        var byUid = new Dictionary<string, ItemStack>(dto.stacks.Length, StringComparer.Ordinal);
        for (int i = 0; i < dto.stacks.Length; i++)
        {
            ItemStackSaveDto row = dto.stacks[i];
            if (row == null || string.IsNullOrEmpty(row.uid) || string.IsNullOrEmpty(row.itemId))
                continue;

            ItemStack stack = CreateStackFromDto(row);
            if (stack != null)
                byUid[row.uid] = stack;
        }

        for (int i = 0; i < dto.stacks.Length; i++)
            WireStackLinks(dto.stacks[i], byUid);

        AddStacksToContainer(body, dto.bodyStackUids, byUid);

        if (gear != null)
        {
            AddWornStacks(gear, dto.wornStackUids, byUid);
            ItemStack left = ResolveUid(dto.wieldLeftUid, byUid);
            ItemStack right = ResolveUid(dto.wieldRightUid, byUid);
            gear.Wield.Restore(left, right, dto.wieldTwoHand);
        }

        body.NotifyContentsChanged();
        return true;
    }

    static void CollectStacks(ItemStack stack, HashSet<ItemStack> into)
    {
        if (stack == null || !into.Add(stack))
            return;

        if (stack.LoadedMagazine != null)
            CollectStacks(stack.LoadedMagazine, into);

        if (stack.Nested == null)
            return;

        IReadOnlyList<ItemStack> nested = stack.Nested.Stacks;
        for (int i = 0; i < nested.Count; i++)
            CollectStacks(nested[i], into);
    }

    static int CompareStacksByUid(ItemStack a, ItemStack b) =>
        string.Compare(FormatUid(a), FormatUid(b), StringComparison.Ordinal);

    static string[] CollectRootUids(IReadOnlyList<ItemStack> stacks)
    {
        if (stacks == null || stacks.Count == 0)
            return Array.Empty<string>();

        var uids = new string[stacks.Count];
        for (int i = 0; i < stacks.Count; i++)
            uids[i] = FormatUid(stacks[i]);
        return uids;
    }

    static ItemStackSaveDto ToStackDto(ItemStack stack)
    {
        var dto = new ItemStackSaveDto
        {
            uid = FormatUid(stack),
            itemId = stack.ItemId,
            count = stack.Count,
            instance = ToInstanceDto(stack.Instance),
            loadedMagazineUid = stack.LoadedMagazine != null ? FormatUid(stack.LoadedMagazine) : null,
            nestedStackUids = stack.Nested != null
                ? CollectRootUids(stack.Nested.Stacks)
                : Array.Empty<string>()
        };
        return dto;
    }

    static ItemInstanceSaveDto ToInstanceDto(ItemInstance instance)
    {
        if (instance == null)
            return null;

        var dto = new ItemInstanceSaveDto
        {
            uid = instance.Uid.ToString("N"),
            damageLevel = instance.DamageLevel,
            hasSelectedAction = instance.SelectedLeaf.HasValue,
            selectedAction = instance.SelectedLeaf.HasValue ? (int)instance.SelectedLeaf.Value : 0,
            chamberRounds = instance.ChamberRounds,
            chamberAmmoId = instance.ChamberAmmoId,
            supplyRounds = instance.SupplyRounds,
            supplyAmmoId = instance.SupplyAmmoId,
            toolCharges = instance.ToolCharges,
            createdWorldMinute = instance.CreatedWorldMinute,
            isRotten = instance.IsRotten,
            isCooked = instance.IsCooked,
            isHot = instance.IsHot,
            hotUntilWorldMinute = instance.HotUntilWorldMinute
        };
        return dto;
    }

    static ItemStack CreateStackFromDto(ItemStackSaveDto dto)
    {
        ItemData item = GameplayData.GetItem(dto.itemId);
        if (item == null)
        {
            Debug.LogWarning($"[InventoryProgressSaveMapper] Unknown item '{dto.itemId}'.");
            return null;
        }

        int damage = dto.instance != null ? dto.instance.damageLevel : 0;
        Guid uid = ParseUid(dto.uid);
        ItemInstance instance = ItemInstance.FromSave(item, damage, uid, dto.instance);
        var stack = new ItemStack(instance, dto.count);
        return stack;
    }

    static void WireStackLinks(ItemStackSaveDto dto, Dictionary<string, ItemStack> byUid)
    {
        if (dto == null || !byUid.TryGetValue(dto.uid, out ItemStack stack))
            return;

        if (!string.IsNullOrEmpty(dto.loadedMagazineUid)
            && byUid.TryGetValue(dto.loadedMagazineUid, out ItemStack magazine))
            stack.TryAttachMagazine(magazine);

        if (dto.nestedStackUids == null || dto.nestedStackUids.Length == 0)
            return;

        if (!stack.TryEnsureNested(new FixedContainerCapacityPolicy()))
            return;

        InventoryContainer nested = stack.Nested;
        nested.ClearStackReferences();
        AddStacksToContainer(nested, dto.nestedStackUids, byUid);
    }

    static void AddStacksToContainer(
        InventoryContainer container,
        string[] uids,
        Dictionary<string, ItemStack> byUid)
    {
        if (container == null || uids == null)
            return;

        for (int i = 0; i < uids.Length; i++)
        {
            ItemStack stack = ResolveUid(uids[i], byUid);
            if (stack != null)
                container.TryAddStackReference(stack);
        }
    }

    static void AddWornStacks(
        CharacterGearService gear,
        string[] uids,
        Dictionary<string, ItemStack> byUid)
    {
        if (uids == null)
            return;

        for (int i = 0; i < uids.Length; i++)
        {
            ItemStack stack = ResolveUid(uids[i], byUid);
            if (stack == null)
                continue;

            stack.TryEnsureNested(new FixedContainerCapacityPolicy());
            if (!gear.Wear.TryAdd(stack))
                Debug.LogWarning($"[InventoryProgressSaveMapper] Wear restore skipped '{stack.ItemId}'.");
        }
    }

    static ItemStack ResolveUid(string uid, Dictionary<string, ItemStack> byUid)
    {
        if (string.IsNullOrEmpty(uid))
            return null;
        return byUid.TryGetValue(uid, out ItemStack stack) ? stack : null;
    }

    static string FormatUid(ItemStack stack) =>
        stack?.Instance != null ? stack.Instance.Uid.ToString("N") : string.Empty;

    static Guid ParseUid(string uid)
    {
        if (string.IsNullOrEmpty(uid))
            return Guid.NewGuid();
        return Guid.TryParse(uid, out Guid parsed) ? parsed : Guid.NewGuid();
    }
}
