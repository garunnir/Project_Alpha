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

    CharacterBodyHost _bodyHost;
    PlayerGearHost _gearHost;
    InventoryTimedMoveHost _moveHost;
    CharacterAttacker _attacker;
    CharacterArriveHost _arriveHost;
    CharacterVaultHost _vaultHost;
    CharacterMotor _motor;
    CharacterCellFarmPipeline _farm;
    CharacterCellFishPipeline _fish;
    CharacterCellConstructionPipeline _construction;
    CharacterActionKind _currentKind;
    bool _dispatching;
    float _tickScale = 1f;

    public int CancelPriority => UiCancelPriority.CharacterAction;

    public CharacterActionKind CurrentKind => _currentKind;
    public int QueueCount => _queue.Count;
    public bool IsDispatching => _dispatching;
    public float ActionTickScale => _tickScale;
    public bool IsBusy => _currentKind != CharacterActionKind.None || _queue.Count > 0;

    public bool HasCancellableWork =>
        _queue.Count > 0 ||
        (_currentKind != CharacterActionKind.None && _currentKind != CharacterActionKind.Combat);

    public event Action Changed;

    /// <summary>Cell Arrive(자동이동) 중 — 게이지는 fill 대신 AutoProgressIcon.</summary>
    public bool IsCellArriving =>
        _currentKind == CharacterActionKind.Cell &&
        _arriveHost != null &&
        _arriveHost.IsBusy;

    public float Progress01
    {
        get
        {
            switch (_currentKind)
            {
                case CharacterActionKind.Gear:
                    return _gearHost != null && _gearHost.Timed != null
                        ? _gearHost.Timed.Progress01
                        : 0f;
                case CharacterActionKind.Inventory:
                    return _moveHost != null ? _moveHost.Progress01 : 0f;
                case CharacterActionKind.Craft:
                    return _crafting != null ? _crafting.CraftProgress01 : 0f;
                case CharacterActionKind.Combat:
                    return _attacker != null ? _attacker.CooldownProgress01 : 0f;
                case CharacterActionKind.Cell:
                    if (_vaultHost != null && _vaultHost.IsBusy)
                        return _vaultHost.Progress01;
                    if (_construction != null && _construction.IsBusy)
                        return _construction.WorkProgress01;
                    if (_fish != null && _fish.IsBusy)
                        return _fish.WorkProgress01;
                    return _farm != null ? _farm.WorkProgress01 : 0f;
                default:
                    return 0f;
            }
        }
    }

    void Awake()
    {
        ResolveBodyRefs();
        EnsureCellPipelines();
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
            _gearHost = refs.GearHost;
            _attacker = refs.Attacker;
            _motor = refs.Motor;
            _vaultHost = refs.VaultHost;
        }
        else
        {
            TryGetComponent(out _bodyHost);
            TryGetComponent(out _gearHost);
            TryGetComponent(out _attacker);
            _motor = CharacterBodyResolve.GetInBody<CharacterMotor>(this);
            _vaultHost = CharacterBodyResolve.GetInBody<CharacterVaultHost>(this);
        }

        if (_moveHost == null)
            TryGetComponent(out _moveHost);
        _arriveHost = CharacterBodyResolve.GetInBody<CharacterArriveHost>(this);
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
    }

    void OnEnable() => UiCancelRouter.Register(this);

    void OnDisable()
    {
        UiCancelRouter.Unregister(this);
        _queue.Clear();
        if (_currentKind != CharacterActionKind.Combat)
            CancelCurrentWork();
        _currentKind = CharacterActionKind.None;
        _farm?.OnOwnerDisabled();
        _fish?.OnOwnerDisabled();
        _construction?.OnOwnerDisabled();
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

    public bool TryRunOrEnqueue(CharacterActionKind kind, Func<bool> start)
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

    public void CancelAll()
    {
        _queue.Clear();
        if (_currentKind == CharacterActionKind.Combat)
        {
            Changed?.Invoke();
            return;
        }

        CancelCurrentWork();
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

    bool IsSourceBusy(CharacterActionKind kind)
    {
        switch (kind)
        {
            case CharacterActionKind.Gear:
                return _gearHost != null && _gearHost.Service != null && _gearHost.Service.IsBusy;
            case CharacterActionKind.Inventory:
                return _moveHost != null && _moveHost.IsBusy;
            case CharacterActionKind.Craft:
                return _crafting != null && _crafting.IsCraftRunning;
            case CharacterActionKind.Combat:
                return _attacker != null && _attacker.IsActionBusy;
            case CharacterActionKind.Cell:
                return (_farm != null && _farm.IsBusy) ||
                       (_fish != null && _fish.IsBusy) ||
                       (_construction != null && _construction.IsBusy) ||
                       (_arriveHost != null && _arriveHost.IsBusy) ||
                       (_vaultHost != null && _vaultHost.IsBusy);
            default:
                return false;
        }
    }

    void CancelCurrentWork()
    {
        switch (_currentKind)
        {
            case CharacterActionKind.Gear:
                _gearHost?.Timed?.Cancel();
                break;
            case CharacterActionKind.Inventory:
                _moveHost?.Cancel();
                break;
            case CharacterActionKind.Craft:
                _crafting?.CancelRunningCraft();
                break;
            case CharacterActionKind.Cell:
                _farm?.Cancel();
                _fish?.Cancel();
                _construction?.Cancel();
                _arriveHost?.Cancel();
                _vaultHost?.Cancel();
                break;
        }
    }
}
