// ============================================================
// CharacterRuntimeDebugModel — Play 모드 캐릭터 런타임 디버그 Odin 프록시
// ============================================================
// CharacterRuntimeDebugDomain = 커버 목록 SSOT. 탭/ShowIf와 1:1.
// 새 런타임 스테이터스 → 같은 변경에서 이 enum + 탭 + DEFINITION.md 인벤토리.

using System;
using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>런타임 디버그 창이 커버하는 도메인. 탭과 1:1.</summary>
public enum CharacterRuntimeDebugDomain
{
    Target = 0,
    Body = 1,
    Illness = 2,
    Needs = 3,
    Climate = 4,
    Skills = 5,
    Combat = 6,
    Mood = 7,
    Chips = 8,
    Emote = 9
}

[Serializable]
public sealed class CharacterRuntimeDebugModel
{
    const string DomainTabs = "Domain";

    static readonly string[] EffectIdChoices =
    {
        BodyPartEffectIds.Bleed,
        BodyPartEffectIds.Bruise,
        BodyPartEffectIds.Cut,
        BodyPartEffectIds.Gunshot,
        BodyPartEffectIds.Fracture,
        BodyPartEffectIds.Infected,
        BodyPartEffectIds.Regenerating,
        BodyPartEffectIds.Adrenaline,
        BodyPartEffectIds.Frostbite,
        BodyPartEffectIds.Heat,
        BodyPartEffectIds.Bloated,
        BodyPartEffectIds.Toxin,
        BodyPartEffectIds.Antibiotic,
        BodyPartEffectIds.Bandaged
    };

    static readonly string[] BodyPartIdChoices = BodyPartIds.StatusConditionParts;

    static readonly ThoughtId[] MemoryThoughtChoices =
    {
        ThoughtId.AteMeal,
        ThoughtId.Vomited,
        ThoughtId.AteRotten,
        ThoughtId.Catharsis,
        ThoughtId.Crafted,
        ThoughtId.AteHotMeal,
        ThoughtId.Recovering,
        ThoughtId.NeedShower,
        ThoughtId.FreshlyBathed,
        ThoughtId.Attractive,
        ThoughtId.PleasantConversation,
        ThoughtId.RestArea,
        ThoughtId.SuitableEnvironment,
        ThoughtId.NatureFriendly,
        ThoughtId.Inspired,
        ThoughtId.Motivated,
        ThoughtId.SkillUp,
        ThoughtId.RelationshipImproved,
        ThoughtId.Loved,
        ThoughtId.MarriedEngaged,
        ThoughtId.Trust,
        ThoughtId.Respect
    };

    CharacterBodyHost _bodyHost;
    CharacterSkillsHost _skillsHost;
    CharacterClimateHost _climateHost;
    PlayerNeedsHost _needsHost;
    CharacterImbalanceHost _imbalanceHost;
    CharacterPainHost _painHost;
    CharacterMoodHost _moodHost;
    CharacterActionHost _actionHost;
    CharacterAppearanceHost _appearance;
    CharacterFactionHost _factionHost;
    CharacterMotor _motor;
    CharacterVision _vision;
    CharacterHearing _hearing;
    CharacterPresenceHost _presenceHost;
    CharacterEmoteHost _emoteHost;
    CharacterDefinitionBinder _definitionBinder;
    PlayerEncumbranceHost _encumbrance;
    readonly List<BodyPartEffect> _effectScratch = new(16);
    readonly List<MoodEntry> _chipScratch = new(16);
    readonly List<BodyPartDebugRow> _bodyPartRows = new(32);
    readonly List<ThermalPartDebugRow> _thermalRows = new(16);
    readonly List<SkillDebugRow> _attributeRows = new(16);
    readonly List<SkillDebugRow> _skillRows = new(64);
    readonly List<EffectDebugRow> _effectRows = new(16);
    readonly List<ThoughtDebugRow> _thoughtRows = new(16);
    readonly List<ChipDebugRow> _chipRows = new(16);

    [HideInInspector]
    public CharacterBodyHost BodyHost => _bodyHost;

    public bool CanWrite => Application.isPlaying && _bodyHost != null;

    bool ShowBindWarning => !CanWrite;

    bool AlwaysShow => true;

    public bool HasNeeds => CanWrite && _needsHost != null && IsPossessed;
    public bool HasMood => CanWrite && _moodHost != null && IsPossessed;
    public bool HasClimate => CanWrite && _climateHost != null;
    public bool HasSkills => CanWrite && _skillsHost != null;
    public bool HasCombat => CanWrite && (_imbalanceHost != null || _painHost != null);
    public bool HasSenses => CanWrite && (_vision != null || _hearing != null);
    public bool HasLocomotion => CanWrite && _motor != null;
    public bool HasPresence => CanWrite && _presenceHost != null;
    public bool HasEmote => CanWrite && _emoteHost != null;
    public bool IsPossessed => _motor != null && _motor.IsPossessed;

    [ShowInInspector, HideLabel, DisplayAsString(EnableRichText = true)]
    [PropertyOrder(-100)]
    [InfoBox("Bind a live CharacterBodyHost in Play mode to edit.", InfoMessageType.Warning, nameof(ShowBindWarning))]
    [GUIColor("@CanWrite ? \"lightgreen\" : \"orange\"")]
    string BindStatus =>
        CanWrite
            ? "<b>Writable</b>  —  Play + bound host"
            : "<b>Read-only</b>  —  bind a live CharacterBodyHost in Play";

    public void Bind(CharacterBodyHost host)
    {
        _bodyHost = host;
        _skillsHost = null;
        _climateHost = null;
        _needsHost = null;
        _imbalanceHost = null;
        _painHost = null;
        _moodHost = null;
        _actionHost = null;
        _appearance = null;
        _factionHost = null;
        _motor = null;
        _vision = null;
        _hearing = null;
        _presenceHost = null;
        _emoteHost = null;
        _definitionBinder = null;
        _encumbrance = null;

        if (host == null)
        {
            RebuildRowCaches();
            return;
        }

        CharacterBodyRefs refs = host.BodyRefs;
        _skillsHost = refs != null ? refs.SkillsHost : null;
        _climateHost = refs != null ? refs.ClimateHost : null;
        _needsHost = refs != null ? refs.NeedsHost : null;
        _imbalanceHost = refs != null ? refs.ImbalanceHost : null;
        _painHost = refs != null ? refs.PainHost : null;
        _moodHost = refs != null ? refs.MoodHost : null;
        _actionHost = refs != null ? refs.ActionHost : null;
        _factionHost = refs != null ? refs.FactionHost : null;
        _motor = refs != null ? refs.Motor : null;
        _definitionBinder = refs != null ? refs.DefinitionBinder : null;
        _encumbrance = refs != null ? refs.EncumbranceHost : null;
        _vision = refs != null ? refs.Vision : null;
        _hearing = refs != null ? refs.Hearing : null;
        _presenceHost = refs != null ? refs.Presence : null;
        _appearance = refs != null ? refs.Appearance : null;
        _emoteHost = refs != null ? refs.Emote : null;
        RebuildRowCaches();
    }

    void RebuildRowCaches()
    {
        _bodyPartRows.Clear();
        _thermalRows.Clear();
        _attributeRows.Clear();
        _skillRows.Clear();

        if (_bodyHost == null)
            return;

        for (int i = 0; i < BodyPartIds.StatusConditionParts.Length; i++)
            _bodyPartRows.Add(new BodyPartDebugRow(this, BodyPartIds.StatusConditionParts[i]));

        for (int i = 0; i < BodyPartIds.ThermalParts.Length; i++)
            _thermalRows.Add(new ThermalPartDebugRow(this, BodyPartIds.ThermalParts[i]));

        ICharacterSkills skills = Skills;
        if (skills == null)
            return;

        for (int i = 0; i < AttributeIds.All.Length; i++)
            _attributeRows.Add(new SkillDebugRow(this, AttributeIds.All[i], isAttribute: true));

        foreach (KeyValuePair<string, SkillDef> pair in SkillCatalog.ById)
        {
            if (pair.Value == null || string.IsNullOrEmpty(pair.Key))
                continue;
            if (AttributeIds.IsAttribute(pair.Key))
                continue;
            _skillRows.Add(new SkillDebugRow(this, pair.Key, isAttribute: false));
        }

        _skillRows.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
    }

    ICharacterBody Body => _bodyHost != null ? _bodyHost.Body : null;

    ICharacterSkills Skills => _skillsHost != null ? _skillsHost.Skills : null;

    ICharacterDefeat Defeat => _skillsHost != null ? _skillsHost.Defeat : null;

