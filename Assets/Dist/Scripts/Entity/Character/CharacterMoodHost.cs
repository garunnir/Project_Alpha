// ============================================================
// CharacterMoodHost — 사고 합산 기분 + Wander 양도 (possess 유지)
// ============================================================
// flowchart LR
//   Clock[WorldClock.MinuteChanged] --> Host
//   BodyNeeds[Body/Needs/Temp/Enc] --> Situational
//   Memory[Eat/Vomit/Rot] --> Host
//   Host --> Mood[clamp Base+sum]
//   Mood -->|below threshold| BreakRoll
//   BreakRoll -->|Wander| Yield[SetScriptedLocomotionInput false + NpcSteer]
//   Yield --> Windows[Close inv/craft]

using System;
using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterMoodHost : MonoBehaviour
{
    public const string LocBreakWander = "msg.status.mood_break_wander";
    public const string LocBreakEnd = "msg.status.mood_break_end";

    [SerializeField] MoodSettings _settings;

    CharacterMotor _motor;
    CharacterPainHost _pain;
    CharacterActionHost _action;
    CharacterBodyHost _bodyHost;
    ICharacterDefeat _defeat;
    IPlayerVitals _vitals;
    PlayerNeedsHost _needs;
    WorldClock _clock;
    bool _clockSubscribed;
    bool _vomitSubscribed;
    bool _bodySubscribed;
    bool _sourcesSubscribed;

    readonly List<MoodThought> _situational = new(16);
    readonly List<MoodThought> _memories = new(16);
    readonly List<MoodThought> _combined = new(32);
    readonly MoodBreakRuntime _break = new();

    float _mood;

    public static CharacterMoodHost Active => PlayerPossessSession.MoodHost;

    public static event Action AnyControlYielded;

    public event Action Changed;
    public event Action BreakChanged;

    public MoodSettings Settings => _settings;
    public float Mood => _mood;
    public IReadOnlyList<MoodThought> Thoughts => _combined;
    public bool IsControlYielded => _break.IsActive;
    public MoodBreakKind BreakKind => _break.Kind;

    public void ClaimActive() { }

    void Awake()
    {
        _motor = CharacterBodyResolve.GetInBody<CharacterMotor>(this);
        TryGetComponent(out _pain);
        TryGetComponent(out _action);
        TryGetComponent(out _bodyHost);
        if (TryGetComponent(out CharacterSkillsHost skills))
            _defeat = skills.Defeat;

        _break.Bind(_motor, _pain, _settings);
        Recalculate(raise: false);
    }

    void OnEnable()
    {
        SubscribeClock();
        SubscribeVomit();
        SubscribeSources();
        if (IsPlayerBody())
            ClaimActive();
    }

    void Start()
    {
        SubscribeClock();
        SubscribeVomit();
        SubscribeSources();
        _break.Bind(_motor, _pain, _settings);
        if (IsPlayerBody())
            ClaimActive();
        Recalculate(raise: true);
    }

    void OnDisable()
    {
        UnsubscribeClock();
        UnsubscribeVomit();
        UnsubscribeSources();
    }

    void Update()
    {
        if (!_break.IsActive)
            return;

        // Hot path: no alloc. Possessed motor uses Player channel.
        float dt = TimeScaleService.Delta(TimeScaleChannel.Player);
        _break.Tick(dt);
    }

    /// <summary>디버그용. 기존 Wander 시작 경로(입력 양도·메시지)를 재사용.</summary>
    public void DebugBeginWander() => BeginWander();

    /// <summary>디버그용. 입력 복귀 + optional Catharsis.</summary>
    public void DebugEndBreak(bool addCatharsis = true) => EndBreak(addCatharsis);

    public void AddMemory(ThoughtId id, int? offsetOverride = null)
    {
        if (_settings == null || !_settings.TryGetThought(id, out MoodSettings.ThoughtRow row) || row == null)
            return;
        if (row.kind != MoodThoughtKind.Memory)
            return;

        int offset = offsetOverride ?? row.offset;
        int minutes = row.durationMinutes < 1 ? 1 : row.durationMinutes;
        int stackLimit = row.stackLimit < 1 ? 1 : row.stackLimit;

        int count = 0;
        int firstIndex = -1;
        for (int i = 0; i < _memories.Count; i++)
        {
            if (_memories[i].Id != id)
                continue;
            if (firstIndex < 0)
                firstIndex = i;
            count++;
        }

        if (count >= stackLimit && firstIndex >= 0)
        {
            _memories[firstIndex] = new MoodThought(id, MoodThoughtKind.Memory, offset, minutes);
        }
        else
        {
            _memories.Add(new MoodThought(id, MoodThoughtKind.Memory, offset, minutes));
        }

        Recalculate(raise: true);
    }

    void OnMinuteChanged()
    {
        if (!IsPlayerBody())
            return;

        TickMemories();
        Recalculate(raise: false);
        bool breakEnded = _break.TickMinute();
        if (breakEnded)
            EndBreak(addCatharsis: true);
        else
            TryRollBreak();

        Changed?.Invoke();
    }

    void TickMemories()
    {
        for (int i = _memories.Count - 1; i >= 0; i--)
        {
            MoodThought thought = _memories[i];
            int left = thought.RemainingMinutes - 1;
            if (left <= 0)
            {
                _memories.RemoveAt(i);
                continue;
            }

            _memories[i] = new MoodThought(thought.Id, thought.Kind, thought.Offset, left);
        }
    }

    void Recalculate(bool raise)
    {
        _situational.Clear();
        BindNeeds(PlayerNeedsHost.Active);
        if (_settings != null)
        {
            ICharacterBody body = _bodyHost != null ? _bodyHost.Body : null;

            PlayerEncumbranceStage stage = PlayerEncumbranceStage.None;
            if (TryGetComponent(out PlayerEncumbranceHost enc))
                stage = enc.Stage;

            MoodSituationalCollector.Collect(
                body,
                GameplayData.Vitals,
                PlayerNeedsHost.Active,
                stage,
                _settings,
                _situational);
        }

        _combined.Clear();
        for (int i = 0; i < _situational.Count; i++)
            _combined.Add(_situational[i]);
        for (int i = 0; i < _memories.Count; i++)
            _combined.Add(_memories[i]);

        float value = _settings != null ? _settings.BaseMood : MoodSettings.DefaultBaseMood;
        for (int i = 0; i < _combined.Count; i++)
            value += _combined[i].Offset;

        float min = _settings != null ? _settings.MoodMin : MoodSettings.DefaultMoodMin;
        float max = _settings != null ? _settings.MoodMax : MoodSettings.DefaultMoodMax;
        if (max < min)
            max = min;
        _mood = Mathf.Clamp(value, min, max);

        if (raise)
            Changed?.Invoke();
    }

    void TryRollBreak()
    {
        if (_break.IsActive || _settings == null)
            return;
        if (_defeat != null && _defeat.IsDefeated)
            return;
        if (_pain != null && _pain.IsPainShocked)
            return;

        float threshold = _settings.BreakThreshold;
        if (_mood >= threshold)
            return;

        float mtbDays = _settings.MinorBreakMtbDays;
        if (_mood < _settings.ExtremeBreakThreshold)
            mtbDays = _settings.ExtremeBreakMtbDays;
        else if (_mood < _settings.MajorBreakThreshold)
            mtbDays = _settings.MajorBreakMtbDays;

        int minutesPerDay = _clock != null && _clock.Settings != null
            ? _clock.Settings.MinutesPerDay
            : WorldClockSettings.DefaultMinutesPerDay;
        float mtbMinutes = mtbDays * minutesPerDay;
        if (mtbMinutes <= 0f)
            return;
        if (UnityEngine.Random.value >= 1f / mtbMinutes)
            return;

        BeginWander();
    }

    void BeginWander()
    {
        _action?.CancelAll();
        _break.BeginWander(
            _settings != null
                ? _settings.WanderDurationMinutes
                : MoodSettings.DefaultWanderDurationMinutes);

        ApplyYield(true);
        AnyControlYielded?.Invoke();
        GameplayMessageLog.Append(
            MessageLogCategory.Status,
            MessageLogImportance.Critical,
            Loc.Get(LocBreakWander));
        BreakChanged?.Invoke();
        Changed?.Invoke();
    }

    void EndBreak(bool addCatharsis)
    {
        _break.End();
        ApplyYield(false);
        if (addCatharsis)
            AddMemory(ThoughtId.Catharsis);
        GameplayMessageLog.Append(
            MessageLogCategory.Status,
            MessageLogImportance.Normal,
            Loc.Get(LocBreakEnd));
        BreakChanged?.Invoke();
        Changed?.Invoke();
    }

    static void ApplyYield(bool yielded)
    {
        PlayerPossessedInputHost input = FindFirstObjectByType<PlayerPossessedInputHost>();
        if (input == null)
            return;

        input.SetScriptedLocomotionInput(!yielded);
    }

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

    void SubscribeVomit()
    {
        if (_vomitSubscribed)
            return;

        PlayerNeedsHost.AnyNeedsVomit += OnVomit;
        _vomitSubscribed = true;
    }

    void UnsubscribeVomit()
    {
        if (!_vomitSubscribed)
            return;

        PlayerNeedsHost.AnyNeedsVomit -= OnVomit;
        _vomitSubscribed = false;
    }

    void OnVomit()
    {
        if (!IsPlayerBody())
            return;

        AddMemory(ThoughtId.Vomited);
    }

    void SubscribeSources()
    {
        if (!_bodySubscribed && _bodyHost?.Body != null)
        {
            _bodyHost.Body.Changed += OnSourceChanged;
            _bodySubscribed = true;
        }

        if (_vitals == null && GameplayData.Vitals != null)
        {
            _vitals = GameplayData.Vitals;
            _vitals.Changed += OnVitalsChanged;
        }

        BindNeeds(PlayerNeedsHost.Active);
        if (_sourcesSubscribed)
            return;

        PlayerEncumbranceHost.StageChanged += OnSourceChanged;
        PlayerEncumbranceHost.ActiveChanged += OnSourceChanged;
        _sourcesSubscribed = true;
    }

    void UnsubscribeSources()
    {
        if (!_sourcesSubscribed)
            return;
        if (_bodySubscribed && _bodyHost?.Body != null)
            _bodyHost.Body.Changed -= OnSourceChanged;
        _bodySubscribed = false;

        if (_vitals != null)
            _vitals.Changed -= OnVitalsChanged;
        _vitals = null;

        BindNeeds(null);
        PlayerEncumbranceHost.StageChanged -= OnSourceChanged;
        PlayerEncumbranceHost.ActiveChanged -= OnSourceChanged;
        _sourcesSubscribed = false;
    }

    void BindNeeds(PlayerNeedsHost needs)
    {
        if (_needs == needs)
            return;

        if (_needs != null)
            _needs.Changed -= OnSourceChanged;

        _needs = needs;
        if (_needs != null)
            _needs.Changed += OnSourceChanged;
    }

    void OnVitalsChanged(string _) => OnSourceChanged();

    void OnSourceChanged()
    {
        if (!isActiveAndEnabled)
            return;

        Recalculate(raise: true);
    }

    bool IsPlayerBody() => _motor != null && _motor.IsPossessed;
}
