// ============================================================
// WeaponAmmoService — 삽탄·장착·교체·분리·탄 빼기 (비컨테이너)
// ============================================================

using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;

/// <summary>
/// 컨텍스트 메뉴와 DnD가 호출. 탄창은 SupplyRounds, 총은 LoadedMagazine.
/// </summary>
public static class WeaponAmmoService
{
    public static string GetLoadBlockedReason(ItemStack ammo, ItemStack target)
    {
        if (ammo?.Item?.ammo == null || target?.Item == null)
            return WeaponAmmoLabels.Blocked;

        ItemStack mag = WeaponAmmoFit.ResolveLoadMagazine(target);
        if (mag != null)
        {
            if (!WeaponAmmoFit.AcceptsAmmoType(mag.Item, ammo.Item))
                return WeaponAmmoLabels.Blocked;
            if (mag.Instance != null &&
                mag.Instance.SupplyRounds > 0 &&
                !string.Equals(mag.Instance.SupplyAmmoId, ammo.ItemId, System.StringComparison.Ordinal))
                return WeaponAmmoLabels.Wrong;
            if (!WeaponAmmoFit.CanLoadMagazine(mag, ammo.Item))
                return WeaponAmmoLabels.Full;
            return null;
        }

        if (WeaponAmmoFit.IsClipFed(target.Item))
        {
            if (!WeaponAmmoFit.AcceptsGunAmmoType(target.Item, ammo.Item))
                return WeaponAmmoLabels.Blocked;
            if (target.Instance != null &&
                target.Instance.ChamberRounds > 0 &&
                !string.Equals(target.Instance.ChamberAmmoId, ammo.ItemId, System.StringComparison.Ordinal))
                return WeaponAmmoLabels.Wrong;
            if (!WeaponAmmoFit.CanLoadClip(target, ammo.Item))
                return WeaponAmmoLabels.Full;
            return null;
        }

        if (WeaponAmmoFit.IsToolAmmoHost(target.Item))
        {
            if (!WeaponAmmoFit.AcceptsToolAmmoType(target.Item, ammo.Item))
                return WeaponAmmoLabels.Blocked;
            if (target.Instance != null &&
                target.Instance.SupplyRounds > 0 &&
                !string.Equals(target.Instance.SupplyAmmoId, ammo.ItemId, System.StringComparison.Ordinal))
                return WeaponAmmoLabels.Wrong;
            if (!WeaponAmmoFit.CanLoadTool(target, ammo.Item))
                return WeaponAmmoLabels.Full;
            return null;
        }

        return WeaponAmmoLabels.Blocked;
    }

    public static string GetAttachBlockedReason(ItemStack magazine, ItemStack gun)
    {
        if (magazine?.Item?.magazine == null || gun?.Item?.gun == null)
            return WeaponAmmoLabels.Blocked;
        if (ReferenceEquals(gun.LoadedMagazine, magazine))
            return WeaponAmmoLabels.Blocked;
        if (!WeaponAmmoFit.AcceptsMagazine(gun.Item, magazine.Item))
            return WeaponAmmoLabels.Blocked;

        CharacterGearService gear = PlayerGearHost.Active?.Service;
        if (gun.LoadedMagazine != null && gear != null && !gear.CanDepositToBody(gun.LoadedMagazine))
            return WeaponAmmoLabels.NoRoom;

        return null;
    }

    public static string GetDetachBlockedReason(ItemStack gun)
    {
        if (gun?.LoadedMagazine == null)
            return WeaponAmmoLabels.Blocked;
        CharacterGearService gear = PlayerGearHost.Active?.Service;
        if (gear != null && !gear.CanDepositToBody(gun.LoadedMagazine))
            return WeaponAmmoLabels.NoRoom;
        return null;
    }

    public static string GetUnloadBlockedReason(ItemStack magazine, InventorySession session)
    {
        if (magazine?.Instance == null || magazine.Instance.SupplyRounds <= 0)
            return WeaponAmmoLabels.Blocked;

        ItemData ammo = GameplayData.GetItem(magazine.Instance.SupplyAmmoId);
        if (ammo == null)
            return WeaponAmmoLabels.Blocked;

        InventoryContainer dest = ResolveUnloadDest(magazine, session);
        if (dest == null)
            return WeaponAmmoLabels.NoRoom;

        ItemStack preview = new ItemStack(ammo, magazine.Instance.SupplyRounds);
        if (!dest.CapacityPolicy.CanAccept(dest, preview))
            return WeaponAmmoLabels.NoRoom;

        return null;
    }

