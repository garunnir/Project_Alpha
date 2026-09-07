// ============================================================
// CharacterVision — 공통 시야 판정 (각=Definition 실시간, 반경=상수). 페이드 표현은 시스템.
// ============================================================

using UnityEngine;

public static class CharacterVisionDefaults
{
    public const float DetectRadius = 10f;
    public const float LoseRadius = 14f;
    public const float SpotAngleDegrees = 90f;
    public const float InnerSpotAngleRatio = 0.6f;
    public const float SpotAngleMinDegrees = 1f;
    public const float SpotAngleMaxDegrees = 360f;
    const float MinDirSqr = 1e-8f;

    /// <summary>XZ 거리·시야각(전체 벌어진 각) 하드 판정. PC/NPC 공통.</summary>
    public static bool IsWithinConeXZ(
        Vector3 selfWorld,
        Vector3 forwardXZ,
        Vector3 targetWorld,
        float maxRadius,
        float spotAngleDegrees)
    {
        float dx = targetWorld.x - selfWorld.x;
        float dz = targetWorld.z - selfWorld.z;
        float distSq = dx * dx + dz * dz;
        float maxR = Mathf.Max(0f, maxRadius);
        if (distSq > maxR * maxR)
            return false;

        float halfOuter = Mathf.Max(0f, spotAngleDegrees) * 0.5f;
        if (halfOuter >= 179.9f)
            return true;

        if (distSq < MinDirSqr)
            return true;

        forwardXZ.y = 0f;
        if (forwardXZ.sqrMagnitude < MinDirSqr)
            return false;

        forwardXZ.Normalize();
        Vector3 toTarget = new Vector3(dx, 0f, dz);
        toTarget.Normalize();
        return Vector3.Angle(forwardXZ, toTarget) < halfOuter;
    }

    /// <summary>전방 대비 대상 XZ 각(도). 전방 없으면 -1.</summary>
    public static float AngleDegreesFromForwardXZ(
        Vector3 forwardXZ,
        float dx,
        float dz,
        float distanceXZ)
    {
        if (distanceXZ * distanceXZ < MinDirSqr)
            return 0f;

        forwardXZ.y = 0f;
        if (forwardXZ.sqrMagnitude < MinDirSqr)
            return -1f;

        forwardXZ.Normalize();
        return Vector3.Angle(forwardXZ, new Vector3(dx, 0f, dz) / distanceXZ);
    }

    public static float ClampSpotAngle(float degrees) =>
        Mathf.Clamp(degrees, SpotAngleMinDegrees, SpotAngleMaxDegrees);
}

[DisallowMultipleComponent]
public sealed class CharacterVision : MonoBehaviour
{
    CharacterSenseBlock _senses = CharacterSenseBlock.Default;
    float _spotAngleDegrees = CharacterVisionDefaults.SpotAngleDegrees;

    PlayerGearHost _gearHost;
    CharacterDefinitionBinder _definitionBinder;

    public float DetectRadius => _senses.sightDetectMeters;
    public float LoseRadius => _senses.sightLoseMeters;

    /// <summary>Definition SO가 있으면 그 시야각(실시간). 없으면 Apply 캐시.</summary>
    public float SpotAngleDegrees => ResolveBaseSpotAngle();

    public float InnerSpotAngleDegrees =>
        Mathf.Clamp(
            SpotAngleDegrees * CharacterVisionDefaults.InnerSpotAngleRatio,
            0f,
            SpotAngleDegrees);

    public float EffectiveDetectRadius
    {
        get
        {
            float factor = _gearHost != null ? _gearHost.VisionFactor : 1f;
            return DetectRadius * Mathf.Clamp01(factor);
        }
    }

    public float EffectiveLoseRadius
    {
        get
        {
            float factor = _gearHost != null ? _gearHost.VisionFactor : 1f;
            return LoseRadius * Mathf.Clamp01(factor);
        }
    }

    public float EffectiveSpotAngleDegrees =>
        CharacterVisionDefaults.ClampSpotAngle(SpotAngleDegrees * ResolveConeFactor());

    public float EffectiveInnerSpotAngleDegrees
    {
        get
        {
            float outer = EffectiveSpotAngleDegrees;
            float inner = outer * CharacterVisionDefaults.InnerSpotAngleRatio * ResolveConeFactor();
            return Mathf.Clamp(inner, 0f, outer);
        }
    }

    public void ApplyFromDefinition(CharacterDefinition definition)
    {
        _senses = definition != null ? definition.Senses : CharacterSenseBlock.Default;
        float angle = definition != null
            ? definition.SpotAngleDegrees
            : CharacterVisionDefaults.SpotAngleDegrees;
        _spotAngleDegrees = CharacterVisionDefaults.ClampSpotAngle(angle);
    }

    public bool CanDetect(Vector3 selfWorld, Vector3 forwardXZ, Vector3 targetWorld) =>
        CharacterVisionDefaults.IsWithinConeXZ(
            selfWorld,
            forwardXZ,
            targetWorld,
            EffectiveDetectRadius,
            EffectiveSpotAngleDegrees);

    public bool CanKeepTarget(Vector3 selfWorld, Vector3 forwardXZ, Vector3 targetWorld) =>
        CharacterVisionDefaults.IsWithinConeXZ(
            selfWorld,
            forwardXZ,
            targetWorld,
            EffectiveLoseRadius,
            EffectiveSpotAngleDegrees);

    void Awake()
    {
        if (_gearHost == null)
            _gearHost = CharacterBodyResolve.GetInBody<PlayerGearHost>(this);
        if (_definitionBinder == null)
            _definitionBinder = CharacterBodyResolve.GetInBody<CharacterDefinitionBinder>(this);
    }

    float ResolveBaseSpotAngle()
    {
        if (_definitionBinder == null)
            _definitionBinder = CharacterBodyResolve.GetInBody<CharacterDefinitionBinder>(this);

        CharacterDefinition def = _definitionBinder != null ? _definitionBinder.Definition : null;
        if (def != null)
            return CharacterVisionDefaults.ClampSpotAngle(def.SpotAngleDegrees);

        return CharacterVisionDefaults.ClampSpotAngle(_spotAngleDegrees);
    }

    float ResolveConeFactor() => 1f;
}
