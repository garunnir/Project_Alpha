// ============================================================
// CombatLeafUtil — Leaf / Family / AnimVerb / 마스크 / ResolveMode
// ============================================================

using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;

public enum WeaponResolveMode
{
    MeleeReach = 0,
    RangedRay = 1,
    /// <summary>인접 walkable face 블록 타겟 — hitscan/캐릭터 데미지 없음.</summary>
    MeleeBlock = 2,
    /// <summary>조준 셀 나무 작물 — hitscan/캐릭터 데미지 없음.</summary>
    MeleePlant = 3
}

public enum AttackPerformResult
{
    Performed = 0,
    Miss = 1,
    Unsupported = 2,
    OutOfRange = 3,
    Cooling = 4,
    NoTarget = 5,
    NoAmmo = 6,
    /// <summary>장애물에 막힘 (빗나감 Miss와 구분 — Impact Blocked).</summary>
    Obstructed = 7
}

public static class CombatLeafUtil
{
    public const int LegacyCuttingValue = 1;

    /// <summary>UI·Cycle·Entry용 Leaf 전부.</summary>
    public static readonly CombatLeaf[] All =
    {
        CombatLeaf.Strike,
        CombatLeaf.Pierce,
        CombatLeaf.Excavate,
        CombatLeaf.Chop,
        CombatLeaf.Semi,
        CombatLeaf.Burst,
        CombatLeaf.Auto,
        CombatLeaf.Raise
    };

    /// <summary>슬롯 파일 AnimVerb 접두. Catalog는 Leaf마다 행.</summary>
    public static readonly AnimVerb[] AllAnimVerbs =
    {
        AnimVerb.Swing,
        AnimVerb.Thrust,
        AnimVerb.Trigger,
        AnimVerb.Raise,
        AnimVerb.Dig
    };

    /// <summary>구 Cutting만 Strike로. Trigger 값은 유지(AnimVerb 베이크용).</summary>
    public static CombatLeaf FoldLegacyCutting(CombatLeaf leaf)
    {
        if ((int)leaf == LegacyCuttingValue)
            return CombatLeaf.Strike;
        return leaf;
    }

    /// <summary>Leaf 정규화: Cutting→Strike, 구 Trigger→Semi.</summary>
    public static CombatLeaf Normalize(CombatLeaf leaf)
    {
        leaf = FoldLegacyCutting(leaf);
        if (leaf == CombatLeaf.Trigger)
            return CombatLeaf.Semi;
        return leaf;
    }

    /// <summary>팔 애니·Pipeline thin·슬롯 파일명(Swing/Thrust/Dig/…).</summary>
    public static AnimVerb ToAnimVerb(CombatLeaf leaf)
    {
        switch (Normalize(leaf))
        {
            case CombatLeaf.Semi:
            case CombatLeaf.Burst:
            case CombatLeaf.Auto:
                return AnimVerb.Trigger;
            case CombatLeaf.Excavate:
                return AnimVerb.Dig;
            case CombatLeaf.Chop:
                return AnimVerb.Swing;
            case CombatLeaf.Pierce:
                return AnimVerb.Thrust;
            case CombatLeaf.Raise:
                return AnimVerb.Raise;
            default:
                return AnimVerb.Swing;
        }
    }

    /// <summary>슬롯 .anim 접두 (HoldSwing_, AttackDig_, …). AnimVerb 이름 유지.</summary>
    public static string AnimClipStem(CombatLeaf leaf) => ToAnimVerb(leaf).ToString();

    public static bool IsRanged(CombatLeaf leaf) =>
        ToAnimVerb(leaf) == AnimVerb.Trigger;

    public static bool TryGetFamily(CombatLeaf leaf, out CombatLeafFamily family)
    {
        switch (Normalize(leaf))
        {
            case CombatLeaf.Strike:
            case CombatLeaf.Pierce:
                family = CombatLeafFamily.Melee;
                return true;
            case CombatLeaf.Semi:
            case CombatLeaf.Burst:
            case CombatLeaf.Auto:
                family = CombatLeafFamily.Trigger;
                return true;
            case CombatLeaf.Excavate:
            case CombatLeaf.Chop:
            case CombatLeaf.Raise:
                family = CombatLeafFamily.Etc;
                return true;
            default:
                family = default;
                return false;
        }
    }

    /// <summary>Odin/컨텍스트 경로. Family 있으면 "Melee/Strike", "Etc/Raise" 등.</summary>
    public static string DropdownPath(CombatLeaf leaf)
    {
        CombatLeaf normalized = Normalize(leaf);
        if (TryGetFamily(normalized, out CombatLeafFamily family))
            return FamilyLabel(family) + "/" + LeafLabel(normalized);
        return LeafLabel(normalized);
    }

    public static string FamilyLabel(CombatLeafFamily family)
    {
        switch (family)
        {
            case CombatLeafFamily.Melee: return "Melee";
            case CombatLeafFamily.Trigger: return "Trigger";
            case CombatLeafFamily.Etc: return "Etc";
            default: return family.ToString();
        }
    }

