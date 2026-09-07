// ============================================================
// CharacterLocomotionAnim — MoveXZ/Speed + L/R/2H overlays + Impact + Hurt weight + thin remap
// ============================================================
using Garunnir.Runtime.Gameplay.Data;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Drives Move (facing-relative MoveX/MoveZ) + RightArm/LeftArm/TwoHand overlays + Impact + Hurt.
/// TwoHand Attack stays UpperBody-masked (full-body replace looked unnatural on Idle).
/// WeaponAction selects Entry clips then Catalog Leaf, projected onto thin keys via
/// <see cref="ArmAnimSlotResolver"/>; Impact Kind via <see cref="ArmImpactSlotResolver"/>.
/// Hurt clips are controller thin only (no Catalog remap).
/// Animation time advances via <see cref="TimeScaleService"/> only.
/// </summary>
[RequireComponent(typeof(CharacterState))]
public class CharacterLocomotionAnim : MonoBehaviour
{
    const string ManualTickHelp =
        "Play 시 Animator 컴포넌트를 끕니다(TimeScale 채널 수동 틱). " +
        "Inspector에서 Animator.enabled가 꺼져 보이는 것은 정상입니다. " +
        "재생은 이 스크립트의 Update → Animator.Update(TimeScaleService.Delta)로만 진행됩니다.";

    const string DefaultRightArmLayer = "RightArm Layer";
    const string DefaultLeftArmLayer = "LeftArm Layer";
    const string DefaultTwoHandLayer = "TwoHand Layer";
    const float DefaultPoseRate = 10f;
    const float DefaultLayerBlendSpeed = 10f;
    const int MaxPoseStepsPerFrame = 8;
    const float MoveDirEpsilonSqr = 1e-6f;
    const string AttackOverlayStateName = "Attack";
    const string HoldOverlayStateName = "Hold";
    const string AimOverlayStateName = "Aim";
    const string ImpactLayerName = "Impact Layer";
    const string ParamImpactRecoil = "ImpactRecoil";
    const string ParamImpactBlocked = "ImpactBlocked";
    const string ImpactRecoilStateName = "Recoil";
    const string ImpactBlockedStateName = "Blocked";
    const string ImpactEmptyStateName = "Empty";

    [InfoBox(ManualTickHelp, InfoMessageType.Warning)]
    [SerializeField] TimeScaleChannel _timeChannel = TimeScaleChannel.Player;

    [Header("Animator (TimeScale manual tick)")]
    [Tooltip(ManualTickHelp)]
    [SerializeField] Animator _animator;
    [SerializeField] RuntimeAnimatorController _defaultController;
    [SerializeField] ArmAnimSlotCatalog _armSlotCatalog;
    [SerializeField] string _paramSpeed = "Speed";
    [SerializeField] string _paramMoveX = "MoveX";
    [SerializeField] string _paramMoveZ = "MoveZ";
    [SerializeField] string _paramAiming = "IsAiming";
    [SerializeField] string _paramStealth = "IsStealth";
    [SerializeField] string _paramSwimming = "IsSwimming";
    [SerializeField] string _paramAttackR = "AttackR";
    [SerializeField] string _paramAttackL = "AttackL";
    [SerializeField] string _paramAttack2H = "Attack2H";
    [SerializeField] string _rightArmLayerName = DefaultRightArmLayer;
    [SerializeField] string _leftArmLayerName = DefaultLeftArmLayer;
    [SerializeField] string _twoHandLayerName = DefaultTwoHandLayer;
    [SerializeField, Min(0f)] float _layerBlendSpeed = DefaultLayerBlendSpeed;
    [SerializeField, Min(0f)] float _poseRate = DefaultPoseRate;

    [ShowInInspector, ReadOnly, PropertyOrder(20)]
    [LabelText("Manual tick active (Animator.enabled forced off)")]
    bool ManualTickActive => _manualControl && _animator != null && !_animator.enabled;

    CharacterState _characterState;
    CharacterAttacker _attacker;
    PlayerGearHost _gearHost;
    CharacterSkillsHost _skillsHost;
    CharacterHitStopState _hitStop;
    CharacterVaultHost _vaultHost;
    ICharacterLocomotion _locomotion;
    bool _manualControl;
    bool _pendingBind = true;
    float _poseAccum;
    RuntimeAnimatorController _weaponSourceController;
    AnimatorOverrideController _resolvedOverride;

    int _hashSpeed;
    int _hashMoveX;
    int _hashMoveZ;
    int _hashAiming;
    int _hashStealth;
    int _hashSwimming;
    int _hashAttackR;
    int _hashAttackL;
    int _hashAttack2H;
    int _rightArmLayerIndex = -1;
    int _leftArmLayerIndex = -1;
    int _twoHandLayerIndex = -1;
    int _moveLayerIndex = -1;
    int _impactLayerIndex = -1;
    int _flinchLayerIndex = -1;
    int _hurtLayerIndex = -1;
    bool _hasSpeed;
    bool _hasMoveX;
    bool _hasMoveZ;
    bool _hasAiming;
    bool _hasStealth;
    bool _hasSwimming;
    bool _hasAttackR;
    bool _hasAttackL;
    bool _hasAttack2H;
    bool _hasImpactRecoil;
    bool _hasImpactBlocked;
    bool _hasArmSpeedR;
    bool _hasArmSpeedL;
    bool _hasArmSpeed2H;
    bool _hasImpactSpeed;
    bool _hasHitFlinch;
    bool _hasHitStagger;
    bool _hasPainShocked;
    bool _hasDefeated;
    int _hashAttackState;
    int _hashHoldState;
    int _hashAimState;
    int _hashArmSpeedR;
    int _hashArmSpeedL;
    int _hashArmSpeed2H;
    int _hashImpactSpeed;
    int _hashImpactRecoil;
    int _hashImpactBlocked;
    int _hashImpactEmpty;
    int _hashImpactRecoilState;
    int _hashImpactBlockedState;
    int _hashHurtFlinch;
    int _hashHurtStagger;
    int _hashHurtPainShocked;
    int _hashHurtDefeated;
    int _hashHurtFlinchState;
    int _hashHurtStaggerState;
    int _hashHurtPainDownState;
    int _hashHurtDeadState;
    float _impactWeightTarget;
    float _flinchWeightTarget;
    float _hurtWeightTarget;
    float _speedHoldR = WeaponAnimClipSpeeds.DefaultSpeed;
    float _speedAimR = WeaponAnimClipSpeeds.DefaultSpeed;
    float _speedAttackR = WeaponAnimClipSpeeds.DefaultSpeed;
    float _speedHoldL = WeaponAnimClipSpeeds.DefaultSpeed;
    float _speedAimL = WeaponAnimClipSpeeds.DefaultSpeed;
    float _speedAttackL = WeaponAnimClipSpeeds.DefaultSpeed;
    float _speedHold2H = WeaponAnimClipSpeeds.DefaultSpeed;
    float _speedAim2H = WeaponAnimClipSpeeds.DefaultSpeed;
    float _speedAttack2H = WeaponAnimClipSpeeds.DefaultSpeed;
    float _speedImpactRecoil = WeaponAnimClipSpeeds.DefaultSpeed;
    float _speedImpactBlocked = WeaponAnimClipSpeeds.DefaultSpeed;

