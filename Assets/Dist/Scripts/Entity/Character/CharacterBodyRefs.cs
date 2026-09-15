// ============================================================
// CharacterBodyRefs — 본체 resolve 캐시 + plain module 소유 (composition root)
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(CharacterBodyRoot))]
public sealed class CharacterBodyRefs : MonoBehaviour
{
    public const string RenderPivotChildName = "3DRenderPivot";

    static readonly List<CharacterBodyRefs> s_active = new(16);

    readonly Dictionary<Type, Component> _byExactType = new(32);
    readonly Dictionary<Type, object> _modules = new(32);

    [SerializeField] Transform _renderRoot;
    [SerializeField] SelectionLayerConfig _selectionLayer;
    [SerializeField] CharacterEmoteCatalog _emoteCatalog;
    [SerializeField] PlayerNeedsSettings _needsSettings;
    [SerializeField] MoodSettings _moodSettings;
    [SerializeField] CombatHitStopSettings _hitStopSettings;
    [SerializeField] WeaponPresentationCatalog _weaponPresentationCatalog;
    [SerializeField] LayerMask _rangedObstructionMask = ~0;
    [SerializeField] TimeScaleChannel _attackerTimeChannel = TimeScaleChannel.World;
    [SerializeField] TimeScaleChannel _combatVfxTimeChannel = TimeScaleChannel.World;
    [SerializeField] TimeScaleChannel _bodyEffectTimeChannel = TimeScaleChannel.World;

    CharacterBodyHost _bodyHost;
    CharacterSkillsHost _skillsHost;
    CharacterTraitsHost _traitsHost;
    CharacterFactionHost _factionHost;
    CharacterFootprintHost _footprintHost;
    PlayerNeedsHost _needs;
    CharacterMoodHost _mood;
    CharacterClimateHost _climate;
    CharacterVision _vision;
    CharacterHearing _hearing;
    CharacterPresenceHost _presence;
    CharacterAppearanceHost _appearance;
    CharacterSightFadeHost _sightFade;
    CharacterSelectionOutlineHost _outline;
    CharacterEmoteHost _emote;
    PlayerInventoryHost _inventory;
    PlayerGearHost _gear;
    PlayerEncumbranceHost _encumbrance;
    InventoryTimedMoveHost _timedMove;
    NearbyContainerDetector _nearby;
    CharacterAttacker _attacker;
    CharacterPainHost _pain;
    CharacterImbalanceHost _imbalance;
    CharacterHitReact _hitReact;
    BodyEffectTicker _bodyEffectTicker;

    bool _resolved;

    public bool IsResolved => _resolved;

    public static int ActiveCount => s_active.Count;

    public static CharacterBodyRefs GetActive(int index) => s_active[index];

    public Transform RenderRoot => _renderRoot;
    public SelectionLayerConfig SelectionLayer => _selectionLayer;
    public CharacterEmoteCatalog EmoteCatalog => _emoteCatalog;
    public PlayerNeedsSettings NeedsSettings => _needsSettings;
    public MoodSettings MoodSettings => _moodSettings;
    public CombatHitStopSettings HitStopSettings => _hitStopSettings;
    public WeaponPresentationCatalog WeaponPresentationCatalog => _weaponPresentationCatalog;
    public LayerMask RangedObstructionMask => _rangedObstructionMask;
    public TimeScaleChannel AttackerTimeChannel => _attackerTimeChannel;
    public TimeScaleChannel CombatVfxTimeChannel => _combatVfxTimeChannel;
    public TimeScaleChannel BodyEffectTimeChannel => _bodyEffectTimeChannel;

    void Awake() => ResolveFromHierarchy();

    void OnEnable()
    {
        if (!s_active.Contains(this))
            s_active.Add(this);
        _bodyHost?.Enable();
        _skillsHost?.Enable();
        _attacker?.Enable();
        _pain?.Enable();
        _imbalance?.Enable();
        _hitReact?.Enable();
        _needs?.Enable();
        _mood?.Enable();
        _inventory?.Enable();
        _timedMove?.Enable();
        _gear?.Enable();
        _encumbrance?.Enable();
        _presence?.Enable();
        _emote?.Enable();
    }

    void OnDisable()
    {
        _nearby?.Dispose();
        _hitReact?.Disable();
        _imbalance?.Disable();
        _pain?.Disable();
        _attacker?.Disable();
        _mood?.Disable();
        _needs?.Disable();
        _skillsHost?.Disable();
        _bodyHost?.Disable();
        _inventory?.Disable();
        _timedMove?.Disable();
        _gear?.Disable();
        _encumbrance?.Disable();
        _presence?.Disable();
        _emote?.Disable();
        _sightFade?.Disable();
        _outline?.Disable();
        s_active.Remove(this);
    }

