// ============================================================
// CharacterSpawnGearApplier — Definition 로드아웃을 스폰 직후 즉시 Wear/Wield/탄 채움
// ============================================================

using System;
using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

public static class CharacterSpawnGearApplier
{
    const string LogPrefix = "[CharacterSpawnGearApplier]";

    public static void Apply(CharacterDefinition definition, GameObject body)
    {
        if (definition == null || body == null)
            return;

        PlayerGearHost gearHost = body.GetBodyModule<PlayerGearHost>();
        PlayerInventoryHost inventoryHost = body.GetBodyModule<PlayerInventoryHost>();
        if (gearHost == null || inventoryHost == null)
        {
            Debug.LogError($"{LogPrefix} '{body.name}' needs PlayerGearHost and PlayerInventoryHost.", body);
            return;
        }

        gearHost.BindDomainIfNeeded();
        CharacterGearService service = gearHost.Service;
        if (service == null)
        {
            Debug.LogError($"{LogPrefix} Gear service missing on '{body.name}'.", body);
            return;
        }

        InventoryContainer bodyContainer = inventoryHost.Container;
        ApplyWear(definition, body, service);
        ApplyWield(definition, body, service, bodyContainer);
        ApplyBodySeeds(definition, body, bodyContainer);
        gearHost.RefreshPrimaryWield();
    }

    static void ApplyWear(CharacterDefinition definition, GameObject body, CharacterGearService service)
    {
        IReadOnlyList<string> ids = definition.WearItemIds;
        if (ids == null)
            return;

        for (int i = 0; i < ids.Count; i++)
        {
            ItemStack stack = CreateStack(ids[i], body);
            if (stack == null)
                continue;

            string reason = service.GetWearBlockedReason(stack);
            if (reason != null || !service.Wear.TryAdd(stack))
            {
                Debug.LogWarning($"{LogPrefix} Wear skipped '{ids[i]}': {reason}", body);
                continue;
            }

            stack.TryEnsureNested(new FixedContainerCapacityPolicy());
        }
    }

    static void ApplyWield(
        CharacterDefinition definition,
        GameObject body,
        CharacterGearService service,
        InventoryContainer bodyContainer)
    {
        IReadOnlyList<CharacterWieldLoadoutEntry> entries = definition.WieldLoadout;
        if (entries == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            CharacterWieldLoadoutEntry entry = entries[i];
            ItemStack stack = CreateStack(entry.itemId, body);
            if (stack == null)
                continue;

            TryFillGunAmmo(stack, body);

            WieldHand hand = entry.hand;
            if (GearHandleRules.IsTwoHandWeapon(stack.Item))
                hand = WieldHand.TwoHand;

            string reason = service.GetWieldBlockedReason(stack, hand);
            if (reason != null ||
                !service.Wield.TryWield(stack, hand, out ItemStack displacedLeft, out ItemStack displacedRight))
            {
                Debug.LogWarning($"{LogPrefix} Wield skipped '{entry.itemId}': {reason}", body);
                DepositToBody(bodyContainer, stack, body);
                continue;
            }

            DepositToBody(bodyContainer, displacedLeft, body);
            DepositToBody(bodyContainer, displacedRight, body);
        }
    }

    static void ApplyBodySeeds(
        CharacterDefinition definition,
        GameObject body,
        InventoryContainer bodyContainer)
    {
        IReadOnlyList<CharacterBodyItemSeed> seeds = definition.BodyItemSeeds;
        if (seeds == null || bodyContainer == null)
            return;

        for (int i = 0; i < seeds.Count; i++)
        {
            CharacterBodyItemSeed seed = seeds[i];
            if (string.IsNullOrEmpty(seed.itemId) || seed.count < 1)
                continue;

            ItemData item = GameplayData.GetItem(seed.itemId);
            if (item == null)
            {
                Debug.LogWarning($"{LogPrefix} Body seed missing '{seed.itemId}'.", body);
                continue;
            }

            if (bodyContainer.AddItem(item, seed.count) < 1)
                Debug.LogWarning($"{LogPrefix} Body seed did not fit '{seed.itemId}'.", body);
        }
    }

