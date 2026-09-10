// ============================================================
// ConsumeService — 인벤 1개 섭취/사용 → 위장·대사·MED heal
// ============================================================
// flowchart LR
//   RMB[Inventory RMB] --> Contrib[ConsumeContextContributor]
//   Contrib --> Action[ConsumeContextAction]
//   Action --> Svc[ConsumeService]
//   Svc --> Host[PlayerNeedsHost]
//   Svc --> Heal[BodyHealApply]
//   Heal --> Restore[BodyPartRestoreService]
//   Heal --> Wrap[bandaged]
//   Svc --> Hand[CharacterHandWork]
//   Hand --> Inv[container_or_wield take 1]

using System;
using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;

public enum ConsumeKind
{
    Eat,
    Drink,
    Use
}

public static class ConsumeService
{
    public const float RawCalorieMultiplier = 0.75f;
    public const string FlagRaw = "RAW";
    public const string FlagCooked = "COOKED";
    public const string TypeFood = "FOOD";
    public const string TypeDrink = "DRINK";
    public const string TypeMed = "MED";
    public const string UseActionHeal = "heal";
    public const string UseActionConsumeDrug = "consume_drug";
    public const string UseActionAntibiotic = "antibiotic";
    public const string UseActionWeakAntibiotic = "weak_antibiotic";
    public const string UseActionStrongAntibiotic = "strong_antibiotic";

    public static ConsumeKind? Classify(ItemData item)
    {
        if (item == null)
            return null;

        if (IsType(item.comestible_type, TypeMed) || IsType(item.type, TypeMed))
            return ConsumeKind.Use;
        if (IsType(item.comestible_type, TypeDrink) || IsType(item.type, TypeDrink))
            return ConsumeKind.Drink;
        if (IsType(item.comestible_type, TypeFood) || IsType(item.type, TypeFood))
            return ConsumeKind.Eat;

        if (item.comestible != null && item.comestible.calories > 0)
            return ConsumeKind.Eat;

        return null;
    }

    public static bool CanConsume(ItemStack stack, InventoryContainer container) =>
        CanConsume(stack, container, partId: null);

    public static bool CanConsume(ItemStack stack, InventoryContainer container, string partId)
    {
        if (MoodGameplayGate.IsBlocked)
            return false;
        if (PlayerNeedsHost.Active == null)
            return false;
        if (stack?.Item == null || stack.Count < 1)
            return false;
        if (!PlayerItemAccess.OwnsInBodyOrWield(stack, container))
            return false;
        if (Classify(stack.Item) == null)
            return false;
        return CanApplyUse(stack.Item, partId);
    }

    public static bool TryBegin(ItemStack stack, InventoryContainer container) =>
        TryBegin(stack, container, partId: null);

    public static bool TryBegin(ItemStack stack, InventoryContainer container, string partId)
    {
        if (!CanConsume(stack, container, partId))
            return false;

        ConsumeKind kind = Classify(stack.Item).Value;
        if (PlayerGearHost.Active?.Service == null)
            return TryConsume(stack, container, partId);

        return CharacterHandWork.TryBegin(
            stack,
            container,
            CharacterHandWork.DefaultHand(stack),
            ConsumeDuration.ActSeconds(kind),
            () => TryConsume(stack, container, partId));
    }

    public static bool TryConsume(ItemStack stack, InventoryContainer container) =>
        TryConsume(stack, container, partId: null);

