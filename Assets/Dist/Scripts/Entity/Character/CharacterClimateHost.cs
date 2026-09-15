// ============================================================
// CharacterClimateHost — PC/NPC 공용 체온·습윤 틱 (plain module)
// ============================================================

using System;
using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;
using UnityEngine;

public sealed class CharacterClimateHost
{
    /// <summary>머리/손/발이 FrostbiteOnsetTempC 이하로 이 시간(World초) 유지되면 frostbite.</summary>
    public const float FrostbiteOnsetSeconds = 30f;

    /// <summary>코어 Hot 이상으로 이 시간(World초) 유지되면 heat 효과.</summary>
    public const float HeatOnsetSeconds = 20f;

    /// <summary>코어가 min/max에 닿았을 때 가슴 ApplyHit 양.</summary>
    public const int ExtremeCoreDamage = 1;

    /// <summary>극단 코어 피해 간격 (World초).</summary>
    public const float ExtremeCoreDamageIntervalSeconds = 4f;

    CharacterBodyRefs _refs;
    CharacterBodyHost _bodyHost;
    PlayerGearHost _gearHost;
    CharacterState _characterState;
    CharacterMotor _motor;
    PlayerMovement _movement;
    TileMapManager _tileMapManager;
    readonly BodyTemp _bodyTemp = new();
    readonly WearEnvExposure _envExposure = new();
    readonly WeatherExposure _weather = new();
    float _mapCellSize = 1f;
    readonly int[] _warmthIn = new int[BodyPartIds.ThermalParts.Length];
    readonly bool[] _presentIn = new bool[BodyPartIds.ThermalParts.Length];
    readonly float[] _frostbiteElapsed = new float[BodyPartIds.FrostbiteParts.Length];
    readonly List<BodyPartEffect> _effectScratch = new(8);
    float _heatElapsed;
    float _extremeDamageElapsed;
    int _lastBodyTempTenths = int.MinValue;
    int _lastWetnessPercent = -1;
    int _bodyTempChangedRaiseDepth;

    public BodyTemp BodyTemperature => _bodyTemp;
    public WearEnvExposure EnvExposure => _envExposure;
    public WeatherExposure Weather => _weather;

    public event Action Changed;

#if UNITY_EDITOR
    public enum EditorOutdoorOverride
    {
        Map = 0,
        ForceOutdoor = 1,
        ForceIndoor = 2
    }

    public static EditorOutdoorOverride DebugOutdoorOverride;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetEditorOutdoorOverride() =>
        DebugOutdoorOverride = EditorOutdoorOverride.Map;

    public static bool TryGetDebugOutdoorOverride(out bool outdoor)
    {
        switch (DebugOutdoorOverride)
        {
            case EditorOutdoorOverride.ForceOutdoor:
                outdoor = true;
                return true;
            case EditorOutdoorOverride.ForceIndoor:
                outdoor = false;
                return true;
            default:
                outdoor = false;
                return false;
        }
    }
#endif

    public void Bind(CharacterBodyRefs refs)
    {
        if (_bodyTemp != null)
            _bodyTemp.Changed -= OnBodyTempChanged;

        _refs = refs;
        _bodyHost = refs != null ? refs.BodyHost : null;
        _gearHost = refs != null ? refs.GearHost : null;
        _characterState = refs != null ? refs.State : null;
        _motor = refs != null ? refs.Motor : null;
        _movement = refs != null ? refs.Get<PlayerMovement>() : null;
        _bodyTemp.Changed += OnBodyTempChanged;
    }

    public void Dispose()
    {
        _bodyTemp.Changed -= OnBodyTempChanged;
    }

    public void ApplyBodyTempDto(BodyTempDto dto)
    {
        _bodyTempChangedRaiseDepth++;
        try
        {
            _bodyTemp.FromDto(dto);
        }
        finally
        {
            _bodyTempChangedRaiseDepth--;
        }

        OnBodyTempChanged();
    }

    void OnBodyTempChanged()
    {
        if (_bodyTempChangedRaiseDepth > 0)
            return;

        _lastBodyTempTenths = _bodyTemp.BodyTempTenths;
        Changed?.Invoke();
    }

