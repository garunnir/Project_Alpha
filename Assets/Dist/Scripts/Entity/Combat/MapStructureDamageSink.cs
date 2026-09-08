// ============================================================
// MapStructureDamageSink — Obstructed 착탄 → Occupied 벽 remaining HP
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;
using UnityEngine;

/// <summary>
/// 원거리/히트스캔 Obstructed에 벽 HP를 붙인다. Miss 값을 만들지 않음.
/// DistScript — CharacterAttacker 소비자 (MapBloodHitSink와 동일).
/// </summary>
public static class MapStructureDamageSink
{
    static bool _wired;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        CharacterAttacker.AnyAttackJudged -= OnAnyAttackJudged;
        _wired = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Wire()
    {
        if (_wired)
            return;
        CharacterAttacker.AnyAttackJudged += OnAnyAttackJudged;
        _wired = true;
    }

    static void OnAnyAttackJudged(AttackOutcome outcome)
    {
        if (outcome.Result != AttackPerformResult.Obstructed)
            return;

        CharacterAttacker attacker = outcome.Attacker != null
            ? CharacterBodyResolve.GetInBody<CharacterAttacker>(outcome.Attacker)
            : null;
        if (attacker == null)
            return;

        MapDigColumnHost digHost = MapDigColumnHost.Runtime;
        TileMapCacheHub hub = TileMapCacheHub.Runtime;
        if (digHost == null || hub == null)
            return;

        float cellSize = digHost.CellSize;
        Vector3Int cell = TileHelper.ConvertWorldToGrid(outcome.ImpactPoint, cellSize);
        if (!hub.CellHasSolidWall(cell.x, cell.z, cell.y) &&
            !TryFindNeighborSolidWall(hub, cell, out cell))
        {
            return;
        }

        if (!hub.TryGetCellTiles(cell.x, cell.z, cell.y, out var tiles) ||
            tiles == null ||
            tiles.Count == 0)
            return;

        TileDefinition definition = null;
        for (int i = 0; i < tiles.Count; i++)
        {
            TileDefinition def = ResolveDefinition(tiles[i].identity.PrefabId);
            if (def == null || !def.occupied.blocksOccupiedCells)
                continue;
            definition = def;
            break;
        }

        if (definition == null)
            return;

        int maxHp = TileDefinitionCombat.BreakDurability(definition);
        digHost.GetOrInitOccupiedHp(cell, maxHp);

        int damage = ResolveOutcomeStructureDamage(attacker, outcome.Action, definition);
        if (damage <= 0)
            return;

        if (!digHost.ApplyOccupiedDamage(cell, damage, out _))
            return;

        digHost.TryBreakOccupiedTile(cell, out _);
    }

    static bool TryFindNeighborSolidWall(
        TileMapCacheHub hub,
        Vector3Int cell,
        out Vector3Int solidCell)
    {
        solidCell = cell;
        Vector3Int[] neighbors =
        {
            cell + Vector3Int.right,
            cell + Vector3Int.left,
            new Vector3Int(cell.x, cell.y, cell.z + 1),
            new Vector3Int(cell.x, cell.y, cell.z - 1),
        };
        for (int i = 0; i < neighbors.Length; i++)
        {
            Vector3Int n = neighbors[i];
            if (!hub.CellHasSolidWall(n.x, n.z, n.y))
                continue;
            solidCell = n;
            return true;
        }

        return false;
    }

    static int ResolveOutcomeStructureDamage(
        CharacterAttacker attacker,
        CombatLeaf leaf,
        TileDefinition definition)
    {
        ItemData item = attacker.WieldedStack != null ? attacker.WieldedStack.Item : null;
        ItemData ammo = WeaponChamber.ResolveAmmo(
            attacker.WieldedStack,
            attacker.WieldedInstance);
        CharacterSkillsHost skillsHost =
            CharacterBodyResolve.GetInBody<CharacterSkillsHost>(attacker);
        ICharacterSkills skills = skillsHost != null ? skillsHost.Skills : null;
        int strength = skills != null
            ? skills.Level(AttributeIds.Str)
            : CombatMath.StrengthBaseline;
        int skillLevel = skills != null
            ? skills.Level(
                CombatLeafUtil.IsRanged(leaf)
                    ? CombatSkillIds.Gun
                    : CombatSkillIds.Melee)
            : 0;

        return CombatMath.ResolveStructureDamage(
            item,
            leaf,
            strength,
            skillLevel,
            definition.materials,
            TileDefinitionCombat.MaterialThickness(definition),
            offenseFactor: 1f,
            ammo);
    }

    static TileDefinition ResolveDefinition(string prefabId)
    {
        if (TilePrefabDB.TryResolveDefinition(prefabId, out TileDefinition def))
            return def;
        return null;
    }
}
