// ============================================================
// CameraZoomController — Cinemachine orthographic 렌즈 줌
// ============================================================
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineCamera))]
public class CameraZoomController : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float _minOrthographicSize = 3f;
    [SerializeField, Min(0.01f)] private float _maxOrthographicSize = 10f;
    [SerializeField, Min(0.01f)] private float _scrollStepSize = 0.5f;
    [SerializeField, Min(0f)] private float _zoomSmoothTime = 0.08f;

    private CinemachineCamera _cinemachineCamera;
    private float _targetOrthographicSize;
    private float _zoomVelocity;

    private void Awake()
    {
        _cinemachineCamera = GetComponent<CinemachineCamera>();
        _targetOrthographicSize = _cinemachineCamera.Lens.OrthographicSize;
        ClampTarget();
    }

    private void Update()
    {
        if (_cinemachineCamera == null)
            return;

        if (InputManager.Instance == null)
            return;

        float scrollY = InputManager.Instance.Actions.Player.Zoom.ReadValue<Vector2>().y;
    
        _targetOrthographicSize -= scrollY * _scrollStepSize / 120f;
        ClampTarget();

        float currentSize = _cinemachineCamera.Lens.OrthographicSize;
        float nextSize = _zoomSmoothTime <= 0f
            ? _targetOrthographicSize
            : Mathf.SmoothDamp(currentSize, _targetOrthographicSize, ref _zoomVelocity, _zoomSmoothTime);
        ApplyOrthographicSize(nextSize);
    }

    private void ClampTarget()
    {
        float min = Mathf.Min(_minOrthographicSize, _maxOrthographicSize);
        float max = Mathf.Max(_minOrthographicSize, _maxOrthographicSize);
        _targetOrthographicSize = Mathf.Clamp(_targetOrthographicSize, min, max);
    }

    private void ApplyOrthographicSize(float size)
    {
        _cinemachineCamera.Lens.OrthographicSize = size;
    }
}
