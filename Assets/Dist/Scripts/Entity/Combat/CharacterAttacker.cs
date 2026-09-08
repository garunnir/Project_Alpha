// ============================================================
// CharacterAttacker — 들기 손 시전(듀얼 포함) + 클립 큐에서 IActionHandler 실행
// ============================================================

using System;
using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterSkillsHost))]
public sealed class CharacterAttacker : MonoBehaviour
{
    const float AimHeight = 0.15f;
    public const float MinRayDistance = 0.001f;
    const float SurfaceProbeMargin = 1f;
    const float FallbackImpactRadius = 0.4f;
    const int PendingCueSlotCount = 2;
    const int HandCooldownSlotCount = (int)WieldHand.TwoHand + 1;
    const float CueCycleEpsilon = 1e-4f;

    [FormerlySerializedAs("_weapon")]
    [SerializeField] WeaponPresentation _presentation;
    [Tooltip("GameplayData ItemData id. 비우면 비무장.")]
    [SerializeField] string _itemId;
    [SerializeField] WeaponPresentationCatalog _catalog;
    [Tooltip("원거리 레이/탄 장애물. Character 포함(~0 권장). 자기 콜라이더는 IsOwnCollider로 제외.")]
    [SerializeField] LayerMask _rangedObstructionMask = ~0;
    [SerializeField] TimeScaleChannel _timeChannel = TimeScaleChannel.World;
    [FormerlySerializedAs("_selectedAction")]
    [SerializeField] CombatLeaf _selectedLeaf = CombatLeaf.Strike;
    [SerializeField] WieldHand _activeWieldHand = WieldHand.TwoHand;
    [SerializeField] string _preferredPartId = BodyPartIds.Torso;
    [SerializeField] TimeScaleChannel _combatVfxTimeChannel = TimeScaleChannel.World;
    ItemInstance _wieldedInstance;
    ItemStack _wieldedStack;
    WieldSlotId _lastDualSlot;
    bool _hasLastDualSlot;
    bool _aimHeld;

    CharacterSkillsHost _skillsHost;
    PlayerGearHost _gearHost;
    CharacterClimateHost _climateHost;
    CharacterActionHost _actionHost;
    CharacterMotor _motor;
    CharacterAppearanceHost _appearance;
    CharacterPainHost _painHost;
    CharacterImbalanceHost _imbalanceHost;
    CharacterState _characterState;
    CharacterLocomotionAnim _locAnim;
    CharacterHitStopState _hitStop;
    CharacterAttackerCombatVfx _combatVfx;
    MapTopologyLineCast _mapLineCast;
    CharacterBodyHost _bodyHost;
    Collider _selfCollider;
    readonly Collider[] _meleeColliders = new Collider[MeleeHitbox.BufferSize];
    readonly Vector3[] _debugContacts = new Vector3[MeleeHitbox.BufferSize];
    MeleeHitboxPose _debugCuePose;
    int _debugCueHitCount;
    int _debugContactCount;
    float _debugCueUntilUnscaled;
    readonly float[] _actionCooldownRemaining = new float[HandCooldownSlotCount];
    readonly float[] _actionCooldownDuration = new float[HandCooldownSlotCount];
    readonly float[] _weaponCooldownRemaining = new float[HandCooldownSlotCount];
    readonly float[] _weaponCooldownDuration = new float[HandCooldownSlotCount];
    readonly float[] _recoilRemaining = new float[HandCooldownSlotCount];
    float _aim01;
    readonly PendingAttack[] _pendingCues = new PendingAttack[PendingCueSlotCount];
    readonly string[] _hitChannelScratch = new string[AttackDamageTags.MaxChannels];

    public event Action AvailableActionsChanged;
    public event Action SelectedLeafChanged;
    public event Action PresentationChanged;
    public event Action ActiveWieldHandChanged;

    /// <summary>시전된 액션 판정(Performed/Miss 및 Cooling/NoAmmo/NoTarget/OutOfRange). 연출 계층이 구독한다.</summary>
    public event Action<AttackOutcome> AttackResolved;

    /// <summary>모든 CharacterAttacker Resolve 공통 훅 (메시지 로그 등).</summary>
    public static event Action<AttackOutcome> AnyAttackResolved;

    /// <summary>클립 큐에서 Attack 로직이 판정한 결과 (피해·히트 VFX).</summary>
    public event Action<AttackOutcome> AttackJudged;

    public static event Action<AttackOutcome> AnyAttackJudged;

    /// <summary>공격 클립 cue 도달. Impact Recoil 연출용.</summary>
    public event Action<WieldHand, CombatLeaf> AttackCueFired;

    public bool HasPendingAttackCue
    {
        get
        {
            for (int i = 0; i < _pendingCues.Length; i++)
            {
                if (_pendingCues[i].Armed && !_pendingCues[i].CueFired)
                    return true;
            }

            return false;
        }
    }

    public bool HasPendingFor(WieldHand hand)
    {
        int index = FindPending(hand);
        return index >= 0 && _pendingCues[index].Armed && !_pendingCues[index].CueFired;
    }

    public bool IsActionBusy
    {
        get
        {
            if (HasPendingAttackCue)
                return true;
            return HasAnyRemaining(_actionCooldownRemaining) ||
                   HasAnyRemaining(_weaponCooldownRemaining);
        }
    }

    public float CooldownProgress01
    {
        get
        {
            float bestRemaining = 0f;
            float bestDuration = 0f;
            ConsiderProgress(
                _actionCooldownRemaining,
                _actionCooldownDuration,
                ref bestRemaining,
                ref bestDuration);
            ConsiderProgress(
                _weaponCooldownRemaining,
                _weaponCooldownDuration,
                ref bestRemaining,
                ref bestDuration);

            if (bestDuration <= 0f)
                return HasPendingAttackCue ? 0f : 1f;
            return 1f - Mathf.Clamp01(bestRemaining / bestDuration);
        }
    }

    public LayerMask RangedObstructionMask => _rangedObstructionMask;

    public MapTopologyLineCast MapLineCast => _mapLineCast;

    public void BindMapCollision(MapTopologyLineCast lineCast) =>
        _mapLineCast = lineCast;

    public WeaponPresentation Presentation => _presentation;
    public WeaponPresentationCatalog Catalog => _catalog;
    public string ItemId => _itemId;
    public ItemInstance WieldedInstance => _wieldedInstance;
    public ItemStack WieldedStack => _wieldedStack;
    public CombatLeafMask AvailableActions { get; private set; }
    public CombatLeaf SelectedLeaf => _selectedLeaf;
    public WieldHand ActiveWieldHand => _activeWieldHand;

    /// <summary>CharacterState.IsAiming. NPC Attack은 SetAimDir. CharacterState 없으면 AimHeld.</summary>
    public bool IsAiming =>
        _characterState != null
            ? _characterState.IsAiming
            : _aimHeld;

    public string PreferredPartId =>
        string.IsNullOrEmpty(_preferredPartId) ? BodyPartIds.Torso : _preferredPartId;

    public void SetPreferredPart(string partId) =>
        _preferredPartId = string.IsNullOrEmpty(partId) ? BodyPartIds.Torso : partId;

    public void SetAimHeld(bool held) => _aimHeld = held;

    /// <summary>raise_guard 핸들러가 판정한 가드 유지.</summary>
    public bool IsRaiseActive { get; private set; }

    /// <summary>애니·시전 손. TwoHand / Left / Right.</summary>
    public void SetActiveWieldHand(WieldHand hand)
    {
        if (_activeWieldHand == hand)
            return;
        _activeWieldHand = hand;
        ActiveWieldHandChanged?.Invoke();
    }

    /// <summary>슬롯 → 애니 손. 양손 모드면 TwoHand.</summary>
    public static WieldHand AnimHandFrom(WieldSlots slots, WieldSlotId slot)
    {
        if (slots != null && slots.IsTwoHand)
            return WieldHand.TwoHand;
        return slot == WieldSlotId.Left ? WieldHand.Left : WieldHand.Right;
    }

    ItemData CurrentItem =>
        string.IsNullOrEmpty(_itemId) ? null : GameplayData.GetItem(_itemId);

