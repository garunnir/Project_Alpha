// ============================================================
// CharacterLocomotionAnim — MoveXZ (Animancer DirectionalMixer) + L/R/2H overlays + Impact + Hurt
// ============================================================
using Animancer;
using Garunnir.Runtime.Gameplay.Data;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Drives Move (facing-relative MoveX/MoveZ) + RightArm/LeftArm/TwoHand overlays + Impact + Hurt + Work.
/// S7 end state: live playback = Animancer layers only. Hybrid may host the graph but
/// <c>CharacterAnimController</c> SM is not required (Controller cleared / Layers[7] weight 0, non-SSOT).
/// Dynamic Layers: Move base always full-body (feet); while moving, Work layer uses UpperBody.mask.
/// CombatLeaf → Entry→Catalog via <see cref="ArmAnimSlotResolver.ResolvePoseClip"/> /
/// <see cref="ArmImpactSlotResolver.ResolveImpactClip"/> (thin remap = clip swap).
/// Animation time advances via <see cref="TimeScaleService"/> → Animancer Evaluate only.
/// </summary>
[RequireComponent(typeof(CharacterState))]
public class CharacterLocomotionAnim : MonoBehaviour
{
    const string ManualTickHelp =
        "Play: Animator stays enabled (Evaluate writes bones only when enabled) + Animancer Graph PauseGraph. " +
        "Time source is CharacterLocomotionAnim → AnimancerComponent.Update(channelDelta)(=Evaluate), " +
        "not PlayableGraph auto-play. Graph paused in Inspector/runtime is expected.";

    const string DefaultRightArmLayer = "RightArm Layer";
    const string DefaultLeftArmLayer = "LeftArm Layer";
    const string DefaultTwoHandLayer = "TwoHand Layer";
    const string MecanimMoveLayerName = "Move Layer";
    const float DefaultPoseRate = 0f;
    const float DefaultLayerBlendSpeed = 10f;
    const int MaxPoseStepsPerFrame = 8;
    const float MoveDirEpsilonSqr = 1e-6f;
    /// <summary>Approximately stopped MoveXZ magnitude (Animancer Move full-body mask).</summary>
    const float MoveStoppedEpsilonSqr = 1e-4f;
    /// <summary>Animancer layer: Directional Move mixer (base locomotion).</summary>
    const int AnimancerMoveLayerIndex = 0;
    /// <summary>Unused spacer between Hurt (6) and Work (8).</summary>
    const int AnimancerSpacerLayerIndex = CharacterLocomotionHurtAnimancer.LayerUnused;
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

    [Header("Animator (TimeScale manual tick via Animancer)")]
    [Tooltip(ManualTickHelp)]
    [SerializeField] Animator _animator;
    [Tooltip("Animancer graph host on the same Animator (no AnimatorController).")]
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("_hybrid")] AnimancerComponent _animancer;
    [Tooltip("Legacy CharacterAnimController / Override root identity only — not live playback SSOT when Animancer owns Move.")]
    [SerializeField] RuntimeAnimatorController _defaultController;
    [SerializeField] ArmAnimSlotCatalog _armSlotCatalog;
    [Header("Animancer Move (S3)")]
    [Tooltip("Idle + Walk/Run directional clips + walk/run ring thresholds (SSOT).")]
    [SerializeField] CharacterLocomotionMoveSet _moveSet;
    [Tooltip("UpperBody.mask — TwoHand arm layer + Dynamic Layers on Work Animancer layer while moving.")]
    [SerializeField] AvatarMask _moveUpperBodyMask;
    [Header("Animancer Arm / Impact (S4)")]
    [Tooltip("RightArm.mask — Animancer RightArm pose layer.")]
    [SerializeField] AvatarMask _rightArmMask;
    [Tooltip("LeftArm.mask — Animancer LeftArm pose layer.")]
    [SerializeField] AvatarMask _leftArmMask;
    [Header("Animancer Hurt / Flinch (S5)")]
    [Tooltip("HeadTorso.mask — Additive Flinch layer.")]
    [SerializeField] AvatarMask _headTorsoMask;
    [Tooltip("HitFlinch_Slot thin clip (not ArmAnimSlotCatalog).")]
    [SerializeField] AnimationClip _hitFlinchClip;
    [Tooltip("HitStagger_Slot thin clip.")]
    [SerializeField] AnimationClip _hitStaggerClip;
    [Tooltip("HitPainDown_Slot thin clip (loop).")]
    [SerializeField] AnimationClip _hitPainDownClip;
    [Tooltip("HitDead_Slot thin clip (one-shot).")]
    [SerializeField] AnimationClip _hitDeadClip;
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
    [Header("Free movement turn lean")]
    [SerializeField, Range(0f, 15f)] float _maxTurnLean = 7f;
    [SerializeField, Min(0.01f)] float _turnLeanSmoothTime = 0.12f;
    Transform _leftFoot;
    Transform _rightFoot;
    Transform _leanSpine;
    Quaternion _spineBeforeLean;
    Quaternion _spineAfterLean;
    float _turnLean;
    bool _leanApplied;
    bool _allowLocomotionLean;

    [ShowInInspector, ReadOnly, PropertyOrder(20)]
    [LabelText("Manual tick active (graph paused, Animator on)")]
    bool ManualTickActive =>
        _manualControl
        && _animator != null
        && _animator.enabled
        && _animancer != null
        && _animancer.IsGraphInitialized
        && !_animancer.Graph.IsGraphPlaying;

    CharacterState _characterState;
    CharacterLocomotionFacing _locomotionFacing;
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
    MixerTransition2D _moveMixerTransition;
    Vector2MixerState _moveMixerState;
    readonly CharacterLocomotionTransitions _moveTransitions = new CharacterLocomotionTransitions();
    public CharacterLocomotionTransitions.Motion MovementMotion => _moveTransitions.Current;
    internal float GetPivotMovementScale(Vector3 input)
    {
        if (!isActiveAndEnabled || _animator == null || !_animator.enabled || !OwnsAnimancerMove
            || _characterState == null || _characterState.IsAiming || _characterState.IsStealth
            || _characterState.IsSwimming || _characterState.IsDiving
            || (_vaultHost != null && _vaultHost.IsBusy) || _hurtWeightTarget > 0.01f
            || _workAnimancer.IsWeightActive(_animancer))
            return 1f;
        return _moveTransitions.GetMovementScale(input);
    }
    bool _animancerMoveReady;
    bool _ensuringAnimancerGraph;
    bool _moveLayerUsesUpperMask;
    float _animancerMoveWeightTarget = 1f;
    readonly CharacterLocomotionArmImpactAnimancer _armImpactAnimancer = new CharacterLocomotionArmImpactAnimancer();
    readonly CharacterLocomotionHurtAnimancer _hurtAnimancer = new CharacterLocomotionHurtAnimancer();
    readonly CharacterLocomotionWorkAnimancer _workAnimancer = new CharacterLocomotionWorkAnimancer();
    int _mecanimWorkLayerIndex = -1;
    bool _pendingAnimancerAttackR;
    bool _pendingAnimancerAttackL;
    bool _pendingAnimancerAttack2H;
    float _speedAim2H = WeaponAnimClipSpeeds.DefaultSpeed;
    float _speedAttack2H = WeaponAnimClipSpeeds.DefaultSpeed;
    float _speedImpactRecoil = WeaponAnimClipSpeeds.DefaultSpeed;
    float _speedImpactBlocked = WeaponAnimClipSpeeds.DefaultSpeed;

    public bool HasAttackTrigger => _hasAttackR || _hasAttackL || _hasAttack2H;

    CombatLeaf _mappedActionL = (CombatLeaf)(-1);
    CombatLeaf _mappedActionR = (CombatLeaf)(-1);
    CombatLeaf _mappedAction2H = (CombatLeaf)(-1);
    WeaponPresentation _mappedPresentationL;
    WeaponPresentation _mappedPresentationR;
    WeaponPresentation _mappedPresentation2H;

    readonly CombatLeaf[] _attackActionQueue = new CombatLeaf[2];
    readonly WieldHand[] _attackHandQueue = new WieldHand[2];
    readonly bool[] _attackSurpriseQueue = new bool[2];
    bool _mappedSurpriseL;
    bool _mappedSurpriseR;
    bool _mappedSurprise2H;
    int _attackQueueHead;
    int _attackQueueCount;

    /// <summary>useHold=false????Attack ?�→?�생 ?�안 overlay ?��?. 0=off 1=armed 2=playing.</summary>
    readonly byte[] _attackOverlayLatch = new byte[3];
    const byte AttackLatchOff = 0;
    const byte AttackLatchArmed = 1;
    const byte AttackLatchPlaying = 2;

    public Animator Animator => _animator;
    /// <summary>S6: Animancer Work layer index when owned; else Mecanim Work Layer index.</summary>
    public int WorkLayerIndex { get; private set; } = -1;
    public ArmAnimSlotCatalog ArmSlotCatalog => _armSlotCatalog;

    public bool TryGetWorkAnim(out Animator animator, out int workLayerIndex)
    {
        animator = _animator;
        workLayerIndex = WorkLayerIndex;
        return _animator != null && WorkLayerIndex >= 0;
    }

    /// <summary>S6: Animancer Work layer index for binders (same as <see cref="WorkLayerIndex"/> when owned).</summary>
    public bool TryGetWorkLayer(out int workLayerIndex)
    {
        workLayerIndex = WorkLayerIndex;
        return WorkLayerIndex >= 0;
    }

    void Awake()
    {
        ResolveBodyRefs();
        _hitStop = CharacterHitStopState.Find(this);

        ResolveAnimatorAndAnimancer();
        ConfigureAnimancerHost();

        if (_defaultController == null)
            _defaultController = ResolveBaseController(ActiveController);

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
            _locomotionFacing = CharacterBodyResolve.GetInBody<CharacterLocomotionFacing>(this);
            return;
        }

        _characterState = CharacterBodyResolve.GetInBody<CharacterState>(this);
        _attacker = CharacterBodyResolve.GetModule<CharacterAttacker>(this);
            _gearHost = CharacterBodyResolve.GetModule<PlayerGearHost>(this);
        _skillsHost = CharacterBodyResolve.GetModule<CharacterSkillsHost>(this);
        _locomotion = CharacterBodyResolve.GetInBody<CharacterMotor>(this);
        _vaultHost = CharacterBodyResolve.GetInBody<CharacterVaultHost>(this);
        _locomotionFacing = CharacterBodyResolve.GetInBody<CharacterLocomotionFacing>(this);
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
        RestoreTurnLean();
        _moveTransitions.Reset(_locomotionFacing);
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
        ResolveAnimatorAndAnimancer();
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

        if (_moveSet == null)
        {
            _moveSet = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterLocomotionMoveSet>(
                "Assets/Dist/SOData/Locomotion/CharacterLocomotionMoveSet.asset");
        }

        if (_moveUpperBodyMask == null)
        {
            _moveUpperBodyMask = UnityEditor.AssetDatabase.LoadAssetAtPath<AvatarMask>(
                "Assets/Dist/Visual/Anim/CharacterAnimator/UpperBody.mask");
        }

        if (_rightArmMask == null)
        {
            _rightArmMask = UnityEditor.AssetDatabase.LoadAssetAtPath<AvatarMask>(
                "Assets/Dist/Visual/Anim/CharacterAnimator/RightArm.mask");
        }

        if (_leftArmMask == null)
        {
            _leftArmMask = UnityEditor.AssetDatabase.LoadAssetAtPath<AvatarMask>(
                "Assets/Dist/Visual/Anim/CharacterAnimator/LeftArm.mask");
        }
