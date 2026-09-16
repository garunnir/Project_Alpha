// ============================================================
// CharacterFacingRotator — CharacterState의 시야 방향에 따라 트랜스폼을 회전시키는 컴포넌트
// ============================================================
using UnityEngine;

[System.Flags]
public enum RotationAxes
{
    None = 0,
    X = 1 << 0,
    Y = 1 << 1,
    Z = 1 << 2
}

public enum RotationDirection
{
    Forward = 1,
    Reverse = -1
}

public class CharacterFacingRotator : MonoBehaviour
{
    [SerializeField] private CharacterState _state;
    [SerializeField] private float _rotationOffset = 0f;
    [SerializeField] private RotationAxes _rotationAxes = RotationAxes.Z;
    [SerializeField] private RotationDirection _rotationDirection = RotationDirection.Forward;

    private Quaternion _lastAppliedRotation;
    private bool _hasLastAppliedRotation;
    private CharacterLocomotionFacing _locomotionFacing;
    private bool _facingResolved;

    public CharacterState BoundState => _state;

    void Awake() => ResolveBodyBinding();

    void OnEnable() => ResolveBodyBinding();

    void ResolveBodyBinding()
    {
        // A prefab-local render pivot owns its body's state. System rigs still use BindState.
        if (_state == null)
            _state = CharacterBodyResolve.GetInBody<CharacterState>(this);
        _locomotionFacing = _state != null
            ? CharacterBodyResolve.GetInBody<CharacterLocomotionFacing>(_state) : null;
        _facingResolved = true;
        _hasLastAppliedRotation = false;
    }

    /// <summary>시스템 리그(PlayerSight 등)가 possess 시 CharacterState를 주입합니다.</summary>
    public void BindState(CharacterState state)
    {
        _state = state;
        _hasLastAppliedRotation = false;
        _facingResolved = false;
        _locomotionFacing = null;
    }

    void LateUpdate()
        => ApplyFacing();

    internal void ApplyFacing()
    {
        if (_state == null) return;
        if (_rotationAxes == RotationAxes.None) return;

        Vector3 dir = ResolveFacingDir();
        if (dir.sqrMagnitude < 1e-4f) return;

        // BodyFacing is world-space; spawned/rotated parents must not add their yaw twice.
        if (_rotationAxes == RotationAxes.Y && transform.parent != null)
            dir = transform.parent.InverseTransformDirection(dir);

        float baseAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float targetAngle = ((int)_rotationDirection * -baseAngle) + _rotationOffset;
        Vector3 targetEuler = transform.localEulerAngles;

        if ((_rotationAxes & RotationAxes.X) != 0) targetEuler.x = targetAngle;
        if ((_rotationAxes & RotationAxes.Y) != 0) targetEuler.y = targetAngle;
        if ((_rotationAxes & RotationAxes.Z) != 0) targetEuler.z = targetAngle;

        Quaternion targetRotation = Quaternion.Euler(targetEuler);
        if (_hasLastAppliedRotation && Mathf.Abs(Quaternion.Dot(_lastAppliedRotation, targetRotation)) > 0.999999f)
            return;

        transform.localRotation = targetRotation;
        _lastAppliedRotation = targetRotation;
        _hasLastAppliedRotation = true;
    }

    Vector3 ResolveFacingDir()
    {
        CharacterLocomotionFacing facing = ResolveLocomotionFacing();
        if (facing != null)
            return facing.BodyFacingDir;
        return _state.GetFacingDir();
    }

    CharacterLocomotionFacing ResolveLocomotionFacing()
    {
        if (_facingResolved)
            return _locomotionFacing;

        _facingResolved = true;
        _locomotionFacing = _state != null
            ? CharacterBodyResolve.GetInBody<CharacterLocomotionFacing>(_state)
            : CharacterBodyResolve.GetInBody<CharacterLocomotionFacing>(this);
        return _locomotionFacing;
    }
}