    public bool HasAttackTrigger => _hasAttackR || _hasAttackL || _hasAttack2H;

    WeaponAction _mappedActionL = (WeaponAction)(-1);
    WeaponAction _mappedActionR = (WeaponAction)(-1);
    WeaponAction _mappedAction2H = (WeaponAction)(-1);
    WeaponPresentation _mappedPresentationL;
    WeaponPresentation _mappedPresentationR;
    WeaponPresentation _mappedPresentation2H;

    readonly WeaponAction[] _attackActionQueue = new WeaponAction[2];
    readonly WieldHand[] _attackHandQueue = new WieldHand[2];
    readonly bool[] _attackSurpriseQueue = new bool[2];
    bool _mappedSurpriseL;
    bool _mappedSurpriseR;
    bool _mappedSurprise2H;
    int _attackQueueHead;
    int _attackQueueCount;

    /// <summary>useHold=false일 때 Attack 큐→재생 동안 overlay 유지. 0=off 1=armed 2=playing.</summary>
    readonly byte[] _attackOverlayLatch = new byte[3];
    const byte AttackLatchOff = 0;
    const byte AttackLatchArmed = 1;
    const byte AttackLatchPlaying = 2;

    public Animator Animator => _animator;
    public int WorkLayerIndex { get; private set; } = -1;
    public ArmAnimSlotCatalog ArmSlotCatalog => _armSlotCatalog;

    public bool TryGetWorkAnim(out Animator animator, out int workLayerIndex)
    {
        animator = _animator;
        workLayerIndex = WorkLayerIndex;
        return _animator != null && WorkLayerIndex >= 0;
    }

    void Awake()
    {
        ResolveBodyRefs();
        _hitStop = CharacterHitStopState.Find(this);

        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();

        if (_defaultController == null && _animator != null)
            _defaultController = ResolveBaseController(_animator.runtimeAnimatorController);

        ApplyWeaponAnimOverride(forceRebind: false);
        CacheAnimatorParameters();
    }

    void ResolveBodyRefs()
    {
        CharacterBodyRefs refs = this.GetBodyRefs();
        if (refs != null)
        {
            _characterState = refs.State;
            _attacker = refs.Attacker;
            _gearHost = refs.GearHost;
            _skillsHost = refs.SkillsHost;
            _locomotion = refs.Motor;
            _vaultHost = refs.VaultHost;
            return;
        }

        _characterState = CharacterBodyResolve.GetInBody<CharacterState>(this);
        _attacker = CharacterBodyResolve.GetInBody<CharacterAttacker>(this);
        _gearHost = CharacterBodyResolve.GetInBody<PlayerGearHost>(this);
        _skillsHost = CharacterBodyResolve.GetInBody<CharacterSkillsHost>(this);
        _locomotion = CharacterBodyResolve.GetInBody<CharacterMotor>(this);
        _vaultHost = CharacterBodyResolve.GetInBody<CharacterVaultHost>(this);
    }

    void OnEnable()
    {
        if (_attacker != null)
        {
            _attacker.PresentationChanged += OnPresentationChanged;
            _attacker.AttackResolved += OnAttackResolved;
            _attacker.AttackJudged += OnAttackJudged;
            _attacker.AttackCueFired += OnAttackCueFired;
        }
    }

    void OnDisable()
    {
        if (_attacker != null)
        {
            _attacker.PresentationChanged -= OnPresentationChanged;
            _attacker.AttackResolved -= OnAttackResolved;
            _attacker.AttackJudged -= OnAttackJudged;
            _attacker.AttackCueFired -= OnAttackCueFired;
        }
    }

    void Reset()
    {
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();
    }

    void OnValidate()
    {
        if (_poseRate < 0f)
            _poseRate = 0f;
        if (_layerBlendSpeed < 0f)
            _layerBlendSpeed = 0f;
#if UNITY_EDITOR
        if (_armSlotCatalog == null)
        {
            _armSlotCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<ArmAnimSlotCatalog>(
                "Assets/Dist/SOData/Combat/Fallbacks/ArmAnimSlotCatalog.asset");
        }
#endif
        if (_animator != null)
            CacheAnimatorParameters();
    }

