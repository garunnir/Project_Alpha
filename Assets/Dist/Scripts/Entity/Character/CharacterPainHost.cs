// ============================================================
// CharacterPainHost — PainTotal·고통 쇼크·기습 스턴 래치 (Defeat/Dead 아님)
// ============================================================

using System;
using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterBodyHost))]
public sealed class CharacterPainHost : MonoBehaviour
{
    readonly List<BodyPartEffect> _effectScratch = new(16);

    CharacterBodyHost _bodyHost;
    CharacterMotor _motor;
    CharacterActionHost _actionHost;
    CharacterAttacker _attacker;
    ICharacterBody _subscribed;
    bool _painShocked;
    bool _painLatched;
    float _lastEffective;
    float _surpriseStunRemain;

    public event Action Changed;

    public bool IsPainShocked => _painShocked;
    public float EffectivePain01 => _lastEffective;
    public float SurpriseStunRemain => _surpriseStunRemain;

    void Awake()
    {
        _bodyHost = GetComponent<CharacterBodyHost>();
        _motor = CharacterBodyResolve.GetInBody<CharacterMotor>(this);
        TryGetComponent(out _actionHost);
        TryGetComponent(out _attacker);
    }

    void OnEnable()
    {
        BindBody();
        Refresh();
    }

    void OnDisable()
    {
        UnbindBody();
        ReleasePossessedInputPolicy();
    }

    void Update()
    {
        if (_surpriseStunRemain <= 0f)
            return;

        float dt = TimeScaleService.Delta(TimeScaleChannel.World);
        if (dt <= 0f)
            return;

        _surpriseStunRemain = Mathf.Max(0f, _surpriseStunRemain - dt);
        if (_surpriseStunRemain <= 0f)
            Refresh();
    }

    /// <summary>기습 기절 래치. 고통/용량 다운과 OR. Defeat 아님.</summary>
    public void ApplySurpriseStun(float seconds)
    {
        float duration = Mathf.Max(0f, seconds);
        if (duration <= 0f)
            return;

        _surpriseStunRemain = Mathf.Max(_surpriseStunRemain, duration);
        Refresh();
    }

    /// <summary>
    /// possessed 전환 후 호출. 다운 중이면 <see cref="InputManager"/> Move/Aim 억제를 맞춘다.
    /// </summary>
    public void SyncPossessedInputPolicy()
    {
        InputManager input = InputManager.Instance;
        if (input == null)
            return;

        bool suppress = _painShocked && _motor != null && _motor.IsPossessed;
        input.SuppressPlayerAction(PlayerAction.Move, this, suppress);
        input.SuppressPlayerAction(PlayerAction.Aim, this, suppress);
    }

    void ReleasePossessedInputPolicy()
    {
        InputManager input = InputManager.Instance;
        if (input == null)
            return;

        input.SuppressPlayerAction(PlayerAction.Move, this, false);
        input.SuppressPlayerAction(PlayerAction.Aim, this, false);
    }

    void BindBody()
    {
        UnbindBody();
        _subscribed = _bodyHost != null ? _bodyHost.Body : null;
        if (_subscribed != null)
            _subscribed.Changed += OnBodyChanged;
    }

    void UnbindBody()
    {
        if (_subscribed != null)
            _subscribed.Changed -= OnBodyChanged;
        _subscribed = null;
    }

    void OnBodyChanged() => Refresh();

    public void Refresh()
    {
        ICharacterBody body = _bodyHost != null ? _bodyHost.Body : null;
        if (body == null || body.IsDeadState)
        {
            _painLatched = false;
            _surpriseStunRemain = 0f;
            SetShocked(false);
            _lastEffective = 0f;
            return;
        }

        _lastEffective = CombatPain.EffectivePain01(body, _effectScratch);
        bool painDown = CombatPain.IsPainDown(_lastEffective, _painLatched);
        _painLatched = painDown;
        bool shocked = painDown ||
                       BodyCapacity.IsCapacityDowned(body) ||
                       _surpriseStunRemain > 0f;
        SetShocked(shocked);
    }

    void SetShocked(bool shocked)
    {
        if (_painShocked == shocked)
            return;

        _painShocked = shocked;
        _motor?.SetMoveLocked(shocked);
        SyncPossessedInputPolicy();
        if (shocked)
        {
            _actionHost?.CancelAll();
            _attacker?.CancelAllPendingCues();
        }

        Changed?.Invoke();
    }
}
