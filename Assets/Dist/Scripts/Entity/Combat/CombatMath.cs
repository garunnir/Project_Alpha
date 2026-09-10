// ============================================================
// CombatMath — ItemData·스탯 기반 데미지/공속/명중 가산 파이프
// ============================================================

using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

/// <summary>
/// 전투 수치 SSOT. CharacterAttacker는 입력·쿨·타겟만 담당하고 식은 여기만.
/// mutation/무술 등은 같은 파이프에 가산만 얹는다 (클래스 상속 금지).
/// </summary>
public static class CombatMath
{
    /// <summary>BN moves → 실시간 초. 100 moves = 1초.</summary>
    public const float MovesPerSecond = 100f;

    /// <summary>근접 기본 사거리 (월드 미터). ItemData에 melee range 필드 없음.</summary>
    public const float MeleeReachMeters = 1.25f;

    const int UnarmedBaseDamage = 1;
    /// <summary>근접 피해 STR 가산·충격량 StrengthSwing 기준. Impulse는 이 값을 복제하지 않음.</summary>
    public const int StrengthBaseline = 8;
    const float MeleeHitBase = 0.55f;
    const float ToHitPerPoint = 0.04f;
    const float SkillHitPerLevel = 0.01f;
    const float GunDispersionScale = 1000f;
    const int MeleeMovesBase = 65;
    const int MeleeMovesVolumeDiv = 250;
    const int MeleeMovesWeightDiv = 60;
    const int GunFireMoves = 100;
    const int PracticeXpPerAttack = 4;
    const float GunDispersionFallback = 300f;
    /// <summary>effective 이 값이면 yaw 1°. 300 → 5°.</summary>
    const float DispersionUnitsPerDegree = 60f;
    /// <summary>aim_speed 이 값이면 조임 1초.</summary>
    const float AimSpeedProgressDivisor = 10f;
    /// <summary>handling 0이면 킥 배율 1. handling=이 값이면 킥 0.5.</summary>
    const float HandlingRef = 10f;

    static readonly string[] ExcavateChannelScratch = new string[AttackDamageTags.MaxChannels];

    public static string SkillId(ItemData item, CombatLeaf action)
    {
        if (CombatLeafUtil.IsRanged(action))
            return CombatSkillIds.Gun;

        if (item == null)
            return CombatSkillIds.Unarmed;

        return CombatSkillIds.Melee;
    }

    public static int PracticeXp(CombatLeaf action) => PracticeXpPerAttack;

    public static float RangeMeters(ItemData item, CombatLeaf action, ItemData ammo = null)
    {
        if (CombatLeafUtil.IsRanged(action))
        {
            int gunRange = item?.gun != null ? item.gun.range : 0;
            int ammoRange = ammo?.ammo != null ? ammo.ammo.range : 0;
            int range = gunRange + ammoRange;
            return range > 0 ? range : MeleeReachMeters;
        }

        return MeleeReachMeters;
    }

    public static int AttackMoves(ItemData item, CombatLeaf action)
    {
        if (CombatLeafUtil.IsRanged(action))
            return GunFireMoves;

        int weight = item != null ? Mathf.Max(0, item.weight_g) : 0;
        int volume = item != null ? Mathf.Max(0, item.volume_ml) : 0;
        return MeleeMovesBase
            + volume / MeleeMovesVolumeDiv
            + weight / MeleeMovesWeightDiv;
    }

    public static float AttackIntervalSeconds(ItemData item, CombatLeaf action) =>
        AttackMoves(item, action) / MovesPerSecond;

    /// <summary>반동 수치(총+탄). handling 전 킥.</summary>
    public static int RecoilUnits(ItemData item, ItemData ammo)
    {
        int recoil = 0;
        if (item?.gun != null)
            recoil += Mathf.Max(0, item.gun.recoil);
        if (ammo?.ammo != null)
            recoil += Mathf.Max(0, ammo.ammo.recoil);
        return recoil;
    }

    /// <summary>킥 배율. handling 0이면 1.</summary>
    public static float HandlingKickFactor(ItemData item)
    {
        int handling = item?.gun != null ? Mathf.Max(0, item.gun.handling) : 0;
        if (handling <= 0)
            return 1f;
        return HandlingRef / (HandlingRef + handling);
    }

    public static float RecoilKickUnits(ItemData item, ItemData ammo) =>
        RecoilUnits(item, ammo) * HandlingKickFactor(item);

    /// <summary>recoilRemaining 천장 = 킥 × 이 배수 (풀 스프레이 블룸).</summary>
    public const float RecoilRemainingMaxKicks = 4f;

    /// <summary>분산용 반동 잔여 상한. 킥 0이면 0.</summary>
    public static float RecoilRemainingMax(ItemData item, ItemData ammo) =>
        RecoilKickUnits(item, ammo) * RecoilRemainingMaxKicks;