    void Update()
    {
        if (_animator == null)
            return;

        bool rebound = false;
        if (_pendingBind || !_manualControl)
        {
            TakeManualControl();
            _pendingBind = false;
            rebound = true;
        }

        if (_hitStop != null && _hitStop.IsFrozen)
            return;

        float speedNorm = ResolveNormalizedSpeed();
        if (_hasSpeed)
            _animator.SetFloat(_hashSpeed, speedNorm);

        ResolveFacingMoveXZ(speedNorm, out float moveX, out float moveZ);
        if (_hasMoveX)
            _animator.SetFloat(_hashMoveX, moveX);
        if (_hasMoveZ)
            _animator.SetFloat(_hashMoveZ, moveZ);

        bool isAiming = _characterState != null && _characterState.IsAiming;
        if (_hasAiming)
            _animator.SetBool(_hashAiming, isAiming);

        bool isStealth = _characterState != null && _characterState.IsStealth;
        if (_hasStealth)
            _animator.SetBool(_hashStealth, isStealth);

        bool isSwimming = _characterState != null
            && (_characterState.IsSwimming || _characterState.IsDiving);
        if (_hasSwimming)
            _animator.SetBool(_hashSwimming, isSwimming);

        ResolveHandActions(out WeaponAction actionL, out WeaponAction actionR, out WeaponAction action2H);
        ResolveHandPresentations(
            out WeaponPresentation presentationL,
            out WeaponPresentation presentationR,
            out WeaponPresentation presentation2H);
        SyncThinActionRemap(
            presentationL,
            presentationR,
            presentation2H,
            actionL,
            actionR,
            action2H,
            surpriseL: _mappedSurpriseL && HasAttackOverlayLatch(WieldHand.Left),
            surpriseR: _mappedSurpriseR && HasAttackOverlayLatch(WieldHand.Right),
            surprise2H: _mappedSurprise2H && HasAttackOverlayLatch(WieldHand.TwoHand));

        if (_attackQueueCount > 0)
        {
            WeaponAction attackAction = _attackActionQueue[_attackQueueHead];
            WieldHand attackHand = _attackHandQueue[_attackQueueHead];
            bool surpriseAttack = _attackSurpriseQueue[_attackQueueHead];
            _attackQueueHead = (_attackQueueHead + 1) % _attackActionQueue.Length;
            _attackQueueCount--;

            if (attackHand == WieldHand.TwoHand)
            {
                SyncThinActionRemap(
                    presentationL,
                    presentationR,
                    presentation2H,
                    actionL,
                    actionR,
                    attackAction,
                    surpriseL: false,
                    surpriseR: false,
                    surprise2H: surpriseAttack);
                ArmAttackOverlay(attackHand);
                if (_hasAttack2H)
                    _animator.SetTrigger(_hashAttack2H);
            }
            else if (attackHand == WieldHand.Left)
            {
                SyncThinActionRemap(
                    presentationL,
                    presentationR,
                    presentation2H,
                    attackAction,
                    actionR,
                    action2H,
                    surpriseL: surpriseAttack,
                    surpriseR: false,
                    surprise2H: false);
                ArmAttackOverlay(attackHand);
                if (_hasAttackL)
                    _animator.SetTrigger(_hashAttackL);
            }
            else
            {
                SyncThinActionRemap(
                    presentationL,
                    presentationR,
                    presentation2H,
                    actionL,
                    attackAction,
                    action2H,
                    surpriseL: false,
                    surpriseR: surpriseAttack,
                    surprise2H: false);
                ArmAttackOverlay(attackHand);
                if (_hasAttackR)
                    _animator.SetTrigger(_hashAttackR);
            }
        }

        float channelDelta = TimeScaleService.Delta(_timeChannel);
        UpdateFlinchWeightTarget();
        UpdateHurtWeightTarget();
        SyncVaultLayerWeights(rebound ? 0f : channelDelta);
        SyncArmLayerWeights(rebound ? 0f : channelDelta);
        SyncImpactLayerWeight(rebound ? 0f : channelDelta);
        SyncFlinchLayerWeight(rebound ? 0f : channelDelta);
        SyncHurtLayerWeight(rebound ? 0f : channelDelta);
        ApplyClipSpeedParams();
        AdvanceAnimator(channelDelta);
        TickAttackOverlayLatches();
        TickAttackCues();
        TickImpactEmpty();
    }

    void SyncThinActionRemap(
        WeaponPresentation presentationL,
        WeaponPresentation presentationR,
        WeaponPresentation presentation2H,
        WeaponAction actionL,
        WeaponAction actionR,
        WeaponAction action2H,
        bool surpriseL = false,
        bool surpriseR = false,
        bool surprise2H = false)
    {
        if (_resolvedOverride == null || _armSlotCatalog == null)
            return;

        if (actionL == _mappedActionL &&
            actionR == _mappedActionR &&
            action2H == _mappedAction2H &&
            surpriseL == _mappedSurpriseL &&
            surpriseR == _mappedSurpriseR &&
            surprise2H == _mappedSurprise2H &&
            ReferenceEquals(presentationL, _mappedPresentationL) &&
            ReferenceEquals(presentationR, _mappedPresentationR) &&
            ReferenceEquals(presentation2H, _mappedPresentation2H))
            return;

        ArmAnimSlotResolver.RemapThinKeys(
            _resolvedOverride,
            _armSlotCatalog,
            presentationL,
            presentationR,
            presentation2H,
            actionL,
            actionR,
            action2H,
            surpriseL,
            surpriseR,
            surprise2H);
        _mappedActionL = actionL;
        _mappedActionR = actionR;
        _mappedAction2H = action2H;
        _mappedSurpriseL = surpriseL;
        _mappedSurpriseR = surpriseR;
        _mappedSurprise2H = surprise2H;
        _mappedPresentationL = presentationL;
        _mappedPresentationR = presentationR;
        _mappedPresentation2H = presentation2H;
        RefreshActionClipSpeeds();
    }

    void OnAttackResolved(AttackOutcome outcome)
    {
        if (outcome.Result != AttackPerformResult.Performed)
            return;
        if (WeaponActionUtil.SuppressesAttackTrigger(outcome.Action))
            return;
        if (_attackQueueCount >= _attackActionQueue.Length)
            return;

        int index = (_attackQueueHead + _attackQueueCount) % _attackActionQueue.Length;
        _attackActionQueue[index] = outcome.Action;
        _attackHandQueue[index] = outcome.Hand;
        _attackSurpriseQueue[index] = outcome.UseSurpriseAttackClip;
        _attackQueueCount++;
    }

    void OnAttackCueFired(WieldHand hand, WeaponAction action)
    {
        if (_attacker != null &&
            !_attacker.AllowsImpactReaction(action, ArmImpactKind.Recoil))
            return;
        PlayImpact(ArmImpactKind.Recoil, hand, action);
    }

    void OnAttackJudged(AttackOutcome outcome)
    {
        if (outcome.Result != AttackPerformResult.Obstructed)
            return;
        if (!WeaponAttack.AllowsImpactReaction(outcome.Attack, ArmImpactKind.Blocked))
            return;
        PlayImpact(ArmImpactKind.Blocked, outcome.Hand, outcome.Action);
    }

    void PlayImpact(ArmImpactKind kind, WieldHand hand, WeaponAction action)
    {
        if (_animator == null || _armSlotCatalog == null || _resolvedOverride == null)
            return;
        if (_impactLayerIndex < 0)
            return;

        CharacterGearService gear = _gearHost != null ? _gearHost.Service : null;
        WeaponPresentationCatalog presentationCatalog = gear?.PresentationCatalog
            ?? (_attacker != null ? _attacker.Catalog : null);
        WeaponPresentation presentation = PresentationForHand(gear, presentationCatalog, hand);
        ArmImpactSlotResolver.ProjectImpact(
            _resolvedOverride,
            _armSlotCatalog,
            presentation,
            action,
            kind,
            hand);
        RefreshImpactClipSpeeds();
        _impactWeightTarget = 1f;
        _animator.SetLayerWeight(_impactLayerIndex, 1f);

        if (kind == ArmImpactKind.Blocked)
        {
            if (_hasImpactBlocked)
                _animator.SetTrigger(_hashImpactBlocked);
        }
        else if (_hasImpactRecoil)
        {
            _animator.SetTrigger(_hashImpactRecoil);
        }
    }