    void Awake()
    {
        ResolveBodyRefs();
        _hitStop = CharacterHitStopState.Find(this);
        _combatVfx = new CharacterAttackerCombatVfx(this, _combatVfxTimeChannel);
        _combatVfx.Bind();
        if (_presentation != null)
            _presentation.RebuildSupportedActions();
        RefreshPresentationFromCatalog();
        RebuildAvailableActions();
        ApplySelectedFromInstance();
    }

    /// <summary>NpcSample GameplayCore 분리 후 루트·자식 SSOT. 스폰 1회 <see cref="CharacterBodyRefs"/> 캐시.</summary>
    void ResolveBodyRefs()
    {
        CharacterBodyRefs refs = this.GetBodyRefs();
        if (refs != null)
        {
            _skillsHost = refs.SkillsHost;
            _gearHost = refs.GearHost;
            refs.TryGet(out _climateHost);
            _actionHost = refs.ActionHost;
            _painHost = refs.PainHost;
            refs.TryGet(out _imbalanceHost);
            _bodyHost = refs.BodyHost;
            _characterState = refs.State;
            _motor = refs.Motor;
            _appearance = refs.Appearance;
            _locAnim = refs.LocomotionAnim;
        }
        else
        {
            _skillsHost = GetComponent<CharacterSkillsHost>();
            _gearHost = GetComponent<PlayerGearHost>();
            _climateHost = GetComponent<CharacterClimateHost>();
            _actionHost = GetComponent<CharacterActionHost>();
            _painHost = GetComponent<CharacterPainHost>();
            _imbalanceHost = GetComponent<CharacterImbalanceHost>();
            _bodyHost = GetComponent<CharacterBodyHost>();
            _characterState = CharacterBodyResolve.GetInBody<CharacterState>(this);
            _motor = CharacterBodyResolve.GetInBody<CharacterMotor>(this);
            _appearance = CharacterBodyResolve.GetInBody<CharacterAppearanceHost>(this);
            _locAnim = CharacterBodyResolve.GetInBody<CharacterLocomotionAnim>(this);
            if (_bodyHost == null)
                _bodyHost = CharacterBodyResolve.GetInBody<CharacterBodyHost>(this);
        }

        _selfCollider = ResolveSelfCollider();
    }

    Collider ResolveSelfCollider() => CharacterBodyResolve.GetBodyCollider(this);

    void OnEnable()
    {
        ICharacterSkills skills = _skillsHost != null ? _skillsHost.Skills : null;
        if (skills != null)
            skills.Refreshed += OnSkillsRefreshed;
        Camera.onPostRender += OnCameraPostRender;
        _combatVfx?.Bind();
    }

    void OnDisable()
    {
        Camera.onPostRender -= OnCameraPostRender;
        ICharacterSkills skills = _skillsHost != null ? _skillsHost.Skills : null;
        if (skills != null)
            skills.Refreshed -= OnSkillsRefreshed;
        CancelAllPendingCues();
        ApplyRaiseFromHandler(false);
        _combatVfx?.Unbind();
    }

    void Update()
    {
        DrawMeleeHitboxDebugLines();

        float dt = TimeScaleService.Delta(_timeChannel);
        if (_actionHost != null)
            dt *= _actionHost.ActionTickScale;
        if (_hitStop != null)
            dt *= _hitStop.SimScale;
        if (dt <= 0f)
            return;

        TickCooldownSlots(_actionCooldownRemaining, dt);
        TickCooldownSlots(_weaponCooldownRemaining, dt);
        TickRecoilSlots(dt);
        TickAimProgress(dt);

        TickRaiseGuard();
        DrawMeleeHitboxDebugLines();
    }

    /// <summary>들기(Wield) 훅. 카탈로그로 Presentation resolve. 선택은 ItemInstance. 약실은 인스턴스.</summary>
    public void SetWieldedItem(ItemStack stack) =>
        SetWieldedItemCore(stack?.ItemId ?? string.Empty, stack);

    /// <summary>인스턴스 없는 경로(비무장·인스펙터). 선택은 SO default / 로컬만.</summary>
    public void SetWieldedItem(string itemId) =>
        SetWieldedItemCore(itemId, null);

    void SetWieldedItemCore(string itemId, ItemStack stack)
    {
        ItemInstance instance = stack?.Instance;
        if (ReferenceEquals(_wieldedStack, stack) &&
            ReferenceEquals(_wieldedInstance, instance) &&
            string.Equals(_itemId, itemId, StringComparison.Ordinal))
            return;

        _wieldedStack = stack;
        _wieldedInstance = instance;
        _itemId = itemId ?? string.Empty;
        RefreshPresentationFromCatalog(notifyPresentation: !IsOccupiedDualHandSwap(stack));
        RebuildAvailableActions();
        ApplySelectedFromInstance();
    }

    bool IsOccupiedDualHandSwap(ItemStack stack)
    {
        WieldSlots slots = _gearHost != null ? _gearHost.Wield : null;
        if (slots == null || slots.IsTwoHand || stack == null)
            return false;
        if (slots.Left?.Item == null || slots.Right?.Item == null)
            return false;
        return slots.Contains(stack);
    }

    [Obsolete("Use SetWieldedItem")]
    public void SetEquippedItem(string itemId) =>
        SetWieldedItem(itemId);

    public void SetPresentation(WeaponPresentation presentation)
    {
        if (_presentation == presentation)
            return;

        _presentation = presentation;
        if (_presentation != null)
            _presentation.RebuildSupportedActions();
        RebuildAvailableActions();
        ApplySelectedFromInstance();
        PresentationChanged?.Invoke();
    }

    public void SetCatalog(WeaponPresentationCatalog catalog)
    {
        _catalog = catalog;
        RefreshPresentationFromCatalog();
    }

    public bool CanPerform(CombatLeaf action) =>
        (AvailableActions & CombatLeafUtil.ToMask(action)) != 0;

    public void CycleSelectedLeaf()
    {
        if (!CombatLeafUtil.TryNextAvailable(
                AvailableActions,
                _selectedLeaf,
                out CombatLeaf next))
            return;

        if (next == _selectedLeaf)
            return;

        _selectedLeaf = next;
        WriteSelectedToInstance(next);
        SelectedLeafChanged?.Invoke();
    }

    public bool TrySelectLeaf(CombatLeaf action)
    {
        if (!CanPerform(action))
            return false;
        WriteSelectedToInstance(action);
        if (_selectedLeaf == action)
            return true;
        _selectedLeaf = action;
        SelectedLeafChanged?.Invoke();
        return true;
    }

    [Obsolete("Use SelectedLeaf / TryPerformSelected. Distance no longer picks an action.")]
    public bool TryGetBestAction(float distance, out CombatLeaf action)
    {
        action = _selectedLeaf;
        if (!CanPerform(_selectedLeaf))
            return false;
        if (GetCooldown(_activeWieldHand) > 0f)
            return false;

        float range = CombatMath.RangeMeters(CurrentItem, _selectedLeaf, WeaponChamber.ResolveAmmo(_wieldedStack, _wieldedInstance));
        return distance <= range;
    }

    public bool TryReload() =>
        WeaponChamber.TryReload(_wieldedInstance, _wieldedStack, CurrentItem);

    public void ApplyRaiseFromHandler(bool active) =>
        IsRaiseActive = active;

    public AttackPerformResult TryPerform(
        CombatLeaf action,
        CharacterBodyHost targetHost,
        float offenseFactor = 1f)
    {
        if (!CanPerform(action))
        {
            Debug.LogWarning(
                $"[CharacterAttacker] Action {action} not available on {name}",
                this);
            return AttackPerformResult.Unsupported;
        }

        ItemData item = CurrentItem;
        if (action == CombatLeaf.Raise)
            return PerformRaise(action, targetHost, offenseFactor, item);
        if (action == CombatLeaf.Excavate)
            return PerformDig(action, targetHost, offenseFactor, item);
        if (action == CombatLeaf.Chop)
            return PerformChop(action, targetHost, offenseFactor, item);

        WeaponResolveMode resolveMode = CombatLeafUtil.ResolveMode(action);
        Vector3 origin = ResolveOrigin();
        AttackPerformResult gate = GateAction(action, item);
        // Cooling/pending/원거리 NoAmmo만 시전을 막는다.
        // 타깃·사거리는 시전 게이트가 아님 (근접=cue 히트박스, 원거리=조준축 발사).
        if (gate != AttackPerformResult.Performed)
            return gate;

        BeginActionCooldown(_activeWieldHand, ResolveActionCooldown(action));
        ArmPendingCue(action, targetHost, offenseFactor);

        AttackPerformResult signal = ResolveActionSignal(
            action, resolveMode, gate, targetHost, origin, item);

        if (!HasAttackOverlayWatch)
            NotifyAttackCue();

        return signal;
    }

