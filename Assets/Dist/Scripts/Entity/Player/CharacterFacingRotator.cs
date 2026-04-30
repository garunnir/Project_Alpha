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

    void LateUpdate()
    {
        if (_state == null) return;
        if (_rotationAxes == RotationAxes.None) return;

        Vector3 dir = _state.GetFacingDir();
        if (dir.sqrMagnitude < 1e-4f) return;

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
}
