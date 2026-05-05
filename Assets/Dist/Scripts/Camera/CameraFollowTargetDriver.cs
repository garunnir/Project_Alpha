using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Drives a proxy transform for Cinemachine follow/look-at.
/// Keeps aim data as Vector3 while exposing a stable transform target.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineCamera))]
public class CameraFollowTargetDriver : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform _followTarget;
    [SerializeField] private CharacterState _characterState;
    [SerializeField] private Transform _proxyTarget;

    [Header("Offsets")]
    [SerializeField] private Vector3 _followOffset = new Vector3(0f, 8f, -8f);
    [SerializeField, Range(0f, 1f)] private float _aimLeadWeight = 0.32f;

    [Header("Damping")]
    [SerializeField, Min(0.01f)] private float _positionSmoothTime = 0.12f;
    [SerializeField] private float _maxSpeed = 100f;

    private CinemachineCamera _cinemachineCamera;
    private Vector3 _smoothedVelocity;
    private Vector3 _latestAimPoint;
    private bool _hasAimPoint;

    private void Awake()
    {
        _cinemachineCamera = GetComponent<CinemachineCamera>();
        EnsureMainCameraBrain();
        EnsureProxyTarget();
        BindToCinemachine();
    }

    private void OnEnable()
    {
        BindCharacterState(_characterState);
        if (_followTarget != null && _proxyTarget != null)
            _proxyTarget.position = GetDesiredPosition();
    }

    private void OnDisable()
    {
        BindCharacterState(null);
    }

    private void LateUpdate()
    {
        if (_followTarget == null || _proxyTarget == null)
            return;

        Vector3 desired = GetDesiredPosition();
        _proxyTarget.position = Vector3.SmoothDamp(
            _proxyTarget.position,
            desired,
            ref _smoothedVelocity,
            _positionSmoothTime,
            _maxSpeed,
            Time.deltaTime);
    }

    public void SetTarget(Transform target, CharacterState state)
    {
        _followTarget = target;
        BindCharacterState(state);
        if (_proxyTarget != null && _followTarget != null)
            _proxyTarget.position = GetDesiredPosition();
    }

    public void SetAimLeadWeight(float weight)
    {
        _aimLeadWeight = Mathf.Clamp01(weight);
    }

    private void BindCharacterState(CharacterState state)
    {
        if (_characterState != null)
            _characterState.AimWorldPointChanged -= OnAimWorldPointChanged;

        _characterState = state;
        _hasAimPoint = false;

        if (_characterState != null)
        {
            _latestAimPoint = _characterState.AimWorldPoint;
            _hasAimPoint = _characterState.IsAiming;
            _characterState.AimWorldPointChanged += OnAimWorldPointChanged;
        }
    }

    private void OnAimWorldPointChanged(Vector3 worldPoint)
    {
        _latestAimPoint = worldPoint;
        _hasAimPoint = _characterState != null && _characterState.IsAiming;
    }

    private Vector3 GetDesiredPosition()
    {
        Vector3 followBase = _followTarget.position + _followOffset;
        if (_characterState == null || !_characterState.IsAiming || !_hasAimPoint)
            return followBase;

        Vector3 aimBase = _latestAimPoint + _followOffset;
        return Vector3.Lerp(followBase, aimBase, _aimLeadWeight);
    }

    private void BindToCinemachine()
    {
        if (_cinemachineCamera == null || _proxyTarget == null)
            return;

        _cinemachineCamera.Follow = _proxyTarget;
        _cinemachineCamera.LookAt = _proxyTarget;
    }

    private void EnsureProxyTarget()
    {
        if (_proxyTarget != null)
            return;

        Transform found = transform.Find("CameraProxyTarget");
        if (found == null)
        {
            GameObject go = new GameObject("CameraProxyTarget");
            found = go.transform;
            found.SetParent(transform, false);
        }

        _proxyTarget = found;
    }

    private static void EnsureMainCameraBrain()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        if (!cam.TryGetComponent(out CinemachineBrain _))
            cam.gameObject.AddComponent<CinemachineBrain>();
    }
}