    IPlayerVitals Vitals => IsPossessed ? GameplayData.Vitals : null;

    // ── Target ───────────────────────────────────────────────

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Target), SdfIconType.PersonFill, TabLayouting = TabLayouting.MultiRow, TextColor = "lightblue")]
    [ShowInInspector, HideLabel]
    [DisplayAsString(18, true)]
    string TargetDisplayName =>
        "<b>" + (
            _appearance != null ? _appearance.ResolveDisplayName()
            : _bodyHost != null ? _bodyHost.name
            : "(none)") + "</b>";

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Target))]
    [HorizontalGroup("Domain/Target/Flags")]
    [ShowInInspector, DisplayAsString, HideLabel]
    [GUIColor("@TargetPossessed ? \"lightblue\" : \"gray\"")]
    string PossessedBadge => TargetPossessed ? "POSSESSED" : "NPC";

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Target))]
    [HorizontalGroup("Domain/Target/Flags")]
    [ShowInInspector, DisplayAsString, HideLabel]
    [GUIColor("@TargetDeadState ? \"red\" : \"lightgreen\"")]
    string DeadBadge => TargetDeadState ? "DEAD" : "ALIVE";

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Target))]
    [HorizontalGroup("Domain/Target/Flags")]
    [ShowInInspector, DisplayAsString, HideLabel]
    [GUIColor("@TargetDefeated ? \"orange\" : \"lightgreen\"")]
    string DefeatedBadge => TargetDefeated ? "DEFEATED" : "UP";

    bool TargetPossessed => IsPossessed;

    bool TargetDeadState => Body != null && Body.IsDeadState;

    bool TargetDefeated => Defeat != null && Defeat.IsDefeated;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Target))]
    [ShowInInspector, DisplayAsString]
    [LabelText("Faction", SdfIconType.FlagFill)]
    string TargetFactionLabel =>
        _factionHost != null && _factionHost.Faction != null
            ? _factionHost.Faction.DisplayName
            : "(none)";

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Target))]
    [ShowInInspector, ReadOnly, EnumToggleButtons]
    [LabelText("Defeat", SdfIconType.XOctagonFill)]
    DefeatCause TargetDefeatCause => Defeat != null ? Defeat.Cause : DefeatCause.None;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Target))]
    [ResponsiveButtonGroup("Domain/Target/Actions")]
    [Button(SdfIconType.HeartFill, "Revive")]
    [GUIColor(0.45f, 0.85f, 0.5f, 1f)]
    [EnableIf(nameof(CanWrite))]
    void TargetRevive() => Defeat?.Revive();

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Target))]
    [ResponsiveButtonGroup("Domain/Target/Actions")]
    [Button(SdfIconType.StopCircleFill, "Cancel All Actions")]
    [EnableIf(nameof(CanWrite))]
    void TargetCancelActions() => _actionHost?.CancelAll();

    // ── Body ─────────────────────────────────────────────────

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Body), SdfIconType.HeartFill, TextColor = "lightred")]
    [ShowInInspector]
    [LabelText("Parts")]
    [TableList(
        AlwaysExpanded = true,
        HideToolbar = true,
        IsReadOnly = true,
        ShowIndexLabels = false,
        DrawScrollView = true,
        MinScrollViewHeight = DebugUi.BodyTableMin,
        MaxScrollViewHeight = DebugUi.BodyTableMax,
        CellPadding = DebugUi.TablePad)]
    List<BodyPartDebugRow> BodyParts => _bodyPartRows;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Body))]
    [BoxGroup("Domain/Body/Apply Effect", centerLabel: true)]
    [HorizontalGroup("Domain/Body/Apply Effect/Fields")]
    [ShowInInspector]
    [ValueDropdown(nameof(BodyPartIdChoices))]
    [LabelText("Part")]
    [EnableIf(nameof(CanWrite))]
    string EffectPartId = BodyPartIds.Chest;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Body))]
    [BoxGroup("Domain/Body/Apply Effect")]
    [HorizontalGroup("Domain/Body/Apply Effect/Fields")]
    [ShowInInspector]
    [ValueDropdown(nameof(EffectIdChoices))]
    [LabelText("Effect")]
    [EnableIf(nameof(CanWrite))]
    string EffectId = BodyPartEffectIds.Bleed;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Body))]
    [BoxGroup("Domain/Body/Apply Effect")]
    [ShowInInspector]
    [LabelText("Intensity")]
    [SuffixLabel("HP pts (tissue / bleed)", Overlay = true)]
    [MinValue(0)]
    [EnableIf(nameof(CanWrite))]
    int EffectIntensity = 1;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Body))]
    [BoxGroup("Domain/Body/Apply Effect")]
    [ShowInInspector]
    [LabelText("Remaining")]
    [SuffixLabel("sec  (−1 = permanent)", Overlay = true)]
    [EnableIf(nameof(CanWrite))]
    float EffectRemainingSeconds = -1f;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Body))]
    [BoxGroup("Domain/Body/Apply Effect")]
    [ResponsiveButtonGroup("Domain/Body/Apply Effect/Buttons")]
    [Button(SdfIconType.PlusCircleFill, "Apply")]
    [GUIColor(0.85f, 0.4f, 0.35f, 1f)]
    [EnableIf(nameof(CanWrite))]
    void ApplyEffect()
    {
        if (Body == null || string.IsNullOrEmpty(EffectPartId) || string.IsNullOrEmpty(EffectId))
            return;
        Body.EnsureEffectMinIntensity(EffectPartId, EffectId, EffectIntensity, EffectRemainingSeconds);
        if (BodyInjury.IsTissue(EffectId))
            BodyInjury.SyncPart(Body, EffectPartId);
    }

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Body))]
    [BoxGroup("Domain/Body/Apply Effect")]
    [ResponsiveButtonGroup("Domain/Body/Apply Effect/Buttons")]
    [Button(SdfIconType.TrashFill, "Clear Part")]
    [EnableIf(nameof(CanWrite))]
    void ClearPartEffects()
    {
        if (Body == null || string.IsNullOrEmpty(EffectPartId))
            return;
        Body.ClearEffectsOn(EffectPartId);
        BodyInjury.SyncPart(Body, EffectPartId);
    }

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Body))]
    [BoxGroup("Domain/Body/Apply Effect")]
    [ShowInInspector]
    [LabelText("On part")]
    [TableList(
        AlwaysExpanded = true,
        HideToolbar = true,
        IsReadOnly = true,
        ShowIndexLabels = false,
        DrawScrollView = true,
        MinScrollViewHeight = DebugUi.CompactTableMin,
        MaxScrollViewHeight = DebugUi.CompactTableMax,
        CellPadding = DebugUi.TablePad)]
    List<EffectDebugRow> SelectedPartEffects
    {
        get
        {
            _effectRows.Clear();
            if (Body == null || string.IsNullOrEmpty(EffectPartId))
                return _effectRows;
            _effectScratch.Clear();
            Body.CollectEffectsUnder(EffectPartId, _effectScratch, includeDescendants: false);
            for (int i = 0; i < _effectScratch.Count; i++)
                _effectRows.Add(new EffectDebugRow(_effectScratch[i]));
            return _effectRows;
        }
    }

    // ── Illness ──────────────────────────────────────────────

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Illness), SdfIconType.DropletFill, TextColor = "purple")]
    [ShowInInspector]
    [ProgressBar(0, 1, Height = DebugUi.MeterHeight, ColorGetter = nameof(GetBloodOxygenBarColor))]
    [EnableIf(nameof(CanWrite))]
    [LabelText("Blood O2", SdfIconType.Wind)]
    float BloodOxygen01
    {
        get => Body != null ? Body.BloodOxygen01 : 0f;
        set
        {
            if (CanWrite && Body != null)
                Body.SetBloodOxygen01(value);
        }
    }

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Illness), SdfIconType.DropletFill, TextColor = "purple")]
    [ShowInInspector]
    [ProgressBar(0, 1, Height = DebugUi.MeterHeight, ColorGetter = nameof(GetBloodBarColor))]
    [EnableIf(nameof(CanWrite))]
    [LabelText("Blood", SdfIconType.DropletFill)]
    float Blood01
    {
        get => Body != null ? Body.Blood01 : 0f;
        set
        {
            if (CanWrite && Body != null)
                Body.SetBlood01(value);
        }
    }

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Illness))]
    [ShowInInspector]
    [ProgressBar(0, 1, Height = DebugUi.MeterHeight, ColorGetter = nameof(GetToxinBarColor))]
    [EnableIf(nameof(CanWrite))]
    [LabelText("Toxin", SdfIconType.Radioactive)]
    float Toxin01
    {
        get => Body != null ? Body.Toxin01 : 0f;
        set
        {
            if (CanWrite && Body != null)
                Body.SetToxin01(value);
        }
    }

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Illness))]
    [ShowInInspector]
    [ProgressBar(0, 1, Height = DebugUi.MeterHeight, ColorGetter = nameof(GetInfectionBarColor))]
    [EnableIf(nameof(CanWrite))]
    [LabelText("Infection", SdfIconType.BugFill)]
    float InfectionProgress01
    {
        get => Body != null ? Body.InfectionProgress01 : 0f;
        set
        {
            if (CanWrite && Body != null)
                Body.SetInfectionProgress01(value);
        }
    }

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Illness))]
    [ShowInInspector]
    [ProgressBar(0, 1, Height = DebugUi.MeterHeight, ColorGetter = nameof(GetImmunityBarColor))]
    [EnableIf(nameof(CanWrite))]
    [LabelText("Immunity", SdfIconType.ShieldFill)]
    float InfectionImmunity01
    {
        get => Body != null ? Body.InfectionImmunity01 : 0f;
        set
        {
            if (CanWrite && Body != null)
                Body.SetInfectionImmunity01(value);
        }
    }

    Color GetBloodBarColor() => DebugUi.BloodColor(Blood01);
    Color GetBloodOxygenBarColor() => DebugUi.BloodColor(BloodOxygen01);

    Color GetToxinBarColor() => DebugUi.Toxin;

    Color GetInfectionBarColor() => DebugUi.Infection;

    Color GetImmunityBarColor() => DebugUi.Immunity;

    // ── Needs ────────────────────────────────────────────────

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Needs), SdfIconType.EggFried, TextColor = "orange")]
    [ShowIf(nameof(HasNeeds))]
    [BoxGroup("Domain/Needs/Stomach")]
    [ShowInInspector]
    [ProgressBar(0, nameof(StomachCapacityMl), Height = DebugUi.MeterHeight, ColorGetter = nameof(GetHungerBarColor))]
    [EnableIf(nameof(HasNeeds))]
    [LabelText("Food ml")]
    float StomachMlFood
    {
        get => _needsHost != null ? _needsHost.StomachMlFood : 0f;
        set
        {
            if (!HasNeeds)
                return;
            _needsHost.SetStomach(value, _needsHost.StomachMlWater, _needsHost.StomachKcal);
        }
    }

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Needs))]
    [ShowIf(nameof(HasNeeds))]
    [BoxGroup("Domain/Needs/Stomach")]
    [ShowInInspector]
    [ProgressBar(0, nameof(StomachCapacityMl), Height = DebugUi.MeterHeight, ColorGetter = nameof(GetThirstBarColor))]
    [EnableIf(nameof(HasNeeds))]
    [LabelText("Water ml")]
    float StomachMlWater
    {
        get => _needsHost != null ? _needsHost.StomachMlWater : 0f;
        set
        {
            if (!HasNeeds)
                return;
            _needsHost.SetStomach(_needsHost.StomachMlFood, value, _needsHost.StomachKcal);
        }
    }

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Needs))]
    [ShowIf(nameof(HasNeeds))]
    [BoxGroup("Domain/Needs/Stomach")]
    [ShowInInspector]
    [SuffixLabel("kcal in stomach", Overlay = true)]
    [MinValue(0)]
    [EnableIf(nameof(HasNeeds))]
    [LabelText("Kcal")]
    float StomachKcal
    {
        get => _needsHost != null ? _needsHost.StomachKcal : 0f;
        set
        {
            if (!HasNeeds)
                return;
            _needsHost.SetStomach(_needsHost.StomachMlFood, _needsHost.StomachMlWater, value);
        }
    }

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Needs))]
    [ShowIf(nameof(HasNeeds))]
    [BoxGroup("Domain/Needs/Vitals")]
    [ShowInInspector]
    [ProgressBar(0, nameof(HungerMax), Height = DebugUi.MeterHeight, ColorGetter = nameof(GetHungerBarColor), CustomValueStringGetter = nameof(HungerLabel))]
    [EnableIf(nameof(HasNeeds))]
    [LabelText("Hunger", SdfIconType.EggFried)]
    int Hunger
    {
        get => Vitals != null ? Vitals.GetCurrent(VitalKeys.Hunger) : 0;
        set => Vitals?.SetCurrent(VitalKeys.Hunger, value);
    }

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Needs))]
    [ShowIf(nameof(HasNeeds))]
    [BoxGroup("Domain/Needs/Vitals")]
    [ShowInInspector]
    [ProgressBar(0, nameof(ThirstMax), Height = DebugUi.MeterHeight, ColorGetter = nameof(GetThirstBarColor), CustomValueStringGetter = nameof(ThirstLabel))]
    [EnableIf(nameof(HasNeeds))]
    [LabelText("Thirst", SdfIconType.DropletFill)]
    int Thirst
    {
        get => Vitals != null ? Vitals.GetCurrent(VitalKeys.Thirst) : 0;
        set => Vitals?.SetCurrent(VitalKeys.Thirst, value);
    }

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Needs))]
    [ShowIf(nameof(HasNeeds))]
    [BoxGroup("Domain/Needs/Vitals")]
    [ShowInInspector]
    [ProgressBar(0, nameof(StaminaMax), Height = DebugUi.MeterHeight, ColorGetter = nameof(GetStaminaBarColor), CustomValueStringGetter = nameof(StaminaLabel))]
    [EnableIf(nameof(HasNeeds))]
    [LabelText("Stamina", SdfIconType.LightningChargeFill)]
    int Stamina
    {
        get => Vitals != null ? Vitals.GetCurrent(VitalKeys.Stamina) : 0;
        set => Vitals?.SetCurrent(VitalKeys.Stamina, value);
    }

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Needs))]
    [ShowIf(nameof(HasNeeds))]
    [BoxGroup("Domain/Needs/Metabolites")]
    [HorizontalGroup("Domain/Needs/Metabolites/Row")]
    [ShowInInspector]
    [EnableIf(nameof(HasNeeds))]
    int Fun
    {
        get => _needsHost != null ? _needsHost.Fun : 0;
        set
        {
            if (HasNeeds)
                _needsHost.SetMetabolites(value, _needsHost.Healthy, _needsHost.Stim);
        }
    }

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Needs))]
    [ShowIf(nameof(HasNeeds))]
    [BoxGroup("Domain/Needs/Metabolites")]
    [HorizontalGroup("Domain/Needs/Metabolites/Row")]
    [ShowInInspector]
    [EnableIf(nameof(HasNeeds))]
    int Healthy
    {
        get => _needsHost != null ? _needsHost.Healthy : 0;
        set
        {
            if (HasNeeds)
                _needsHost.SetMetabolites(_needsHost.Fun, value, _needsHost.Stim);
        }
    }

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Needs))]
    [ShowIf(nameof(HasNeeds))]
    [BoxGroup("Domain/Needs/Metabolites")]
    [HorizontalGroup("Domain/Needs/Metabolites/Row")]
    [ShowInInspector]
    [EnableIf(nameof(HasNeeds))]
    int Stim
    {
        get => _needsHost != null ? _needsHost.Stim : 0;
        set
        {
            if (HasNeeds)
                _needsHost.SetMetabolites(_needsHost.Fun, _needsHost.Healthy, value);
        }
    }

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Needs))]
    [ShowIf(nameof(HasNeeds))]
    [BoxGroup("Domain/Needs/Sleep")]
    [ShowInInspector]
    [ProgressBar(0, 1, Height = DebugUi.MeterHeight, ColorGetter = nameof(GetFatigueBarColor))]
    [EnableIf(nameof(HasNeeds))]
    [LabelText("Fatigue")]
    float Fatigue01
    {
        get => _needsHost != null ? _needsHost.Fatigue01 : 0f;
        set
        {
            if (HasNeeds)
                _needsHost.SetFatigue01(value);
        }
    }

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Needs))]
    [ShowIf(nameof(HasNeeds))]
    [BoxGroup("Domain/Needs/Sleep")]
    [ShowInInspector]
    [ProgressBar(0, 1, Height = DebugUi.MeterHeight, ColorGetter = nameof(GetSleepBarColor))]
    [EnableIf(nameof(HasNeeds))]
    [LabelText("Sleep debt")]
    float SleepDebt01
    {
        get => _needsHost != null ? _needsHost.SleepDebt01 : 0f;
        set
        {
            if (HasNeeds)
                _needsHost.SetSleepDebt01(value);
        }
    }

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Needs))]
    [ShowIf(nameof(HasNeeds))]
    [BoxGroup("Domain/Needs/Sleep")]
    [ShowInInspector, ReadOnly]
    [ProgressBar(0, 1, Height = DebugUi.MeterHeight, ColorGetter = nameof(GetSleepBarColor))]
    [LabelText("Sleep display")]
    float SleepDisplay01 => _needsHost != null ? _needsHost.SleepDisplay01 : 0f;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Needs))]
    [ShowIf(nameof(HasNeeds))]
    [BoxGroup("Domain/Needs/Sleep")]
    [ShowInInspector, DisplayAsString]
    [GUIColor("@IsSleeping ? \"lightblue\" : \"gray\"")]
    [LabelText("State")]
    string SleepStateLabel => IsSleeping ? "SLEEPING" : "AWAKE";

    bool IsSleeping => _needsHost != null && _needsHost.IsSleeping;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Needs))]
    [ShowIf(nameof(HasNeeds))]
    [BoxGroup("Domain/Needs/Sleep")]
    [ResponsiveButtonGroup("Domain/Needs/Sleep/Buttons")]
    [Button(SdfIconType.MoonFill, "Try Sleep")]
    [EnableIf(nameof(HasNeeds))]
    void TrySleep() => _needsHost?.TrySleep();

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Needs))]
    [ShowIf(nameof(HasNeeds))]
    [BoxGroup("Domain/Needs/Sleep")]
    [ResponsiveButtonGroup("Domain/Needs/Sleep/Buttons")]
    [Button(SdfIconType.SunFill, "Wake")]
    [EnableIf(nameof(HasNeeds))]
    void Wake() => _needsHost?.Wake();

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Needs))]
    [HideIf(nameof(HasNeeds))]
    [InfoBox("Needs/Vitals: possessed player only.", SdfIconType.InfoCircleFill, nameof(AlwaysShow))]
    [ShowInInspector, ReadOnly, HideLabel]
    string NeedsUnavailable => "";

    float StomachCapacityMl =>
        _needsHost != null && _needsHost.Settings != null
            ? _needsHost.Settings.StomachCapacityMl
            : PlayerNeedsSettings.DefaultStomachCapacityMl;

    int HungerMax => Vitals != null ? Vitals.GetMax(VitalKeys.Hunger) : DefaultPlayerVitals.DefaultHungerMax;

    int ThirstMax => Vitals != null ? Vitals.GetMax(VitalKeys.Thirst) : DefaultPlayerVitals.DefaultThirstMax;

    int StaminaMax => Vitals != null ? Vitals.GetMax(VitalKeys.Stamina) : DefaultPlayerVitals.DefaultStaminaMax;

    string HungerLabel => Hunger + " / " + HungerMax;

    string ThirstLabel => Thirst + " / " + ThirstMax;

    string StaminaLabel => Stamina + " / " + StaminaMax;

    Color GetHungerBarColor() => DebugUi.Hunger;

    Color GetThirstBarColor() => DebugUi.Thirst;

    Color GetStaminaBarColor() => DebugUi.Stamina;

    Color GetFatigueBarColor() => DebugUi.Fatigue;

    Color GetSleepBarColor() => DebugUi.Sleep;

    // ── Climate ──────────────────────────────────────────────

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Climate), SdfIconType.ThermometerHalf, TextColor = "cyan")]
    [ShowIf(nameof(HasClimate))]
    [ShowInInspector]
    [ProgressBar(0, 1, Height = DebugUi.MeterHeight, ColorGetter = nameof(GetWetBarColor))]
    [EnableIf(nameof(HasClimate))]
    [LabelText("Wetness", SdfIconType.Water)]
    float Wetness01
    {
        get => _climateHost != null ? _climateHost.EnvExposure.Wetness01 : 0f;
        set
        {
            if (HasClimate)
                _climateHost.EnvExposure.SetWetness01(value);
        }
    }

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Climate))]
    [ShowIf(nameof(HasClimate))]
    [ShowInInspector, DisplayAsString]
    [GUIColor(nameof(GetFeelingColor))]
    [LabelText("Core feeling")]
    string CoreFeelingLabel => CoreFeeling.ToString();

    BodyTempFeeling CoreFeeling =>
        _climateHost != null ? _climateHost.BodyTemperature.Feeling : BodyTempFeeling.Comfortable;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Climate))]
    [ShowIf(nameof(HasClimate))]
    [ShowInInspector]
    [LabelText("Thermal parts")]
    [TableList(
        AlwaysExpanded = true,
        HideToolbar = true,
        IsReadOnly = true,
        ShowIndexLabels = false,
        DrawScrollView = false,
        CellPadding = DebugUi.TablePad)]
    List<ThermalPartDebugRow> ThermalParts => _thermalRows;

    Color GetWetBarColor() => DebugUi.Wet;

    Color GetFeelingColor()
    {
        switch (CoreFeeling)
        {
            case BodyTempFeeling.Cold: return DebugUi.TempCold;
            case BodyTempFeeling.Cool: return DebugUi.TempCool;
            case BodyTempFeeling.Comfortable: return DebugUi.TempOk;
            case BodyTempFeeling.Warm: return DebugUi.TempWarm;
            case BodyTempFeeling.Hot: return DebugUi.TempHot;
            default: return Color.white;
        }
    }

    // ── Skills ───────────────────────────────────────────────

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Skills), SdfIconType.LightningChargeFill, TextColor = "yellow")]
    [ShowIf(nameof(HasSkills))]
    [ShowInInspector]
    [LabelText("Attributes")]
    [TableList(
        AlwaysExpanded = true,
        HideToolbar = true,
        IsReadOnly = true,
        ShowIndexLabels = false,
        DrawScrollView = false,
        CellPadding = DebugUi.TablePad)]
    List<SkillDebugRow> AttributeRows => _attributeRows;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Skills))]
    [ShowIf(nameof(HasSkills))]
    [ShowInInspector, Searchable]
    [LabelText("Skills")]
    [TableList(
        AlwaysExpanded = true,
        HideToolbar = true,
        IsReadOnly = true,
        ShowIndexLabels = false,
        ShowPaging = true,
        NumberOfItemsPerPage = DebugUi.SkillPageSize,
        DrawScrollView = true,
        MinScrollViewHeight = DebugUi.SkillTableMin,
        MaxScrollViewHeight = DebugUi.SkillTableMax,
        CellPadding = DebugUi.TablePad)]
    List<SkillDebugRow> SkillRows => _skillRows;

    // ── Combat ───────────────────────────────────────────────

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat), SdfIconType.ShieldFill, TextColor = "orange")]
    [ShowIf(nameof(HasSenses))]
    [BoxGroup("Domain/Combat/Senses")]
    [HorizontalGroup("Domain/Combat/Senses/Sight")]
    [ShowInInspector, ReadOnly, LabelText("Sight detect (base)")]
    float SenseSightDetectBase =>
        _definitionBinder != null && _definitionBinder.Definition != null
            ? _definitionBinder.Definition.Senses.sightDetectMeters
            : CharacterSenseBlock.Default.sightDetectMeters;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasSenses))]
    [BoxGroup("Domain/Combat/Senses")]
    [HorizontalGroup("Domain/Combat/Senses/Sight")]
    [ShowInInspector, ReadOnly, LabelText("effective")]
    float SenseSightDetectEffective => _vision != null
        ? _vision.EffectiveDetectRadius
        : CharacterSenseBlock.Default.sightDetectMeters;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasSenses))]
    [BoxGroup("Domain/Combat/Senses")]
    [HorizontalGroup("Domain/Combat/Senses/Sight")]
    [ShowInInspector, ReadOnly, LabelText("lose (base)")]
    float SenseSightLoseBase =>
        _definitionBinder != null && _definitionBinder.Definition != null
            ? _definitionBinder.Definition.Senses.sightLoseMeters
            : CharacterSenseBlock.Default.sightLoseMeters;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasSenses))]
    [BoxGroup("Domain/Combat/Senses")]
    [HorizontalGroup("Domain/Combat/Senses/Sight")]
    [ShowInInspector, ReadOnly, LabelText("lose effective")]
    float SenseSightLoseEffective => _vision != null
        ? _vision.EffectiveLoseRadius
        : CharacterSenseBlock.Default.sightLoseMeters;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasSenses))]
    [BoxGroup("Domain/Combat/Senses")]
    [HorizontalGroup("Domain/Combat/Senses/Hearing")]
    [ShowInInspector, ReadOnly, LabelText("Hearing (base)")]
    float SenseHearingBase =>
        _definitionBinder != null && _definitionBinder.Definition != null
            ? _definitionBinder.Definition.Senses.hearingRadiusMeters
            : CharacterSenseBlock.Default.hearingRadiusMeters;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasSenses))]
    [BoxGroup("Domain/Combat/Senses")]
    [HorizontalGroup("Domain/Combat/Senses/Hearing")]
    [ShowInInspector, ReadOnly, LabelText("effective")]
    float SenseHearingEffective => _hearing != null
        ? _hearing.EffectiveHearingRadius
        : CharacterSenseBlock.Default.hearingRadiusMeters;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasLocomotion))]
    [BoxGroup("Domain/Combat/Locomotion")]
    [HorizontalGroup("Domain/Combat/Locomotion/Walk")]
    [ShowInInspector, ReadOnly, LabelText("Walk (definition)")]
    string LocomotionWalkDefinition =>
        _definitionBinder != null && _definitionBinder.Definition != null &&
        _definitionBinder.Definition.WalkSpeedMeters > 0f
            ? $"{_definitionBinder.Definition.WalkSpeedMeters:0.##} m/s"
            : "(prefab default)";

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasLocomotion))]
    [BoxGroup("Domain/Combat/Locomotion")]
    [HorizontalGroup("Domain/Combat/Locomotion/Walk")]
    [ShowInInspector, ReadOnly, LabelText("motor base")]
    float LocomotionMotorWalkBase => _motor != null
        ? _motor.BaseWalkSpeed
        : CharacterLocomotionDefaults.DefaultWalkSpeedMeters;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasLocomotion))]
    [BoxGroup("Domain/Combat/Locomotion")]
    [HorizontalGroup("Domain/Combat/Locomotion/Walk")]
    [ShowInInspector, ReadOnly, LabelText("current")]
    float LocomotionCurrentSpeed => _motor != null ? _motor.CurrentSpeed : 0f;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasPresence))]
    [BoxGroup("Domain/Combat/Presence")]
    [HorizontalGroup("Domain/Combat/Presence/Row")]
    [ShowInInspector, ReadOnly, LabelText("Visibility")]
    float PresenceVisibility01 => _presenceHost.Visibility01;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasPresence))]
    [BoxGroup("Domain/Combat/Presence")]
    [HorizontalGroup("Domain/Combat/Presence/Row")]
    [ShowInInspector, ReadOnly, LabelText("Noise")]
    float PresenceNoise01 => _presenceHost.Noise01;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasPresence))]
    [BoxGroup("Domain/Combat/Presence")]
    [ShowInInspector, ReadOnly, LabelText("Stealth")]
    bool PresenceStealthActive =>
        _bodyHost != null &&
        _bodyHost.BodyRefs != null &&
        _bodyHost.BodyRefs.State != null &&
        _bodyHost.BodyRefs.State.IsStealth;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasCombat))]
    [BoxGroup("Domain/Combat/Surprise")]
    [ShowInInspector, ReadOnly, LabelText("Sees possessed (vision)")]
    bool SurpriseSeesPossessed =>
        CanWrite &&
        TryResolvePossessedBody(out CharacterBodyHost possessed) &&
        CombatSurprise.HasVisionOf(_bodyHost, possessed);

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasCombat))]
    [BoxGroup("Domain/Combat/Surprise")]
    [ShowInInspector, ReadOnly, LabelText("Surprise stun remain")]
    float SurpriseStunRemain =>
        _painHost != null ? _painHost.SurpriseStunRemain : 0f;

    static bool TryResolvePossessedBody(out CharacterBodyHost host)
    {
        host = null;
        int count = CharacterBodyHost.ActiveCount;
        for (int i = 0; i < count; i++)
        {
            CharacterBodyHost candidate = CharacterBodyHost.GetActive(i);
            if (candidate == null)
                continue;
            CharacterMotor motor = candidate.BodyRefs != null ? candidate.BodyRefs.Motor : null;
            if (motor == null || !motor.IsPossessed)
                continue;
            host = candidate;
            return true;
        }

        return false;
    }

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat), SdfIconType.ShieldFill, TextColor = "orange")]
    [ShowIf(nameof(HasCombat))]
    [BoxGroup("Domain/Combat/Imbalance")]
    [ShowInInspector]
    [ProgressBar(0, 1, Height = DebugUi.MeterHeight, ColorGetter = nameof(GetImbalanceBarColor))]
    [EnableIf(nameof(HasCombat))]
    [LabelText("Imbalance")]
    float Imbalance01
    {
        get => _imbalanceHost != null ? _imbalanceHost.Imbalance01 : 0f;
        set
        {
            if (HasCombat && _imbalanceHost != null)
                _imbalanceHost.SetImbalance01(value);
        }
    }

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasCombat))]
    [BoxGroup("Domain/Combat/Imbalance")]
    [HorizontalGroup("Domain/Combat/Imbalance/Factors")]
    [ShowInInspector, ReadOnly, SuffixLabel("move", Overlay = true)]
    float MoveSpeedFactor => _imbalanceHost != null ? _imbalanceHost.MoveSpeedFactor : 1f;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasCombat))]
    [BoxGroup("Domain/Combat/Imbalance")]
    [HorizontalGroup("Domain/Combat/Imbalance/Factors")]
    [ShowInInspector, ReadOnly, SuffixLabel("hit", Overlay = true)]
    float HitAccuracyFactor => _imbalanceHost != null ? _imbalanceHost.HitAccuracyFactor : 1f;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasCombat))]
    [BoxGroup("Domain/Combat/Pain")]
    [InfoBox(
        "Pain is derived from tissue injuries (bruise/cut/gunshot/fracture) + bleed. Remaining HP = max − injury sum.",
        SdfIconType.InfoCircleFill,
        nameof(AlwaysShow))]
    [ShowInInspector, ReadOnly]
    [ProgressBar(0, 1, Height = DebugUi.MeterHeight, ColorGetter = nameof(GetPainBarColor))]
    [LabelText("Effective")]
    float PainEffective01 => ResolveEffectivePain01();

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasCombat))]
    [BoxGroup("Domain/Combat/Pain")]
    [ShowInInspector, ReadOnly]
    [ProgressBar(0, 1, Height = DebugUi.MeterHeight, ColorGetter = nameof(GetPainTotalBarColor))]
    [LabelText("Total")]
    float PainTotal01 => Body != null ? CombatPain.PainTotal01(Body) : 0f;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasCombat))]
    [BoxGroup("Domain/Combat/Pain")]
    [ShowInInspector, ReadOnly]
    [ProgressBar(0, 1, Height = DebugUi.MeterHeight)]
    [LabelText("Factor")]
    float PainFactor => Body != null ? CombatPain.PainFactor(Body, _effectScratch) : 1f;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasCombat))]
    [BoxGroup("Domain/Combat/Pain")]
    [HorizontalGroup("Domain/Combat/Pain/Thresholds")]
    [ShowInInspector, ReadOnly, LabelText("Shock ≥")]
    float PainShockThreshold => CombatPain.PainShockThreshold;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasCombat))]
    [BoxGroup("Domain/Combat/Pain")]
    [HorizontalGroup("Domain/Combat/Pain/Thresholds")]
    [ShowInInspector, ReadOnly, LabelText("Wake <")]
    float PainWakeThreshold => CombatPain.PainWakeThreshold;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasCombat))]
    [BoxGroup("Domain/Combat/Pain")]
    [HorizontalGroup("Domain/Combat/Pain/Flags")]
    [ShowInInspector, DisplayAsString, HideLabel]
    [GUIColor("@IsPainShockedFromPain ? \"red\" : \"lightgreen\"")]
    string PainShockBadge => IsPainShockedFromPain ? "SHOCK (pain)" : "no shock";

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasCombat))]
    [BoxGroup("Domain/Combat/Pain")]
    [HorizontalGroup("Domain/Combat/Pain/Flags")]
    [ShowInInspector, DisplayAsString, HideLabel]
    [GUIColor("@IsPainShockedHost ? \"red\" : \"gray\"")]
    string HostShockBadge => IsPainShockedHost ? "HOST SHOCKED" : "host ok";

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasCombat))]
    [BoxGroup("Domain/Combat/Pain")]
    [HorizontalGroup("Domain/Combat/Pain/Flags")]
    [ShowInInspector, DisplayAsString, HideLabel]
    [GUIColor("@IsCapacityDowned ? \"orange\" : \"lightgreen\"")]
    string CapacityDownBadge => IsCapacityDowned ? "CAPACITY DOWN" : "capacity ok";

    bool IsPainShockedFromPain => CombatPain.IsPainShocked(PainEffective01);

    bool IsPainShockedHost => _painHost != null && _painHost.IsPainShocked;

    bool IsCapacityDowned => BodyCapacity.IsCapacityDowned(Body);

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(PainHostMissing))]
    [InfoBox("CharacterPainHost missing on target — move-lock / host shock flags unavailable.", InfoMessageType.Warning)]
    [ShowInInspector, ReadOnly, HideLabel]
    string PainHostMissingNote => "";

    bool PainHostMissing => CanWrite && Body != null && _painHost == null;

    float ResolveEffectivePain01()
    {
        if (Body == null)
            return 0f;
        return CombatPain.EffectivePain01(Body, _effectScratch);
    }

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasCombat))]
    [BoxGroup("Domain/Combat/Capacity")]
    [ShowInInspector, ReadOnly]
    [ProgressBar(0, 1, Height = DebugUi.MeterHeight, ColorGetter = nameof(GetCapacityBarColor))]
    float Consciousness => BodyCapacity.Consciousness(Body);

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasCombat))]
    [BoxGroup("Domain/Combat/Capacity")]
    [ShowInInspector, ReadOnly]
    [ProgressBar(0, 1, Height = DebugUi.MeterHeight, ColorGetter = nameof(GetCapacityBarColor))]
    float Moving => BodyCapacity.Moving(Body);

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasCombat))]
    [BoxGroup("Domain/Combat/Capacity")]
    [ShowInInspector, ReadOnly]
    [ProgressBar(0, 1, Height = DebugUi.MeterHeight, ColorGetter = nameof(GetCapacityBarColor))]
    float Breathing => BodyCapacity.Breathing(Body);

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasCombat))]
    [BoxGroup("Domain/Combat/Capacity")]
    [ShowInInspector, ReadOnly]
    [ProgressBar(0, 1, Height = DebugUi.MeterHeight, ColorGetter = nameof(GetCapacityBarColor))]
    float Manipulation => BodyCapacity.Manipulation(Body);

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Combat))]
    [ShowIf(nameof(HasCombat))]
    [BoxGroup("Domain/Combat/Encumbrance")]
    [ShowInInspector, DisplayAsString]
    [GUIColor(nameof(GetEncumbranceColor))]
    [LabelText("Stage", SdfIconType.BoxSeam)]
    string EncumbranceLabel => EncumbranceStage.ToString();

    PlayerEncumbranceStage EncumbranceStage =>
        _encumbrance != null ? _encumbrance.Stage : PlayerEncumbranceStage.None;

    Color GetImbalanceBarColor() => DebugUi.Imbalance;

    Color GetPainBarColor() => DebugUi.PainColor(PainEffective01);

    Color GetPainTotalBarColor() => DebugUi.PainColor(PainTotal01);

    Color GetCapacityBarColor() => DebugUi.Capacity;

    Color GetEncumbranceColor()
    {
        switch (EncumbranceStage)
        {
            case PlayerEncumbranceStage.None: return DebugUi.Alive;
            case PlayerEncumbranceStage.Light: return DebugUi.TempWarm;
            case PlayerEncumbranceStage.Medium: return DebugUi.Hunger;
            case PlayerEncumbranceStage.Heavy: return DebugUi.Pain;
            case PlayerEncumbranceStage.Extreme: return DebugUi.Dead;
            default: return Color.white;
        }
    }

    // ── Emote ────────────────────────────────────────────────

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Emote), SdfIconType.ChatSquareTextFill, TextColor = "cyan")]
    [ShowIf(nameof(HasEmote))]
    [ShowInInspector, ReadOnly, LabelText("Resolved")]
    EmoteId EmoteResolvedId => _emoteHost != null ? _emoteHost.ResolvedId : EmoteId.None;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Emote))]
    [ShowIf(nameof(HasEmote))]
    [ShowInInspector, ReadOnly]
    EmoteSource EmoteResolvedSource => _emoteHost != null ? _emoteHost.ResolvedSource : EmoteSource.None;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Emote))]
    [ShowIf(nameof(HasEmote))]
    [ShowInInspector, ReadOnly]
    [GUIColor("@EmoteDisplayVisible ? \"lightgreen\" : \"gray\"")]
    bool EmoteDisplayVisible => _emoteHost != null && _emoteHost.IsDisplayVisible;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Emote))]
    [ShowIf(nameof(HasEmote))]
    [ShowInInspector, ReadOnly]
    EmoteHideReason EmoteHideReason => _emoteHost != null ? _emoteHost.HideReason : EmoteHideReason.NoActiveEmote;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Emote))]
    [ShowIf(nameof(HasEmote))]
    [ShowInInspector, ReadOnly]
    bool EmoteCatalogBound => _emoteHost != null && _emoteHost.Catalog != null;

    // ── Mood ─────────────────────────────────────────────────

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Mood), SdfIconType.EmojiSmileFill, TextColor = "lightmagenta")]
    [ShowIf(nameof(HasMood))]
    [ShowInInspector, ReadOnly]
    [ProgressBar(nameof(MoodMin), nameof(MoodMax), Height = DebugUi.MeterHeight, ColorGetter = nameof(GetMoodBarColor))]
    [LabelText("Mood", SdfIconType.EmojiSmileFill)]
    float MoodValue => _moodHost != null ? _moodHost.Mood : 0f;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Mood))]
    [ShowIf(nameof(HasMood))]
    [HorizontalGroup("Domain/Mood/Flags")]
    [ShowInInspector, DisplayAsString, HideLabel]
    [GUIColor("@BreakKind != MoodBreakKind.None ? \"orange\" : \"gray\"")]
    string BreakBadge => BreakKind == MoodBreakKind.None ? "stable" : BreakKind.ToString();

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Mood))]
    [ShowIf(nameof(HasMood))]
    [HorizontalGroup("Domain/Mood/Flags")]
    [ShowInInspector, DisplayAsString, HideLabel]
    [GUIColor("@IsControlYielded ? \"red\" : \"lightgreen\"")]
    string YieldBadge => IsControlYielded ? "CONTROL YIELDED" : "in control";

    MoodBreakKind BreakKind => _moodHost != null ? _moodHost.BreakKind : MoodBreakKind.None;

    bool IsControlYielded => _moodHost != null && _moodHost.IsControlYielded;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Mood))]
    [ShowIf(nameof(HasMood))]
    [ShowInInspector]
    [LabelText("Thoughts")]
    [TableList(
        AlwaysExpanded = true,
        HideToolbar = true,
        IsReadOnly = true,
        ShowIndexLabels = false,
        DrawScrollView = true,
        MinScrollViewHeight = DebugUi.CompactTableMin,
        MaxScrollViewHeight = DebugUi.CompactTableMax,
        CellPadding = DebugUi.TablePad)]
    List<ThoughtDebugRow> Thoughts
    {
        get
        {
            _thoughtRows.Clear();
            if (_moodHost == null)
                return _thoughtRows;
            IReadOnlyList<MoodThought> thoughts = _moodHost.Thoughts;
            if (thoughts == null)
                return _thoughtRows;
            for (int i = 0; i < thoughts.Count; i++)
                _thoughtRows.Add(new ThoughtDebugRow(thoughts[i]));
            return _thoughtRows;
        }
    }

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Mood))]
    [ShowIf(nameof(HasMood))]
    [BoxGroup("Domain/Mood/Memory")]
    [ShowInInspector]
    [EnumToggleButtons]
    [EnableIf(nameof(HasMood))]
    [LabelText("Memory")]
    ThoughtId MemoryToAdd = ThoughtId.AteMeal;

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Mood))]
    [ShowIf(nameof(HasMood))]
    [BoxGroup("Domain/Mood/Memory")]
    [Button(SdfIconType.PlusCircleFill, "Add Memory")]
    [EnableIf(nameof(HasMood))]
    void AddMemoryThought() => _moodHost?.AddMemory(MemoryToAdd);

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Mood))]
    [ShowIf(nameof(HasMood))]
    [ResponsiveButtonGroup("Domain/Mood/Break")]
    [Button(SdfIconType.SignpostFill, "Begin Wander")]
    [GUIColor(0.95f, 0.7f, 0.3f, 1f)]
    [EnableIf(nameof(HasMood))]
    void DebugBeginWander() => _moodHost?.DebugBeginWander();

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Mood))]
    [ShowIf(nameof(HasMood))]
    [ResponsiveButtonGroup("Domain/Mood/Break")]
    [Button(SdfIconType.CheckCircleFill, "End Break")]
    [GUIColor(0.45f, 0.85f, 0.5f, 1f)]
    [EnableIf(nameof(HasMood))]
    void DebugEndBreak() => _moodHost?.DebugEndBreak(addCatharsis: true);

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Mood))]
    [HideIf(nameof(HasMood))]
    [InfoBox("Mood: possessed player only.", SdfIconType.InfoCircleFill, nameof(AlwaysShow))]
    [ShowInInspector, ReadOnly, HideLabel]
    string MoodUnavailable => "";

    float MoodMin =>
        _moodHost != null && _moodHost.Settings != null
            ? _moodHost.Settings.MoodMin
            : MoodSettings.DefaultMoodMin;

    float MoodMax =>
        _moodHost != null && _moodHost.Settings != null
            ? _moodHost.Settings.MoodMax
            : MoodSettings.DefaultMoodMax;

    Color GetMoodBarColor() => DebugUi.MoodColor(MoodValue, MoodMax);

    // ── Chips (HUD MoodEntry, separate from Mood score) ───────

    [TabGroup(DomainTabs, nameof(CharacterRuntimeDebugDomain.Chips), SdfIconType.TagsFill, TextColor = "lightgreen")]
    [InfoBox("HUD MoodEntry chips — not CharacterMoodHost score.", SdfIconType.InfoCircleFill, nameof(AlwaysShow))]
    [ShowInInspector]
    [LabelText("HUD chips")]
    [TableList(
        AlwaysExpanded = true,
        HideToolbar = true,
        IsReadOnly = true,
        ShowIndexLabels = false,
        DrawScrollView = true,
        MinScrollViewHeight = DebugUi.CompactTableMin,
        MaxScrollViewHeight = DebugUi.ChipTableMax,
        CellPadding = DebugUi.TablePad)]
    List<ChipDebugRow> Chips
    {
        get
        {
            _chipRows.Clear();
            if (!CanWrite)
                return _chipRows;

            PlayerEncumbranceStage stage = _encumbrance != null
                ? _encumbrance.Stage
                : PlayerEncumbranceStage.None;
            PlayerNeedsHost needs = IsPossessed ? _needsHost : null;
            PlayerStatusMoodEntries.Collect(Body, Vitals, stage, needs, _chipScratch);
            for (int i = 0; i < _chipScratch.Count; i++)
                _chipRows.Add(new ChipDebugRow(_chipScratch[i]));
            return _chipRows;
        }
    }

    // ── nested rows ──────────────────────────────────────────

    [Serializable]
    public sealed class BodyPartDebugRow
    {
        readonly CharacterRuntimeDebugModel _owner;
        readonly string _partId;

        public BodyPartDebugRow(CharacterRuntimeDebugModel owner, string partId)
        {
            _owner = owner;
            _partId = partId;
        }

        bool CanEdit => _owner != null && _owner.CanWrite;

        bool Present => _owner.Body != null && _owner.Body.Has(_partId);

        bool CanSever => CanEdit && Present && BodyPartIds.IsSeverable(_partId);

        bool CanRestore => CanEdit && !Present && BodyPartIds.IsSeverable(_partId);

        [ShowInInspector, DisplayAsString]
        [TableColumnWidth(DebugUi.IdCol, true)]
        public string Id => _partId;

        [ShowInInspector, DisplayAsString, HideLabel]
        [GUIColor("@Present ? \"lightgreen\" : \"gray\"")]
        [TableColumnWidth(DebugUi.MarkCol, false)]
        public string PresentMark => Present ? "●" : "○";

        [ShowInInspector, ReadOnly]
        [ProgressBar(0, 1, ColorGetter = nameof(GetPainColor))]
        [TableColumnWidth(DebugUi.MeterCol, true)]
        [LabelText("Pain")]
        public float Pain01 =>
            _owner.Body != null && Present
                ? CombatPain.PartPain01(_owner.Body, _partId)
                : 0f;

        [ShowInInspector]
        [TableColumnWidth(DebugUi.StatCol, false)]
        [EnableIf(nameof(CanEdit))]
        public int Cur
        {
            get => _owner.Body != null ? _owner.Body.GetConditionCur(_partId) : 0;
            set
            {
                if (!_owner.CanWrite)
                    return;
                ICharacterBody body = _owner.Body;
                if (body == null || !body.Has(_partId))
                    return;
                BodyInjury.SetCur(body, _partId, value);
            }
        }

        [ShowInInspector]
        [TableColumnWidth(DebugUi.StatCol, false)]
        [EnableIf(nameof(CanEdit))]
        public int Max
        {
            get => _owner.Body != null ? _owner.Body.GetConditionMax(_partId) : 0;
            set
            {
                if (!_owner.CanWrite)
                    return;
                ICharacterBody body = _owner.Body;
                if (body == null || !body.Has(_partId))
                    return;
                int cur = body.GetConditionCur(_partId);
                body.SetCondition(_partId, cur, value);
                BodyInjury.SyncPart(body, _partId);
            }
        }

        [Button(SdfIconType.Scissors, "Sever")]
        [TableColumnWidth(DebugUi.BtnCol, false)]
        [EnableIf(nameof(CanSever))]
        [GUIColor(0.95f, 0.4f, 0.35f, 1f)]
        void Sever()
        {
            if (!_owner.CanWrite || !Present || !BodyPartIds.IsSeverable(_partId))
                return;
            _owner.Body?.RemovePart(_partId);
        }

        [Button(SdfIconType.HeartFill, "Regen")]
        [TableColumnWidth(DebugUi.BtnCol, false)]
        [EnableIf(nameof(CanRestore))]
        [GUIColor(0.45f, 0.85f, 0.5f, 1f)]
        void Regen()
        {
            if (!_owner.CanWrite || _owner.Body == null || Present || !BodyPartIds.IsSeverable(_partId))
                return;
            BodyPartRestoreService.TryRegenerate(_owner.Body, _partId);
        }

        [Button(SdfIconType.Wrench, "Prosthetic")]
        [TableColumnWidth(DebugUi.BtnColWide, false)]
        [EnableIf(nameof(CanRestore))]
        void Prosthetic()
        {
            if (!_owner.CanWrite || _owner.Body == null || Present || !BodyPartIds.IsSeverable(_partId))
                return;
            BodyPartRestoreService.TryAttachProsthetic(_owner.Body, _partId);
        }

        Color GetPainColor() => DebugUi.PainColor(Pain01);
    }

    [Serializable]
    public sealed class ThermalPartDebugRow
    {
        readonly CharacterRuntimeDebugModel _owner;
        readonly string _partId;

        public ThermalPartDebugRow(CharacterRuntimeDebugModel owner, string partId)
        {
            _owner = owner;
            _partId = partId;
        }

        bool CanEdit => _owner != null && _owner.HasClimate;

        bool Tracked =>
            _owner._climateHost != null
            && _owner._climateHost.BodyTemperature.IsPartTracked(_partId);

        [ShowInInspector, DisplayAsString]
        [TableColumnWidth(DebugUi.IdCol, true)]
        public string Id => _partId;

        [ShowInInspector, DisplayAsString, HideLabel]
        [GUIColor("@Tracked ? \"lightgreen\" : \"gray\"")]
        [TableColumnWidth(DebugUi.MarkCol, false)]
        public string TrackedMark => Tracked ? "●" : "○";

        [ShowInInspector]
        [SuffixLabel("°C", Overlay = true)]
        [TableColumnWidth(DebugUi.MeterCol, true)]
        [EnableIf(nameof(CanEdit))]
        [GUIColor(nameof(GetTempColor))]
        public float TempC
        {
            get
            {
                if (_owner._climateHost == null)
                    return 0f;
                return _owner._climateHost.BodyTemperature.TryGetPartTempC(_partId, out float t)
                    ? t
                    : 0f;
            }
            set
            {
                if (!_owner.HasClimate)
                    return;
                _owner._climateHost.BodyTemperature.SetPartTempC(_partId, value);
            }
        }

        Color GetTempColor()
        {
            float t = TempC;
            if (t <= DebugUi.TempColdC)
                return DebugUi.TempCold;
            if (t >= DebugUi.TempHotC)
                return DebugUi.TempHot;
            if (t < DebugUi.TempComfortC)
                return Color.Lerp(DebugUi.TempCold, DebugUi.TempOk, Mathf.InverseLerp(DebugUi.TempColdC, DebugUi.TempComfortC, t));
            return Color.Lerp(DebugUi.TempOk, DebugUi.TempHot, Mathf.InverseLerp(DebugUi.TempComfortC, DebugUi.TempHotC, t));
        }
    }

    [Serializable]
    public sealed class SkillDebugRow
    {
        readonly CharacterRuntimeDebugModel _owner;
        readonly string _id;
        readonly bool _isAttribute;

        public SkillDebugRow(CharacterRuntimeDebugModel owner, string id, bool isAttribute)
        {
            _owner = owner;
            _id = id;
            _isAttribute = isAttribute;
        }

        bool CanEdit => _owner != null && _owner.CanWrite;

        bool IsAttribute => _isAttribute;

        [ShowInInspector, DisplayAsString]
        [TableColumnWidth(DebugUi.SkillIdCol, true)]
        public string Id => _id;

        [ShowInInspector]
        [TableColumnWidth(DebugUi.StatCol, false)]
        [EnableIf(nameof(CanEdit))]
        public int Base
        {
            get => _owner.Skills != null ? _owner.Skills.BaseLevel(_id) : 0;
            set
            {
                if (!_owner.CanWrite)
                    return;
                _owner.Skills?.SetBaseLevel(_id, value);
            }
        }

        [ShowInInspector, ReadOnly]
        [TableColumnWidth(DebugUi.StatCol, false)]
        [GUIColor("@Buffed > Base ? \"lightgreen\" : \"white\"")]
        public int Buffed => _owner.Skills != null ? _owner.Skills.Level(_id) : 0;

        [ShowInInspector]
        [HideIf(nameof(IsAttribute))]
        [ProgressBar(SkillGrowth.MinPotential, SkillGrowth.MaxPotential)]
        [TableColumnWidth(DebugUi.MeterCol, true)]
        [EnableIf(nameof(CanEdit))]
        public int Potential
        {
            get => _owner.Skills != null ? _owner.Skills.Potential(_id) : 0;
            set
            {
                if (!_owner.CanWrite)
                    return;
                _owner.Skills?.SetPotential(_id, value);
            }
        }

        [ShowInInspector, ReadOnly]
        [HideIf(nameof(IsAttribute))]
        [TableColumnWidth(DebugUi.StatCol, false)]
        public int XP => _owner.Skills != null ? _owner.Skills.Experience(_id) : 0;

        [Button(SdfIconType.Plus, "+10 XP")]
        [HideIf(nameof(IsAttribute))]
        [TableColumnWidth(DebugUi.BtnColWide, false)]
        [EnableIf(nameof(CanEdit))]
        void Practice10() => _owner.Skills?.AddPractice(_id, 10);
    }

    [Serializable]
    public sealed class EffectDebugRow
    {
        public EffectDebugRow(BodyPartEffect effect)
        {
            EffectId = effect.EffectId;
            Intensity = effect.Intensity;
            RemainingSeconds = effect.RemainingSeconds;
        }

        [ShowInInspector, DisplayAsString]
        [TableColumnWidth(DebugUi.IdCol, true)]
        public string EffectId { get; }

        [ShowInInspector, DisplayAsString]
        [TableColumnWidth(DebugUi.StatCol, false)]
        public int Intensity { get; }

        [ShowInInspector, DisplayAsString]
        [TableColumnWidth(DebugUi.MeterCol, true)]
        public string Remaining =>
            RemainingSeconds < 0f ? "∞" : RemainingSeconds.ToString("0.0s");

        float RemainingSeconds { get; }
    }

    [Serializable]
    public sealed class ThoughtDebugRow
    {
        public ThoughtDebugRow(MoodThought thought)
        {
            Id = thought.Id;
            Kind = thought.Kind;
            Offset = thought.Offset;
            RemainingMinutes = thought.RemainingMinutes;
        }

        [ShowInInspector, DisplayAsString]
        [TableColumnWidth(DebugUi.IdCol, true)]
        public ThoughtId Id { get; }

        [ShowInInspector, DisplayAsString]
        [TableColumnWidth(DebugUi.IdCol, true)]
        public MoodThoughtKind Kind { get; }

        [ShowInInspector, DisplayAsString]
        [GUIColor("@Offset < 0 ? \"orange\" : Offset > 0 ? \"lightgreen\" : \"gray\"")]
        [TableColumnWidth(DebugUi.StatCol, false)]
        public int Offset { get; }

        [ShowInInspector, DisplayAsString]
        [SuffixLabel("min")]
        [TableColumnWidth(DebugUi.MeterCol, true)]
        public int RemainingMinutes { get; }
    }

    [Serializable]
    public sealed class ChipDebugRow
    {
        public ChipDebugRow(MoodEntry entry)
        {
            IconId = entry.IconId;
            Polarity = entry.Polarity;
            Intensity = entry.Intensity;
            Tooltip = entry.TooltipText;
        }

        [ShowInInspector, DisplayAsString]
        [TableColumnWidth(DebugUi.IdCol, true)]
        public MoodIconId IconId { get; }

        [ShowInInspector, DisplayAsString]
        [GUIColor(nameof(GetPolarityColor))]
        [TableColumnWidth(DebugUi.IdCol, false)]
        public MoodPolarity Polarity { get; }

        [ShowInInspector, ReadOnly]
        [ProgressBar(0, 1, ColorGetter = nameof(GetPolarityColor))]
        [TableColumnWidth(DebugUi.MeterCol, true)]
        public float Intensity { get; }

        [ShowInInspector, DisplayAsString(false)]
        public string Tooltip { get; }

        Color GetPolarityColor()
        {
            switch (Polarity)
            {
                case MoodPolarity.Positive: return DebugUi.Alive;
                case MoodPolarity.Negative: return DebugUi.Dead;
                default: return DebugUi.Neutral;
            }
        }
    }

    /// <summary>에디터 디버그 창 레이아웃·색 SSOT. 게임플레이 수치 아님.</summary>
    public static class DebugUi
    {
        public const int MeterHeight = 18;
        public const int TablePad = 3;
        public const int BodyTableMin = 260;
        public const int BodyTableMax = 480;
        public const int CompactTableMin = 88;
        public const int CompactTableMax = 200;
        public const int ChipTableMax = 280;
        public const int SkillTableMin = 180;
        public const int SkillTableMax = 340;
        public const int SkillPageSize = 12;
        public const int IdCol = 110;
        public const int SkillIdCol = 140;
        public const int MarkCol = 28;
        public const int MeterCol = 90;
        public const int StatCol = 48;
        public const int BtnCol = 64;
        public const int BtnColWide = 88;

        public const float TempColdC = 32f;
        public const float TempComfortC = 37f;
        public const float TempHotC = 40f;

        public static readonly Color Alive = new Color(0.55f, 0.85f, 0.5f);
        public static readonly Color Dead = new Color(1f, 0.35f, 0.3f);
        public static readonly Color Neutral = new Color(0.7f, 0.7f, 0.7f);
        public static readonly Color Blood = new Color(0.75f, 0.15f, 0.18f);
        public static readonly Color Toxin = new Color(0.55f, 0.25f, 0.75f);
        public static readonly Color Infection = new Color(0.35f, 0.7f, 0.3f);
        public static readonly Color Immunity = new Color(0.45f, 0.75f, 0.95f);
        public static readonly Color Pain = new Color(0.95f, 0.45f, 0.15f);
        public static readonly Color Hunger = new Color(0.85f, 0.55f, 0.2f);
        public static readonly Color Thirst = new Color(0.35f, 0.7f, 0.95f);
        public static readonly Color Stamina = new Color(0.9f, 0.82f, 0.25f);
        public static readonly Color Fatigue = new Color(0.55f, 0.5f, 0.7f);
        public static readonly Color Sleep = new Color(0.4f, 0.5f, 0.85f);
        public static readonly Color Wet = new Color(0.3f, 0.75f, 0.85f);
        public static readonly Color Capacity = new Color(0.6f, 0.85f, 0.7f);
        public static readonly Color Imbalance = new Color(0.95f, 0.75f, 0.25f);
        public static readonly Color MoodGood = new Color(0.45f, 0.85f, 0.4f);
        public static readonly Color MoodBad = new Color(0.95f, 0.4f, 0.35f);
        public static readonly Color TempCold = new Color(0.45f, 0.7f, 1f);
        public static readonly Color TempCool = new Color(0.55f, 0.85f, 0.95f);
        public static readonly Color TempOk = new Color(0.55f, 0.85f, 0.5f);
        public static readonly Color TempWarm = new Color(1f, 0.7f, 0.25f);
        public static readonly Color TempHot = new Color(1f, 0.35f, 0.3f);

        public static Color PainColor(float pain01) =>
            Color.Lerp(TempOk, Pain, Mathf.Clamp01(pain01));

        public static Color BloodColor(float blood01) =>
            Color.Lerp(Dead, Blood, Mathf.Clamp01(blood01));

        public static Color MoodColor(float mood, float max)
        {
            float t = max > 0f ? Mathf.Clamp01(mood / max) : 0f;
            return Color.Lerp(MoodBad, MoodGood, t);
        }
    }
}
