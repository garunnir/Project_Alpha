// ============================================================
// GearReservedWorkSource — GearTimedAction → ReservedWorkHub
// ============================================================

using System;
using UnityEngine;

public sealed class GearReservedWorkSource : MonoBehaviour, IReservedWorkSource
{
    PlayerGearHost _gearHost;

    bool _lastBusy;

    public bool HasActiveWork =>
        _gearHost != null && _gearHost.Service != null && _gearHost.Service.IsBusy;

    public event Action Changed;

    void Awake()
    {
        _gearHost = this.GetBodyModule<PlayerGearHost>();
        if (_gearHost == null)
            _gearHost = PlayerGearHost.Active;
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
