// ============================================================
// CharacterGearLabels — Character/Gear UI·컨텍스트 문구 SSOT
// ============================================================

using System.Text;
using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

public static class CharacterGearLabels
{
    const string EmptyValue = "—";

    const string KeyTitle = "Character.Title";
    const string KeyTabStatus = "Character.Tab.Status";
    const string KeyTabEquipment = "Character.Tab.Equipment";
    const string KeyTabEncumbrance = "Character.Tab.Encumbrance";
    const string KeyTabBodyTemp = "Character.Tab.BodyTemp";
    const string KeyWear = "Character.Wear";
    const string KeyTakeOff = "Character.TakeOff";
    const string KeyWieldGroup = "Character.WieldGroup";
    const string KeyWieldLeft = "Character.WieldLeft";
    const string KeyWieldRight = "Character.WieldRight";
    const string KeyWieldTwoHand = "Character.WieldTwoHand";
    const string KeyWieldGripGroup = "Character.WieldGripGroup";
    const string KeyWieldOpposite = "Character.WieldOpposite";
    const string KeyUnwield = "Character.Unwield";
    const string KeyDropFloor = "Character.DropFloor";
    const string KeyHandActionGroup = "Character.HandActionGroup";
    const string KeyActionNone = "Character.ActionNone";
    const string DisplayStrike = "STRIKE";
    const string DisplayPierce = "PIERCE";
    const string DisplaySemi = "SEMI";
    const string DisplayBurst = "BURST";
    const string DisplayAuto = "AUTO";
    const string DisplayRaise = "RAISE";
    const string DisplayExcavate = "EXCAVATE";
    const string DisplayChop = "CHOP";
    const string KeySlotLeft = "Character.SlotLeft";
    const string KeySlotRight = "Character.SlotRight";
    const string KeyWornFilterAll = "Character.WornFilterAll";
    const string KeyNeedStrength = "Character.NeedStrength";
    const string KeyLiftStrain = "Character.LiftStrain";
    const string KeyHoverRequiredStr = "Character.HoverRequiredStr";
    const string KeyBlockedBusy = "Character.BlockedBusy";
    const string KeyBlockedTool = "Character.BlockedToolSession";
    const string KeyBlockedWearable = "Character.BlockedNotWearable";
    const string KeyBlockedEquipped = "Character.BlockedAlreadyEquipped";
    const string KeyBlockedAlreadyGrip = "Character.BlockedAlreadyGrip";
    const string KeyBlockedTwoHandOnly = "Character.BlockedTwoHandOnly";
    const string KeyBlockedInvalid = "Character.BlockedInvalid";
    const string KeyBlockedNoStowRoom = "Character.BlockedNoStowRoom";
    const string KeyBlockedWearOverlap = "Character.BlockedWearOverlap";
    const string KeyHoverEnc = "Character.Hover.Enc";
    const string KeyHoverWarm = "Character.Hover.Warm";
    const string KeyHoverCoverage = "Character.Hover.Coverage";
    const string KeyHoverMaxEnc = "Character.Hover.MaxEnc";
    const string KeyHoverEnvProt = "Character.Hover.EnvProt";
    const string KeyHoverThickness = "Character.Hover.Thickness";
    const string KeyHoverPowerArmor = "Character.Hover.PowerArmor";
    const string KeyHoverPowerArmorYes = "Character.Hover.PowerArmorYes";
    const string KeyHoverLayer = "Character.Hover.Layer";
    const string KeyHoverSided = "Character.Hover.Sided";
    const string KeyHoverSidedYes = "Character.Hover.SidedYes";
    const string KeyHoverWetness = "Character.Hover.Wetness";
    const string KeyHoverExposure = "Character.Hover.Exposure";
    const string KeyHoverBodyTemp = "Character.Hover.BodyTemp";
    const string KeyHoverBodyTempTarget = "Character.Hover.BodyTempTarget";
    const string KeyHoverFeeling = "Character.Hover.Feeling";
    const string KeyEncTotals = "Character.Enc.Totals";
    const string KeyEncWetnessLine = "Character.Enc.WetnessLine";
    const string KeyBodyTempTotals = "Character.BodyTemp.Totals";
    const string KeyFeelingCold = "Character.BodyTemp.Feeling.Cold";
    const string KeyFeelingCool = "Character.BodyTemp.Feeling.Cool";
    const string KeyFeelingComfortable = "Character.BodyTemp.Feeling.Comfortable";
    const string KeyFeelingWarm = "Character.BodyTemp.Feeling.Warm";
    const string KeyFeelingHot = "Character.BodyTemp.Feeling.Hot";
    const string KeyHoverWeather = "Character.Hover.Weather";
    const string KeyHoverAmbient = "Character.Hover.Ambient";
    const string KeyHoverVision = "Character.Hover.Vision";
    const string KeyWeatherClear = "Character.Weather.Clear";
    const string KeyWeatherRain = "Character.Weather.Rain";
    const string KeyWeatherWind = "Character.Weather.Wind";
    const string KeyWeatherSnow = "Character.Weather.Snow";
    const string KeyWeatherLine = "Character.Weather.Line";
    const string KeyVisionLine = "Character.Vision.Line";
    const string KeyEncWeatherVisionLine = "Character.Enc.WeatherVisionLine";