    /// <summary>
    /// 천장에 가까울수록 킥이 줄어드는 가산 (점근). next = remaining + kick*(1 - remaining/cap).
    /// </summary>
    public static float ApplyRecoilKick(float remaining, float kick, float cap)
    {
        if (kick <= 0f)
            return Mathf.Max(0f, remaining);
        if (cap <= 0f)
            return remaining + kick;

        float t = Mathf.Clamp01(remaining / cap);
        float next = remaining + kick * (1f - t);
        return Mathf.Min(next, cap);
    }

    /// <summary>
    /// 지수 감쇠 기준 잔여. remaining이 이 값일 때 초당 감소량이 옛 선형 MovesPerSecond와 같음.
    /// </summary>
    public const float RecoilRecoverRefUnits = 100f;

    /// <summary>지수 감쇠 λ(1/초). remaining *= exp(-λ·dt).</summary>
    public static float RecoilRecoverLambda =>
        MovesPerSecond / RecoilRecoverRefUnits;

    /// <summary>반동 잔여 지수 감쇠. 미소값은 0으로 스냅.</summary>
    public static float DecayRecoilRemaining(float remaining, float dt)
    {
        if (remaining <= 0f || dt <= 0f)
            return 0f;

        float next = remaining * Mathf.Exp(-RecoilRecoverLambda * dt);
        const float SnapEpsilon = 0.01f;
        return next < SnapEpsilon ? 0f : next;
    }

    /// <summary>aim_speed 0이면 호출측이 aim01=1. 양수면 초당 조임.</summary>
    public static float AimProgressPerSecond(int aimSpeed)
    {
        if (aimSpeed <= 0)
            return 0f;
        return aimSpeed / AimSpeedProgressDivisor;
    }

    public static int AimSpeedOf(ItemData item) =>
        item?.gun != null ? Mathf.Max(0, item.gun.aim_speed) : 0;

    public static float EffectiveDispersion(
        ItemData item,
        ItemData ammo,
        float recoilRemaining,
        float aim01)
    {
        float dispersion;
        if (item?.gun != null)
        {
            dispersion = item.gun.dispersion;
            float sight = Mathf.Max(0, item.gun.sight_dispersion);
            dispersion += sight * (1f - Mathf.Clamp01(aim01));
        }
        else
        {
            dispersion = GunDispersionFallback;
        }

        if (ammo?.ammo != null)
        {
            dispersion += ammo.ammo.dispersion;
            dispersion += Mathf.Max(0, ammo.ammo.shot_spread);
        }

        return dispersion + Mathf.Max(0f, recoilRemaining);
    }

    public static float DispersionYawDegrees(float effectiveDispersion) =>
        Mathf.Max(0f, effectiveDispersion) / DispersionUnitsPerDegree;

    public static Vector3 SpreadFireDirection(Vector3 direction, float effectiveDispersion)
    {
        Vector3 dir = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.forward;
        float degrees = DispersionYawDegrees(effectiveDispersion);
        if (degrees <= 0f)
            return dir;

        float yaw = (UnityEngine.Random.value * 2f - 1f) * degrees;
        return Quaternion.AngleAxis(yaw, Vector3.up) * dir;
    }

    /// <summary>
    /// 조준 거리에서 ±yaw 월드점을 화면에 투영한 반폭(픽셀). UI 탄퍼짐 포인터용.
    /// </summary>
    public static float ProjectYawHalfWidthPixels(
        Camera cam,
        Vector3 origin,
        Vector3 fireDir,
        float rangeMeters,
        float effectiveDispersion)
    {
        if (cam == null || rangeMeters <= 1e-4f)
            return 0f;

        float degrees = DispersionYawDegrees(effectiveDispersion);
        if (degrees <= 1e-4f)
            return 0f;

        Vector3 dir = fireDir.sqrMagnitude > 1e-8f ? fireDir.normalized : Vector3.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-8f)
            return 0f;
        dir.Normalize();

        Vector3 center = origin + dir * rangeMeters;
        Vector3 left = origin + (Quaternion.AngleAxis(-degrees, Vector3.up) * dir) * rangeMeters;
        Vector3 right = origin + (Quaternion.AngleAxis(degrees, Vector3.up) * dir) * rangeMeters;

        Vector3 screenCenter = cam.WorldToScreenPoint(center);
        if (screenCenter.z <= 0f)
            return 0f;

        Vector3 screenLeft = cam.WorldToScreenPoint(left);
        Vector3 screenRight = cam.WorldToScreenPoint(right);
        if (screenLeft.z <= 0f || screenRight.z <= 0f)
            return 0f;

