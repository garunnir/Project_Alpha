// ============================================================
// PlayerInventoryHost — 캐릭터 몸통 인벤 (인스턴스별, Detector 대상 아님)
// ============================================================

using System;
using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

public enum BodyLootDisplayKind
{
    None = 0,
    Unconscious = 1,
    Dead = 2,
}

public sealed class PlayerInventoryHost : IInventoryContainerProvider
{
    public const string DefaultInstanceId = "player-body";
    public const string DefaultContainerDefId = "player_body";
    public const string UnconsciousBodyContainerDefId = "unconscious_body";
    public const string DeadBodyContainerDefId = "dead_body";
    public const string UniqueBodyInstanceIdPrefix = "character-body-";

    public static string CreateUniqueBodyInstanceId() =>
        UniqueBodyInstanceIdPrefix + Guid.NewGuid().ToString("N");

    string _containerDefId = DefaultContainerDefId;
    string _containerId = DefaultInstanceId;
    readonly float _baseMaxWeight = 50f;
    readonly float _baseMaxVolume = 30f;

    CharacterBodyRefs _refs;
    CharacterState _characterState;
    InventoryContainer _container;
    PlayerCarryCapacityPolicy _capacityPolicy;
    CharacterPainHost _painHost;
    CharacterSkillsHost _skillsHost;
    CharacterBodyHost _bodyHost;
    ICharacterBody _subscribedBody;
    ICharacterDefeat _subscribedDefeat;
    BodyLootDisplayKind _lastLootDisplayKind = BodyLootDisplayKind.None;
    bool _lastLootAvailableToPlayer;
    bool _enabled;

    public CharacterBodyRefs BodyRefs => _refs;
    public InventoryContainer Container => _container;
    public string ContainerId => _containerId;
    public Vector3 WorldPosition =>
        _refs != null ? _refs.transform.position : Vector3.zero;
    public Vector3Int GridPosition =>
        _characterState != null ? _characterState.GridPos : Vector3Int.zero;

    public void AssignInstanceId(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return;

        if (_container != null)
        {
            Debug.LogError("[PlayerInventoryHost] AssignInstanceId must run before Enable.");
            return;
        }

        _containerId = instanceId;
    }

    public static bool IsNpcBodyInstanceId(string instanceId) =>
        !string.IsNullOrEmpty(instanceId) &&
        instanceId.StartsWith(UniqueBodyInstanceIdPrefix, StringComparison.Ordinal);

    public void Bind(CharacterBodyRefs refs)
    {
        _refs = refs;
        _characterState = refs != null ? refs.State : null;
        _painHost = refs != null ? refs.PainHost : null;
        _skillsHost = refs != null ? refs.SkillsHost : null;
        _bodyHost = refs != null ? refs.BodyHost : null;
        if (string.IsNullOrWhiteSpace(_containerId))
            _containerId = DefaultInstanceId;
    }

    public void Enable()
    {
        EnsureContainer();
        InventoryContainerRegistry.Register(this);
        SubscribeLootDisplaySignals();
        CacheLootDisplaySnapshot();
        _enabled = true;
    }

    public void Disable()
    {
        if (!_enabled)
            return;

        _enabled = false;
        UnsubscribeLootDisplaySignals();
        InventoryContainerRegistry.Unregister(this);
    }

    void EnsureContainer()
    {
        if (_container != null)
            return;

        ContainerData containerDef = GameplayData.GetContainer(_containerDefId);
        if (containerDef == null)
        {
            Debug.LogWarning(
                $"[PlayerInventoryHost] Container definition '{_containerDefId}' not found in GameData.");
            return;
        }

        _capacityPolicy = new PlayerCarryCapacityPolicy(
            () => _baseMaxWeight,
            () => _baseMaxVolume);
        string instanceId = string.IsNullOrWhiteSpace(_containerId) ? DefaultInstanceId : _containerId;
        _container = InventoryContainer.Create(containerDef, _capacityPolicy, instanceId);
    }

    /// <summary>
    /// 자기 몸, Defeat, 고통 쇼크면 true. 살아 있는 타인 몸은 Nearby에서 제외.
    /// </summary>
    public bool IsAvailableToPlayer(GameObject player)
    {
        if (player == null || _characterState == null)
            return false;
        if (player == _characterState.gameObject)
            return true;

        ICharacterDefeat defeat = _skillsHost != null ? _skillsHost.Defeat : null;
        if (defeat != null && defeat.IsDefeated)
            return true;
        return _painHost != null && _painHost.IsPainShocked;
    }

