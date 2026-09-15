// ============================================================
// WorldEnvironmentPresenter — Period 라이팅 + Kind VFX (실내 mute)
// ============================================================

using System;
using UnityEngine;

[DefaultExecutionOrder(50)]
public sealed class WorldEnvironmentPresenter : MonoBehaviour
{
    [Serializable]
    public struct PeriodLighting
    {
        public Color LightColor;
        public float LightIntensity;
        public Color AmbientColor;
        public float AmbientIntensity;
    }

    [Header("Lighting (WorldClock.Period SSOT)")]
    [SerializeField] Light _sunLight;
    [SerializeField] PeriodLighting _dawn = new PeriodLighting
    {
        LightColor = new Color(1f, 0.72f, 0.45f),
        LightIntensity = 0.85f,
        AmbientColor = new Color(0.45f, 0.4f, 0.5f),
        AmbientIntensity = 0.7f
    };
    [SerializeField] PeriodLighting _day = new PeriodLighting
    {
        LightColor = Color.white,
        LightIntensity = 1.1f,
        AmbientColor = new Color(0.55f, 0.55f, 0.6f),
        AmbientIntensity = 1f
    };
    [SerializeField] PeriodLighting _dusk = new PeriodLighting
    {
        LightColor = new Color(1f, 0.55f, 0.35f),
        LightIntensity = 0.75f,
        AmbientColor = new Color(0.4f, 0.35f, 0.45f),
        AmbientIntensity = 0.65f
    };
    [SerializeField] PeriodLighting _night = new PeriodLighting
    {
        LightColor = new Color(0.55f, 0.65f, 1f),
        LightIntensity = 0.25f,
        AmbientColor = new Color(0.12f, 0.14f, 0.22f),
        AmbientIntensity = 0.35f
    };

    [Header("Weather VFX (follow transform; mute when indoor)")]
    [SerializeField] Transform _vfxFollow;
    [SerializeField] ParticleSystem _rainVfx;
    [SerializeField] ParticleSystem _windVfx;
    [SerializeField] ParticleSystem _snowVfx;

    WorldClock _clock;
    WorldWeatherHost _weather;
    CharacterClimateHost _playerClimate;
    bool _lastOutdoor = true;
    WeatherKind _lastKind = WeatherKind.Clear;
    DayPeriod _lastPeriod = DayPeriod.Day;

    void OnEnable()
    {
        BindClock();
        BindWeather();
        ApplyAll(force: true);
    }

    void OnDisable()
    {
        UnbindClock();
        UnbindWeather();
        SetVfxActive(WeatherKind.Clear, outdoor: false);
    }

    void Update()
    {
        if (_clock == null && WorldClock.Instance != null)
            BindClock();
        if (_weather == null && WorldWeatherHost.Instance != null)
            BindWeather();

        if (_vfxFollow == null)
        {
            PlayerGearHost gear = PlayerGearHost.Active;
            if (gear != null && gear.BodyRefs != null)
                _vfxFollow = gear.BodyRefs.transform;
        }

        if (_vfxFollow != null)
            transform.position = _vfxFollow.position;

        bool outdoor = ResolveOutdoor();
        if (outdoor != _lastOutdoor)
        {
            _lastOutdoor = outdoor;
            ApplyWeatherVfx(_weather != null ? _weather.CurrentKind : WeatherKind.Clear, outdoor);
        }
    }

    void BindClock()
    {
        UnbindClock();
        _clock = WorldClock.Instance;
        if (_clock == null)
            return;
        _clock.PeriodChanged += OnPeriodChanged;
        ApplyPeriodLighting(_clock.Period);
        _lastPeriod = _clock.Period;
    }

    void UnbindClock()
    {
        if (_clock == null)
            return;
        _clock.PeriodChanged -= OnPeriodChanged;
        _clock = null;
    }

    void BindWeather()
    {
        UnbindWeather();
        _weather = WorldWeatherHost.Instance;
        if (_weather == null)
            return;
        _weather.WeatherKindChanged += OnWeatherKindChanged;
        ApplyWeatherVfx(_weather.CurrentKind, ResolveOutdoor());
        _lastKind = _weather.CurrentKind;
    }

    void UnbindWeather()
    {
        if (_weather == null)
            return;
        _weather.WeatherKindChanged -= OnWeatherKindChanged;
        _weather = null;
    }

    void OnPeriodChanged()
    {
        if (_clock == null)
            return;
        ApplyPeriodLighting(_clock.Period);
        _lastPeriod = _clock.Period;
    }

    void OnWeatherKindChanged()
    {
        if (_weather == null)
            return;
        ApplyWeatherVfx(_weather.CurrentKind, ResolveOutdoor());
        _lastKind = _weather.CurrentKind;
    }

    void ApplyAll(bool force)
    {
        DayPeriod period = _clock != null ? _clock.Period : DayPeriod.Day;
        WeatherKind kind = _weather != null ? _weather.CurrentKind : WeatherKind.Clear;
        bool outdoor = ResolveOutdoor();
        if (force || period != _lastPeriod)
            ApplyPeriodLighting(period);
        if (force || kind != _lastKind || outdoor != _lastOutdoor)
            ApplyWeatherVfx(kind, outdoor);
        _lastPeriod = period;
        _lastKind = kind;
        _lastOutdoor = outdoor;
    }

    void ApplyPeriodLighting(DayPeriod period)
    {
        PeriodLighting profile = ResolvePeriodProfile(period);
        if (_sunLight != null)
        {
            _sunLight.color = profile.LightColor;
            _sunLight.intensity = profile.LightIntensity;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = profile.AmbientColor;
        RenderSettings.ambientIntensity = profile.AmbientIntensity;
    }

    PeriodLighting ResolvePeriodProfile(DayPeriod period)
    {
        switch (period)
        {
            case DayPeriod.Dawn:
                return _dawn;
            case DayPeriod.Dusk:
                return _dusk;
            case DayPeriod.Night:
                return _night;
            default:
                return _day;
        }
    }

    void ApplyWeatherVfx(WeatherKind kind, bool outdoor)
    {
        SetVfxActive(kind, outdoor);
    }

    void SetVfxActive(WeatherKind kind, bool outdoor)
    {
        bool rain = outdoor && kind == WeatherKind.Rain;
        bool wind = outdoor && kind == WeatherKind.Wind;
        bool snow = outdoor && kind == WeatherKind.Snow;
        SetParticlePlaying(_rainVfx, rain);
        SetParticlePlaying(_windVfx, wind);
        SetParticlePlaying(_snowVfx, snow);
    }

    static void SetParticlePlaying(ParticleSystem ps, bool play)
    {
        if (ps == null)
            return;
        if (play)
        {
            if (!ps.isPlaying)
                ps.Play(true);
        }
        else if (ps.isPlaying)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    bool ResolveOutdoor()
    {
        if (_playerClimate == null)
        {
            PlayerGearHost gear = PlayerGearHost.Active;
            if (gear != null && gear.BodyRefs != null)
                _playerClimate = gear.BodyRefs.ClimateHost;
        }

        if (_playerClimate != null)
            return _playerClimate.EvaluateMapOutdoor();
        return true;
    }
}