    public static string Title => GetOr(KeyTitle, "캐릭터");
    public static string TabStatus => GetOr(KeyTabStatus, "상태");
    public static string TabEquipment => GetOr(KeyTabEquipment, "장비");
    public static string TabEncumbrance => GetOr(KeyTabEncumbrance, "방해");
    public static string TabBodyTemp => GetOr(KeyTabBodyTemp, "체온");
    public static string Wear => GetOr(KeyWear, "착용");
    public static string TakeOff => GetOr(KeyTakeOff, "벗기");
    public static string WieldGroup => GetOr(KeyWieldGroup, "들기");
    public static string WieldLeft => GetOr(KeyWieldLeft, "왼손 들기");
    public static string WieldRight => GetOr(KeyWieldRight, "오른손 들기");
    public static string WieldTwoHand => GetOr(KeyWieldTwoHand, "양손 들기");
    public static string WieldGripGroup => GetOr(KeyWieldGripGroup, "잡기");
    public static string WieldOpposite => GetOr(KeyWieldOpposite, "반대손으로 잡기");
    public static string Unwield => GetOr(KeyUnwield, "내려놓기");
    public static string DropFloor => GetOr(KeyDropFloor, "바닥에 놓기");
    public static string HandActionGroup => GetOr(KeyHandActionGroup, "사용 액션");
    public static string FamilyMelee => "Melee";
    public static string FamilyTrigger => "Trigger";
    public static string FamilyEtc => "Etc";
    public static string ActionNone => GetOr(KeyActionNone, "없음");
    public static string ActionStrike => ActionLabel(CombatLeaf.Strike);
    public static string ActionPierce => ActionLabel(CombatLeaf.Pierce);
    public static string ActionTrigger => ActionLabel(CombatLeaf.Semi);
    public static string ActionRaise => ActionLabel(CombatLeaf.Raise);
    public static string ActionExcavate => ActionLabel(CombatLeaf.Excavate);
    public static string ActionChop => ActionLabel(CombatLeaf.Chop);

