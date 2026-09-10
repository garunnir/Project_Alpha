// ============================================================
// CharacterSessionHub — possess 플레이어 세션 SSOT (인벤·기어·GameplayData 바인딩)
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterBodyHost))]
[RequireComponent(typeof(CharacterSkillsHost))]
[RequireComponent(typeof(CharacterTraitsHost))]
[RequireComponent(typeof(PlayerGearHost))]
[RequireComponent(typeof(PlayerEncumbranceHost))]
public sealed class CharacterSessionHub : MonoBehaviour
{
    [SerializeField] CharacterBodyHost _bodyHost;
    [SerializeField] CharacterSkillsHost _skillsHost;
    [SerializeField] CharacterTraitsHost _traitsHost;
    [SerializeField] PlayerGearHost _gear;
    [SerializeField] PlayerEncumbranceHost _encumbrance;
    [SerializeField] InventoryTimedMoveHost _timedMove;
    [SerializeField] PlayerInventoryHost _inventory;
    [SerializeField] NearbyContainerDetector _detector;
    [SerializeField] CharacterActionHost _action;
    [SerializeField] CharacterSightHost _sight;
    [SerializeField] CharacterImbalanceHost _imbalance;
    [SerializeField] CharacterMoodHost _mood;
    [SerializeField] PlayerNeedsHost _needs;

    /// <summary>현재 possess된 플레이어 세션. 없으면 null.</summary>
    public static CharacterSessionHub Player { get; private set; }

    public static bool HasPlayer => Player != null;

    /// <summary>플레이어 세션 몸 데이터. possess 중이 아니면 null.</summary>
    public static ICharacterBody SessionBody => Player?._bodyHost?.Body;

    public static CharacterBodyHost SessionBodyHost => Player?._bodyHost;

    public static PlayerGearHost GearHost => Player?._gear;

    public static PlayerEncumbranceHost EncumbranceHost => Player?._encumbrance;

    public static InventoryTimedMoveHost TimedMoveHost => Player?._timedMove;

    public static CharacterImbalanceHost ImbalanceHost => Player?._imbalance;

    public static CharacterMoodHost MoodHost => Player?._mood;

    public static PlayerNeedsHost NeedsHost => Player?._needs;

    public static CharacterSkillsHost SessionSkillsHost => Player?._skillsHost;

    public static CharacterTraitsHost SessionTraitsHost => Player?._traitsHost;

    public static CharacterActionHost SessionActionHost => Player?._action;

    /// <summary>possess 플레이어 Sight 구독 게이트. Dig StructureLock 등.</summary>
    public static CharacterSightHost SessionSightHost => Player?._sight;

    public static PlayerInventoryHost SessionInventory => Player?._inventory;

    public static NearbyContainerDetector SessionDetector => Player?._detector;

    public CharacterBodyHost BodyHost => _bodyHost;
    public CharacterTraitsHost TraitsHost => _traitsHost;
    public CharacterActionHost Action => _action;
    public CharacterSightHost Sight => _sight;
    public PlayerInventoryHost Inventory => _inventory;
    public NearbyContainerDetector Detector => _detector;

    void Awake() => EnsureRefs();

    void OnValidate() => EnsureRefs();

    void Reset() => EnsureRefs();

    void OnDisable()
    {
        if (Player != this)
            return;

        ClearPlayerSession();
    }

    public void BecomePlayer(PlayerMovement movement, PlayerInventoryRuntime inventoryRuntime)
    {
        EnsureRefs();
        Player = this;

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

        CharacterDefinitionBinder binder = this.GetBodyRefs()?.DefinitionBinder;
        if (binder == null)
            binder = CharacterBodyResolve.GetInBody<CharacterDefinitionBinder>(this);
        if (movement != null && binder != null)
            movement.ApplyWalkSpeedFromDefinition(binder.Definition);

        PlayerStatusUIBridge.RebindFromGameplayData();
    }

    static void ClearPlayerSession()
    {
        Player = null;
        GameplayPlayerRuntime.RegisterPossessedTraitsResolver(null);
        PlayerEncumbranceHost.NotifySessionCleared();
    }

    void EnsureRefs()
    {
        if (_bodyHost == null)
            TryGetComponent(out _bodyHost);
        if (_skillsHost == null)
            TryGetComponent(out _skillsHost);
        if (_traitsHost == null)
            TryGetComponent(out _traitsHost);
        if (_gear == null)
            TryGetComponent(out _gear);
        if (_encumbrance == null)
            TryGetComponent(out _encumbrance);
        if (_timedMove == null)
            TryGetComponent(out _timedMove);
        if (_inventory == null)
            TryGetComponent(out _inventory);
        if (_detector == null)
            TryGetComponent(out _detector);
        if (_action == null)
            TryGetComponent(out _action);
        if (_sight == null)
        {
            TryGetComponent(out _sight);
            if (_sight == null && _action != null)
                _sight = _action.SightHost;
        }
        if (_imbalance == null)
            TryGetComponent(out _imbalance);
        if (_mood == null)
            TryGetComponent(out _mood);
        if (_needs == null)
            TryGetComponent(out _needs);
    }
}
