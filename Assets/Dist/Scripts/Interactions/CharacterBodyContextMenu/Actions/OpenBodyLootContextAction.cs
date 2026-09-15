// ============================================================
// OpenBodyLootContextAction — 쓰러진 NPC 몸 루팅 UI 열기
// ============================================================

using UnityEngine;

public sealed class OpenBodyLootContextAction : IContextMenuAction
{
    readonly InventoryContainer _container;

    public OpenBodyLootContextAction(InventoryContainer container) =>
        _container = container;

    public string GetDisabledReason()
    {
        if (_container == null)
            return CharacterGearLabels.BlockedInvalid;
        return null;
    }

    public void Execute()
    {
        if (_container == null)
            return;

        if (UIOverlayRouter.Instance != null)
        {
            UIOverlayRouter.Instance.OpenLootFromContainer(_container);
            return;
        }

        Debug.LogWarning("[OpenBodyLootContextAction] UIOverlayRouter missing.");
    }
}