    /// <summary>Leaf 표시. 동작 SSOT는 Attack.logicId.</summary>
    public static string ActionLabel(CombatLeaf leaf)
    {
        switch (CombatLeafUtil.Normalize(leaf))
        {
            case CombatLeaf.Strike:
                return DisplayStrike;
            case CombatLeaf.Pierce:
                return DisplayPierce;
            case CombatLeaf.Excavate:
                return DisplayExcavate;
            case CombatLeaf.Chop:
                return DisplayChop;
            case CombatLeaf.Semi:
                return DisplaySemi;
            case CombatLeaf.Burst:
                return DisplayBurst;
            case CombatLeaf.Auto:
                return DisplayAuto;
            case CombatLeaf.Raise:
                return DisplayRaise;
            default:
                return ActionNone;
        }
    }
    public static string SlotLeft => GetOr(KeySlotLeft, "L");
    public static string SlotRight => GetOr(KeySlotRight, "R");
    public static string WornFilterAll => GetOr(KeyWornFilterAll, "전체");
    public static string LiftStrain => GetOr(KeyLiftStrain, "힘 부담");
    public static string BlockedBusy => GetOr(KeyBlockedBusy, "다른 동작 진행 중");
    public static string BlockedToolSession => GetOr(KeyBlockedTool, "도구 사용 중");
    public static string BlockedNotWearable => GetOr(KeyBlockedWearable, "착용할 수 없음");
    public static string BlockedAlreadyEquipped => GetOr(KeyBlockedEquipped, "이미 장착됨");
    public static string BlockedAlreadyGrip => GetOr(KeyBlockedAlreadyGrip, "이미 그렇게 들고 있음");
    public static string BlockedTwoHandOnly => GetOr(KeyBlockedTwoHandOnly, "양손으로만 들 수 있음");
    public static string BlockedInvalid => GetOr(KeyBlockedInvalid, "유효하지 않음");
    public static string BlockedNoStowRoom => GetOr(KeyBlockedNoStowRoom, "가방에 넣을 공간이 없음");
    public static string HoverEnc => GetOr(KeyHoverEnc, "enc");
    public static string HoverWarm => GetOr(KeyHoverWarm, "warm");
    public static string HoverCoverage => GetOr(KeyHoverCoverage, "coverage");
    public static string HoverMaxEnc => GetOr(KeyHoverMaxEnc, "max_enc");
    public static string HoverEnvProt => GetOr(KeyHoverEnvProt, "env_prot");
    public static string HoverThickness => GetOr(KeyHoverThickness, "thickness");
    public static string HoverPowerArmor => GetOr(KeyHoverPowerArmor, "power_armor");
    public static string HoverPowerArmorYes => GetOr(KeyHoverPowerArmorYes, "yes");
    public static string HoverLayer => GetOr(KeyHoverLayer, "layer");
    public static string HoverSided => GetOr(KeyHoverSided, "sided");
    public static string HoverSidedYes => GetOr(KeyHoverSidedYes, "yes");
    public static string HoverWetness => GetOr(KeyHoverWetness, "wetness");
    public static string HoverExposure => GetOr(KeyHoverExposure, "exposure");
    public static string HoverBodyTemp => GetOr(KeyHoverBodyTemp, "체온");
    public static string HoverBodyTempTarget => GetOr(KeyHoverBodyTempTarget, "목표");
    public static string HoverFeeling => GetOr(KeyHoverFeeling, "감각");
    public static string HoverWeather => GetOr(KeyHoverWeather, "날씨");
    public static string HoverAmbient => GetOr(KeyHoverAmbient, "환경");
    public static string HoverVision => GetOr(KeyHoverVision, "시야");

    public static string FormatWearOverlap(string otherItemName) =>
        Loc.TryGet(KeyBlockedWearOverlap, out string template)
            ? SafeFormat(template, otherItemName ?? EmptyValue)
            : $"같은 부위·레이어 충돌 ({otherItemName ?? EmptyValue})";

    public static string FormatNeedStrength(int required, int have) =>
        Loc.TryGet(KeyNeedStrength, out string template)
            ? SafeFormat(template, required, have)
            : $"힘 부족 (필요 {required}, 현재 {have})";

    public static string FormatRequiredStr(int required, int have, bool strain) =>
        Loc.TryGet(KeyHoverRequiredStr, out string template)
            ? SafeFormat(template, required, have, strain ? LiftStrain : string.Empty)
            : $"필요 힘 {required} / 현재 {have}"
              + (strain ? $" ({LiftStrain})" : string.Empty);

    public static string FormatEncTotals(WearStatsAggregator.WearArmorTotals totals)
    {
        string power = totals.AnyPowerArmor ? HoverPowerArmorYes : EmptyValue;
        if (Loc.TryGet(KeyEncTotals, out string template))
        {
            return SafeFormat(
                template,
                totals.TotalEncumbrance,
                totals.TotalMaxEncumbrance,
                totals.MaxCoverage,
                totals.TotalEnvironmentalProtection,
                totals.TotalMaterialThickness,
                power);
        }

        return $"{HoverEnc} {totals.TotalEncumbrance}"
               + $"  {HoverMaxEnc} {totals.TotalMaxEncumbrance}"
               + $"  {HoverCoverage} {totals.MaxCoverage}"
               + $"\n{HoverEnvProt} {totals.TotalEnvironmentalProtection}"
               + $"  {HoverThickness} {totals.TotalMaterialThickness}"
               + $"  {HoverPowerArmor} {power}";
    }