    void SyncImpactLayerWeight(float channelDelta)
    {
        SetLayerWeightToward(_impactLayerIndex, _impactWeightTarget, channelDelta);
    }

    void SyncFlinchLayerWeight(float channelDelta)
    {
        SetLayerWeightToward(_flinchLayerIndex, _flinchWeightTarget, channelDelta);
    }

    void SyncHurtLayerWeight(float channelDelta)
    {
        SetLayerWeightToward(_hurtLayerIndex, _hurtWeightTarget, channelDelta);
    }

    void UpdateFlinchWeightTarget()
    {
        if (_flinchLayerIndex < 0 || _animator == null)
            return;

        bool flinch = false;
        AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(_flinchLayerIndex);
        flinch = current.shortNameHash == _hashHurtFlinchState;
        if (!flinch && _animator.IsInTransition(_flinchLayerIndex))
        {
            AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(_flinchLayerIndex);
            flinch = next.shortNameHash == _hashHurtFlinchState;
        }

        if (!flinch && _hasHitFlinch && _animator.GetBool(_hashHurtFlinch))
            flinch = true;

        _flinchWeightTarget = flinch ? 1f : 0f;
    }

    void UpdateHurtWeightTarget()
    {
        // 할당 없음. 상태 해시 + 대기 트리거만.
        if (_hurtLayerIndex < 0 || _animator == null)
            return;

        bool hurt = false;
        if (_hasDefeated && _animator.GetBool(_hashHurtDefeated))
            hurt = true;
        else if (_hasPainShocked && _animator.GetBool(_hashHurtPainShocked))
            hurt = true;
        else
        {
            AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(_hurtLayerIndex);
            hurt = IsHurtPlayingState(current.shortNameHash);
            if (!hurt && _animator.IsInTransition(_hurtLayerIndex))
            {
                AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(_hurtLayerIndex);
                hurt = IsHurtPlayingState(next.shortNameHash);
            }

            if (!hurt && _hasHitStagger && _animator.GetBool(_hashHurtStagger))
                hurt = true;
        }

        _hurtWeightTarget = hurt ? 1f : 0f;
    }

    bool IsHurtPlayingState(int shortNameHash) =>
        shortNameHash == _hashHurtStaggerState ||
        shortNameHash == _hashHurtPainDownState ||
        shortNameHash == _hashHurtDeadState;

    void TickImpactEmpty()
    {
        if (_impactLayerIndex < 0 || _animator == null || _impactWeightTarget <= 0f)
            return;

        AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(_impactLayerIndex);
        bool inImpact =
            current.shortNameHash == _hashImpactRecoilState ||
            current.shortNameHash == _hashImpactBlockedState;
        if (_animator.IsInTransition(_impactLayerIndex))
        {
            AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(_impactLayerIndex);
            if (next.shortNameHash == _hashImpactRecoilState ||
                next.shortNameHash == _hashImpactBlockedState)
                inImpact = true;
        }

        if (!inImpact && current.shortNameHash == _hashImpactEmpty)
            _impactWeightTarget = 0f;
    }

    void OnPresentationChanged()
    {
        // Dual hand swap only changes CharacterAttacker.Presentation.
        // Full rebind pops Attack overlay; thin remap from gear slots is enough.
        _mappedActionL = (WeaponAction)(-1);
        _mappedActionR = (WeaponAction)(-1);
        _mappedAction2H = (WeaponAction)(-1);
        _mappedSurpriseL = false;
        _mappedSurpriseR = false;
        _mappedSurprise2H = false;
        _mappedPresentationL = null;
        _mappedPresentationR = null;
        _mappedPresentation2H = null;
    }

    void ApplyWeaponAnimOverride(bool forceRebind)
    {
        if (_animator == null)
            return;

        RuntimeAnimatorController source = _defaultController;
        if (_attacker != null &&
            _attacker.Presentation != null &&
            _attacker.Presentation.AnimatorOverride != null)
        {
            source = _attacker.Presentation.AnimatorOverride;
        }

        if (source == null)
            return;

        if (!forceRebind && ReferenceEquals(source, _weaponSourceController) && _resolvedOverride != null)
            return;

        if (_resolvedOverride != null)
        {
            if (ReferenceEquals(_animator.runtimeAnimatorController, _resolvedOverride))
                _animator.runtimeAnimatorController = null;
            Destroy(_resolvedOverride);
            _resolvedOverride = null;
        }

        _weaponSourceController = source;
        _mappedActionL = (WeaponAction)(-1);
        _mappedActionR = (WeaponAction)(-1);
        _mappedAction2H = (WeaponAction)(-1);
        _mappedSurpriseL = false;
        _mappedSurpriseR = false;
        _mappedSurprise2H = false;
        _mappedPresentationL = null;
        _mappedPresentationR = null;
        _mappedPresentation2H = null;

        ResolveHandActions(out WeaponAction actionL, out WeaponAction actionR, out WeaponAction action2H);
        ResolveHandPresentations(
            out WeaponPresentation presentationL,
            out WeaponPresentation presentationR,
            out WeaponPresentation presentation2H);
        if (_armSlotCatalog != null)
        {
            _resolvedOverride = ArmAnimSlotResolver.BuildResolvedOverride(
                source,
                _armSlotCatalog,
                presentationL,
                presentationR,
                presentation2H,
                actionL,
                actionR,
                action2H);
            _mappedActionL = actionL;
            _mappedActionR = actionR;
            _mappedAction2H = action2H;
            _mappedPresentationL = presentationL;
            _mappedPresentationR = presentationR;
            _mappedPresentation2H = presentation2H;
            RefreshActionClipSpeeds();
            RefreshImpactClipSpeeds();
        }

        RuntimeAnimatorController next = _resolvedOverride != null
            ? (RuntimeAnimatorController)_resolvedOverride
            : source;

        if (!ReferenceEquals(_animator.runtimeAnimatorController, next))
            _animator.runtimeAnimatorController = next;

        CacheAnimatorParameters();
        RefreshWorkLayerIndex();

        if (forceRebind || _manualControl)
        {
            _manualControl = false;
            _pendingBind = true;
        }
    }

