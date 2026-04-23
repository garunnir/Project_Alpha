// ============================================================
// PlayerLookController — 마우스 위치 기반 시야 방향을 CharacterState에 전달하는 컴포넌트
// ============================================================
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterState))]
public class PlayerLookController : MonoBehaviour
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
        Camera cam = _refCam != null ? _refCam : Camera.main;
        if (!ScreenRaycaster.TryGetMouseWorldPosition(cam, transform.position.y, out Vector3 worldPos)) return;
        Vector3 dir = worldPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 1e-4f)
            _characterState.SetAimDir(dir.normalized);
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
