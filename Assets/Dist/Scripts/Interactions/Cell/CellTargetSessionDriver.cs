// ============================================================
// CellTargetSessionDriver — Farm/Fish/Construction 타겟 세션 단일 Update
// ============================================================

using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-40)]
public sealed class CellTargetSessionDriver : MonoBehaviour
{
    static CellTargetSessionDriver _instance;
    ICellTargetSessionTickable _active;
    IUiCancelConsumer _cancelRegistration;

    public static CellTargetSessionDriver Ensure()
    {
        if (_instance != null)
            return _instance;

        CellTargetSessionDriver existing = FindFirstObjectByType<CellTargetSessionDriver>(
            FindObjectsInactive.Include);
        if (existing != null)
        {
            _instance = existing;
            return _instance;
        }

        var go = new GameObject(nameof(CellTargetSessionDriver));
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<CellTargetSessionDriver>();
        return _instance;
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    void OnDestroy()
    {
        if (ReferenceEquals(_instance, this))
            _instance = null;
    }

    void Update() => _active?.Tick();

    public static bool TrySetActive(ICellTargetSessionTickable session)
    {
        CellTargetSessionDriver driver = Ensure();
        if (driver._active != null && !ReferenceEquals(driver._active, session))
            return false;

        driver._active = session;
        if (session != null)
        {
            driver._cancelRegistration = session;
            UiCancelRouter.Register(session);
        }

        return true;
    }

    public static void ClearActive(ICellTargetSessionTickable session)
    {
        if (_instance == null)
            return;

        if (!ReferenceEquals(_instance._active, session))
            return;

        if (_instance._cancelRegistration != null)
            UiCancelRouter.Unregister(_instance._cancelRegistration);

        _instance._cancelRegistration = null;
        _instance._active = null;
    }

    public static ICellTargetSessionTickable Active =>
        _instance != null ? _instance._active : null;
}
