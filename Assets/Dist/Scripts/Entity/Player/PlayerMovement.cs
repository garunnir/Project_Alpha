using UnityEngine;
using UnityEngine.InputSystem;
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(CharacterState))]
public class PlayerMovement : MonoBehaviour
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
    [SerializeField] private float _climbAllowance = 0.3f;      // 오를 수 있는 낮은 둔덕 높이
	[SerializeField] private float _baseSkin = 0.02f;              // 벽에 바짝 붙을 때 여유
    [SerializeField] private LayerMask _collisionMask = ~0;    // 검사할 레이어
    [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Ignore;

	Rigidbody _rb;
	CapsuleCollider _capsule;
	Vector3 _desiredMove;
	Vector3 _moveDir;
	bool _isRun = false;

	// 재사용할 히트 배열 (NonAlloc)
	RaycastHit[] _hits = new RaycastHit[8];

	[Header("Gizmos")]
    [SerializeField] private bool _drawGizmos = true;
	[SerializeField] private Color _gizmoCapsuleColor = new Color(0f, 0.6f, 1f, 0.25f);
	[SerializeField] private Color _gizmoCastColor = Color.yellow;
	[SerializeField] private Color _gizmoHitColor = Color.red;
	[SerializeField] private Color _gizmoSlideColor = Color.green;

	// 마지막 스윕 데이터 (OnDrawGizmos에서 읽음)
	int _lastHitCount = 0;
	Vector3 _lastP1, _lastDesiredMove, _lastSlide;
	float _lastWorldRadius = 0f;
	int _lastNearestIndex = -1;
	private CharacterState _characterState; 

	void Awake()
	{
		_rb = GetComponent<Rigidbody>();
		_capsule = GetComponent<CapsuleCollider>();
		_characterState = GetComponent<CharacterState>();
		// 물리 기반 이동 시 회전은 외부로부터 고정하는 것이 일반적입니다.
		_rb.freezeRotation = true;
	}
	void Start(){
		InputManager.Instance.Actions.Player.Move.performed += OnMove;
		InputManager.Instance.Actions.Player.Move.canceled += OnMove;
		InputManager.Instance.Actions.Player.Run.performed += OnRun;
	}


	#region Input System Callbacks
	public void OnMove(InputAction.CallbackContext context)
	{
		Vector2 position = context.ReadValue<Vector2>();
		_moveDir = MoveByCameraRelativeInput(position);
	}

	public void OnRun(InputAction.CallbackContext context)
	{
		_isRun = context.ReadValue<float>() == 1 ? true : false;
		Debug.Log("PlayerMovement: isRun = " + _isRun);
	}
	#endregion

	public Vector3 MoveByCameraRelativeInput(Vector2 move)
	{
		// 카메라 기준 입력을 월드 XZ 평면으로 변환하여 반환합니다.
		// 반환값: (x: 월드 X 축 방향 속도비, y: 월드 Z 축 방향 속도비)
		// 카메라의 피치(tilt)는 무시하고 수평 성분만 사용합니다.

		float inputH = move.x;
		float inputV = move.y;

		// 카메라가 없으면 기본 월드 입력을 반환
		Camera camera = _refCam;
		camera ??= Camera.main;
		if (camera == null)
			return new Vector3(inputH, 0, inputV);

		Transform camT = camera.transform;

		// 카메라의 수평(지면에 투영된) 전방/우측 벡터 계산
		Vector3 camForward = Vector3.ProjectOnPlane(camT.forward, Vector3.up).normalized;
		Vector3 camRight = Vector3.ProjectOnPlane(camT.right, Vector3.up).normalized;

		// 카메라 기준 입력을 월드 방향으로 변환
		Vector3 worldDir = camRight * inputH + camForward * inputV;

		// 대각선 입력 시 속도 보정(크기 > 1인 경우 정규화)
		if (worldDir.sqrMagnitude > 1f)
			worldDir = worldDir.normalized;

		return new Vector3(worldDir.x, 0, worldDir.z);
	}
	Vector3 _currentVelocity;
 	private Vector3 CalNextMoveDir(float moveSpeed,ref Vector3 currentVelocity)
	{
		Vector3 targetVel = _moveDir * moveSpeed;
		// 다음 프레임의 이동 방향과 속도를 계산합니다.
		currentVelocity = Vector3.MoveTowards(currentVelocity, targetVel, _acceleration * Time.fixedDeltaTime);

		if (_moveDir.sqrMagnitude <= Mathf.Epsilon)
		{
			currentVelocity *= _inertia;
		}
		Vector3 desiredMove = currentVelocity * Time.fixedDeltaTime;
		return desiredMove;
	}
	void FixedUpdate()
	{
		float speed = _moveSpeed * (_isRun ? _sprintMultiplier : 1f);

		_desiredMove = CalNextMoveDir(speed, ref _currentVelocity);
		_characterState.UpdateState(_desiredMove);
        _characterState.UpdateGridPos(transform.position);
		if (_desiredMove.sqrMagnitude <= Mathf.Epsilon)
			return;

		// 캡슐 캐스트를 위한 월드 좌표 두 점 계산 (캡슐의 중심 기준)
		Vector3 worldCenter = transform.TransformPoint(_capsule.center);
		Vector3 up = transform.up;
		float halfHeight = Mathf.Max(0f, (_capsule.height * 0.5f) - _capsule.radius);
		Vector3 p1 = worldCenter + up * halfHeight;
		Vector3 p2 = worldCenter - up * (halfHeight - _climbAllowance);//낮은둔덕은 올라갈수있게 한다.

		float distance = _desiredMove.magnitude;
		//GC를 줄이는 NonAlloc 버전 사용하여 스윕
		_lastDesiredMove = _desiredMove;
		_lastP1 = p1; 
		_lastWorldRadius = _capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
		int hitCount = Physics.CapsuleCastNonAlloc(p1, p2, _lastWorldRadius, _desiredMove.normalized, _hits, distance + _baseSkin, _collisionMask, _triggerInteraction);
		_lastHitCount = hitCount;

		if (hitCount == 0)
		{
			// 장애물이 없으면 이동
			_rb.MovePosition(_rb.position + _desiredMove);
			return;
		}

		// 가장 가까운 유효 히트 찾기 (자기 자신 무시)
		RaycastHit? nearest = null;
		float minDist = float.PositiveInfinity;
		for (int i = 0; i < hitCount; i++)
		{
			var h = _hits[i];
			if (h.collider == null || h.collider.gameObject == gameObject) continue;
			// 레이어 필터(안전) — CapsuleCastNonAlloc에 이미 적용되지만 추가 필터링 안전장치
			if (((1 << h.collider.gameObject.layer) & _collisionMask.value) == 0) continue;
			if (h.distance < minDist && h.distance >= 0f)
			{
				minDist = h.distance;
				nearest = h;
			}
		}

		if (!nearest.HasValue)
		{
			_lastNearestIndex = -1;
			// 필터링 후 충돌 없음 -> 이동
			_rb.MovePosition(_rb.position + _desiredMove);
			return;
		}

		RaycastHit hit = nearest.Value;
		// nearest 히트 index 저장 (for gizmos)
		for (int i = 0; i < hitCount; i++)
		{
			if (_hits[i].collider == hit.collider && Mathf.Approximately(_hits[i].distance, hit.distance)) { _lastNearestIndex = i; break; }
		}

		// 충돌면의 노멀을 이용해 슬라이딩 벡터 계산 미끄럼타기 위함
		Vector3 slide = Vector3.ProjectOnPlane(_desiredMove, hit.normal);

		// 슬라이드 방향으로 이동 가능한지 검사
		if (slide.sqrMagnitude > Mathf.Epsilon)
		{
			float slideDist = slide.magnitude;
			int slideHits = Physics.CapsuleCastNonAlloc(p1, p2, _capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y), slide.normalized, _hits, slideDist + _baseSkin, _collisionMask, _triggerInteraction);
			bool slideBlocked = false;
			for (int i = 0; i < slideHits; i++)
			{
				if (_hits[i].collider == null || _hits[i].collider.gameObject == gameObject) continue;
				if (((1 << _hits[i].collider.gameObject.layer) & _collisionMask.value) == 0) continue;
				slideBlocked = true;
				if(Config.DebugMode.PlayerMovement)	Debug.Log("PlayerMovement: Slide blocked by " + _hits[i].collider.name);
				break;
			}

			if (!slideBlocked)
			{
				_lastSlide = slide;
				_rb.MovePosition(_rb.position + slide);
				if(Config.DebugMode.PlayerMovement)	Debug.Log("PlayerMovement: Sliding");
				return;
			}
		}

		// 슬라이드도 막히면, 가능한 거리만큼 이동(히트 이전의 거리 - skin)
		float moveAllowed = Mathf.Max(0f, minDist - _baseSkin);
		if (moveAllowed > 0f)
		{
			Vector3 partial = _desiredMove.normalized * moveAllowed;
			_rb.MovePosition(_rb.position + partial);
		}
		else
		{
			// 거의 붙어있으면 정지
			_rb.MovePosition(_rb.position);
			if(Config.DebugMode.PlayerMovement)	Debug.LogError("PlayerMovement: Stuck!");
		}
	}

	void OnDrawGizmos()
	{
		if (!_drawGizmos) return;

		// 기본 캡슐 (현재 transform 기준)
		if (_capsule == null)
		{
			_capsule = GetComponent<CapsuleCollider>();
			if (_capsule == null) return;
		}

		Gizmos.color = _gizmoCapsuleColor;
		Vector3 wc = transform.TransformPoint(_capsule.center);
		Vector3 up = transform.up;
		float halfH = Mathf.Max(0f, (_capsule.height * 0.5f) - _capsule.radius);
		Vector3 cp1 = wc + up * halfH;
		Vector3 cp2 = wc - up * halfH;
		// 단순한 시각화: 양 끝에 와이어구 그리기
		Gizmos.DrawWireSphere(cp1, _capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y));
		Gizmos.DrawWireSphere(cp2, _capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y));
		Gizmos.DrawLine(cp1 + transform.right * _capsule.radius, cp2 + transform.right * _capsule.radius);
		Gizmos.DrawLine(cp1 - transform.right * _capsule.radius, cp2 - transform.right * _capsule.radius);
		Gizmos.color = Color.skyBlue;
		Gizmos.DrawWireSphere(transform.position + _desiredMove, 0.01f);

		// 캐스트 시각화
		if (_lastHitCount >= 0)
		{
			Gizmos.color = _gizmoCastColor;
			Gizmos.DrawLine(_lastP1, _lastP1 + _lastDesiredMove.normalized * (_lastDesiredMove.magnitude + _baseSkin));

			// 히트 포인트들
			for (int i = 0; i < _lastHitCount; i++)
			{
				var h = _hits[i];
				if (h.collider == null) continue;
				Gizmos.color = (i == _lastNearestIndex) ? _gizmoHitColor : new Color(1f, 0.5f, 0.2f, 1f);
				Gizmos.DrawSphere(h.point, 0.05f);
				// 노멀
				Gizmos.color = Color.magenta;
				Gizmos.DrawLine(h.point, h.point + h.normal * 0.5f);
			}

			// 슬라이드 벡터
			if (_lastSlide.sqrMagnitude > 0f)
			{
				Gizmos.color = _gizmoSlideColor;
				Gizmos.DrawLine(transform.position, transform.position + _lastSlide);
			}
		}
	}
}
