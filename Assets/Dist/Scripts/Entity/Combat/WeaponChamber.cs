// ============================================================
// WeaponChamber — 약실 SSOT 조회·장착 탄창 보급 1발 (Attack이 호출)
// ============================================================

using Garunnir.Runtime.Gameplay.Data;

/// <summary>
/// 약실은 ItemInstance. 탄창은 LoadedMagazine + SupplyRounds. clip_size는 클립 용량.
/// </summary>
public static class WeaponChamber
{
    public const int MagazineFedCapacity = 1;

    public static bool FeedsOnFire(WeaponAttack attack) =>
        attack == null || attack.FeedsChamberOnFire;

    public static int ChamberCapacity(ItemData item, bool hasMagazine)
    {
        if (hasMagazine)
            return MagazineFedCapacity;

        int clip = item?.gun != null ? item.gun.clip_size : 0;
        return clip > 0 ? clip : 0;
    }

    public static bool TryGetMagazine(ItemStack weapon, out ItemStack magazine)
    {
        magazine = weapon != null ? weapon.LoadedMagazine : null;
        return magazine?.Item?.magazine != null;
    }

    public static bool HasSupply(ItemStack magazine)
    {
        return magazine?.Instance != null && magazine.Instance.SupplyRounds > 0;
    }

    public static bool CanCommitFire(
        ItemData item,
        ItemInstance instance,
        ItemStack weapon,
        WeaponAttack attack)
    {
        if (item?.gun == null || instance == null)
            return false;
        if (instance.ChamberRounds > 0)
            return true;
        if (!FeedsOnFire(attack))
            return false;
        return TryGetMagazine(weapon, out ItemStack magazine) && HasSupply(magazine);
    }

    public static bool EnsureChamberForFire(
        ItemInstance instance,
        ItemStack weapon,
        ItemData item,
        WeaponAttack attack)
    {
        if (instance == null)
            return false;
        if (instance.ChamberRounds > 0)
            return true;
        if (!FeedsOnFire(attack))
            return false;
        return TryFeedFromMagazine(instance, weapon, item);
    }

    public static bool TryFeedFromMagazine(
        ItemInstance instance,
        ItemStack weapon,
        ItemData item)
    {
        if (instance == null || instance.ChamberRounds > 0)
            return instance != null && instance.ChamberRounds > 0;
        if (!TryGetMagazine(weapon, out ItemStack magazine))
            return false;

        int capacity = ChamberCapacity(item, hasMagazine: true);
        if (capacity <= 0 || instance.ChamberRounds >= capacity)
            return false;
        if (!TryTakeOneFromMagazine(magazine, out string ammoId))
            return false;
        return instance.TryAddChamberRound(capacity, ammoId);
    }

    public static bool TryConsume(ItemInstance instance) =>
        instance != null && instance.TryConsumeChamberRound();

    /// <summary>기어/어택이 호출. 탄창 보급 1발 → 약실. CombatLeaf.Reload 없음.</summary>
    public static bool TryReload(ItemInstance instance, ItemStack weapon, ItemData item) =>
        TryFeedFromMagazine(instance, weapon, item);

    /// <summary>약실에 기록된 탄, 없으면 장착 탄창 보급 탄.</summary>
    public static ItemData ResolveAmmo(ItemStack weapon, ItemInstance instance = null)
    {
        instance ??= weapon?.Instance;
        if (instance != null && !string.IsNullOrEmpty(instance.ChamberAmmoId))
        {
            ItemData loaded = GameplayData.GetItem(instance.ChamberAmmoId);
            if (loaded?.ammo != null)
                return loaded;
        }

        if (TryGetMagazine(weapon, out ItemStack magazine) &&
            magazine.Instance != null &&
            !string.IsNullOrEmpty(magazine.Instance.SupplyAmmoId))
        {
            ItemData magAmmo = GameplayData.GetItem(magazine.Instance.SupplyAmmoId);
            if (magAmmo?.ammo != null)
                return magAmmo;
        }

        return null;
    }

    public static int ResolvePierce(ItemStack weapon, ItemInstance instance = null)
    {
        ItemData ammo = ResolveAmmo(weapon, instance);
        int pierce = ammo?.ammo != null ? ammo.ammo.pierce : 0;
        return pierce > 0 ? pierce : 0;
    }

    static bool TryTakeOneFromMagazine(ItemStack magazine, out string ammoId)
    {
        ammoId = null;
        if (magazine?.Instance == null)
            return false;
        return magazine.Instance.TryTakeSupplyRounds(1, out ammoId) > 0;
    }
}