    void OnDestroy()
    {
        if (_resolvedOverride == null)
            return;
        if (_animator != null && ReferenceEquals(_animator.runtimeAnimatorController, _resolvedOverride))
            _animator.runtimeAnimatorController = _defaultController;
        Destroy(_resolvedOverride);
        _resolvedOverride = null;
    }

    static RuntimeAnimatorController ResolveBaseController(RuntimeAnimatorController controller)
    {
        if (controller is AnimatorOverrideController overrideController &&
            overrideController.runtimeAnimatorController != null)
        {
            return overrideController.runtimeAnimatorController;
        }

        return controller;
    }

    void RefreshWorkLayerIndex()
    {
        WorkLayerIndex = _animator != null
            ? CharacterWorkLayerAnim.ResolveLayerIndex(_animator)
            : -1;
    }

    void TickAttackCues()
    {
        if (_attacker == null || !_attacker.HasPendingAttackCue || _animator == null)
            return;

        TickAttackCueHand(WieldHand.Right, _rightArmLayerIndex);
        TickAttackCueHand(WieldHand.Left, _leftArmLayerIndex);
        TickAttackCueHand(WieldHand.TwoHand, _twoHandLayerIndex);
    }

    void TickAttackCueHand(WieldHand hand, int layerIndex)
    {
        if (!_attacker.HasPendingFor(hand))
            return;

        if (layerIndex < 0)
        {
            _attacker.NotifyAttackCueForHand(hand);
            return;
        }

        AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(layerIndex);
        bool inAttack = current.shortNameHash == _hashAttackState;
        float normalizedTime = current.normalizedTime;
        if (_animator.IsInTransition(layerIndex))
        {
            AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(layerIndex);
            if (next.shortNameHash == _hashAttackState)
            {
                inAttack = true;
                normalizedTime = next.normalizedTime;
            }
        }

        _attacker.NotifyAttackOverlayTick(hand, inAttack, normalizedTime);
    }

    // Update: SetFloat only. No alloc. Clip table lookup is cached on remap/Impact.
    void ApplyClipSpeedParams()
    {
        if (_animator == null)
            return;
        if (_hasArmSpeedR)
            _animator.SetFloat(
                _hashArmSpeedR,
                SpeedForArmLayer(_rightArmLayerIndex, _speedHoldR, _speedAimR, _speedAttackR));
        if (_hasArmSpeedL)
            _animator.SetFloat(
                _hashArmSpeedL,
                SpeedForArmLayer(_leftArmLayerIndex, _speedHoldL, _speedAimL, _speedAttackL));
        if (_hasArmSpeed2H)
            _animator.SetFloat(
                _hashArmSpeed2H,
                SpeedForArmLayer(_twoHandLayerIndex, _speedHold2H, _speedAim2H, _speedAttack2H));
        if (_hasImpactSpeed)
            _animator.SetFloat(_hashImpactSpeed, SpeedForImpactLayer());
    }

    float SpeedForArmLayer(int layerIndex, float hold, float aim, float attack)
    {
        if (layerIndex < 0)
            return WeaponAnimClipSpeeds.DefaultSpeed;
        AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (_animator.IsInTransition(layerIndex))
        {
            AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(layerIndex);
            int nextHash = next.shortNameHash;
            if (nextHash == _hashAttackState ||
                nextHash == _hashAimState ||
                nextHash == _hashHoldState)
                info = next;
        }

        if (info.shortNameHash == _hashAttackState)
            return attack;
        if (info.shortNameHash == _hashAimState)
            return aim;
        return hold;
    }

    float SpeedForImpactLayer()
    {
        if (_impactLayerIndex < 0)
            return WeaponAnimClipSpeeds.DefaultSpeed;
        AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(_impactLayerIndex);
        if (_animator.IsInTransition(_impactLayerIndex))
        {
            AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(_impactLayerIndex);
            int nextHash = next.shortNameHash;
            if (nextHash == _hashImpactRecoilState || nextHash == _hashImpactBlockedState)
                info = next;
        }

        if (info.shortNameHash == _hashImpactBlockedState)
            return _speedImpactBlocked;
        if (info.shortNameHash == _hashImpactRecoilState)
            return _speedImpactRecoil;
        return WeaponAnimClipSpeeds.DefaultSpeed;
    }

    void RefreshActionClipSpeeds()
    {
        ArmAnimSlotCatalog.HandClips hold = _armSlotCatalog != null ? _armSlotCatalog.HoldThin : null;
        ArmAnimSlotCatalog.HandClips aim = _armSlotCatalog != null ? _armSlotCatalog.AimThin : null;
        ArmAnimSlotCatalog.HandClips attack = _armSlotCatalog != null ? _armSlotCatalog.AttackThin : null;
        ResolveHandPresentations(
            out WeaponPresentation presentationL,
            out WeaponPresentation presentationR,
            out WeaponPresentation presentation2H);
        _speedHoldR = SpeedOfThin(hold != null ? hold.rightBase : null, presentationR);
        _speedAimR = SpeedOfThin(aim != null ? aim.rightBase : null, presentationR);
        _speedAttackR = SpeedOfThin(attack != null ? attack.rightBase : null, presentationR);
        _speedHoldL = SpeedOfThin(hold != null ? hold.leftBase : null, presentationL);
        _speedAimL = SpeedOfThin(aim != null ? aim.leftBase : null, presentationL);
        _speedAttackL = SpeedOfThin(attack != null ? attack.leftBase : null, presentationL);
        _speedHold2H = SpeedOfThin(hold != null ? hold.twoHandBase : null, presentation2H);
        _speedAim2H = SpeedOfThin(aim != null ? aim.twoHandBase : null, presentation2H);
        _speedAttack2H = SpeedOfThin(attack != null ? attack.twoHandBase : null, presentation2H);
    }

    void RefreshImpactClipSpeeds()
    {
        WeaponPresentation presentation = _attacker != null ? _attacker.Presentation : null;
        if (_armSlotCatalog == null)
        {
            _speedImpactRecoil = WeaponAnimClipSpeeds.DefaultSpeed;
            _speedImpactBlocked = WeaponAnimClipSpeeds.DefaultSpeed;
            return;
        }

        _speedImpactRecoil = SpeedOfThin(_armSlotCatalog.ImpactRecoilThin, presentation);
        _speedImpactBlocked = SpeedOfThin(_armSlotCatalog.ImpactBlockedThin, presentation);
    }