    public static bool TryConsume(ItemStack stack, InventoryContainer container, string partId)
    {
        if (!CanConsume(stack, container, partId))
            return false;

        PlayerNeedsHost host = PlayerNeedsHost.Active;
        ItemData item = stack.Item;
        ConsumeKind kind = Classify(item).Value;
        bool wasHot = stack.Instance != null && stack.Instance.IsHot;
        bool wasCooked = stack.Instance != null && stack.Instance.IsCooked;
        bool rotten = ItemRot.IsRottenNow(stack.Instance);

        if (PlayerItemAccess.TryTakeOne(stack, container) <= 0)
            return false;

        switch (kind)
        {
            case ConsumeKind.Eat:
                ApplyFood(host, item, wasCooked);
                break;
            case ConsumeKind.Drink:
                ApplyDrink(host, item);
                break;
            case ConsumeKind.Use:
                ApplyMed(host, item, partId);
                break;
        }

        ApplyComestibleSideEffects(host, item);
        if (rotten)
            ApplyRotPenalty(host);

        RememberConsumeMood(item, kind, rotten, wasHot);
        return true;
    }

    static void RememberConsumeMood(ItemData item, ConsumeKind kind, bool rotten, bool wasHot)
    {
        CharacterMoodHost mood = CharacterMoodHost.Active;
        if (mood == null)
            return;

        if (rotten)
            mood.AddMemory(ThoughtId.AteRotten);

        if (kind != ConsumeKind.Eat && kind != ConsumeKind.Drink)
            return;

        if (wasHot)
            mood.AddMemory(ThoughtId.AteHotMeal);

        int fun = item?.comestible != null ? item.comestible.fun : 0;
        mood.AddMemory(ThoughtId.AteMeal, fun != 0 ? fun : (int?)null);
    }

    static void ApplyRotPenalty(PlayerNeedsHost host)
    {
        PlayerNeedsSettings settings = host.Settings;
        int fun = settings != null
            ? settings.RotFunPenalty
            : PlayerNeedsSettings.DefaultRotFunPenalty;
        int healthy = settings != null
            ? settings.RotHealthyPenalty
            : PlayerNeedsSettings.DefaultRotHealthyPenalty;
        if (fun != 0 || healthy != 0)
            host.ApplyMetabolites(fun, healthy, 0);

        ICharacterBody body = PlayerPossessSession.SessionBody;
        if (body != null)
            body.SetToxin01(body.Toxin01 + BodyIllness.RotToxinAdd);
    }

    static void ApplyFood(PlayerNeedsHost host, ItemData item, bool wasCooked)
    {
        float ml = item.volume_ml;
        float kcal = item.comestible != null ? item.comestible.calories : 0f;
        if (HasRawWithoutCooked(item, wasCooked))
            kcal *= RawCalorieMultiplier;

        host.IngestFood(ml, kcal);
    }

    static void ApplyDrink(PlayerNeedsHost host, ItemData item)
    {
        float ml = item.volume_ml;
        int quench = item.comestible != null ? item.comestible.quench : 0;
        host.IngestDrink(ml, quench);
    }

    static void ApplyMed(PlayerNeedsHost host, ItemData item, string partId)
    {
        ICharacterBody body = PlayerPossessSession.SessionBody;
        if (TryApplyAntibiotic(body, item))
            return;

        UseActionData action = item.use_action;
        if (action == null || string.IsNullOrEmpty(action.type))
            return;

        if (IsType(action.type, UseActionHeal))
        {
            if (body != null && !string.IsNullOrEmpty(partId))
                BodyHealApply.TryApply(body, action, partId);
            return;
        }

        if (!IsType(action.type, UseActionConsumeDrug))
            return;

        ComestibleDetailData comestible = item.comestible;
        int fun = comestible != null ? comestible.fun : 0;
        int healthy = comestible != null ? comestible.healthy : 0;
        int stim = comestible != null ? comestible.stim : 0;
        host.ApplyMetabolites(fun, healthy, stim);

        if (body != null && !string.IsNullOrEmpty(action.effect_id))
        {
            float seconds = action.duration > 0 ? action.duration : -1f;
            body.AddEffect(BodyPartIds.Chest, new BodyPartEffect(action.effect_id, 1, seconds));
        }

        if (body != null)
            ApplyMedIllnessRelief(body);
    }

