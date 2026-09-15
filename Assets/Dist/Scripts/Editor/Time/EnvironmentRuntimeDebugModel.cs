// ============================================================
// EnvironmentRuntimeDebugModel — Play 모드 월드 환경 디버그 Odin 프록시
// ============================================================
// EnvironmentRuntimeDebugDomain = 커버 목록 SSOT. 탭/ShowIf와 1:1.

using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>런타임 환경 디버그 창이 커버하는 도메인. 탭과 1:1.</summary>
public enum EnvironmentRuntimeDebugDomain
{
    Time = 0,
    Weather = 1,
    Outdoor = 2
}

[System.Serializable]
public sealed class EnvironmentRuntimeDebugModel
{
    public bool CanWrite => Application.isPlaying && WorldClock.Instance != null;

    public bool CanWriteWeather => CanWrite && WorldWeatherHost.Instance != null;

    bool ShowBindWarning => !CanWrite;

    bool ShowWeatherHostWarning => CanWrite && !CanWriteWeather;

    bool ShowIndoorWeatherIgnored =>
        CanWrite && !ResolveEffectiveOutdoor();

    [ShowInInspector, ReadOnly, PropertyOrder(-100)]
    [InfoBox("Play mode with a live WorldClock to edit.", InfoMessageType.Warning, nameof(ShowBindWarning))]
            [InfoBox("No WorldWeatherHost — weather Kind writes are disabled.", InfoMessageType.Warning, nameof(ShowWeatherHostWarning))]
    [InfoBox("Effective indoor: rain/wind and period ambient offsets are ignored.", InfoMessageType.Info, nameof(ShowIndoorWeatherIgnored))]
    public bool Writable => CanWrite;

    int MinutesPerHour
    {
        get
        {
            WorldClock clock = WorldClock.Instance;
            WorldClockSettings settings = clock != null ? clock.Settings : null;
            int minutesPerDay = settings != null
                ? settings.MinutesPerDay
                : WorldClockSettings.DefaultMinutesPerDay;
            return Mathf.Max(1, minutesPerDay / 24);
        }
    }

    int MaxMinuteOfHour => MinutesPerHour - 1;

    // ── Time ─────────────────────────────────────────────────

    [TabGroup("Env", "Time")]
    [ShowInInspector, ReadOnly]
    string ClockLabel
    {
        get
        {
            WorldClock clock = WorldClock.Instance;
            if (clock == null)
                return "(no WorldClock)";
            return TimeDisplayFormat.Format(clock.DayIndex, clock.HourOfDay, clock.MinuteOfHour)
                   + "  " + clock.Period;
        }
    }

    [TabGroup("Env", "Time")]
    [ShowInInspector, EnableIf(nameof(CanWrite))]
    [MinValue(0)]
    int DayIndex
    {
        get
        {
            WorldClock clock = WorldClock.Instance;
            return clock != null ? clock.DayIndex : 0;
        }
        set
        {
            WorldClock clock = WorldClock.Instance;
            if (!CanWrite || clock == null)
                return;
            clock.SetTime(value, clock.MinuteOfDay);
        }
    }

    [TabGroup("Env", "Time")]
    [ShowInInspector, EnableIf(nameof(CanWrite))]
    [PropertyRange(0, 23)]
    int Hour
    {
        get
        {
            WorldClock clock = WorldClock.Instance;
            return clock != null ? clock.HourOfDay : 0;
        }
        set
        {
            WorldClock clock = WorldClock.Instance;
            if (!CanWrite || clock == null)
                return;
            int hour = Mathf.Clamp(value, 0, 23);
            int minute = Mathf.Clamp(clock.MinuteOfHour, 0, MaxMinuteOfHour);
            clock.SetTime(clock.DayIndex, hour * MinutesPerHour + minute);
        }
    }

    [TabGroup("Env", "Time")]
    [ShowInInspector, EnableIf(nameof(CanWrite))]
    [MinValue(0)]
    int Minute
    {
        get
        {
            WorldClock clock = WorldClock.Instance;
            return clock != null ? clock.MinuteOfHour : 0;
        }
        set
        {
            WorldClock clock = WorldClock.Instance;
            if (!CanWrite || clock == null)
                return;
            int minute = Mathf.Clamp(value, 0, MaxMinuteOfHour);
            clock.SetTime(clock.DayIndex, clock.HourOfDay * MinutesPerHour + minute);
        }
    }

    [TabGroup("Env", "Time")]
    [ShowInInspector, ReadOnly]
    DayPeriod Period
    {
        get
        {
            WorldClock clock = WorldClock.Instance;
            return clock != null ? clock.Period : DayPeriod.Day;
        }
    }

    [TabGroup("Env", "Time")]
    [ButtonGroup("Env/Time/Periods")]
    [EnableIf(nameof(CanWrite))]
    void Dawn() => JumpToPeriod(DayPeriod.Dawn);

    [TabGroup("Env", "Time")]
    [ButtonGroup("Env/Time/Periods")]
    [EnableIf(nameof(CanWrite))]
    void Day() => JumpToPeriod(DayPeriod.Day);