    public static string LeafLabel(CombatLeaf leaf)
    {
        switch (Normalize(leaf))
        {
            case CombatLeaf.Strike: return "Strike";
            case CombatLeaf.Pierce: return "Pierce";
            case CombatLeaf.Excavate: return "Excavate";
            case CombatLeaf.Chop: return "Chop";
            case CombatLeaf.Semi: return "Semi";
            case CombatLeaf.Burst: return "Burst";
            case CombatLeaf.Auto: return "Auto";
            case CombatLeaf.Raise: return "Raise";
            default: return Normalize(leaf).ToString();
        }
    }

    public static CombatLeafMask ToMask(CombatLeaf leaf)
    {
        switch (Normalize(leaf))
        {
            case CombatLeaf.Strike: return CombatLeafMask.Strike;
            case CombatLeaf.Pierce: return CombatLeafMask.Pierce;
            case CombatLeaf.Excavate: return CombatLeafMask.Excavate;
            case CombatLeaf.Chop: return CombatLeafMask.Chop;
            case CombatLeaf.Raise: return CombatLeafMask.Raise;
            case CombatLeaf.Semi: return CombatLeafMask.Semi;
            case CombatLeaf.Burst: return CombatLeafMask.Burst;
            case CombatLeaf.Auto: return CombatLeafMask.Auto;
            default: return CombatLeafMask.None;
        }
    }

    public static WeaponResolveMode ResolveMode(CombatLeaf leaf)
    {
        CombatLeaf normalized = Normalize(leaf);
        if (normalized == CombatLeaf.Excavate)
            return WeaponResolveMode.MeleeBlock;
        if (normalized == CombatLeaf.Chop)
            return WeaponResolveMode.MeleePlant;
        return IsRanged(normalized)
            ? WeaponResolveMode.RangedRay
            : WeaponResolveMode.MeleeReach;
    }

    public static bool SuppressesAttackTrigger(CombatLeaf leaf) =>
        Normalize(leaf) == CombatLeaf.Raise;

    /// <summary>시전 1회당 발사 수. Burst=gun.burst(없으면 DefaultBurstShots). Auto=홀드 재시전당 1발.</summary>
    public const int DefaultBurstShots = 3;
    /// <summary>레거시 클릭 볼리 상한. Auto는 hold 재시전이라 발사 수에 쓰지 않음.</summary>
    public const int AutoClickVolleyMax = 10;

    public static int ShotsPerPerform(CombatLeaf leaf, ItemData item)
    {
        switch (Normalize(leaf))
        {
            case CombatLeaf.Burst:
            {
                int burst = item?.gun != null ? item.gun.burst : 0;
                return burst > 0 ? burst : DefaultBurstShots;
            }
            case CombatLeaf.Auto:
                return 1;
            default:
                return 1;
        }
    }

    /// <summary>볼리 탄 사이 간격 = 기본 공속 × 이 비율 (첫 탄 제외).</summary>
    public const float BurstShotIntervalFactor = 0.2f;
    public const float AutoShotIntervalFactor = 0.12f;

    public static float VolleyShotIntervalFactor(CombatLeaf leaf)
    {
        switch (Normalize(leaf))
        {
            case CombatLeaf.Burst: return BurstShotIntervalFactor;
            case CombatLeaf.Auto: return AutoShotIntervalFactor;
            default: return 1f;
        }
    }

    public static bool TryNextAvailable(
        CombatLeafMask available,
        CombatLeaf current,
        out CombatLeaf next)
    {
        next = current;
        if (available == CombatLeafMask.None)
            return false;

        CombatLeaf normalized = Normalize(current);
        int count = All.Length;
        int currentIndex = 0;
        for (int i = 0; i < count; i++)
        {
            if (All[i] != normalized)
                continue;
            currentIndex = i;
            break;
        }

        for (int step = 1; step <= count; step++)
        {
            CombatLeaf candidate = All[(currentIndex + step) % count];
            if ((available & ToMask(candidate)) == 0)
                continue;
            next = candidate;
            return true;
        }

        return false;
    }

    public static bool TryFirstAvailable(CombatLeafMask available, out CombatLeaf leaf)
    {
        for (int i = 0; i < All.Length; i++)
        {
            CombatLeaf candidate = All[i];
            if ((available & ToMask(candidate)) == 0)
                continue;
            leaf = candidate;
            return true;
        }

        leaf = CombatLeaf.Strike;
        return false;
    }

    /// <summary>가용 Leaf를 All 순으로 나열 (UI Family 그룹은 Contributor).</summary>
    public static void CollectAvailableLeaves(
        CombatLeafMask available,
        List<CombatLeaf> into)
    {
        if (into == null)
            return;
        into.Clear();
        for (int i = 0; i < All.Length; i++)
        {
            CombatLeaf leaf = All[i];
            if ((available & ToMask(leaf)) == 0)
                continue;
            into.Add(leaf);
        }
    }
}
