// ============================================================
// PlayerGearHost — 플레이어 Wear/Wield 호스트 + HelmetVision (Kind는 WorldWeatherHost 포워드)
// ============================================================

using System;
using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInventoryHost))]
[RequireComponent(typeof(InventoryTimedMoveHost))]
[RequireComponent(typeof(CharacterActionHost))]
public sealed class PlayerGearHost : MonoBehaviour
{
    [SerializeField] PlayerInventoryHost _inventoryHost;
    [SerializeField] CharacterSkillsHost _skillsHost;
    [SerializeField] CharacterAttacker _attacker;
    [SerializeField] PlayerMovement _movement;
    [SerializeField] TimeScaleChannel _timeChannel = TimeScaleChannel.World;

    CharacterGearService _service;
    CharacterActionHost _actionHost;
    CharacterClimateHost _climateHost;
    CharacterBodyHost _bodyHost;
    ICharacterBody _subscribedBody;
    WorldWeatherHost _subscribedWeather;
    bool _bound;
    int _lastWetnessPercent = -1;
    int _lastBodyTempTenths = int.MinValue;
    int _lastVisionPercent = -1;
    bool _hasLastWeatherKind;
    WeatherKind _lastWeatherKind;

    public static PlayerGearHost Active => CharacterSessionHub.GearHost;

    public void ClaimActive() { }

    public CharacterGearService Service => _service;
    public EquipmentWearState Wear => _service?.Wear;
    public WieldSlots Wield => _service?.Wield;
    public GearTimedAction Timed => _service?.Timed;
    public WearEnvExposure EnvExposure =>
        _climateHost != null ? _climateHost.EnvExposure : null;
    public BodyTemp BodyTemperature =>
        _climateHost != null ? _climateHost.BodyTemperature : null;
    public WeatherExposure Weather =>
        _climateHost != null ? _climateHost.Weather : null;
    public float VisionFactor { get; private set; } = HelmetVision.FullVisionFactor;
    public bool HasHeadVisionPenalty => VisionFactor < HelmetVision.FullVisionFactor;
    public bool HasLiftStrain => _service != null && _service.HasLiftStrain;

    public event Action Changed;

    void Awake()
    {
        EnsureReferences();
        _service = new CharacterGearService();
    }

    void OnEnable()
    {
        EnsureBound();
        if (_climateHost != null)
            _climateHost.Changed += OnClimateChanged;
        EnsureWeatherSubscription();
        SubscribeBody();
        _service?.DropWieldForMissingHands(_subscribedBody);
        ApplyLiftStrainMovement();
        ApplyVisionToCamera();
        RefreshPrimaryWield();
    }

    void OnDisable()
    {
        if (_service != null)
        {
            _service.LiftStrainChanged -= ApplyLiftStrainMovement;
            _service.Changed -= OnServiceChanged;
            _service.Unbind();
            _bound = false;
        }

        if (_climateHost != null)
            _climateHost.Changed -= OnClimateChanged;

        UnbindWeatherSubscription();

        UnsubscribeBody();

        if (_movement != null)
        {
            _movement.SetLiftStrainMovement(1f);
            _movement.SetEnvMovement(1f);
        }

        CameraZoomController zoom = CameraZoomController.Active;
        if (zoom != null)
            zoom.SetVisionFactor(HelmetVision.FullVisionFactor);
    }

    void OnValidate() => EnsureReferences();

    void Reset() => EnsureReferences();

    void Update()
    {
        EnsureWeatherSubscription();
        if (_service == null)
            return;
        float dt = TimeScaleService.Delta(_timeChannel);
        if (_actionHost != null)
            dt *= _actionHost.ActionTickScale;
        _service.Tick(dt);
        TickVisionAndNotify();
    }

    /// <summary>월드 날씨 Kind — WorldWeatherHost 포워드. Host 없으면 Clear.</summary>
    public WeatherKind WorldWeatherKind
    {
        get
        {
            WorldWeatherHost weather = WorldWeatherHost.Instance;
            return weather != null ? weather.CurrentKind : WeatherKind.Clear;
        }
    }

    /// <summary>디버그·호환용. Kind SSOT는 WorldWeatherHost.SetKind.</summary>
    public void SetWeatherKind(WeatherKind kind)
    {
        WorldWeatherHost weather = WorldWeatherHost.Instance;
        if (weather != null)
            weather.SetKind(kind, WeatherChangeReason.Debug);
        Changed?.Invoke();
    }

    void TickVisionAndNotify()
    {
        VisionFactor = HelmetVision.ComputeVisionFactor(_service.Wear);
        ApplyVisionToCamera();

        BodyTemp bodyTemp = BodyTemperature;
        WearEnvExposure env = EnvExposure;
        int wetPercent = env != null ? env.WetnessPercent : 0;
        int tempTenths = bodyTemp != null ? bodyTemp.BodyTempTenths : int.MinValue;
        int visionPct = HelmetVision.VisionPercent(VisionFactor);
        WeatherKind weatherKind = WorldWeatherKind;
        if (wetPercent == _lastWetnessPercent
            && tempTenths == _lastBodyTempTenths
            && visionPct == _lastVisionPercent
            && _hasLastWeatherKind
            && weatherKind == _lastWeatherKind)
            return;

        _lastWetnessPercent = wetPercent;
        _lastBodyTempTenths = tempTenths;
        _lastVisionPercent = visionPct;
        _lastWeatherKind = weatherKind;
        _hasLastWeatherKind = true;
        Changed?.Invoke();
    }

