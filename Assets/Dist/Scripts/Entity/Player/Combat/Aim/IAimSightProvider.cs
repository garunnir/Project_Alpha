// ============================================================
// IAimSightProvider — 조준 sample 해석 (Layer1 입력). CharacterState 직접 쓰기 없음
// ============================================================

using IsoTilemap;
using UnityEngine;

/// <summary>
/// 조준 월드점·시선 방향 sample. 기본 구현:
/// <see cref="PlayerMouseSphereAimProvider"/>. 소비·구독은 <see cref="CharacterSightHost"/>.
/// </summary>
public interface IAimSightProvider
{
    bool TrySampleSight(Transform body, in AimSightContext ctx, out AimSightSample sample);
}

/// <summary>
/// 조준 SphereCast·오클루전 설정.
/// Cast 파라미터 = PlayerAimController; FlattenAimY = CombatAimSightPolicy(Leaf) + 컨트롤러 기본.
/// </summary>
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