    /// <summary>
    /// Nearby NPC 몸 탭 아이콘·폴백 라벨 SSOT. 사망 &gt; 기절·무력(고통 쇼크·비사망 Defeat).
    /// </summary>
    public BodyLootDisplayKind GetBodyLootDisplayKind()
    {
        if (!IsNpcBodyInstanceId(_containerId))
            return BodyLootDisplayKind.None;

        ICharacterBody body = _bodyHost != null ? _bodyHost.Body : null;
        if (body != null && body.IsDeadState)
            return BodyLootDisplayKind.Dead;

        ICharacterDefeat defeat = _skillsHost != null ? _skillsHost.Defeat : null;
        if (defeat != null && defeat.IsDefeated && defeat.Cause == DefeatCause.BodyFatal)
            return BodyLootDisplayKind.Dead;

        if (_painHost != null && _painHost.IsPainShocked)
            return BodyLootDisplayKind.Unconscious;

        if (defeat != null && defeat.IsDefeated)
            return BodyLootDisplayKind.Unconscious;

        return BodyLootDisplayKind.None;
    }

    public string ResolveBodyLootContainerDefId()
    {
        return GetBodyLootDisplayKind() switch
        {
            BodyLootDisplayKind.Dead => DeadBodyContainerDefId,
            BodyLootDisplayKind.Unconscious => UnconsciousBodyContainerDefId,
            _ => DefaultContainerDefId,
        };
    }

    public string ResolveBodyLootDisplayName()
    {
        CharacterAppearanceHost appearance = _refs != null ? _refs.Appearance : null;
        if (appearance != null)
        {
            string displayName = appearance.ResolveDisplayName();
            if (!string.IsNullOrEmpty(displayName))
                return displayName;
        }

        ContainerData displayDef = GameplayData.GetContainer(ResolveBodyLootContainerDefId());
        return displayDef != null ? UITextPresenter.GetContainerName(displayDef) : string.Empty;
    }

    void SubscribeLootDisplaySignals()
    {
        if (!IsNpcBodyInstanceId(_containerId))
            return;

        if (_painHost != null)
            _painHost.Changed += OnLootDisplaySignalsChanged;

        if (_skillsHost != null)
        {
            _subscribedDefeat = _skillsHost.Defeat;
            if (_subscribedDefeat != null)
                _subscribedDefeat.Changed += OnLootDisplaySignalsChanged;
        }

        BindBodyLootSignals();
    }

    void UnsubscribeLootDisplaySignals()
    {
        if (_painHost != null)
            _painHost.Changed -= OnLootDisplaySignalsChanged;

        if (_subscribedDefeat != null)
        {
            _subscribedDefeat.Changed -= OnLootDisplaySignalsChanged;
            _subscribedDefeat = null;
        }

        UnbindBodyLootSignals();
    }

    void BindBodyLootSignals()
    {
        UnbindBodyLootSignals();
        _subscribedBody = _bodyHost != null ? _bodyHost.Body : null;
        if (_subscribedBody != null)
            _subscribedBody.Changed += OnLootDisplaySignalsChanged;
    }

    void UnbindBodyLootSignals()
    {
        if (_subscribedBody != null)
            _subscribedBody.Changed -= OnLootDisplaySignalsChanged;
        _subscribedBody = null;
    }

    void CacheLootDisplaySnapshot()
    {
        _lastLootDisplayKind = GetBodyLootDisplayKind();
        _lastLootAvailableToPlayer = IsAvailableToPlayer(ResolvePlayerInteractor());
    }

    void OnLootDisplaySignalsChanged()
    {
        if (!IsNpcBodyInstanceId(_containerId))
            return;

        BodyLootDisplayKind nextKind = GetBodyLootDisplayKind();
        bool nextAvailable = IsAvailableToPlayer(ResolvePlayerInteractor());
        if (nextKind == _lastLootDisplayKind && nextAvailable == _lastLootAvailableToPlayer)
            return;

        bool availabilityChanged = nextAvailable != _lastLootAvailableToPlayer;
        _lastLootDisplayKind = nextKind;
        _lastLootAvailableToPlayer = nextAvailable;

        PlayerInventoryRuntime runtime = PlayerInventoryRuntime.Active;
        if (runtime == null)
            return;

        if (availabilityChanged)
            runtime.RefreshNearbyContainers();
        else
            runtime.Session?.NotifySidebarLayoutChanged();
    }

    static GameObject ResolvePlayerInteractor()
    {
        PlayerPossessSession session = PlayerPossessSession.Current;
        if (session?.Body == null)
            return null;

        return session.Body.TryGetBodyComponent(out CharacterState state) ? state.gameObject : null;
    }

    public bool RegisterToSession(InventorySession session) =>
        session != null && _container != null && session.TryAddSidebarContainer(_container);

    public bool UnregisterFromSession(InventorySession session) =>
        session != null && _container != null && session.TryRemoveSidebarContainer(_container.InstanceId);
}
