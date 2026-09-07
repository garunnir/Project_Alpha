// ============================================================
// CharacterAttackerCombatVfx — Action 시전 VFX + Hit + 피 오버레이 (plain)
// ============================================================

using Lean.Pool;
using UnityEngine;

public sealed class CharacterAttackerCombatVfx
{
    readonly CharacterAttacker _attacker;
    readonly TimeScaleChannel _timeChannel;
    bool _bound;

    public CharacterAttackerCombatVfx(CharacterAttacker attacker, TimeScaleChannel timeChannel)
    {
        _attacker = attacker;
        _timeChannel = timeChannel;
    }

    public void Bind()
    {
        if (_attacker == null || _bound)
            return;

        _attacker.AttackResolved += OnAttackResolved;
        _attacker.AttackJudged += OnAttackJudged;
        _attacker.AttackCueFired += OnAttackCueFired;
        _bound = true;
    }

    public void Unbind()
    {
        if (_attacker == null || !_bound)
            return;

        _attacker.AttackResolved -= OnAttackResolved;
        _attacker.AttackJudged -= OnAttackJudged;
        _attacker.AttackCueFired -= OnAttackCueFired;
        _bound = false;
    }

    void OnAttackResolved(AttackOutcome outcome)
    {
        if (outcome.Result != AttackPerformResult.Performed)
            return;

        ArmAnimSlotCatalog pipeline = ResolvePipeline();
        WeaponActionVfx vfx = WeaponActionVfxResolver.Resolve(
            _attacker.Presentation,
            outcome.Action,
            pipeline);
        if (vfx == null)
            return;

        Spawn(vfx.actionVfx, outcome.OriginPoint, outcome.Direction);
    }

    void OnAttackJudged(AttackOutcome outcome)
    {
        bool obstructed = outcome.Result == AttackPerformResult.Obstructed;
        if (obstructed)
        {
            if (WeaponAttack.AllowsImpactReaction(outcome.Attack, ArmImpactKind.Blocked))
                SpawnImpactKind(ArmImpactKind.Blocked, outcome.OriginPoint, outcome.Direction);
        }

        if (!obstructed &&
            outcome.Result != AttackPerformResult.Performed &&
            outcome.Result != AttackPerformResult.Miss)
            return;

        WeaponImpactVfxDefaults impactDefaults = _attacker.Catalog != null
            ? _attacker.Catalog.ImpactVfxDefaults
            : null;

        GameObject woundPrefab = ResolveWoundOverlay(outcome, impactDefaults);
        WeaponActionVfx vfx = WeaponActionVfxResolver.ResolveImpact(
            outcome.Attack,
            _attacker.Presentation,
            outcome.Action,
            ResolvePipeline(),
            impactDefaults,
            outcome.HitTag);

        GameObject impactPrefab = null;
        if (vfx != null)
            impactPrefab = outcome.DidHit ? vfx.hitVfx : vfx.missVfx;

        if (impactPrefab == null && woundPrefab == null)
            return;

        Vector3 impactForward = -outcome.Direction;
        if (outcome.ResolveMode == WeaponResolveMode.RangedRay)
        {
            if (WeaponAttack.UsesFlightProjectile(outcome.Attack))
            {
                Spawn(impactPrefab, outcome.ImpactPoint, impactForward);
                Spawn(woundPrefab, outcome.ImpactPoint, impactForward);
                return;
            }

            GameObject tracerPrefab = vfx != null ? vfx.tracerVfx : null;
            if (SpawnTracer(tracerPrefab, outcome, impactPrefab, woundPrefab))
                return;

            Spawn(impactPrefab, outcome.ImpactPoint, impactForward);
            Spawn(woundPrefab, outcome.ImpactPoint, impactForward);
            return;
        }

        Spawn(impactPrefab, outcome.ImpactPoint, impactForward);
        Spawn(woundPrefab, outcome.ImpactPoint, impactForward);
    }

    static GameObject ResolveWoundOverlay(AttackOutcome outcome, WeaponImpactVfxDefaults defaults)
    {
        if (defaults == null || !outcome.DidHit)
            return null;
        if (outcome.DidSeverPart)
            return defaults.SeverBleedVfx;
        if (outcome.LeftCutWound)
            return defaults.CutBleedVfx;
        return null;
    }

    void OnAttackCueFired(WieldHand hand, WeaponAction action)
    {
        if (_attacker != null &&
            !_attacker.AllowsImpactReaction(action, ArmImpactKind.Recoil))
            return;
        SpawnImpactKind(ArmImpactKind.Recoil, _attacker.ResolveOrigin(), _attacker.transform.forward);
    }

    void SpawnImpactKind(ArmImpactKind kind, Vector3 origin, Vector3 forward)
    {
        WeaponActionVfx vfx = WeaponActionVfxResolver.ResolveImpactKind(ResolvePipeline(), kind);
        if (vfx == null)
            return;
        Spawn(vfx.actionVfx, origin, forward);
    }

    ArmAnimSlotCatalog ResolvePipeline()
    {
        if (_attacker?.Catalog != null && _attacker.Catalog.AnimPipeline != null)
            return _attacker.Catalog.AnimPipeline;
        CharacterLocomotionAnim loc = CharacterBodyResolve.GetInBody<CharacterLocomotionAnim>(_attacker);
        return loc != null ? loc.ArmSlotCatalog : null;
    }

    GameObject Spawn(GameObject prefab, Vector3 position, Vector3 forward)
    {
        if (prefab == null)
            return null;

        Quaternion rotation = forward.sqrMagnitude > 1e-6f
            ? Quaternion.LookRotation(forward, Vector3.up)
            : Quaternion.identity;

        GameObject instance = LeanPool.Spawn(prefab, position, rotation);
        VfxChannelTicker ticker = instance.GetComponent<VfxChannelTicker>();
        if (ticker != null)
            ticker.SetChannel(_timeChannel);
        return instance;
    }

    bool SpawnTracer(
        GameObject prefab,
        AttackOutcome outcome,
        GameObject impactPrefab,
        GameObject woundPrefab)
    {
        GameObject instance = Spawn(prefab, outcome.OriginPoint, outcome.Direction);
        if (instance == null)
            return false;

        VfxTracerLine tracer = instance.GetComponent<VfxTracerLine>();
        if (tracer == null)
            return false;

        tracer.Play(
            outcome.OriginPoint,
            outcome.ImpactPoint,
            impactPrefab,
            _timeChannel,
            woundPrefab);
        return true;
    }
}
