// ============================================================
// CombatLeafVfx — Leaf 1개의 연출 프리팹 슬롯 묶음
// ============================================================

using System;
using UnityEngine;

[Serializable]
public sealed class CombatLeafVfx
{
    [Tooltip("시전 순간 공격자 쪽에 뜨는 연출 (근접 휘두름 / 원거리 총구).")]
    public GameObject actionVfx;

    [Tooltip("발사 지점에서 타격 지점까지 잇는 궤적. RangedRay에서만 사용.")]
    public GameObject tracerVfx;

    [Tooltip("명중 시 타격 지점 연출.")]
    public GameObject hitVfx;

    [Tooltip("빗나감·차폐 시 타격 지점 연출.")]
    public GameObject missVfx;
}
