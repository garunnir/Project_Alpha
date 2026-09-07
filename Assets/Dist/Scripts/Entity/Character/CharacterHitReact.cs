// ============================================================
// CharacterHitReact — 피격 밀침·Flinch·Stagger·PainDown·사망 Dead 애니 큐 (ApplyHit 미구독)
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterBodyHost))]
[RequireComponent(typeof(CharacterImbalanceHost))]
public sealed class CharacterHitReact : MonoBehaviour
{
    public const string HurtLayerName = "Hurt Layer";
    public const string FlinchLayerName = "Flinch Layer";
    public const string ParamFlinch = "HitFlinch";
    public const string ParamStagger = "HitStagger";
    public const string ParamPainShocked = "IsPainShocked";
    public const string ParamDefeated = "IsDefeated";
    public const string StateEmpty = "Empty";
    public const string StateFlinch = "Flinch";
    public const string StateStagger = "Stagger";
    public const string StatePainDown = "PainDown";
    public const string StateDead = "Dead";
    public const string ClipFlinch = "HitFlinch_Slot";
    public const string ClipStagger = "HitStagger_Slot";
    public const string ClipPainDown = "HitPainDown_Slot";
    public const string ClipDead = "HitDead_Slot";

    CharacterBodyHost _bodyHost;
    CharacterMotor _motor;
    CharacterActionHost _actionHost;
    CharacterAttacker _attacker;
    CharacterAppearanceHost _appearance;
    PlayerGearHost _gear;
    CharacterPainHost _pain;
    CharacterSkillsHost _skillsHost;
    ICharacterDefeat _defeat;
    CharacterImbalanceHost _imbalance;
    Animator _animator;
    int _hashFlinch;
    int _hashStagger;
    int _hashPainShocked;
    int _hashDefeated;
    int _hurtLayerIndex = -1;
    int _flinchLayerIndex = -1;
    bool _hasFlinch;
    bool _hasStagger;
    bool _hasPainShocked;
    bool _hasDefeated;

    void Awake()
    {
        _bodyHost = GetComponent<CharacterBodyHost>();
        _motor = CharacterBodyResolve.GetInBody<CharacterMotor>(this);
        TryGetComponent(out _actionHost);
        TryGetComponent(out _attacker);
        _appearance = CharacterBodyResolve.GetInBody<CharacterAppearanceHost>(this);
        TryGetComponent(out _gear);
        TryGetComponent(out _pain);
        TryGetComponent(out _skillsHost);
        TryGetComponent(out _imbalance);
        _animator = CharacterBodyResolve.GetInBody<Animator>(this);
        if (_animator == null)
        {
            Debug.LogError(
                $"[CharacterHitReact] '{name}' needs an Animator under the body root.",
                this);
        }

        CacheHurtParams();
    }

    void OnEnable()
    {
        CharacterAttacker.AnyAttackJudged += OnAnyAttackJudged;
        if (_pain != null)
            _pain.Changed += OnPainChanged;
        BindDefeat();
        SyncPainBool();
        SyncDeadBool();
    }

    void OnDisable()
    {
        CharacterAttacker.AnyAttackJudged -= OnAnyAttackJudged;
        if (_pain != null)
            _pain.Changed -= OnPainChanged;
        UnbindDefeat();
    }

    void CacheHurtParams()
    {
        _hasFlinch = false;
        _hasStagger = false;
        _hasPainShocked = false;
        _hasDefeated = false;
        _hurtLayerIndex = -1;
        _flinchLayerIndex = -1;
        if (_animator == null)
            return;

        _hashFlinch = Animator.StringToHash(ParamFlinch);
        _hashStagger = Animator.StringToHash(ParamStagger);
        _hashPainShocked = Animator.StringToHash(ParamPainShocked);
        _hashDefeated = Animator.StringToHash(ParamDefeated);
        _hurtLayerIndex = _animator.GetLayerIndex(HurtLayerName);
        _flinchLayerIndex = _animator.GetLayerIndex(FlinchLayerName);

        for (int i = 0; i < _animator.parameterCount; i++)
        {
            AnimatorControllerParameter p = _animator.parameters[i];
            if (p.nameHash == _hashFlinch && p.type == AnimatorControllerParameterType.Trigger)
                _hasFlinch = true;
            else if (p.nameHash == _hashStagger && p.type == AnimatorControllerParameterType.Trigger)
                _hasStagger = true;
            else if (p.nameHash == _hashPainShocked && p.type == AnimatorControllerParameterType.Bool)
                _hasPainShocked = true;
            else if (p.nameHash == _hashDefeated && p.type == AnimatorControllerParameterType.Bool)
                _hasDefeated = true;
        }
    }

