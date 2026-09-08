// ============================================================
// AttackOutcome — 시전된 공격 1회의 판정 결과 스냅샷 (연출 소비용)
// ============================================================

using System;
using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

public readonly struct AttackOutcome
{
    public readonly CombatLeaf Action;
    public readonly WieldHand Hand;
    public readonly WeaponResolveMode ResolveMode;
    public readonly AttackPerformResult Result;
    public readonly CharacterBodyHost Target;
    /// <summary>시전한 몸. 연출·메시지 로그용.</summary>
    public readonly CharacterBodyHost Attacker;
    public readonly string AimedPartId;
    /// <summary>완화 후 HP. 0이어도 히트일 수 있음.</summary>
    public readonly int Damage;

    /// <summary>완화 전 HP 합. 오버펜 분할에만 p = Damage/RawDamage.</summary>
    public readonly int RawDamage;

    /// <summary>이 몸에 전달된 J_hit (밀침). 데미지 아님.</summary>
    public readonly float ImpulseJin;

    /// <summary>공격이 출발한 월드 지점 (근접 휘두름 기준점 / 원거리 총구).</summary>
    public readonly Vector3 OriginPoint;

    /// <summary>타격이 닿은 월드 지점. 빗나감·차폐 시에도 채워진다.</summary>
    public readonly Vector3 ImpactPoint;

    /// <summary>Origin에서 Impact를 향하는 정규화 방향.</summary>
    public readonly Vector3 Direction;

    /// <summary>Hit 특성 키 (bash/cut/bullet). Action 시그널·Reaction은 비움.</summary>
    public readonly string HitTag;

    /// <summary>이번 히트가 유기 부위에 남긴 조직 부상 ID (bruise/cut/gunshot). 절단·의체·피해 0이면 빈 문자열.</summary>
    public readonly string AppliedTissueId;

    /// <summary>이번 히트에서 부위 파괴(RemovePart)에 성공했다.</summary>
    public readonly bool DidSeverPart;

    public readonly WeaponAttack Attack;

    /// <summary>무기 축 접촉 0=손/자루 ~ 1=끝. 근접 히트박스만. 치명타 Pending.</summary>
    public readonly float WeaponReach01;

    /// <summary>피격자가 공격자를 시력으로 인지하지 못한 채 맞음.</summary>
    public readonly bool IsSurprise;

    /// <summary>근접 기습 특수 (목 조준 시도 / 기절). 피해 배율과 별개.</summary>
    public readonly SurpriseMeleeKind SurpriseMelee;

    /// <summary>시전 시작 시 Attack thin에 기습 클립 사용 (히트 판정과 분리).</summary>
    public readonly bool UseSurpriseAttackClip;

    public bool DidHit => Result == AttackPerformResult.Performed;

    /// <summary>자상 엔트리가 이번 히트에 남음. 절단 성공은 false (소켓 Bleed만).</summary>
    public bool LeftCutWound =>
        DidHit &&
        !DidSeverPart &&
        string.Equals(AppliedTissueId, BodyPartEffectIds.Cut, StringComparison.Ordinal);

    public AttackOutcome(
        CombatLeaf action,
        WieldHand hand,
        WeaponResolveMode resolveMode,
        AttackPerformResult result,
        CharacterBodyHost target,
        string aimedPartId,
        int damage,
        Vector3 originPoint,
        Vector3 impactPoint,
        string hitTag = null,
        WeaponAttack attack = null,
        float weaponReach01 = 0f,
        int rawDamage = 0,
        float impulseJin = 0f,
        string appliedTissueId = null,
        bool didSeverPart = false,
        bool isSurprise = false,
        SurpriseMeleeKind surpriseMelee = SurpriseMeleeKind.None,
        bool useSurpriseAttackClip = false,
        CharacterBodyHost attacker = null)
    {
        Action = action;
        Hand = hand;
        ResolveMode = resolveMode;
        Result = result;
        Target = target;
        Attacker = attacker;
        AimedPartId = aimedPartId;
        Damage = damage;
        RawDamage = rawDamage;
        ImpulseJin = Mathf.Max(0f, impulseJin);
        OriginPoint = originPoint;
        ImpactPoint = impactPoint;
        HitTag = hitTag ?? string.Empty;
        AppliedTissueId = appliedTissueId ?? string.Empty;
        DidSeverPart = didSeverPart;
        Attack = attack;
        WeaponReach01 = Mathf.Clamp01(weaponReach01);
        IsSurprise = isSurprise;
        SurpriseMelee = surpriseMelee;
        UseSurpriseAttackClip = useSurpriseAttackClip;

        Vector3 offset = impactPoint - originPoint;
        Direction = offset.sqrMagnitude > 1e-6f ? offset.normalized : Vector3.forward;
    }
}
