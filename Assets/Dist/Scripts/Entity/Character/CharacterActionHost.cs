// ============================================================
// CharacterActionHost — 행위자 1줄 행동 큐(종류별) + CancelAll + TickScale
// ============================================================

using System;
using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;
using UnityEngine;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public sealed class CharacterActionHost : MonoBehaviour, IUiCancelConsumer
{
    struct Job
    {
        public CharacterActionKind Kind;
        public Func<bool> Start;
    }

    [SerializeField] UICraftingController _crafting;
    [SerializeField] FarmWorkClipCatalog _farmWorkClips;
    [SerializeField] FishWorkClipCatalog _fishWorkClips;

    readonly List<Job> _queue = new();
    readonly List<BodyPartEffect> _effectScratch = new(16);
    readonly ICharacterActionSource[] _sources = new ICharacterActionSource[(int)CharacterActionKind.Cell + 1];

    CharacterBodyHost _bodyHost;
    CharacterSkillsHost _skillsHost;
    CharacterPainHost _painHost;
    PlayerGearHost _gearHost;
    InventoryTimedMoveHost _moveHost;
    CharacterAttacker _attacker;
    CharacterArriveHost _arriveHost;
    CharacterVaultHost _vaultHost;
    CharacterMotor _motor;
    CharacterCellFarmPipeline _farm;
    CharacterCellFishPipeline _fish;
    CharacterCellConstructionPipeline _construction;
    CharacterDigPipeline _dig;
    CharacterChopPipeline _chop;
    CharacterSightHost _sightHost;
    CharacterActionKind _currentKind;
    bool _dispatching;
    float _tickScale = 1f;
    bool _knockdownDown;
    ICharacterDefeat _subscribedDefeat;

    public int CancelPriority => UiCancelPriority.CharacterAction;

    public CharacterActionKind CurrentKind => _currentKind;
    public int QueueCount => _queue.Count;
    public bool IsDispatching => _dispatching;
    public float ActionTickScale => _tickScale;
    public bool IsBusy => _currentKind != CharacterActionKind.None || _queue.Count > 0;

    /// <summary>게이지 표시 SSOT — CurrentKind만으로는 cancel 후 stale가 남을 수 있음.</summary>
    public bool HasVisibleProgress =>
        _currentKind != CharacterActionKind.None &&
        (IsCellArriving || IsSourceBusy(_currentKind));

    public CharacterSightHost SightHost
    {
        get
        {
            EnsureSightHost();
            return _sightHost;
        }
    }

    public CharacterDigPipeline DigPipeline
    {
        get
        {
            EnsureDigPipeline();
            return _dig;
        }
    }

    public CharacterChopPipeline ChopPipeline
    {
        get
        {
            EnsureChopPipeline();
            return _chop;
        }
    }

    public bool HasCancellableWork =>
        _queue.Count > 0 ||
        (_dig != null && _dig.IsActive) ||
        (_chop != null && _chop.IsActive) ||
        (_currentKind != CharacterActionKind.None && _currentKind != CharacterActionKind.Combat);

    public event Action Changed;

    /// <summary>Cell Arrive(자동이동) 중 — 게이지는 fill 대신 AutoProgressIcon.</summary>
    public bool IsCellArriving =>
        _currentKind == CharacterActionKind.Cell &&
        _arriveHost != null &&
        _arriveHost.IsBusy;

    public float Progress01 => GetSource(_currentKind)?.Progress01 ?? 0f;

    void Awake()
    {
        ResolveBodyRefs();
        EnsureCellPipelines();
        BuildSources();
        if (_crafting == null)
            _crafting = FindAnyObjectByType<UICraftingController>();
        RefreshTickScale();
    }

    void ResolveBodyRefs()
    {
        CharacterBodyRefs refs = this.GetBodyRefs();
        if (refs != null)
        {
            _bodyHost = refs.BodyHost;
            _skillsHost = refs.SkillsHost;
            _painHost = refs.PainHost;
            _gearHost = refs.GearHost;
            _attacker = refs.Attacker;
            _motor = refs.Motor;
            _vaultHost = refs.VaultHost;
            _moveHost = refs.TimedMoveHost;
        }
        else
        {
            _bodyHost = CharacterBodyResolve.GetModule<CharacterBodyHost>(this);
            _skillsHost = CharacterBodyResolve.GetModule<CharacterSkillsHost>(this);
            _painHost = CharacterBodyResolve.GetModule<CharacterPainHost>(this);
            _gearHost = CharacterBodyResolve.GetModule<PlayerGearHost>(this);
            _attacker = CharacterBodyResolve.GetModule<CharacterAttacker>(this);
            _motor = CharacterBodyResolve.GetInBody<CharacterMotor>(this);
            _vaultHost = CharacterBodyResolve.GetInBody<CharacterVaultHost>(this);
        }

        if (_moveHost == null)
            _moveHost = CharacterBodyResolve.GetModule<InventoryTimedMoveHost>(this);
        _arriveHost = CharacterBodyResolve.GetInBody<CharacterArriveHost>(this);
    }

    void BuildSources()
    {
        _sources[(int)CharacterActionKind.Gear] = new GearActionSource(_gearHost);
        _sources[(int)CharacterActionKind.Inventory] = new InventoryActionSource(_moveHost);
        _sources[(int)CharacterActionKind.Craft] = new CraftActionSource(_crafting);
        _sources[(int)CharacterActionKind.Combat] = new CombatActionSource(_attacker);
        _sources[(int)CharacterActionKind.Cell] = new CellActionSource(
            _farm,
            _fish,
            _construction,
            _arriveHost,
            _vaultHost,
            _dig,
            _chop);
    }

    void EnsureCellPipelines()
    {
        if (_farmWorkClips == null)
            _farmWorkClips = FarmWorkClipCatalog.Runtime;
        if (_fishWorkClips == null)
            _fishWorkClips = FishWorkClipCatalog.Runtime;

        _farm ??= new CharacterCellFarmPipeline(this, _arriveHost, _motor, _farmWorkClips);
        _fish ??= new CharacterCellFishPipeline(this, _arriveHost, _motor, _fishWorkClips);
        _construction ??= new CharacterCellConstructionPipeline(this, _arriveHost, _motor);
        EnsureDigPipeline();
        EnsureChopPipeline();
    }

    void EnsureDigPipeline()
    {
        EnsureSightHost();
        if (_dig == null)
            _dig = new CharacterDigPipeline();
        _dig.BindSightHost(_sightHost);
    }

    void EnsureSightHost()
    {
        if (_sightHost != null)
            return;

        if (TryGetComponent(out _sightHost))
            return;

        Debug.LogError(
            "[CharacterActionHost] CharacterSightHost missing on body. Add it on the prefab (NpcSample GameplayCore).",
            this);
    }

    void EnsureChopPipeline() =>
        _chop ??= new CharacterChopPipeline();

    void OnEnable()
    {
        UiCancelRouter.Register(this);
        SubscribeKnockdownSignals();
        _knockdownDown = IsKnockdownDown();
    }

    void OnDisable()
    {
        UiCancelRouter.Unregister(this);
        UnsubscribeKnockdownSignals();
        _queue.Clear();
        if (_currentKind != CharacterActionKind.Combat)
            CancelCurrentWork();
        _currentKind = CharacterActionKind.None;
        _farm?.OnOwnerDisabled();
        _fish?.OnOwnerDisabled();
        _construction?.OnOwnerDisabled();
        _dig?.Clear();
        _chop?.Clear();
        ResetKnockdownLatches();
    }

    public bool TryHandleCancel()
    {
        if (_motor != null && !_motor.IsPossessed)
            return false;
        if (!HasCancellableWork)
            return false;

        CancelAll();
        return true;
    }

    void Update()
    {
        // Rule 6: scratch 재사용. TickScale·소스 idle 폴링만. 할당 없음.
        RefreshTickScale();
        TickCellWork();

        if (_currentKind == CharacterActionKind.None)
        {
            TryDequeue();
            return;
        }

        if (IsSourceBusy(_currentKind))
            return;

        _currentKind = CharacterActionKind.None;
        Changed?.Invoke();
        TryDequeue();
    }

    void TickCellWork()
    {
        if (_currentKind != CharacterActionKind.Cell)
            return;

        float dt = TimeScaleService.Delta(
            _motor != null && _motor.IsPossessed
                ? TimeScaleChannel.Player
                : TimeScaleChannel.World);
        dt *= _tickScale;

        if (_farm != null && _farm.IsBusy)
            _farm.Tick(dt);
        if (_fish != null && _fish.IsBusy)
            _fish.Tick(dt);
        if (_construction != null && _construction.IsBusy)
            _construction.Tick(dt);
    }

    public void BindCellWorkAnim(Animator animator, int workLayerIndex)
    {
        EnsureCellPipelines();
        _farm?.BindWorkAnim(animator, workLayerIndex);
        _fish?.BindWorkAnim(animator, workLayerIndex);
        _construction?.BindWorkAnim(animator, workLayerIndex);
    }

    public void SetFishWorkClips(FishWorkClipCatalog clips)
    {
        _fishWorkClips = clips;
        EnsureCellPipelines();
        _fish?.SetClipCatalog(clips);
    }

    public void SetFarmWorkClips(FarmWorkClipCatalog clips)
    {
        _farmWorkClips = clips;
        EnsureCellPipelines();
        _farm?.SetClipCatalog(clips);
    }

    public bool TryRunFarm(
        FarmCellActionKind kind,
        Vector3Int cell,
        ItemStack stack,
        InventoryContainer container)
    {
        EnsureCellPipelines();
        return _farm != null && _farm.TryRun(kind, cell, stack, container);
    }

    public bool TryRunFish(
        FishCellActionKind kind,
        Vector3Int cell,
        ItemStack stack,
        InventoryContainer container)
    {
        EnsureCellPipelines();
        return _fish != null && _fish.TryRun(kind, cell, stack, container);
    }

    public bool TryRunConstruction(ConstructionData data, Vector3Int cell, int facingQuarters)
    {
        EnsureCellPipelines();
        return _construction != null && _construction.TryRun(data, cell, facingQuarters);
    }

    /// <summary>기본: 큐 flush + current CancelSoft(Combat 제외) 후 즉시 시작.</summary>
    public bool TryRunImmediate(CharacterActionKind kind, Func<bool> start)
    {
        if (start == null || kind == CharacterActionKind.None)
            return false;

        if (_dispatching)
            return start();

        if (_currentKind == CharacterActionKind.Combat)
            return false;

        _queue.Clear();

        if (_currentKind != CharacterActionKind.None)
        {
            GetSource(_currentKind)?.CancelSoft();
            _currentKind = CharacterActionKind.None;
            Changed?.Invoke();
        }

        return BeginNow(kind, start);
    }

    /// <summary>예약: 현재 작업 유지, busy면 큐 append/replace.</summary>
    public bool TryEnqueue(CharacterActionKind kind, Func<bool> start)
    {
        if (start == null || kind == CharacterActionKind.None)
            return false;

        if (_dispatching)
            return start();

        if (_currentKind != CharacterActionKind.None)
        {
            EnqueueOrReplace(kind, start);
            return true;
        }

        return BeginNow(kind, start);
    }

    /// <summary>호환 alias — 기본은 Immediate. 예약 경로는 TryEnqueue를 명시 호출.</summary>
    public bool TryRunOrEnqueue(CharacterActionKind kind, Func<bool> start) =>
        TryRunImmediate(kind, start);

    public void CancelAll() => InterruptAll(CharacterInterruptReason.UserCancel);

    public void InterruptAll(CharacterInterruptReason reason)
    {
        _queue.Clear();
        _dig?.Clear();
        _chop?.Clear();

        if (reason == CharacterInterruptReason.Knockdown)
        {
            InterruptAllSourcesHard(reason);
            _currentKind = CharacterActionKind.None;
            Changed?.Invoke();
            return;
        }

        if (_currentKind == CharacterActionKind.Combat)
        {
            Changed?.Invoke();
            return;
        }

        GetSource(_currentKind)?.CancelSoft();
        _currentKind = CharacterActionKind.None;
        Changed?.Invoke();
    }

    void RefreshTickScale()
    {
        ICharacterBody body = _bodyHost != null ? _bodyHost.Body : null;
        _tickScale = CharacterActionDelay.TickScale(body, _effectScratch);
    }

    bool BeginNow(CharacterActionKind kind, Func<bool> start)
    {
        _currentKind = kind;
        _dispatching = true;
        bool ok;
        try
        {
            ok = start();
        }
        finally
        {
            _dispatching = false;
        }

        if (!ok)
        {
            _currentKind = CharacterActionKind.None;
            return false;
        }

        if (!IsSourceBusy(kind))
        {
            _currentKind = CharacterActionKind.None;
            Changed?.Invoke();
            TryDequeue();
            return true;
        }

        Changed?.Invoke();
        return true;
    }

    void TryDequeue()
    {
        while (_currentKind == CharacterActionKind.None && _queue.Count > 0)
        {
            Job job = _queue[0];
            _queue.RemoveAt(0);
            if (BeginNow(job.Kind, job.Start))
                return;
        }
    }

    /// <summary>
    /// Gear/Inv/Craft는 FIFO append. Combat은 큐에 최대 1개 — 이미 있으면 Start만 교체.
    /// </summary>
    void EnqueueOrReplace(CharacterActionKind kind, Func<bool> start)
    {
        if (kind == CharacterActionKind.Combat)
        {
            for (int i = 0; i < _queue.Count; i++)
            {
                if (_queue[i].Kind != CharacterActionKind.Combat)
                    continue;
                _queue[i] = new Job { Kind = kind, Start = start };
                Changed?.Invoke();
                return;
            }
        }

        _queue.Add(new Job { Kind = kind, Start = start });
        Changed?.Invoke();
    }

    bool IsSourceBusy(CharacterActionKind kind) =>
        GetSource(kind)?.IsBusy ?? false;

    void CancelCurrentWork() => GetSource(_currentKind)?.CancelSoft();

    ICharacterActionSource GetSource(CharacterActionKind kind)
    {
        if (kind == CharacterActionKind.None)
            return null;

        int index = (int)kind;
        return index >= 0 && index < _sources.Length ? _sources[index] : null;
    }

    void InterruptAllSourcesHard(CharacterInterruptReason reason)
    {
        for (int i = 1; i < _sources.Length; i++)
            _sources[i]?.InterruptHard(reason);
    }

    void SubscribeKnockdownSignals()
    {
        UnsubscribeKnockdownSignals();

        if (_painHost != null)
            _painHost.Changed += OnKnockdownLatchSignalsChanged;

        if (_skillsHost != null)
        {
            _subscribedDefeat = _skillsHost.Defeat;
            if (_subscribedDefeat != null)
                _subscribedDefeat.Changed += OnDefeatKnockdownChanged;
        }
    }

    void UnsubscribeKnockdownSignals()
    {
        if (_painHost != null)
            _painHost.Changed -= OnKnockdownLatchSignalsChanged;

        if (_subscribedDefeat != null)
            _subscribedDefeat.Changed -= OnDefeatKnockdownChanged;
        _subscribedDefeat = null;
    }

    void OnKnockdownLatchSignalsChanged() =>
        SyncKnockdownLatchState(IsKnockdownDown());

    void OnDefeatKnockdownChanged()
    {
        bool down = IsKnockdownDown();
        if (down && !_knockdownDown)
            InterruptAll(CharacterInterruptReason.Knockdown);
        SyncKnockdownLatchState(down);
    }

    void SyncKnockdownLatchState(bool down)
    {
        if (!down && _knockdownDown)
            ResetKnockdownLatches();
        _knockdownDown = down;
    }

    bool IsKnockdownDown()
    {
        if (_painHost != null && _painHost.IsPainShocked)
            return true;

        ICharacterDefeat defeat = _skillsHost != null ? _skillsHost.Defeat : null;
        return defeat != null && defeat.IsDefeated;
    }

    void ResetKnockdownLatches()
    {
        for (int i = 0; i < _sources.Length; i++)
            _sources[i]?.ResetKnockdownLatch();
    }
}