    /// <summary>방해 totals + Phase E wetness + Phase G weather/vision.</summary>
    public static string FormatEncTotalsWithWetness(
        WearStatsAggregator.WearArmorTotals totals,
        WearEnvExposure exposure,
        WeatherExposure weather = null,
        float visionFactor = HelmetVision.FullVisionFactor)
    {
        string baseLine = FormatEncTotals(totals);
        if (exposure != null)
            baseLine += "\n" + FormatWetnessLine(exposure);
        string weatherVision = FormatWeatherVisionLine(weather, visionFactor);
        if (!string.IsNullOrEmpty(weatherVision))
            baseLine += "\n" + weatherVision;
        return baseLine;
    }

    public static string FormatWetnessLine(WearEnvExposure exposure)
    {
        if (exposure == null)
            return string.Empty;

        int wet = exposure.WetnessPercent;
        int exposurePct = Mathf.RoundToInt(exposure.ExposureFactor * 100f);
        if (Loc.TryGet(KeyEncWetnessLine, out string template))
            return SafeFormat(template, wet, exposurePct, exposure.LastEnvProtection);

        return $"{HoverWetness} {wet}%"
               + $"  {HoverExposure} {exposurePct}%"
               + $"  {HoverEnvProt} {exposure.LastEnvProtection}";
    }

    public static string FormatWeatherLabel(WeatherKind kind)
    {
        switch (kind)
        {
            case WeatherKind.Rain:
                return GetOr(KeyWeatherRain, "비");
            case WeatherKind.Wind:
                return GetOr(KeyWeatherWind, "바람");
            case WeatherKind.Snow:
                return GetOr(KeyWeatherSnow, "눈");
            default:
                return GetOr(KeyWeatherClear, "맑음");
        }
    }

    public static string FormatWeatherLine(WeatherExposure weather)
    {
        if (weather == null)
            return string.Empty;

        string label = FormatWeatherLabel(weather.Kind);
        if (Loc.TryGet(KeyWeatherLine, out string template))
            return SafeFormat(template, label, weather.AmbientTempC);

        return $"{HoverWeather} {label}"
               + $"  {HoverAmbient} {weather.AmbientTempC:0.#}°C";
    }

    public static string FormatVisionLine(float visionFactor)
    {
        int pct = HelmetVision.VisionPercent(visionFactor);
        if (Loc.TryGet(KeyVisionLine, out string template))
            return SafeFormat(template, pct);

        return $"{HoverVision} {pct}%";
    }

    public static string FormatWeatherVisionLine(
        WeatherExposure weather,
        float visionFactor)
    {
        string weatherLine = FormatWeatherLine(weather);
        string visionLine = FormatVisionLine(visionFactor);
        if (string.IsNullOrEmpty(weatherLine))
            return visionLine;
        if (Loc.TryGet(KeyEncWeatherVisionLine, out string template))
            return SafeFormat(
                template,
                FormatWeatherLabel(weather.Kind),
                weather.AmbientTempC,
                HelmetVision.VisionPercent(visionFactor));

        return weatherLine + "  " + visionLine;
    }

    /// <summary>체온 탭 totals — BodyTemp + TotalWarmth + weather/vision.</summary>
    public static string FormatBodyTempTotals(
        WearStatsAggregator.WearArmorTotals totals,
        BodyTemp bodyTemp,
        WeatherExposure weather = null,
        float visionFactor = HelmetVision.FullVisionFactor)
    {
        string core;
        if (bodyTemp == null)
        {
            core = $"{HoverWarm} {totals.TotalWarmth}";
        }
        else
        {
            string feeling = FormatBodyTempFeeling(bodyTemp.Feeling);
            if (Loc.TryGet(KeyBodyTempTotals, out string template))
            {
                core = SafeFormat(
                    template,
                    bodyTemp.BodyTempDisplayC,
                    feeling,
                    totals.TotalWarmth,
                    bodyTemp.TargetTempDisplayC);
            }
            else
            {
                core = $"{HoverBodyTemp} {bodyTemp.BodyTempDisplayC:0.0}°C ({feeling})"
                       + $"\n{HoverWarm} {totals.TotalWarmth}"
                       + $"  {HoverBodyTempTarget} {bodyTemp.TargetTempDisplayC:0.0}°C";
            }
        }

        string weatherVision = FormatWeatherVisionLine(weather, visionFactor);
        if (string.IsNullOrEmpty(weatherVision))
            return core;
        return core + "\n" + weatherVision;
    }