        float half = 0.5f * Vector2.Distance(
            new Vector2(screenLeft.x, screenLeft.y),
            new Vector2(screenRight.x, screenRight.y));
        return half > 0f ? half : 0f;
    }

    public static int Damage(
        ItemData item,
        WeaponAttack attack,
        CombatLeaf action,
        int strength,
        int skillLevel,
        ItemData ammo = null)
    {
        _ = attack;
        string[] channels = new string[AttackDamageTags.MaxChannels];
        int n = AttackDamageTags.WriteChannels(item, action, channels, ammo);
        int total = 0;
        for (int i = 0; i < n; i++)
            total += DamageForTag(item, channels[i], strength, skillLevel, ammo);
        return total;
    }

    /// <summary>채널 한 줄 피해. 원거리는 탄 양 + 총 가산. 한 타 합산은 Damage(…).</summary>
    public static int DamageForTag(
        ItemData item,
        string damageTag,
        int strength,
        int skillLevel,
        ItemData ammo = null)
    {
        if (item?.gun != null)
        {
            int ammoDmg = ammo?.ammo != null ? ammo.ammo.damage : 0;
            int gunDmg = item.gun.ranged_damage;
            return Mathf.Max(0, ammoDmg + gunDmg + skillLevel / 2);
        }

        if (item == null)
        {
            return Mathf.Max(
                UnarmedBaseDamage,
                strength / 2 + skillLevel / 2);
        }

        int baseDmg = string.Equals(damageTag, AttackDamageTags.Cut, System.StringComparison.Ordinal)
            ? item.cutting
            : item.bashing;
        int strBonus = Mathf.Max(0, (strength - StrengthBaseline) / 2);
        return Mathf.Max(0, baseDmg + strBonus + skillLevel / 4);
    }

    public static string SkillIdForTag(ItemData item, string damageTag)
    {
        _ = damageTag;
        if (item?.gun != null)
            return CombatSkillIds.Gun;

        if (item == null)
            return CombatSkillIds.Unarmed;

        return CombatSkillIds.Melee;
    }

    /// <summary>원거리 확정 히트에서 조준 부위 유지 확률. 실패는 인접 산란·피해 유지. 근접 연결은 미사용.</summary>
    public static float HitChance(
        ItemData item,
        CombatLeaf action,
        int skillLevel,
        string aimedPartId,
        ItemData ammo = null,
        float rangedEffectiveDispersion = -1f)
    {
        float chance;
        if (CombatLeafUtil.IsRanged(action))
        {
            float effective = rangedEffectiveDispersion >= 0f
                ? rangedEffectiveDispersion
                : EffectiveDispersion(item, ammo, 0f, 1f);
            chance = Mathf.Clamp01(1f - effective / GunDispersionScale)
                + skillLevel * SkillHitPerLevel;
        }
        else
        {
            int toHit = item != null ? item.to_hit : 0;
            chance = MeleeHitBase
                + toHit * ToHitPerPoint
                + skillLevel * SkillHitPerLevel;
        }

        return Mathf.Clamp01(chance * BodyPartHitDifficulty.Get(aimedPartId));
    }

    /// <summary>약실에 1발 이상. 메거진 보급·자동 장전은 WeaponChamber.</summary>
    public static bool CanFireGun(ItemData item, ItemInstance instance) =>
        item?.gun != null && instance != null && instance.ChamberRounds > 0;

    /// <summary>
    /// 구조물 cue 피해 — 채널(bash/cut/bullet) × 재질 resist + optional tool potency(첫 채널).
    /// Dig/Chop/벽 HP 공용. Dig 게이트(품질≥1)는 호출부가 담당.
    /// </summary>
    public static int ResolveStructureDamage(
        ItemData item,
        CombatLeaf action,
        int strength,
        int skillLevel,
        IReadOnlyList<string> targetMaterials,
        int targetMaterialThickness,
        float offenseFactor = 1f,
        ItemData ammo = null,
        int toolPotencyBonus = 0)
    {
        int channelCount = AttackDamageTags.WriteChannels(
            item,
            action,
            ExcavateChannelScratch,
            ammo);
        if (channelCount <= 0)
            return 0;

        float factor = Mathf.Max(0f, offenseFactor);
        int total = 0;
        for (int i = 0; i < channelCount; i++)
        {
            string damageTag = ExcavateChannelScratch[i];
            int raw = DamageForTag(item, damageTag, strength, skillLevel, ammo);
            if (i == 0 && toolPotencyBonus > 0)
                raw += toolPotencyBonus;
            raw = Mathf.Max(0, Mathf.RoundToInt(raw * factor));
            total += WearCombatDefense.MitigateStructureDamage(
                targetMaterials,
                targetMaterialThickness,
                raw,
                damageTag);
        }

        return total;
    }

    /// <summary>
    /// Excavate cue 피해 — DIG potency 게이트 + <see cref="ResolveStructureDamage"/>.
    /// <paramref name="digPotencyLevel"/>는 <see cref="MapPlantService.ResolveDigQualityLevel"/> 결과.
    /// 타일 내구(<see cref="MapDigConsts.DefaultDigDurability"/> / TileDefinition.breakDurability)와
    /// cue 횟수 패리티를 맞춘다.
    /// </summary>
    public static int ResolveExcavateDamage(
        ItemData item,
        CombatLeaf action,
        int digPotencyLevel,
        int strength,
        int skillLevel,
        IReadOnlyList<string> targetMaterials,
        int targetMaterialThickness,
        float offenseFactor = 1f,
        ItemData ammo = null)
    {
        if (digPotencyLevel < 1)
            return 0;

        return ResolveStructureDamage(
            item,
            action,
            strength,
            skillLevel,
            targetMaterials,
            targetMaterialThickness,
            offenseFactor,
            ammo,
            digPotencyLevel);
    }
}
