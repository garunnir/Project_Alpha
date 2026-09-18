// ============================================================
// CharacterLocomotion — 캐릭터 공용 캡슐·맵 토폴로지 이동을 해결
// ============================================================

using System;
using IsoTilemap;
using UnityEngine;

public interface ICharacterLocomotion
{
    void BindMapCollision(MapCollisionServices services);
    void SetDesiredWorldDir(Vector3 worldDirXZ);
    void SetSpeed(float metersPerSecond);
    bool IsStuck { get; }
    float CurrentSpeed { get; }
    /// <summary>애니 Speed 정규화 분모 — PC/NPC 공통 달리기 상한(m/s).</summary>
    float AnimSpeedReference { get; }
}

public static class CharacterLocomotionDefaults
{
    public const float DefaultWalkSpeedMeters = 3f;
    public const float DefaultRunMaxSpeedMeters = 12f;
    public const int HitBufferSize = 8;
    public const float ClimbAllowance = 0.3f;
    public const float BaseSkin = 0.02f;
    public const int AllCollisionLayers = ~0;
    public const float LogicalGravity = -9.81f;
    public const float TopologyPushSpeed = 4f;
    public const int TopologyPushMaxIterations = 4;
}

/// <summary>
/// CharacterMotor Collision Inspector SSOT.
/// 기본값은 <see cref="CharacterLocomotionDefaults"/>.
/// </summary>
[Serializable]
public struct CharacterLocomotionCollisionSettings
{
    [Min(0f)] public float ClimbAllowance;
    [Min(0f)] public float BaseSkin;
    [Tooltip("WalkableOnly 소품·경사 등 Physics 충돌 레이어")]
    public LayerMask CollisionMask;
    public QueryTriggerInteraction TriggerInteraction;
    [Tooltip("논리 낙하 중력 (useGravity 대신 사용)")]
    public float LogicalGravity;
    [Tooltip("topology 벽 셀 끼임 탈출 push 속도")]
    [Min(0f)] public float TopologyPushSpeed;
    [Tooltip("같은 FixedUpdate 내 topology 탈출 push 최대 반복")]
    [Min(1)] public int TopologyPushMaxIterations;

    public static CharacterLocomotionCollisionSettings Default => new CharacterLocomotionCollisionSettings
    {
        ClimbAllowance = CharacterLocomotionDefaults.ClimbAllowance,
        BaseSkin = CharacterLocomotionDefaults.BaseSkin,
        CollisionMask = CharacterLocomotionDefaults.AllCollisionLayers,
        TriggerInteraction = QueryTriggerInteraction.Ignore,
        LogicalGravity = CharacterLocomotionDefaults.LogicalGravity,
        TopologyPushSpeed = CharacterLocomotionDefaults.TopologyPushSpeed,
        TopologyPushMaxIterations = CharacterLocomotionDefaults.TopologyPushMaxIterations,
    };
}

public sealed class CharacterLocomotion
{
    readonly Rigidbody _rigidbody;
    readonly CapsuleCollider _capsule;
    readonly Transform _transform;
    readonly CharacterState _characterState;
    readonly KinematicMover _mover;
    readonly RaycastHit[] _hits;
    readonly float _climbAllowance;
    readonly float _baseSkin;
    readonly LayerMask _collisionMask;
    readonly QueryTriggerInteraction _triggerInteraction;
    readonly float _logicalGravity;
    readonly float _topologyPushSpeed;
    readonly int _topologyPushMaxIterations;

    MapCollisionServices _mapCollision;
    MapTopologyDepenetration.Tracker _gridStuckTracker;
    float _verticalVelocity;
    internal bool IsAirborne => _mapCollision != null && Mathf.Abs(_verticalVelocity) > 0.0001f;
    internal uint LandingSequence { get; private set; }
    internal float LandingSpeed { get; private set; }
    internal float LandingAirTime { get; private set; }
    float _airTime;
    CharacterSwimHost _swimHost;

    public int LastHitCount { get; private set; }
    public Vector3 LastCapsulePoint { get; private set; }
    public Vector3 LastDesiredMove { get; private set; }
    public bool LastPhysicsStuck { get; private set; }
    public MapTopologyDepenetration.PushOutResult LastTopologyPush { get; private set; }
    public bool IsStuck =>
        LastPhysicsStuck ||
        (LastTopologyPush.WasBlocking && LastTopologyPush.StillBlocking);

