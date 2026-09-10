// ============================================================
// AimSightSample — 입력층이 본체에 제안하는 조준 sample (쓰기 없음)
// ============================================================

using UnityEngine;

/// <summary>마우스/패드 SphereCast 결과. 본체 <see cref="CharacterSightHost"/>가 수락 여부 결정.</summary>
public readonly struct AimSightSample
{
    public readonly Vector3 SightDirFlat;
    public readonly Vector3 AimWorldPoint;
    public readonly float Reach;

    public AimSightSample(Vector3 sightDirFlat, Vector3 aimWorldPoint, float reach)
    {
        SightDirFlat = sightDirFlat;
        AimWorldPoint = aimWorldPoint;
        Reach = reach;
    }
}
