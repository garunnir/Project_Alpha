// ============================================================
// CharacterHitReact — 피격 밀침·Flinch·Stagger·PainDown·사망 Dead 애니 큐 (ApplyHit 미구독)
// ============================================================

using Animancer;
using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

public sealed class CharacterHitReact
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

    CharacterBodyRefs _refs;
    CharacterBodyHost _bodyHost;
    CharacterMotor _motor;
    CharacterActionHost _actionHost;
    CharacterAttacker _attacker;
    CharacterAppearanceHost _appearance;
    PlayerGearHost _gear;
    CharacterPainHost _pain;
    CharacterSkillsHost _skillsHost;
    ICharacterDefeat _defeat;
    ICharacterBody _subscribedBody;
    CharacterImbalanceHost _imbalance;
    Animator _animator;
    HybridAnimancerComponent _hybrid;
    CharacterLocomotionAnim _loco;
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
    bool _eventsBound;

    public CharacterBodyRefs BodyRefs => _refs;

    public void Bind(CharacterBodyRefs refs)
    {
        _refs = refs;
        if (refs != null)
        {
            _bodyHost = refs.BodyHost;
            _motor = refs.Motor;
            _actionHost = refs.ActionHost;
            _attacker = refs.Attacker;
            _appearance = refs.Appearance;
            _gear = refs.GearHost;
            _pain = refs.PainHost;
            _skillsHost = refs.SkillsHost;
            _imbalance = refs.ImbalanceHost;
            _animator = CharacterBodyResolve.GetInBody<Animator>(refs);
            _hybrid = CharacterBodyResolve.GetInBody<HybridAnimancerComponent>(refs);
            _loco = refs.LocomotionAnim;
            if (_hybrid != null && _hybrid.Animator == null && _animator != null)
                _hybrid.Animator = _animator;
        }
        else
        {
            _animator = null;
            _hybrid = null;
            _loco = null;
        }

        if (_animator == null && refs != null)
        {
            Debug.LogError(
                $"[CharacterHitReact] '{refs.name}' needs an Animator under the body root.",
                refs);
        }

        CacheHurtParams();
        if (_eventsBound)
            Enable();
    }

    public void Enable()
    {
        if (_attacker == null && _refs != null)
            _attacker = _refs.Attacker;
        if (_pain == null && _refs != null)
            _pain = _refs.PainHost;
        if (_imbalance == null && _refs != null)
            _imbalance = _refs.ImbalanceHost;
        if (_loco == null && _refs != null)
            _loco = _refs.LocomotionAnim;

        if (_eventsBound)
        {
            BindDefeat();
            BindBodySignals();
            SyncHurtBools();
            return;
        }

        _eventsBound = true;
        CharacterAttacker.AnyAttackJudged += OnAnyAttackJudged;
        if (_pain != null)
            _pain.Changed += OnPainChanged;
        BindDefeat();
        BindBodySignals();
        SyncHurtBools();
    }

    public void Disable()
    {
        if (!_eventsBound)
            return;

        _eventsBound = false;
        CharacterAttacker.AnyAttackJudged -= OnAnyAttackJudged;
        if (_pain != null)
            _pain.Changed -= OnPainChanged;
        UnbindDefeat();
        UnbindBodySignals();
    }

    /// <summary><see cref="CharacterSkillsHost.BindSkills"/> 후 Defeat 재구독·Hurt bool 동기화.</summary>
    public void NotifyDefeatHostRebuilt()
    {
        if (!_eventsBound)
            return;

        BindDefeat();
        SyncHurtBools();
    }

    /// <summary>무기 Override·컨트롤러 교체 후 Hurt 파라미터 캐시·bool 재동기화.</summary>
    public void RefreshAnimatorHurtBinding()
    {
        if (_refs != null)
        {
            if (_hybrid == null)
            {
                _hybrid = CharacterBodyResolve.GetInBody<HybridAnimancerComponent>(_refs);
                if (_hybrid != null && _hybrid.Animator == null && _animator != null)
                    _hybrid.Animator = _animator;
            }

            _loco = _refs.LocomotionAnim;
        }

        CacheHurtParams();
        if (_eventsBound)
            SyncHurtBools();
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

        // S7+: Hybrid Controller often cleared — Mecanim Hurt layers/params unavailable.
        // Animancer path uses CLA TryPlay*/TrySync* (indices/_has* unused).
        if (UsesAnimancerHurt || (_hybrid != null && !HasHybridControllerPlayable()))
            return;

        _hurtLayerIndex = AnimGetLayerIndex(HurtLayerName);
        _flinchLayerIndex = AnimGetLayerIndex(FlinchLayerName);

        if (!TryGetHurtParameters(out AnimatorControllerParameter[] parameters))
            return;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter p = parameters[i];
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

    bool HasHybridControllerPlayable() =>
        _hybrid != null
        && _hybrid.Controller.IsValid
        && _hybrid.Controller.State != null;

    bool TryGetHurtParameters(out AnimatorControllerParameter[] parameters)
    {
        parameters = null;
        if (_hybrid != null)
        {
            EnsureHybridReady();
            if (_hybrid.Controller.IsValid && _hybrid.Controller.State != null)
            {
                parameters = _hybrid.parameters;
                return parameters != null;
            }

            return false;
        }

        if (_animator == null)
            return false;

        parameters = _animator.parameters;
        return parameters != null;
    }

    void OnPainChanged() => SyncPainBool();

    void OnDefeatChanged() => SyncDeadBool();

    void OnBodyChanged() => SyncHurtBools();

    void BindBodySignals()
    {
        UnbindBodySignals();
        _subscribedBody = _bodyHost != null ? _bodyHost.Body : null;
        if (_subscribedBody != null)
            _subscribedBody.Changed += OnBodyChanged;
    }

    void UnbindBodySignals()
    {
        if (_subscribedBody != null)
            _subscribedBody.Changed -= OnBodyChanged;
        _subscribedBody = null;
    }

    void SyncHurtBools()
    {
        SyncPainBool();
        SyncDeadBool();
    }

    void BindDefeat()
    {
        UnbindDefeat();
        _skillsHost = _refs != null ? _refs.SkillsHost : _skillsHost;
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

    bool UsesAnimancerHurt =>
        _loco != null && _loco.OwnsAnimancerHurt;

    void SyncPainBool()
    {
        bool shocked = _pain != null && _pain.IsPainShocked;
        if (_loco != null && _loco.TrySyncHitPainShocked(shocked))
            return;

        if (!_hasPainShocked || _animator == null)
            return;
        AnimSetBool(_hashPainShocked, shocked);
        if (shocked)
            LiftHurtLayer();
    }

    void SyncDeadBool()
    {
        // Defeat SSOT = ICharacterDefeat.IsDefeated (not body.IsDeadState).
        bool dead = _defeat != null && _defeat.IsDefeated;
        if (_loco != null && _loco.TrySyncHitDefeated(dead))
        {
            if (!dead)
                SyncPainBool();
            return;
        }

        if (!_hasDefeated || _animator == null)
            return;
        AnimSetBool(_hashDefeated, dead);
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
        if (_loco != null && _loco.TryPlayHitFlinch())
            return;

        if (!_hasFlinch || _animator == null)
            return;
        AnimResetTrigger(_hashFlinch);
        AnimSetTrigger(_hashFlinch);
        LiftFlinchLayer();
    }

    void PlayStagger()
    {
        if (_loco != null && _loco.TryPlayHitStagger())
            return;

        if (!_hasStagger || _animator == null)
            return;
        AnimResetTrigger(_hashStagger);
        AnimSetTrigger(_hashStagger);
        LiftHurtLayer();
    }

    void LiftFlinchLayer()
    {
        if (UsesAnimancerHurt)
            return;
        if (_flinchLayerIndex < 0 || _animator == null)
            return;
        AnimSetLayerWeight(_flinchLayerIndex, 1f);
    }

    void LiftHurtLayer()
    {
        if (UsesAnimancerHurt)
            return;
        if (_hurtLayerIndex < 0 || _animator == null)
            return;
        AnimSetLayerWeight(_hurtLayerIndex, 1f);
    }

    void EnsureHybridReady()
    {
        if (_hybrid == null)
            return;

        if (_hybrid.Animator == null && _animator != null)
            _hybrid.Animator = _animator;

        // CLA owns Animancer layer layout when S5 Hurt path is active — do not PlayController.
        if (_loco != null && _loco.OwnsAnimancerHurt)
        {
            if (_hybrid.IsGraphInitialized)
                _hybrid.Graph.PauseGraph();
            return;
        }

        _hybrid.PlayController();
        if (_hybrid.IsGraphInitialized)
            _hybrid.Graph.PauseGraph();
    }

    void AnimSetBool(int id, bool value)
    {
        if (_hybrid != null)
        {
            EnsureHybridReady();
            if (!HasHybridControllerPlayable())
                return;
            _hybrid.SetBool(id, value);
            return;
        }

        _animator.SetBool(id, value);
    }

    void AnimSetTrigger(int id)
    {
        if (_hybrid != null)
        {
            EnsureHybridReady();
            if (!HasHybridControllerPlayable())
                return;
            _hybrid.SetTrigger(id);
            return;
        }

        _animator.SetTrigger(id);
    }

    void AnimResetTrigger(int id)
    {
        if (_hybrid != null)
        {
            EnsureHybridReady();
            if (!HasHybridControllerPlayable())
                return;
            _hybrid.ResetTrigger(id);
            return;
        }

        _animator.ResetTrigger(id);
    }

    void AnimSetLayerWeight(int layerIndex, float weight)
    {
        if (_hybrid != null)
        {
            EnsureHybridReady();
            if (!HasHybridControllerPlayable())
                return;
            _hybrid.SetLayerWeight(layerIndex, weight);
            return;
        }

        _animator.SetLayerWeight(layerIndex, weight);
    }

    int AnimGetLayerIndex(string layerName)
    {
        if (string.IsNullOrEmpty(layerName))
            return -1;

        if (_hybrid != null)
        {
            EnsureHybridReady();
            if (!HasHybridControllerPlayable())
                return -1;
            return _hybrid.GetLayerIndex(layerName);
        }

        return _animator != null ? _animator.GetLayerIndex(layerName) : -1;
    }
}
