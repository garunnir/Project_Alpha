using UnityEngine;
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerMovement : MonoBehaviour
{
	public float moveSpeed = 5f;
	public float sprintMultiplier = 2f;
	public float acceleration = 10f;
	[Tooltip("관성(감쇠) 계수. 0에 가까울수록 미끄러지듯 멈춤, 1에 가까울수록 즉시 멈춤")]
	[Range(0f, 1f)]
	public float inertia = 0.9f;
	private Vector3 currentVelocity = Vector3.zero;
	Rigidbody2D rb;
	Collider2D col;
	ContactFilter2D filter;
	RaycastHit2D[] hits = new RaycastHit2D[4];
	public float skin = 0.02f;              // 벽에 바짝 붙을 때 여유
	public LayerMask collisionMask;         // Wall(및 막아야 할 레이어)

	void Awake()
	{
		rb = GetComponent<Rigidbody2D>();
		col = GetComponent<Collider2D>();

		filter = new ContactFilter2D
		{
			useLayerMask = true,
			layerMask = collisionMask,
			useTriggers = false
		};
	}

	void Update()
	{




		Vector3 targetVelocity = moveInput * speed;
		currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, acceleration * Time.deltaTime);

		// 관성 적용: 입력이 없을 때 점진적으로 멈춤


		transform.position += transform.TransformDirection(currentVelocity) * Time.deltaTime;
	}
	void FixedUpdate()
	{
		Vector2 input = new Vector2(
			Input.GetAxisRaw("Horizontal"),
			Input.GetAxisRaw("Vertical")
		).normalized;

				float speed = moveSpeed;
		if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
		{
			speed *= sprintMultiplier;
		}

		if (input == Vector2.zero)
		{
			currentVelocity *= inertia;
		}


		Vector2 delta = input * speed * Time.fixedDeltaTime;
		if (delta == Vector2.zero) return;

		// 1) 진행 방향으로 스윕 캐스트
		int hitCount = col.Cast(delta.normalized, filter, hits, delta.magnitude + skin);

		if (hitCount > 0)
		{
			// 가장 가까운 히트만 고려
			float minDist = float.MaxValue;
			for (int i = 0; i < hitCount; i++)
				minDist = Mathf.Min(minDist, hits[i].distance);

			// 2) 충돌 직전까지 이동 (skin만큼 띄우기)
			Vector2 move = delta.normalized * Mathf.Max(0f, minDist - skin);
			rb.MovePosition(rb.position + move);

			// 3) 슬라이딩(선택): 접촉 노말로 미끄러짐 구현
			// 접촉 노말을 제거한 접선 성분으로 한번 더 이동하면 코너에서 부드럽게 돈다.
			Vector2 normal = hits[0].normal; // 가장 가까운 히트의 노말
			Vector2 tangent = delta - Vector2.Dot(delta, normal) * normal;
			if (tangent.sqrMagnitude > 1e-4f)
			{
				int hits2 = col.Cast(tangent.normalized, filter, hits, tangent.magnitude + skin);
				float min2 = tangent.magnitude;
				for (int i = 0; i < hits2; i++) min2 = Mathf.Min(min2, hits[i].distance);
				rb.MovePosition(rb.position + tangent.normalized * Mathf.Max(0f, min2 - skin));
			}
		}
		else
		{
			// 충돌 없음 → 정상 이동
			rb.MovePosition(rb.position + delta);
		}
	}
}
