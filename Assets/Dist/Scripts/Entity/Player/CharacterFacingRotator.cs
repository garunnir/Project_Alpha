// ============================================================
// CharacterFacingRotator — CharacterState의 시야 방향에 따라 트랜스폼을 회전시키는 컴포넌트
// ============================================================
using UnityEngine;

public class CharacterFacingRotator : MonoBehaviour
{
    [SerializeField] private CharacterState _state;
    [SerializeField] private float _rotationOffset = 0f;

    void LateUpdate()
    {
        if (_state == null) return;
        Vector3 dir = _state.GetFacingDir();
        if (dir.sqrMagnitude < 1e-4f) return;

        float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        transform.localRotation = Quaternion.Euler(0f, 0f, -angle + _rotationOffset);
    }
}
