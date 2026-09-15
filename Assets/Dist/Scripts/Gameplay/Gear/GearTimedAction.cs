// ============================================================
// GearTimedAction — Wear/Wield 진행 바용 단일 타이머
// ============================================================

using System;
using UnityEngine;

public sealed class GearTimedAction
{
    public enum Kind
    {
        None = 0,
        Wear,
        TakeOff,
        Wield,
        Unwield,
        InventoryTransfer,
        AmmoLoad,
        MagAttach,
        StripWear
    }

    float _elapsed;
    float _duration;
    Action _onComplete;

    public Kind CurrentKind { get; private set; }
    public bool IsRunning => CurrentKind != Kind.None;
    public float Duration => _duration;
    public float Elapsed => _elapsed;
    public float Progress01 =>
        _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);

    public event Action Changed;
    public event Action Completed;
    public event Action Cancelled;

    public bool TryBegin(Kind kind, float durationSeconds, Action onComplete)
    {
        if (IsRunning || kind == Kind.None || onComplete == null)
            return false;

        CurrentKind = kind;
        _duration = Mathf.Max(0f, durationSeconds);
        _elapsed = 0f;
        _onComplete = onComplete;
        Changed?.Invoke();

        if (_duration <= 0f)
        {
            FinishSuccess();
            return true;
        }

        return true;
    }

    /// <summary>Immediate 경로 — running이면 Cancel 후 Begin.</summary>
    public bool TryBeginOrPreempt(Kind kind, float durationSeconds, Action onComplete)
    {
        if (kind == Kind.None || onComplete == null)
            return false;

        if (IsRunning)
            Cancel();

        return TryBegin(kind, durationSeconds, onComplete);
    }

    public void Cancel()
    {
        if (!IsRunning)
            return;

        ClearState();
        Cancelled?.Invoke();
        Changed?.Invoke();
    }

    public void Tick(float deltaSeconds)
    {
        if (!IsRunning || deltaSeconds <= 0f)
            return;

        _elapsed += deltaSeconds;
        Changed?.Invoke();
        if (_elapsed < _duration)
            return;

        FinishSuccess();
    }

    void FinishSuccess()
    {
        Action complete = _onComplete;
        ClearState();
        complete?.Invoke();
        Completed?.Invoke();
        Changed?.Invoke();
    }

    void ClearState()
    {
        CurrentKind = Kind.None;
        _elapsed = 0f;
        _duration = 0f;
        _onComplete = null;
    }
}
