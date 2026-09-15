// ============================================================
// PlayerGearHost — 플레이어 Wear/Wield 호스트 + HelmetVision (Kind는 WorldWeatherHost 포워드)
// ============================================================

using System;
using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;
using UnityEngine;

public sealed class PlayerGearHost
{
    TimeScaleChannel _timeChannel = TimeScaleChannel.World;

    CharacterBodyRefs _refs;
    PlayerInventoryHost _inventoryHost;
    CharacterSkillsHost _skillsHost;
    CharacterAttacker _attacker;
    PlayerMovement _movement;
    CharacterGearService _service;
    CharacterActionHost _actionHost;
    CharacterClimateHost _climateHost;
    CharacterBodyHost _bodyHost;
    CharacterState _characterState;
    ICharacterBody _subscribedBody;
    WorldWeatherHost _subscribedWeather;
    bool _bound;
    bool _enabled;
    int _lastWetnessPercent = -1;
    int _lastBodyTempTenths = int.MinValue;
    int _lastVisionPercent = -1;
    bool _hasLastWeatherKind;
    WeatherKind _lastWeatherKind;

    public CharacterBodyRefs BodyRefs => _refs;

    public static PlayerGearHost Active => PlayerPossessSession.GearHost;

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

    public void Bind(CharacterBodyRefs refs)
    {
        _refs = refs;
        _inventoryHost = refs != null ? refs.InventoryHost : null;
        _skillsHost = refs != null ? refs.SkillsHost : null;
        _attacker = refs != null ? refs.Attacker : null;
        _climateHost = refs != null ? refs.ClimateHost : null;
        _bodyHost = refs != null ? refs.BodyHost : null;
        _actionHost = refs != null ? refs.ActionHost : null;
        _characterState = refs != null ? refs.State : null;
        if (_service == null)
            _service = new CharacterGearService();
    }

    public void Enable()
    {
        EnsureBound();
        _climateHost = _refs != null ? _refs.ClimateHost : _climateHost;
        if (_climateHost != null)
            _climateHost.Changed += OnClimateChanged;
        EnsureWeatherSubscription();
        SubscribeBody();
        _service?.DropWieldForMissingHands(_subscribedBody);
        ApplyLiftStrainMovement();
        ApplyVisionToCamera();
        RefreshPrimaryWield();
        _enabled = true;
    }

    public void Disable()
    {
        if (!_enabled)
            return;

        _enabled = false;
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

    /// <summary>Gear timed + helmet vision. heap 없음.</summary>
    public void Tick()
    {
        if (!_enabled)
            return;

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

    public void BindDomainIfNeeded() => EnsureBound();

    void EnsureBound()
    {
        if (_bound || _service == null)
            return;

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
                    StringComparison.Ordinal))
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

    public void DropAllWieldedToWorld()
    {
        if (_service == null)
            return;

        _service.DropAllWieldedToWorld(
            ResolveSmallItemPrefab(),
            ResolveDropWorldPosition(),
            ResolveWorldGrid());
        RefreshPrimaryWield();
    }

    Vector3 ResolveDropWorldPosition()
    {
        if (_characterState != null && _characterState.BodyWorldPoint.sqrMagnitude > 1e-6f)
            return _characterState.BodyWorldPoint;
        return _refs != null ? _refs.transform.position : Vector3.zero;
    }

    IWorldGrid ResolveWorldGrid()
    {
        TileMapManager map = UnityEngine.Object.FindFirstObjectByType<TileMapManager>();
        return map != null ? map.WorldGrid : null;
    }

    static SmallItemObject ResolveSmallItemPrefab()
    {
        SmallItemObject[] all = Resources.FindObjectsOfTypeAll<SmallItemObject>();
        for (int i = 0; i < all.Length; i++)
        {
            SmallItemObject obj = all[i];
            if (obj == null || obj.gameObject.scene.IsValid())
                continue;
            return obj;
        }

        return UnityEngine.Object.FindFirstObjectByType<SmallItemObject>(
            FindObjectsInactive.Include);
    }
}
