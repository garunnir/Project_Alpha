// ============================================================
// PlayerSightVisionBinder — possessed CharacterVision → Spot range·각·yaw
// ============================================================

using UnityEngine;

/// <summary>
/// 씬 <c>PlayerSight</c> 시스템 리그. Spot Light에는 로직 MB 없음.
/// 반경·시야각 SSOT는 possessed <see cref="CharacterVision"/> — Spot은 시각 동기만.
/// </summary>
public static class PlayerSightVisionBinder
{
    public const string RootName = "PlayerSight";

    static Transform s_root;
    static Light s_spotLight;
    static float s_lastRange = -1f;
    static float s_lastSpotAngle = -1f;
    static float s_lastInnerAngle = -1f;
    static CharacterState s_boundState;
    static CharacterVision s_boundVision;

    public static float SpotAngleDegrees
    {
        get
        {
            if (TryGetConeAngles(out float spot, out _))
                return spot;
            return CharacterVisionDefaults.SpotAngleDegrees;
        }
    }

    public static float InnerSpotAngleDegrees
    {
        get
        {
            if (TryGetConeAngles(out _, out float inner))
                return inner;
            return CharacterVisionDefaults.SpotAngleDegrees * CharacterVisionDefaults.InnerSpotAngleRatio;
        }
    }

    public static bool TryGetConeAngles(out float spotAngleDegrees, out float innerSpotAngleDegrees)
    {
        spotAngleDegrees = CharacterVisionDefaults.SpotAngleDegrees;
        innerSpotAngleDegrees = CharacterVisionDefaults.SpotAngleDegrees * CharacterVisionDefaults.InnerSpotAngleRatio;

        CharacterVision vision = s_boundVision;
        if (vision == null)
        {
            vision = ResolveVision(s_boundState);
            s_boundVision = vision;
        }

        if (vision == null)
            return false;

        spotAngleDegrees = vision.EffectiveSpotAngleDegrees;
        innerSpotAngleDegrees = vision.EffectiveInnerSpotAngleDegrees;
        return true;
    }

    /// <summary>PlayerSight 루트 XZ 전방 — Spot 시각 부채꼴과 동일.</summary>
    public static bool TryGetSightForwardXZ(out Vector3 forwardXZ)
    {
        forwardXZ = Vector3.forward;
        if (!EnsureResolved() || s_root == null)
            return false;

        forwardXZ = s_root.forward;
        forwardXZ.y = 0f;
        if (forwardXZ.sqrMagnitude < 1e-6f)
            return false;

        forwardXZ.Normalize();
        return true;
    }

    /// <summary>possess / Bind 직후. Light에 컨트롤러를 붙이지 않고 state만 캐시.</summary>
    public static void Bind(PlayerPossessedInputHost host)
    {
        CharacterState state = host != null ? host.BodyState : null;
        if (!EnsureResolved())
        {
            s_boundState = null;
            s_boundVision = null;
            return;
        }

        s_boundState = state;
        s_boundVision = ResolveVision(state);

        s_lastRange = -1f;
        s_lastSpotAngle = -1f;
        s_lastInnerAngle = -1f;
        SyncLightFromVision(host);
        ApplyFacingYaw(state);
    }

    public static void Clear()
    {
        s_boundState = null;
        s_boundVision = null;
        s_lastRange = -1f;
        s_lastSpotAngle = -1f;
        s_lastInnerAngle = -1f;
    }

    /// <summary>매 프레임 range·각. state 바뀌면 Bind.</summary>
    public static void Sync(PlayerPossessedInputHost host)
    {
        if (host == null || host.Body == null)
        {
            if (s_boundState != null)
                Clear();
            return;
        }

        CharacterState state = host.BodyState;
        if (!ReferenceEquals(s_boundState, state))
        {
            Bind(host);
            return;
        }

        SyncLightFromVision(host);
    }

    /// <summary>
    /// PlayerSight 루트 yaw만 갱신 (Light GO에 MB 없음).
    /// <see cref="PossessedTransformFollower"/> LateUpdate에서 위치 적용 후 호출.
    /// </summary>
    public static void ApplyFacingYaw(CharacterState state)
    {
        if (!EnsureResolved() || s_root == null || state == null)
            return;

        Vector3 dir = state.GetFacingDir();
        dir.y = 0f;
        // 정지·MoveDir=0이면 루트 yaw 유지 (페이드/Spot 전방 SSOT)
        if (dir.sqrMagnitude < 1e-6f)
            return;

        dir.Normalize();
        s_root.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    static void SyncLightFromVision(PlayerPossessedInputHost host)
    {
        if (host == null || host.Body == null)
            return;
        if (!EnsureResolved() || s_spotLight == null)
            return;

        CharacterVision vision = s_boundVision;
        if (vision == null)
        {
            vision = ResolveVision(host.Body);
            s_boundVision = vision;
        }

        float targetRange = vision != null
            ? vision.EffectiveDetectRadius
            : CharacterVisionDefaults.DetectRadius;
        float targetSpot = vision != null
            ? vision.EffectiveSpotAngleDegrees
            : CharacterVisionDefaults.SpotAngleDegrees;
        float targetInner = vision != null
            ? vision.EffectiveInnerSpotAngleDegrees
            : CharacterVisionDefaults.SpotAngleDegrees * CharacterVisionDefaults.InnerSpotAngleRatio;

        if (Mathf.Abs(s_lastRange - targetRange) >= 0.001f)
        {
            s_spotLight.range = targetRange;
            s_lastRange = targetRange;
        }

        if (Mathf.Abs(s_lastSpotAngle - targetSpot) >= 0.01f ||
            Mathf.Abs(s_lastInnerAngle - targetInner) >= 0.01f)
        {
            s_spotLight.spotAngle = targetSpot;
            s_spotLight.innerSpotAngle = Mathf.Min(targetInner, targetSpot);
            s_lastSpotAngle = targetSpot;
            s_lastInnerAngle = targetInner;
        }
    }

    static bool EnsureResolved()
    {
        if (s_root != null && s_spotLight != null)
            return true;

        GameObject rootGo = GameObject.Find(RootName);
        if (rootGo == null)
        {
            s_root = null;
            s_spotLight = null;
            return false;
        }

        s_root = rootGo.transform;
        s_spotLight = null;
        Light[] lights = rootGo.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null || lights[i].type != LightType.Spot)
                continue;
            s_spotLight = lights[i];
            break;
        }

        return s_spotLight != null;
    }

    static CharacterVision ResolveVision(Component bodyMember) =>
        bodyMember != null ? CharacterBodyResolve.GetInBody<CharacterVision>(bodyMember) : null;

    static CharacterVision ResolveVision(GameObject body) =>
        body != null ? body.GetBodyComponent<CharacterVision>() : null;
}
