// ============================================================
// PlayerPossessSession — possess 플레이어 세션 SSOT (plain, possess 시 생성)
// InventorySession(인벤 UI 세션)과 별개 — 이름 혼동 주의.
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

public sealed class PlayerPossessSession
{
    CharacterBodyHost _bodyHost;
    CharacterSkillsHost _skillsHost;
    CharacterTraitsHost _traitsHost;
    PlayerGearHost _gear;
    PlayerEncumbranceHost _encumbrance;
    InventoryTimedMoveHost _timedMove;
    PlayerInventoryHost _inventory;
    NearbyContainerDetector _detector;
    CharacterActionHost _action;
    CharacterSightHost _sight;
    CharacterImbalanceHost _imbalance;
    CharacterMoodHost _mood;
    PlayerNeedsHost _needs;

    /// <summary>현재 possess된 플레이어 세션. 없으면 null.</summary>
    public static PlayerPossessSession Current { get; private set; }

    public static bool HasPlayer => Current != null;

    /// <summary>플레이어 세션 몸 데이터. possess 중이 아니면 null.</summary>
    public static ICharacterBody SessionBody => Current?._bodyHost?.Body;

    public static CharacterBodyHost SessionBodyHost => Current?._bodyHost;

    public static PlayerGearHost GearHost => Current?._gear;

    public static PlayerEncumbranceHost EncumbranceHost => Current?._encumbrance;

    public static InventoryTimedMoveHost TimedMoveHost => Current?._timedMove;

    public static CharacterImbalanceHost ImbalanceHost => Current?._imbalance;

    public static CharacterMoodHost MoodHost => Current?._mood;

    public static PlayerNeedsHost NeedsHost => Current?._needs;

    public static CharacterSkillsHost SessionSkillsHost => Current?._skillsHost;

    public static CharacterTraitsHost SessionTraitsHost => Current?._traitsHost;

    public static CharacterActionHost SessionActionHost => Current?._action;

    /// <summary>possess 플레이어 Sight 구독 게이트. Dig StructureLock 등.</summary>
    public static CharacterSightHost SessionSightHost => Current?._sight;

    public static PlayerInventoryHost SessionInventory => Current?._inventory;

    public static NearbyContainerDetector SessionDetector => Current?._detector;

    public GameObject Body { get; private set; }

    public CharacterBodyHost BodyHost => _bodyHost;

    public CharacterTraitsHost TraitsHost => _traitsHost;

    public CharacterActionHost ActionHost => _action;

    public CharacterSightHost Sight => _sight;

    public PlayerInventoryHost Inventory => _inventory;

    public NearbyContainerDetector Detector => _detector;

    public static void Begin(
        GameObject body,
        PlayerMovement movement,
        PlayerInventoryRuntime inventoryRuntime)
    {
        if (body == null)
        {
            Clear();
            return;
        }

        Clear();

        var session = new PlayerPossessSession();
        session.Body = body;
        session.ResolveHosts(body);
        Current = session;
        session.Activate(movement, inventoryRuntime);
    }

    public static void Clear()
    {
        if (Current == null)
            return;

        Current = null;
        GameplayPlayerRuntime.RegisterPossessedTraitsResolver(null);
        PlayerEncumbranceHost.NotifySessionCleared();
    }

    /// <summary>본체 비활성화 시 세션 해제.</summary>
    public static void EnsureBodyActive()
    {
        if (Current == null || Current.Body == null)
            return;

        if (!Current.Body.activeInHierarchy)
            Clear();
    }

    void ResolveHosts(GameObject body)
    {
        _bodyHost = body.GetBodyComponent<CharacterBodyHost>();
        _skillsHost = body.GetBodyComponent<CharacterSkillsHost>();
        _traitsHost = body.GetBodyComponent<CharacterTraitsHost>();
        _gear = body.GetBodyComponent<PlayerGearHost>();
        _encumbrance = body.GetBodyComponent<PlayerEncumbranceHost>();
        _timedMove = body.GetBodyComponent<InventoryTimedMoveHost>();
        _inventory = body.GetBodyComponent<PlayerInventoryHost>();
        _detector = body.GetBodyComponent<NearbyContainerDetector>();
        _action = body.GetBodyComponent<CharacterActionHost>();
        _sight = body.GetBodyComponent<CharacterSightHost>();
        if (_sight == null && _action != null)
            _sight = _action.SightHost;
        _imbalance = body.GetBodyComponent<CharacterImbalanceHost>();
        _mood = body.GetBodyComponent<CharacterMoodHost>();
        _needs = body.GetBodyComponent<PlayerNeedsHost>();
    }

    void Activate(PlayerMovement movement, PlayerInventoryRuntime inventoryRuntime)
    {
        inventoryRuntime?.BindBody(_inventory, _detector);

        _timedMove?.ClaimActive();
        if (_encumbrance != null)
        {
            _encumbrance.BindMovement(movement);
            _encumbrance.ClaimActive();
        }

        if (_gear != null)
        {
            _gear.BindMovement(movement);
            _gear.ClaimActive();
        }

        _imbalance?.ClaimActive();
        _mood?.ClaimActive();
        _needs?.ClaimActive();

        if (_bodyHost != null && _bodyHost.Body != null)
            GameplayData.Body = _bodyHost.Body;

        if (_skillsHost != null && _skillsHost.Skills is DefaultCharacterSkills seeded)
            GameplayData.Stats = new DefaultPlayerStats(seeded);

        if (_traitsHost != null)
            GameplayPlayerRuntime.RegisterPossessedTraitsResolver(() => _traitsHost.Traits);

        CharacterDefinitionBinder binder = Body.GetBodyRefs()?.DefinitionBinder;
        if (binder == null)
            binder = Body.GetBodyComponent<CharacterDefinitionBinder>();
        if (movement != null && binder != null)
            movement.ApplyWalkSpeedFromDefinition(binder.Definition);

        PlayerStatusUIBridge.RebindFromGameplayData();
    }
}
