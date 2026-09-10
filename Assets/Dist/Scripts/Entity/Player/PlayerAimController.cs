// ============================================================
// PlayerAimController — RMB 조준 입력 + IAimSightProvider 호출 (해석식 없음)
// ============================================================
using IsoTilemap;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAimController : MonoBehaviour
{
    [SerializeField] private Camera _refCam;
    [SerializeField] private float _sphereRadius = 0.10f;
    [SerializeField] private float _castOriginYOffset = 0.35f;
    [Tooltip("마우스 거리와 무관하게 조준·상호작용 SphereCast가 닿는 최대 거리.")]
    [SerializeField] private float _maxAimDistance = 15f;
    [Tooltip("Leaf 미바인딩·비-MeleeBlock용 기본. MeleeBlock(Excavate) flatten은 CombatAimSightPolicy가 false로 고정.")]
    [SerializeField] private bool _flattenAimYToPlayerHeight = true;
    [Tooltip("막힘 검사 레이어(플레이어 본체 레이어는 제외하는 것을 권장)")]
    [SerializeField] private LayerMask _aimObstructionMask = ~0;

    private CharacterState _characterState;
    private Transform _bodyTransform;
    private CharacterAttacker _attacker;
    private CharacterActionHost _actionHost;
    private MapTopologyLineCast _topologyLineCast;
    private IAimSightProvider _sightProvider = new PlayerMouseSphereAimProvider();
    private bool _isAiming;
    private bool _connected;

    bool ShouldDrawAimDebug => Config.DebugMode.PlayerSight;

    public float CastOriginYOffset => _castOriginYOffset;
    public float SphereRadius => _sphereRadius;
    public float MaxAimDistance => _maxAimDistance;

    public IAimSightProvider SightProvider => _sightProvider;

    /// <summary>게임패드 등 교체용. null이면 default mouse/sphere.</summary>
    public void SetSightProvider(IAimSightProvider provider) =>
        _sightProvider = provider ?? new PlayerMouseSphereAimProvider();

    public bool TryResolveSightWorldPoint(out Vector3 aimWorldPoint)
    {
        Transform body = _bodyTransform != null ? _bodyTransform : transform;
        if (CombatAimSightHoldLock.TryApplyLockedSight(
                _characterState,
                body,
                _castOriginYOffset,
                _attacker,
                _actionHost))
        {
            aimWorldPoint = _characterState.AimWorldPoint;
            return true;
        }

        return PlayerSightTarget.TryResolveWorldPoint(
            body,
            _refCam != null ? _refCam : Camera.main,
            _topologyLineCast,
            BuildSightSettings(),
            out aimWorldPoint);
    }

    PlayerSightTarget.Settings BuildSightSettings() => BuildAimContext().ToSightSettings();

    AimSightContext BuildAimContext() => new(
        _refCam != null ? _refCam : Camera.main,
        _topologyLineCast,
        _castOriginYOffset,
        _sphereRadius,
        _maxAimDistance,
        ResolveFlattenAimY(),
        _aimObstructionMask);

    /// <summary>입력 기본 + Leaf 정책. Attacker 없으면 Inspector 기본만.</summary>
    bool ResolveFlattenAimY()
    {
        if (_attacker == null)
            return _flattenAimYToPlayerHeight;

        return CombatAimSightPolicy
            .Resolve(_attacker.SelectedLeaf, _flattenAimYToPlayerHeight)
            .FlattenAimYToPlayerHeight;
    }

    void Awake()
    {
        _characterState = GetComponent<CharacterState>();
        if (_bodyTransform == null)
            _bodyTransform = transform;
        if (_sightProvider == null)
            _sightProvider = new PlayerMouseSphereAimProvider();
    }

    public void BindBody(CharacterState state, Transform bodyTransform) =>
        BindBody(state, bodyTransform, null, null);

    public void BindBody(
        CharacterState state,
        Transform bodyTransform,
        CharacterAttacker attacker,
        CharacterActionHost actionHost = null)
    {
        _characterState = state;
        _bodyTransform = bodyTransform;
        _attacker = attacker;
        _actionHost = actionHost;
    }

    public void BindMapCollision(MapTopologyLineCast lineCast) => _topologyLineCast = lineCast;

    public void SetEnabled(bool enabled)
    {
        if (enabled) ConnectController();
        else DisconnectController();
    }

    void ConnectController()
    {
        InputManager input = InputManager.Instance;
        if (input == null || _connected)
            return;

        input.PlayerLookAtPerformed += OnLookAtHoldPerformed;
        input.PlayerLookAtCanceled += OnLookAtCanceled;
        _connected = true;
    }

    void DisconnectController()
    {
        InputManager input = InputManager.Instance;
        if (input != null && _connected)
        {
            input.PlayerLookAtPerformed -= OnLookAtHoldPerformed;
            input.PlayerLookAtCanceled -= OnLookAtCanceled;
        }

        _connected = false;

        if (_isAiming)
        {
            _isAiming = false;
            _characterState?.ClearAim();
        }
    }

    void OnLookAtHoldPerformed(InputAction.CallbackContext context)
    {
        _isAiming = true;
    }

    void OnLookAtCanceled(InputAction.CallbackContext context)
    {
        if (!_isAiming)
            return;

        _isAiming = false;
        _characterState?.ClearAim();
    }

    void LateUpdate()
    {
        if (_characterState == null || !_isAiming || InputManager.Instance == null)
            return;
        if (!InputManager.Instance.IsPlayerActionEnabled(PlayerAction.Aim))
        {
            _isAiming = false;
            _characterState?.ClearAim();
            return;
        }

        Transform body = _bodyTransform != null ? _bodyTransform : transform;

        // Dig LMB 잠금: SphereCast 스킵, SightDir=블록. 카메라 자유.
        if (CombatAimSightHoldLock.TryApplyLockedSight(
                _characterState,
                body,
                _castOriginYOffset,
                _attacker,
                _actionHost))
        {
            if (ShouldDrawAimDebug)
            {
                Vector3 origin = body.position + Vector3.up * _castOriginYOffset;
                Debug.DrawLine(origin, _characterState.AimWorldPoint, Color.cyan, 0f, false);
            }
            return;
        }

        if (!_sightProvider.TryUpdateSight(_characterState, body, BuildAimContext()))
            return;

        if (ShouldDrawAimDebug)
        {
            Vector3 origin = body.position + Vector3.up * _castOriginYOffset;
            Debug.DrawLine(origin, _characterState.AimWorldPoint, Color.red, 0f, false);
        }
    }

    void OnDrawGizmos()
    {
        if (!ShouldDrawAimDebug) return;
        if (_characterState == null && !TryGetComponent(out _characterState)) return;
        if (!_characterState.IsAiming) return;
        Transform body = _bodyTransform != null ? _bodyTransform : transform;
        Vector3 origin = body.position + Vector3.up * _castOriginYOffset;
        Vector3 aim = _characterState.AimWorldPoint;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, aim);
        Gizmos.DrawWireSphere(aim, 0.1f);
    }
}