    /// <summary>Needs sleep-wake · Mood break · Climate thermal. heap 없음.</summary>
    void Update()
    {
        _needs?.Tick();
        _mood?.Tick();
        _climate?.Tick();
        _attacker?.Tick();
        _pain?.Tick();
        _imbalance?.Tick();
        _bodyEffectTicker?.Tick();
    }

    /// <summary>Presence·Emote·Gear·TimedMove·HitStop 틱. heap 없음 (struct context / 만료 슬롯).</summary>
    void LateUpdate()
    {
        _presence?.Tick();
        _emote?.Tick();
        _gear?.Tick();
        _timedMove?.Tick();
        _bodyHost?.Tick();
    }

    void OnDestroy()
    {
        _skillsHost?.Dispose();
        _climate?.Dispose();
    }

    /// <summary>스폰(inactive) 직후·SetActive 전에 Factory가 호출. 이후 GetInBody는 캐시만 조회.</summary>
    public void ResolveFromHierarchy()
    {
        if (_resolved)
            return;

        _byExactType.Clear();
        Component[] components = GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
                continue;

            Type type = component.GetType();
            if (!_byExactType.ContainsKey(type))
                _byExactType[type] = component;
        }

        _resolved = true;
        EnsureModules();
    }

    public void Invalidate() => _resolved = false;

    /// <summary>inactive 스폰 직후 SetActive 전. Awake 이전에 캐시를 채운다. 프리팹 루트에 미리 있어야 함.</summary>
    public static CharacterBodyRefs EnsureResolved(GameObject instance)
    {
        if (instance == null)
            return null;

        CharacterBodyRefs refs = instance.GetComponentInParent<CharacterBodyRefs>();
        if (refs == null)
            instance.TryGetComponent(out refs);

        if (refs == null)
        {
            Debug.LogError(
                $"[CharacterBodyRefs] '{instance.name}' needs CharacterBodyRefs on the prefab root.",
                instance);
            return null;
        }

        refs.ResolveFromHierarchy();
        return refs;
    }

    public bool TryGet<T>(out T component) where T : Component
    {
        if (!_resolved)
            ResolveFromHierarchy();

        component = null;
        if (!_byExactType.TryGetValue(typeof(T), out Component cached))
            return false;

        component = cached as T;
        return component != null;
    }

    public T Get<T>() where T : Component
    {
        TryGet(out T component);
        return component;
    }

    public bool TryGetModule<T>(out T module) where T : class
    {
        if (!_resolved)
            ResolveFromHierarchy();

        module = null;
        if (!_modules.TryGetValue(typeof(T), out object boxed))
            return false;

        module = boxed as T;
        return module != null;
    }

    public T GetModule<T>() where T : class
    {
        TryGetModule(out T module);
        return module;
    }

    public CharacterState State => Get<CharacterState>();
    public CharacterMotor Motor => Get<CharacterMotor>();
    public Collider BodyCollider => Get<CapsuleCollider>() as Collider ?? Get<Collider>();
    public CharacterDefinitionBinder DefinitionBinder => Get<CharacterDefinitionBinder>();
    public CharacterAttacker Attacker
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _attacker;
        }
    }

    public CharacterActionHost ActionHost => Get<CharacterActionHost>();
    public CharacterPainHost PainHost
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _pain;
        }
    }

    public CharacterImbalanceHost ImbalanceHost
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _imbalance;
        }
    }

    public CharacterHitReact HitReact
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _hitReact;
        }
    }

    public BodyEffectTicker BodyEffectTicker
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _bodyEffectTicker;
        }
    }
    public CharacterVision Vision
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _vision;
        }
    }

    public CharacterHearing Hearing
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _hearing;
        }
    }

    public CharacterPresenceHost Presence
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _presence;
        }
    }

    public CharacterAppearanceHost Appearance
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _appearance;
        }
    }

    public CharacterSightFadeHost SightFade
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _sightFade;
        }
    }

    public CharacterSelectionOutlineHost SelectionOutline
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _outline;
        }
    }

    public CharacterEmoteHost Emote
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _emote;
        }
    }

    public PlayerInventoryHost InventoryHost
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _inventory;
        }
    }

    public PlayerGearHost GearHost
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _gear;
        }
    }

    public PlayerEncumbranceHost EncumbranceHost
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _encumbrance;
        }
    }

    public InventoryTimedMoveHost TimedMoveHost
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _timedMove;
        }
    }

    public NearbyContainerDetector NearbyDetector
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _nearby;
        }
    }

    public CharacterBodyHost BodyHost
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _bodyHost;
        }
    }

    public CharacterSkillsHost SkillsHost
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _skillsHost;
        }
    }

    public CharacterTraitsHost TraitsHost
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _traitsHost;
        }
    }

    public CharacterFactionHost FactionHost
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _factionHost;
        }
    }

    public CharacterFootprintHost FootprintHost
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _footprintHost;
        }
    }

    public PlayerNeedsHost NeedsHost
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _needs;
        }
    }

    public CharacterMoodHost MoodHost
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _mood;
        }
    }

    public CharacterClimateHost ClimateHost
    {
        get
        {
            if (!_resolved)
                ResolveFromHierarchy();
            return _climate;
        }
    }

    public CharacterLocomotionAnim LocomotionAnim => Get<CharacterLocomotionAnim>();
    public CharacterVaultHost VaultHost => Get<CharacterVaultHost>();

    void EnsureModules()
    {
        if (_bodyHost == null)
            _bodyHost = new CharacterBodyHost();
        _bodyHost.Bind(this);
        RegisterModule(_bodyHost);

        if (_skillsHost == null)
            _skillsHost = new CharacterSkillsHost();
        _skillsHost.Bind(this);
        RegisterModule(_skillsHost);

        if (_traitsHost == null)
            _traitsHost = new CharacterTraitsHost();
        _traitsHost.Bind(this);
        RegisterModule(_traitsHost);

        if (_factionHost == null)
            _factionHost = new CharacterFactionHost();
        _factionHost.Bind(this);
        RegisterModule(_factionHost);

        if (_footprintHost == null)
            _footprintHost = new CharacterFootprintHost();
        _footprintHost.Bind(this);
        RegisterModule(_footprintHost);

        if (_needs == null)
            _needs = new PlayerNeedsHost();
        _needs.Bind(this);
        RegisterModule(_needs);

        if (_mood == null)
            _mood = new CharacterMoodHost();
        _mood.Bind(this);
        RegisterModule(_mood);

        if (_climate == null)
            _climate = new CharacterClimateHost();
        _climate.Bind(this);
        RegisterModule(_climate);

        if (_attacker == null)
            _attacker = new CharacterAttacker();
        _attacker.Bind(this);
        RegisterModule(_attacker);

        if (_pain == null)
            _pain = new CharacterPainHost();
        _pain.Bind(this);
        RegisterModule(_pain);

        if (_imbalance == null)
            _imbalance = new CharacterImbalanceHost();
        _imbalance.Bind(this);
        RegisterModule(_imbalance);

        if (_hitReact == null)
            _hitReact = new CharacterHitReact();
        _hitReact.Bind(this);
        RegisterModule(_hitReact);

        if (_bodyEffectTicker == null)
            _bodyEffectTicker = new BodyEffectTicker();
        _bodyEffectTicker.Bind(this);
        RegisterModule(_bodyEffectTicker);

        if (_inventory == null)
            _inventory = new PlayerInventoryHost();
        _inventory.Bind(this);
        RegisterModule(_inventory);

        if (_timedMove == null)
            _timedMove = new InventoryTimedMoveHost();
        _timedMove.Bind(this);
        RegisterModule(_timedMove);

        if (_gear == null)
            _gear = new PlayerGearHost();
        _gear.Bind(this);
        RegisterModule(_gear);

        if (_encumbrance == null)
            _encumbrance = new PlayerEncumbranceHost();
        _encumbrance.Bind(this);
        RegisterModule(_encumbrance);

        if (_nearby == null)
            _nearby = new NearbyContainerDetector();
        _nearby.Bind(this);
        RegisterModule(_nearby);

        if (_vision == null)
            _vision = new CharacterVision();
        _vision.Bind(this);
        RegisterModule(_vision);

        if (_hearing == null)
            _hearing = new CharacterHearing();
        RegisterModule(_hearing);

        if (_presence == null)
            _presence = new CharacterPresenceHost();
        _presence.Bind(this);
        RegisterModule(_presence);

        if (_appearance == null)
            _appearance = new CharacterAppearanceHost();
        _appearance.Bind(this);
        RegisterModule(_appearance);

        if (_sightFade == null)
            _sightFade = new CharacterSightFadeHost();
        _sightFade.Bind(this);
        RegisterModule(_sightFade);

        if (_outline == null)
            _outline = new CharacterSelectionOutlineHost();
        _outline.Bind(this);
        RegisterModule(_outline);

        if (_emote == null)
            _emote = new CharacterEmoteHost();
        _emote.Bind(this);
        RegisterModule(_emote);
    }

    void RegisterModule<T>(T module) where T : class
    {
        if (module == null)
            return;

        _modules[typeof(T)] = module;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        CharacterSenseGizmo.Draw(this, selectedOnly: false);
        SightFade?.DrawGizmos();
        _attacker?.DrawGizmos();
    }

    void OnDrawGizmosSelected() => CharacterSenseGizmo.Draw(this, selectedOnly: true);
#endif
}