    AttackPerformResult GateAction(CombatLeaf action, ItemData item)
    {
        if (GetCooldown(_activeWieldHand) > 0f)
            return AttackPerformResult.Cooling;

        if (_hitStop != null && _hitStop.IsFrozen)
            return AttackPerformResult.Cooling;

        if (_painHost != null && _painHost.IsPainShocked)
            return AttackPerformResult.Cooling;
        if (_imbalanceHost != null && _imbalanceHost.IsFullyUnbalanced)
            return AttackPerformResult.Cooling;
        if (_imbalanceHost == null && _motor != null && _motor.IsStaggered)
            return AttackPerformResult.Cooling;

        // cue 대기 중 재시전 → pending 리셋·시전 VFX 연타 방지
        if (HasPendingFor(_activeWieldHand))
            return AttackPerformResult.Cooling;

        if (action == CombatLeaf.Raise)
            return AttackPerformResult.Performed;

        if (CombatLeafUtil.IsRanged(action) &&
            !WeaponChamber.CanCommitFire(item, _wieldedInstance, _wieldedStack, AttackFor(action)))
            return AttackPerformResult.NoAmmo;

        return AttackPerformResult.Performed;
    }

    float ResolveAttackerWearEncAccuracyFactor()
    {
        EquipmentWearState wear = _gearHost != null ? _gearHost.Wear : null;
        if (wear == null)
            return 1f;
        return WearCombatDefense.WearEncAccuracyFactor(
            WearStatsAggregator.Aggregate(wear).TotalEncumbrance);
    }

    float ResolveAttackerEnvAccuracyFactor()
    {
        if (_climateHost == null)
            return 1f;
        BodyTemp bodyTemp = _climateHost.BodyTemperature;
        WearEnvExposure env = _climateHost.EnvExposure;
        if (bodyTemp == null || env == null)
            return 1f;
        return GearEnvPenalties.HitAccuracyFactor(bodyTemp.Feeling, env.Wetness01);
    }

    float ResolveAttackerImbalanceAccuracyFactor()
    {
        if (_imbalanceHost == null)
            return 1f;
        return _imbalanceHost.HitAccuracyFactor;
    }

    static EquipmentWearState ResolveTargetWear(CharacterBodyHost targetHost)
    {
        if (targetHost == null)
            return null;
        if (CharacterBodyResolve.TryGetInBody(targetHost, out PlayerGearHost gear))
            return gear.Wear;
        return null;
    }

    AttackPerformResult ResolveActionSignal(
        CombatLeaf action,
        WeaponResolveMode resolveMode,
        AttackPerformResult result,
        CharacterBodyHost targetHost,
        Vector3 origin,
        ItemData item)
    {
        Vector3 impact = ResolveOutcomeImpact(targetHost, origin, item, action);
        return Resolve(
            action,
            resolveMode,
            result,
            targetHost,
            string.Empty,
            0,
            origin,
            impact);
    }

    Vector3 ResolveOutcomeImpact(
        CharacterBodyHost targetHost,
        Vector3 origin,
        ItemData item,
        CombatLeaf action)
    {
        if (targetHost == null || targetHost.Body == null)
            return ResolveAimImpact(origin, item, action);

        Collider targetCollider = CharacterBodyResolve.GetBodyCollider(targetHost);
        Vector3 targetCenter = ResolveBodyCenter(targetHost.transform, targetCollider);
        return ResolveImpactPoint(targetCollider, targetCenter, origin);
    }

    Vector3 ResolveAimImpact(Vector3 origin, ItemData item, CombatLeaf action)
    {
        if (_characterState != null)
        {
            Vector3 aim = _characterState.AimWorldPoint;
            if (aim.sqrMagnitude > 1e-6f)
                return aim;

            Vector3 dir = _characterState.SightDir;
            if (dir.sqrMagnitude < 1e-6f)
                dir = _characterState.InteractionDir;
            if (dir.sqrMagnitude > 1e-6f)
            {
                float dist = _characterState.InteractionReach;
                if (dist > 0f)
                    return origin + dir.normalized * dist;
            }
        }

        Vector3 fallbackDir = ResolveBodyForwardXZ();

        float fallbackDist = CombatMath.RangeMeters(
            item,
            action,
            WeaponChamber.ResolveAmmo(_wieldedStack, _wieldedInstance));
        if (fallbackDist <= 0f)
            fallbackDist = FallbackImpactRadius;
        return origin + fallbackDir.normalized * fallbackDist;
    }

    AttackPerformResult Resolve(
        CombatLeaf action,
        WeaponResolveMode resolveMode,
        AttackPerformResult result,
        CharacterBodyHost target,
        string aimedPartId,
        int damage,
        Vector3 origin,
        Vector3 impact)
    {
        bool useSurpriseClip = false;
        if (result == AttackPerformResult.Performed &&
            !CombatLeafUtil.IsRanged(action) &&
            !CombatLeafUtil.SuppressesAttackTrigger(action))
        {
            CharacterBodyHost primary = target;
            if (primary == null)
                primary = CombatSurprise.ResolvePrimaryAnimTarget(_bodyHost);
            useSurpriseClip = primary != null &&
                              CombatSurprise.IsSurpriseHit(_bodyHost, primary);
        }

        var outcome = new AttackOutcome(
            action,
            _activeWieldHand,
            resolveMode,
            result,
            target,
            aimedPartId,
            damage,
            origin,
            impact,
            useSurpriseAttackClip: useSurpriseClip,
            attacker: _bodyHost);
        AttackResolved?.Invoke(outcome);
        AnyAttackResolved?.Invoke(outcome);
        return result;
    }

    public static Vector3 ResolveBodyCenter(Transform owner, Collider collider) =>
        collider != null
            ? collider.bounds.center
            : owner.position + Vector3.up * AimHeight;

    static Vector3 ResolveImpactPoint(
        Collider targetCollider,
        Vector3 targetCenter,
        Vector3 origin)
    {
        Vector3 offset = targetCenter - origin;
        float distance = offset.magnitude;
        if (distance <= MinRayDistance)
            return targetCenter;

        Vector3 direction = offset / distance;
        if (targetCollider != null &&
            targetCollider.Raycast(
                new Ray(origin, direction),
                out RaycastHit surface,
                distance + SurfaceProbeMargin))
        {
            return surface.point;
        }

        float radius = targetCollider != null
            ? Mathf.Min(targetCollider.bounds.extents.x, targetCollider.bounds.extents.z)
            : FallbackImpactRadius;
        return targetCenter - direction * radius;
    }