    public static bool TryBeginLoad(ItemStack ammo, ItemStack target, InventorySession session)
    {
        if (GetLoadBlockedReason(ammo, target) != null)
            return false;

        CharacterGearService gear = PlayerGearHost.Active?.Service;
        if (gear == null)
            return false;

        ItemStack mag = WeaponAmmoFit.ResolveLoadMagazine(target);
        bool clip = mag == null && WeaponAmmoFit.IsClipFed(target.Item);
        bool tool = mag == null && !clip && WeaponAmmoFit.IsToolAmmoHost(target.Item);
        ItemStack active = clip || tool ? target : mag;
        if (target?.Item?.gun != null && target.LoadedMagazine != null)
            active = target;

        float duration = clip
            ? WeaponAmmoDuration.ClipLoadSeconds(target.Item)
            : tool
                ? WeaponAmmoDuration.ToolLoadSeconds(target.Item)
                : WeaponAmmoDuration.LoadSeconds(mag.Item);

        return gear.TryBeginDomainTimed(
            active,
            GearTimedAction.Kind.AmmoLoad,
            duration,
            () => ApplyLoad(ammo, target, session));
    }

    public static bool TryBeginAttach(ItemStack magazine, ItemStack gun, InventorySession session)
    {
        if (GetAttachBlockedReason(magazine, gun) != null)
            return false;

        CharacterGearService gear = PlayerGearHost.Active?.Service;
        if (gear == null)
            return false;

        return gear.TryBeginDomainTimed(
            gun,
            GearTimedAction.Kind.MagAttach,
            WeaponAmmoDuration.AttachSeconds(gun.Item, magazine.Item),
            () => ApplyAttach(magazine, gun, session));
    }

    public static bool TryBeginDetach(ItemStack gun, InventorySession session)
    {
        if (GetDetachBlockedReason(gun) != null)
            return false;

        CharacterGearService gear = PlayerGearHost.Active?.Service;
        if (gear == null)
            return false;

        ItemData magItem = gun.LoadedMagazine != null ? gun.LoadedMagazine.Item : null;
        return gear.TryBeginDomainTimed(
            gun,
            GearTimedAction.Kind.MagAttach,
            WeaponAmmoDuration.AttachSeconds(gun.Item, magItem),
            () => ApplyDetach(gun, session));
    }

    public static bool TryBeginUnload(ItemStack magazine, InventorySession session)
    {
        if (GetUnloadBlockedReason(magazine, session) != null)
            return false;

        CharacterGearService gear = PlayerGearHost.Active?.Service;
        if (gear == null)
            return false;

        ItemStack active = FindGunHoldingMagazine(magazine, session) ?? magazine;

        return gear.TryBeginDomainTimed(
            active,
            GearTimedAction.Kind.AmmoLoad,
            WeaponAmmoDuration.LoadSeconds(magazine.Item),
            () => ApplyUnload(magazine, session));
    }

    public static bool TryApplyDrop(ItemStack dragged, ItemStack target, InventorySession session)
    {
        if (dragged?.Item == null || target?.Item == null)
            return false;

        if (dragged.Item.ammo != null)
            return TryBeginLoad(dragged, target, session);

        if (dragged.Item.magazine != null && target.Item.gun != null)
            return TryBeginAttach(dragged, target, session);

        if (dragged.Item.gun != null && target.Item.magazine != null)
            return TryBeginAttach(target, dragged, session);

        return false;
    }

    public static void CollectReachableStacks(
        InventorySession session,
        CharacterGearService gear,
        List<ItemStack> dest)
    {
        dest.Clear();
        if (session != null)
        {
            IReadOnlyList<InventoryContainer> containers = session.GetSidebarContainers();
            for (int c = 0; c < containers.Count; c++)
            {
                InventoryContainer container = containers[c];
                if (container == null)
                    continue;
                IReadOnlyList<ItemStack> stacks = container.Stacks;
                for (int i = 0; i < stacks.Count; i++)
                {
                    if (stacks[i] != null)
                        dest.Add(stacks[i]);
                }
            }
        }

        if (gear?.Wield == null)
            return;

        AddUnique(dest, gear.Wield.Left);
        if (!ReferenceEquals(gear.Wield.Left, gear.Wield.Right))
            AddUnique(dest, gear.Wield.Right);
    }

