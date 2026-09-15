// ============================================================
// InventoryReservedWorkSource — InventoryTimedMoveHost → ReservedWorkHub
// ============================================================

using System;
using UnityEngine;

public sealed class InventoryReservedWorkSource : MonoBehaviour, IReservedWorkSource
{
    InventoryTimedMoveHost _moveHost;

    bool _lastBusy;

    public bool HasActiveWork => _moveHost != null && _moveHost.IsBusy;

    public event Action Changed;

    void Awake()
    {
        _moveHost = this.GetBodyModule<InventoryTimedMoveHost>();
        if (_moveHost == null)
            _moveHost = PlayerPossessSession.TimedMoveHost;
    }

    void OnEnable() => ReservedWorkHub.Register(this);

    void OnDisable() => ReservedWorkHub.Unregister(this);

    void Update()
    {
        bool busy = HasActiveWork;
        if (busy == _lastBusy)
            return;

        _lastBusy = busy;
        Changed?.Invoke();
    }
}
