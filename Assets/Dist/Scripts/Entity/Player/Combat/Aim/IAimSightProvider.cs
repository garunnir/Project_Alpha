// ============================================================
// IAimSightProvider — 조준 시선 해석 (Layer1). CharacterState만 소비자
// ============================================================

using IsoTilemap;
using UnityEngine;

/// <summary>
/// 입력 게이트 밖의 조준 월드점·시선 방향 해석. 기본 구현:
/// <see cref="PlayerMouseSphereAimProvider"/>.
/// </summary>
public interface IAimSightProvider
{
    bool TryUpdateSight(CharacterState state, Transform body, in AimSightContext ctx);
}

/// <summary>조준 SphereCast·오클루전 설정. 진실원은 PlayerAimController Inspector.</summary>
public readonly struct AimSightContext
{
    public readonly Camera Camera;
    public readonly MapTopologyLineCast TopologyLineCast;
    public readonly float CastOriginYOffset;
    public readonly float SphereRadius;
    public readonly float MaxDistance;
    public readonly bool FlattenAimYToPlayerHeight;
    public readonly LayerMask ObstructionMask;

    public AimSightContext(
        Camera camera,
        MapTopologyLineCast topologyLineCast,
        float castOriginYOffset,
        float sphereRadius,
        float maxDistance,
        bool flattenAimYToPlayerHeight,
        LayerMask obstructionMask)
    {
        Camera = camera;
        TopologyLineCast = topologyLineCast;
        CastOriginYOffset = castOriginYOffset;
        SphereRadius = sphereRadius;
        MaxDistance = maxDistance;
        FlattenAimYToPlayerHeight = flattenAimYToPlayerHeight;
        ObstructionMask = obstructionMask;
    }

    public PlayerSightTarget.Settings ToSightSettings() => new()
    {
        CastOriginYOffset = CastOriginYOffset,
        SphereRadius = SphereRadius,
        MaxDistance = MaxDistance,
        FlattenAimYToPlayerHeight = FlattenAimYToPlayerHeight,
        ObstructionMask = ObstructionMask,
    };
}