    static void AddUnique(List<ItemStack> dest, ItemStack stack)
    {
        if (stack == null)
            return;
        for (int i = 0; i < dest.Count; i++)
        {
            if (ReferenceEquals(dest[i], stack))
                return;
        }

        dest.Add(stack);
    }

    static void ApplyLoad(ItemStack ammo, ItemStack target, InventorySession session)
    {
        if (GetLoadBlockedReason(ammo, target) != null)
            return;

        InventoryContainer ammoOwner = FindOwner(session, ammo);
        if (ammoOwner == null)
            return;

        ItemStack mag = WeaponAmmoFit.ResolveLoadMagazine(target);
        int added;
        ItemData ammoItem = ammo.Item;
        if (mag != null)
        {
            int room = mag.Item.magazine.capacity - mag.Instance.SupplyRounds;
            int want = ammo.Count < room ? ammo.Count : room;
            int taken = ammoOwner.TryTakeFromStack(ammo, want);
            added = mag.Instance.TryAddSupplyRounds(taken, ammoItem.id, mag.Item.magazine.capacity);
            Refund(ammoOwner, ammoItem, taken - added);
        }
        else if (WeaponAmmoFit.IsToolAmmoHost(target.Item))
        {
            int capacity = WeaponAmmoFit.ResolveToolCapacity(target.Item);
            int room = capacity - target.Instance.SupplyRounds;
            int want = ammo.Count < room ? ammo.Count : room;
            int taken = ammoOwner.TryTakeFromStack(ammo, want);
            added = target.Instance.TryAddSupplyRounds(taken, ammoItem.id, capacity);
            Refund(ammoOwner, ammoItem, taken - added);
        }
        else
        {
            int room = target.Item.gun.clip_size - target.Instance.ChamberRounds;
            int want = ammo.Count < room ? ammo.Count : room;
            int taken = ammoOwner.TryTakeFromStack(ammo, want);
            added = 0;
            for (int i = 0; i < taken; i++)
            {
                if (!target.Instance.TryAddChamberRound(target.Item.gun.clip_size, ammoItem.id))
                    break;
                added++;
            }

            Refund(ammoOwner, ammoItem, taken - added);
        }

        if (added <= 0)
            return;

        NotifyMutation(session, ammoOwner, FindOwner(session, target));
    }

    static void ApplyAttach(ItemStack magazine, ItemStack gun, InventorySession session)
    {
        if (GetAttachBlockedReason(magazine, gun) != null)
            return;

        CharacterGearService gear = PlayerGearHost.Active?.Service;
        if (!TryExtractMagazine(magazine, session, gear, out ItemStack toAttach, out InventoryContainer magOwner))
            return;

        ItemStack previous = gun.LoadedMagazine;
        if (previous != null)
            gun.DetachMagazine();

        if (!gun.TryAttachMagazine(toAttach))
        {
            RestoreExtracted(toAttach, magOwner, gear);
            if (previous != null)
                gun.TryAttachMagazine(previous);
            NotifyMutation(session, magOwner, FindOwner(session, gun));
            return;
        }

        if (previous != null)
            gear?.DepositToBody(previous);

        NotifyMutation(session, magOwner, FindOwner(session, gun));
    }

    static void ApplyDetach(ItemStack gun, InventorySession session)
    {
        if (GetDetachBlockedReason(gun) != null)
            return;

        ItemStack mag = gun.DetachMagazine();
        if (mag == null)
            return;

        PlayerGearHost.Active?.Service?.DepositToBody(mag);
        NotifyMutation(session, FindOwner(session, gun), null);
    }

    static void ApplyUnload(ItemStack magazine, InventorySession session)
    {
        if (GetUnloadBlockedReason(magazine, session) != null)
            return;

        int taken = magazine.Instance.TryTakeSupplyRounds(
            magazine.Instance.SupplyRounds,
            out string ammoId);
        if (taken <= 0 || string.IsNullOrEmpty(ammoId))
            return;

        ItemData ammo = GameplayData.GetItem(ammoId);
        InventoryContainer dest = ResolveUnloadDest(magazine, session);
        if (ammo != null && dest != null)
            dest.AddItem(ammo, taken);

        ItemStack gun = FindGunHoldingMagazine(magazine, session);
        NotifyMutation(session, dest, FindOwner(session, gun));
    }

