// ============================================================
// PlayerStatusUIBridge — PlayerStatusViewModel 씬 SSOT + GameplayData bind (Possess 시 rebind)
// ============================================================

using UnityEngine;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public sealed class PlayerStatusUIBridge : MonoBehaviour
{
    public static PlayerStatusUIBridge Instance { get; private set; }

    PlayerStatusViewModel _viewModel;

    public PlayerStatusViewModel ViewModel
    {
        get
        {
            EnsureInitialized();
            return _viewModel;
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "[PlayerStatusUIBridge] Duplicate bridge. Keep one under System/PlayerStatus.",
                this);
            enabled = false;
            return;
        }

        Instance = this;
        BindFromGameplayData();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        _viewModel?.Unbind();
        _viewModel = null;
    }

    void EnsureInitialized()
    {
        if (_viewModel != null)
            return;

        BindFromGameplayData();
    }

    void BindFromGameplayData()
    {
        if (_viewModel == null)
            _viewModel = new PlayerStatusViewModel();

        _viewModel.Bind(PlayerPossessSession.SessionBody, GameplayData.Vitals, GameplayData.Stats);
    }

    public static void RebindFromGameplayData()
    {
        if (Instance == null)
        {
            Debug.LogError(
                "[PlayerStatusUIBridge] Rebind skipped — bridge missing. " +
                "Run Dist/MCP/PlayerStatus/Setup Canvas In Open Scene.");
            return;
        }

        Instance.BindFromGameplayData();
    }

    public static bool TryResolve(out PlayerStatusViewModel viewModel)
    {
        viewModel = null;
        if (Instance == null)
            return false;

        viewModel = Instance.ViewModel;
        return viewModel != null;
    }
}