    /// <summary>
    /// Occupied 손 시전. 호출 1회=시전 의도 1회 (플레이어 클릭·NPC Attack 틱 공통).
    /// <see cref="IsActionBusy"/>면 Cooling. TwoHand·한손=그 손 1회.
    /// 듀얼=이번 손 1회, 그 손이 NoAmmo/Unsupported면 같은 호출 안 반대손.
    /// 손별 액션은 인스턴스 Selected.
    /// </summary>
    public AttackPerformResult TryPerformSelected(CharacterBodyHost targetHost)
    {
        if (IsActionBusy)
            return AttackPerformResult.Cooling;

        CharacterGearService gear = _gearHost != null ? _gearHost.Service : null;
        WieldSlots slots = gear != null ? gear.Wield : null;
        ICharacterSkills skills = _skillsHost != null ? _skillsHost.Skills : null;
        if (slots == null ||
            !PrimaryWieldResolver.TryResolvePrimary(
                slots,
                _catalog,
                skills,
                out PrimaryWieldResolver.HandScore primary,
                out PrimaryWieldResolver.HandScore secondary))
        {
            _hasLastDualSlot = false;
            SyncActiveHandFromGear();
            return TryPerform(_selectedLeaf, targetHost);
        }

        bool hasPrimary = primary.Action != null && primary.Stack?.Item != null;
        bool hasSecondary = !slots.IsTwoHand
            && secondary.Stack?.Item != null
            && secondary.Action != null
            && secondary.IsOffHand;
        if (!hasPrimary && !hasSecondary)
        {
            _hasLastDualSlot = false;
            SyncActiveHandFromGear();
            return TryPerform(_selectedLeaf, targetHost);
        }

        if (!hasSecondary)
        {
            _hasLastDualSlot = false;
            return PerformOccupiedHand(primary, slots, skills, targetHost);
        }

        if (!hasPrimary)
        {
            _hasLastDualSlot = false;
            return PerformOccupiedHand(secondary, slots, skills, targetHost);
        }

        bool preferSecondary = _hasLastDualSlot && _lastDualSlot == primary.Slot;
        PrimaryWieldResolver.HandScore first = preferSecondary ? secondary : primary;
        PrimaryWieldResolver.HandScore second = preferSecondary ? primary : secondary;
        AttackPerformResult result = PerformOccupiedHand(first, slots, skills, targetHost);
        if (result == AttackPerformResult.Performed)
            return result;
        return PerformOccupiedHand(second, slots, skills, targetHost);
    }

    AttackPerformResult PerformOccupiedHand(
        PrimaryWieldResolver.HandScore score,
        WieldSlots slots,
        ICharacterSkills skills,
        CharacterBodyHost targetHost)
    {
        if (score.Action == null || score.Stack?.Item == null)
            return AttackPerformResult.Unsupported;

        WieldHand hand = AnimHandFrom(slots, score.Slot);
        float factor = score.IsOffHand
            ? PrimaryWieldResolver.OffHandFactor(skills, hand)
            : 1f;
        ApplyWieldStep(score, hand);
        AttackPerformResult result = TryPerform(score.Action.Value, targetHost, factor);
        if (result == AttackPerformResult.Performed)
        {
            _lastDualSlot = score.Slot;
            _hasLastDualSlot = true;
        }

        return result;
    }

    void ApplyWieldStep(PrimaryWieldResolver.HandScore score, WieldHand hand)
    {
        if (score.Stack == null || score.Action == null)
            return;
        SetActiveWieldHand(hand);
        SetWieldedItem(score.Stack);
    }

    void SyncActiveHandFromGear()
    {
        WieldSlots slots = _gearHost != null ? _gearHost.Wield : null;
        if (slots == null)
            return;

        ICharacterSkills skills = _skillsHost != null ? _skillsHost.Skills : null;
        if (!PrimaryWieldResolver.TryResolvePrimary(
                slots,
                _catalog,
                skills,
                out PrimaryWieldResolver.HandScore primary,
                out _))
        {
            SetActiveWieldHand(WieldHand.TwoHand);
            return;
        }

        SetActiveWieldHand(AnimHandFrom(slots, primary.Slot));
    }

    AttackPerformResult PerformRaise(
        CombatLeaf action,
        CharacterBodyHost targetHost,
        float offenseFactor,
        ItemData item)
    {
        AttackPerformResult gate = GateAction(action, item);
        if (gate == AttackPerformResult.Performed)
            BeginActionCooldown(_activeWieldHand, ResolveActionCooldown(action));
        var context = new ActionHandlerContext(
            action,
            _activeWieldHand,
            AttackFor(action),
            targetHost,
            Mathf.Max(0f, offenseFactor),
            _itemId ?? string.Empty,
            _wieldedInstance,
            _wieldedStack);
        IActionHandler handler = ActionHandlerRegistry.Resolve(context.Attack, action);
        handler?.Execute(this, context);

        Vector3 origin = ResolveOrigin();
        Vector3 impact = ResolveOutcomeImpact(targetHost, origin, item, action);
        return Resolve(
            action,
            WeaponResolveMode.MeleeReach,
            gate,
            targetHost,
            string.Empty,
            0,
            origin,
            impact);
    }

    /// <summary>
    /// Dig Leaf outcome. ActiveWield 무기 스택의 DIG quality(level ≥ MinDig)가
    /// cue당 타일 내구도 피해 최소 조건. Dig 전용 손 스캔 아님.
    /// </summary>
    AttackPerformResult PerformDig(
        CombatLeaf action,
        CharacterBodyHost targetHost,
        float offenseFactor,
        ItemData item)
    {
        if (item == null || !MapPlantService.HasDigQuality(item))
            return AttackPerformResult.Unsupported;

        AttackPerformResult gate = GateAction(action, item);
        if (gate != AttackPerformResult.Performed)
            return gate;

        BeginActionCooldown(_activeWieldHand, ResolveActionCooldown(action));
        ArmPendingCue(action, targetHost, offenseFactor);

        Vector3 origin = ResolveOrigin();
        Vector3 impact = ResolveOutcomeImpact(targetHost, origin, item, action);
        AttackPerformResult signal = Resolve(
            action,
            WeaponResolveMode.MeleeBlock,
            AttackPerformResult.Performed,
            targetHost,
            string.Empty,
            0,
            origin,
            impact);

        if (!HasAttackOverlayWatch)
            NotifyAttackCue();

        return signal;
    }

    /// <summary>
    /// Chop Leaf outcome. ActiveWield 무기 AXE quality(level ≥ MinAxe)가
    /// cue당 나무 OccupiedCell 피해 최소 조건.
    /// </summary>
    AttackPerformResult PerformChop(
        CombatLeaf action,
        CharacterBodyHost targetHost,
        float offenseFactor,
        ItemData item)
    {
        if (item == null || !MapPlantService.HasAxeQuality(item))
            return AttackPerformResult.Unsupported;

        AttackPerformResult gate = GateAction(action, item);
        if (gate != AttackPerformResult.Performed)
            return gate;

        BeginActionCooldown(_activeWieldHand, ResolveActionCooldown(action));
        ArmPendingCue(action, targetHost, offenseFactor);

        Vector3 origin = ResolveOrigin();
        Vector3 impact = ResolveOutcomeImpact(targetHost, origin, item, action);
        AttackPerformResult signal = Resolve(
            action,
            WeaponResolveMode.MeleePlant,
            AttackPerformResult.Performed,
            targetHost,
            string.Empty,
            0,
            origin,
            impact);

        if (!HasAttackOverlayWatch)
            NotifyAttackCue();

        return signal;
    }

    /// <summary>
    /// Excavate cue당 인접 face 블록 피해. 채널 × TileDefinition 재질 + DIG potency.
    /// </summary>
    public int ResolveMeleeBlockBreakDamage(
        in ActionHandlerContext context,
        TileDefinition targetDefinition)
    {
        ItemData item = context.Stack?.Item ?? ItemFor(context.ItemId);
        int digLevel = MapPlantService.ResolveDigQualityLevel(item);
        ICharacterSkills skills = _skillsHost != null ? _skillsHost.Skills : null;
        int skillLevel = skills != null ? skills.Level(CombatSkillIds.Melee) : 0;
        int strength = skills != null
            ? skills.Level(AttributeIds.Str)
            : CombatMath.StrengthBaseline;
        return CombatMath.ResolveExcavateDamage(
            item,
            context.Leaf,
            digLevel,
            strength,
            skillLevel,
            targetDefinition?.materials,
            TileDefinitionCombat.MaterialThickness(targetDefinition),
            context.OffenseFactor);
    }

    /// <summary>Chop cue당 나무 OccupiedCell 피해. 채널 × 재질 + AXE potency.</summary>
    public int ResolveMeleePlantChopDamage(in ActionHandlerContext context)
    {
        ItemData item = context.Stack?.Item ?? ItemFor(context.ItemId);
        int axeLevel = MapPlantService.ResolveAxeQualityLevel(item);
        ICharacterSkills skills = _skillsHost != null ? _skillsHost.Skills : null;
        int skillLevel = skills != null ? skills.Level(CombatSkillIds.Melee) : 0;
        int strength = skills != null
            ? skills.Level(AttributeIds.Str)
            : CombatMath.StrengthBaseline;

        CharacterActionHost host = CharacterBodyResolve.GetInBody<CharacterActionHost>(this);
        TileDefinition definition = host != null ? host.ChopPipeline.ActiveDefinition : null;
        return CombatMath.ResolveStructureDamage(
            item,
            context.Leaf,
            strength,
            skillLevel,
            definition?.materials,
            TileDefinitionCombat.MaterialThickness(definition),
            context.OffenseFactor,
            ammo: null,
            toolPotencyBonus: axeLevel);
    }

