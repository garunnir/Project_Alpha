// ============================================================
// KinematicMover — 순수 C# 캡슐 이동 로직 (속도 계산 + 충돌 해결)
// ============================================================
using UnityEngine;

public class KinematicMover
{
    public float Acceleration;
    public float Inertia;
    public float BaseSkin;
    public LayerMask CollisionMask;
    public QueryTriggerInteraction TriggerInteraction;

    // Gizmo / debug용 read-only 상태
    public Vector3 LastSlide { get; private set; }
    public int LastNearestIndex { get; private set; } = -1;
    public Vector3 WorldMoveDir => _moveDir;
    public bool IsSprinting => _isSprinting;
    public float CurrentSpeed => _currentVelocity.magnitude;
    public bool IsInertiaActive { get; private set; }

    Vector3 _moveDir;
    Vector3 _currentVelocity;
    internal Vector3 Velocity => _currentVelocity;
    bool _brakingTurn;

    // Called after the drive proposes a velocity, before collision and separate vertical gravity.
    internal Vector3 ApplyTurnMomentum(Vector3 previousVelocity, float dt, bool airborne,
        float pivotScale, float walkSpeed, float braking, float airAcceleration)
    {
        if (dt <= 0f) { _currentVelocity = previousVelocity; return Vector3.zero; }
        Vector3 target = _currentVelocity * Mathf.Clamp01(pivotScale);
        if (airborne)
        {
            _brakingTurn = false;
            _currentVelocity = _moveDir.sqrMagnitude <= 0.0001f ? previousVelocity
                : Vector3.MoveTowards(previousVelocity, target, Mathf.Max(0f, airAcceleration) * dt);
        }
        else
        {
            bool opposed = _moveDir.sqrMagnitude > 0.0001f
                && Vector3.Dot(previousVelocity.normalized, _moveDir.normalized) < -0.5f;
            _brakingTurn = opposed && (_brakingTurn || previousVelocity.magnitude > walkSpeed + 0.1f);
            if (_brakingTurn || pivotScale < 0.999f)
            {
                _currentVelocity = Vector3.MoveTowards(previousVelocity,
                    _brakingTurn ? Vector3.zero : target, Mathf.Max(0.01f, braking) * dt);
                if (_currentVelocity.sqrMagnitude < 0.0001f) _brakingTurn = false;
            }
        }
        IsInertiaActive = _currentVelocity.magnitude > walkSpeed;
        return _currentVelocity * dt;
    }
    bool _isSprinting;

    public void SetInput(Vector2 input, Camera camera)
    {
        SetWorldDirection(CameraRelativeInput(input, camera));
    }

    public void SetWorldDirection(Vector3 worldDirection)
    {
        Vector3 flatDirection = new Vector3(worldDirection.x, 0f, worldDirection.z);
        _moveDir = flatDirection.sqrMagnitude > 1f
            ? flatDirection.normalized
            : flatDirection;
    }

    public void SetSprinting(bool value) => _isSprinting = value;
    public void ApplySpeedBoost(float boostAmount, float maxSpeed)
    {
        float boost = Mathf.Max(0f, boostAmount);
        if (boost <= Mathf.Epsilon) return;

        float speedCap = Mathf.Max(0f, maxSpeed);
        Vector3 direction = _currentVelocity.sqrMagnitude > Mathf.Epsilon
            ? _currentVelocity.normalized
            : (_moveDir.sqrMagnitude > Mathf.Epsilon ? _moveDir.normalized : Vector3.zero);
        if (direction == Vector3.zero) return;

        float boostedSpeed = Mathf.Min(_currentVelocity.magnitude + boost, speedCap);
        _currentVelocity = direction * boostedSpeed;
    }

    public void SetInitialVelocity(float initialSpeed)
    {
        float clampedSpeed = Mathf.Max(0f, initialSpeed);
        if (_moveDir.sqrMagnitude <= Mathf.Epsilon || clampedSpeed <= Mathf.Epsilon)
        {
            _currentVelocity = Vector3.zero;
            return;
        }

        _currentVelocity = _moveDir.normalized * clampedSpeed;
    }

    // 반환값: 이번 FixedUpdate에서 이동할 Vector3 (호출측 dt = Player FixedDelta 이미 반영)
    public Vector3 CalcDesiredMove(
        float baseSpeed,
        float sprintMultiplier,
        float dt,
        float customBaseSpeed,
        float inertiaEnableThreshold,
        float runMaxSpeed)
    {
        float baseThresholdSpeed = Mathf.Max(baseSpeed + 0.01f, customBaseSpeed);
        float thresholdSpeed = Mathf.Max(inertiaEnableThreshold, baseThresholdSpeed + 0.01f);
        float clampedRunMaxSpeed = Mathf.Max(runMaxSpeed, thresholdSpeed);

        float walkSpeed = baseSpeed;
        float sprintTopSpeed = baseSpeed * sprintMultiplier;
        bool hasInput = _moveDir.sqrMagnitude > Mathf.Epsilon;
        float currentSpeed = _currentVelocity.magnitude;

        // 목표 이속 0(불균형 풀)·입력 없음 → 관성 감속만. 스프린트 가속 이어가기 금지.
        if (!hasInput || walkSpeed <= Mathf.Epsilon)
            return CoastMove(dt, baseThresholdSpeed);

        Vector3 direction = _moveDir.normalized;
        float desiredSpeed;
        if (_isSprinting)
        {
            // 달리기는 "현재속도 + 가속분"을 우선하고 상한(runMaxSpeed)만 적용
            float acceleratedSpeed = currentSpeed + (Acceleration * dt);
            desiredSpeed = Mathf.Clamp(acceleratedSpeed, walkSpeed, clampedRunMaxSpeed);
        }
        else
        {
            desiredSpeed = walkSpeed;
        }

        // 관성 진입은 임계 초과, 해제는 기준속도 구간 복귀 시점으로 분리
        if (currentSpeed >= thresholdSpeed)
            IsInertiaActive = true;
        else if (currentSpeed <= baseThresholdSpeed)
            IsInertiaActive = false;

        Vector3 targetVelocity = direction * desiredSpeed;
        _currentVelocity = IsInertiaActive
            ? Vector3.MoveTowards(_currentVelocity, targetVelocity, Acceleration * dt)
            : targetVelocity;

        return _currentVelocity * dt;
    }