    float SpeedOfThin(AnimationClip thin, WeaponPresentation presentation)
    {
        if (thin == null || _resolvedOverride == null)
            return WeaponAnimClipSpeeds.DefaultSpeed;
        AnimationClip playing = ArmAnimSlotResolver.EffectiveClip(thin, _resolvedOverride);
        return SpeedOfClip(playing, presentation);
    }

    float SpeedOfClip(AnimationClip playing, WeaponPresentation presentation)
    {
        if (playing == null)
            return WeaponAnimClipSpeeds.DefaultSpeed;
        WeaponAnimClipSpeeds local = presentation != null ? presentation.AnimClipSpeeds : null;
        if (local != null && local.Contains(playing))
            return local.GetSpeed(playing);
        WeaponAnimClipSpeeds catalog = _armSlotCatalog != null ? _armSlotCatalog.ClipSpeeds : null;
        if (catalog != null)
            return catalog.GetSpeed(playing);
        return WeaponAnimClipSpeeds.DefaultSpeed;
    }

    void AdvanceAnimator(float channelDelta)
    {
        if (channelDelta <= 0f)
            return;

        if (_poseRate <= 0f)
        {
            _animator.Update(channelDelta);
            return;
        }

        float step = 1f / _poseRate;
        _poseAccum += channelDelta;
        int steps = 0;
        while (_poseAccum >= step && steps < MaxPoseStepsPerFrame)
        {
            _poseAccum -= step;
            _animator.Update(step);
            steps++;
        }

        if (steps >= MaxPoseStepsPerFrame && _poseAccum >= step)
            _poseAccum %= step;
    }

    void CacheAnimatorParameters()
    {
        _hasSpeed = false;
        _hasMoveX = false;
        _hasMoveZ = false;
        _hasAiming = false;
        _hasStealth = false;
        _hasSwimming = false;
        _hasAttackR = false;
        _hasAttackL = false;
        _hasAttack2H = false;
        _hasImpactRecoil = false;
        _hasImpactBlocked = false;
        _hasArmSpeedR = false;
        _hasArmSpeedL = false;
        _hasArmSpeed2H = false;
        _hasImpactSpeed = false;
        _hasHitFlinch = false;
        _hasHitStagger = false;
        _hasPainShocked = false;
        _hasDefeated = false;
        _rightArmLayerIndex = -1;
        _leftArmLayerIndex = -1;
        _twoHandLayerIndex = -1;
        _impactLayerIndex = -1;
        _flinchLayerIndex = -1;
        _hurtLayerIndex = -1;
        _hashAttackState = Hash(AttackOverlayStateName);
        _hashHoldState = Hash(HoldOverlayStateName);
        _hashAimState = Hash(AimOverlayStateName);
        _hashArmSpeedR = Hash(WeaponAnimClipSpeeds.ParamRight);
        _hashArmSpeedL = Hash(WeaponAnimClipSpeeds.ParamLeft);
        _hashArmSpeed2H = Hash(WeaponAnimClipSpeeds.ParamTwoHand);
        _hashImpactSpeed = Hash(WeaponAnimClipSpeeds.ParamImpact);
        _hashImpactRecoil = Hash(ParamImpactRecoil);
        _hashImpactBlocked = Hash(ParamImpactBlocked);
        _hashImpactEmpty = Hash(ImpactEmptyStateName);
        _hashImpactRecoilState = Hash(ImpactRecoilStateName);
        _hashImpactBlockedState = Hash(ImpactBlockedStateName);
        _hashHurtFlinch = Hash(CharacterHitReact.ParamFlinch);
        _hashHurtStagger = Hash(CharacterHitReact.ParamStagger);
        _hashHurtPainShocked = Hash(CharacterHitReact.ParamPainShocked);
        _hashHurtDefeated = Hash(CharacterHitReact.ParamDefeated);
        _hashHurtFlinchState = Hash(CharacterHitReact.StateFlinch);
        _hashHurtStaggerState = Hash(CharacterHitReact.StateStagger);
        _hashHurtPainDownState = Hash(CharacterHitReact.StatePainDown);
        _hashHurtDeadState = Hash(CharacterHitReact.StateDead);

        if (_animator == null || _animator.runtimeAnimatorController == null)
            return;

        _hashSpeed = Hash(_paramSpeed);
        _hashMoveX = Hash(_paramMoveX);
        _hashMoveZ = Hash(_paramMoveZ);
        _hashAiming = Hash(_paramAiming);
        _hashStealth = Hash(_paramStealth);
        _hashSwimming = Hash(_paramSwimming);
        _hashAttackR = Hash(_paramAttackR);
        _hashAttackL = Hash(_paramAttackL);
        _hashAttack2H = Hash(_paramAttack2H);

        AnimatorControllerParameter[] parameters = _animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            int nameHash = parameters[i].nameHash;
            if (Match(_paramSpeed, _hashSpeed, nameHash)) _hasSpeed = true;
            if (Match(_paramMoveX, _hashMoveX, nameHash)) _hasMoveX = true;
            if (Match(_paramMoveZ, _hashMoveZ, nameHash)) _hasMoveZ = true;
            if (Match(_paramAiming, _hashAiming, nameHash)) _hasAiming = true;
            if (Match(_paramStealth, _hashStealth, nameHash)) _hasStealth = true;
            if (Match(_paramSwimming, _hashSwimming, nameHash)) _hasSwimming = true;
            if (Match(_paramAttackR, _hashAttackR, nameHash)) _hasAttackR = true;
            if (Match(_paramAttackL, _hashAttackL, nameHash)) _hasAttackL = true;
            if (Match(_paramAttack2H, _hashAttack2H, nameHash)) _hasAttack2H = true;
            if (Match(ParamImpactRecoil, _hashImpactRecoil, nameHash)) _hasImpactRecoil = true;
            if (Match(ParamImpactBlocked, _hashImpactBlocked, nameHash)) _hasImpactBlocked = true;
            if (Match(WeaponAnimClipSpeeds.ParamRight, _hashArmSpeedR, nameHash)) _hasArmSpeedR = true;
            if (Match(WeaponAnimClipSpeeds.ParamLeft, _hashArmSpeedL, nameHash)) _hasArmSpeedL = true;
            if (Match(WeaponAnimClipSpeeds.ParamTwoHand, _hashArmSpeed2H, nameHash)) _hasArmSpeed2H = true;
            if (Match(WeaponAnimClipSpeeds.ParamImpact, _hashImpactSpeed, nameHash)) _hasImpactSpeed = true;
            if (Match(CharacterHitReact.ParamFlinch, _hashHurtFlinch, nameHash)) _hasHitFlinch = true;
            if (Match(CharacterHitReact.ParamStagger, _hashHurtStagger, nameHash)) _hasHitStagger = true;
            if (Match(CharacterHitReact.ParamPainShocked, _hashHurtPainShocked, nameHash)) _hasPainShocked = true;
            if (Match(CharacterHitReact.ParamDefeated, _hashHurtDefeated, nameHash)) _hasDefeated = true;
        }

