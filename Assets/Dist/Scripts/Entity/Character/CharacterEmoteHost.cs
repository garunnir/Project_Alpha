// ============================================================
// CharacterEmoteHost — 월드 이모트 소스 우선순위·필터·표시 SSOT
// ============================================================

using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterEmoteHost : MonoBehaviour
{
    const int SourceSlotCount = 4;

    struct SourceSlot
    {
        public EmoteId Id;
        public float ExpireAt;
        public bool Active;
    }

    [SerializeField] CharacterEmoteCatalog _catalog;
    [SerializeField] CharacterEmoteSettings _settings = CharacterEmoteSettings.DefaultUnity;

    CharacterMotor _motor;
    CharacterSightFadeHost _fadeHost;
    CharacterMoodHost _moodHost;
    readonly SourceSlot[] _slots = new SourceSlot[SourceSlotCount];

    EmoteId _resolvedId = EmoteId.None;
    EmoteSource _resolvedSource = EmoteSource.None;
    EmoteHideReason _hideReason = EmoteHideReason.NoActiveEmote;
    float _nextExpireCheck;

    public CharacterEmoteCatalog Catalog => _catalog;
    public EmoteId ResolvedId => _resolvedId;
    public EmoteSource ResolvedSource => _resolvedSource;
    public EmoteHideReason HideReason => _hideReason;

    public bool IsDisplayVisible =>
        _resolvedId != EmoteId.None &&
        _hideReason != EmoteHideReason.CatalogMissing &&
        _hideReason != EmoteHideReason.SightFadeHidden &&
        _hideReason != EmoteHideReason.ObserverOnlyOnPossessed;

    public void ConfigureCatalog(CharacterEmoteCatalog catalog) => _catalog = catalog;

    void Awake()
    {
        CharacterBodyRefs refs = this.GetBodyRefs();
        if (refs != null)
        {
            _motor = refs.Motor;
            _fadeHost = refs.SightFade;
            refs.TryGet(out _moodHost);
        }
        else
        {
            _motor = CharacterBodyResolve.GetInBody<CharacterMotor>(this);
            _fadeHost = CharacterBodyResolve.GetInBody<CharacterSightFadeHost>(this);
            _moodHost = CharacterBodyResolve.GetInBody<CharacterMoodHost>(this);
        }

        RebuildResolved();
    }

    void OnEnable()
    {
        if (_moodHost != null)
            _moodHost.Changed += OnMoodChanged;
        RefreshMoodEmote();
    }

    void OnDisable()
    {
        if (_moodHost != null)
            _moodHost.Changed -= OnMoodChanged;
    }

    void OnMoodChanged() => RefreshMoodEmote();

    void RefreshMoodEmote()
    {
        if (_motor == null || !_motor.IsPossessed || _moodHost == null)
        {
            Clear(EmoteSource.Mood);
            return;
        }

        EmoteId id = CharacterMoodEmoteMapper.FromMood(_moodHost.Mood);
        Request(new EmoteRequest(id, EmoteSource.Mood));
    }

    public void SetAlertSpotted()
    {
        if (!CanApplyObserverEmote())
            return;

        Request(new EmoteRequest(EmoteId.AlertSpotted, EmoteSource.Combat));
    }

    public void SetAlertSuspicious()
    {
        if (!CanApplyObserverEmote())
            return;

        Request(new EmoteRequest(EmoteId.AlertSuspicious, EmoteSource.Combat));
    }

    public void ClearCombat() => Clear(EmoteSource.Combat);

    bool CanApplyObserverEmote() =>
        _motor == null || !_motor.IsPossessed;

    void Update()
    {
        float now = TimeScaleService.TimeNow(TimeScaleChannel.Realtime);
        if (now < _nextExpireCheck)
            return;

        _nextExpireCheck = now + 0.05f;
        if (!TickExpirations(now))
            return;

        RebuildResolved();
    }

    public void Request(in EmoteRequest request)
    {
        if (request.Source == EmoteSource.None)
            return;

        if (request.Id == EmoteId.None)
        {
            Clear(request.Source);
            return;
        }

        if (_catalog == null || !_catalog.TryGetEntry(request.Id, out CharacterEmoteCatalog.Entry entry))
            return;

        if (entry.ObserverOnly && _motor != null && _motor.IsPossessed)
            return;

        int index = (int)request.Source;
        if ((uint)index >= _slots.Length)
            return;

        _slots[index].Id = request.Id;
        _slots[index].Active = true;
        _slots[index].ExpireAt = request.DurationSeconds.HasValue
            ? TimeScaleService.TimeNow(TimeScaleChannel.Realtime) + request.DurationSeconds.Value
            : float.PositiveInfinity;
        RebuildResolved();
    }

    public void Clear(EmoteSource source)
    {
        if (source == EmoteSource.None)
            return;

        int index = (int)source;
        if ((uint)index >= _slots.Length)
            return;

        if (!_slots[index].Active)
            return;

        _slots[index].Active = false;
        RebuildResolved();
    }

    public bool TryGetResolvedDisplay(out Sprite sprite, out Color tint)
    {
        sprite = null;
        tint = CharacterEmoteLayout.DefaultIconColor;

        if (!IsDisplayVisible || _catalog == null)
            return false;

        if (!_catalog.TryGetEntry(_resolvedId, out CharacterEmoteCatalog.Entry entry))
            return false;

        sprite = entry.Sprite;
        tint = entry.Tint;
        return sprite != null;
    }

    bool TickExpirations(float now)
    {
        bool changed = false;
        for (int i = 1; i < _slots.Length; i++)
        {
            ref SourceSlot slot = ref _slots[i];
            if (!slot.Active || now < slot.ExpireAt)
                continue;

            slot.Active = false;
            changed = true;
        }

        return changed;
    }

    void RebuildResolved()
    {
        EmoteId bestId = EmoteId.None;
        EmoteSource bestSource = EmoteSource.None;
        EmotePriority bestPriority = EmotePriority.Mood;

        for (int i = 1; i < _slots.Length; i++)
        {
            ref SourceSlot slot = ref _slots[i];
            if (!slot.Active || slot.Id == EmoteId.None)
                continue;

            EmoteSource source = (EmoteSource)i;
            EmotePriority priority = EmotePriorityUtility.FromSource(source);
            if (bestId != EmoteId.None && priority < bestPriority)
                continue;

            if (_catalog == null || !_catalog.TryGetEntry(slot.Id, out CharacterEmoteCatalog.Entry entry))
                continue;

            if (entry.ObserverOnly && _motor != null && _motor.IsPossessed)
                continue;

            bestId = slot.Id;
            bestSource = source;
            bestPriority = priority;
        }

        _resolvedId = bestId;
        _resolvedSource = bestSource;
        _hideReason = ResolveHideReason(bestId);
    }

    EmoteHideReason ResolveHideReason(EmoteId id)
    {
        if (id == EmoteId.None)
            return EmoteHideReason.NoActiveEmote;

        if (_catalog == null || !_catalog.TryGetEntry(id, out CharacterEmoteCatalog.Entry entry))
            return EmoteHideReason.CatalogMissing;

        if (entry.ObserverOnly && _motor != null && _motor.IsPossessed)
            return EmoteHideReason.ObserverOnlyOnPossessed;

        if (!PassesSightFadeGate())
            return EmoteHideReason.SightFadeHidden;

        return EmoteHideReason.None;
    }

    bool PassesSightFadeGate()
    {
        if (_motor != null && _motor.IsPossessed)
            return true;

        if (_fadeHost == null)
            return true;

        return _fadeHost.DisplayVisibility > _settings.HiddenThreshold;
    }
}
