// ============================================================
// CharacterMotor — 캐릭터 공용 캡슐 locomotion (possessed=Player 채널, 아니면 World)
// ============================================================

using IsoTilemap;
using UnityEngine;

public interface ICharacterMotorDrive
{
    float AnimSpeedReference { get; }
    Vector3 CalcDesiredMove(KinematicMover mover, float deltaTime);
    void AfterMove(CharacterMotor motor);
}

[DefaultExecutionOrder(-40)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(CharacterState))]
public sealed class CharacterMotor : MonoBehaviour, ICharacterLocomotion
{
    [Header("Movement")]
    [SerializeField, Min(0f)] float _moveSpeed = CharacterLocomotionDefaults.DefaultWalkSpeedMeters;
    [SerializeField, Min(0f)] float _runMaxSpeedMeters = CharacterLocomotionDefaults.DefaultRunMaxSpeedMeters;
    [SerializeField] MovementStyle _activeStyle;

    [Header("Collision")]
    [SerializeField] CharacterLocomotionCollisionSettings _collision =
        CharacterLocomotionCollisionSettings.Default;

    readonly RaycastHit[] _hits =
        new RaycastHit[CharacterLocomotionDefaults.HitBufferSize];

    Rigidbody _rigidbody;
    CapsuleCollider _capsule;
    CharacterState _characterState;
    KinematicMover _mover;
    CharacterLocomotion _locomotion;
    MapCollisionServices _pendingMapCollision;
    ICharacterMotorDrive _drive;
    CharacterHitStopState _hitStop;
    bool _possessed;
    int _scriptedLocomotionDepth;
    bool _hasTravelLimit;
    float _remainingTravelDistance;
    float _envSpeedMultiplier = 1f;
    float _imbalanceSpeedMultiplier = 1f;
    float _swimSpeedMultiplier = 1f;
    float _serializedWalkSpeed;
    float _serializedRunMaxSpeedMeters;
    Vector3 _knockbackVelocity;
    float _staggerRemaining;
    bool _moveLocked;

    public bool IsStaggered => _staggerRemaining > 0f;
    public bool IsMoveLocked => _moveLocked;
    public bool IsMoveInhibited => _moveLocked || _staggerRemaining > 0f;
    public Vector3 KnockbackVelocity => _knockbackVelocity;

    public bool IsPossessed => _possessed;
    public bool IsScriptedLocomotion => _scriptedLocomotionDepth > 0;
    public bool IsStuck => _locomotion != null && _locomotion.IsStuck;
    public float CurrentSpeed => _mover != null ? _mover.CurrentSpeed : 0f;
    public float AnimSpeedReference
    {
        get
        {
            float runRef = _runMaxSpeedMeters > Mathf.Epsilon
                ? _runMaxSpeedMeters
                : CharacterLocomotionDefaults.DefaultRunMaxSpeedMeters;
            return Mathf.Max(runRef, _moveSpeed + 0.01f);
        }
    }
    public MovementStyle ActiveStyle => _activeStyle;
    public float SerializedWalkSpeed => _serializedWalkSpeed;
    public float RunMaxSpeedMeters => _runMaxSpeedMeters;
    public KinematicMover Mover => _mover;
    public CapsuleCollider Capsule => _capsule;
    public RaycastHit[] Hits => _hits;
    public int LastHitCount => _locomotion != null ? _locomotion.LastHitCount : 0;
    public Vector3 LastP1 =>
        _locomotion != null ? _locomotion.LastCapsulePoint : Vector3.zero;
    public Vector3 LastDesiredMove =>
        _locomotion != null ? _locomotion.LastDesiredMove : Vector3.zero;
    public Vector3 LastAppliedDelta { get; private set; }
    public float BaseSkin => _collision.BaseSkin;
    public int LastNearestIndex => _mover != null ? _mover.LastNearestIndex : -1;
    public Vector3 LastSlide => _mover != null ? _mover.LastSlide : Vector3.zero;
    public bool IsSprinting => _mover != null && _mover.IsSprinting;
    public bool IsInertiaActive => _mover != null && _mover.IsInertiaActive;
    public bool LastPhysicsStuck => _locomotion != null && _locomotion.LastPhysicsStuck;
    public MapTopologyDepenetration.PushOutResult LastTopologyPush =>
        _locomotion != null
            ? _locomotion.LastTopologyPush
            : MapTopologyDepenetration.PushOutResult.None;

    float EffectiveMoveSpeed =>
        _activeStyle != null && !_activeStyle.UsesCharacterWalkSpeed
            ? _activeStyle.MoveSpeed
            : _moveSpeed;

    public float BaseWalkSpeed => EffectiveMoveSpeed;

