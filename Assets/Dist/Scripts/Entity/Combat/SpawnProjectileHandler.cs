// ============================================================
// SpawnProjectileHandler — spawn_projectile: Attack 프리팹이면 비행, 없으면 히트스캔
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

public sealed class SpawnProjectileHandler : IActionHandler
{
    readonly RaycastHit[] _hits = new RaycastHit[CombatHitscan.BufferSize];
    readonly Collider[] _overlaps = new Collider[CombatHitscan.BufferSize];
    readonly CharacterBodyHost[] _hosts = new CharacterBodyHost[CombatHitscan.BufferSize];
    readonly Vector3[] _impacts = new Vector3[CombatHitscan.BufferSize];

    public string LogicId => ActionHandlerIds.SpawnProjectile;

    public void Execute(CharacterAttacker attacker, in ActionHandlerContext context)
    {
        if (attacker == null)
            return;

        ItemData item = attacker.ItemFor(context.ItemId);
        WeaponResolveMode mode = WeaponResolveMode.RangedRay;
        Vector3 origin = attacker.ResolveOrigin();

        if (attacker.GetWeaponCooldown(context.Hand) > 0f)
        {
            attacker.EmitJudgedGate(context, mode, AttackPerformResult.Cooling, item, origin);
            return;
        }

        ItemData ammoProbe = WeaponChamber.ResolveAmmo(context.Stack, context.Instance);
        Vector3 feet = CharacterFeetPose.GetFeetWorld(attacker.transform);
        float range = CombatHitscan.EffectiveRange(item, context.Leaf, ammoProbe, origin, feet);

        int shots = CombatLeafUtil.ShotsPerPerform(context.Leaf, item);
        if (shots < 1)
            shots = 1;

        int fired = 0;
        for (int s = 0; s < shots; s++)
        {
            if (!WeaponChamber.EnsureChamberForFire(
                    context.Instance, context.Stack, item, context.Attack))
            {
                if (fired == 0)
                    attacker.EmitJudgedGate(context, mode, AttackPerformResult.NoAmmo, item, origin);
                break;
            }

            ItemData ammo = WeaponChamber.ResolveAmmo(context.Stack, context.Instance);
            if (!FireOne(attacker, context, item, ammo, origin, range, mode))
                break;
            fired++;
        }

        if (fired > 0)
        {
            attacker.CommitAttempt(
                context,
                item,
                consumeAmmo: false,
                WeaponChamber.ResolveAmmo(context.Stack, context.Instance),
                applyCooldown: true,
                practice: false);
        }
    }

    bool FireOne(
        CharacterAttacker attacker,
        in ActionHandlerContext context,
        ItemData item,
        ItemData ammo,
        Vector3 origin,
        float range,
        WeaponResolveMode mode)
    {
        float effective = attacker.RangedEffectiveDispersion(context.Hand, item, ammo);
        Vector3 dir = CombatMath.SpreadFireDirection(attacker.ResolveFireDirection(), effective);

        attacker.CommitAttempt(
            context, item, consumeAmmo: true, ammo, applyCooldown: false, practice: true);

        if (TryLaunchFlight(attacker, context, item, ammo, origin, dir, range, effective))
        {
            attacker.AddRecoilKick(context.Hand, item, ammo);
            return true;
        }

        int pierce = WeaponChamber.ResolvePierce(context.Stack, context.Instance);
        CombatHitscan.Trace(
            attacker,
            origin,
            dir,
            range,
            attacker.RangedObstructionMask,
            pierce,
            _hits,
            _overlaps,
            _hosts,
            _impacts,
            out int bodyHitCount,
            out bool obstructed,
            out Vector3 obstructImpact,
            out bool missAtRangeEnd,
            out Vector3 missImpact);

        float jin = CombatImpulse.ShotJin(item, ammo);
        for (int i = 0; i < bodyHitCount; i++)
        {
            CharacterBodyHost host = _hosts[i];
            var hitContext = new ActionHandlerContext(
                context.Leaf,
                context.Hand,
                context.Attack,
                host,
                context.OffenseFactor,
                context.ItemId,
                context.Instance,
                context.Stack);
            float p = attacker.ResolveCommittedHit(
                hitContext,
                mode,
                item,
                origin,
                consumeAmmo: false,
                ammo,
                applyCooldown: false,
                practice: false,
                impactOverride: _impacts[i],
                rangedEffectiveDispersion: effective,
                impulseJinOverride: jin,
                impulseContinues: i < bodyHitCount - 1);
            jin = CombatImpulse.ExitJin(jin, p);
            if (jin < CombatImpulse.MinContinueJin)
                break;
        }

        attacker.AddRecoilKick(context.Hand, item, ammo);

        if (obstructed)
        {
            attacker.EmitJudged(
                context,
                mode,
                AttackPerformResult.Obstructed,
                null,
                string.Empty,
                0,
                origin,
                obstructImpact,
                item,
                ammo);
            return true;
        }

        if (missAtRangeEnd)
        {
            attacker.EmitJudged(
                context,
                mode,
                AttackPerformResult.Miss,
                null,
                string.Empty,
                0,
                origin,
                missImpact,
                item,
                ammo);
        }

        return true;
    }

    static bool TryLaunchFlight(
        CharacterAttacker attacker,
        in ActionHandlerContext context,
        ItemData item,
        ItemData ammo,
        Vector3 origin,
        Vector3 direction,
        float range,
        float rangedEffectiveDispersion)
    {
        if (!WeaponAttack.UsesFlightProjectile(context.Attack))
            return false;

        DistProjectile spawned = UnityEngine.Object.Instantiate(context.Attack.ProjectilePrefab);
        spawned.Launch(
            attacker,
            context,
            item,
            ammo,
            origin,
            direction,
            range,
            pierce: WeaponChamber.ResolvePierce(context.Stack, context.Instance),
            attacker.RangedObstructionMask,
            rangedEffectiveDispersion);
        return true;
    }
}