    public CharacterLocomotion(
        Rigidbody rigidbody,
        CapsuleCollider capsule,
        Transform transform,
        CharacterState characterState,
        KinematicMover mover,
        RaycastHit[] hits,
        float climbAllowance,
        float baseSkin,
        LayerMask collisionMask,
        QueryTriggerInteraction triggerInteraction,
        float logicalGravity,
        float topologyPushSpeed,
        int topologyPushMaxIterations)
    {
        _rigidbody = rigidbody;
        _capsule = capsule;
        _transform = transform;
        _characterState = characterState;
        _mover = mover;
        _hits = hits;
        _climbAllowance = climbAllowance;
        _baseSkin = baseSkin;
        _collisionMask = collisionMask;
        _triggerInteraction = triggerInteraction;
        _logicalGravity = logicalGravity;
        _topologyPushSpeed = topologyPushSpeed;
        _topologyPushMaxIterations = topologyPushMaxIterations;
        if (_transform != null)
            _transform.TryGetComponent(out _swimHost);
    }

    public void BindMapCollision(MapCollisionServices services) => _mapCollision = services;

    public Vector3 Move(Vector3 desiredMove, float deltaTime)
    {
        LastDesiredMove = desiredMove;

        Vector3 oldPosition = _rigidbody.position;
        float feetOffset = CharacterFeetPose.GetFeetOffset(_transform);

        Vector3Int footprint = ResolveGridFootprint();

        LastPhysicsStuck = ResolvePhysicsHorizontal(desiredMove, out Vector3 horizontalDelta);
        Vector3 newPosition = ApplyTopologyHorizontal(oldPosition, feetOffset, horizontalDelta, footprint);

        float cellSize = _mapCollision != null ? _mapCollision.Query.CellSize : 1f;
        MapCollisionGrid.FeetCell feetCell =
            MapCollisionGrid.ResolveFeetCell(newPosition, feetOffset, cellSize);
        ApplyLogicalVertical(ref newPosition, feetOffset, deltaTime, ref feetCell, footprint);

        LastTopologyPush = ResolveGridStuck(
            ref newPosition,
            feetOffset,
            ref feetCell,
            footprint,
            deltaTime);

        _rigidbody.MovePosition(newPosition);
        _rigidbody.linearVelocity = Vector3.zero;
        _characterState.UpdateGridPos(_transform.position);
        return horizontalDelta;
    }

    bool ResolvePhysicsHorizontal(Vector3 desiredMove, out Vector3 horizontalDelta)
    {
        horizontalDelta = Vector3.zero;
        if (desiredMove.sqrMagnitude <= Mathf.Epsilon)
        {
            LastHitCount = 0;
            return false;
        }

        Vector3 worldCenter = _transform.TransformPoint(_capsule.center);
        Vector3 up = _transform.up;
        float halfHeight = Mathf.Max(0f, (_capsule.height * 0.5f) - _capsule.radius);
        Vector3 p1 = worldCenter + up * halfHeight;
        Vector3 p2 = worldCenter - up * (halfHeight - _climbAllowance);
        float radius = _capsule.radius *
            Mathf.Max(_transform.lossyScale.x, _transform.lossyScale.y);

        int hitCount = Physics.CapsuleCastNonAlloc(
            p1,
            p2,
            radius,
            desiredMove.normalized,
            _hits,
            desiredMove.magnitude + _baseSkin,
            _collisionMask,
            _triggerInteraction);

        LastCapsulePoint = p1;
        LastHitCount = hitCount;

        if (hitCount == 0)
        {
            horizontalDelta = desiredMove;
            return false;
        }

        horizontalDelta = _mover.ResolveMove(
            desiredMove,
            p1,
            p2,
            radius,
            _hits,
            hitCount,
            _capsule);
        return horizontalDelta.sqrMagnitude <= Mathf.Epsilon;
    }

    Vector3Int ResolveGridFootprint() =>
        _characterState != null
            ? _characterState.GridFootprint
            : CharacterGridFootprintDefaults.Default;

