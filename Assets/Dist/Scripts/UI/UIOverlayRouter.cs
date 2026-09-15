// ============================================================
// UIOverlayRouter — 오버레이 UI 라우팅 (PopUpManager 보완)
// ============================================================

using Interactions;
using Sirenix.OdinInspector;
using UnityEngine;

public sealed class UIOverlayRouter : MonoBehaviour
{
    public static UIOverlayRouter Instance { get; private set; }

    [Required, SerializeField] MonoBehaviour _inventoryController;

    IInventoryOverlayController InventoryController => _inventoryController as IInventoryOverlayController;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[UIOverlayRouter] Duplicate instance ignored.", this);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void OnValidate()
    {
        if (!_inventoryController)
        {
            MonoBehaviour[] candidates = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] is IInventoryOverlayController)
                {
                    _inventoryController = candidates[i];
                    break;
                }
            }
        }

        if (_inventoryController != null && _inventoryController is not IInventoryOverlayController)
        {
            Debug.LogWarning(
                $"[UIOverlayRouter] Assigned component does not implement {nameof(IInventoryOverlayController)}.",
                this);
            _inventoryController = null;
        }
    }

    public void OpenInventory() => InventoryController?.OpenInventory();

    public void CloseInventory() => InventoryController?.CloseInventory();

    public void ToggleInventory() => InventoryController?.ToggleInventory();

    public void OpenLootFromInteractable(ContainerInteractable interactable) =>
        OpenLootFromContainer(interactable?.Container);

    public void OpenLootFromContainer(InventoryContainer focusContainer)
    {
        if (focusContainer == null)
            return;

        InventoryController?.OpenLoot(focusContainer);
    }

    public void HandlePopup(UIPopupType type, object data)
    {
        switch (type)
        {
            case UIPopupType.Chest:
                if (data is ContainerInteractable container)
                    OpenLootFromInteractable(container);
                break;
        }
    }
}