    void OnClimateChanged() => Changed?.Invoke();

    void OnWorldWeatherChanged() => Changed?.Invoke();

    void EnsureWeatherSubscription()
    {
        WorldWeatherHost weather = WorldWeatherHost.Instance;
        if (weather == _subscribedWeather)
            return;
        UnbindWeatherSubscription();
        _subscribedWeather = weather;
        if (_subscribedWeather != null)
            _subscribedWeather.WeatherKindChanged += OnWorldWeatherChanged;
    }

    void UnbindWeatherSubscription()
    {
        if (_subscribedWeather == null)
            return;
        _subscribedWeather.WeatherKindChanged -= OnWorldWeatherChanged;
        _subscribedWeather = null;
    }

    public void BindMovement(PlayerMovement movement)
    {
        _movement = movement;
        ApplyLiftStrainMovement();
    }

    void EnsureReferences()
    {
        if (_inventoryHost == null)
            TryGetComponent(out _inventoryHost);
        if (_skillsHost == null)
            TryGetComponent(out _skillsHost);
        if (_attacker == null)
            TryGetComponent(out _attacker);
        if (_movement == null)
            TryGetComponent(out _movement);
        if (_climateHost == null)
            TryGetComponent(out _climateHost);
        if (_bodyHost == null)
            TryGetComponent(out _bodyHost);
        if (_actionHost == null)
            TryGetComponent(out _actionHost);
    }

    public void BindDomainIfNeeded() => EnsureBound();

    void EnsureBound()
    {
        if (_bound || _service == null)
            return;

        EnsureReferences();
        _service.Bind(
            Strength,
            Skills,
            BodyContainer,
            FloorContainer,
            RefreshPrimaryWield,
            ResolveCharacterBody,
            ApplyStackSelectedLeaf);
        _service.SetActionHost(_actionHost);
        _service.SetPresentationCatalog(_attacker != null ? _attacker.Catalog : null);
        _service.LiftStrainChanged += ApplyLiftStrainMovement;
        _service.Changed += OnServiceChanged;
        _bound = true;
    }

    int Strength()
    {
        ICharacterSkills skills = Skills();
        return skills != null ? skills.Level(AttributeIds.Str) : 0;
    }

    ICharacterSkills Skills() =>
        _skillsHost != null ? _skillsHost.Skills : GameplayData.CharacterSkills;

    ICharacterBody ResolveCharacterBody() =>
        _bodyHost != null ? _bodyHost.Body : null;

    void SubscribeBody()
    {
        ICharacterBody body = ResolveCharacterBody();
        if (ReferenceEquals(_subscribedBody, body))
            return;

        UnsubscribeBody();
        _subscribedBody = body;
        if (_subscribedBody != null)
            _subscribedBody.Changed += OnBodyChanged;
    }

    void UnsubscribeBody()
    {
        if (_subscribedBody == null)
            return;
        _subscribedBody.Changed -= OnBodyChanged;
        _subscribedBody = null;
    }

    void OnBodyChanged() =>
        _service?.DropWieldForMissingHands(_subscribedBody);

    InventoryContainer BodyContainer() =>
        _inventoryHost != null ? _inventoryHost.Container : null;

    InventoryContainer FloorContainer()
    {
        InventorySession session = PlayerInventoryRuntime.Active?.Session;
        if (session == null)
            return null;

        IReadOnlyList<InventoryContainer> sidebar = session.GetSidebarContainers();
        for (int i = 0; i < sidebar.Count; i++)
        {
            InventoryContainer c = sidebar[i];
            if (c != null
                && string.Equals(
                    c.InstanceId,
                    FloorLootHost.DefaultInstanceId,
                    System.StringComparison.Ordinal))
                return c;
        }

        return null;
    }

    void OnServiceChanged()
    {
        if (_service != null)
            VisionFactor = HelmetVision.ComputeVisionFactor(_service.Wear);
        ApplyVisionToCamera();
        Changed?.Invoke();
        PlayerInventoryRuntime.Active?.Session?.NotifySidebarLayoutChanged();
    }

    void ApplyLiftStrainMovement()
    {
        if (_movement == null)
            return;
        float factor = HasLiftStrain ? GearConstants.LiftStrainMoveFactor : 1f;
        _movement.SetLiftStrainMovement(factor);
    }

    void ApplyVisionToCamera()
    {
        CameraZoomController zoom = CameraZoomController.Active;
        if (zoom == null)
            return;
        zoom.SetVisionFactor(VisionFactor);
    }

    public void RefreshPrimaryWield()
    {
        if (_attacker == null || _service == null)
            return;

        if (!PrimaryWieldResolver.TryResolvePrimary(
                _service.Wield,
                _attacker.Catalog,
                Skills(),
                out PrimaryWieldResolver.HandScore primary,
                out _))
        {
            _attacker.SetWieldedItem((ItemStack)null);
            // 비무장: 한손 슬롯이 아니라 UpperBody TwoHand overlay.
            _attacker.SetActiveWieldHand(WieldHand.TwoHand);
            return;
        }

        _attacker.SetWieldedItem(primary.Stack);
        _attacker.SetActiveWieldHand(
            CharacterAttacker.AnimHandFrom(_service.Wield, primary.Slot));
    }

    bool ApplyStackSelectedLeaf(ItemStack stack, CombatLeaf? action) =>
        _attacker != null && _attacker.TryApplyStackSelectedLeaf(stack, action);
}
