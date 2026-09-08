// ============================================================
// MeleeHitHandler — melee_hit: cue 히트박스 겹침 = 확정 타격
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

public sealed class MeleeHitHandler : IActionHandler
{
    readonly CharacterBodyHost[] _hosts = new CharacterBodyHost[MeleeHitbox.BufferSize];
    readonly MeleeHitContact[] _contacts = new MeleeHitContact[MeleeHitbox.BufferSize];

    public string LogicId => ActionHandlerIds.MeleeHit;

    public void Execute(CharacterAttacker attacker, in ActionHandlerContext context)
    {
        if (attacker == null)
            return;

        ItemData item = attacker.ItemFor(context.ItemId);
        WeaponResolveMode mode = WeaponResolveMode.MeleeReach;
        Vector3 origin = attacker.ResolveOrigin();

            if (attacker.GetWeaponCooldown(context.Hand) > 0f)
        {
            attacker.EmitJudgedGate(context, mode, AttackPerformResult.Cooling, item, origin);
            return;
        }

        attacker.CommitAttempt(
            context,
            item,
            consumeAmmo: false,
            applyCooldown: true,
            practice: true);

        int hitCount = attacker.CollectMeleeHits(
            item,
            context.Leaf,
            context.Attack,
            _hosts,
            _contacts);
        for (int i = 0; i < hitCount; i++)
        {
            CharacterBodyHost host = _hosts[i];
            MeleeHitContact contact = _contacts[i];
            var hitContext = new ActionHandlerContext(
                context.Leaf,
                context.Hand,
                context.Attack,
                host,
                context.OffenseFactor,
                context.ItemId,
                context.Instance,
                context.Stack);
            attacker.ResolveCommittedHit(
                hitContext,
                mode,
                item,
                origin,
                consumeAmmo: false,
                ammo: null,
                applyCooldown: false,
                rollHitChance: false,
                practice: false,
                weaponReach01: contact.WeaponReach01,
                impactOverride: contact.WorldPoint);
        }
    }
}