    void TickRaiseGuard()
    {
        bool shouldRaise = CanPerform(CombatLeaf.Raise)
            && _selectedLeaf == CombatLeaf.Raise
            && IsAiming;
        if (shouldRaise == IsRaiseActive)
            return;

        if (!ActionHandlerRegistry.TryGet(ActionHandlerIds.RaiseGuard, out IActionHandler handler))
        {
            ApplyRaiseFromHandler(false);
            return;
        }

        var context = new ActionHandlerContext(
            CombatLeaf.Raise,
            _activeWieldHand,
            AttackFor(CombatLeaf.Raise),
            null,
            1f,
            _itemId ?? string.Empty,
            _wieldedInstance,
            _wieldedStack);
        handler.Execute(this, context);
    }

    public float GetWeaponCooldown(WieldHand hand) =>
        _weaponCooldownRemaining[CooldownIndex(hand)];

    public float GetActionCooldown(WieldHand hand) =>
        _actionCooldownRemaining[CooldownIndex(hand)];

    /// <summary>동작 쿨·무기 쿨 중 큰 쪽. cue 핸들러는 <see cref="GetWeaponCooldown"/>만 본다.</summary>
    public float GetCooldown(WieldHand hand) =>
        Mathf.Max(GetActionCooldown(hand), GetWeaponCooldown(hand));

    /// <summary>슬롯 radial fill. 동작·무기 쿨 중 남은 비율 (1=쿨 시작, 0=준비).</summary>
    public float GetCooldownOverlay01(WieldHand hand)
    {
        int index = CooldownIndex(hand);
        float remaining = 0f;
        float duration = 0f;
        ConsiderSlot(
            _actionCooldownRemaining[index],
            _actionCooldownDuration[index],
            ref remaining,
            ref duration);
        ConsiderSlot(
            _weaponCooldownRemaining[index],
            _weaponCooldownDuration[index],
            ref remaining,
            ref duration);
        if (duration <= 0f)
            return 0f;
        return Mathf.Clamp01(remaining / duration);
    }

    public ItemData ItemFor(string itemId) =>
        string.IsNullOrEmpty(itemId) ? null : GameplayData.GetItem(itemId);

    public Vector3 ResolveOrigin()
    {
        GameObject root = CharacterBodyResolve.GetBodyRoot(gameObject);
        Transform basis = root != null ? root.transform : transform;
        return ResolveBodyCenter(basis, _selfCollider);
    }

    /// <summary>원거리 발사 방향. 엔티티 락온 없음 — 조준축과 동일.</summary>
    public Vector3 ResolveFireDirection() => ResolveSwingAxis();

    public float GetRecoil(WieldHand hand) =>
        _recoilRemaining[CooldownIndex(hand)];

    public float Aim01 => _aim01;

    public float RangedEffectiveDispersion(WieldHand hand, ItemData item, ItemData ammo) =>
        CombatMath.EffectiveDispersion(item, ammo, GetRecoil(hand), ResolveAim01(item));

    /// <summary>
    /// 원거리 탄퍼짐 미리보기. 시전·킥 없음. 근접/비총이면 false.
    /// </summary>
    public bool TryPreviewRangedSpread(out float effectiveDispersion)
    {
        effectiveDispersion = 0f;
        ItemData item = CurrentItem;
        if (item?.gun == null || !CombatLeafUtil.IsRanged(_selectedLeaf))
            return false;

        effectiveDispersion = RangedEffectiveDispersion(
            _activeWieldHand,
            item,
            WeaponChamber.ResolveAmmo(_wieldedStack, _wieldedInstance));
        return true;
    }

    /// <summary>
    /// Excavate face 조준 미리보기. DIG quality + resolve + <see cref="MapDigService.CanBreak"/>.
    /// 시전 없음. resolve SSOT는 <paramref name="host"/>.
    /// </summary>
    public bool TryPreviewMeleeBlockTarget(
        PlayerCombatController host,
        out DigTileTarget target)
    {
        target = default;
        if (CombatLeafUtil.Normalize(_selectedLeaf) != CombatLeaf.Excavate)
            return false;

        ItemData item = CurrentItem;
        if (item == null || !MapPlantService.HasDigQuality(item))
            return false;

        if (host == null || !host.TryResolveDigTargetFromAim(out target))
            return false;

        return MapDigService.CanBreak(target);
    }

    float ResolveAim01(ItemData item)
    {
        if (_aimHeld)
            return 1f;
        if (!IsAiming)
            return 0f;
        if (CombatMath.AimSpeedOf(item) <= 0)
            return 1f;
        return _aim01;
    }

    /// <summary>effective dispersion yaw. 킥은 발사 후 <see cref="AddRecoilKick"/>.</summary>
    public Vector3 ResolveSpreadFireDirection(WieldHand hand, ItemData item, ItemData ammo)
    {
        return CombatMath.SpreadFireDirection(
            ResolveFireDirection(),
            RangedEffectiveDispersion(hand, item, ammo));
    }

    public void AddRecoilKick(WieldHand hand, ItemData item, ItemData ammo)
    {
        int index = CooldownIndex(hand);
        float mass = CombatImpulse.InertialMassKg(
            _appearance,
            _gearHost != null ? _gearHost.Wear : null,
            _gearHost != null ? _gearHost.Wield : null);
        float deltaV = CombatImpulse.ShooterDeltaV(item, ammo, mass);
        float kick = CombatImpulse.DispersionKickFromDeltaV(deltaV);
        float cap = CombatMath.RecoilRemainingMax(item, ammo);
        _recoilRemaining[index] = CombatMath.ApplyRecoilKick(
            _recoilRemaining[index],
            kick,
            cap);
        if (_motor != null && deltaV > 0.001f)
        {
            Vector3 back = -ResolveFireDirection();
            back.y = 0f;
            if (back.sqrMagnitude > 1e-6f)
                _motor.ApplyKnockback(back.normalized * deltaV);
        }
    }

    float ResolveImpulseJin(ItemData item, CombatLeaf action, ItemData ammo)
    {
        if (CombatLeafUtil.IsRanged(action))
            return CombatImpulse.ShotJin(item, ammo);

        ICharacterSkills skills = _skillsHost != null ? _skillsHost.Skills : null;
        int strength = skills != null
            ? skills.Level(AttributeIds.Str)
            : CombatMath.StrengthBaseline;
        return CombatImpulse.MeleeJin(item, action, strength);
    }