    static bool TryApplyAntibiotic(ICharacterBody body, ItemData item)
    {
        if (item == null)
            return false;

        string actionType = item.use_action != null ? item.use_action.type : null;
        bool fromAction = BodyIllness.TryAntibioticIntensity(actionType, out int intensity);
        if (!fromAction && !BodyIllness.TryAntibioticIntensity(item.id, out intensity))
            return false;
        if (body == null)
            return true;

        body.EnsureEffectMinIntensity(
            BodyPartIds.Chest,
            BodyPartEffectIds.Antibiotic,
            intensity,
            BodyIllness.MedImmunityDurationSeconds);
        return true;
    }

    static void ApplyMedIllnessRelief(ICharacterBody body)
    {
        body.SetToxin01(body.Toxin01 - BodyIllness.MedToxinClear);
        ReduceBleedIntensity(body, BodyIllness.MedBleedIntensityReduce);
    }

    static void ReduceBleedIntensity(ICharacterBody body, int reduceBy)
    {
        if (reduceBy <= 0)
            return;

        IReadOnlyList<BodyPartNode> roots = body.Roots;
        for (int r = 0; r < roots.Count; r++)
            ReduceBleedSubtree(body, roots[r], reduceBy);
    }

    static void ReduceBleedSubtree(ICharacterBody body, BodyPartNode node, int reduceBy)
    {
        if (node == null)
            return;

        body.ReduceEffectIntensity(node.PartId, BodyPartEffectIds.Bleed, reduceBy);

        IReadOnlyList<BodyPartNode> children = node.Children;
        for (int c = 0; c < children.Count; c++)
            ReduceBleedSubtree(body, children[c], reduceBy);
    }

    static void ApplyComestibleSideEffects(PlayerNeedsHost host, ItemData item)
    {
        ComestibleDetailData comestible = item.comestible;
        if (comestible == null)
            return;

        ConsumeKind? kind = Classify(item);
        bool drugAlreadyApplied = kind == ConsumeKind.Use
            && item.use_action != null
            && IsType(item.use_action.type, UseActionConsumeDrug);
        if (!drugAlreadyApplied)
        {
            if (comestible.fun != 0 || comestible.healthy != 0 || comestible.stim != 0)
                host.ApplyMetabolites(comestible.fun, comestible.healthy, comestible.stim);
        }

        host.ApplyAddiction(comestible.addiction_type, comestible.addiction_potential);

        Dictionary<string, int> vitamins = comestible.vitamins;
        if (vitamins == null || vitamins.Count == 0)
            return;

        foreach (KeyValuePair<string, int> pair in vitamins)
            host.AddVitamin(pair.Key, pair.Value);
    }

    public static bool IsHealItem(ItemData item) => IsHealAction(item);

    public static bool IsHealAction(UseActionData action)
    {
        return action != null && IsType(action.type, UseActionHeal);
    }

    static bool CanApplyUse(ItemData item, string partId)
    {
        if (!IsHealAction(item))
            return true;

        ICharacterBody body = PlayerPossessSession.SessionBody;
        UseActionData action = item.use_action;
        if (string.IsNullOrEmpty(partId))
            return BodyHealApply.CanApply(body, action);
        return BodyHealApply.CanApplyTo(body, action, partId);
    }

    static bool IsHealAction(ItemData item)
    {
        UseActionData action = item != null ? item.use_action : null;
        return IsHealAction(action);
    }

    static bool HasRawWithoutCooked(ItemData item, bool wasCooked)
    {
        if (wasCooked)
            return false;
        if (HasFlag(item, FlagCooked))
            return false;
        return HasFlag(item, FlagRaw);
    }

    static bool HasFlag(ItemData item, string flag)
    {
        if (item?.flags == null || string.IsNullOrEmpty(flag))
            return false;

        for (int i = 0; i < item.flags.Count; i++)
        {
            string value = item.flags[i];
            if (!string.IsNullOrEmpty(value) && value.Equals(flag, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    static bool IsType(string value, string expected)
    {
        return !string.IsNullOrEmpty(value)
            && value.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }
}
