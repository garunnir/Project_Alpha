using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour
{
	[Header("Movement")]
	public float moveSpeed = 5f;
	public float sprintMultiplier = 2f;
	public float acceleration = 10f;
	[Tooltip("관성(감쇠) 계수. 0에 가까울수록 미끄러지듯 멈춤, 1에 가까울수록 즉시 멈춤")]
	[Range(0f, 1f)]
	public float inertia = 0.9f;

	[Header("Collision")]
	public float _baseSkin = 0.02f;              // 벽에 바짝 붙을 때 여유
	public LayerMask collisionMask = ~0;    // 검사할 레이어
	public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

	Rigidbody rb;
	CapsuleCollider capsule;
	Vector3 currentVelocity = Vector3.zero;

	// 재사용할 히트 배열 (NonAlloc)
	RaycastHit[] hits = new RaycastHit[8];

	[Header("Gizmos")]
	public bool drawGizmos = true;
	public Color gizmoCapsuleColor = new Color(0f, 0.6f, 1f, 0.25f);
	public Color gizmoCastColor = Color.yellow;
	public Color gizmoHitColor = Color.red;
	public Color gizmoSlideColor = Color.green;

	// 마지막 스윕 데이터 (OnDrawGizmos에서 읽음)
	int lastHitCount = 0;
	Vector3 lastP1, lastP2, lastDesiredMove, lastSlide;
	float lastWorldRadius = 0f;
	int lastNearestIndex = -1;

	void Awake()
	{
		rb = GetComponent<Rigidbody>();
		capsule = GetComponent<CapsuleCollider>();
		// 물리 기반 이동 시 회전은 외부로부터 고정하는 것이 일반적입니다.
		rb.freezeRotation = true;
	}

	void FixedUpdate()
	{
		// 입력 (월드 XZ 평면 기준)
		Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
		Vector3 moveDir = input.normalized;

		float speed = moveSpeed * ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? sprintMultiplier : 1f);
		Vector3 targetVel = moveDir * speed;
		currentVelocity = Vector3.MoveTowards(currentVelocity, targetVel, acceleration * Time.fixedDeltaTime);

		if (moveDir.sqrMagnitude <= Mathf.Epsilon)
		{
			currentVelocity *= inertia;
		}

		Vector3 desiredMove = currentVelocity * Time.fixedDeltaTime;
		if (desiredMove.sqrMagnitude <= Mathf.Epsilon)
			return;

		// 캡슐 캐스트를 위한 월드 좌표 두 점 계산 (캡슐의 중심 기준)
		Vector3 worldCenter = transform.TransformPoint(capsule.center);
		Vector3 up = transform.up;
		float halfHeight = Mathf.Max(0f, (capsule.height * 0.5f) - capsule.radius);
		Vector3 p1 = worldCenter + up * halfHeight;
		Vector3 p2 = worldCenter - up * halfHeight;

		float distance = desiredMove.magnitude;
		//GC를 줄이는 NonAlloc 버전 사용하여 스윕
		lastDesiredMove = desiredMove;
		lastP1 = p1; lastP2 = p2;
		lastWorldRadius = capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
		int hitCount = Physics.CapsuleCastNonAlloc(p1, p2, lastWorldRadius, desiredMove.normalized, hits, distance + _baseSkin, collisionMask, triggerInteraction);
		lastHitCount = hitCount;

		if (hitCount == 0)
		{
			// 장애물이 없으면 이동
			rb.MovePosition(rb.position + desiredMove);
			return;
		}

		// 가장 가까운 유효 히트 찾기 (자기 자신 무시)
		RaycastHit? nearest = null;
		float minDist = float.PositiveInfinity;
		for (int i = 0; i < hitCount; i++) 
		{
			var h = hits[i];
			if (h.collider == null||h.collider.gameObject == gameObject) continue;
			// 레이어 필터(안전) — CapsuleCastNonAlloc에 이미 적용되지만 추가 필터링 안전장치
			if (((1 << h.collider.gameObject.layer) & collisionMask.value) == 0) continue;
			if (h.distance < minDist && h.distance >= 0f)
			{
				minDist = h.distance;
				nearest = h;
			}
		}

		if (!nearest.HasValue)
		{
			lastNearestIndex = -1;
			// 필터링 후 충돌 없음 -> 이동
			rb.MovePosition(rb.position + desiredMove);
			return;
		}

		RaycastHit hit = nearest.Value;
		// nearest 히트 index 저장 (for gizmos)
		for (int i = 0; i < hitCount; i++)
		{
			if (hits[i].collider == hit.collider && Mathf.Approximately(hits[i].distance, hit.distance)) { lastNearestIndex = i; break; }
		}

		// 충돌면의 노멀을 이용해 슬라이딩 벡터 계산
		Vector3 slide = Vector3.ProjectOnPlane(desiredMove, hit.normal);

		// 슬라이드 방향으로 이동 가능한지 검사
		if (slide.sqrMagnitude > Mathf.Epsilon)
		{
			float slideDist = slide.magnitude;
			int slideHits = Physics.CapsuleCastNonAlloc(p1, p2, capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y), slide.normalized, hits, slideDist + _baseSkin, collisionMask, triggerInteraction);
			bool slideBlocked = false;
			for (int i = 0; i < slideHits; i++)
			{
				if (hits[i].collider == null) continue;
				if (((1 << hits[i].collider.gameObject.layer) & collisionMask.value) == 0) continue;
				slideBlocked = true; break;
			}

			if (!slideBlocked)
			{
				lastSlide = slide;
				rb.MovePosition(rb.position + slide);
				return;
			}
		}

		// 슬라이드도 막히면, 가능한 거리만큼 이동(히트 이전의 거리 - skin)
		float moveAllowed = Mathf.Max(0f, minDist - _baseSkin);
		if (moveAllowed > 0f)
		{
			Vector3 partial = desiredMove.normalized * moveAllowed;
			rb.MovePosition(rb.position + partial);
		}
		else
		{
			// 거의 붙어있으면 정지
			rb.MovePosition(rb.position);
			Debug.LogError("PlayerMovement: Stuck!");
		}
	}

	void OnDrawGizmos()
	{
		if (!drawGizmos) return;

		// 기본 캡슐 (현재 transform 기준)
		if (capsule == null) {
			capsule = GetComponent<CapsuleCollider>();
			if (capsule == null) return;
		}

		Gizmos.color = gizmoCapsuleColor;
		Vector3 wc = transform.TransformPoint(capsule.center);
		Vector3 up = transform.up;
		float halfH = Mathf.Max(0f, (capsule.height * 0.5f) - capsule.radius);
		Vector3 cp1 = wc + up * halfH;
		Vector3 cp2 = wc - up * halfH;
		// 단순한 시각화: 양 끝에 와이어구 그리기
		Gizmos.DrawWireSphere(cp1, capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y));
		Gizmos.DrawWireSphere(cp2, capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y));
		Gizmos.DrawLine(cp1 + transform.right * capsule.radius, cp2 + transform.right * capsule.radius);
		Gizmos.DrawLine(cp1 - transform.right * capsule.radius, cp2 - transform.right * capsule.radius);

		// 캐스트 시각화
		if (lastHitCount >= 0)
		{
			Gizmos.color = gizmoCastColor;
			Gizmos.DrawLine(lastP1, lastP1 + lastDesiredMove.normalized * (lastDesiredMove.magnitude + _baseSkin));

			// 히트 포인트들
			for (int i = 0; i < lastHitCount; i++)
			{
				var h = hits[i];
				if (h.collider == null) continue;
				Gizmos.color = (i == lastNearestIndex) ? gizmoHitColor : new Color(1f, 0.5f, 0.2f, 1f);
				Gizmos.DrawSphere(h.point, 0.05f);
				// 노멀
				Gizmos.color = Color.magenta;
				Gizmos.DrawLine(h.point, h.point + h.normal * 0.5f);
			}

			// 슬라이드 벡터
			if (lastSlide.sqrMagnitude > 0f)
			{
				Gizmos.color = gizmoSlideColor;
				Gizmos.DrawLine(transform.position, transform.position + lastSlide);
			}
		}
	}
}
