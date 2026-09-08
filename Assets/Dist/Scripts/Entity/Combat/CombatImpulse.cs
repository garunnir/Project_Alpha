// ============================================================
// CombatImpulse — 밀침 J / Δv SSOT (hp 데미지와 분리)
// ============================================================

using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

/// <summary>
/// 근접 J = m_weapon × StrengthSwing(str) / T.
/// 원거리 J_shot = RecoilUnits × RecoilToImpulse.
/// J_hit = 이 몸에 남는 J (다음 몸으로 안 가면 J_in 전부).
/// 피해자 Δv = (J_hit / m) × VictimDeltaVScale. 문서: docs/locomotion/LOCOMOTION.md.
/// </summary>
public static class CombatImpulse
{
    /// <summary>BN recoil 정수 → J_shot.</summary>
    public const float RecoilToImpulse = 0.05f;

    /// <summary>STR=StrengthBaseline 일 때 StrengthSwing.</summary>
    public const float StrengthSwingAtBaseline = 1f;

    /// <summary>비무장 휘두름 질량 (kg).</summary>
    public const float UnarmedMassKg = 0.4f;

    /// <summary>Appearance 없을 때 몸 질량.</summary>
    public const float FallbackBodyMassKg = 70f;

    /// <summary>나눗셈 하한.</summary>
    public const float MinInertialMassKg = 5f;

    /// <summary>이 Δv 이상이면 Stagger.</summary>
    public const float StaggerDeltaV = 1.2f;

    /// <summary>Stagger 이동 잠금 초 (레거시). 이동 계약은 CombatImbalance 이속 배율을 쓴다.</summary>
    public const float StaggerSeconds = 0.45f;

    /// <summary>넉백 속도 감쇠 / 초.</summary>
    public const float KnockbackDecayPerSecond = 8f;

    /// <summary>피해자 J/m → 모터 Δv. 사수 분산 킥에는 안 곱함.</summary>
    public const float VictimDeltaVScale = 16f;

    /// <summary>사수 Δv → 기존 분산 킥 단위. remaining += Δv × 이 값.</summary>
    public const float KickToDispersionPerDeltaV = 1400f;

    public const string TechniqueBrutal = "BRUTAL";
    public const string TechniqueSweep = "SWEEP";
    public const string EffectBeanbag = "BEANBAG";

    /// <summary>BRUTAL 기법: hp raw 배율. J는 그대로.</summary>
    public const float BrutalHpFactor = 1.25f;

    /// <summary>SWEEP 기법: 근접 J 배율.</summary>
    public const float SweepJinFactor = 1.35f;

    /// <summary>이 이하면 오버펜 중단.</summary>
    public const float MinContinueJin = 0.001f;

    public static float StrengthSwing(int strength)
    {
        int baseline = CombatMath.StrengthBaseline;
        if (baseline <= 0)
            return StrengthSwingAtBaseline;
        return StrengthSwingAtBaseline * (strength / (float)baseline);
    }

    public static float WeaponMassKg(ItemData item)
    {
        if (item == null)
            return UnarmedMassKg;
        float kg = item.Weight;
        return kg > 0f ? kg : UnarmedMassKg;
    }

    public static float MeleeJin(ItemData item, CombatLeaf action, int strength)
    {
        float t = CombatMath.AttackIntervalSeconds(item, action);
        if (t < 0.01f)
            t = 0.01f;
        float v = StrengthSwing(strength) / t;
        return WeaponMassKg(item) * v * MeleeJinFactor(item);
    }

    public static float MeleeJinFactor(ItemData item) =>
        HasTechnique(item, TechniqueSweep) ? SweepJinFactor : 1f;

    public static float HpFactor(ItemData item) =>
        HasTechnique(item, TechniqueBrutal) ? BrutalHpFactor : 1f;

    public static bool HasTechnique(ItemData item, string technique)
    {
        if (item?.techniques == null || string.IsNullOrEmpty(technique))
            return false;
        for (int i = 0; i < item.techniques.Count; i++)
        {
            if (string.Equals(item.techniques[i], technique, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static bool IsBeanbag(ItemData ammo)
    {
        if (ammo == null)
            return false;
        if (!string.IsNullOrEmpty(ammo.id) &&
            ammo.id.IndexOf("beanbag", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        List<string> effects = ammo.ammo?.effects;
        if (effects == null)
            return false;
        for (int i = 0; i < effects.Count; i++)
        {
            if (string.Equals(effects[i], EffectBeanbag, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static int ArmorPen(ItemData ammo)
    {
        if (ammo?.ammo == null)
            return 0;
        return Mathf.Max(0, ammo.ammo.pierce);
    }

    /// <summary>다음 타깃으로 넘어가는 J. 오버펜 중에만 p 사용.</summary>
    public static float ExitJin(float jin, float penetration01) =>
        jin * Mathf.Clamp01(penetration01);

    public static float ShotJin(ItemData gun, ItemData ammo)
    {
        return CombatMath.RecoilUnits(gun, ammo) * RecoilToImpulse;
    }

    /// <summary>오버펜 분할용. 밀침 직접 배율 아님.</summary>
    public static float Penetration01(int hp, int raw)
    {
        if (raw <= 0)
            return 0f;
        return Mathf.Clamp01(hp / (float)raw);
    }

    public static float Transferred(float jin, float penetration01) =>
        jin * (1f - Mathf.Clamp01(penetration01));

    /// <summary>이 몸에 남는 J. 다음 몸으로 안 나가면 J_in 전부.</summary>
    public static float HitJin(float jinIn, bool continues, float penetration01)
    {
        if (jinIn <= 0f)
            return 0f;
        if (!continues)
            return jinIn;
        return Transferred(jinIn, penetration01);
    }

    public static float DeltaV(float j, float massKg)
    {
        float m = Mathf.Max(MinInertialMassKg, massKg);
        return j / m;
    }

    public static float VictimDeltaV(float jHit, float massKg) =>
        DeltaV(jHit, massKg) * VictimDeltaVScale;

    public static float InertialMassKg(
        CharacterAppearanceHost appearance,
        EquipmentWearState wear,
        WieldSlots wield)
    {
        float mass = appearance != null && appearance.RemainingMassKg > 0.01f
            ? appearance.RemainingMassKg
            : FallbackBodyMassKg;
        mass += GearMassKg(wear, wield);
        return Mathf.Max(MinInertialMassKg, mass);
    }

    public static float GearMassKg(EquipmentWearState wear, WieldSlots wield)
    {
        float kg = 0f;
        if (wear != null)
        {
            var worn = wear.Worn;
            for (int i = 0; i < worn.Count; i++)
            {
                ItemStack stack = worn[i];
                if (stack != null)
                    kg += stack.TotalWeight;
            }
        }

        if (wield == null)
            return kg;

        ItemStack left = wield.Left;
        ItemStack right = wield.Right;
        if (left != null)
            kg += left.TotalWeight;
        if (right != null && !ReferenceEquals(left, right))
            kg += right.TotalWeight;
        return kg;
    }

    public static float ShooterDeltaV(
        ItemData gun,
        ItemData ammo,
        float shooterMassKg)
    {
        float jShot = ShotJin(gun, ammo);
        float kick = CombatMath.HandlingKickFactor(gun);
        return DeltaV(jShot * kick, shooterMassKg);
    }

    public static float DispersionKickFromDeltaV(float shooterDeltaV) =>
        Mathf.Max(0f, shooterDeltaV) * KickToDispersionPerDeltaV;
}
