// ============================================================
// WearCombatDefense — Wear 림월드 3단 주사 완화 + WearEnc 명중 배율
// ============================================================

using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

/// <summary>
/// Phase D combat consume SSOT. Thickness+resist → ArmorRating only (no HP subtract).
/// Docs: docs/equipment/GEAR.md
/// </summary>
public static class WearCombatDefense
{
    /// <summary>coverage 정수 → 확률 (BN %, 호버·구 API).</summary>
    public const float CoveragePercentScale = 100f;

    /// <summary>thickness 1 → ArmorRating 가산.</summary>
    public const float ThicknessToArmorRating = 10f;

    /// <summary>material resist 1 → ArmorRating 가산.</summary>
    public const float ResistToArmorRating = 10f;

    /// <summary>림월드 ArmorRating 상한 (200%).</summary>
    public const float ArmorRatingCap = 200f;

    /// <summary>3단 주사 난수 상한.</summary>
    public const float ArmorRollMax = 100f;

    /// <summary>근접 AP = rawDamage × 이 값.</summary>
    public const float MeleeArmorPenPerDamage = 1.5f;

    /// <summary>착용 enc 1포인트당 공격자 HitChance 감소.</summary>
    public const float WearEncHitPenaltyPerPoint = 0.01f;

    /// <summary>WearEncAccuracyFactor 최대 감소량 (0.35 = 최대 −35%).</summary>
    public const float WearEncHitPenaltyCap = 0.35f;

    /// <summary>구조물·타일 deterministic 완화용. rating=100 → 약 50% 감쇠 기준.</summary>
    public const float StructureMitigationScale = 100f;

    const int CoverScratchCap = 16;

    static readonly CoverPiece[] CoverScratch = new CoverPiece[CoverScratchCap];

    public readonly struct ArmorMitigateResult
    {
        public readonly int Damage;
        public readonly string DamageTag;

        public ArmorMitigateResult(int damage, string damageTag)
        {
            Damage = damage < 0 ? 0 : damage;
            DamageTag = string.IsNullOrEmpty(damageTag)
                ? AttackDamageTags.Fallback
                : damageTag;
        }
    }

    struct CoverPiece
    {
        public ItemData Item;
        public int WornIndex;
        public int OutsideRank;
    }

    /// <summary>
    /// WearEncAccuracyFactor = 1 − min(enc × WearEncHitPenaltyPerPoint, WearEncHitPenaltyCap).
    /// </summary>
    public static float WearEncAccuracyFactor(int totalWearEncumbrance)
    {
        if (totalWearEncumbrance <= 0)
            return 1f;

        float penalty = totalWearEncumbrance * WearEncHitPenaltyPerPoint;
        if (penalty > WearEncHitPenaltyCap)
            penalty = WearEncHitPenaltyCap;
        return Mathf.Clamp01(1f - penalty);
    }

    /// <summary>
    /// 명중 후 부위 방어. 덮는 조각을 바깥→안으로 3단 주사.
    /// 튕김=0. 완화=절반, Sharp(cut/bullet)는 전 레이어 후 bash.
    /// 두께+resist는 ArmorRating만. AP는 원거리 pierce, 아니면 raw×1.5.
    /// </summary>
    public static ArmorMitigateResult MitigateDamage(
        EquipmentWearState wear,
        string aimedPartId,
        int rawDamage,
        string damageTag,
        int armorPen = 0)
    {
        if (rawDamage <= 0)
            return new ArmorMitigateResult(0, damageTag);

        if (wear == null)
            return new ArmorMitigateResult(rawDamage, damageTag);

        string partId = string.IsNullOrEmpty(aimedPartId)
            ? BodyPartIds.Torso
            : aimedPartId;

        int coverCount = CollectCoveringPieces(wear, partId);
        if (coverCount <= 0)
            return new ArmorMitigateResult(rawDamage, damageTag);

        SortCoveringOutsideIn(coverCount);

        float remaining = rawDamage;
        float ap = ResolveArmorPenetration(rawDamage, armorPen);
        bool mitigated = false;
        string incomingTag = string.IsNullOrEmpty(damageTag)
            ? AttackDamageTags.Fallback
            : damageTag;

        for (int i = 0; i < coverCount && remaining > 0f; i++)
        {
            ItemData item = CoverScratch[i].Item;
            int resist = MaxMaterialResist(item, incomingTag);
            float rating = ArmorRating(item.armor.material_thickness, resist);
            float effective = rating - ap;
            if (effective <= 0f)
                continue;

            float roll = Random.Range(0f, ArmorRollMax);
            float half = effective * 0.5f;
            if (roll < half)
            {
                remaining = 0f;
                break;
            }

            if (roll <= effective)
            {
                remaining *= 0.5f;
                mitigated = true;
            }
        }

        string resultTag = incomingTag;
        if (mitigated && remaining > 0f && IsSharpTag(incomingTag))
            resultTag = AttackDamageTags.Bash;

        return new ArmorMitigateResult(Mathf.RoundToInt(remaining), resultTag);
    }

