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
    const float DefaultBodyTurnDegPerSec = 270f;

    [SerializeField] CharacterState _state;
    [SerializeField] CharacterMotor _motor;

    [Tooltip("Body yaw turn rate (deg/sec). SSOT for root/MoveXZ facing chase.")]
    [SerializeField, Min(0f)] float _bodyTurnDegPerSec = DefaultBodyTurnDegPerSec;

    Vector3 _bodyFacingDir = Vector3.forward;
    bool _hasFacing;

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
        if (_state == null)
            ResolveRefs();
        if (_state == null)
            return;

        EnsureFacingSeeded();

        float dt = ResolveDelta();
        if (dt <= 0f)
            return;

        if (_state.IsAiming)
        {
            SnapToSight();
            return;
        }

        Vector3 wish = _state.GetFacingDir();
        wish.y = 0f;
        if (wish.sqrMagnitude <= WishEpsilonSqr)
            return;

        wish.Normalize();
        _bodyFacingDir = RotateDirTowards(_bodyFacingDir, wish, _bodyTurnDegPerSec * dt);
    }

    void ResolveRefs()
    {
        if (_state == null)
            _state = CharacterBodyResolve.GetInBody<CharacterState>(this);
        if (_motor == null)
            _motor = CharacterBodyResolve.GetInBody<CharacterMotor>(this);
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
        return Vector3.RotateTowards(current, target, maxDegrees * Mathf.Deg2Rad, 0f).normalized;
    }
}
