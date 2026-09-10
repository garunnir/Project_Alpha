// ============================================================
// PlayerEncumbranceHost — 몸통 무게 비율 → 과적 단계·이동·숙련 적용
// ============================================================

using System;
using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInventoryHost))]
public sealed class PlayerEncumbranceHost : MonoBehaviour, ISkillModifierSource
{
    [Required, SerializeField] PlayerInventoryHost _inventoryHost;
    [SerializeField] PlayerMovement _movement;
    [SerializeField] CharacterSkillsHost _skillsHost;

    InventoryContainer _subscribedContainer;
    ICharacterSkills _skills;
    bool _modifierRegistered;

    public static PlayerEncumbranceHost Active => PlayerPossessSession.EncumbranceHost;

    public static event Action ActiveChanged;
    public static event Action StageChanged;

    public PlayerEncumbranceStage Stage { get; private set; } = PlayerEncumbranceStage.None;

    public float WeightRatio
    {
        get
        {
            InventoryContainer container = _inventoryHost != null ? _inventoryHost.Container : null;
            if (container?.CapacityPolicy == null)
                return 0f;

            float max = container.CapacityPolicy.GetMaxWeight(container);
            if (max <= 0f)
                return float.PositiveInfinity;

            return container.GetTotalWeight() / max;
        }
    }

    void Awake()
    {
        EnsureReferences();
    }

    void OnEnable()
    {
        EnsureReferences();
        SubscribeContainer();
        RegisterModifierSource();
        Refresh();
    }

    void Start()
    {
        // PlayerInventoryHost.Awake 이후 컨테이너가 생길 수 있음.
        SubscribeContainer();
        RegisterModifierSource();
        Refresh();
    }

    void OnDisable()
    {
        UnsubscribeContainer();
        UnregisterModifierSource();
    }

    void OnValidate() => EnsureReferences();
    void Reset() => EnsureReferences();

    public void ClaimActive() => ActiveChanged?.Invoke();

    public static void NotifySessionCleared() => ActiveChanged?.Invoke();

    public void BindMovement(PlayerMovement movement)
    {
        _movement = movement;
        Refresh();
    }

    void EnsureReferences()
    {
        if (_inventoryHost == null)
            TryGetComponent(out _inventoryHost);
        if (_movement == null)
            TryGetComponent(out _movement);
        if (_skillsHost == null)
            TryGetComponent(out _skillsHost);
    }

    void SubscribeContainer()
    {
        InventoryContainer container = _inventoryHost != null ? _inventoryHost.Container : null;
        if (container == _subscribedContainer)
            return;

        UnsubscribeContainer();
        _subscribedContainer = container;
        if (_subscribedContainer != null)
            _subscribedContainer.ContentsChanged += OnContentsChanged;
    }

    void UnsubscribeContainer()
    {
        if (_subscribedContainer == null)
            return;

        _subscribedContainer.ContentsChanged -= OnContentsChanged;
        _subscribedContainer = null;
    }

    void RegisterModifierSource()
    {
        ICharacterSkills skills = ResolveSkills();
        if (skills == null || _modifierRegistered)
            return;

        skills.AddModifierSource(this);
        _skills = skills;
        _modifierRegistered = true;
    }

    void UnregisterModifierSource()
    {
        if (!_modifierRegistered || _skills == null)
            return;

        _skills.RemoveModifierSource(this);
        _skills = null;
        _modifierRegistered = false;
    }

    ICharacterSkills ResolveSkills()
    {
        if (_skillsHost != null)
            return _skillsHost.Skills;

        return GameplayData.CharacterSkills;
    }

    void OnContentsChanged() => Refresh();

    public void Refresh()
    {
        SubscribeContainer();

        InventoryContainer container = _inventoryHost != null ? _inventoryHost.Container : null;
        PlayerEncumbranceStage next = PlayerEncumbranceStage.None;
        if (container?.CapacityPolicy != null)
        {
            next = PlayerEncumbrance.ResolveStage(
                container.GetTotalWeight(),
                container.CapacityPolicy.GetMaxWeight(container));
        }

        ApplyMovement(next);

        bool stageChanged = next != Stage;
        Stage = next;

        if (_modifierRegistered)
            _skills?.Refresh();
        else
        {
            RegisterModifierSource();
            _skills?.Refresh();
        }

        if (stageChanged)
            StageChanged?.Invoke();
    }

    void ApplyMovement(PlayerEncumbranceStage stage)
    {
        if (_movement == null)
            return;

        _movement.SetEncumbranceMovement(
            PlayerEncumbrance.GetMoveSpeedMultiplier(stage),
            PlayerEncumbrance.BlocksSprint(stage),
            PlayerEncumbrance.BlocksMovement(stage));
    }

    public void CollectModifiers(Dictionary<string, int> into) =>
        PlayerEncumbrance.CollectSkillModifiers(Stage, into);
}
