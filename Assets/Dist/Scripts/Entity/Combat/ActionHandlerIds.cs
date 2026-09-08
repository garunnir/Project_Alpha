// ============================================================
// ActionHandlerIds — IActionHandler logicId / 데미지 채널(ItemData) SSOT
// ============================================================

using System;
using Garunnir.Runtime.Gameplay.Data;

public static class ActionHandlerIds
{
    public const string MeleeHit = "melee_hit";
    public const string SpawnProjectile = "spawn_projectile";
    public const string RaiseGuard = "raise_guard";
    public const string MeleeBlockTarget = "melee_block_target";
    /// <summary>레거시 직렬화 id — <see cref="MeleeBlockTarget"/>로 대체.</summary>
    public const string MapDigBreak = "map_dig_break";
    public const string MeleePlantTarget = "melee_plant_target";

    public static string DefaultFor(CombatLeaf leaf)
    {
        leaf = CombatLeafUtil.Normalize(leaf);
        if (leaf == CombatLeaf.Excavate)
            return MeleeBlockTarget;
        if (leaf == CombatLeaf.Chop)
            return MeleePlantTarget;

        switch (CombatLeafUtil.ToAnimVerb(leaf))
        {
            case AnimVerb.Trigger:
                return SpawnProjectile;
            case AnimVerb.Raise:
                return RaiseGuard;
            default:
                return MeleeHit;
        }
    }
}

/// <summary>
/// 특성 채널. 계산기·Hit 연출이 같은 키를 쓴다. Action이 고르지 않음.
/// Trigger는 탄 damage_type (없으면 bullet). 근접은 ItemData에 양이 있는 채널(0..N).
/// </summary>
public static class AttackDamageTags
{
    public const string Bash = "bash";
    public const string Cut = "cut";
    public const string Bullet = "bullet";
    public const int MaxChannels = 3;

    /// <summary>아이템·동사 없을 때 최후 폴백.</summary>
    public static string Fallback => Bash;

    /// <summary>첫 채널. Practice·단일 키용. 한 타 합산은 WriteChannels.</summary>
    public static string Resolve(ItemData item, CombatLeaf action, ItemData ammo = null)
    {
        string[] scratch = ChannelScratch;
        int n = WriteChannels(item, action, scratch, ammo);
        return n > 0 ? scratch[0] : Fallback;
    }

    /// <summary>
    /// Trigger → 탄 특성 1개. 그 외 cutting&gt;0이면 cut, bashing&gt;0(또는 비무장)이면 bash.
    /// </summary>
    public static int WriteChannels(
        ItemData item,
        CombatLeaf action,
        string[] dest,
        ItemData ammo = null)
    {
        if (dest == null || dest.Length == 0)
            return 0;

        if (CombatLeafUtil.IsRanged(action))
        {
            dest[0] = FromAmmoDamageType(ammo);
            return 1;
        }

        int n = 0;
        if (item != null && item.cutting > 0 && n < dest.Length)
            dest[n++] = Cut;
        if ((item == null || item.bashing > 0) && n < dest.Length)
            dest[n++] = Bash;
        if (n == 0)
            dest[n++] = Fallback;
        return n;
    }

    /// <summary>BN damage_type → Dist 채널. 미지 원거리 타입은 bullet.</summary>
    public static string FromAmmoDamageType(ItemData ammo)
    {
        string raw = ammo?.ammo != null ? ammo.ammo.damage_type : null;
        if (string.IsNullOrEmpty(raw))
            return Bullet;

        if (string.Equals(raw, Bullet, StringComparison.OrdinalIgnoreCase))
            return Bullet;
        if (string.Equals(raw, Bash, StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "bashing", StringComparison.OrdinalIgnoreCase))
            return Bash;
        if (string.Equals(raw, Cut, StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "cutting", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "stab", StringComparison.OrdinalIgnoreCase))
            return Cut;

        return Bullet;
    }

    static readonly string[] ChannelScratch = new string[MaxChannels];
}

/// <summary>Hit 테이블에 행이 없을 때.</summary>
public static class AttackImpactTags
{
    public const string Fallback = "fallback";
}

public static class ActionHandlerRegistry
{
    static readonly MeleeHitHandler Melee = new MeleeHitHandler();
    static readonly SpawnProjectileHandler Projectile = new SpawnProjectileHandler();
    static readonly RaiseGuardHandler Raise = new RaiseGuardHandler();
    static readonly MeleeBlockTargetHandler MeleeBlock = new MeleeBlockTargetHandler();
    static readonly MeleePlantTargetHandler MeleePlant = new MeleePlantTargetHandler();

    public static bool TryGet(string logicId, out IActionHandler handler)
    {
        handler = null;
        if (string.Equals(logicId, ActionHandlerIds.MeleeHit, StringComparison.Ordinal))
        {
            handler = Melee;
            return true;
        }

        if (string.Equals(logicId, ActionHandlerIds.SpawnProjectile, StringComparison.Ordinal))
        {
            handler = Projectile;
            return true;
        }

        if (string.Equals(logicId, ActionHandlerIds.RaiseGuard, StringComparison.Ordinal))
        {
            handler = Raise;
            return true;
        }

        if (string.Equals(logicId, ActionHandlerIds.MeleeBlockTarget, StringComparison.Ordinal)
            || string.Equals(logicId, ActionHandlerIds.MapDigBreak, StringComparison.Ordinal))
        {
            handler = MeleeBlock;
            return true;
        }

        if (string.Equals(logicId, ActionHandlerIds.MeleePlantTarget, StringComparison.Ordinal))
        {
            handler = MeleePlant;
            return true;
        }

        return false;
    }

    public static IActionHandler Resolve(WeaponAttack attack, CombatLeaf action)
    {
        string logicId = attack != null && !string.IsNullOrEmpty(attack.LogicId)
            ? attack.LogicId
            : ActionHandlerIds.DefaultFor(action);
        TryGet(logicId, out IActionHandler handler);
        return handler;
    }
}
