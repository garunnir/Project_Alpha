// ============================================================
// CharacterPainHost — PainTotal·고통 쇼크·기습 스턴 래치 (Defeat/Dead 아님)
// ============================================================

using System;
using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

public sealed class CharacterPainHost
{
    readonly List<BodyPartEffect> _effectScratch = new(16);

    CharacterBodyRefs _refs;
    CharacterBodyHost _bodyHost;
    CharacterMotor _motor;
    CharacterActionHost _actionHost;
    ICharacterBody _subscribed;
    bool _painShocked;
    bool _painLatched;
    float _lastEffective;
    float _surpriseStunRemain;
    bool _enabled;

    public event Action Changed;

    public CharacterBodyRefs BodyRefs => _refs;
    public bool IsPainShocked => _painShocked;
    public float EffectivePain01 => _lastEffective;
    public float SurpriseStunRemain => _surpriseStunRemain;

    public void Bind(CharacterBodyRefs refs)
    {
        _refs = refs;
        _bodyHost = refs != null ? refs.BodyHost : null;
        _motor = refs != null ? refs.Motor : null;
        _actionHost = refs != null ? refs.ActionHost : null;
        if (_enabled)
            Enable();
    }

    public void Enable()
    {
        _enabled = true;
        BindBody();
        Refresh();
    }

    public void Disable()
    {
        _enabled = false;
        UnbindBody();
        ReleasePossessedInputPolicy();
    }

    /// <summary>기습 스턴 잔여. heap 없음.</summary>
    public void Tick()
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
            _actionHost?.InterruptAll(CharacterInterruptReason.Knockdown);

        Changed?.Invoke();
    }
}