    void Awake()
    {
        _serializedWalkSpeed = Mathf.Max(0f, _moveSpeed);
        _serializedRunMaxSpeedMeters = Mathf.Max(
            0f,
            _runMaxSpeedMeters > 0f
                ? _runMaxSpeedMeters
                : CharacterLocomotionDefaults.DefaultRunMaxSpeedMeters);
        _runMaxSpeedMeters = _serializedRunMaxSpeedMeters;
        NormalizeRunMaxSpeedMeters();
        _rigidbody = GetComponent<Rigidbody>();
        _capsule = GetComponent<CapsuleCollider>();
        _characterState = GetComponent<CharacterState>();

        _rigidbody.freezeRotation = true;
        _rigidbody.useGravity = false;

        _mover = new KinematicMover
        {
            BaseSkin = _collision.BaseSkin,
            CollisionMask = _collision.CollisionMask,
            TriggerInteraction = _collision.TriggerInteraction,
        };

        _locomotion = new CharacterLocomotion(
            _rigidbody,
            _capsule,
            transform,
            _characterState,
            _mover,
            _hits,
            _collision.ClimbAllowance,
            _collision.BaseSkin,
            _collision.CollisionMask,
            _collision.TriggerInteraction,
            _collision.LogicalGravity,
            _collision.TopologyPushSpeed,
            _collision.TopologyPushMaxIterations);
        _locomotion.BindMapCollision(_pendingMapCollision);
        _possessed = false;
        _hitStop = CharacterHitStopState.Find(this);
    }

    void FixedUpdate()
    {
        // 할당 없음. _hits는 CapsuleCastNonAlloc 재사용.
        float deltaTime = TimeScaleService.FixedDelta(
            _possessed ? TimeScaleChannel.Player : TimeScaleChannel.World);
        if (_hitStop != null)
            deltaTime *= _hitStop.SimScale;
        if (deltaTime <= 0f)
            return;

        TickKnockback(deltaTime);

        Vector3 desiredMove;
        if (IsMoveInhibited)
        {
            desiredMove = _knockbackVelocity * deltaTime;
        }
        else if (_possessed && _drive != null && !IsScriptedLocomotion)
        {
            desiredMove = _drive.CalcDesiredMove(_mover, deltaTime)
                + _knockbackVelocity * deltaTime;
        }
        else
        {
            desiredMove = _mover.CalcConstantSpeedMove(
                EffectiveMoveSpeed
                    * _envSpeedMultiplier
                    * _imbalanceSpeedMultiplier
                    * _swimSpeedMultiplier,
                deltaTime)
                + _knockbackVelocity * deltaTime;
            if (_hasTravelLimit &&
                desiredMove.sqrMagnitude >
                _remainingTravelDistance * _remainingTravelDistance)
            {
                desiredMove = desiredMove.normalized * _remainingTravelDistance;
            }
        }

        LastAppliedDelta = _locomotion.Move(desiredMove, deltaTime);

        if ((!_possessed || IsScriptedLocomotion) && _hasTravelLimit)
        {
            _remainingTravelDistance = Mathf.Max(
                0f,
                _remainingTravelDistance - LastAppliedDelta.magnitude);
            if (_remainingTravelDistance <= Mathf.Epsilon)
                _mover.SetWorldDirection(Vector3.zero);
        }

        if (_possessed && !IsScriptedLocomotion)
            _drive?.AfterMove(this);
    }

    public void BindDrive(ICharacterMotorDrive drive) => _drive = drive;

    public void ConfigureDriveMover(float acceleration, float inertia)
    {
        if (_mover == null)
            return;

        _mover.Acceleration = acceleration;
        _mover.Inertia = inertia;
    }

    public void SetPossessed(bool possessed)
    {
        _possessed = possessed;
        if (possessed && !IsScriptedLocomotion)
            ClearTravelLimit();
        if (TryGetComponent(out CharacterSwimHost swim))
            swim.NotifyPossessedChanged(possessed);
        else if (CharacterBodyResolve.TryGetInBody(this, out CharacterSwimHost bodySwim))
            bodySwim.NotifyPossessedChanged(possessed);

        if (TryGetComponent(out CharacterVaultHost vault))
            vault.NotifyPossessedChanged(possessed);
        else if (CharacterBodyResolve.TryGetInBody(this, out CharacterVaultHost bodyVault))
            bodyVault.NotifyPossessedChanged(possessed);
    }

    /// <summary>NpcSteer 등 스크립트 조향. possessed여도 NPC와 동일 등속·travel limit.</summary>
    public void BeginScriptedLocomotion()
    {
        _scriptedLocomotionDepth++;
    }

    public void EndScriptedLocomotion()
    {
        if (_scriptedLocomotionDepth <= 0)
            return;

        _scriptedLocomotionDepth--;
        if (_scriptedLocomotionDepth > 0)
            return;

        _mover?.SetWorldDirection(Vector3.zero);
        ClearTravelLimit();
    }

    public void BindMapCollision(MapCollisionServices services)
    {
        _pendingMapCollision = services;
        _locomotion?.BindMapCollision(services);
    }

