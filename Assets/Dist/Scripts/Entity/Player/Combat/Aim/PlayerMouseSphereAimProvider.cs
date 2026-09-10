// ============================================================
// PlayerMouseSphereAimProvider — 마우스 평면 + SphereCast sample (Layer1 default)
// ============================================================

using UnityEngine;

/// <summary>
/// <see cref="PlayerSightTarget.TryResolveWorldPoint"/> → <see cref="AimSightSample"/>.
/// State 쓰기는 <see cref="CharacterSightHost.TryAcceptMouseAim"/>.
/// </summary>
public sealed class PlayerMouseSphereAimProvider : IAimSightProvider
{
    public bool TrySampleSight(Transform body, in AimSightContext ctx, out AimSightSample sample)
    {
        sample = default;
        if (body == null)
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

        sample = new AimSightSample(sightFlat.normalized, aimPoint, sightFlat.magnitude);
        return true;
    }
}
