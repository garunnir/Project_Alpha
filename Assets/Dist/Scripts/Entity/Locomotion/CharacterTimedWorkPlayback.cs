// ============================================================
// CharacterTimedWorkPlayback — Work Layer 타이머·클립 재생 (plain class)
// ============================================================

using System;
using UnityEngine;

/// <summary>
/// Farm/Fish 셀 Work 대기. Animator·WorkLayerIndex는
/// <see cref="CharacterLocomotionAnim"/> 바인드 후 <see cref="Bind"/>로 주입.
/// </summary>
public sealed class CharacterTimedWorkPlayback
{
    Animator _animator;
    int _workLayerIndex = -1;
    float _elapsed;
    float _duration;
    Action _onComplete;
    bool _running;

    public bool IsBusy => _running;

    public float Progress01 =>
        _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);

    public bool IsBound => _animator != null && _workLayerIndex >= 0;

    public void Bind(Animator animator, int workLayerIndex)
    {
        _animator = animator;
        _workLayerIndex = workLayerIndex;
    }

    public void Tick(float deltaTime)
    {
        if (!_running || _duration <= 0f)
            return;

        _elapsed += deltaTime;
        if (_elapsed < _duration)
            return;

        Finish();
    }

    public bool TryBegin(AnimationClip clip, float configuredDurationSeconds, Action onComplete)
    {
        if (onComplete == null || _running)
            return false;

        float clipLen = clip != null ? clip.length : 0f;
        float duration = Mathf.Max(clipLen, configuredDurationSeconds);

        if (clip != null && _animator != null)
            CharacterWorkLayerAnim.TryPlay(_animator, ref _workLayerIndex, clip);

        _onComplete = onComplete;
        _elapsed = 0f;
        _duration = Mathf.Max(0f, duration);
        _running = true;

        if (_duration <= 0f)
            Finish();

        return true;
    }

    public void Cancel()
    {
        if (!_running)
            return;

        CharacterWorkLayerAnim.Stop(_animator, _workLayerIndex);
        _running = false;
        _onComplete = null;
        _elapsed = 0f;
        _duration = 0f;
    }

    void Finish()
    {
        Action complete = _onComplete;
        _running = false;
        _onComplete = null;
        _elapsed = 0f;
        _duration = 0f;
        CharacterWorkLayerAnim.Stop(_animator, _workLayerIndex);
        complete?.Invoke();
    }
}
