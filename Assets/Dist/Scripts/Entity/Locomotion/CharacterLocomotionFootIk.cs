// ============================================================
// CharacterLocomotionFootIk — logical-Floor foot plant IK (visual only)
// ============================================================

using IsoTilemap;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class CharacterLocomotionFootIk : MonoBehaviour
{
    const int FloorGridYBandHalfExtent = 1;

    [SerializeField] float _maxFootLift = 0.35f;
    [SerializeField] float _maxPelvisOffset = 0.2f;
    [SerializeField] float _footIkWeight = 1f;
    [SerializeField] float _pelvisWeight = 1f;

    Animator _animator;
    CharacterMotor _motor;
    CharacterLocomotionAnim _animation;
    CharacterVaultHost _vaultHost;
    CharacterState _characterState;
    IMapTopologyQuery _query;
    float _cellSize = 1f;
    bool _triedResolveQuery;

    /// <summary>Bootstrap / map bind — same services as motor vault (logical Floor SSOT).</summary>
    public void BindMapCollision(MapCollisionServices services)
    {
        if (services == null || services.Query == null)
        {
            _query = null;
            _cellSize = 1f;
            return;
        }

        _query = services.Query;
        _cellSize = _query.CellSize > 0f ? _query.CellSize : 1f;
        _triedResolveQuery = true;
    }

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _motor = CharacterBodyResolve.GetInBody<CharacterMotor>(this);
        _animation = CharacterBodyResolve.GetInBody<CharacterLocomotionAnim>(this);
        ResolveBodyRefs();
    }

    void ResolveBodyRefs()
    {
        if (_vaultHost != null && _characterState != null)
            return;

        Transform body = transform;
        while (body != null)
        {
            if (_vaultHost == null && body.TryGetComponent(out CharacterVaultHost vault))
                _vaultHost = vault;

            if (_characterState == null && body.TryGetComponent(out CharacterState state))
                _characterState = state;

            if (_vaultHost != null && _characterState != null)
                return;

            if (body.TryGetComponent(out CharacterBodyRoot _))
                break;

            body = body.parent;
        }

        if (_vaultHost == null)
            _vaultHost = GetComponentInParent<CharacterVaultHost>();
        if (_characterState == null)
            _characterState = GetComponentInParent<CharacterState>();
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (_animator == null)
            return;

        if (_vaultHost == null || _characterState == null)
            ResolveBodyRefs();

        // Vault busy: zero foot IK only — CharacterVaultIkHost keeps hand IK.
        if ((_vaultHost != null && _vaultHost.IsBusy) || (_motor != null && _motor.IsAirborne)
            || (_animation != null && _animation.MovementMotion == CharacterLocomotionTransitions.Motion.LandRoll))
        {
            ClearFootIk();
            return;
        }

        EnsureQuery();
        if (_query == null || _footIkWeight <= 0f)
        {
            ClearFootIk();
            return;
        }

        int feetGridY = ResolveFeetGridY();
        bool leftOk = TryApplyFoot(AvatarIKGoal.LeftFoot, feetGridY, out float leftAnimY, out float leftIkY);
        bool rightOk = TryApplyFoot(AvatarIKGoal.RightFoot, feetGridY, out float rightAnimY, out float rightIkY);

        if (!leftOk && !rightOk)
        {
            ClearFootIk();
            return;
        }

        ApplyOptionalPelvis(leftOk, rightOk, leftAnimY, leftIkY, rightAnimY, rightIkY);
    }

    void EnsureQuery()
    {
        if (_query != null || _triedResolveQuery)
            return;

        _triedResolveQuery = true;
        TileMapManager[] managers = Object.FindObjectsByType<TileMapManager>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        if (managers == null || managers.Length == 0)
            return;

        MapCollisionServices services = managers[0].MapCollisionServices;
        if (services != null)
            BindMapCollision(services);
    }

    int ResolveFeetGridY()
    {
        if (_characterState != null)
            return _characterState.GridPos.y;

        Transform body = _vaultHost != null ? _vaultHost.transform : transform.root;
        Vector3 feet = CharacterFeetPose.GetFeetWorld(body);
        return MapCollisionGrid.WorldToGridY(feet, _cellSize);
    }

    bool TryApplyFoot(AvatarIKGoal goal, int feetGridY, out float animY, out float ikY)
    {
        animY = 0f;
        ikY = 0f;

        Vector3 animPos = _animator.GetIKPosition(goal);
        animY = animPos.y;

        if (!TrySampleFloorSurfaceY(animPos.x, animPos.z, feetGridY, out float surfaceY))
        {
            _animator.SetIKPositionWeight(goal, 0f);
            return false;
        }

        float delta = surfaceY - animY;
        float clampedDelta = Mathf.Clamp(delta, -_maxFootLift, _maxFootLift);
        ikY = animY + clampedDelta;

        var target = new Vector3(animPos.x, ikY, animPos.z);
        _animator.SetIKPosition(goal, target);
        _animator.SetIKPositionWeight(goal, _footIkWeight);
        return true;
    }

    bool TrySampleFloorSurfaceY(float worldX, float worldZ, int feetGridY, out float surfaceY)
    {
        surfaceY = 0f;

        // XZ from foot bone; Y band centered on character feet gridY (logical Floor).
        var xzAtFeet = new Vector3(worldX, feetGridY * _cellSize, worldZ);
        Vector3Int cell = TileHelper.ConvertWorldToGrid(xzAtFeet, _cellSize);

        bool found = false;
        int bestAbsDy = int.MaxValue;
        float bestSurface = 0f;

        for (int dy = -FloorGridYBandHalfExtent; dy <= FloorGridYBandHalfExtent; dy++)
        {
            int gridY = feetGridY + dy;
            if (!_query.CellHasFloor(cell.x, cell.z, gridY))
                continue;

            float candidate = MapCollisionGrid.GridYToSurfaceY(gridY, _cellSize);
            int absDy = dy < 0 ? -dy : dy;
            if (!found || absDy < bestAbsDy)
            {
                found = true;
                bestAbsDy = absDy;
                bestSurface = candidate;
            }
        }

        if (!found)
            return false;

        surfaceY = bestSurface;
        return true;
    }

    void ApplyOptionalPelvis(
        bool leftOk,
        bool rightOk,
        float leftAnimY,
        float leftIkY,
        float rightAnimY,
        float rightIkY)
    {
        if (_pelvisWeight <= 0f || _maxPelvisOffset <= 0f)
            return;

        float maxRaise = 0f;
        if (leftOk)
            maxRaise = Mathf.Max(maxRaise, leftIkY - leftAnimY);
        if (rightOk)
            maxRaise = Mathf.Max(maxRaise, rightIkY - rightAnimY);

        if (maxRaise <= 0f)
            return;

        float pelvisDown = Mathf.Min(maxRaise, _maxPelvisOffset) * _pelvisWeight;
        Vector3 body = _animator.bodyPosition;
        body.y -= pelvisDown;
        _animator.bodyPosition = body;
    }

    void ClearFootIk()
    {
        _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
        _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
    }
}
