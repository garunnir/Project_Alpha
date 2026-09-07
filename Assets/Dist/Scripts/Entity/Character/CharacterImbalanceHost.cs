// ============================================================
// CharacterImbalanceHost — 불균형 미터·이속 배율·자빠짐 엣지 (이동 잠금 타이머 아님)
// ============================================================

using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterBodyHost))]
public sealed class CharacterImbalanceHost : MonoBehaviour
{
    CharacterMotor _motor;
    PlayerMovement _movement;
    CharacterActionHost _actionHost;
    CharacterAttacker _attacker;
    float _imbalance;
    float _lastEmittedBucket = -1f;

    public static CharacterImbalanceHost Active => CharacterSessionHub.ImbalanceHost;

    public void ClaimActive() { }
    public event Action Changed;

    public float Imbalance01 => _imbalance;
    public bool IsFullyUnbalanced => _imbalance >= 1f - 1e-4f;
    public float MoveSpeedFactor => CombatImbalance.MoveSpeedFactor(_imbalance);
    public float HitAccuracyFactor => CombatImbalance.HitAccuracyFactor(_imbalance);

    /// <summary>디버그/치트용. 클램프 후 이속 배율 적용 + Changed.</summary>
    public void SetImbalance01(float value)
    {
        float next = Mathf.Clamp01(value);
        if (Mathf.Abs(next - _imbalance) < 1e-6f)
            return;

        _imbalance = next;
        ApplySpeedFactor(MoveSpeedFactor);
        _lastEmittedBucket = CombatImbalance.BucketIntensity(_imbalance);
        Changed?.Invoke();
    }

    void Awake()
    {
        _motor = CharacterBodyResolve.GetInBody<CharacterMotor>(this);
        _movement = CharacterBodyResolve.GetInBody<PlayerMovement>(this);
        TryGetComponent(out _actionHost);
        TryGetComponent(out _attacker);
    }

    void OnDisable() => ApplySpeedFactor(1f);

    void Update()
    {
        if (_imbalance <= 0f)
            return;

        bool possessed = _motor != null && _motor.IsPossessed;
        float dt = TimeScaleService.Delta(
            possessed ? TimeScaleChannel.Player : TimeScaleChannel.World);
        if (dt <= 0f)
            return;

        float before = _imbalance;
        _imbalance = Mathf.Max(0f, _imbalance - CombatImbalance.RecoverPerSecond * dt);
        if (Mathf.Abs(before - _imbalance) < 1e-6f)
            return;

        ApplySpeedFactor(MoveSpeedFactor);
        EmitChangedIfBucketMoved();
    }

    /// <summary>
    /// 피격 Δv를 불균형에 더함. 1로 닿고 능동 속도가 FallSpeedMin 이상이면 자빠짐 true.
    /// </summary>
    public bool ApplyHit(float deltaV)
    {
        float drain = CombatImbalance.DrainFromDeltaV(deltaV);
        if (drain <= 0f)
            return false;

        float before = _imbalance;
        _imbalance = Mathf.Clamp01(_imbalance + drain);
        ApplySpeedFactor(MoveSpeedFactor);
        EmitChangedIfBucketMoved();

        bool crossedFull = before < 1f - 1e-4f && _imbalance >= 1f - 1e-4f;
        if (!crossedFull)
            return false;

        float activeSpeed = _motor != null ? _motor.CurrentSpeed : 0f;
        return activeSpeed >= CombatImbalance.FallSpeedMin;
    }

    public void NotifyFallen()
    {
        _actionHost?.CancelAll();
        _attacker?.CancelAllPendingCues();
    }

    void ApplySpeedFactor(float factor)
    {
        float f = Mathf.Max(0f, factor);
        if (_movement != null)
            _movement.SetImbalanceMovement(f);
        else
            _motor?.SetImbalanceMovement(f);
    }

    void EmitChangedIfBucketMoved()
    {
        float bucket = CombatImbalance.BucketIntensity(_imbalance);
        if (Mathf.Abs(bucket - _lastEmittedBucket) < 1e-6f)
            return;
        _lastEmittedBucket = bucket;
        Changed?.Invoke();
    }
}
