// ============================================================
// DistProjectile — 발사체 엔티티 (명중·피해·관통 소유)
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DistProjectile : MonoBehaviour
{
    public const float DefaultSpeed = 28f;
    public const float DefaultMaxLifetime = 3f;
    const int MaxHitHistory = 8;

    [SerializeField] float _speed = DefaultSpeed;
    [SerializeField] float _maxLifetime = DefaultMaxLifetime;
    [SerializeField] TimeScaleChannel _timeChannel = TimeScaleChannel.World;

    CharacterAttacker _attacker;
    ActionHandlerContext _context;
    ItemData _item;
    ItemData _ammo;
    Vector3 _direction;
    float _rangeRemaining;
    float _lifeRemaining;
    int _pierceRemaining;
    float _impulseRemaining;
    float _rangedEffectiveDispersion;
    LayerMask _obstructionMask;
    MapTopologyLineCast _mapLineCast;
    float _mapFeetY;
    bool _launched;
    readonly CharacterBodyHost[] _hitHosts = new CharacterBodyHost[MaxHitHistory];
    int _hitCount;

    public void Launch(
        CharacterAttacker attacker,
        in ActionHandlerContext context,
        ItemData item,
        ItemData ammo,
        Vector3 origin,
        Vector3 direction,
        float range,
        int pierce,
        LayerMask obstructionMask,
        float rangedEffectiveDispersion)
    {
        if (attacker == null || direction.sqrMagnitude < 1e-8f)
        {
            Destroy(gameObject);
            return;
        }

        _attacker = attacker;
        _context = context;
        _item = item;
        _ammo = ammo;
        _direction = direction.normalized;
        _rangeRemaining = Mathf.Max(0f, range);
        _lifeRemaining = Mathf.Max(0.01f, _maxLifetime);
        _pierceRemaining = Mathf.Max(0, pierce);
        _impulseRemaining = CombatImpulse.ShotJin(item, ammo);
        _obstructionMask = obstructionMask;
        _rangedEffectiveDispersion = rangedEffectiveDispersion;
        _mapLineCast = attacker.MapLineCast;
        _mapFeetY = CharacterFeetPose.GetFeetWorld(attacker.transform).y;
        _hitCount = 0;
        _launched = true;
        transform.SetPositionAndRotation(
            origin,
            Quaternion.LookRotation(_direction, Vector3.up));
    }

    void Update()
    {
        if (!_launched)
            return;

        // 할당 없음. MapTopologyLineCast는 스택 샘플.
        float dt = TimeScaleService.Delta(_timeChannel);
        if (dt <= 0f)
            return;

        _lifeRemaining -= dt;
        if (_lifeRemaining <= 0f || _rangeRemaining <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        float step = Mathf.Min(_speed * dt, _rangeRemaining);
        Vector3 origin = transform.position;
        if (TryMapObstruction(origin, step, out float mapDist, out Vector3 mapImpact))
        {
            EmitObstructionMiss(origin, mapImpact);
            Destroy(gameObject);
            return;
        }

        if (step > CharacterAttacker.MinRayDistance &&
            Physics.Raycast(
                origin,
                _direction,
                out RaycastHit hit,
                step,
                _obstructionMask,
                QueryTriggerInteraction.Ignore))
        {
            if (TryResolveBody(hit.collider, out CharacterBodyHost host))
            {
                if (ApplyBodyHit(host, origin))
                    return;
            }
            else if (!IsSelf(hit.collider))
            {
                EmitObstructionMiss(origin, hit.point);
                Destroy(gameObject);
                return;
            }
        }

        transform.position = origin + _direction * step;
        _rangeRemaining -= step;
    }

    bool ApplyBodyHit(CharacterBodyHost host, Vector3 origin)
    {
        if (host == null || AlreadyHit(host))
            return false;

        RememberHit(host);
        var hitContext = new ActionHandlerContext(
            _context.Leaf,
            _context.Hand,
            _context.Attack,
            host,
            _context.OffenseFactor,
            _context.ItemId,
            _context.Instance,
            _context.Stack);
        float p = _attacker.ResolveCommittedHit(
            hitContext,
            WeaponResolveMode.RangedRay,
            _item,
            origin,
            consumeAmmo: false,
            _ammo,
            rangedEffectiveDispersion: _rangedEffectiveDispersion,
            impulseJinOverride: _impulseRemaining,
            impulseContinues: _pierceRemaining > 0);
        _impulseRemaining = CombatImpulse.ExitJin(_impulseRemaining, p);

        if (_pierceRemaining <= 0 || _impulseRemaining < CombatImpulse.MinContinueJin)
        {
            Destroy(gameObject);
            return true;
        }

        _pierceRemaining--;
        return false;
    }

    void EmitObstructionMiss(Vector3 origin, Vector3 impact)
    {
        if (_attacker == null)
            return;
        _attacker.EmitJudged(
            _context,
            WeaponResolveMode.RangedRay,
            AttackPerformResult.Obstructed,
            _context.Target,
            string.Empty,
            0,
            origin,
            impact);
    }

    bool TryMapObstruction(
        Vector3 origin,
        float step,
        out float mapDist,
        out Vector3 mapImpact)
    {
        mapDist = step;
        mapImpact = origin + _direction * step;
        if (_mapLineCast == null || step <= CharacterAttacker.MinRayDistance)
            return false;

        Vector3 feet = origin;
        feet.y = _mapFeetY;
        if (!_mapLineCast.TryGetBlockingDistance(feet, _direction, step, out mapDist))
            return false;

        mapImpact = origin + _direction * mapDist;
        return true;
    }

    bool TryResolveBody(Collider collider, out CharacterBodyHost host)
    {
        host = null;
        if (collider == null || IsSelf(collider))
            return false;
        return CharacterBodyResolve.TryResolveBodyHost(collider, out host);
    }

    bool IsSelf(Collider collider) =>
        _attacker != null && CharacterBodyResolve.IsColliderOnBody(_attacker, collider);

    bool AlreadyHit(CharacterBodyHost host)
    {
        for (int i = 0; i < _hitCount; i++)
        {
            if (ReferenceEquals(_hitHosts[i], host))
                return true;
        }

        return false;
    }

    void RememberHit(CharacterBodyHost host)
    {
        if (_hitCount >= _hitHosts.Length)
            return;
        _hitHosts[_hitCount++] = host;
    }
}