    public static string FormatBodyTempLine(BodyTemp bodyTemp)
    {
        if (bodyTemp == null)
            return string.Empty;

        string feeling = FormatBodyTempFeeling(bodyTemp.Feeling);
        return $"{HoverBodyTemp} {bodyTemp.BodyTempDisplayC:0.0}°C"
               + $"  {HoverFeeling} {feeling}"
               + $"  {HoverWarm} {bodyTemp.LastTotalWarmth}"
               + $"  {HoverBodyTempTarget} {bodyTemp.TargetTempDisplayC:0.0}°C";
    }

    public static string FormatPartBodyTemp(string partId, int warmth, float partTempC)
    {
        return $"{partId}"
               + $"\n{HoverWarm} {warmth}"
               + $"\n{HoverBodyTemp} {partTempC:0.0}°C";
    }

    public static string FormatBodyTempFeeling(BodyTempFeeling feeling)
    {
        switch (feeling)
        {
            case BodyTempFeeling.Cold:
                return GetOr(KeyFeelingCold, "추움");
            case BodyTempFeeling.Cool:
                return GetOr(KeyFeelingCool, "서늘");
            case BodyTempFeeling.Comfortable:
                return GetOr(KeyFeelingComfortable, "편안");
            case BodyTempFeeling.Warm:
                return GetOr(KeyFeelingWarm, "따뜻");
            case BodyTempFeeling.Hot:
                return GetOr(KeyFeelingHot, "더움");
            default:
                return EmptyValue;
        }
    }

    public static string FormatPartArmorStats(
        string partId,
        WearStatsAggregator.WearPartArmorStats stats)
    {
        string power = stats.PowerArmor ? HoverPowerArmorYes : EmptyValue;
        return $"{partId}"
               + $"\n{HoverEnc} {stats.Encumbrance}  {HoverMaxEnc} {stats.MaxEncumbrance}"
               + $"  {HoverCoverage} {stats.Coverage}"
               + $"\n{HoverEnvProt} {stats.EnvironmentalProtection}"
               + $"  {HoverThickness} {stats.MaterialThickness}"
               + $"  {HoverPowerArmor} {power}";
    }

    public static void AppendItemArmorHover(StringBuilder sb, ArmorDetailData armor)
    {
        if (sb == null || armor == null)
            return;

        sb.Append('\n').Append(HoverEnc).Append(' ').Append(armor.encumbrance);
        sb.Append("  ").Append(HoverMaxEnc).Append(' ').Append(armor.max_encumbrance);
        sb.Append("  ").Append(HoverCoverage).Append(' ').Append(armor.coverage);
        sb.Append('\n').Append(HoverEnvProt).Append(' ').Append(armor.environmental_protection);
        sb.Append("  ").Append(HoverThickness).Append(' ').Append(armor.material_thickness);
        if (armor.power_armor)
            sb.Append("  ").Append(HoverPowerArmor).Append(' ').Append(HoverPowerArmorYes);
        sb.Append("  ").Append(HoverWarm).Append(' ').Append(armor.warmth);
        string layer = WearOverlapRules.NormalizeLayer(armor);
        sb.Append('\n').Append(HoverLayer).Append(' ').Append(layer);
        if (armor.sided)
            sb.Append("  ").Append(HoverSided).Append(' ').Append(HoverSidedYes);
    }

    static string GetOr(string key, string fallback) =>
        Loc.TryGet(key, out string text) ? text : fallback;

    static string SafeFormat(string template, params object[] args)
    {
        try
        {
            return string.Format(template, args);
        }
        catch
        {
            return template;
        }
    }
}