    static ItemStack FindGunHoldingMagazine(ItemStack magazine, InventorySession session)
    {
        if (magazine == null)
            return null;

        CharacterGearService gear = PlayerGearHost.Active?.Service;
        if (gear?.Wield != null)
        {
            if (gear.Wield.Left?.LoadedMagazine == magazine)
                return gear.Wield.Left;
            if (gear.Wield.Right?.LoadedMagazine == magazine)
                return gear.Wield.Right;
        }

        if (session == null)
            return null;

        IReadOnlyList<InventoryContainer> containers = session.GetSidebarContainers();
        for (int c = 0; c < containers.Count; c++)
        {
            InventoryContainer container = containers[c];
            if (container == null)
                continue;
            IReadOnlyList<ItemStack> stacks = container.Stacks;
            for (int i = 0; i < stacks.Count; i++)
            {
                ItemStack stack = stacks[i];
                if (stack?.LoadedMagazine == magazine)
                    return stack;
            }
        }

        return null;
    }

    static bool TryExtractMagazine(
        ItemStack magazine,
        InventorySession session,
        CharacterGearService gear,
        out ItemStack extracted,
        out InventoryContainer owner)
    {
        extracted = null;
        owner = FindOwner(session, magazine);
        if (owner != null && owner.ContainsStackReference(magazine))
        {
            if (magazine.Count > 1)
            {
                magazine.SetCount(magazine.Count - 1);
                extracted = new ItemStack(magazine.Item, 1, magazine.DamageLevel);
                CopySupply(magazine, extracted);
                owner.NotifyContentsChanged();
                return true;
            }

            if (!owner.TryRemoveStackReference(magazine))
                return false;

            extracted = magazine;
            return true;
        }

        if (gear?.Wield != null &&
            gear.Wield.Contains(magazine) &&
            gear.Wield.TryUnwield(magazine, out ItemStack removed) &&
            removed != null)
        {
            extracted = removed;
            return true;
        }

        return false;
    }

    static void CopySupply(ItemStack source, ItemStack dest)
    {
        if (source?.Instance == null || dest?.Instance == null || source.Item?.magazine == null)
            return;
        if (source.Instance.SupplyRounds <= 0 || string.IsNullOrEmpty(source.Instance.SupplyAmmoId))
            return;

        dest.Instance.TryAddSupplyRounds(
            source.Instance.SupplyRounds,
            source.Instance.SupplyAmmoId,
            source.Item.magazine.capacity);
    }

    static void RestoreExtracted(ItemStack stack, InventoryContainer owner, CharacterGearService gear)
    {
        if (stack == null)
            return;
        if (owner != null && owner.TryAddStackReference(stack))
            return;
        gear?.DepositToBody(stack);
    }

    static void Refund(InventoryContainer dest, ItemData item, int count)
    {
        if (dest == null || item == null || count <= 0)
            return;
        dest.AddItem(item, count);
    }

    static InventoryContainer ResolveUnloadDest(ItemStack magazine, InventorySession session)
    {
        InventoryContainer owner = FindOwner(session, magazine);
        if (owner != null)
            return owner;

        CharacterGearService gear = PlayerGearHost.Active?.Service;
        if (gear?.Wield != null)
        {
            ItemStack gun = null;
            if (gear.Wield.Left?.LoadedMagazine == magazine)
                gun = gear.Wield.Left;
            else if (gear.Wield.Right?.LoadedMagazine == magazine)
                gun = gear.Wield.Right;
            if (gun != null)
            {
                InventoryContainer gunOwner = FindOwner(session, gun);
                if (gunOwner != null)
                    return gunOwner;
            }
        }

        return PlayerInventoryRuntime.Active?.Host?.Container;
    }

    static InventoryContainer FindOwner(InventorySession session, ItemStack stack)
    {
        if (session != null && session.TryFindOwner(stack, out InventoryContainer owner))
            return owner;
        return null;
    }

    static void NotifyMutation(
        InventorySession session,
        InventoryContainer first,
        InventoryContainer second)
    {
        if (session == null)
            session = PlayerInventoryRuntime.Active?.Session;

        if (session != null)
        {
            if (first != null && second != null && !ReferenceEquals(first, second))
                session.NotifyExternalStacksChanged(first, second);
            else if (first != null)
                session.NotifyExternalStacksChanged(first);
            else if (second != null)
                session.NotifyExternalStacksChanged(second);
            else
                session.NotifyExternalStacksChanged();
        }

        PlayerGearHost.Active?.Service?.NotifyAmmoChanged();
    }
}
