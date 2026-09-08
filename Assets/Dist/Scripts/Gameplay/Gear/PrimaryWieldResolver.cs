// ============================================================
// PrimaryWieldResolver — DPS 최대 손 → SetWieldedItem / 최고 액션
// ============================================================

using System;
using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

public static class PrimaryWieldResolver
{
    public struct HandScore
    {
        public WieldSlotId Slot;
        public ItemStack Stack;
        public CombatLeaf? Action;
        public float Score;
        public bool IsOffHand;
    }

    /// <summary>
    /// Occupied 손별 score 최대. 동점 Right → Left.
    /// 손 액션은 ItemInstance + Presentation default (DPS 최고 액션 아님).
    /// </summary>
    public static bool TryResolvePrimary(
        WieldSlots slots,
        WeaponPresentationCatalog catalog,
        ICharacterSkills skills,
        out HandScore primary,
        out HandScore secondary)
    {
        primary = default;
        secondary = default;
        if (slots == null)
            return false;

        if (slots.IsTwoHand)
        {
            ItemStack stack = slots.Left ?? slots.Right;
            if (stack?.Item == null)
                return false;

            WeaponPresentation presentation = CombatLeafRows.Resolve(catalog, stack);
            CombatLeaf action = CombatLeafRows.ResolveSelected(stack.Instance, presentation);
            primary = new HandScore
            {
                Slot = WieldSlotId.Right,
                Stack = stack,
                Action = action,
                Score = ScoreHand(stack, action, presentation, skills, offHandFactor: 1f),
                IsOffHand = false
            };
            return true;
        }

        HandScore left = EvaluateSlot(WieldSlotId.Left, slots.Left, catalog, skills);
        HandScore right = EvaluateSlot(WieldSlotId.Right, slots.Right, catalog, skills);

        bool leftOk = left.Stack != null && left.Action != null;
        bool rightOk = right.Stack != null && right.Action != null;
        if (!leftOk && !rightOk)
            return false;

        if (leftOk && rightOk)
        {
            // 동점 Right 우선
            if (right.Score >= left.Score)
            {
                primary = right;
                secondary = left;
                secondary.IsOffHand = true;
                secondary.Score = ScoreHand(
                    secondary.Stack,
                    secondary.Action,
                    CombatLeafRows.Resolve(catalog, secondary.Stack),
                    skills,
                    OffHandFactor(skills, WieldHand.Left));
            }
            else
            {
                primary = left;
                secondary = right;
                secondary.IsOffHand = true;
                secondary.Score = ScoreHand(
                    secondary.Stack,
                    secondary.Action,
                    CombatLeafRows.Resolve(catalog, secondary.Stack),
                    skills,
                    OffHandFactor(skills, WieldHand.Right));
            }

            primary.IsOffHand = false;
            return true;
        }

        primary = rightOk ? right : left;
        primary.IsOffHand = false;
        return true;
    }

    [Obsolete("Do not pick select by DPS. Use CombatLeafRows.ResolveSelected.")]
    public static CombatLeaf? BestActionForItem(
        ItemData item,
        ICharacterSkills skills)
    {
        WeaponPresentation presentation = null;
        return CombatLeafRows.Default(presentation);
    }

    public static float OffHandFactor(ICharacterSkills skills, WieldHand hand)
    {
        if (skills == null)
            return GearConstants.OffHandDpsFactorMin;

        string skillId = HandProficiencyIds.ForHand(hand);
        return GearConstants.OffHandDpsFactor(skills.Level(skillId));
    }

    static HandScore EvaluateSlot(
        WieldSlotId slot,
        ItemStack stack,
        WeaponPresentationCatalog catalog,
        ICharacterSkills skills)
    {
        if (stack?.Item == null)
            return default;

        WeaponPresentation presentation = CombatLeafRows.Resolve(catalog, stack);
        CombatLeaf action = CombatLeafRows.ResolveSelected(stack.Instance, presentation);
        return new HandScore
        {
            Slot = slot,
            Stack = stack,
            Action = action,
            Score = ScoreHand(stack, action, presentation, skills, 1f),
            IsOffHand = false
        };
    }

    static float ScoreHand(
        ItemStack stack,
        CombatLeaf? action,
        WeaponPresentation presentation,
        ICharacterSkills skills,
        float offHandFactor)
    {
        ItemData item = stack?.Item;
        if (item == null || action == null)
            return 0f;
        return Dps(stack, action.Value, presentation, skills, offHandFactor);
    }

    static float Dps(
        ItemStack stack,
        CombatLeaf action,
        WeaponPresentation presentation,
        ICharacterSkills skills,
        float offHandFactor)
    {
        ItemData item = stack.Item;
        WeaponAttack attack = null;
        if (presentation != null &&
            presentation.TryGetEntry(action, out WeaponPresentation.Entry entry))
            attack = entry.attack;

        if (CombatLeafUtil.IsRanged(action) &&
            !WeaponChamber.CanCommitFire(item, stack.Instance, stack, attack))
            return 0f;

        float interval = CombatMath.AttackIntervalSeconds(item, action);
        if (interval <= 0f)
            return 0f;

        int strength = skills != null ? skills.Level(AttributeIds.Str) : 8;
        string skillId = CombatMath.SkillId(item, action);
        int skillLevel = skills != null && !string.IsNullOrEmpty(skillId)
            ? skills.Level(skillId)
            : 0;
        int damage = CombatMath.Damage(
            item,
            attack,
            action,
            strength,
            skillLevel,
            WeaponChamber.ResolveAmmo(stack, stack.Instance));
        return damage / interval * Mathf.Max(0f, offHandFactor);
    }
}