        if (!string.IsNullOrEmpty(_rightArmLayerName))
            _rightArmLayerIndex = _animator.GetLayerIndex(_rightArmLayerName);
        if (!string.IsNullOrEmpty(_leftArmLayerName))
            _leftArmLayerIndex = _animator.GetLayerIndex(_leftArmLayerName);
        if (!string.IsNullOrEmpty(_twoHandLayerName))
            _twoHandLayerIndex = _animator.GetLayerIndex(_twoHandLayerName);
        _impactLayerIndex = _animator.GetLayerIndex(ImpactLayerName);
        _flinchLayerIndex = _animator.GetLayerIndex(CharacterHitReact.FlinchLayerName);
        _hurtLayerIndex = _animator.GetLayerIndex(CharacterHitReact.HurtLayerName);
        _moveLayerIndex = _animator.GetLayerIndex("Move Layer");
    }

    static int Hash(string name) =>
        string.IsNullOrEmpty(name) ? 0 : Animator.StringToHash(name);

    static bool Match(string name, int hash, int nameHash) =>
        !string.IsNullOrEmpty(name) && nameHash == hash;

    void SyncVaultLayerWeights(float channelDelta)
    {
        if (_vaultHost == null || !_vaultHost.IsBusy || _animator == null)
            return;

        SuppressLocomotionLayers(channelDelta);
    }

    void SuppressLocomotionLayers(float channelDelta)
    {
        SetLayerWeightToward(_moveLayerIndex, 0f, channelDelta);
        SetLayerWeightToward(_rightArmLayerIndex, 0f, channelDelta);
        SetLayerWeightToward(_leftArmLayerIndex, 0f, channelDelta);
        SetLayerWeightToward(_twoHandLayerIndex, 0f, channelDelta);
        SetLayerWeightToward(_impactLayerIndex, 0f, channelDelta);
    }

    void SyncArmLayerWeights(float channelDelta)
    {
        if (_vaultHost != null && _vaultHost.IsBusy)
            return;

        bool twoHand = false;
        bool leftArmed = false;
        bool rightArmed = false;

        CharacterGearService gear = _gearHost != null ? _gearHost.Service : null;
        if (gear?.Wield != null)
        {
            twoHand = gear.Wield.IsTwoHand;
            leftArmed = !twoHand && gear.Wield.Left != null;
            rightArmed = !twoHand && gear.Wield.Right != null;
            // 양손 비움 = 비무장 Presentation. 아이템 유무로 끄면 Attack overlay가 안 보인다.
            if (!twoHand && !leftArmed && !rightArmed)
                ApplyActiveWieldHandFlags(ref twoHand, ref leftArmed, ref rightArmed);
        }
        else if (_attacker != null)
        {
            // ItemId 유무와 무관 — 비무장(빈 id)도 ActiveWieldHand overlay 사용.
            ApplyActiveWieldHandFlags(ref twoHand, ref leftArmed, ref rightArmed);
        }

        ResolveHandActions(out WeaponAction actionL, out WeaponAction actionR, out WeaponAction action2H);
        WeaponPresentationCatalog catalog = gear?.PresentationCatalog
            ?? (_attacker != null ? _attacker.Catalog : null);
        bool isAiming = _characterState != null && _characterState.IsAiming;

        float rightTarget = twoHand
            ? 0f
            : ArmOverlayWeight(
                rightArmed,
                WieldHand.Right,
                PresentationForHand(gear, catalog, WieldHand.Right),
                actionR,
                isAiming);
        float leftTarget = twoHand
            ? 0f
            : ArmOverlayWeight(
                leftArmed,
                WieldHand.Left,
                PresentationForHand(gear, catalog, WieldHand.Left),
                actionL,
                isAiming);
        float twoHandTarget = ArmOverlayWeight(
            twoHand,
            WieldHand.TwoHand,
            PresentationForHand(gear, catalog, WieldHand.TwoHand),
            action2H,
            isAiming);

        SetLayerWeightToward(_rightArmLayerIndex, rightTarget, channelDelta);
        SetLayerWeightToward(_leftArmLayerIndex, leftTarget, channelDelta);
        SetLayerWeightToward(_twoHandLayerIndex, twoHandTarget, channelDelta);
    }

    float ArmOverlayWeight(
        bool armed,
        WieldHand hand,
        WeaponPresentation presentation,
        WeaponAction action,
        bool isAiming)
    {
        if (IsInAttackOverlay(hand) || HasQueuedAttack(hand) || HasAttackOverlayLatch(hand))
            return 1f;
        if (!armed)
            return 0f;
        if (isAiming)
            return 1f;
        if (presentation != null && !presentation.UsesHold(action))
            return 0f;
        return 1f;
    }

    static int AttackLatchIndex(WieldHand hand)
    {
        if (hand == WieldHand.Left)
            return 0;
        if (hand == WieldHand.TwoHand)
            return 2;
        return 1;
    }

    void ArmAttackOverlay(WieldHand hand)
    {
        _attackOverlayLatch[AttackLatchIndex(hand)] = AttackLatchArmed;
    }

    bool HasAttackOverlayLatch(WieldHand hand) =>
        _attackOverlayLatch[AttackLatchIndex(hand)] != AttackLatchOff;

    void TickAttackOverlayLatches()
    {
        TickAttackOverlayLatch(WieldHand.Left);
        TickAttackOverlayLatch(WieldHand.Right);
        TickAttackOverlayLatch(WieldHand.TwoHand);
    }

    void TickAttackOverlayLatch(WieldHand hand)
    {
        int index = AttackLatchIndex(hand);
        byte state = _attackOverlayLatch[index];
        if (state == AttackLatchOff)
            return;

        if (state == AttackLatchArmed)
        {
            if (IsInAttackOverlay(hand))
                _attackOverlayLatch[index] = AttackLatchPlaying;
            return;
        }

        if (!IsInAttackOverlay(hand) && !HasQueuedAttack(hand))
            _attackOverlayLatch[index] = AttackLatchOff;
    }

    bool IsInAttackOverlay(WieldHand hand)
    {
        int layerIndex = hand == WieldHand.Left
            ? _leftArmLayerIndex
            : hand == WieldHand.TwoHand
                ? _twoHandLayerIndex
                : _rightArmLayerIndex;
        if (layerIndex < 0 || _animator == null)
            return false;

        AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (current.shortNameHash == _hashAttackState)
            return true;
        if (!_animator.IsInTransition(layerIndex))
            return false;
        AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(layerIndex);
        return next.shortNameHash == _hashAttackState;
    }

    bool HasQueuedAttack(WieldHand hand)
    {
        for (int i = 0; i < _attackQueueCount; i++)
        {
            int index = (_attackQueueHead + i) % _attackHandQueue.Length;
            if (_attackHandQueue[index] == hand)
                return true;
        }

        return false;
    }

    WeaponPresentation PresentationForHand(
        CharacterGearService gear,
        WeaponPresentationCatalog catalog,
        WieldHand hand)
    {
        if (gear?.Wield != null)
        {
            if (hand == WieldHand.TwoHand || gear.Wield.IsTwoHand)
            {
                ItemStack stack = gear.Wield.Left ?? gear.Wield.Right;
                if (stack?.Item != null)
                    return WeaponActionRows.Resolve(catalog, stack);
            }
            else if (hand == WieldHand.Left && gear.Wield.Left != null)
                return WeaponActionRows.Resolve(catalog, gear.Wield.Left);
            else if (hand == WieldHand.Right && gear.Wield.Right != null)
                return WeaponActionRows.Resolve(catalog, gear.Wield.Right);
        }

        return _attacker != null ? _attacker.Presentation : null;
    }

    void ApplyActiveWieldHandFlags(ref bool twoHand, ref bool leftArmed, ref bool rightArmed)
    {
        if (_attacker == null)
            return;
        WieldHand hand = _attacker.ActiveWieldHand;
        twoHand = hand == WieldHand.TwoHand;
        leftArmed = hand == WieldHand.Left;
        rightArmed = hand == WieldHand.Right;
    }

    void SetLayerWeightToward(int layerIndex, float target, float channelDelta)
    {
        if (layerIndex < 0)
            return;

        float current = _animator.GetLayerWeight(layerIndex);
        if (_layerBlendSpeed <= 0f || channelDelta <= 0f)
        {
            if (!Mathf.Approximately(current, target))
                _animator.SetLayerWeight(layerIndex, target);
            return;
        }

        float next = Mathf.MoveTowards(current, target, _layerBlendSpeed * channelDelta);
        if (!Mathf.Approximately(current, next))
            _animator.SetLayerWeight(layerIndex, next);
    }

    void ResolveHandPresentations(
        out WeaponPresentation presentationL,
        out WeaponPresentation presentationR,
        out WeaponPresentation presentation2H)
    {
        CharacterGearService gear = _gearHost != null ? _gearHost.Service : null;
        WeaponPresentationCatalog catalog = gear?.PresentationCatalog
            ?? (_attacker != null ? _attacker.Catalog : null);
        presentationL = PresentationForHand(gear, catalog, WieldHand.Left);
        presentationR = PresentationForHand(gear, catalog, WieldHand.Right);
        presentation2H = PresentationForHand(gear, catalog, WieldHand.TwoHand);
    }

    void ResolveHandActions(out WeaponAction actionL, out WeaponAction actionR, out WeaponAction action2H)
    {
        actionL = WeaponAction.Swing;
        actionR = WeaponAction.Swing;
        action2H = _attacker != null ? _attacker.SelectedAction : WeaponAction.Swing;

        CharacterGearService gear = _gearHost != null ? _gearHost.Service : null;
        if (gear?.Wield == null)
        {
            if (_attacker != null)
            {
                actionL = _attacker.SelectedAction;
                actionR = _attacker.SelectedAction;
            }
            return;
        }

        WeaponPresentationCatalog catalog = gear.PresentationCatalog
            ?? (_attacker != null ? _attacker.Catalog : null);

        if (gear.Wield.IsTwoHand)
        {
            ItemStack stack = gear.Wield.Left ?? gear.Wield.Right;
            action2H = ActionForStack(catalog, stack, action2H);
            actionL = action2H;
            actionR = action2H;
            return;
        }

        actionL = ActionForStack(catalog, gear.Wield.Left, actionL);
        actionR = ActionForStack(catalog, gear.Wield.Right, actionR);
    }

    static WeaponAction ActionForStack(
        WeaponPresentationCatalog catalog,
        ItemStack stack,
        WeaponAction fallback)
    {
        if (stack?.Item == null)
            return fallback;

        WeaponPresentation presentation = WeaponActionRows.Resolve(catalog, stack);
        return WeaponActionRows.ResolveSelected(stack.Instance, presentation);
    }

    void TakeManualControl()
    {
        if (_animator == null)
            return;

        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _animator.enabled = true;
        _animator.Update(0f);
        _animator.enabled = false;
        _animator.Rebind();
        _animator.Update(0f);
        _manualControl = true;
    }

    float ResolveNormalizedSpeed()
    {
        if (_locomotion == null)
            return 0f;

        float max = _locomotion.AnimSpeedReference;
        if (max <= 1e-4f)
            return 0f;

        return Mathf.Clamp01(_locomotion.CurrentSpeed / max);
    }

    void ResolveFacingMoveXZ(float speedNorm, out float moveX, out float moveZ)
    {
        moveX = 0f;
        moveZ = 0f;

        if (speedNorm <= 1e-4f || _characterState == null)
            return;

        Vector3 wish = _characterState.MoveDir;
        wish.y = 0f;
        if (wish.sqrMagnitude <= MoveDirEpsilonSqr)
            return;

        Vector3 facing = _characterState.GetFacingDir();
        facing.y = 0f;
        if (facing.sqrMagnitude <= MoveDirEpsilonSqr)
            return;

        Quaternion facingRot = Quaternion.LookRotation(facing.normalized, Vector3.up);
        Vector3 local = Quaternion.Inverse(facingRot) * wish.normalized;
        local.y = 0f;
        if (local.sqrMagnitude <= MoveDirEpsilonSqr)
            return;

        local.Normalize();
        moveX = local.x * speedNorm;
        moveZ = local.z * speedNorm;
    }
}