#endif
        ResolveAnimatorAndAnimancer();
        if (_animator != null)
            CacheAnimatorParameters();
    }

    void ResolveAnimatorAndAnimancer()
    {
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();
        if (_animancer == null)
            _animancer = GetComponentInChildren<AnimancerComponent>();
        if (_animancer != null && _animancer.Animator == null && _animator != null)
            _animancer.Animator = _animator;
        if (_animator != null && _animator.isHuman && _animator.avatar != null && _animator.avatar.isValid)
        {
            _leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            _leanSpine = _animator.GetBoneTransform(HumanBodyBones.Spine);
        }
    }

    /// <summary>
    /// Clear AnimatorController on the Animator — live playback is Animancer layers only.
    /// </summary>
    void ConfigureAnimancerHost()
    {
        if (_animator == null)
            return;

        // CharacterMotor owns translation; source transition clips contain root travel/yaw.
        _animator.applyRootMotion = false;

        if (_animator.runtimeAnimatorController != null)
            _animator.runtimeAnimatorController = null;
    }

    RuntimeAnimatorController ActiveController
    {
        get => _animator != null ? _animator.runtimeAnimatorController : null;
        set
        {
            if (_animator != null)
                _animator.runtimeAnimatorController = value;
        }
    }

    void Update()
        => TickAnimation(TimeScaleService.Delta(_timeChannel));

    /// <summary>One channel-time tick; also used by deterministic Play-mode checks.</summary>
    internal void TickAnimation(float channelDelta)
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

        if (channelDelta > 0f)
            RestoreTurnLean();

        float speedNorm = ResolveNormalizedSpeed();
        if (_hasSpeed)
            AnimSetFloat(_hashSpeed, speedNorm);

        ResolveFacingMoveXZ(speedNorm, out float moveX, out float moveZ);
        if (OwnsAnimancerMove)
        {
            try
            {
                SyncAnimancerMove(moveX, moveZ, rebound ? 0f : channelDelta);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, this);
            }
        }
        else
        {
            _locomotionFacing?.TickFacing(channelDelta);
            if (_hasMoveX)
                AnimSetFloat(_hashMoveX, moveX);
            if (_hasMoveZ)
                AnimSetFloat(_hashMoveZ, moveZ);
        }

        bool isAiming = _characterState != null && _characterState.IsAiming;
        if (_hasAiming)
            AnimSetBool(_hashAiming, isAiming);

        bool isStealth = _characterState != null && _characterState.IsStealth;
        if (_hasStealth)
            AnimSetBool(_hashStealth, isStealth);

        bool isSwimming = _characterState != null
            && (_characterState.IsSwimming || _characterState.IsDiving);
        if (_hasSwimming)
            AnimSetBool(_hashSwimming, isSwimming);

        ResolveHandActions(out CombatLeaf actionL, out CombatLeaf actionR, out CombatLeaf action2H);
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
            CombatLeaf attackAction = _attackActionQueue[_attackQueueHead];
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
                if (OwnsAnimancerArmImpact)
                    _pendingAnimancerAttack2H = true;
                else if (_hasAttack2H)
                    AnimSetTrigger(_hashAttack2H);
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
                if (OwnsAnimancerArmImpact)
                    _pendingAnimancerAttackL = true;
                else if (_hasAttackL)
                    AnimSetTrigger(_hashAttackL);
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
                if (OwnsAnimancerArmImpact)
                    _pendingAnimancerAttackR = true;
                else if (_hasAttackR)
                    AnimSetTrigger(_hashAttackR);
            }
        }

        UpdateFlinchWeightTarget();
        UpdateHurtWeightTarget();
        SyncVaultLayerWeights(rebound ? 0f : channelDelta);
        if (OwnsAnimancerMove)
            EnsureAnimancerPlayableReady();
        // Pose before weight: arm layers use Body+Head masks — weight>0 with no clip = T-pose upper body.
        if (OwnsAnimancerArmImpact)
            SyncAnimancerArmPoses(presentationL, presentationR, presentation2H, actionL, actionR, action2H);
        SyncArmLayerWeights(rebound ? 0f : channelDelta);
        SyncImpactLayerWeight(rebound ? 0f : channelDelta);
        SyncFlinchLayerWeight(rebound ? 0f : channelDelta);
        SyncHurtLayerWeight(rebound ? 0f : channelDelta);
        if (OwnsAnimancerMove)
            SyncOverlayLayerWeightAfterArms();
        ApplyClipSpeedParams();
        AdvanceAnimator(channelDelta);
        if (channelDelta > 0f)
            ApplyTurnLean(channelDelta);
        TickAttackOverlayLatches();
        TickAttackCues();
        TickImpactEmpty();
        TickHurtEmpty();
    }

    void SyncThinActionRemap(
        WeaponPresentation presentationL,
        WeaponPresentation presentationR,
        WeaponPresentation presentation2H,
        CombatLeaf actionL,
        CombatLeaf actionR,
        CombatLeaf action2H,
        bool surpriseL = false,
        bool surpriseR = false,
        bool surprise2H = false)
    {
        if (_armSlotCatalog == null)
            return;
        if (!OwnsAnimancerArmImpact && _resolvedOverride == null)
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

        // S4 Animancer arm: ResolvePoseClip clip swap only (no Mecanim thin Remap / Rebind).
        // Mecanim path still remaps thin keys onto Override.
        if (!OwnsAnimancerArmImpact)
        {
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
        }

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
        if (CombatLeafUtil.SuppressesAttackTrigger(outcome.Action))
            return;
        if (_attackQueueCount >= _attackActionQueue.Length)
            return;

        int index = (_attackQueueHead + _attackQueueCount) % _attackActionQueue.Length;
        _attackActionQueue[index] = outcome.Action;
        _attackHandQueue[index] = outcome.Hand;
        _attackSurpriseQueue[index] = outcome.UseSurpriseAttackClip;
        _attackQueueCount++;
    }

    void OnAttackCueFired(WieldHand hand, CombatLeaf action)
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

    void PlayImpact(ArmImpactKind kind, WieldHand hand, CombatLeaf action)
    {
        if (_animator == null || _armSlotCatalog == null)
            return;

        CharacterGearService gear = _gearHost != null ? _gearHost.Service : null;
        WeaponPresentationCatalog presentationCatalog = gear?.PresentationCatalog
            ?? (_attacker != null ? _attacker.Catalog : null);
        WeaponPresentation presentation = PresentationForHand(gear, presentationCatalog, hand);

        if (OwnsAnimancerArmImpact)
        {
            AnimationClip clip = ArmImpactSlotResolver.ResolveImpactClip(
                _armSlotCatalog,
                presentation,
                action,
                kind,
                hand);
            if (clip == null)
                return;
            float speed = SpeedOfClip(clip, presentation);
            if (kind == ArmImpactKind.Blocked)
                _speedImpactBlocked = speed;
            else
                _speedImpactRecoil = speed;
            _armImpactAnimancer.PlayImpact(_animancer, clip, speed);
            _impactWeightTarget = 1f;
            return;
        }

        if (_resolvedOverride == null || _impactLayerIndex < 0)
            return;

        ArmImpactSlotResolver.ProjectImpact(
            _resolvedOverride,
            _armSlotCatalog,
            presentation,
            action,
            kind,
            hand);
        RefreshImpactClipSpeeds();
        _impactWeightTarget = 1f;
        AnimSetLayerWeight(_impactLayerIndex, 1f);

        if (kind == ArmImpactKind.Blocked)
        {
            if (_hasImpactBlocked)
                AnimSetTrigger(_hashImpactBlocked);
        }
        else if (_hasImpactRecoil)
        {
            AnimSetTrigger(_hashImpactRecoil);
        }
    }

    void SyncImpactLayerWeight(float channelDelta)
    {
        if (OwnsAnimancerArmImpact)
        {
            SetAnimancerArmLayerWeightToward(
                CharacterLocomotionArmImpactAnimancer.LayerImpact,
                _impactWeightTarget,
                channelDelta);
            return;
        }

        SetLayerWeightToward(_impactLayerIndex, _impactWeightTarget, channelDelta);
    }

    void SyncFlinchLayerWeight(float channelDelta)
    {
        if (OwnsAnimancerHurt)
        {
            SetAnimancerHurtLayerWeightToward(
                CharacterLocomotionHurtAnimancer.LayerFlinch,
                _flinchWeightTarget,
                channelDelta);
            ForceMecanimFlinchHurtLayerWeightsZero();
            return;
        }

        SetLayerWeightToward(_flinchLayerIndex, _flinchWeightTarget, channelDelta);
    }

    void SyncHurtLayerWeight(float channelDelta)
    {
        if (OwnsAnimancerHurt)
        {
            SetAnimancerHurtLayerWeightToward(
                CharacterLocomotionHurtAnimancer.LayerHurt,
                _hurtWeightTarget,
                channelDelta);
            ForceMecanimFlinchHurtLayerWeightsZero();
            return;
        }

        SetLayerWeightToward(_hurtLayerIndex, _hurtWeightTarget, channelDelta);
    }

    void UpdateFlinchWeightTarget()
    {
        if (OwnsAnimancerHurt)
        {
            _flinchWeightTarget = _hurtAnimancer.IsFlinchPlaying() ? 1f : 0f;
            return;
        }

        if (_flinchLayerIndex < 0 || _animator == null)
            return;

        bool flinch = false;
        AnimatorStateInfo current = AnimGetCurrentAnimatorStateInfo(_flinchLayerIndex);
        flinch = current.shortNameHash == _hashHurtFlinchState;
        if (!flinch && AnimIsInTransition(_flinchLayerIndex))
        {
            AnimatorStateInfo next = AnimGetNextAnimatorStateInfo(_flinchLayerIndex);
            flinch = next.shortNameHash == _hashHurtFlinchState;
        }

        if (!flinch && _hasHitFlinch && AnimGetBool(_hashHurtFlinch))
            flinch = true;

        _flinchWeightTarget = flinch ? 1f : 0f;
    }

    void UpdateHurtWeightTarget()
    {
        if (OwnsAnimancerHurt)
        {
            _hurtWeightTarget = _hurtAnimancer.IsHurtWeightActive() ? 1f : 0f;
            return;
        }

        if (_hurtLayerIndex < 0 || _animator == null)
            return;

        bool hurt = false;
        if (_hasDefeated && AnimGetBool(_hashHurtDefeated))
            hurt = true;
        else if (_hasPainShocked && AnimGetBool(_hashHurtPainShocked))
            hurt = true;
        else
        {
            AnimatorStateInfo current = AnimGetCurrentAnimatorStateInfo(_hurtLayerIndex);
            hurt = IsHurtPlayingState(current.shortNameHash);
            if (!hurt && AnimIsInTransition(_hurtLayerIndex))
            {
                AnimatorStateInfo next = AnimGetNextAnimatorStateInfo(_hurtLayerIndex);
                hurt = IsHurtPlayingState(next.shortNameHash);
            }

            if (!hurt && _hasHitStagger && AnimGetBool(_hashHurtStagger))
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
        if (_impactWeightTarget <= 0f)
            return;

        if (OwnsAnimancerArmImpact)
        {
            if (!_armImpactAnimancer.IsImpactPlaying())
                _impactWeightTarget = 0f;
            return;
        }

        if (_impactLayerIndex < 0 || _animator == null)
            return;

        AnimatorStateInfo current = AnimGetCurrentAnimatorStateInfo(_impactLayerIndex);
        bool inImpact =
            current.shortNameHash == _hashImpactRecoilState ||
            current.shortNameHash == _hashImpactBlockedState;
        if (AnimIsInTransition(_impactLayerIndex))
        {
            AnimatorStateInfo next = AnimGetNextAnimatorStateInfo(_impactLayerIndex);
            if (next.shortNameHash == _hashImpactRecoilState ||
                next.shortNameHash == _hashImpactBlockedState)
                inImpact = true;
        }

        if (!inImpact && current.shortNameHash == _hashImpactEmpty)
            _impactWeightTarget = 0f;
    }

    void TickHurtEmpty()
    {
        if (!OwnsAnimancerHurt)
            return;
        _hurtAnimancer.TickEmptyWeights(_animancer);
        _flinchWeightTarget = _hurtAnimancer.IsFlinchPlaying() ? 1f : 0f;
        _hurtWeightTarget = _hurtAnimancer.IsHurtWeightActive() ? 1f : 0f;
    }

    void SetAnimancerHurtLayerWeightToward(int animancerLayerIndex, float target, float channelDelta)
    {
        if (_animancer == null || !_animancer.IsGraphInitialized)
            return;

        float current = _hurtAnimancer.GetLayerWeight(_animancer, animancerLayerIndex);
        if (_layerBlendSpeed <= 0f || channelDelta <= 0f)
        {
            if (!Mathf.Approximately(current, target))
                _hurtAnimancer.SetLayerWeight(_animancer, animancerLayerIndex, target);
            return;
        }

        float next = Mathf.MoveTowards(current, target, _layerBlendSpeed * channelDelta);
        if (!Mathf.Approximately(current, next))
            _hurtAnimancer.SetLayerWeight(_animancer, animancerLayerIndex, next);
    }

    /// <summary>HitReact bridge: play Additive Flinch on Animancer when S5 owned.</summary>
    public bool TryPlayHitFlinch()
    {
        if (!OwnsAnimancerHurt)
            return false;
        EnsureAnimancerPlayableReady();
        _hurtAnimancer.PlayFlinch(_animancer, _hitFlinchClip);
        _flinchWeightTarget = 1f;
        ForceMecanimFlinchHurtLayerWeightsZero();
        return true;
    }

    /// <summary>HitReact bridge: play Override Stagger on Animancer when S5 owned.</summary>
    public bool TryPlayHitStagger()
    {
        if (!OwnsAnimancerHurt)
            return false;
        EnsureAnimancerPlayableReady();
        _hurtAnimancer.PlayStagger(_animancer, _hitStaggerClip);
        _hurtWeightTarget = 1f;
        ForceMecanimFlinchHurtLayerWeightsZero();
        return true;
    }

    /// <summary>HitReact bridge: PainDown loop while shocked (ICharacterDefeat path separate).</summary>
    public bool TrySyncHitPainShocked(bool shocked)
    {
        if (!OwnsAnimancerHurt)
            return false;
        EnsureAnimancerPlayableReady();
        if (shocked)
        {
            _hurtAnimancer.PlayPainDown(_animancer, _hitPainDownClip);
            _hurtWeightTarget = 1f;
        }
        else
        {
            _hurtAnimancer.ClearHurtIfMode(_animancer, clearPain: true, clearDead: false);
            _hurtWeightTarget = _hurtAnimancer.IsHurtWeightActive() ? 1f : 0f;
        }

        ForceMecanimFlinchHurtLayerWeightsZero();
        return true;
    }

    /// <summary>HitReact bridge: Dead one-shot from ICharacterDefeat.IsDefeated (not body.IsDeadState).</summary>
    public bool TrySyncHitDefeated(bool defeated)
    {
        if (!OwnsAnimancerHurt)
            return false;
        EnsureAnimancerPlayableReady();
        if (defeated)
        {
            _hurtAnimancer.PlayDead(_animancer, _hitDeadClip);
            _hurtWeightTarget = 1f;
        }
        else
        {
            _hurtAnimancer.ClearHurtIfMode(_animancer, clearPain: false, clearDead: true);
            _hurtWeightTarget = _hurtAnimancer.IsHurtWeightActive() ? 1f : 0f;
        }

        ForceMecanimFlinchHurtLayerWeightsZero();
        return true;
    }

    void OnPresentationChanged()
    {
        // Dual hand swap: same AnimatorOverride source → ApplyWeaponAnimOverride early-outs;
        // mapped invalidation drives ResolvePoseClip / thin remap without Rebind.
        // Loadout Presentation with a new Override source → full BuildResolvedOverride + Rebind.
        _mappedActionL = (CombatLeaf)(-1);
        _mappedActionR = (CombatLeaf)(-1);
        _mappedAction2H = (CombatLeaf)(-1);
        _mappedSurpriseL = false;
        _mappedSurpriseR = false;
        _mappedSurprise2H = false;
        _mappedPresentationL = null;
        _mappedPresentationR = null;
        _mappedPresentation2H = null;
        ApplyWeaponAnimOverride(forceRebind: false);
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

        // Animancer path: Presentation/Catalog clip speeds only — no Mecanim Override remapping onto Hybrid.
        if (OwnsAnimancerMove && _armSlotCatalog != null)
        {
            if (!forceRebind && ReferenceEquals(source, _weaponSourceController))
            {
                RefreshActionClipSpeeds();
                RefreshImpactClipSpeeds();
                return;
            }

            if (_resolvedOverride != null)
            {
                if (ReferenceEquals(ActiveController, _resolvedOverride))
                    ActiveController = null;
                Destroy(_resolvedOverride);
                _resolvedOverride = null;
            }

            _weaponSourceController = source;
            _mappedActionL = (CombatLeaf)(-1);
            _mappedActionR = (CombatLeaf)(-1);
            _mappedAction2H = (CombatLeaf)(-1);
            _mappedSurpriseL = false;
            _mappedSurpriseR = false;
            _mappedSurprise2H = false;
            _mappedPresentationL = null;
            _mappedPresentationR = null;
            _mappedPresentation2H = null;
            RefreshActionClipSpeeds();
            RefreshImpactClipSpeeds();
            CacheAnimatorParameters();
            RefreshWorkLayerIndex();
            this.GetBodyRefs()?.HitReact?.RefreshAnimatorHurtBinding();
            if (forceRebind || _manualControl)
            {
                _manualControl = false;
                _pendingBind = true;
            }

            return;
        }

        if (source == null)
            return;

        if (!forceRebind && ReferenceEquals(source, _weaponSourceController) && _resolvedOverride != null)
            return;

        if (_resolvedOverride != null)
        {
            if (ReferenceEquals(ActiveController, _resolvedOverride))
                ActiveController = null;
            Destroy(_resolvedOverride);
            _resolvedOverride = null;
        }

        _weaponSourceController = source;
        _mappedActionL = (CombatLeaf)(-1);
        _mappedActionR = (CombatLeaf)(-1);
        _mappedAction2H = (CombatLeaf)(-1);
        _mappedSurpriseL = false;
        _mappedSurpriseR = false;
        _mappedSurprise2H = false;
        _mappedPresentationL = null;
        _mappedPresentationR = null;
        _mappedPresentation2H = null;

        ResolveHandActions(out CombatLeaf actionL, out CombatLeaf actionR, out CombatLeaf action2H);
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

        if (!ReferenceEquals(ActiveController, next))
            ActiveController = next;

        CacheAnimatorParameters();
        RefreshWorkLayerIndex();
        this.GetBodyRefs()?.HitReact?.RefreshAnimatorHurtBinding();

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
        if (ReferenceEquals(ActiveController, _resolvedOverride))
            ActiveController = _defaultController;
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
        _mecanimWorkLayerIndex = AnimGetLayerIndex(CharacterWorkLayerAnim.LayerName);
        if (OwnsAnimancerWork || WantsAnimancerWork)
            WorkLayerIndex = CharacterLocomotionWorkAnimancer.LayerWork;
        else
            WorkLayerIndex = _mecanimWorkLayerIndex;
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

        if (OwnsAnimancerArmImpact)
        {
            if (_armImpactAnimancer.TryGetAttackNormalizedTime(hand, out float animancerNt))
            {
                _attacker.NotifyAttackOverlayTick(hand, inAttack: true, animancerNt);
                return;
            }

            _attacker.NotifyAttackOverlayTick(hand, inAttack: false, normalizedTime: 0f);
            return;
        }

        if (layerIndex < 0)
        {
            _attacker.NotifyAttackCueForHand(hand);
            return;
        }

        AnimatorStateInfo current = AnimGetCurrentAnimatorStateInfo(layerIndex);
        bool inAttack = current.shortNameHash == _hashAttackState;
        float normalizedTime = current.normalizedTime;
        if (AnimIsInTransition(layerIndex))
        {
            AnimatorStateInfo next = AnimGetNextAnimatorStateInfo(layerIndex);
            if (next.shortNameHash == _hashAttackState)
            {
                inAttack = true;
                normalizedTime = next.normalizedTime;
            }
        }

        _attacker.NotifyAttackOverlayTick(hand, inAttack, normalizedTime);
    }

    // Update: SetFloat only on Mecanim path. Animancer arm/impact use state.Speed in SyncAnimancerArmPoses / PlayImpact.
    void ApplyClipSpeedParams()
    {
        if (_animator == null || OwnsAnimancerArmImpact)
            return;
        if (_hasArmSpeedR)
            AnimSetFloat(
                _hashArmSpeedR,
                SpeedForArmLayer(_rightArmLayerIndex, _speedHoldR, _speedAimR, _speedAttackR));
        if (_hasArmSpeedL)
            AnimSetFloat(
                _hashArmSpeedL,
                SpeedForArmLayer(_leftArmLayerIndex, _speedHoldL, _speedAimL, _speedAttackL));
        if (_hasArmSpeed2H)
            AnimSetFloat(
                _hashArmSpeed2H,
                SpeedForArmLayer(_twoHandLayerIndex, _speedHold2H, _speedAim2H, _speedAttack2H));
        if (_hasImpactSpeed)
            AnimSetFloat(_hashImpactSpeed, SpeedForImpactLayer());
    }

    float SpeedForArmLayer(int layerIndex, float hold, float aim, float attack)
    {
        if (layerIndex < 0)
            return WeaponAnimClipSpeeds.DefaultSpeed;
        AnimatorStateInfo info = AnimGetCurrentAnimatorStateInfo(layerIndex);
        if (AnimIsInTransition(layerIndex))
        {
            AnimatorStateInfo next = AnimGetNextAnimatorStateInfo(layerIndex);
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
        AnimatorStateInfo info = AnimGetCurrentAnimatorStateInfo(_impactLayerIndex);
        if (AnimIsInTransition(_impactLayerIndex))
        {
            AnimatorStateInfo next = AnimGetNextAnimatorStateInfo(_impactLayerIndex);
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
        ResolveHandPresentations(
            out WeaponPresentation presentationL,
            out WeaponPresentation presentationR,
            out WeaponPresentation presentation2H);
        ResolveHandActions(out CombatLeaf actionL, out CombatLeaf actionR, out CombatLeaf action2H);

        if (OwnsAnimancerArmImpact || _armSlotCatalog != null)
        {
            _speedHoldR = SpeedOfClip(
                ArmAnimSlotResolver.ResolvePoseClip(
                    _armSlotCatalog, presentationR, actionR, WieldHand.Right,
                    ArmAnimSlotResolver.PoseKind.Hold),
                presentationR);
            _speedAimR = SpeedOfClip(
                ArmAnimSlotResolver.ResolvePoseClip(
                    _armSlotCatalog, presentationR, actionR, WieldHand.Right,
                    ArmAnimSlotResolver.PoseKind.Aim),
                presentationR);
            _speedAttackR = SpeedOfClip(
                ArmAnimSlotResolver.ResolvePoseClip(
                    _armSlotCatalog, presentationR, actionR, WieldHand.Right,
                    ArmAnimSlotResolver.PoseKind.Attack,
                    _mappedSurpriseR),
                presentationR);
            _speedHoldL = SpeedOfClip(
                ArmAnimSlotResolver.ResolvePoseClip(
                    _armSlotCatalog, presentationL, actionL, WieldHand.Left,
                    ArmAnimSlotResolver.PoseKind.Hold),
                presentationL);
            _speedAimL = SpeedOfClip(
                ArmAnimSlotResolver.ResolvePoseClip(
                    _armSlotCatalog, presentationL, actionL, WieldHand.Left,
                    ArmAnimSlotResolver.PoseKind.Aim),
                presentationL);
            _speedAttackL = SpeedOfClip(
                ArmAnimSlotResolver.ResolvePoseClip(
                    _armSlotCatalog, presentationL, actionL, WieldHand.Left,
                    ArmAnimSlotResolver.PoseKind.Attack,
                    _mappedSurpriseL),
                presentationL);
            _speedHold2H = SpeedOfClip(
                ArmAnimSlotResolver.ResolvePoseClip(
                    _armSlotCatalog, presentation2H, action2H, WieldHand.TwoHand,
                    ArmAnimSlotResolver.PoseKind.Hold),
                presentation2H);
            _speedAim2H = SpeedOfClip(
                ArmAnimSlotResolver.ResolvePoseClip(
                    _armSlotCatalog, presentation2H, action2H, WieldHand.TwoHand,
                    ArmAnimSlotResolver.PoseKind.Aim),
                presentation2H);
            _speedAttack2H = SpeedOfClip(
                ArmAnimSlotResolver.ResolvePoseClip(
                    _armSlotCatalog, presentation2H, action2H, WieldHand.TwoHand,
                    ArmAnimSlotResolver.PoseKind.Attack,
                    _mappedSurprise2H),
                presentation2H);
            return;
        }

        ArmAnimSlotCatalog.HandClips hold = _armSlotCatalog != null ? _armSlotCatalog.HoldThin : null;
        ArmAnimSlotCatalog.HandClips aim = _armSlotCatalog != null ? _armSlotCatalog.AimThin : null;
        ArmAnimSlotCatalog.HandClips attack = _armSlotCatalog != null ? _armSlotCatalog.AttackThin : null;
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

        if (OwnsAnimancerArmImpact)
        {
            CombatLeaf action = _attacker != null ? _attacker.SelectedLeaf : CombatLeaf.Strike;
            WieldHand hand = _attacker != null ? _attacker.ActiveWieldHand : WieldHand.Right;
            _speedImpactRecoil = SpeedOfClip(
                ArmImpactSlotResolver.ResolveImpactClip(
                    _armSlotCatalog, presentation, action, ArmImpactKind.Recoil, hand),
                presentation);
            _speedImpactBlocked = SpeedOfClip(
                ArmImpactSlotResolver.ResolveImpactClip(
                    _armSlotCatalog, presentation, action, ArmImpactKind.Blocked, hand),
                presentation);
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
            AdvanceAnimGraph(channelDelta);
            return;
        }

        float step = 1f / _poseRate;
        _poseAccum += channelDelta;
        int steps = 0;
        while (_poseAccum >= step && steps < MaxPoseStepsPerFrame)
        {
            _poseAccum -= step;
            AdvanceAnimGraph(step);
            steps++;
        }

        if (steps >= MaxPoseStepsPerFrame && _poseAccum >= step)
            _poseAccum %= step;
    }

    void AdvanceAnimGraph(float deltaTime)
    {
        if (_animancer != null)
        {
            EnsureAnimancerPlayableReady();
            _animancer.Evaluate(deltaTime);
            return;
        }

        _animator.Update(deltaTime);
    }

    bool OwnsAnimancerMove =>
        _animancer != null && _moveSet != null && _moveSet.IsConfigured;

    /// <summary>Work Animancer layer 8; Layers[7] Hybrid remnant stays weight 0 (non-SSOT, S7).</summary>
    bool WantsAnimancerArmImpact =>
        OwnsAnimancerMove
        && _armSlotCatalog != null
        && _rightArmMask != null
        && _leftArmMask != null
        && _moveUpperBodyMask != null;

    bool OwnsAnimancerArmImpact =>
        WantsAnimancerArmImpact && _armImpactAnimancer.IsReady;

    bool WantsAnimancerHurt =>
        WantsAnimancerArmImpact
        && _headTorsoMask != null
        && _hitFlinchClip != null
        && _hitStaggerClip != null
        && _hitPainDownClip != null
        && _hitDeadClip != null;

    /// <summary>Flinch/Hurt Animancer ownership.</summary>
    public bool OwnsAnimancerHurt =>
        WantsAnimancerHurt && _hurtAnimancer.IsReady;

    /// <summary>Work Animancer when Move graph is owned (same Animancer Evaluate path).</summary>
    bool WantsAnimancerWork => OwnsAnimancerMove;

    /// <summary>Work Animancer ownership.</summary>
    public bool OwnsAnimancerWork =>
        WantsAnimancerWork && _workAnimancer.IsReady;

    int AnimancerDynamicMaskLayerIndex =>
        WantsAnimancerWork
            ? CharacterLocomotionWorkAnimancer.LayerWork
            : AnimancerSpacerLayerIndex;

    void EnsureAnimancerPlayableReady()
    {
        if (_animancer == null || _ensuringAnimancerGraph)
            return;

        if (_animancer.Animator == null && _animator != null)
            _animancer.Animator = _animator;

        _ensuringAnimancerGraph = true;
        try
        {
            if (OwnsAnimancerMove)
            {
                EnsureAnimancerMoveGraph();
                if (!_animancerMoveReady)
                {
                    Debug.LogError(
                        $"[CharacterLocomotionAnim] Animancer Move not ready on '{name}'.",
                        this);
                }
            }

            if (_animancer.IsGraphInitialized && _animancer.Graph.IsGraphPlaying)
                _animancer.Graph.PauseGraph();
        }
        finally
        {
            _ensuringAnimancerGraph = false;
        }
    }

    void EnsureAnimancerMoveGraph()
    {
        if (_animancer == null || !OwnsAnimancerMove)
            return;

        if (_moveMixerTransition == null)
            _moveMixerTransition = new MixerTransition2D();

        if (!_moveSet.TryConfigureMixer(_moveMixerTransition))
        {
            _animancerMoveReady = false;
            _armImpactAnimancer.Invalidate();
            _hurtAnimancer.Invalidate();
            _workAnimancer.Invalidate();
            Debug.LogError(
                $"[CharacterLocomotionAnim] MoveSet not configured on '{name}'. Falling back to Mecanim.",
                this);
            return;
        }

        AnimancerLayer moveLayer = _animancer.Layers[AnimancerMoveLayerIndex];
        moveLayer.SetDebugName("Animancer Move");

        if (_moveMixerState == null || !_moveMixerState.IsValid() || _moveMixerState.Layer != moveLayer)
            _moveMixerState = (Vector2MixerState)moveLayer.Play(_moveMixerTransition);
        else if (!_moveTransitions.IsActive && moveLayer.CurrentState != _moveMixerState)
            moveLayer.Play(_moveMixerState);

        moveLayer.Weight = 1f;

        bool wantArmImpact = WantsAnimancerArmImpact;
        bool hurtWasReady = _hurtAnimancer.IsReady;

        if (wantArmImpact)
        {
            _armImpactAnimancer.Ensure(_animancer, _rightArmMask, _leftArmMask, _moveUpperBodyMask);
            if (WantsAnimancerHurt)
                _hurtAnimancer.Ensure(_animancer, _headTorsoMask);
            else
                _hurtAnimancer.Invalidate();
        }
        else
        {
            _armImpactAnimancer.Invalidate();
            _hurtAnimancer.Invalidate();
            KeepUnusedLayerInert();
        }

        if (WantsAnimancerWork)
            _workAnimancer.Ensure(_animancer);
        else
            _workAnimancer.Invalidate();

        _animancerMoveReady = _moveMixerState != null && _moveMixerState.IsValid();
        if (!_animancerMoveReady)
        {
            Debug.LogError(
                $"[CharacterLocomotionAnim] Animancer Move Play failed on '{name}'. Falling back to Mecanim.",
                this);
            return;
        }

        ApplyAnimancerMoveWeightImmediate(Mathf.Max(_animancerMoveWeightTarget, 1f));
        ForceMecanimMoveLayerWeightZero();
        if (OwnsAnimancerArmImpact)
            ForceMecanimArmImpactLayerWeightsZero();
        if (OwnsAnimancerHurt)
        {
            ForceMecanimFlinchHurtLayerWeightsZero();
            if (!hurtWasReady)
                this.GetBodyRefs()?.HitReact?.RefreshAnimatorHurtBinding();
        }
        if (OwnsAnimancerWork)
        {
            ForceMecanimWorkLayerWeightZero();
            RefreshWorkLayerIndex();
        }

        KeepUnusedLayerInert();
    }

    void SyncAnimancerMove(float moveX, float moveZ, float channelDelta)
    {
        EnsureAnimancerPlayableReady();
        if (!_animancerMoveReady || _moveMixerState == null)
            return;

        ForceMecanimMoveLayerWeightZero();
        if (OwnsAnimancerArmImpact)
            ForceMecanimArmImpactLayerWeightsZero();
        if (OwnsAnimancerHurt)
            ForceMecanimFlinchHurtLayerWeightsZero();
        if (OwnsAnimancerWork)
            ForceMecanimWorkLayerWeightZero();
        CharacterMotor motor = _locomotion as CharacterMotor;
        bool allowTransitions = _characterState != null && motor != null
            && !_characterState.IsAiming && !_characterState.IsStealth
            && !_characterState.IsSwimming && !_characterState.IsDiving
            && !motor.IsMoveInhibited && !motor.IsStuck && !motor.IsAirborne
            && (_vaultHost == null || !_vaultHost.IsBusy)
            && _hurtWeightTarget <= 0.01f
            && !_workAnimancer.IsWeightActive(_animancer);
        _moveTransitions.Tick(_animancer.Layers[AnimancerMoveLayerIndex], _moveMixerState,
            _moveSet, _locomotionFacing, motor != null && motor.Mover != null ? motor.Mover.WorldMoveDir : Vector3.zero,
            motor != null && motor.IsSprinting, allowTransitions, channelDelta,
            GetFootOffset(), _leftFoot != null && _rightFoot != null);
        _allowLocomotionLean = allowTransitions && !_moveTransitions.IsActive;

        _locomotionFacing?.TickFacing(channelDelta);
        ResolveFacingMoveXZ(ResolveNormalizedSpeed(), out moveX, out moveZ);
        Vector2 targetMove = new Vector2(moveX, moveZ);
        _moveMixerState.Parameter = channelDelta <= 0f || _moveSet.ParameterSmoothTime <= 0f
            ? targetMove : Vector2.Lerp(_moveMixerState.Parameter, targetMove,
                1f - Mathf.Exp(-channelDelta / _moveSet.ParameterSmoothTime));

        bool stopped = (moveX * moveX) + (moveZ * moveZ) <= MoveStoppedEpsilonSqr;
        // Dynamic Layers: Move base stays full-body (feet). Overlay host uses UpperBody while moving.
        ApplyAnimancerMoveDynamicMask(stopped);

        bool vaultBusy = _vaultHost != null && _vaultHost.IsBusy;
        _animancerMoveWeightTarget = vaultBusy ? 0f : 1f;
        SetAnimancerMoveWeightToward(_animancerMoveWeightTarget, channelDelta);
    }

    void SyncOverlayLayerWeightAfterArms()
    {
        if (!_animancerMoveReady)
            return;

        float moveX = _moveMixerState != null ? _moveMixerState.Parameter.x : 0f;
        float moveZ = _moveMixerState != null ? _moveMixerState.Parameter.y : 0f;
        bool stopped = (moveX * moveX) + (moveZ * moveZ) <= MoveStoppedEpsilonSqr;
        ApplyAnimancerMoveDynamicMask(stopped);
        SyncOverlayLayerWeight(stopped);
    }

    void ApplyAnimancerMoveDynamicMask(bool stopped)
    {
        if (_animancer == null || !_animancer.IsGraphInitialized)
            return;

        // Never assign Mask=null — Animancer default-mask path can throw ArgumentNullException
        // and abort Update before Evaluate → permanent T-pose.
        // Moving: UpperBody on Work/overlay host. Idle: leave mask as-is (host weight usually 0).
        AnimancerLayer maskLayer = _animancer.Layers[AnimancerDynamicMaskLayerIndex];
        bool wantUpper = !stopped && _moveUpperBodyMask != null;
        bool fullBodyOverride = _hurtWeightTarget > 0.01f || _impactWeightTarget > 0.01f;
        if (fullBodyOverride)
            wantUpper = false;

        if (!wantUpper)
        {
            _moveLayerUsesUpperMask = false;
            return;
        }

        if (_moveLayerUsesUpperMask && maskLayer.Mask == _moveUpperBodyMask)
            return;

        maskLayer.Mask = _moveUpperBodyMask;
        _moveLayerUsesUpperMask = true;
    }

    void SyncOverlayLayerWeight(bool _)
    {
        if (_animancer == null || !_animancer.IsGraphInitialized)
            return;

        KeepUnusedLayerInert();
        if (OwnsAnimancerWork)
            ForceMecanimWorkLayerWeightZero();
    }

    /// <summary>Layers[7] unused spacer — weight 0.</summary>
    void KeepUnusedLayerInert()
    {
        if (_animancer == null || !_animancer.IsGraphInitialized)
            return;

        AnimancerLayer spacer = _animancer.Layers[AnimancerSpacerLayerIndex];
        spacer.SetDebugName("Unused layer spacer");
        if (!Mathf.Approximately(spacer.Weight, 0f))
            spacer.Weight = 0f;
    }

    void ForceMecanimMoveLayerWeightZero()
    {
        // Controller cleared under Animancer Move — no Mecanim layers to zero.
        if (OwnsAnimancerMove || !HasMecanimController())
        {
            _moveLayerIndex = -1;
            return;
        }

        if (_moveLayerIndex < 0)
            return;

        if (!Mathf.Approximately(AnimGetLayerWeight(_moveLayerIndex), 0f))
            AnimSetLayerWeight(_moveLayerIndex, 0f);
    }

    void ForceMecanimArmImpactLayerWeightsZero()
    {
        if (OwnsAnimancerMove || !HasMecanimController())
        {
            _rightArmLayerIndex = -1;
            _leftArmLayerIndex = -1;
            _twoHandLayerIndex = -1;
            _impactLayerIndex = -1;
            return;
        }

        if (_rightArmLayerIndex >= 0 && !Mathf.Approximately(AnimGetLayerWeight(_rightArmLayerIndex), 0f))
            AnimSetLayerWeight(_rightArmLayerIndex, 0f);
        if (_leftArmLayerIndex >= 0 && !Mathf.Approximately(AnimGetLayerWeight(_leftArmLayerIndex), 0f))
            AnimSetLayerWeight(_leftArmLayerIndex, 0f);
        if (_twoHandLayerIndex >= 0 && !Mathf.Approximately(AnimGetLayerWeight(_twoHandLayerIndex), 0f))
            AnimSetLayerWeight(_twoHandLayerIndex, 0f);
        if (_impactLayerIndex >= 0 && !Mathf.Approximately(AnimGetLayerWeight(_impactLayerIndex), 0f))
            AnimSetLayerWeight(_impactLayerIndex, 0f);
    }

    void ForceMecanimFlinchHurtLayerWeightsZero()
    {
        if (OwnsAnimancerMove || !HasMecanimController())
        {
            _flinchLayerIndex = -1;
            _hurtLayerIndex = -1;
            return;
        }

        if (_flinchLayerIndex >= 0 && !Mathf.Approximately(AnimGetLayerWeight(_flinchLayerIndex), 0f))
            AnimSetLayerWeight(_flinchLayerIndex, 0f);
        if (_hurtLayerIndex >= 0 && !Mathf.Approximately(AnimGetLayerWeight(_hurtLayerIndex), 0f))
            AnimSetLayerWeight(_hurtLayerIndex, 0f);
    }

    void ForceMecanimWorkLayerWeightZero()
    {
        if (OwnsAnimancerMove || !HasMecanimController())
        {
            _mecanimWorkLayerIndex = -1;
            return;
        }

        if (_mecanimWorkLayerIndex < 0)
            _mecanimWorkLayerIndex = AnimGetLayerIndex(CharacterWorkLayerAnim.LayerName);
        if (_mecanimWorkLayerIndex < 0)
            return;

        if (!Mathf.Approximately(AnimGetLayerWeight(_mecanimWorkLayerIndex), 0f))
            AnimSetLayerWeight(_mecanimWorkLayerIndex, 0f);
    }

    void SetAnimancerMoveWeightToward(float target, float channelDelta)
    {
        if (_animancer == null || !_animancer.IsGraphInitialized)
            return;

        AnimancerLayer moveLayer = _animancer.Layers[AnimancerMoveLayerIndex];
        float current = moveLayer.Weight;
        if (_layerBlendSpeed <= 0f || channelDelta <= 0f)
        {
            if (!Mathf.Approximately(current, target))
                ApplyAnimancerMoveWeightImmediate(target);
            return;
        }

        float next = Mathf.MoveTowards(current, target, _layerBlendSpeed * channelDelta);
        if (!Mathf.Approximately(current, next))
            ApplyAnimancerMoveWeightImmediate(next);
    }

    void ApplyAnimancerMoveWeightImmediate(float weight)
    {
        if (_animancer == null || !_animancer.IsGraphInitialized)
            return;

        AnimancerLayer moveLayer = _animancer.Layers[AnimancerMoveLayerIndex];
        moveLayer.Weight = weight;
    }

    void AnimSetFloat(int id, float value)
    {
        if (!HasMecanimController())
            return;
        _animator.SetFloat(id, value);
    }

    void AnimSetBool(int id, bool value)
    {
        if (!HasMecanimController())
            return;
        _animator.SetBool(id, value);
    }

    void AnimSetTrigger(int id)
    {
        if (!HasMecanimController())
            return;
        _animator.SetTrigger(id);
    }

    void AnimSetLayerWeight(int layerIndex, float weight)
    {
        if (!HasMecanimController() || layerIndex < 0)
            return;
        _animator.SetLayerWeight(layerIndex, weight);
    }

    float AnimGetLayerWeight(int layerIndex)
    {
        if (!HasMecanimController() || layerIndex < 0)
            return 0f;
        return _animator.GetLayerWeight(layerIndex);
    }

    AnimatorStateInfo AnimGetCurrentAnimatorStateInfo(int layerIndex)
    {
        if (!HasMecanimController() || layerIndex < 0)
            return default;
        return _animator.GetCurrentAnimatorStateInfo(layerIndex);
    }

    AnimatorStateInfo AnimGetNextAnimatorStateInfo(int layerIndex)
    {
        if (!HasMecanimController() || layerIndex < 0)
            return default;
        return _animator.GetNextAnimatorStateInfo(layerIndex);
    }

    bool AnimIsInTransition(int layerIndex)
    {
        if (!HasMecanimController() || layerIndex < 0)
            return false;
        return _animator.IsInTransition(layerIndex);
    }

    bool AnimGetBool(int id)
    {
        if (!HasMecanimController())
            return false;
        return _animator.GetBool(id);
    }


    int AnimGetLayerIndex(string layerName)
    {
        if (string.IsNullOrEmpty(layerName))
            return -1;

        // Animancer owns Move — Mecanim layer names are not addressable.
        if (OwnsAnimancerMove)
            return -1;

        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            RuntimeAnimatorController c = ActiveController ?? _defaultController;
            if (c is AnimatorOverrideController o)
                c = o.runtimeAnimatorController;
            if (c is UnityEditor.Animations.AnimatorController ac)
            {
                for (int i = 0; i < ac.layers.Length; i++)
                {
                    if (ac.layers[i].name == layerName)
                        return i;
                }
            }
#endif
            return -1;
        }

        if (!HasMecanimController())
            return -1;
        return _animator.GetLayerIndex(layerName);
    }

    bool HasMecanimController() =>
        _animator != null && _animator.runtimeAnimatorController != null;

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

        if (_animator == null || ActiveController == null)
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

        if (!TryGetAnimatorParameters(out AnimatorControllerParameter[] parameters))
            return;

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
            _rightArmLayerIndex = AnimGetLayerIndex(_rightArmLayerName);
        if (!string.IsNullOrEmpty(_leftArmLayerName))
            _leftArmLayerIndex = AnimGetLayerIndex(_leftArmLayerName);
        if (!string.IsNullOrEmpty(_twoHandLayerName))
            _twoHandLayerIndex = AnimGetLayerIndex(_twoHandLayerName);
        _impactLayerIndex = AnimGetLayerIndex(ImpactLayerName);
        _flinchLayerIndex = AnimGetLayerIndex(CharacterHitReact.FlinchLayerName);
        _hurtLayerIndex = AnimGetLayerIndex(CharacterHitReact.HurtLayerName);
        _moveLayerIndex = AnimGetLayerIndex(MecanimMoveLayerName);
    }

    bool TryGetAnimatorParameters(out AnimatorControllerParameter[] parameters)
    {
        parameters = null;

        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            parameters = _animator.parameters;
            return true;
        }

#if UNITY_EDITOR
        RuntimeAnimatorController active = ActiveController ?? _defaultController;
        if (active is AnimatorOverrideController overrideController)
            active = overrideController.runtimeAnimatorController;
        if (active is UnityEditor.Animations.AnimatorController editorController)
        {
            parameters = editorController.parameters;
            return true;
        }
#endif
        return false;
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
        _animancerMoveWeightTarget = 0f;
        SetAnimancerMoveWeightToward(0f, channelDelta);
        SetLayerWeightToward(_moveLayerIndex, 0f, channelDelta);
        if (OwnsAnimancerArmImpact)
        {
            SetAnimancerArmLayerWeightToward(
                CharacterLocomotionArmImpactAnimancer.LayerRightArm, 0f, channelDelta);
            SetAnimancerArmLayerWeightToward(
                CharacterLocomotionArmImpactAnimancer.LayerLeftArm, 0f, channelDelta);
            SetAnimancerArmLayerWeightToward(
                CharacterLocomotionArmImpactAnimancer.LayerTwoHand, 0f, channelDelta);
            SetAnimancerArmLayerWeightToward(
                CharacterLocomotionArmImpactAnimancer.LayerImpact, 0f, channelDelta);
            ForceMecanimArmImpactLayerWeightsZero();
            return;
        }

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
            // Empty both hands = unarmed Presentation. ActiveWieldHand still drives Attack overlay.
            if (!twoHand && !leftArmed && !rightArmed)
                ApplyActiveWieldHandFlags(ref twoHand, ref leftArmed, ref rightArmed);
        }
        else if (_attacker != null)
        {
            ApplyActiveWieldHandFlags(ref twoHand, ref leftArmed, ref rightArmed);
        }

        ResolveHandActions(out CombatLeaf actionL, out CombatLeaf actionR, out CombatLeaf action2H);
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

        if (OwnsAnimancerArmImpact)
        {
            SetAnimancerArmLayerWeightToward(
                CharacterLocomotionArmImpactAnimancer.LayerRightArm, rightTarget, channelDelta);
            SetAnimancerArmLayerWeightToward(
                CharacterLocomotionArmImpactAnimancer.LayerLeftArm, leftTarget, channelDelta);
            SetAnimancerArmLayerWeightToward(
                CharacterLocomotionArmImpactAnimancer.LayerTwoHand, twoHandTarget, channelDelta);
            ForceMecanimArmImpactLayerWeightsZero();
            return;
        }

        SetLayerWeightToward(_rightArmLayerIndex, rightTarget, channelDelta);
        SetLayerWeightToward(_leftArmLayerIndex, leftTarget, channelDelta);
        SetLayerWeightToward(_twoHandLayerIndex, twoHandTarget, channelDelta);
    }

    void SetAnimancerArmLayerWeightToward(int animancerLayerIndex, float target, float channelDelta)
    {
        if (_animancer == null || !_animancer.IsGraphInitialized)
            return;

        float current = _animancer.Layers[animancerLayerIndex].Weight;
        if (_layerBlendSpeed <= 0f || channelDelta <= 0f)
        {
            if (!Mathf.Approximately(current, target))
                _animancer.Layers[animancerLayerIndex].Weight = target;
            return;
        }

        float next = Mathf.MoveTowards(current, target, _layerBlendSpeed * channelDelta);
        if (!Mathf.Approximately(current, next))
            _animancer.Layers[animancerLayerIndex].Weight = next;
    }

    void SyncAnimancerArmPoses(
        WeaponPresentation presentationL,
        WeaponPresentation presentationR,
        WeaponPresentation presentation2H,
        CombatLeaf actionL,
        CombatLeaf actionR,
        CombatLeaf action2H)
    {
        if (!OwnsAnimancerArmImpact)
            return;

        bool isAiming = _characterState != null && _characterState.IsAiming;
        SyncOneAnimancerArmPose(
            WieldHand.Right, presentationR, actionR, isAiming, _mappedSurpriseR, ref _pendingAnimancerAttackR,
            _speedHoldR, _speedAimR, _speedAttackR);
        SyncOneAnimancerArmPose(
            WieldHand.Left, presentationL, actionL, isAiming, _mappedSurpriseL, ref _pendingAnimancerAttackL,
            _speedHoldL, _speedAimL, _speedAttackL);
        SyncOneAnimancerArmPose(
            WieldHand.TwoHand, presentation2H, action2H, isAiming, _mappedSurprise2H, ref _pendingAnimancerAttack2H,
            _speedHold2H, _speedAim2H, _speedAttack2H);
        _armImpactAnimancer.TickAttackExits();
    }

    void SyncOneAnimancerArmPose(
        WieldHand hand,
        WeaponPresentation presentation,
        CombatLeaf action,
        bool isAiming,
        bool surprise,
        ref bool pendingAttackRestart,
        float speedHold,
        float speedAim,
        float speedAttack)
    {
        bool restart = pendingAttackRestart;
        pendingAttackRestart = false;

        bool wantAttack = restart
            || HasAttackOverlayLatch(hand)
            || HasQueuedAttack(hand)
            || _armImpactAnimancer.IsInAttack(hand);

        ArmAnimSlotResolver.PoseKind pose;
        float speed;
        if (wantAttack)
        {
            pose = ArmAnimSlotResolver.PoseKind.Attack;
            speed = speedAttack;
        }
        else if (isAiming)
        {
            pose = ArmAnimSlotResolver.PoseKind.Aim;
            speed = speedAim;
        }
        else
        {
            pose = ArmAnimSlotResolver.PoseKind.Hold;
            speed = speedHold;
        }

        // useHold gate: weight already 0 from SyncArmLayerWeights; still keep a Hold clip ready.
        AnimationClip clip = ArmAnimSlotResolver.ResolvePoseClip(
            _armSlotCatalog,
            presentation,
            action,
            hand,
            pose,
            useSurpriseAttack: pose == ArmAnimSlotResolver.PoseKind.Attack && surprise);
        if (clip == null)
            return;

        _armImpactAnimancer.SyncPose(
            _animancer,
            hand,
            clip,
            speed,
            pose,
            restartAttack: restart);
    }

    float ArmOverlayWeight(
        bool armed,
        WieldHand hand,
        WeaponPresentation presentation,
        CombatLeaf action,
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

        // Animancer: never raise mask weight without a resolvable clip (empty layer → bind/T-pose).
        if (OwnsAnimancerArmImpact
            && _armSlotCatalog != null
            && ArmAnimSlotResolver.ResolvePoseClip(
                _armSlotCatalog, presentation, action, hand, ArmAnimSlotResolver.PoseKind.Hold) == null)
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
        if (OwnsAnimancerArmImpact)
            return _armImpactAnimancer.IsInAttack(hand);

        int layerIndex = hand == WieldHand.Left
            ? _leftArmLayerIndex
            : hand == WieldHand.TwoHand
                ? _twoHandLayerIndex
                : _rightArmLayerIndex;
        if (layerIndex < 0 || _animator == null)
            return false;

        AnimatorStateInfo current = AnimGetCurrentAnimatorStateInfo(layerIndex);
        if (current.shortNameHash == _hashAttackState)
            return true;
        if (!AnimIsInTransition(layerIndex))
            return false;
        AnimatorStateInfo next = AnimGetNextAnimatorStateInfo(layerIndex);
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
                    return CombatLeafRows.Resolve(catalog, stack);
            }
            else if (hand == WieldHand.Left && gear.Wield.Left != null)
                return CombatLeafRows.Resolve(catalog, gear.Wield.Left);
            else if (hand == WieldHand.Right && gear.Wield.Right != null)
                return CombatLeafRows.Resolve(catalog, gear.Wield.Right);
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

        float current = AnimGetLayerWeight(layerIndex);
        if (_layerBlendSpeed <= 0f || channelDelta <= 0f)
        {
            if (!Mathf.Approximately(current, target))
                AnimSetLayerWeight(layerIndex, target);
            return;
        }

        float next = Mathf.MoveTowards(current, target, _layerBlendSpeed * channelDelta);
        if (!Mathf.Approximately(current, next))
            AnimSetLayerWeight(layerIndex, next);
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

    void ResolveHandActions(out CombatLeaf actionL, out CombatLeaf actionR, out CombatLeaf action2H)
    {
        actionL = CombatLeaf.Strike;
        actionR = CombatLeaf.Strike;
        action2H = _attacker != null ? _attacker.SelectedLeaf : CombatLeaf.Strike;

        CharacterGearService gear = _gearHost != null ? _gearHost.Service : null;
        if (gear?.Wield == null)
        {
            if (_attacker != null)
            {
                actionL = _attacker.SelectedLeaf;
                actionR = _attacker.SelectedLeaf;
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

    static CombatLeaf ActionForStack(
        WeaponPresentationCatalog catalog,
        ItemStack stack,
        CombatLeaf fallback)
    {
        if (stack?.Item == null)
            return fallback;

        WeaponPresentation presentation = CombatLeafRows.Resolve(catalog, stack);
        return CombatLeafRows.ResolveSelected(stack.Instance, presentation);
    }

    void TakeManualControl()
    {
        if (_animator == null)
            return;

        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        if (_animancer != null)
        {
            // Evaluate only writes humanoid bones while Animator.enabled=true (verified Play).
            // Keep Animator on; PauseGraph in EnsureAnimancerPlayableReady blocks PlayableGraph auto-tick.
            // Do not Rebind after Hybrid.OnEnable — that unbinds AnimationPlayableOutput (T-pose).
            _animator.enabled = true;
            if (!_animancer.IsGraphInitialized)
                _animator.Rebind();

            EnsureAnimancerPlayableReady();
            if (_animancerMoveReady && _animancer.IsGraphInitialized)
            {
                AnimancerLayer moveLayer = _animancer.Layers[AnimancerMoveLayerIndex];
                if (moveLayer.Weight < 1f)
                    moveLayer.Weight = 1f;
            }

            _animancer.Evaluate(0f);
        }
        else
        {
            // Mecanim-only: disable auto-update and drive via Animator.Update(channelDelta).
            _animator.enabled = true;
            _animator.Update(0f);
            _animator.enabled = false;
            _animator.Rebind();
            _animator.Update(0f);
        }

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

    Vector2 GetFootOffset()
    {
        if (_leftFoot == null || _rightFoot == null || _animator == null) return Vector2.zero;
        Vector3 difference = _animator.transform.InverseTransformVector(_leftFoot.position - _rightFoot.position);
        return new Vector2(difference.z, difference.y);
    }

    void RestoreTurnLean()
    {
        if (_leanApplied && _leanSpine != null
            && Mathf.Abs(Quaternion.Dot(_leanSpine.localRotation, _spineAfterLean)) > 0.99999f)
            _leanSpine.localRotation = _spineBeforeLean;
        _leanApplied = false;
    }

    void ApplyTurnLean(float delta)
    {
        if (_leanSpine == null || _locomotionFacing == null) return;
        if (!_allowLocomotionLean)
        {
            _turnLean = 0f;
            return;
        }

        float target = -Mathf.Clamp(_locomotionFacing.AngularVelocity / 360f, -1f, 1f)
            * _maxTurnLean * Mathf.Clamp01(ResolveNormalizedSpeed() * 2f);
        _turnLean = Mathf.Lerp(_turnLean, target, 1f - Mathf.Exp(-delta / _turnLeanSmoothTime));
        _spineBeforeLean = _leanSpine.localRotation;
        // Only lateral spine lean; no extra root rotation or chest yaw competing with aim.
        _leanSpine.rotation = Quaternion.AngleAxis(_turnLean, _animator.transform.forward) * _leanSpine.rotation;
        _spineAfterLean = _leanSpine.localRotation;
        _leanApplied = true;
    }

    void ResolveFacingMoveXZ(float speedNorm, out float moveX, out float moveZ)
    {
        moveX = 0f;
        moveZ = 0f;

        if (speedNorm <= 1e-4f || _characterState == null)
            return;

        // Free locomotion turns the body and keeps a forward gait. Strafing belongs to aim mode.
        if (!_characterState.IsAiming && !_characterState.IsSwimming && !_characterState.IsDiving)
        {
            moveZ = speedNorm;
            return;
        }

        Vector3 wish = _characterState.MoveDir;
        wish.y = 0f;
        if (wish.sqrMagnitude <= MoveDirEpsilonSqr)
            return;

        Vector3 facing = _locomotionFacing != null
            ? _locomotionFacing.BodyFacingDir
            : _characterState.GetFacingDir();
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
