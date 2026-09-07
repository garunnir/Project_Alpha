// ============================================================
// CharacterHitStopState — 이 캐릭터만 애니·이동·공격을 잠시 멈춤 (plain, BodyHost 소유)
// ============================================================

using UnityEngine;

public sealed class CharacterHitStopState
{
    public const string DefaultSettingsPath = CombatHitStopSettings.DefaultAssetPath;

    readonly CharacterBodyHost _bodyHost;
    CombatHitStopSettings _settings;
    CharacterAttacker _attacker;
    float _remaining;
    bool _bound;

    public CharacterHitStopState(CharacterBodyHost bodyHost, CombatHitStopSettings settings)
    {
        _bodyHost = bodyHost;
        _settings = settings;
    }

    public bool IsFrozen => _remaining > 0f;

    /// <summary>시뮬 배율. 경직 중 0, 아니면 1. Update 할당 없음.</summary>
    public float SimScale => _remaining > 0f ? 0f : 1f;

    public void SetSettings(CombatHitStopSettings settings) => _settings = settings;

    public static CharacterHitStopState Find(Component origin)
    {
        CharacterBodyHost host = CharacterBodyResolve.GetInBody<CharacterBodyHost>(origin);
        return host != null ? host.HitStop : null;
    }

    public void Bind()
    {
        if (_bound)
            return;

        if (_attacker == null && _bodyHost != null)
            _attacker = CharacterBodyResolve.GetInBody<CharacterAttacker>(_bodyHost);

        if (_attacker != null)
            _attacker.AttackJudged += OnAttackerJudged;
        CharacterAttacker.AnyAttackJudged += OnAnyAttackJudged;
        _bound = true;
    }

    public void Unbind()
    {
        if (!_bound)
            return;

        if (_attacker != null)
            _attacker.AttackJudged -= OnAttackerJudged;
        CharacterAttacker.AnyAttackJudged -= OnAnyAttackJudged;
        _remaining = 0f;
        _bound = false;
    }

    public void Tick(float realtimeDelta)
    {
        if (_remaining <= 0f)
            return;
        _remaining -= realtimeDelta;
        if (_remaining < 0f)
            _remaining = 0f;
    }

    public void Apply(float seconds)
    {
        if (seconds <= 0f)
            return;
        if (seconds > _remaining)
            _remaining = seconds;
    }

    void OnAttackerJudged(AttackOutcome outcome) => ApplyResolved(outcome);

    void OnAnyAttackJudged(AttackOutcome outcome)
    {
        if (outcome.Target != _bodyHost)
            return;
        ApplyResolved(outcome);
    }

    void ApplyResolved(in AttackOutcome outcome)
    {
        if (_settings == null)
            return;
        Apply(_settings.ResolveDuration(outcome));
    }
}
