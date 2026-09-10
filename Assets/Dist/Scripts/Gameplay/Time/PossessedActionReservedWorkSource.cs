// ============================================================
// PossessedActionReservedWorkSource — possessed 행동 큐 → ReservedWorkHub
// ============================================================

using System;
using UnityEngine;

public sealed class PossessedActionReservedWorkSource : MonoBehaviour, IReservedWorkSource
{
    CharacterActionHost _host;
    bool _lastBusy;

    public bool HasActiveWork => _host != null && _host.HasCancellableWork;

    public event Action Changed;

    void OnEnable() => ReservedWorkHub.Register(this);

    void OnDisable()
    {
        BindHost(null);
        ReservedWorkHub.Unregister(this);
    }

    void Update()
    {
        // Rule 6: hub/호스트 포인터 비교 + bool만. 할당 없음.
        PlayerPossessSession session = PlayerPossessSession.Current;
        CharacterActionHost host = session?.ActionHost;
        if (host != _host)
            BindHost(host);

        NotifyIfBusyChanged();
    }

    void BindHost(CharacterActionHost host)
    {
        if (_host != null)
            _host.Changed -= OnHostChanged;

        _host = host;
        if (_host != null)
            _host.Changed += OnHostChanged;
    }

    void OnHostChanged() => NotifyIfBusyChanged();

    void NotifyIfBusyChanged()
    {
        bool busy = HasActiveWork;
        if (busy == _lastBusy)
            return;

        _lastBusy = busy;
        Changed?.Invoke();
    }
}
