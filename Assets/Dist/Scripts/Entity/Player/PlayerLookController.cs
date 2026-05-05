// ============================================================
// PlayerLookController — 마우스 기준 방향으로 SphereCast해 막힌 지점·시야를 CharacterState에 전달
// ============================================================
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.IO;

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
    private const string AgentDebugLogPath = "debug-849359.log";

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

    void LateUpdate()
    {
        if (!_isAiming) return;
        Camera cam = _refCam != null ? _refCam : Camera.main;
        Vector3 origin = transform.position + Vector3.up * _castOriginYOffset;

        // #region agent log
        if (Time.frameCount % 10 == 0)
        {
            AgentDebugLog(
                runId: "pre-fix",
                hypothesisId: "H1_CAMERA_REF",
                location: "PlayerLookController.cs:64",
                message: "camera source and player origin",
                dataJson: $"{{\"frame\":{Time.frameCount},\"camNull\":{(cam == null).ToString().ToLowerInvariant()},\"camName\":\"{Safe(cam != null ? cam.name : "null")}\",\"camPos\":\"{Safe(cam != null ? cam.transform.position.ToString("F3") : "null")}\",\"camTag\":\"{Safe(cam != null ? cam.tag : "null")}\",\"playerPos\":\"{Safe(transform.position.ToString("F3"))}\",\"castOrigin\":\"{Safe(origin.ToString("F3"))}\"}}");
        }
        // #endregion

        if (!ScreenRaycaster.TryGetMouseWorldPosition(cam, transform.position.y, out Vector3 mousePlanePos)) return;

        Vector3 flatTarget = mousePlanePos;
        flatTarget.y = origin.y;

        Vector3 toTarget = flatTarget - origin;
        toTarget.y = 0f;
        float maxDist = toTarget.magnitude;
        if (maxDist < 1e-4f) return;
        Vector3 dir = toTarget / maxDist;

        RaycastHit hit = default;
        bool hasHit = Physics.SphereCast(origin, _sphereRadius, dir, out hit, maxDist,
                _aimObstructionMask, QueryTriggerInteraction.Ignore);
        Vector3 aimPoint;
        if (hasHit)
            aimPoint = hit.point;
        else
            aimPoint = origin + dir * maxDist;

        // #region agent log
        if (Time.frameCount % 10 == 0)
        {
            AgentDebugLog(
                runId: "pre-fix",
                hypothesisId: "H3_AIM_RESULT",
                location: "PlayerLookController.cs:93",
                message: "aim computation result",
                dataJson: $"{{\"frame\":{Time.frameCount},\"mousePlanePos\":\"{Safe(mousePlanePos.ToString("F3"))}\",\"flatTarget\":\"{Safe(flatTarget.ToString("F3"))}\",\"dir\":\"{Safe(dir.ToString("F3"))}\",\"maxDist\":{maxDist.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)},\"hasHit\":{hasHit.ToString().ToLowerInvariant()},\"hitCollider\":\"{Safe(hasHit && hit.collider != null ? hit.collider.name : "none")}\",\"hitPath\":\"{Safe(hasHit && hit.collider != null ? GetHierarchyPath(hit.collider.transform) : "none")}\",\"hitDistance\":{(hasHit ? hit.distance.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) : "0.0")},\"hitLayer\":{(hasHit && hit.collider != null ? hit.collider.gameObject.layer : -1)},\"isSelfOrChildHit\":{(hasHit && hit.collider != null && hit.collider.transform.IsChildOf(transform)).ToString().ToLowerInvariant()},\"aimPoint\":\"{Safe(aimPoint.ToString("F3"))}\"}}");
        }
        // #endregion

        Vector3 sightFlat = aimPoint - origin;
        sightFlat.y = 0f;
        if (sightFlat.sqrMagnitude < 1e-4f) return;
        _characterState.SetAimDir(sightFlat.normalized, aimPoint);

        // #region agent log
        if (Time.frameCount % 10 == 0)
        {
            AgentDebugLog(
                runId: "pre-fix",
                hypothesisId: "H4_STATE_MISMATCH",
                location: "PlayerLookController.cs:108",
                message: "character state update",
                dataJson: $"{{\"frame\":{Time.frameCount},\"sightFlat\":\"{Safe(sightFlat.ToString("F3"))}\",\"sightDir\":\"{Safe(sightFlat.normalized.ToString("F3"))}\",\"storedAim\":\"{Safe(_characterState.AimWorldPoint.ToString("F3"))}\"}}");
        }
        // #endregion
    }

    void OnDrawGizmos()
    {
        if (_characterState == null || !_characterState.IsAiming) return;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, _characterState.AimWorldPoint);
        Gizmos.DrawWireSphere(_characterState.AimWorldPoint, 0.1f);
    }

    private static void AgentDebugLog(string runId, string hypothesisId, string location, string message, string dataJson)
    {
        try
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string line =
                "{\"sessionId\":\"849359\",\"runId\":\"" + runId +
                "\",\"hypothesisId\":\"" + hypothesisId +
                "\",\"location\":\"" + location +
                "\",\"message\":\"" + message +
                "\",\"data\":" + dataJson +
                ",\"timestamp\":" + timestamp + "}";
            File.AppendAllText(AgentDebugLogPath, line + Environment.NewLine);
        }
        catch
        {
            // Debug logging must not break gameplay.
        }
    }

    private static string Safe(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null) return string.Empty;
        string path = t.name;
        Transform p = t.parent;
        while (p != null)
        {
            path = p.name + "/" + path;
            p = p.parent;
        }
        return path;
    }
}
