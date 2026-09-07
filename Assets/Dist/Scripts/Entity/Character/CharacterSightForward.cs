// ============================================================
// CharacterSightForward — PC/NPC 시야 부채꼴 전방 XZ SSOT
// ============================================================

using UnityEngine;

public static class CharacterSightForward
{
    const float MinDirSqr = 1e-6f;

    /// <summary>
    /// 시야·기습·NPC 탐지 공통 XZ 전방.
    /// possessed + PlayerSight 리그 → Spot forward, else GetFacingDir → body yaw → world +Z.
    /// </summary>
    public static Vector3 ResolveXZ(CharacterState state, Transform bodyTransform)
    {
        if (state != null &&
            CharacterBodyResolve.TryGetInBody(state, out CharacterMotor motor) &&
            motor.IsPossessed &&
            PlayerSightVisionBinder.TryGetSightForwardXZ(out Vector3 spotForward))
        {
            return spotForward;
        }

        if (state != null)
        {
            Vector3 dir = state.GetFacingDir();
            dir.y = 0f;
            if (dir.sqrMagnitude > MinDirSqr)
                return dir.normalized;
        }

        if (bodyTransform != null)
        {
            Vector3 forward = bodyTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > MinDirSqr)
                return forward.normalized;
        }

        return Vector3.forward;
    }

    public static bool IsWithinDetectCone(
        CharacterVision vision,
        Vector3 selfFeet,
        Vector3 forwardXZ,
        Vector3 targetFeet,
        float visibility01) =>
        IsWithinCone(vision, selfFeet, forwardXZ, targetFeet, visibility01, keepRadius: false);

    public static bool IsWithinKeepCone(
        CharacterVision vision,
        Vector3 selfFeet,
        Vector3 forwardXZ,
        Vector3 targetFeet,
        float visibility01) =>
        IsWithinCone(vision, selfFeet, forwardXZ, targetFeet, visibility01, keepRadius: true);

    public static bool IsWithinCone(
        CharacterVision vision,
        Vector3 selfFeet,
        Vector3 forwardXZ,
        Vector3 targetFeet,
        float visibility01,
        bool keepRadius)
    {
        float visibility = Mathf.Clamp01(visibility01);
        if (visibility <= 0f)
            return false;

        float radius;
        float spotAngle;
        if (vision != null)
        {
            radius = (keepRadius ? vision.EffectiveLoseRadius : vision.EffectiveDetectRadius) * visibility;
            spotAngle = vision.EffectiveSpotAngleDegrees;
        }
        else
        {
            radius = (keepRadius
                ? CharacterVisionDefaults.LoseRadius
                : CharacterVisionDefaults.DetectRadius) * visibility;
            spotAngle = CharacterVisionDefaults.SpotAngleDegrees;
        }

        return CharacterVisionDefaults.IsWithinConeXZ(
            selfFeet,
            forwardXZ,
            targetFeet,
            radius,
            spotAngle);
    }
}