    public void Tick()
    {
        if (_gearHost == null && _refs != null)
            _gearHost = _refs.GearHost;

        float dt = TimeScaleService.Delta(TimeScaleChannel.World);
        if (dt <= 0f)
            return;

        ICharacterBody body = _bodyHost != null ? _bodyHost.Body : null;
        EquipmentWearState wear = _gearHost != null ? _gearHost.Wear : null;
        int envProt = 0;
        if (_gearHost != null)
        {
            WearStatsAggregator.WearArmorTotals totals = WearStatsAggregator.Aggregate(wear);
            envProt = totals.TotalEnvironmentalProtection;
        }

        _weather.Resolve(ResolveWorldWeatherKind(), ResolveDayPeriod(), ResolveOutdoor());
        float ambientTempC = _weather.AmbientTempC;
        float ambientWet = _weather.AmbientWetnessGainPerSecond;
        ambientWet = Mathf.Max(ambientWet, ResolveLiquidWetnessGain());

        _envExposure.Tick(dt, envProt, ambientWet);

        int n = BodyPartIds.ThermalParts.Length;
        for (int i = 0; i < n; i++)
        {
            string partId = BodyPartIds.ThermalParts[i];
            bool present = body != null && body.Has(partId);
            _presentIn[i] = present;
            _warmthIn[i] = present ? WearStatsAggregator.WarmthForPart(wear, partId) : 0;
        }

        _bodyTemp.Tick(dt, _envExposure.Wetness01, ambientTempC, _warmthIn, _presentIn);
        TickFrostbiteAndHeat(body, dt);
        TickExtremeCoreDamage(body, dt);
        ApplyLocomotionEnv(body);

        int tenths = _bodyTemp.BodyTempTenths;
        int wetPercent = _envExposure.WetnessPercent;
        if (tenths == _lastBodyTempTenths && wetPercent == _lastWetnessPercent)
            return;

        _lastBodyTempTenths = tenths;
        _lastWetnessPercent = wetPercent;
        Changed?.Invoke();
    }

    WeatherKind ResolveWorldWeatherKind()
    {
        WorldWeatherHost weather = WorldWeatherHost.Instance;
        if (weather != null)
        {
            TileMapCacheHub hub = TileMapCacheHub.Runtime;
            if (hub != null)
            {
                EnsureMapCellSize();
                Vector3 world = ResolveEntityWorld();
                Vector3Int floor = OccupiedCellCoord.ResolveFromWorld(hub, world, _mapCellSize);
                if (weather.TryGetKindAt(floor.x, floor.z, out WeatherKind atCell))
                    return atCell;
            }

            return weather.CurrentKind;
        }

        PlayerGearHost host = _gearHost != null ? _gearHost : PlayerGearHost.Active;
        return host != null ? host.WorldWeatherKind : WeatherKind.Clear;
    }

    static DayPeriod ResolveDayPeriod()
    {
        WorldClock clock = WorldClock.Instance;
        return clock != null ? clock.Period : DayPeriod.Day;
    }

    bool ResolveOutdoor()
    {
#if UNITY_EDITOR
        if (TryGetDebugOutdoorOverride(out bool forced))
            return forced;
#endif
        return EvaluateMapOutdoor();
    }

    /// <summary>맵 셀 실내외. 에디터 오버라이드 무시.</summary>
    public bool EvaluateMapOutdoor()
    {
        TileMapCacheHub hub = TileMapCacheHub.Runtime;
        if (hub == null)
            return true;

        EnsureMapCellSize();
        Vector3 world = ResolveEntityWorld();
        Vector3Int floor = OccupiedCellCoord.ResolveFromWorld(hub, world, _mapCellSize);
        return hub.IsOutdoorEvaluation(floor.y, floor.x, floor.z);
    }

    Vector3 ResolveEntityWorld()
    {
        if (_characterState != null)
        {
            Vector3 body = _characterState.BodyWorldPoint;
            if (body.sqrMagnitude > 1e-6f)
                return body;
        }

        return _refs != null ? _refs.transform.position : Vector3.zero;
    }