    public Vector3 ResolveSwingAxis()
    {
        Vector3 dir = Vector3.zero;
        if (_characterState != null)
        {
            dir = _characterState.SightDir;
            if (dir.sqrMagnitude < 1e-6f)
                dir = _characterState.InteractionDir;
            if (dir.sqrMagnitude < 1e-6f)
                dir = _characterState.GetFacingDir();
        }

        if (dir.sqrMagnitude < 1e-6f)
            dir = ResolveBodyForwardXZ();

        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f)
            return Vector3.forward;
        return dir.normalized;
    }

    Vector3 ResolveBodyForwardXZ()
    {
        GameObject root = CharacterBodyResolve.GetBodyRoot(gameObject);
        Transform basis = root != null ? root.transform : transform;
        Vector3 dir = basis.forward;
        dir.y = 0f;
        return dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
    }

    public bool IsOwnCollider(Collider collider) =>
        CharacterBodyResolve.IsColliderOnBody(this, collider);

    public int CollectMeleeHits(
        ItemData item,
        CombatLeaf action,
        WeaponAttack attack,
        CharacterBodyHost[] hosts,
        MeleeHitContact[] contacts)
    {
        int hitCount = MeleeHitbox.Collect(
            this, item, action, attack, _meleeColliders, hosts, contacts);
        if (ShouldDrawMeleeHitbox &&
            MeleeHitbox.TryGetPose(this, item, action, attack, out MeleeHitboxPose pose))
        {
            _debugCuePose = pose;
            _debugCueHitCount = hitCount;
            _debugContactCount = hitCount;
            int cap = hitCount < _debugContacts.Length ? hitCount : _debugContacts.Length;
            for (int i = 0; i < cap; i++)
                _debugContacts[i] = contacts[i].WorldPoint;
            _debugCueUntilUnscaled = Time.unscaledTime + MeleeHitbox.DebugCueHoldSeconds;
        }

        return hitCount;
    }

    /// <summary>Animator Animation Event. 클립 이벤트가 있으면 정규화 시각보다 우선.</summary>
    public void NotifyAttackCue()
    {
        for (int i = 0; i < _pendingCues.Length; i++)
        {
            if (_pendingCues[i].Armed && !_pendingCues[i].CueFired)
                ExecutePendingCue(i);
        }
    }

    public void NotifyAttackCueForHand(WieldHand hand)
    {
        int index = FindPending(hand);
        if (index < 0)
            return;
        ExecutePendingCue(index);
    }

    public void NotifyAttackOverlayTick(
        WieldHand hand,
        bool inAttack,
        float normalizedTime)
    {
        int index = FindPending(hand);
        if (index < 0)
            return;

        PendingAttack pending = _pendingCues[index];
        if (!pending.Armed || pending.CueFired)
            return;

        if (inAttack)
        {
            float cycle = CycleNormalizedTime(normalizedTime);
            if (!pending.SawCueWindup && IsCueWindup(cycle, pending.CueNormalizedTime))
                pending.SawCueWindup = true;
            pending.SawAttackState = true;
            _pendingCues[index] = pending;
            if (pending.SawCueWindup && cycle >= pending.CueNormalizedTime)
                ExecutePendingCue(index);
            return;
        }

        if (!pending.SawAttackState || !pending.SawCueWindup)
            return;

        CancelPendingAt(index);
    }

    static float CycleNormalizedTime(float normalizedTime)
    {
        if (normalizedTime < 0f)
            return 0f;
        return normalizedTime - Mathf.Floor(normalizedTime);
    }

    static bool IsCueWindup(float cycleNormalizedTime, float cueNormalizedTime)
    {
        if (cycleNormalizedTime < cueNormalizedTime)
            return true;
        return cueNormalizedTime <= CueCycleEpsilon &&
               cycleNormalizedTime <= CueCycleEpsilon;
    }

    bool HasAttackOverlayWatch =>
        _locAnim != null && _locAnim.HasAttackTrigger;

    void ArmPendingCue(
        CombatLeaf action,
        CharacterBodyHost targetHost,
        float offenseFactor)
    {
        WeaponAttack attack = AttackFor(action);

        float cueTime = attack != null
            ? attack.CueNormalizedTime
            : WeaponAttack.DefaultCueNormalizedTime;

        int index = FindPending(_activeWieldHand);
        if (index < 0)
            index = FindEmptyPending();
        if (index < 0)
            index = 0;

        _pendingCues[index] = new PendingAttack
        {
            Armed = true,
            CueFired = false,
            SawAttackState = false,
            SawCueWindup = false,
            Action = action,
            Hand = _activeWieldHand,
            Target = targetHost,
            OffenseFactor = Mathf.Max(0f, offenseFactor),
            Attack = attack,
            CueNormalizedTime = cueTime,
            ItemId = _itemId ?? string.Empty,
            Instance = _wieldedInstance,
            Stack = _wieldedStack
        };
    }

    public WeaponAttack ResolveAttack(CombatLeaf action) => AttackFor(action);

    public bool AllowsImpactReaction(CombatLeaf action, ArmImpactKind kind) =>
        WeaponAttack.AllowsImpactReaction(AttackFor(action), kind);

    WeaponAttack AttackFor(CombatLeaf action) =>
        EntryFor(action)?.attack;

    WeaponPresentation.Entry EntryFor(CombatLeaf action)
    {
        if (_presentation != null &&
            _presentation.TryGetEntry(action, out WeaponPresentation.Entry entry))
            return entry;
        return null;
    }

    float ResolveActionCooldown(CombatLeaf action)
    {
        WeaponPresentation.Entry entry = EntryFor(action);
        return entry != null ? entry.ActionCooldownSeconds : 0f;
    }

    int FindPending(WieldHand hand)
    {
        for (int i = 0; i < _pendingCues.Length; i++)
        {
            if (_pendingCues[i].Armed && _pendingCues[i].Hand == hand)
                return i;
        }

        return -1;
    }

    int FindEmptyPending()
    {
        for (int i = 0; i < _pendingCues.Length; i++)
        {
            if (!_pendingCues[i].Armed)
                return i;
        }

        return -1;
    }

    void ExecutePendingCue(int index)
    {
        PendingAttack pending = _pendingCues[index];
        if (!pending.Armed || pending.CueFired)
            return;

        pending.CueFired = true;
        pending.Armed = false;
        _pendingCues[index] = pending;

        AttackCueFired?.Invoke(pending.Hand, pending.Action);

        var context = new ActionHandlerContext(
            pending.Action,
            pending.Hand,
            pending.Attack,
            pending.Target,
            pending.OffenseFactor,
            pending.ItemId,
            pending.Instance,
            pending.Stack);

        IActionHandler handler = ActionHandlerRegistry.Resolve(pending.Attack, pending.Action);
        if (handler == null)
        {
            ItemData item = ItemFor(pending.ItemId);
            Vector3 origin = ResolveOrigin();
            EmitJudgedGate(
                context,
                CombatLeafUtil.ResolveMode(pending.Action),
                AttackPerformResult.Unsupported,
                item,
                origin);
            return;
        }

        handler.Execute(this, context);
    }

    void CancelPendingAt(int index)
    {
        _pendingCues[index] = default;
    }

    public void CancelAllPendingCues()
    {
        for (int i = 0; i < _pendingCues.Length; i++)
            _pendingCues[i] = default;
    }

    public void EmitJudgedGate(
        in ActionHandlerContext context,
        WeaponResolveMode resolveMode,
        AttackPerformResult result,
        ItemData item,
        Vector3 origin)
    {
        Vector3 impact = ResolveOutcomeImpact(context.Target, origin, item, context.Leaf);
        EmitJudged(
            context,
            resolveMode,
            result,
            context.Target,
            string.Empty,
            0,
            origin,
            impact,
            item);
    }

    public float ResolveCommittedHit(
        in ActionHandlerContext context,
        WeaponResolveMode resolveMode,
        ItemData item,
        Vector3 origin,
        bool consumeAmmo,
        ItemData ammo = null,
        bool applyCooldown = true,
        bool rollHitChance = true,
        bool practice = true,
        float weaponReach01 = 0f,
        Vector3? impactOverride = null,
        float rangedEffectiveDispersion = -1f,
        float impulseJinOverride = -1f,
        bool impulseContinues = false)
    {
        CharacterBodyHost targetHost = context.Target;
        if (targetHost == null || targetHost.Body == null)
        {
            EmitJudgedGate(context, resolveMode, AttackPerformResult.NoTarget, item, origin);
            return 0f;
        }

        Collider targetCollider = CharacterBodyResolve.GetBodyCollider(targetHost);
        Vector3 targetCenter = ResolveBodyCenter(targetHost.transform, targetCollider);

        if (!AimPartResolver.TryResolve(
                targetHost.Body,
                PreferredPartId,
                out string aimedPart))
        {
            EmitJudgedGate(context, resolveMode, AttackPerformResult.NoTarget, item, origin);
            return 0f;
        }

        Vector3 impact = impactOverride ?? ResolveImpactPoint(targetCollider, targetCenter, origin);
        ammo ??= WeaponChamber.ResolveAmmo(context.Stack, context.Instance);
        CommitAttempt(context, item, consumeAmmo, ammo, applyCooldown: applyCooldown, practice: practice);

        ICharacterSkills skills = _skillsHost != null ? _skillsHost.Skills : null;
        int channelCount = AttackDamageTags.WriteChannels(
            item, context.Leaf, _hitChannelScratch, ammo);
        string hitTag = channelCount > 0
            ? _hitChannelScratch[0]
            : AttackDamageTags.Fallback;
        string skillId = CombatMath.SkillIdForTag(item, hitTag);
        int skillLevel = skills != null && !string.IsNullOrEmpty(skillId)
            ? skills.Level(skillId)
            : 0;
        int strength = skills != null ? skills.Level(AttributeIds.Str) : CombatMath.StrengthBaseline;
        float factor = context.OffenseFactor;

        if (rollHitChance)
        {
            float hitChance = CombatMath.HitChance(
                    item,
                    context.Leaf,
                    skillLevel,
                    aimedPart,
                    ammo,
                    rangedEffectiveDispersion)
                * factor
                * ResolveAttackerWearEncAccuracyFactor()
                * ResolveAttackerEnvAccuracyFactor()
                * ResolveAttackerImbalanceAccuracyFactor();

            if (UnityEngine.Random.value > hitChance)
                aimedPart = AimPartResolver.ScatterToNeighbor(targetHost.Body, aimedPart);
        }

        bool isSurprise = CombatSurprise.IsSurpriseHit(_bodyHost, targetHost);
        SurpriseMeleeKind surpriseMelee = SurpriseMeleeKind.None;
        if (isSurprise && !CombatLeafUtil.IsRanged(context.Leaf))
        {
            int defStr = CombatSurprise.ResolveStrength(targetHost);
            surpriseMelee = CombatSurprise.RollMeleeSpecial(strength, defStr);
            if (surpriseMelee == SurpriseMeleeKind.Neck &&
                targetHost.Body.Has(BodyPartIds.Neck))
                aimedPart = BodyPartIds.Neck;
        }

        EquipmentWearState wear = ResolveTargetWear(targetHost);
        int damage = 0;
        int rawDamage = 0;
        string hitPart = OrganHitResolver.Resolve(targetHost.Body, aimedPart);
        if (surpriseMelee == SurpriseMeleeKind.Neck &&
            targetHost.Body.Has(BodyPartIds.Neck))
            hitPart = BodyPartIds.Neck;

        string appliedTissueId = string.Empty;
        bool didSeverPart = false;
        int tissueRank = 0;
        for (int i = 0; i < channelCount; i++)
        {
            string damageTag = _hitChannelScratch[i];
            int channelRaw = Mathf.Max(
                0,
                Mathf.RoundToInt(
                    CombatMath.DamageForTag(item, damageTag, strength, skillLevel, ammo)
                    * factor
                    * CombatImpulse.HpFactor(item)));
            if (isSurprise)
                channelRaw = CombatSurprise.ApplyDamageMultiplier(channelRaw);
            rawDamage += channelRaw;
            WearCombatDefense.ArmorMitigateResult mitigated = WearCombatDefense.MitigateDamage(
                wear,
                aimedPart,
                channelRaw,
                damageTag,
                CombatImpulse.ArmorPen(ammo));
            BodyPartEffect[] seeds =
                string.Equals(mitigated.DamageTag, AttackDamageTags.Cut, StringComparison.Ordinal)
                    ? BuildSeeds(_presentation, context.Leaf, context.Attack)
                    : null;
            BodyHitApplyResult applied = BodyDamageService.ApplyHit(
                targetHost.Body,
                hitPart,
                mitigated.Damage,
                seeds,
                mitigated.DamageTag);
            if (applied.Severed)
                didSeverPart = true;
            int rank = BodyInjury.OverlayRank(applied.TissueId);
            if (rank > tissueRank)
            {
                tissueRank = rank;
                appliedTissueId = applied.TissueId;
            }

            damage += mitigated.Damage;
        }

        if (CombatImpulse.IsBeanbag(ammo))
            damage = 0;

        if (surpriseMelee == SurpriseMeleeKind.Stun &&
            CharacterBodyResolve.TryGetInBody(targetHost, out CharacterPainHost targetPain))
            targetPain.ApplySurpriseStun(CombatSurprise.StunSeconds);

        float jinIn = impulseJinOverride >= 0f
            ? impulseJinOverride
            : ResolveImpulseJin(item, context.Leaf, ammo);
        float p = CombatImpulse.Penetration01(damage, rawDamage);
        EmitJudged(
            context,
            resolveMode,
            AttackPerformResult.Performed,
            targetHost,
            hitPart,
            damage,
            origin,
            impact,
            item,
            ammo,
            weaponReach01,
            rawDamage,
            CombatImpulse.HitJin(jinIn, impulseContinues, p),
            appliedTissueId,
            didSeverPart,
            isSurprise,
            surpriseMelee);
        return p;
    }

    public void CommitAttempt(
        in ActionHandlerContext context,
        ItemData item,
        bool consumeAmmo,
        ItemData ammo = null,
        bool applyCooldown = true,
        bool practice = true)
    {
        if (applyCooldown && !CombatLeafUtil.IsRanged(context.Leaf))
        {
            float cooldown = CombatMath.AttackIntervalSeconds(item, context.Leaf);
            BeginWeaponCooldown(context.Hand, cooldown);
        }

        if (consumeAmmo)
        {
            WeaponChamber.TryConsume(context.Instance);
            PlayerGearHost.Active?.Service?.NotifyAmmoChanged();
        }
        if (practice)
            Practice(item, context.Attack, context.Leaf, ammo);
    }

    public void EmitJudged(
        in ActionHandlerContext context,
        WeaponResolveMode resolveMode,
        AttackPerformResult result,
        CharacterBodyHost target,
        string aimedPartId,
        int damage,
        Vector3 origin,
        Vector3 impact,
        ItemData item = null,
        ItemData ammo = null,
        float weaponReach01 = 0f,
        int rawDamage = 0,
        float impulseJinOverride = -1f,
        string appliedTissueId = null,
        bool didSeverPart = false,
        bool isSurprise = false,
        SurpriseMeleeKind surpriseMelee = SurpriseMeleeKind.None)
    {
        if (item == null)
            item = ItemFor(context.ItemId);
        ammo ??= WeaponChamber.ResolveAmmo(context.Stack, context.Instance);
        int n = AttackDamageTags.WriteChannels(item, context.Leaf, _hitChannelScratch, ammo);
        string hitTag = n > 0 ? _hitChannelScratch[0] : AttackDamageTags.Fallback;
        float impulseJin = 0f;
        if (result == AttackPerformResult.Performed)
        {
            impulseJin = impulseJinOverride >= 0f
                ? impulseJinOverride
                : ResolveImpulseJin(item, context.Leaf, ammo);
        }
        var outcome = new AttackOutcome(
            context.Leaf,
            context.Hand,
            resolveMode,
            result,
            target,
            aimedPartId,
            damage,
            origin,
            impact,
            hitTag,
            context.Attack,
            weaponReach01,
            rawDamage,
            impulseJin,
            appliedTissueId,
            didSeverPart,
            isSurprise,
            surpriseMelee,
            attacker: _bodyHost);
        AttackJudged?.Invoke(outcome);
        AnyAttackJudged?.Invoke(outcome);
    }

    void BeginActionCooldown(WieldHand hand, float seconds)
    {
        if (seconds <= 0f)
            return;
        BeginCooldownSlot(
            _actionCooldownRemaining,
            _actionCooldownDuration,
            hand,
            seconds);
    }

    void BeginWeaponCooldown(WieldHand hand, float seconds)
    {
        BeginCooldownSlot(
            _weaponCooldownRemaining,
            _weaponCooldownDuration,
            hand,
            seconds);
    }

    void BeginCooldownSlot(
        float[] remaining,
        float[] durationSlots,
        WieldHand hand,
        float seconds)
    {
        int index = CooldownIndex(hand);
        float duration = Mathf.Max(0f, seconds);
        remaining[index] = duration;
        durationSlots[index] = duration;
    }

    static void TickCooldownSlots(float[] remaining, float dt)
    {
        for (int i = 0; i < remaining.Length; i++)
        {
            if (remaining[i] <= 0f)
                continue;
            remaining[i] = Mathf.Max(0f, remaining[i] - dt);
        }
    }

    void TickRecoilSlots(float dt)
    {
        if (dt <= 0f)
            return;

        for (int i = 0; i < _recoilRemaining.Length; i++)
        {
            if (_recoilRemaining[i] <= 0f)
                continue;
            _recoilRemaining[i] = CombatMath.DecayRecoilRemaining(_recoilRemaining[i], dt);
        }
    }

    void TickAimProgress(float dt)
    {
        if (_aimHeld)
        {
            _aim01 = 1f;
            return;
        }

        if (!IsAiming)
        {
            _aim01 = 0f;
            return;
        }

        int aimSpeed = CombatMath.AimSpeedOf(CurrentItem);
        if (aimSpeed <= 0)
        {
            _aim01 = 1f;
            return;
        }

        _aim01 = Mathf.Min(1f, _aim01 + dt * CombatMath.AimProgressPerSecond(aimSpeed));
    }

    static bool HasAnyRemaining(float[] remaining)
    {
        for (int i = 0; i < remaining.Length; i++)
        {
            if (remaining[i] > 0f)
                return true;
        }

        return false;
    }

    static void ConsiderProgress(
        float[] remaining,
        float[] durationSlots,
        ref float bestRemaining,
        ref float bestDuration)
    {
        for (int i = 0; i < remaining.Length; i++)
        {
            if (remaining[i] <= bestRemaining)
                continue;
            bestRemaining = remaining[i];
            bestDuration = durationSlots[i];
        }
    }

    static void ConsiderSlot(
        float remaining,
        float duration,
        ref float bestRemaining,
        ref float bestDuration)
    {
        if (remaining <= bestRemaining)
            return;
        bestRemaining = remaining;
        bestDuration = duration;
    }

    static int CooldownIndex(WieldHand hand)
    {
        int index = (int)hand;
        if ((uint)index >= HandCooldownSlotCount)
            return (int)WieldHand.Right;
        return index;
    }

    void Practice(ItemData item, WeaponAttack attack, CombatLeaf action, ItemData ammo = null)
    {
        if (_skillsHost == null)
            return;
        _ = attack;
        string damageTag = AttackDamageTags.Resolve(item, action, ammo);
        string skillId = CombatMath.SkillIdForTag(item, damageTag);
        int xp = CombatMath.PracticeXp(action);
        if (string.IsNullOrEmpty(skillId) || xp <= 0)
            return;
        _skillsHost.Skills?.AddPractice(skillId, xp);
    }

    static BodyPartEffect[] BuildSeeds(
        WeaponPresentation presentation,
        CombatLeaf action,
        WeaponAttack attack)
    {
        if (presentation != null &&
            presentation.TryGetEntry(action, out WeaponPresentation.Entry entry) &&
            entry?.effectSeeds != null &&
            entry.effectSeeds.Length > 0)
        {
            var seeds = new BodyPartEffect[entry.effectSeeds.Length];
            for (int i = 0; i < entry.effectSeeds.Length; i++)
            {
                WeaponPresentation.EffectSeed seed = entry.effectSeeds[i];
                if (seed == null)
                {
                    seeds[i] = default;
                    continue;
                }

                seeds[i] = new BodyPartEffect(
                    seed.effectId,
                    seed.intensity,
                    seed.remainingSeconds);
            }

            return seeds;
        }

        if (attack == null ||
            attack.EffectSeeds == null ||
            attack.EffectSeeds.Length == 0)
            return null;

        var attackSeeds = new BodyPartEffect[attack.EffectSeeds.Length];
        for (int i = 0; i < attack.EffectSeeds.Length; i++)
        {
            WeaponAttack.EffectSeed seed = attack.EffectSeeds[i];
            if (seed == null)
            {
                attackSeeds[i] = default;
                continue;
            }

            attackSeeds[i] = new BodyPartEffect(
                seed.effectId,
                seed.intensity,
                seed.remainingSeconds);
        }

        return attackSeeds;
    }

    struct PendingAttack
    {
        public bool Armed;
        public bool CueFired;
        public bool SawAttackState;
        public bool SawCueWindup;
        public CombatLeaf Action;
        public WieldHand Hand;
        public CharacterBodyHost Target;
        public float OffenseFactor;
        public WeaponAttack Attack;
        public float CueNormalizedTime;
        public string ItemId;
        public ItemInstance Instance;
        public ItemStack Stack;
    }

    void OnSkillsRefreshed() => RebuildAvailableActions();

    void RefreshPresentationFromCatalog(bool notifyPresentation = true)
    {
        if (_catalog == null)
            return;

        WeaponPresentation resolved = _catalog.Resolve(_itemId, CurrentItem);
        if (resolved == _presentation)
            return;

        _presentation = resolved;
        if (_presentation != null)
            _presentation.RebuildSupportedActions();
        if (notifyPresentation)
            PresentationChanged?.Invoke();
    }

    void RebuildAvailableActions()
    {
        CombatLeafMask previous = AvailableActions;
        AvailableActions = CombatLeafRows.Available(_presentation);
        if (previous != AvailableActions)
            AvailableActionsChanged?.Invoke();
        ApplySelectedFromInstance();
    }

    void ApplySelectedFromInstance()
    {
        CombatLeaf next = CombatLeafRows.ResolveSelected(_wieldedInstance, _presentation);
        if (next == _selectedLeaf)
            return;

        _selectedLeaf = next;
        SelectedLeafChanged?.Invoke();
    }

    void WriteSelectedToInstance(CombatLeaf action)
    {
        if (_wieldedInstance == null)
            return;
        _wieldedInstance.SelectedLeaf = action;
    }

    bool ShouldDrawMeleeHitbox => Config.DebugMode.MeleeHitbox;

    void DrawMeleeHitboxDebugLines()
    {
        if (!ShouldDrawMeleeHitbox)
            return;
        if (!TryGetMeleeHitboxDebugDraw(out MeleeHitboxPose pose, out Color color, out bool cueHold))
            return;

        MeleeHitbox.DrawDebugWire(pose, color, 0f);
        if (!cueHold)
            return;
        for (int i = 0; i < _debugContactCount; i++)
            MeleeHitbox.DrawDebugContact(_debugContacts[i], 0f);
    }

    void OnDrawGizmos()
    {
        if (!ShouldDrawMeleeHitbox)
            return;
        if (!TryGetMeleeHitboxDebugDraw(out MeleeHitboxPose pose, out Color color, out bool cueHold))
            return;

        MeleeHitbox.DrawGizmoWire(pose, color);
        if (!cueHold)
            return;
        for (int i = 0; i < _debugContactCount; i++)
            MeleeHitbox.DrawGizmoContact(_debugContacts[i]);
    }

    void OnCameraPostRender(Camera cam)
    {
        if (cam == null)
            return;
        if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection)
            return;
        if ((cam.cullingMask & (1 << gameObject.layer)) == 0)
            return;
        if (!ShouldDrawMeleeHitbox)
            return;
        if (!TryGetMeleeHitboxDebugDraw(out MeleeHitboxPose pose, out Color color, out bool cueHold))
            return;

        MeleeHitbox.DrawGl(
            pose,
            color,
            cueHold ? _debugContacts : null,
            cueHold ? _debugContactCount : 0,
            cam);
    }

    bool TryGetMeleeHitboxDebugDraw(out MeleeHitboxPose pose, out Color color, out bool cueHold)
    {
        pose = default;
        color = MeleeHitbox.PreviewWire;
        cueHold = Time.unscaledTime < _debugCueUntilUnscaled && _debugCuePose.IsValid;
        if (cueHold)
        {
            pose = _debugCuePose;
            color = _debugCueHitCount > 0 ? MeleeHitbox.CueHitWire : MeleeHitbox.CueMissWire;
            return true;
        }

        if (CombatLeafUtil.IsRanged(_selectedLeaf) ||
            CombatLeafUtil.SuppressesAttackTrigger(_selectedLeaf))
            return false;

        if (!MeleeHitbox.TryGetPose(
                this,
                CurrentItem,
                _selectedLeaf,
                AttackFor(_selectedLeaf),
                out pose))
            return false;

        color = MeleeHitbox.PreviewWire;
        return true;
    }
}