    /// <summary>
    /// 구조물·타일 deterministic 완화. Wear 3단 주사 대신 rating 비례 감쇠.
    /// 채널·MaterialData resist는 Wear와 동일 SSOT.
    /// </summary>
    public static int MitigateStructureDamage(
        IReadOnlyList<string> materialIds,
        int materialThickness,
        int rawDamage,
        string damageTag)
    {
        if (rawDamage <= 0)
            return 0;

        int resist = MaxMaterialResist(materialIds, damageTag);
        if (resist <= 0 && materialThickness <= 0)
            return rawDamage;

        float rating = ArmorRating(materialThickness, resist);
        float ap = ResolveArmorPenetration(rawDamage, 0);
        float effective = Mathf.Max(0f, rating - ap);
        if (effective <= 0f)
            return rawDamage;

        float absorb = effective / (effective + StructureMitigationScale);
        return Mathf.Max(0, Mathf.RoundToInt(rawDamage * (1f - absorb)));
    }

    public static float ResolveArmorPenetration(int rawDamage, int ammoPierce)
    {
        if (ammoPierce > 0)
            return ammoPierce;

        if (rawDamage <= 0)
            return 0f;

        return rawDamage * MeleeArmorPenPerDamage;
    }

    public static float ArmorRating(int materialThickness, int materialResist)
    {
        float rating = Mathf.Max(0, materialThickness) * ThicknessToArmorRating
            + Mathf.Max(0, materialResist) * ResistToArmorRating;
        if (rating > ArmorRatingCap)
            return ArmorRatingCap;
        return rating;
    }

    public static float ArmorEngageChance(int coverage) =>
        Mathf.Clamp01(coverage / CoveragePercentScale);

    /// <summary>
    /// Covering worn pieces의 materials 중 해당 채널 resist 최댓값.
    /// materials/MaterialData 없으면 0.
    /// </summary>
    public static int MaterialResistForPart(
        EquipmentWearState wear,
        string partId,
        string damageTag)
    {
        if (wear == null || string.IsNullOrEmpty(partId))
            return 0;

        int best = 0;
        IReadOnlyList<ItemStack> worn = wear.Worn;
        for (int i = 0; i < worn.Count; i++)
        {
            ItemStack stack = worn[i];
            ItemData item = stack?.Item;
            if (item == null || !GearHandleRules.CoversPart(item, partId))
                continue;

            int piece = MaxMaterialResist(item, damageTag);
            if (piece > best)
                best = piece;
        }

        return best;
    }

    static bool IsSharpTag(string damageTag) =>
        string.Equals(damageTag, AttackDamageTags.Cut, System.StringComparison.Ordinal)
        || string.Equals(damageTag, AttackDamageTags.Bullet, System.StringComparison.Ordinal);

    static int CollectCoveringPieces(EquipmentWearState wear, string partId)
    {
        int count = 0;
        IReadOnlyList<ItemStack> worn = wear.Worn;
        for (int i = 0; i < worn.Count && count < CoverScratchCap; i++)
        {
            ItemData item = worn[i]?.Item;
            if (item?.armor == null || !GearHandleRules.CoversPart(item, partId))
                continue;

            CoverScratch[count] = new CoverPiece
            {
                Item = item,
                WornIndex = i,
                OutsideRank = GearConstants.ArmorLayerOutsideRank(
                    WearOverlapRules.NormalizeLayer(item.armor))
            };
            count++;
        }

        return count;
    }

    static void SortCoveringOutsideIn(int count)
    {
        for (int i = 1; i < count; i++)
        {
            CoverPiece key = CoverScratch[i];
            int j = i - 1;
            while (j >= 0
                && (CoverScratch[j].OutsideRank > key.OutsideRank
                    || (CoverScratch[j].OutsideRank == key.OutsideRank
                        && CoverScratch[j].WornIndex > key.WornIndex)))
            {
                CoverScratch[j + 1] = CoverScratch[j];
                j--;
            }

            CoverScratch[j + 1] = key;
        }
    }

    public static int MaxMaterialResist(
        IReadOnlyList<string> materialIds,
        string damageTag)
    {
        if (materialIds == null || materialIds.Count == 0)
            return 0;

        int best = 0;
        for (int i = 0; i < materialIds.Count; i++)
        {
            string materialId = materialIds[i];
            if (string.IsNullOrEmpty(materialId))
                continue;

            MaterialData material = GameplayData.GetMaterial(materialId);
            if (material == null)
                continue;

            int resist = ResistForTag(material, damageTag);
            if (resist > best)
                best = resist;
        }

        return best;
    }

    static int MaxMaterialResist(ItemData item, string damageTag) =>
        item != null ? MaxMaterialResist(item.materials, damageTag) : 0;

    static int ResistForTag(MaterialData material, string damageTag)
    {
        if (string.Equals(damageTag, AttackDamageTags.Cut, System.StringComparison.Ordinal))
            return material.cut_resist;
        if (string.Equals(damageTag, AttackDamageTags.Bullet, System.StringComparison.Ordinal))
            return material.bullet_resist;
        return material.bash_resist;
    }
}