    public Vector3 CalcConstantSpeedMove(float speed, float deltaTime)
    {
        float clampedSpeed = Mathf.Max(0f, speed);
        if (deltaTime <= 0f)
            return Vector3.zero;

        // 이속 0·입력 없음 → 스냅 0 금지, 관성 미끄러짐(자빠짐).
        if (_moveDir.sqrMagnitude <= Mathf.Epsilon || clampedSpeed <= Mathf.Epsilon)
            return CoastMove(deltaTime, 0.01f);

        _currentVelocity = _moveDir.normalized * clampedSpeed;
        IsInertiaActive = false;
        return _currentVelocity * deltaTime;
    }

    Vector3 CoastMove(float dt, float baseThresholdSpeed)
    {
        float currentSpeed = _currentVelocity.magnitude;
        float damp = Inertia > 1e-4f ? Inertia : 0.85f;
        IsInertiaActive = currentSpeed > baseThresholdSpeed;
        _currentVelocity = IsInertiaActive ? _currentVelocity * damp : Vector3.zero;
        if (_currentVelocity.sqrMagnitude < 1e-6f)
            _currentVelocity = Vector3.zero;
        return _currentVelocity * dt;
    }

    // desired: 이동하고 싶은 벡터
    // p1/p2:   CapsuleCast용 월드 좌표 두 점
    // radius:  스케일 반영된 캡슐 반지름
    // hitBuffer: 첫 번째 CapsuleCast 결과 배열 (슬라이드 검사 시 재사용됨)
    // hitCount: 첫 번째 캐스트에서 나온 히트 수
    // self:    무시할 자기 자신의 Collider
    // 반환값: 실제로 이동할 delta. Vector3.zero == 완전히 막힘(stuck)
    public Vector3 ResolveMove(Vector3 desired, Vector3 p1, Vector3 p2, float radius,
                               RaycastHit[] hitBuffer, int hitCount, Collider self)
    {
        LastSlide = Vector3.zero;
        LastNearestIndex = -1;

        RaycastHit? nearest = FindNearest(hitBuffer, hitCount, self, out float minDist, out int nearestIdx);
        if (!nearest.HasValue)
            return desired;

        LastNearestIndex = nearestIdx;
        Vector3 slide = Vector3.ProjectOnPlane(desired, nearest.Value.normal);

        if (slide.sqrMagnitude > Mathf.Epsilon)
        {
            // hitBuffer를 슬라이드 검사에 재사용 (첫 번째 캐스트 결과는 더 이상 필요 없음)
            int slideCount = Physics.CapsuleCastNonAlloc(
                p1, p2, radius, slide.normalized, hitBuffer,
                slide.magnitude + BaseSkin, CollisionMask, TriggerInteraction);

            if (!IsBlocked(hitBuffer, slideCount, self))
            {
                LastSlide = slide;
                return slide;
            }
        }

        float allowed = Mathf.Max(0f, minDist - BaseSkin);
        return allowed > 0f ? desired.normalized * allowed : Vector3.zero;
    }

    static Vector3 CameraRelativeInput(Vector2 input, Camera camera)
    {
        Camera cam = camera != null ? camera : Camera.main;
        if (cam == null) return new Vector3(input.x, 0f, input.y);

        Transform t = cam.transform;
        Vector3 forward = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
        Vector3 right   = Vector3.ProjectOnPlane(t.right,   Vector3.up).normalized;
        Vector3 dir = right * input.x + forward * input.y;
        if (dir.sqrMagnitude > 1f) dir.Normalize();
        return new Vector3(dir.x, 0f, dir.z);
    }

    static RaycastHit? FindNearest(RaycastHit[] hits, int count, Collider self,
                                   out float minDist, out int nearestIdx)
    {
        minDist = float.PositiveInfinity;
        nearestIdx = -1;
        RaycastHit? nearest = null;
        for (int i = 0; i < count; i++)
        {
            var h = hits[i];
            if (h.collider == null || h.collider == self) continue;
            if (h.distance < minDist && h.distance >= 0f)
            {
                minDist = h.distance;
                nearest = h;
                nearestIdx = i;
            }
        }
        return nearest;
    }

    static bool IsBlocked(RaycastHit[] hits, int count, Collider self)
    {
        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider == null || hits[i].collider == self) continue;
            return true;
        }
        return false;
    }
}
