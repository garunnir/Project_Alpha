// ============================================================
// CharacterSenseGizmo — 캐릭터 가시·가청 범위 Scene/Play 디버그 기즈모
// ============================================================

using UnityEngine;

public static class CharacterSenseGizmo
{
    public const bool DrawGizmos = true;
    public const bool OnlyWhenSelected = false;
    public const bool DrawVisionDetect = true;
    public const bool DrawVisionLose = true;
    public const bool DrawHearing = true;

#if UNITY_EDITOR
    public static void Draw(CharacterBodyRefs refs, bool selectedOnly)
    {
        if (!DrawGizmos || refs == null)
            return;

        if (OnlyWhenSelected != selectedOnly)
            return;

        ResolveRadii(
            refs,
            out float detectRadius,
            out float loseRadius,
            out float hearingRadius,
            out float spotAngle,
            out float innerSpotAngle);

        Transform transform = refs.transform;
        CharacterState state = refs.State;
        if (state == null)
            refs.TryGetComponent(out state);

        Vector3 center = CharacterFeetPose.GetFeetWorld(transform);
        Vector3 forward = CharacterSightForward.ResolveXZ(state, transform);

        if (DrawVisionDetect && detectRadius > 0f)
        {
            CharacterSightFadeGizmoColors.DrawVisionSectorXZ(
                center,
                forward,
                detectRadius,
                0f,
                spotAngle,
                innerSpotAngle);
        }

        if (DrawVisionLose && loseRadius > 0f && loseRadius > detectRadius + 0.01f)
        {
            CharacterSenseGizmoColors.DrawVisionConeWireXZ(
                center,
                forward,
                loseRadius,
                spotAngle,
                CharacterSenseGizmoColors.VisionLoseWire);
        }

        if (DrawHearing && hearingRadius > 0f)
            CharacterSenseGizmoColors.DrawHearingSphereGizmos(center, hearingRadius);
    }

    static void ResolveRadii(
        CharacterBodyRefs refs,
        out float detectRadius,
        out float loseRadius,
        out float hearingRadius,
        out float spotAngle,
        out float innerSpotAngle)
    {
        CharacterVision vision = refs.Vision;
        CharacterHearing hearing = refs.Hearing;
        if (Application.isPlaying && vision != null)
        {
            detectRadius = vision.EffectiveDetectRadius;
            loseRadius = vision.EffectiveLoseRadius;
            spotAngle = vision.EffectiveSpotAngleDegrees;
            innerSpotAngle = vision.EffectiveInnerSpotAngleDegrees;
            hearingRadius = hearing != null ? hearing.EffectiveHearingRadius : 0f;
            return;
        }

        CharacterDefinitionBinder binder = refs.DefinitionBinder;
        if (binder == null)
            refs.TryGetComponent(out binder);

        CharacterDefinition definition = binder != null ? binder.Definition : null;
        CharacterSenseBlock senses = definition != null ? definition.Senses : CharacterSenseBlock.Default;
        detectRadius = senses.sightDetectMeters;
        loseRadius = senses.sightLoseMeters;
        hearingRadius = senses.hearingRadiusMeters;
        spotAngle = CharacterVisionDefaults.ClampSpotAngle(
            definition != null
                ? definition.SpotAngleDegrees
                : CharacterVisionDefaults.SpotAngleDegrees);
        innerSpotAngle = spotAngle * CharacterVisionDefaults.InnerSpotAngleRatio;
    }
#else
    public static void Draw(CharacterBodyRefs refs, bool selectedOnly)
    {
    }
#endif
}