    Vector3 ApplyTopologyHorizontal(
        Vector3 oldPosition,
        float feetOffset,
        Vector3 horizontalDelta,
        Vector3Int footprint)
    {
        if (_mapCollision == null)
            return oldPosition + horizontalDelta;

        Vector3 feetWorld = CharacterFeetPose.GetFeetWorld(oldPosition, feetOffset);
        Vector3 topologyDelta = horizontalDelta;
        if (horizontalDelta.sqrMagnitude > Mathf.Epsilon)
        {
            topologyDelta = _mapCollision.CollisionResolver.ClampHorizontal(
                feetWorld,
                horizontalDelta,
                footprint);
        }

        float cellSize = _mapCollision.Query.CellSize;
        int gridY = MapCollisionGrid.WorldToGridY(feetWorld, cellSize);
        Vector3 targetFeet = feetWorld + new Vector3(topologyDelta.x, 0f, topologyDelta.z);
        targetFeet = MapTopologyCapsuleMargin.ClampHorizontal(
            _mapCollision.Query,
            targetFeet,
            gridY,
            footprint,
            ResolveHorizontalCapsuleRadius(),
            _baseSkin);

        return oldPosition + new Vector3(
            targetFeet.x - feetWorld.x,
            topologyDelta.y,
            targetFeet.z - feetWorld.z);
    }

    float ResolveHorizontalCapsuleRadius() =>
        _capsule.radius * Mathf.Max(_transform.lossyScale.x, _transform.lossyScale.y);

    MapTopologyDepenetration.PushOutResult ResolveGridStuck(
        ref Vector3 bodyPosition,
        float feetOffset,
        ref MapCollisionGrid.FeetCell feetCell,
        Vector3Int footprint,
        float deltaTime)
    {
        if (_mapCollision == null)
            return MapTopologyDepenetration.PushOutResult.None;

        return _mapCollision.Depenetration.TryResolveGridStuck(
            ref bodyPosition,
            feetOffset,
            ref feetCell,
            ref _gridStuckTracker,
            footprint,
            _topologyPushSpeed,
            _topologyPushMaxIterations,
            deltaTime);
    }

    void ApplyLogicalVertical(
        ref Vector3 worldPosition,
        float feetOffset,
        float deltaTime,
        ref MapCollisionGrid.FeetCell feetCell,
        Vector3Int footprint)
    {
        if (_mapCollision == null)
            return;

        if (_characterState != null &&
            (_characterState.IsDiving || _characterState.IsSwimming))
        {
            ApplySwimVertical(ref worldPosition, feetOffset, deltaTime, ref feetCell);
            return;
        }

        float impactSpeed = Mathf.Max(0f, -(_verticalVelocity + _logicalGravity * deltaTime));
        bool wasAirborne = IsAirborne;
        _mapCollision.FloorSupport.ApplyVertical(
            ref worldPosition,
            ref _verticalVelocity,
            deltaTime,
            feetOffset,
            ref feetCell,
            ref _gridStuckTracker,
            footprint,
            _logicalGravity);
        if (IsAirborne)
            _airTime += deltaTime;
        else
        {
            if (wasAirborne)
            {
                LandingSpeed = impactSpeed;
                LandingAirTime = _airTime;
                unchecked { LandingSequence++; }
            }
            _airTime = 0f;
        }
    }

    void ApplySwimVertical(
        ref Vector3 worldPosition,
        float feetOffset,
        float deltaTime,
        ref MapCollisionGrid.FeetCell feetCell)
    {
        float cellSize = _mapCollision.Query.CellSize;
        MapSwimImmersion immersion = _swimHost != null
            ? _swimHost.LastImmersion
            : MapSwimQuery.Resolve(
                feetCell.FeetWorld,
                cellSize,
                _characterState != null && _characterState.IsDiving,
                _characterState != null ? _characterState.GridFootprint : default);

        float surfaceY = immersion.SurfaceFeetY;
        float bottomY = immersion.ColumnBottomFeetY;
        if (surfaceY < bottomY)
            surfaceY = bottomY;

        float vertical = _characterState != null ? _characterState.SwimVerticalInput : 0f;
        float nextFeetY = feetCell.FeetY + vertical * MapSwimConsts.DiveVerticalSpeed * deltaTime;
        nextFeetY = Mathf.Clamp(nextFeetY, bottomY, surfaceY);
        _verticalVelocity = 0f;
        _airTime = 0f;
        worldPosition.y = nextFeetY + feetOffset;
        feetCell = MapCollisionGrid.WithFeetY(feetCell, nextFeetY, cellSize);
    }
}
