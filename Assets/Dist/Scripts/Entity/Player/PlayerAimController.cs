// ============================================================
// PlayerAimController — 마우스 위치 기반 조준 방향을 CharacterState에 전달하는 컴포넌트
// ============================================================
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterState))]
public class PlayerAimController : MonoBehaviour
{
    [SerializeField] private Camera _refCam;

    private CharacterState _characterState;
    private bool _isAiming;

    void Awake()
    {
        _characterState = GetComponent<CharacterState>();
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled) ConnectController();
        else DisconnectController();
    }

    void ConnectController()
    {
        InputManager.Instance.Actions.Player.LookAt.started += OnLookAt;
        InputManager.Instance.Actions.Player.LookAt.performed += OnLookAt;
        InputManager.Instance.Actions.Player.LookAt.canceled += OnLookAtCanceled;
    }

    void DisconnectController()
    {
        InputManager.Instance.Actions.Player.LookAt.started -= OnLookAt;
        InputManager.Instance.Actions.Player.LookAt.performed -= OnLookAt;
        InputManager.Instance.Actions.Player.LookAt.canceled -= OnLookAtCanceled;
    }

    void OnLookAt(InputAction.CallbackContext context)
    {
        _isAiming = true;
    }

    void OnLookAtCanceled(InputAction.CallbackContext context)
    {
        _isAiming = false;
        _characterState.ClearAim();
    }

    void FixedUpdate()
    {
        if (!_isAiming) return;
        Vector3 aimDir = GetMouseWorldDir();
        if (aimDir != Vector3.zero)
            _characterState.SetAimDir(aimDir);
    }

    Vector3 GetMouseWorldDir()
    {
        var mousePos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
        Camera cam = _refCam != null ? _refCam : Camera.main;
        if (cam == null) return Vector3.zero;
        Ray ray = cam.ScreenPointToRay(mousePos);
        if (Mathf.Abs(ray.direction.y) < 1e-6f) return Vector3.zero;
        float t = (transform.position.y - ray.origin.y) / ray.direction.y;
        if (t < 0f) return Vector3.zero;
        Vector3 worldPos = ray.origin + ray.direction * t;
        Vector3 dir = worldPos - transform.position;
        dir.y = 0f;
        return dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.zero;
    }

    void OnDrawGizmos()
    {
        if (_characterState == null || !_characterState.IsAiming) return;
        Vector3 aimDir = _characterState.SightDir;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + aimDir * 1.5f);
        Gizmos.DrawWireSphere(transform.position + aimDir * 1.5f, 0.1f);
    }
}