    static ItemStack CreateStack(string itemId, GameObject body)
    {
        if (string.IsNullOrEmpty(itemId))
            return null;

        ItemData item = GameplayData.GetItem(itemId);
        if (item == null)
        {
            Debug.LogWarning($"{LogPrefix} Item missing '{itemId}'.", body);
            return null;
        }

        return new ItemStack(item, 1);
    }

    static void TryFillGunAmmo(ItemStack gun, GameObject body)
    {
        if (gun?.Item?.gun == null || gun.Instance == null)
            return;

        if (WeaponAmmoFit.IsClipFed(gun.Item))
        {
            ItemData ammo = FindAmmoForGun(gun.Item);
            if (ammo == null)
            {
                Debug.LogWarning($"{LogPrefix} No compatible ammo for clip '{gun.ItemId}'.", body);
                return;
            }

            int cap = gun.Item.gun.clip_size;
            while (gun.Instance.TryAddChamberRound(cap, ammo.id))
            {
            }

            return;
        }

        if (!WeaponAmmoFit.HasMagazineWell(gun.Item))
            return;

        string magId = FirstAllowedMagazineId(gun.Item);
        ItemData magItem = string.IsNullOrEmpty(magId) ? null : GameplayData.GetItem(magId);
        if (magItem?.magazine == null)
        {
            Debug.LogWarning($"{LogPrefix} No compatible magazine for '{gun.ItemId}'.", body);
            return;
        }

        ItemStack magazine = new ItemStack(magItem, 1);
        ItemData magAmmo = FindAmmoForMagazine(magItem);
        if (magAmmo != null && magazine.Instance != null)
        {
            int cap = magItem.magazine.capacity;
            magazine.Instance.TryAddSupplyRounds(cap, magAmmo.id, cap);
        }
        else
            Debug.LogWarning($"{LogPrefix} No compatible ammo for mag '{magId}'.", body);

        if (!gun.TryAttachMagazine(magazine))
            Debug.LogWarning($"{LogPrefix} Mag attach failed '{gun.ItemId}' + '{magId}'.", body);
    }

    static string FirstAllowedMagazineId(ItemData gun)
    {
        if (gun?.gun?.magazines == null)
            return null;

        for (int i = 0; i < gun.gun.magazines.Count; i++)
        {
            GunMagazineGroup group = gun.gun.magazines[i];
            if (group?.magazines == null || group.magazines.Count == 0)
                continue;
            if (!string.IsNullOrEmpty(group.magazines[0]))
                return group.magazines[0];
        }

        return null;
    }

    static ItemData FindAmmoForMagazine(ItemData magazine)
    {
        return FindFirstItem(item =>
            item?.ammo != null && WeaponAmmoFit.AcceptsAmmoType(magazine, item));
    }

    static ItemData FindAmmoForGun(ItemData gun)
    {
        return FindFirstItem(item =>
            item?.ammo != null && WeaponAmmoFit.AcceptsGunAmmoType(gun, item));
    }

    static void DepositToBody(InventoryContainer bodyContainer, ItemStack stack, GameObject body)
    {
        if (stack?.Item == null || bodyContainer == null)
            return;

        if (!bodyContainer.TryAddStackReference(stack))
            Debug.LogWarning($"{LogPrefix} Body deposit failed '{stack.ItemId}'.", body);
    }

    static ItemData FindFirstItem(Func<ItemData, bool> match)
    {
        ItemData hit = FindFirstItem(GameplayData.GameItems, match);
        if (hit != null)
            return hit;
        return FindFirstItem(GameplayData.RefData, match);
    }

    static ItemData FindFirstItem(GameDatabase db, Func<ItemData, bool> match)
    {
        if (db?.Items == null || match == null)
            return null;

        for (int i = 0; i < db.Items.Count; i++)
        {
            ItemData item = db.Items[i];
            if (item != null && match(item))
                return item;
        }

        return null;
    }
}
