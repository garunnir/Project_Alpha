// ============================================================
// CharacterLocomotionFacing — BodyFacing angular chase for root/MoveXZ
// ============================================================

using UnityEngine;

/// <summary>
/// Tracks BodyFacing from wish/<see cref="CharacterState.GetFacingDir"/>.
/// Aim snaps to Sight; <see cref="CharacterFacingRotator"/> and MoveXZ consume <see cref="BodyFacingDir"/>.
/// Upper/Chest bone lead intentionally not used (caused turn jitter).
/// </summary>
[RequireComponent(typeof(CharacterState))]
public sealed class CharacterLocomotionFacing : MonoBehaviour
{
    const float WishEpsilonSqr = 1e-6f;
    const float DefaultBodyTurnDegPerSec = 720f;

    [SerializeField] CharacterState _state;
    [SerializeField] CharacterMotor _motor;

    [Tooltip("Maximum body yaw speed (deg/sec). Larger direction changes turn faster.")]
    [SerializeField, Min(0f)] float _bodyTurnDegPerSec = DefaultBodyTurnDegPerSec;
    [SerializeField, Min(0f)] float _minimumTurnDegPerSec = 180f;
    [SerializeField, Min(0f)] float _turnAccelerationDegPerSec2 = 1800f;

    Vector3 _bodyFacingDir = Vector3.forward;
    bool _hasFacing;
    bool _animationTurn;
    float _currentTurnSpeed;
    CharacterLocomotionAnim _animationDriver;
    CharacterHitStopState _hitStop;
    Vector3 _lastTickFacing;
    public float AngularVelocity { get; private set; }

    public void SetAnimationTurn(Vector3 direction)
    {
        if (direction.sqrMagnitude <= WishEpsilonSqr) return;
        _bodyFacingDir = direction.normalized;
        _animationTurn = true;
        _currentTurnSpeed = 0f;
    }

    public void EndAnimationTurn() => _animationTurn = false;

    /// <summary>Smoothed body facing on XZ (aim = Sight snap).</summary>
    public Vector3 BodyFacingDir => _bodyFacingDir;

    /// <summary>Inspector SSOT: body turn deg/sec.</summary>
    public float BodyTurnDegPerSec => _bodyTurnDegPerSec;

    void Awake()
    {
        ResolveRefs();
        EnsureFacingSeeded();
    }

    void OnEnable()
    {
        ResolveRefs();
        EnsureFacingSeeded();
    }

    void Update()
    {
        // The animation host orders turn selection, facing and graph evaluation in one tick.
        if (_animationDriver != null && _animationDriver.isActiveAndEnabled
            && _animationDriver.Animator != null) return;
        TickFacing(ResolveDelta());
    }

    internal void TickFacing(float dt)
    {
        if (_state == null)
            return;

        EnsureFacingSeeded();

        if (dt <= 0f || (_hitStop != null && _hitStop.IsFrozen))
            return;

        if (_state.IsAiming)
        {
            SnapToSight();
            AngularVelocity = 0f;
            _lastTickFacing = _bodyFacingDir;
            return;
        }

        if (_animationTurn)
        {
            UpdateAngularVelocity(dt);
            return;
        }

        Vector3 wish = _state.GetFacingDir();
        wish.y = 0f;
        if (wish.sqrMagnitude <= WishEpsilonSqr)
        {
            AngularVelocity = 0f;
            _lastTickFacing = _bodyFacingDir;
            return;
        }

        wish.Normalize();
        float angle = Vector3.Angle(_bodyFacingDir, wish);
        float desiredSpeed = Mathf.Lerp(
            _minimumTurnDegPerSec,
            _bodyTurnDegPerSec,
            Mathf.SmoothStep(0f, 1f, angle / 180f));
        _currentTurnSpeed = Mathf.MoveTowards(
            _currentTurnSpeed,
            desiredSpeed,
            _turnAccelerationDegPerSec2 * dt);
        float remaining = Mathf.Max(0f, angle - 0.1f);
        float brakingSpeed = Mathf.Sqrt(2f * _turnAccelerationDegPerSec2 * remaining);
        float appliedSpeed = Mathf.Min(_currentTurnSpeed, brakingSpeed);
        _bodyFacingDir = RotateDirTowards(_bodyFacingDir, wish, appliedSpeed * dt);
        if (remaining <= 0.1f)
            _currentTurnSpeed = 0f;
        UpdateAngularVelocity(dt);
    }

    void UpdateAngularVelocity(float dt)
    {
        AngularVelocity = _lastTickFacing.sqrMagnitude > WishEpsilonSqr
            ? Vector3.SignedAngle(_lastTickFacing, _bodyFacingDir, Vector3.up) / dt : 0f;
        _lastTickFacing = _bodyFacingDir;
    }

    void ResolveRefs()
    {
        if (_state == null)
            _state = CharacterBodyResolve.GetInBody<CharacterState>(this);
        if (_motor == null)
            _motor = CharacterBodyResolve.GetInBody<CharacterMotor>(this);
        if (_animationDriver == null)
            _animationDriver = CharacterBodyResolve.GetInBody<CharacterLocomotionAnim>(this);
        _hitStop = CharacterHitStopState.Find(this);
    }

    void EnsureFacingSeeded()
    {
        if (_hasFacing)
            return;

        Vector3 seed = Vector3.zero;
        if (_state != null)
        {
            seed = _state.GetFacingDir();
            seed.y = 0f;
        }

        if (seed.sqrMagnitude <= WishEpsilonSqr)
        {
            seed = transform.forward;
            seed.y = 0f;
        }

        if (seed.sqrMagnitude <= WishEpsilonSqr)
            seed = Vector3.forward;

        _bodyFacingDir = seed.normalized;
        _hasFacing = true;
    }

    void SnapToSight()
    {
        Vector3 sight = _state.SightDir;
        sight.y = 0f;
        if (sight.sqrMagnitude <= WishEpsilonSqr)
            sight = _state.GetFacingDir();
        sight.y = 0f;
        if (sight.sqrMagnitude <= WishEpsilonSqr)
            return;

        sight.Normalize();
        _bodyFacingDir = sight;
        _hasFacing = true;
        _currentTurnSpeed = 0f;
    }

    float ResolveDelta()
    {
        bool possessed = _motor != null && _motor.IsPossessed;
        return TimeScaleService.Delta(
            possessed ? TimeScaleChannel.Player : TimeScaleChannel.World);
    }

    static Vector3 RotateDirTowards(Vector3 current, Vector3 target, float maxDegrees)
    {
        current.y = 0f;
        target.y = 0f;
        if (current.sqrMagnitude <= WishEpsilonSqr)
            current = Vector3.forward;
        else
            current.Normalize();
        if (target.sqrMagnitude <= WishEpsilonSqr)
            return current;
        target.Normalize();
        if (maxDegrees <= 0f)
            return current;
        float yaw = Vector3.SignedAngle(current, target, Vector3.up);
        return (Quaternion.AngleAxis(Mathf.Clamp(yaw, -maxDegrees, maxDegrees), Vector3.up) * current).normalized;
    }
}
