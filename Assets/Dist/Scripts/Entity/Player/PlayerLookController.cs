// ============================================================
// PlayerLookController — 마우스 기준 방향으로 SphereCast해 막힌 지점·시야를 CharacterState에 전달
// ============================================================
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterState))]
public class PlayerLookController : MonoBehaviour
{
    [SerializeField] private Camera _refCam;
    [SerializeField] private float _sphereRadius = 0.15f;
    [SerializeField] private float _castOriginYOffset = 0.35f;
    [Tooltip("막힘 검사 레이어(플레이어 본체 레이어는 제외하는 것을 권장)")]
    [SerializeField] private LayerMask _aimObstructionMask = ~0;

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
        if (!ScreenRaycaster.TryGetMouseWorldPosition(cam, transform.position.y, out Vector3 mousePlanePos)) return;

        Vector3 origin = transform.position + Vector3.up * _castOriginYOffset;
        Vector3 flatTarget = mousePlanePos;
        flatTarget.y = origin.y;

        Vector3 toTarget = flatTarget - origin;
        toTarget.y = 0f;
        float maxDist = toTarget.magnitude;
        if (maxDist < 1e-4f) return;
        Vector3 dir = toTarget / maxDist;

        Vector3 aimPoint;
        if (Physics.SphereCast(origin, _sphereRadius, dir, out RaycastHit hit, maxDist,
                _aimObstructionMask, QueryTriggerInteraction.Ignore))
            aimPoint = hit.point;
        else
            aimPoint = origin + dir * maxDist;

        Vector3 sightFlat = aimPoint - origin;
        sightFlat.y = 0f;
        if (sightFlat.sqrMagnitude < 1e-4f) return;
        _characterState.SetAimDir(sightFlat.normalized, aimPoint);
    }

    void OnDrawGizmos()
    {
        if (_characterState == null || !_characterState.IsAiming) return;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, _characterState.AimWorldPoint);
        Gizmos.DrawWireSphere(_characterState.AimWorldPoint, 0.1f);
    }
}
