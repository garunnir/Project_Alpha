// ============================================================
// PlayerNeedsHost — 플레이어 위장·저장 kcal·갈증·수면 피로 분 틱 + 섭취/대사 API
// ============================================================
// WorldClock.MinuteChanged
//   → digest mlWater (fast) / mlFood / kcal→stored
//   → burn stored * activity (Sprint > Busy > Walk > Idle)
//   → drain thirst
//   → fatigue/debt: awake saturating rise / sleep exp decay
//   → rot scan possessed + open containers
//   → stored/thirst <=0 → chest ApplyHit (파괴 출혈 → 과다출혈 BodyFatal)
//   → every N world hours → needs warning events
// ConsumeService → IngestFood / IngestDrink / ApplyMetabolites / RotToxin / MedIllnessRelief
// Ingest overflow → Bloated; ingest while bloated → vomit + OvereatHit
// TrySleep / Wake — 기립 휴식. 이동·행동·ESC면 기상. 의식 공식에 안 곱음.

using System;
using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerNeedsHost : MonoBehaviour, IUiCancelConsumer
{
    [SerializeField] PlayerNeedsSettings _settings;

    CharacterMotor _motor;
    CharacterActionHost _actionHost;
    PlayerInventoryHost _inventoryHost;
    WorldClock _clock;
    bool _clockSubscribed;
    readonly List<BodyPartEffect> _effectScratch = new(8);

    float _mlFood;
    float _mlWater;
    float _kcal;
    float _kcalBurnAcc;
    float _kcalAbsorbAcc;
    float _thirstDrainAcc;
    int _minutesSinceNeedsWarning;
    bool _needsFatalApplied;

    int _fun;
    int _healthy;
    int _stim;
    string _addictionType;
    int _addictionPotential;
    Dictionary<string, int> _vitamins;

    float _fatigue01;
    float _sleepDebt01;
    bool _isSleeping;

    public static PlayerNeedsHost Active => PlayerPossessSession.NeedsHost;

    public static event Action AnyNeedsVomit;
    public static event Action<NeedsFatalKind> AnyNeedsFatal;
    public static event Action<NeedsWarningKind> AnyNeedsWarning;

    public event Action Changed;

    public PlayerNeedsSettings Settings => _settings;
    public float StomachMlFood => _mlFood;
    public float StomachMlWater => _mlWater;
    public float StomachKcal => _kcal;
    public float StomachUsedMl => _mlFood + _mlWater;
    public int Fun => _fun;
    public int Healthy => _healthy;
    public int Stim => _stim;
    public string AddictionType => _addictionType;
    public int AddictionPotential => _addictionPotential;
    public float Fatigue01 => _fatigue01;
    public float SleepDebt01 => _sleepDebt01;
    public bool IsSleeping => _isSleeping;
    public int CancelPriority => UiCancelPriority.CharacterAction;

    public float PerceivedFatigue01
    {
        get
        {
            float mask = ResolveStimFatigueMask();
            float value = _fatigue01 * (1f - mask);
            if (value < 0f)
                return 0f;
            return value > 1f ? 1f : value;
        }
    }

    public float SleepDisplay01
    {
        get
        {
            float perceived = PerceivedFatigue01;
            return perceived > _sleepDebt01 ? perceived : _sleepDebt01;
        }
    }

    public void ClaimActive() { }

    void Awake()
    {
        _motor = CharacterBodyResolve.GetInBody<CharacterMotor>(this);
        TryGetComponent(out _actionHost);
        TryGetComponent(out _inventoryHost);
    }

    void OnEnable()
    {
        SubscribeClock();
        UiCancelRouter.Register(this);
        if (IsPlayerBody())
            BindPlayer();
    }

    void Start()
    {
        SubscribeClock();
        if (!IsPlayerBody())
            return;

        if (_settings == null)
            Debug.LogError("[PlayerNeedsHost] PlayerNeedsSettings is missing.", this);
        BindPlayer();
    }

    void OnDisable()
    {
        UiCancelRouter.Unregister(this);
        UnsubscribeClock();
        if (_isSleeping)
            _isSleeping = false;
    }

    void Update()
    {
        // Hot path: wake checks only. No alloc/LINQ/string.
        if (!_isSleeping || !IsPlayerBody())
            return;

        if (_actionHost != null && _actionHost.IsBusy)
        {
            Wake();
            return;
        }

        float walkMin = _settings != null
            ? _settings.WalkActivitySpeedMin
            : PlayerNeedsSettings.DefaultWalkActivitySpeedMin;
        if (_motor != null && _motor.CurrentSpeed >= walkMin)
            Wake();
    }

    /// <summary>S3 ConsumeService: food volume + calories into stomach. Overflow discarded.</summary>
    public NeedsIngestResult IngestFood(float ml, float kcal)
    {
        return IngestStomach(ml, 0f, kcal, 0);
    }

    /// <summary>S3 ConsumeService: drink volume into stomach + quench on thirst. Overflow ml discarded.</summary>
    public NeedsIngestResult IngestDrink(float ml, int quench)
    {
        return IngestStomach(0f, ml, 0f, quench);
    }

    /// <summary>Cap is mlFood+mlWater and stored+stomach kcal. Overflow discarded + Bloated.</summary>
    public NeedsIngestResult IngestStomach(float mlFood, float mlWater, float kcal, int quench)
    {
        if (mlFood < 0f)
            mlFood = 0f;
        if (mlWater < 0f)
            mlWater = 0f;
        if (kcal < 0f)
            kcal = 0f;

        if (HasBloated() && mlFood + mlWater + kcal > 0f)
            return ApplyVomit();

        float wantMl = mlFood + mlWater;
        float room = StomachRoomMl();
        float acceptScale = 1f;
        bool mlOverflow = wantMl > room;
        if (mlOverflow)
            acceptScale = wantMl > 0f ? room / wantMl : 0f;

        float acceptedFood = mlFood * acceptScale;
        float acceptedWater = mlWater * acceptScale;
        float acceptedKcal = kcal * acceptScale;

        float kcalRoom = StoredStomachKcalRoom();
        bool kcalOverflow = acceptedKcal > kcalRoom;
        if (kcalOverflow)
            acceptedKcal = kcalRoom > 0f ? kcalRoom : 0f;

        _mlFood += acceptedFood;
        _mlWater += acceptedWater;
        _kcal += acceptedKcal;

        if (quench != 0)
            AddThirst(quench);

        bool overflowed = mlOverflow || kcalOverflow;
        if (overflowed)
            TryApplyBloated();

        var result = new NeedsIngestResult(
            acceptedFood,
            mlFood - acceptedFood,
            acceptedWater,
            mlWater - acceptedWater,
            acceptedKcal,
            kcal - acceptedKcal,
            overflowed);

        RaiseChanged();
        return result;
    }

    /// <summary>S3 ConsumeService: fun/healthy/stim deltas. Host stores; tick decay is later.</summary>
    public void ApplyMetabolites(int funDelta, int healthyDelta, int stimDelta)
    {
        _fun += funDelta;
        _healthy += healthyDelta;
        _stim += stimDelta;
        RaiseChanged();
    }

    /// <summary>디버그/치트용. Fun/Healthy/Stim 절대값.</summary>
    public void SetMetabolites(int fun, int healthy, int stim)
    {
        _fun = fun;
        _healthy = healthy;
        _stim = stim;
        RaiseChanged();
    }

    public void SetFatigue01(float value)
    {
        float next = Mathf.Clamp01(value);
        if (Mathf.Abs(next - _fatigue01) < 1e-6f)
            return;
        _fatigue01 = next;
        RaiseChanged();
    }

    public void SetSleepDebt01(float value)
    {
        float next = Mathf.Clamp01(value);
        if (Mathf.Abs(next - _sleepDebt01) < 1e-6f)
            return;
        _sleepDebt01 = next;
        RaiseChanged();
    }

    public void ApplyAddiction(string type, int potential)
    {
        if (string.IsNullOrEmpty(type) || potential == 0)
            return;

        if (_addictionType == type)
            _addictionPotential += potential;
        else
        {
            _addictionType = type;
            _addictionPotential = potential;
        }

        RaiseChanged();
    }

    public void AddVitamin(string id, int amount)
    {
        if (string.IsNullOrEmpty(id) || amount == 0)
            return;

        _vitamins ??= new Dictionary<string, int>(StringComparer.Ordinal);
        _vitamins.TryGetValue(id, out int current);
        _vitamins[id] = current + amount;
        RaiseChanged();
    }

    public int GetVitamin(string id)
    {
        if (string.IsNullOrEmpty(id) || _vitamins == null)
            return 0;
        return _vitamins.TryGetValue(id, out int value) ? value : 0;
    }

    public void SetStomach(float mlFood, float mlWater, float kcal)
    {
        if (mlFood < 0f)
            mlFood = 0f;
        if (mlWater < 0f)
            mlWater = 0f;
        if (kcal < 0f)
            kcal = 0f;

        float cap = _settings != null ? _settings.StomachCapacityMl : 0f;
        float total = mlFood + mlWater;
        if (total > cap && total > 0f)
        {
            float scale = cap / total;
            mlFood *= scale;
            mlWater *= scale;
            kcal *= scale;
        }

        _mlFood = mlFood;
        _mlWater = mlWater;
        _kcal = kcal;
        RaiseChanged();
    }

    void OnMinuteChanged()
    {
        // Hot path: one world minute, no LINQ/closures/string.
        if (!IsPlayerBody())
            return;

        BindPlayer();
        if (_settings == null)
            return;

        IPlayerVitals vitals = GameplayData.Vitals;
        if (vitals == null)
            return;

        bool changed = false;
        DigestStomach(vitals, ref changed);
        BurnStored(vitals, ref changed);
        DrainThirst(vitals, ref changed);
        TickSleepPressure(ref changed);
        ScanPossessedAndOpenRot();
        bool fatalThisTick = TryApplyNeedsFatal(vitals);
        if (!fatalThisTick)
            TickNeedsWarnings(vitals);

        if (changed)
            Changed?.Invoke();
    }

    void DigestStomach(IPlayerVitals vitals, ref bool changed)
    {
        float water = _mlWater < _settings.WaterDigestMlPerMinute
            ? _mlWater
            : _settings.WaterDigestMlPerMinute;
        if (water > 0f)
        {
            _mlWater -= water;
            changed = true;
        }

        float food = _mlFood < _settings.FoodDigestMlPerMinute
            ? _mlFood
            : _settings.FoodDigestMlPerMinute;
        if (food > 0f)
        {
            _mlFood -= food;
            changed = true;
        }

        float kcalWant = _kcal < _settings.DigestKcalPerMinute
            ? _kcal
            : _settings.DigestKcalPerMinute;
        if (kcalWant <= 0f)
            return;

        int storedMax = vitals.GetMax(VitalKeys.Hunger);
        int stored = vitals.GetCurrent(VitalKeys.Hunger);
        float room = storedMax - stored;
        if (room <= 0f)
            return;

        float kcalMove = kcalWant < room ? kcalWant : room;
        _kcal -= kcalMove;
        _kcalAbsorbAcc += kcalMove;
        int absorb = (int)_kcalAbsorbAcc;
        if (absorb > 0)
        {
            _kcalAbsorbAcc -= absorb;
            vitals.SetCurrent(VitalKeys.Hunger, stored + absorb);
        }

        changed = true;
    }

    void BurnStored(IPlayerVitals vitals, ref bool changed)
    {
        int minutesPerDay = ResolveMinutesPerDay();
        float perMinute = _settings.DailyKcalBurn / (float)minutesPerDay;
        _kcalBurnAcc += perMinute * ResolveActivityMul();
        int burn = (int)_kcalBurnAcc;
        if (burn <= 0)
            return;

        _kcalBurnAcc -= burn;
        int stored = vitals.GetCurrent(VitalKeys.Hunger);
        int next = stored - burn;
        if (next < 0)
            next = 0;
        if (next == stored)
            return;

        vitals.SetCurrent(VitalKeys.Hunger, next);
        changed = true;
    }

    void DrainThirst(IPlayerVitals vitals, ref bool changed)
    {
        int minutesPerDay = ResolveMinutesPerDay();
        _thirstDrainAcc += _settings.DailyThirstDrain / minutesPerDay;
        int drain = (int)_thirstDrainAcc;
        if (drain <= 0)
            return;

        _thirstDrainAcc -= drain;
        int current = vitals.GetCurrent(VitalKeys.Thirst);
        int next = current - drain;
        if (next < 0)
            next = 0;
        if (next == current)
            return;

        vitals.SetCurrent(VitalKeys.Thirst, next);
        changed = true;
    }

    public bool TrySleep()
    {
        if (!IsPlayerBody() || _isSleeping)
            return false;

        _actionHost?.CancelAll();
        _isSleeping = true;
        RaiseChanged();
        return true;
    }

    public void Wake()
    {
        if (!_isSleeping)
            return;

        _isSleeping = false;
        RaiseChanged();
    }

    public bool TryHandleCancel()
    {
        if (!IsPlayerBody() || !_isSleeping)
            return false;

        Wake();
        return true;
    }

    void TickSleepPressure(ref bool changed)
    {
        if (_settings == null)
            return;

        const float dtMinutes = 1f;
        int minutesPerDay = ResolveMinutesPerDay();
        float nextFatigue;
        float nextDebt;
        if (_isSleeping)
        {
            nextFatigue = ExpDecay01(_fatigue01, _settings.FatigueSleepTauMinutes, dtMinutes);
            nextDebt = ExpDecay01(_sleepDebt01, _settings.DebtSleepTauMinutes, dtMinutes);
        }
        else
        {
            float activityMul = ResolveActivityMul();
            if (activityMul < 0.01f)
                activityMul = 0.01f;
            float wakeTau = _settings.FatigueWakeTauMinutes / activityMul;
            nextFatigue = SaturatingRise01(_fatigue01, wakeTau, dtMinutes);
            nextDebt = SaturatingRise01(
                _sleepDebt01,
                _settings.DebtWakeTauMinutes(minutesPerDay),
                dtMinutes);
        }

        if (Mathf.Abs(nextFatigue - _fatigue01) < 0.0001f
            && Mathf.Abs(nextDebt - _sleepDebt01) < 0.0001f)
            return;

        _fatigue01 = nextFatigue;
        _sleepDebt01 = nextDebt;
        changed = true;
    }

    float ResolveStimFatigueMask()
    {
        if (_settings == null)
            return 0f;

        int gate = _settings.RotFunPenalty;
        if (gate < 0)
            gate = -gate;
        if (gate <= 0 || _stim < gate)
            return 0f;
        return _settings.StimFatigueMask;
    }

    static float SaturatingRise01(float current, float tauMinutes, float dtMinutes)
    {
        if (dtMinutes <= 0f)
            return current < 0f ? 0f : current > 1f ? 1f : current;
        if (tauMinutes <= 0f)
            return 1f;

        float remain = 1f - current;
        if (remain <= 0f)
            return 1f;
        float next = 1f - remain * Mathf.Exp(-dtMinutes / tauMinutes);
        if (next < 0f)
            return 0f;
        return next > 1f ? 1f : next;
    }

    static float ExpDecay01(float current, float tauMinutes, float dtMinutes)
    {
        if (current <= 0f || dtMinutes <= 0f)
            return current < 0f ? 0f : current;
        if (tauMinutes <= 0f)
            return 0f;

        float next = current * Mathf.Exp(-dtMinutes / tauMinutes);
        return next < 0.0001f ? 0f : next;
    }

    float ResolveActivityMul()
    {
        if (_motor != null && _motor.IsSprinting)
            return _settings.ActivityMulSprint;
        if (_actionHost != null && _actionHost.IsBusy)
            return _settings.ActivityMulBusy;
        if (_motor != null && _motor.CurrentSpeed >= _settings.WalkActivitySpeedMin)
            return _settings.ActivityMulWalk;
        return _settings.ActivityMulIdle;
    }

    int ResolveMinutesPerDay()
    {
        if (_clock != null && _clock.Settings != null)
            return _clock.Settings.MinutesPerDay;
        return WorldClockSettings.DefaultMinutesPerDay;
    }

    float StomachRoomMl()
    {
        float cap = _settings != null ? _settings.StomachCapacityMl : 0f;
        float room = cap - StomachUsedMl;
        return room > 0f ? room : 0f;
    }

    float StoredStomachKcalRoom()
    {
        int storedMax = _settings != null ? _settings.MaxStoredKcal : 0;
        IPlayerVitals vitals = GameplayData.Vitals;
        int stored = vitals != null ? vitals.GetCurrent(VitalKeys.Hunger) : 0;
        float room = storedMax - stored - _kcal;
        return room > 0f ? room : 0f;
    }

    void AddThirst(int delta)
    {
        IPlayerVitals vitals = GameplayData.Vitals;
        if (vitals == null)
            return;
        vitals.SetCurrent(VitalKeys.Thirst, vitals.GetCurrent(VitalKeys.Thirst) + delta);
    }

    NeedsIngestResult ApplyVomit()
    {
        float keep = 1f;
        if (_settings != null)
            keep = 1f - _settings.VomitStomachFraction;

        _mlFood *= keep;
        _mlWater *= keep;
        _kcal *= keep;

        ICharacterBody body = PlayerPossessSession.SessionBody;
        int hit = _settings != null ? _settings.OvereatHit : PlayerNeedsSettings.DefaultOvereatHit;
        if (body != null && hit > 0)
            BodyDamageService.ApplyHit(body, BodyPartIds.Chest, hit);

        AnyNeedsVomit?.Invoke();
        RaiseChanged();
        return new NeedsIngestResult(0f, 0f, 0f, 0f, 0f, 0f, overflowed: true);
    }

    void TryApplyBloated()
    {
        ICharacterBody body = PlayerPossessSession.SessionBody;
        if (body == null || HasBloated(body))
            return;

        float seconds = ResolveBloatDurationSeconds();
        float remaining = seconds > 0f ? seconds : -1f;
        body.AddEffect(BodyPartIds.Chest, new BodyPartEffect(BodyPartEffectIds.Bloated, 1, remaining));
    }

    float ResolveBloatDurationSeconds()
    {
        int minutes = _settings != null
            ? _settings.BloatWorldMinutes
            : PlayerNeedsSettings.DefaultBloatWorldMinutes;
        if (minutes <= 0)
            return 0f;

        float rate = WorldClockSettings.DefaultWorldMinutesPerRealtimeSecond;
        if (_clock != null && _clock.Settings != null)
            rate = _clock.Settings.WorldMinutesPerRealtimeSecond;
        if (rate <= 0f)
            rate = WorldClockSettings.DefaultWorldMinutesPerRealtimeSecond;

        return minutes / rate;
    }

    bool HasBloated() => HasBloated(PlayerPossessSession.SessionBody);

    bool HasBloated(ICharacterBody body)
    {
        if (body == null || !body.Has(BodyPartIds.Chest))
            return false;

        _effectScratch.Clear();
        body.CollectEffectsUnder(BodyPartIds.Chest, _effectScratch, includeDescendants: false);
        for (int i = 0; i < _effectScratch.Count; i++)
        {
            if (_effectScratch[i].EffectId == BodyPartEffectIds.Bloated)
                return true;
        }

        return false;
    }

    void ScanPossessedAndOpenRot()
    {
        int worldMinute = ItemRot.CurrentWorldMinute();
        PlayerInventoryRuntime runtime = PlayerInventoryRuntime.Active;
        InventoryContainer possessed = _inventoryHost != null
            ? _inventoryHost.Container
            : runtime != null ? runtime.Host != null ? runtime.Host.Container : null : null;
        ScanContainerRot(possessed, worldMinute);

        if (runtime == null || !runtime.IsInventoryContextActive)
            return;

        InventoryContainer open = runtime.LootProximity.ActiveContainer;
        if (open == null || open == possessed)
            return;

        ScanContainerRot(open, worldMinute);
    }

    static void ScanContainerRot(InventoryContainer container, int worldMinute)
    {
        if (container == null)
            return;

        IReadOnlyList<ItemStack> stacks = container.Stacks;
        for (int i = 0; i < stacks.Count; i++)
        {
            ItemStack stack = stacks[i];
            if (stack?.Instance == null)
                continue;

            ItemRot.TryStampCreated(stack.Instance, worldMinute);
            stack.Instance.SetRotten(ItemRot.IsRotten(stack.Instance, worldMinute));
            if (stack.Nested != null)
                ScanContainerRot(stack.Nested, worldMinute);
        }
    }

    bool TryApplyNeedsFatal(IPlayerVitals vitals)
    {
        if (_needsFatalApplied)
            return false;

        int stored = vitals.GetCurrent(VitalKeys.Hunger);
        int thirst = vitals.GetCurrent(VitalKeys.Thirst);
        if (stored > 0 && thirst > 0)
            return false;

        _needsFatalApplied = true;
        NeedsFatalKind kind = stored <= 0 ? NeedsFatalKind.Starve : NeedsFatalKind.Dehydrate;
        AnyNeedsFatal?.Invoke(kind);

        ICharacterBody body = PlayerPossessSession.SessionBody;
        if (body != null && body.Has(BodyPartIds.Chest))
        {
            int chest = body.GetConditionCur(BodyPartIds.Chest);
            if (chest > 0)
                BodyDamageService.ApplyHit(body, BodyPartIds.Chest, chest);
        }

        return true;
    }

    void TickNeedsWarnings(IPlayerVitals vitals)
    {
        if (_needsFatalApplied)
            return;

        int hours = _settings.WarningIntervalWorldHours;
        if (hours <= 0)
            return;

        int interval = hours * (ResolveMinutesPerDay() / 24);
        if (interval < 1)
            interval = 1;

        _minutesSinceNeedsWarning++;
        if (_minutesSinceNeedsWarning < interval)
            return;

        _minutesSinceNeedsWarning = 0;

        int stored = vitals.GetCurrent(VitalKeys.Hunger);
        int storedMax = vitals.GetMax(VitalKeys.Hunger);
        int thirst = vitals.GetCurrent(VitalKeys.Thirst);
        int thirstMax = vitals.GetMax(VitalKeys.Thirst);

        if (stored > 0 && storedMax > 0)
        {
            int pct = stored * 100 / storedMax;
            if (pct < _settings.WarningKcalPct10)
                AnyNeedsWarning?.Invoke(NeedsWarningKind.Hunger10);
            else if (pct < _settings.WarningKcalPct25)
                AnyNeedsWarning?.Invoke(NeedsWarningKind.Hunger25);
            else if (pct < _settings.WarningKcalPct50)
                AnyNeedsWarning?.Invoke(NeedsWarningKind.Hunger50);
            else if (pct < _settings.WarningKcalPct70)
                AnyNeedsWarning?.Invoke(NeedsWarningKind.Hunger70);
        }

        if (thirst > 0 && thirstMax > 0)
        {
            float ratio = thirst / (float)thirstMax;
            if (ratio <= _settings.MoodVeryThirstyRatio)
                AnyNeedsWarning?.Invoke(NeedsWarningKind.ThirstDanger);
        }
    }

    void BindPlayer()
    {
        ClaimActive();
        SyncHungerMax();
    }

    void SyncHungerMax()
    {
        if (_settings == null)
            return;

        IPlayerVitals vitals = GameplayData.Vitals;
        if (vitals == null)
            return;

        int max = _settings.MaxStoredKcal;
        int oldMax = vitals.GetMax(VitalKeys.Hunger);
        if (oldMax == max)
            return;

        int oldCur = vitals.GetCurrent(VitalKeys.Hunger);
        bool wasFull = oldMax > 0 && oldCur >= oldMax;
        vitals.SetMax(VitalKeys.Hunger, max);
        if (wasFull)
            vitals.SetCurrent(VitalKeys.Hunger, max);
    }

    bool IsPlayerBody() => _motor != null && _motor.IsPossessed;

    void SubscribeClock()
    {
        if (_clockSubscribed)
            return;

        WorldClock clock = WorldClock.Instance;
        if (clock == null)
            return;

        _clock = clock;
        _clock.MinuteChanged += OnMinuteChanged;
        _clockSubscribed = true;
    }

    void UnsubscribeClock()
    {
        if (!_clockSubscribed)
            return;

        if (_clock != null)
            _clock.MinuteChanged -= OnMinuteChanged;
        _clock = null;
        _clockSubscribed = false;
    }

    void RaiseChanged() => Changed?.Invoke();

#if UNITY_EDITOR
    [ContextMenu("Needs/Sleep")]
    void DebugSleep() => TrySleep();

    [ContextMenu("Needs/Wake")]
    void DebugWake() => Wake();

    [ContextMenu("Needs/Add Fatigue 0.3")]
    void DebugAddFatigue()
    {
        _fatigue01 = Mathf.Clamp01(_fatigue01 + 0.3f);
        RaiseChanged();
    }

    [ContextMenu("Needs/Add Sleep Debt 0.3")]
    void DebugAddSleepDebt()
    {
        _sleepDebt01 = Mathf.Clamp01(_sleepDebt01 + 0.3f);
        RaiseChanged();
    }
#endif
}

public enum NeedsFatalKind
{
    Starve = 0,
    Dehydrate = 1
}

public enum NeedsWarningKind
{
    Hunger70 = 0,
    Hunger50 = 1,
    Hunger25 = 2,
    Hunger10 = 3,
    ThirstDanger = 4
}

public readonly struct NeedsIngestResult
{
    public readonly float AcceptedMlFood;
    public readonly float DiscardedMlFood;
    public readonly float AcceptedMlWater;
    public readonly float DiscardedMlWater;
    public readonly float AcceptedKcal;
    public readonly float DiscardedKcal;
    public readonly bool Overflowed;

    public NeedsIngestResult(
        float acceptedMlFood,
        float discardedMlFood,
        float acceptedMlWater,
        float discardedMlWater,
        float acceptedKcal,
        float discardedKcal,
        bool overflowed)
    {
        AcceptedMlFood = acceptedMlFood;
        DiscardedMlFood = discardedMlFood;
        AcceptedMlWater = acceptedMlWater;
        DiscardedMlWater = discardedMlWater;
        AcceptedKcal = acceptedKcal;
        DiscardedKcal = discardedKcal;
        Overflowed = overflowed;
    }
}
