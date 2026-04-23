// ============================================================
// PlayerMovement — KinematicMover를 이용한 캡슐 기반 플레이어 이동 (MonoBehaviour 래퍼)
// ============================================================
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(CharacterState))]
public class PlayerMovement : MonoBehaviour, IMovable
{
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _sprintMultiplier = 2f;
    [SerializeField] private float _acceleration = 10f;
    [SerializeField] private Camera _refCam;
    [Tooltip("관성(감쇠) 계수. 0에 가까울수록 미끄러지듯 멈춤, 1에 가까울수록 즉시 멈춤")]
    [Range(0f, 1f)]
    [SerializeField] private float _inertia = 0.9f;

    [Header("Collision")]
    [SerializeField] private float _climbAllowance = 0.3f;
    [SerializeField] private float _baseSkin = 0.02f;
    [SerializeField] private LayerMask _collisionMask = ~0;
    [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Gizmos")]
    [SerializeField] private bool _drawGizmos = true;
    [SerializeField] private Color _gizmoCapsuleColor = new Color(0f, 0.6f, 1f, 0.25f);
    [SerializeField] private Color _gizmoCastColor = Color.yellow;
    [SerializeField] private Color _gizmoHitColor = Color.red;
    [SerializeField] private Color _gizmoSlideColor = Color.green;

    [SerializeField,ReadOnly] private Vector2 _moveDir;
    Rigidbody _rb;
    CapsuleCollider _capsule;
    CharacterState _characterState;
    KinematicMover _mover;

    RaycastHit[] _hits = new RaycastHit[8];

    // Gizmo 전용 캐시
    int _lastHitCount;
    Vector3 _lastP1, _lastDesiredMove;
    float _lastWorldRadius;

    public void SetControllEnabled(bool enabled)
    {
        if (enabled) ConnectController();
        else DisconnectController();
    }

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _capsule = GetComponent<CapsuleCollider>();
        _characterState = GetComponent<CharacterState>();
        _rb.freezeRotation = true;

        _mover = new KinematicMover
        {
            Acceleration       = _acceleration,
            Inertia            = _inertia,
            BaseSkin           = _baseSkin,
            CollisionMask      = _collisionMask,
            TriggerInteraction = _triggerInteraction,
        };
    }

    void ConnectController()
    {
        InputManager.Instance.Actions.Player.Move.performed += OnMove;
        InputManager.Instance.Actions.Player.Move.canceled  += OnMove;
        InputManager.Instance.Actions.Player.Run.performed  += OnRun;
    }

    void DisconnectController()
    {
        InputManager.Instance.Actions.Player.Move.performed -= OnMove;
        InputManager.Instance.Actions.Player.Move.canceled  -= OnMove;
        InputManager.Instance.Actions.Player.Run.performed  -= OnRun;
    }

    public UnityEngine.Vector2 GetDirection(){
        return _moveDir;
    }
    public void OnMove(InputAction.CallbackContext context)
    {
        Vector2 inputDir=context.ReadValue<Vector2>();
        _mover.SetInput(inputDir, _refCam);
        _characterState.SetMoveDir(_mover.WorldMoveDir);
        _characterState.UpdateGridPos(transform.position);
    }

    public void OnRun(InputAction.CallbackContext context)
    {
        bool isRun = context.ReadValue<float>() == 1f;
        _mover.SetSprinting(isRun);
        Debug.Log("PlayerMovement: isRun = " + isRun);
    }

    void FixedUpdate()
    {
        Vector3 desiredMove = _mover.CalcDesiredMove(_moveSpeed, _sprintMultiplier, Time.fixedDeltaTime);
        _lastDesiredMove = desiredMove;

        if (desiredMove.sqrMagnitude <= Mathf.Epsilon)
        {
            _lastHitCount = 0;
            return;
        }

        Vector3 worldCenter = transform.TransformPoint(_capsule.center);
        Vector3 up          = transform.up;
        float halfHeight    = Mathf.Max(0f, (_capsule.height * 0.5f) - _capsule.radius);
        Vector3 p1 = worldCenter + up * halfHeight;
        Vector3 p2 = worldCenter - up * (halfHeight - _climbAllowance);
        float radius = _capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);

        float distance = desiredMove.magnitude;
        int hitCount = Physics.CapsuleCastNonAlloc(
            p1, p2, radius, desiredMove.normalized,
            _hits, distance + _baseSkin, _collisionMask, _triggerInteraction);

        _lastP1          = p1;
        _lastWorldRadius = radius;
        _lastHitCount    = hitCount;

        if (hitCount == 0)
        {
            _rb.MovePosition(_rb.position + desiredMove);
            return;
        }

        Vector3 delta = _mover.ResolveMove(desiredMove, p1, p2, radius, _hits, hitCount, _capsule);

        if (delta.sqrMagnitude <= Mathf.Epsilon)
        {
            _rb.MovePosition(_rb.position);
            if (Config.DebugMode.PlayerMovement) Debug.LogError("PlayerMovement: Stuck!");
            return;
        }

        if (Config.DebugMode.PlayerMovement && _mover.LastSlide.sqrMagnitude > 0f)
            Debug.Log("PlayerMovement: Sliding");

        _rb.MovePosition(_rb.position + delta);
        _moveDir=delta.normalized;
    }

    void OnDrawGizmos()
    {
        if (!_drawGizmos) return;

        if (_capsule == null)
        {
            _capsule = GetComponent<CapsuleCollider>();
            if (_capsule == null) return;
        }

        float scale  = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
        float radius = _capsule.radius * scale;
        Vector3 wc   = transform.TransformPoint(_capsule.center);
        Vector3 up   = transform.up;
        float halfH  = Mathf.Max(0f, (_capsule.height * 0.5f) - _capsule.radius);
        Vector3 cp1  = wc + up * halfH;
        Vector3 cp2  = wc - up * halfH;

        Gizmos.color = _gizmoCapsuleColor;
        Gizmos.DrawWireSphere(cp1, radius);
        Gizmos.DrawWireSphere(cp2, radius);
        Gizmos.DrawLine(cp1 + transform.right * _capsule.radius, cp2 + transform.right * _capsule.radius);
        Gizmos.DrawLine(cp1 - transform.right * _capsule.radius, cp2 - transform.right * _capsule.radius);
        Gizmos.color = Color.skyBlue;
        Gizmos.DrawWireSphere(transform.position + _lastDesiredMove, 0.01f);

        if (_lastHitCount > 0)
        {
            Gizmos.color = _gizmoCastColor;
            Gizmos.DrawLine(_lastP1, _lastP1 + _lastDesiredMove.normalized * (_lastDesiredMove.magnitude + _baseSkin));

            for (int i = 0; i < _lastHitCount; i++)
            {
                var h = _hits[i];
                if (h.collider == null) continue;
                Gizmos.color = (i == _mover.LastNearestIndex) ? _gizmoHitColor : new Color(1f, 0.5f, 0.2f, 1f);
                Gizmos.DrawSphere(h.point, 0.05f);
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(h.point, h.point + h.normal * 0.5f);
            }

            if (_mover.LastSlide.sqrMagnitude > 0f)
            {
                Gizmos.color = _gizmoSlideColor;
                Gizmos.DrawLine(transform.position, transform.position + _mover.LastSlide);
            }
        }
    }
}
