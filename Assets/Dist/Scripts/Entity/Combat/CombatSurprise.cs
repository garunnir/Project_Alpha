// ============================================================
// CombatSurprise — 기습 인지·피해 배율·근접 특수 STR 판정 SSOT
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

public enum SurpriseMeleeKind
{
    None = 0,
    Stun = 1,
    Neck = 2
}

public static class CombatSurprise
{
    public const float DamageMultiplier = 2.5f;

    public const float SpecialBaseChance = 0.45f;
    public const float SpecialPerStrDelta = 0.08f;
    public const float SpecialChanceMin = 0.1f;
    public const float SpecialChanceMax = 0.95f;

    public const float NeckBaseChance = 0.35f;
    public const float NeckPerStrDelta = 0.06f;
    public const float NeckChanceMin = 0.05f;
    public const float NeckChanceMax = 0.85f;

    /// <summary>기습 기절 래치 지속(초, World).</summary>
    public const float StunSeconds = 4f;

    /// <summary>
    /// observer가 subject를 시력으로 인지하는지.
    /// Vision lock(이미 Vision 타깃)이면 LoseRadius, 아니면 DetectRadius.
    /// </summary>
    public static bool HasVisionOf(CharacterBodyHost observer, CharacterBodyHost subject)
    {
        if (observer == null || subject == null || observer == subject)
            return false;

        float visibility = CharacterPresenceHost.ResolveVisibility01(subject);
        if (visibility <= 0f)
            return false;

        Transform observerTf = observer.transform;
        Vector3 selfFeet = CharacterFeetPose.GetFeetWorld(observerTf);
        Vector3 targetFeet = CharacterFeetPose.GetFeetWorld(subject.transform);

        CharacterState state = CharacterBodyResolve.GetInBody<CharacterState>(observer);
        Vector3 forward = CharacterSightForward.ResolveXZ(state, observerTf);

        bool visionLock = false;
        NpcManager npcManager = NpcManager.Active;
        if (npcManager != null)
            visionLock = npcManager.TryGetVisionLock(observer, subject);

        CharacterVision vision = CharacterBodyResolve.GetInBody<CharacterVision>(observer);
        return CharacterSightForward.IsWithinCone(
            vision,
            selfFeet,
            forward,
            targetFeet,
            visibility,
            visionLock);
    }

    public static bool IsSurpriseHit(CharacterBodyHost attacker, CharacterBodyHost target) =>
        attacker != null &&
        target != null &&
        !HasVisionOf(target, attacker);

    public static int ApplyDamageMultiplier(int rawDamage)
    {
        if (rawDamage <= 0)
            return 0;
        return Mathf.Max(1, Mathf.RoundToInt(rawDamage * DamageMultiplier));
    }

    public static int ResolveStrength(CharacterBodyHost host)
    {
        if (host != null &&
            CharacterBodyResolve.TryGetInBody(host, out CharacterSkillsHost skillsHost) &&
            skillsHost.Skills != null)
            return skillsHost.Skills.Level(AttributeIds.Str);
        return CombatMath.StrengthBaseline;
    }

    /// <summary>근접 기습 특수: None / Stun / Neck. 배율 피해와 별개.</summary>
    public static SurpriseMeleeKind RollMeleeSpecial(int attackerStr, int defenderStr)
    {
        int delta = attackerStr - defenderStr;
        float specialChance = Mathf.Clamp(
            SpecialBaseChance + delta * SpecialPerStrDelta,
            SpecialChanceMin,
            SpecialChanceMax);
        if (Random.value > specialChance)
            return SurpriseMeleeKind.None;

        float neckChance = Mathf.Clamp(
            NeckBaseChance + delta * NeckPerStrDelta,
            NeckChanceMin,
            NeckChanceMax);
        return Random.value < neckChance ? SurpriseMeleeKind.Neck : SurpriseMeleeKind.Stun;
    }

    /// <summary>애니용 1차 대상: NPC 전투 타깃, 없으면 조준축 사거리 안 최근접 적대.</summary>
    public static CharacterBodyHost ResolvePrimaryAnimTarget(CharacterBodyHost attacker)
    {
        if (attacker == null)
            return null;

        if (NpcManager.Active != null &&
            NpcManager.Active.TryGetCombatTarget(attacker, out CharacterBodyHost npcTarget) &&
            npcTarget != null)
            return npcTarget;

        if (!CharacterBodyResolve.TryGetInBody(attacker, out CharacterFactionHost selfFaction))
            return null;

        Transform tf = attacker.transform;
        Vector3 origin = CharacterFeetPose.GetFeetWorld(tf);
        Vector3 aimDir = tf.forward;
        float range = 2.5f;
        CharacterState state = CharacterBodyResolve.GetInBody<CharacterState>(attacker);
        if (state != null)
        {
            Vector3 sight = state.SightDir;
            if (sight.sqrMagnitude > 1e-6f)
                aimDir = sight;
            else if (state.AimWorldPoint.sqrMagnitude > 1e-6f)
            {
                Vector3 toAim = state.AimWorldPoint - origin;
                toAim.y = 0f;
                if (toAim.sqrMagnitude > 1e-6f)
                    aimDir = toAim.normalized;
            }
        }

        CharacterAttacker atk = CharacterBodyResolve.GetInBody<CharacterAttacker>(attacker);
        if (atk != null)
        {
            ItemData item = atk.ItemFor(atk.ItemId);
            float r = CombatMath.RangeMeters(item, atk.SelectedAction, null);
            if (r > 0f)
                range = r;
        }

        aimDir.y = 0f;
        if (aimDir.sqrMagnitude < 1e-6f)
            aimDir = Vector3.forward;
        aimDir.Normalize();

        CharacterBodyHost best = null;
        float bestDist = float.MaxValue;
        int hostCount = CharacterBodyHost.ActiveCount;
        for (int i = 0; i < hostCount; i++)
        {
            CharacterBodyHost host = CharacterBodyHost.GetActive(i);
            if (host == null || host == attacker)
                continue;
            if (host.Body == null || host.Body.IsDeadState)
                continue;
            if (!CharacterBodyResolve.TryGetInBody(host, out CharacterFactionHost otherFaction))
                continue;
            if (!CharacterHostility.IsHostile(selfFaction, otherFaction))
                continue;

            Vector3 feet = CharacterFeetPose.GetFeetWorld(host.transform);
            Vector3 offset = feet - origin;
            offset.y = 0f;
            float dist = offset.magnitude;
            if (dist > range || dist >= bestDist)
                continue;
            if (dist > 1e-4f && Vector3.Dot(aimDir, offset / dist) < 0.25f)
                continue;

            best = host;
            bestDist = dist;
        }

        return best;
    }
}
