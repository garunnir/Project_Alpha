// ============================================================
// PlayerMouseSphereAimProvider — 마우스 평면 + SphereCast 조준 (Layer1 default)
// ============================================================

using UnityEngine;

/// <summary>
/// <see cref="PlayerSightTarget.TryResolveWorldPoint"/> → <see cref="CharacterState.SetAimDir"/>.
/// </summary>
public sealed class PlayerMouseSphereAimProvider : IAimSightProvider
{
    public bool TryUpdateSight(CharacterState state, Transform body, in AimSightContext ctx)
    {
        if (state == null || body == null)
            return false;

        if (!PlayerSightTarget.TryResolveWorldPoint(
                body,
                ctx.Camera,
                ctx.TopologyLineCast,
                ctx.ToSightSettings(),
                out Vector3 aimPoint))
        {
            return false;
        }

        Vector3 origin = body.position + Vector3.up * ctx.CastOriginYOffset;
        Vector3 sightFlat = aimPoint - origin;
        sightFlat.y = 0f;
        if (sightFlat.sqrMagnitude < 1e-4f)
            return false;

        state.SetAimDir(sightFlat.normalized, aimPoint, sightFlat.magnitude);
        return true;
    }
}