    void OnPainChanged() => SyncPainBool();

    void OnDefeatChanged() => SyncDeadBool();

    void BindDefeat()
    {
        UnbindDefeat();
        _defeat = _skillsHost != null ? _skillsHost.Defeat : null;
        if (_defeat != null)
            _defeat.Changed += OnDefeatChanged;
    }

    void UnbindDefeat()
    {
        if (_defeat != null)
            _defeat.Changed -= OnDefeatChanged;
        _defeat = null;
    }

    bool IsHurtLocked =>
        (_pain != null && _pain.IsPainShocked) ||
        (_defeat != null && _defeat.IsDefeated);

    void SyncPainBool()
    {
        if (!_hasPainShocked || _animator == null)
            return;
        bool shocked = _pain != null && _pain.IsPainShocked;
        _animator.SetBool(_hashPainShocked, shocked);
        if (shocked)
            LiftHurtLayer();
    }

    void SyncDeadBool()
    {
        if (!_hasDefeated || _animator == null)
            return;
        bool dead = _defeat != null && _defeat.IsDefeated;
        _animator.SetBool(_hashDefeated, dead);
        if (dead)
            LiftHurtLayer();
    }

    void OnAnyAttackJudged(AttackOutcome outcome)
    {
        if (!outcome.DidHit || outcome.Target != _bodyHost)
            return;

        ICharacterBody body = _bodyHost != null ? _bodyHost.Body : null;
        if (body == null || body.IsDeadState)
            return;

        bool locked = IsHurtLocked;
        if (!locked)
            PlayFlinch();

        float mass = CombatImpulse.InertialMassKg(
            _appearance,
            _gear != null ? _gear.Wear : null,
            _gear != null ? _gear.Wield : null);
        float dv = CombatImpulse.VictimDeltaV(outcome.ImpulseJin, mass);
        if (dv > 0.001f && _motor != null)
        {
            Vector3 dir = outcome.Direction;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-6f)
                _motor.ApplyKnockback(dir.normalized * dv);
        }

        bool fell = false;
        if (_imbalance != null)
            fell = _imbalance.ApplyHit(dv);
        else if (dv >= CombatImpulse.StaggerDeltaV)
            fell = true;

        if (fell)
        {
            if (_imbalance != null)
                _imbalance.NotifyFallen();
            else
            {
                _actionHost?.CancelAll();
                _attacker?.CancelAllPendingCues();
            }

            if (!locked)
                PlayStagger();
        }

        _pain?.Refresh();
    }

    void PlayFlinch()
    {
        if (!_hasFlinch || _animator == null)
            return;
        _animator.ResetTrigger(_hashFlinch);
        _animator.SetTrigger(_hashFlinch);
        LiftFlinchLayer();
    }

    void PlayStagger()
    {
        if (!_hasStagger || _animator == null)
            return;
        _animator.ResetTrigger(_hashStagger);
        _animator.SetTrigger(_hashStagger);
        LiftHurtLayer();
    }

    void LiftFlinchLayer()
    {
        if (_flinchLayerIndex < 0 || _animator == null)
            return;
        _animator.SetLayerWeight(_flinchLayerIndex, 1f);
    }

    void LiftHurtLayer()
    {
        if (_hurtLayerIndex < 0 || _animator == null)
            return;
        _animator.SetLayerWeight(_hurtLayerIndex, 1f);
    }
}