    [TabGroup("Env", "Time")]
    [ButtonGroup("Env/Time/Periods")]
    [EnableIf(nameof(CanWrite))]
    void Dusk() => JumpToPeriod(DayPeriod.Dusk);

    [TabGroup("Env", "Time")]
    [ButtonGroup("Env/Time/Periods")]
    [EnableIf(nameof(CanWrite))]
    void Night() => JumpToPeriod(DayPeriod.Night);

    void JumpToPeriod(DayPeriod period)
    {
        WorldClock clock = WorldClock.Instance;
        if (!CanWrite || clock == null)
            return;

        WorldClockSettings settings = clock.Settings;
        if (settings == null)
            return;

        int minute;
        switch (period)
        {
            case DayPeriod.Dawn:
                minute = settings.DawnStartMinute;
                break;
            case DayPeriod.Day:
                minute = settings.DayStartMinute;
                break;
            case DayPeriod.Dusk:
                minute = settings.DuskStartMinute;
                break;
            default:
                minute = settings.NightStartMinute;
                break;
        }

        clock.SetTime(clock.DayIndex, minute);
    }

    // ── Weather ──────────────────────────────────────────────

    [TabGroup("Env", "Weather")]
    [ShowInInspector, EnableIf(nameof(CanWriteWeather))]
    WeatherKind Kind
    {
        get
        {
            WorldWeatherHost host = WorldWeatherHost.Instance;
            return host != null ? host.CurrentKind : WeatherKind.Clear;
        }
        set
        {
            WorldWeatherHost host = WorldWeatherHost.Instance;
            if (!CanWriteWeather || host == null)
                return;
            host.SetKind(value, WeatherChangeReason.Debug);
        }
    }

    [TabGroup("Env", "Weather")]
    [ShowInInspector, EnableIf(nameof(CanWriteWeather))]
    bool SchedulerEnabled
    {
        get
        {
            WorldWeatherHost host = WorldWeatherHost.Instance;
            return host != null && host.SchedulerEnabled;
        }
        set
        {
            WorldWeatherHost host = WorldWeatherHost.Instance;
            if (!CanWriteWeather || host == null)
                return;
            host.SchedulerEnabled = value;
        }
    }

    [TabGroup("Env", "Weather")]
    [ShowInInspector, ReadOnly]
    float PreviewAmbientTempC
    {
        get
        {
            WorldClock clock = WorldClock.Instance;
            DayPeriod period = clock != null ? clock.Period : DayPeriod.Day;
            return WeatherExposure.ResolveAmbientTempC(Kind, period, ResolveEffectiveOutdoor());
        }
    }

    [TabGroup("Env", "Weather")]
    [ShowInInspector, ReadOnly]
    float PreviewWetnessGainPerSecond
    {
        get
        {
            return WeatherExposure.ResolveWetnessGainPerSecond(Kind, ResolveEffectiveOutdoor());
        }
    }

    [TabGroup("Env", "Weather")]
    [ShowInInspector, ReadOnly]
    float LiveAmbientTempC
    {
        get
        {
            CharacterClimateHost host = ResolvePlayerClimate();
            return host != null ? host.Weather.AmbientTempC : PreviewAmbientTempC;
        }
    }

    [TabGroup("Env", "Weather")]
    [ShowInInspector, ReadOnly]
    int LiveWetnessPercent
    {
        get
        {
            CharacterClimateHost host = ResolvePlayerClimate();
            return host != null ? host.EnvExposure.WetnessPercent : 0;
        }
    }

    // ── Outdoor ──────────────────────────────────────────────

    [TabGroup("Env", "Outdoor")]
    [ShowInInspector, EnableIf(nameof(CanWrite))]
    CharacterClimateHost.EditorOutdoorOverride OutdoorOverride
    {
        get => CharacterClimateHost.DebugOutdoorOverride;
        set => CharacterClimateHost.DebugOutdoorOverride = value;
    }

    [TabGroup("Env", "Outdoor")]
    [ShowInInspector, ReadOnly]
    bool MapOutdoor
    {
        get
        {
            CharacterClimateHost host = ResolvePlayerClimate();
            return host != null ? host.EvaluateMapOutdoor() : true;
        }
    }

    [TabGroup("Env", "Outdoor")]
    [ShowInInspector, ReadOnly]
    bool EffectiveOutdoor => ResolveEffectiveOutdoor();

    bool ResolveEffectiveOutdoor()
    {
        if (CharacterClimateHost.TryGetDebugOutdoorOverride(out bool forced))
            return forced;

        CharacterClimateHost host = ResolvePlayerClimate();
        if (host != null)
            return host.EvaluateMapOutdoor();
        return true;
    }

    static CharacterClimateHost ResolvePlayerClimate()
    {
        PlayerGearHost gear = PlayerGearHost.Active;
        if (gear == null || gear.BodyRefs == null)
            return null;
        return gear.BodyRefs.ClimateHost;
    }
}
