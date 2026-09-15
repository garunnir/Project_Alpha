// ============================================================
// CharacterPresenceHost — 타인 탐지용 가시성·소음 스탯 SSOT (plain module)
// ============================================================

using UnityEngine;

public sealed class CharacterPresenceHost : ICharacterPresence
{
    readonly CharacterPresenceSettings _settings = CharacterPresenceSettings.DefaultUnity;

    CharacterState _state;
    CharacterMotor _motor;
    CharacterPresenceResolved _resolved = CharacterPresenceResolved.Identity;
    bool _enabled;

    public float Visibility01 => _resolved.Visibility01;
    public float Noise01 => _resolved.Noise01;
    public CharacterPresenceResolved Resolved => _resolved;

    public void Bind(CharacterBodyRefs refs)
    {
        if (_state != null)
            _state.StealthChanged -= OnStealthChanged;

        _state = refs != null ? refs.State : null;
        _motor = refs != null ? refs.Motor : null;
        if (_enabled)
            Enable();
        else
            Refresh();
    }

    public void Enable()
    {
        _enabled = true;
        if (_state != null)
            _state.StealthChanged += OnStealthChanged;
        Refresh();
    }

    public void Disable()
    {
        _enabled = false;
        if (_state != null)
            _state.StealthChanged -= OnStealthChanged;
    }

    public void Tick() => Refresh();

    void OnStealthChanged(bool _) => Refresh();

    void Refresh()
    {
        var ctx = new CharacterPresenceContext
        {
            IsStealthActive = _state != null && _state.IsStealth,
            CurrentSpeed = _motor != null ? _motor.CurrentSpeed : 0f,
            IsSprinting = _motor != null && _motor.IsSprinting,
            NoiseReferenceSpeed = _settings.NoiseReferenceSpeed,
            BodyScale01 = 1f,
            Transparency01 = 1f,
        };
        _resolved = CharacterPresenceResolved.Evaluate(in ctx, in _settings);
    }

    public static bool TryResolve(Component target, out CharacterPresenceResolved resolved)
    {
        CharacterPresenceHost host = CharacterBodyResolve.GetModule<CharacterPresenceHost>(target);
        if (host != null)
        {
            resolved = host.Resolved;
            return true;
        }

        resolved = CharacterPresenceResolved.Identity;
        return false;
    }

    public static float ResolveVisibility01(Component target) =>
        TryResolve(target, out CharacterPresenceResolved resolved)
            ? resolved.Visibility01
            : 1f;

    public static float ResolveNoise01(Component target) =>
        TryResolve(target, out CharacterPresenceResolved resolved)
            ? resolved.Noise01
            : 1f;
}