    public void SetDesiredWorldDir(Vector3 worldDirXZ)
    {
        if (_mover == null || _moveLocked)
            return;

        _mover.SetWorldDirection(worldDirXZ);
        _characterState?.SetMoveDir(_mover.WorldMoveDir);
    }

    public void SetSpeed(float metersPerSecond) =>
        _moveSpeed = Mathf.Max(0f, metersPerSecond);

    /// <summary>애니 정규화 분모 SSOT. possessed <see cref="PlayerMovement"/>가 동일 비율로 동기화.</summary>
    public void SetAnimRunSpeedReference(float runMaxSpeedMeters)
    {
        _runMaxSpeedMeters = Mathf.Max(0f, runMaxSpeedMeters);
        NormalizeRunMaxSpeedMeters();
    }

    public void ApplyWalkSpeedFromDefinition(CharacterDefinition definition)
    {
        if (definition == null || definition.WalkSpeedMeters <= 0f)
            return;

        ApplyLocomotionWalkProfile(
            CharacterDefinition.ResolveWalkSpeedMeters(definition, _serializedWalkSpeed));
    }

    void ApplyLocomotionWalkProfile(float walkSpeedMeters)
    {
        _moveSpeed = Mathf.Max(0f, walkSpeedMeters);
        if (_serializedWalkSpeed <= Mathf.Epsilon)
        {
            NormalizeRunMaxSpeedMeters();
            return;
        }

        float ratio = _moveSpeed / _serializedWalkSpeed;
        _runMaxSpeedMeters = _serializedRunMaxSpeedMeters * ratio;
        NormalizeRunMaxSpeedMeters();
    }

    void NormalizeRunMaxSpeedMeters() =>
        _runMaxSpeedMeters = Mathf.Max(_runMaxSpeedMeters, _moveSpeed + 0.01f);

    /// <summary>Env 이동 배율 (GearEnv × limp). Possessed는 PlayerMovement.SetEnvMovement가 같은 값을 쓴다.</summary>
    public void SetEnvMovement(float speedMultiplier) =>
        _envSpeedMultiplier = Mathf.Max(0f, speedMultiplier);

    /// <summary>Wade/Swim/Dive 배율. CharacterSwimHost. Possessed는 PlayerMovement.SetSwimMovement.</summary>
    public void SetSwimMovement(float speedMultiplier) =>
        _swimSpeedMultiplier = Mathf.Max(0f, speedMultiplier);

    /// <summary>불균형 이동 배율 (1 − Imbalance). Possessed는 PlayerMovement.SetImbalanceMovement.</summary>
    public void SetImbalanceMovement(float speedMultiplier) =>
        _imbalanceSpeedMultiplier = Mathf.Max(0f, speedMultiplier);

    public void SetActiveMovementStyle(MovementStyle style)
    {
        _activeStyle = style;
        if (style != null && !style.UsesCharacterWalkSpeed)
            _moveSpeed = Mathf.Max(0f, style.MoveSpeed);
    }

    public void SetTravelLimit(float maxDistance)
    {
        _hasTravelLimit = true;
        _remainingTravelDistance = Mathf.Max(0f, maxDistance);
    }

    public void ClearTravelLimit()
    {
        _hasTravelLimit = false;
        _remainingTravelDistance = 0f;
    }

    /// <summary>월드 XZ 속도(m/s)를 넉백에 더함. 맵 Move로 소진.</summary>
    public void ApplyKnockback(Vector3 velocityXz)
    {
        velocityXz.y = 0f;
        _knockbackVelocity += velocityXz;
    }

    public void BeginStagger(float seconds)
    {
        if (seconds <= 0f)
            return;
        if (seconds > _staggerRemaining)
            _staggerRemaining = seconds;
        _mover?.SetWorldDirection(Vector3.zero);
    }

    public void SetMoveLocked(bool locked)
    {
        _moveLocked = locked;
        if (!locked)
            return;

        _mover?.SetWorldDirection(Vector3.zero);
        ClearTravelLimit();
        _characterState?.ClearMoveDir();
        _characterState?.ClearAim();
    }

    void TickKnockback(float deltaTime)
    {
        if (_staggerRemaining > 0f)
            _staggerRemaining = Mathf.Max(0f, _staggerRemaining - deltaTime);

        if (_knockbackVelocity.sqrMagnitude < 1e-8f)
        {
            _knockbackVelocity = Vector3.zero;
            return;
        }

        float decay = CombatImpulse.KnockbackDecayPerSecond * deltaTime;
        _knockbackVelocity = Vector3.Lerp(_knockbackVelocity, Vector3.zero, Mathf.Clamp01(decay));
        if (_knockbackVelocity.sqrMagnitude < 1e-6f)
            _knockbackVelocity = Vector3.zero;
    }
}