    void EnsureMapCellSize()
    {
        if (_tileMapManager == null)
            _tileMapManager = UnityEngine.Object.FindFirstObjectByType<TileMapManager>();

        IWorldGrid grid = _tileMapManager != null ? _tileMapManager.WorldGrid : null;
        if (grid != null)
            _mapCellSize = grid.CellSize;
    }

    float ResolveLiquidWetnessGain()
    {
        CharacterState state = _characterState;
        if (state == null)
            return 0f;

        if (state.IsDiving)
            return MapSwimConsts.LiquidWetnessGainDive;
        if (state.IsSwimming)
            return MapSwimConsts.LiquidWetnessGainSwim;
        if (state.IsWading)
            return MapSwimConsts.LiquidWetnessGainWade;
        return 0f;
    }

    void TickFrostbiteAndHeat(ICharacterBody body, float dt)
    {
        if (body == null)
            return;

        for (int i = 0; i < BodyPartIds.FrostbiteParts.Length; i++)
        {
            string partId = BodyPartIds.FrostbiteParts[i];
            if (!body.Has(partId) || !_bodyTemp.IsPartTracked(partId))
            {
                _frostbiteElapsed[i] = 0f;
                continue;
            }

            if (!_bodyTemp.TryGetPartTempC(partId, out float partTempC)
                || !BodyTemp.IsFrostbiteTemp(partTempC))
            {
                _frostbiteElapsed[i] = 0f;
                continue;
            }

            _frostbiteElapsed[i] += dt;
            if (_frostbiteElapsed[i] < FrostbiteOnsetSeconds)
                continue;

            TryAddEffectOnce(body, partId, BodyPartEffectIds.Frostbite);
        }

        if (!body.Has(BodyPartIds.Chest) || !_bodyTemp.IsPartTracked(BodyPartIds.Chest))
        {
            _heatElapsed = 0f;
            return;
        }

        if (_bodyTemp.Feeling < BodyTempFeeling.Hot)
        {
            _heatElapsed = 0f;
            return;
        }

        _heatElapsed += dt;
        if (_heatElapsed < HeatOnsetSeconds)
            return;

        TryAddEffectOnce(body, BodyPartIds.Chest, BodyPartEffectIds.Heat);
    }

    void TickExtremeCoreDamage(ICharacterBody body, float dt)
    {
        if (body == null || !body.Has(BodyPartIds.Chest))
            return;

        float core = _bodyTemp.BodyTempC;
        bool extreme = core <= BodyTemp.BodyTempMinC || core >= BodyTemp.BodyTempMaxC;
        if (!extreme)
        {
            _extremeDamageElapsed = 0f;
            return;
        }

        _extremeDamageElapsed += dt;
        if (_extremeDamageElapsed < ExtremeCoreDamageIntervalSeconds)
            return;

        _extremeDamageElapsed = 0f;
        BodyDamageService.ApplyHit(body, BodyPartIds.Chest, ExtremeCoreDamage);
    }

    void TryAddEffectOnce(ICharacterBody body, string partId, string effectId)
    {
        if (HasEffect(body, partId, effectId))
            return;

        body.AddEffect(partId, new BodyPartEffect(effectId, 1, -1f));
    }

    bool HasEffect(ICharacterBody body, string partId, string effectId)
    {
        _effectScratch.Clear();
        body.CollectEffectsUnder(partId, _effectScratch, includeDescendants: false);
        for (int i = 0; i < _effectScratch.Count; i++)
        {
            if (_effectScratch[i].EffectId == effectId)
                return true;
        }

        return false;
    }

    void ApplyLocomotionEnv(ICharacterBody body)
    {
        // Hot path: 4 Has + 2 float muls, no alloc. Same value → motor (NPC) and possessed PlayerMovement.
        float factor = BodyLocomotionPenalties.CombinedMoveSpeedFactor(
            body,
            _bodyTemp.Feeling,
            _envExposure.Wetness01);
        _motor?.SetEnvMovement(factor);
        _movement?.SetEnvMovement(factor);
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Verify BodyTemp DTO Round-Trip")]
    void DebugVerifyBodyTempDtoRoundTrip()
    {
        Debug.Log("[CharacterClimateHost] BodyTemp DTO " + BodyTemp.ExecuteDtoRoundTripVerify(), _refs);
    }
#endif
}
