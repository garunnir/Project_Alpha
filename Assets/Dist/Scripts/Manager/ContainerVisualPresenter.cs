// ============================================================
// ContainerVisualPresenter — 사이드바 컨테이너 표시 아이콘·라벨 SSOT
// ============================================================
// 우선순위: 월드 타일 thumbnail → provider SpriteRenderer → 중첩(아이템 아이콘) → null

using IsoTilemap;
using UnityEngine;

public static class ContainerVisualPresenter
{
    /// <summary>
    /// 컨테이너의 표시 스프라이트. 가상 바닥·합산은 <see cref="ContainerIconCatalog"/> SSOT.
    /// </summary>
    public static Sprite GetDisplayIcon(InventoryContainer container, InventorySession session = null)
    {
        if (container == null)
            return null;

        if (container.InstanceId == FloorLootHost.DefaultInstanceId)
            return ResolveCatalogIcon(FloorLootHost.DefaultContainerDefId);

        if (LootAggregateHost.IsAggregateContainer(container))
            return ResolveCatalogIcon(LootAggregateHost.DefaultContainerDefId);

        if (TryResolveBodyLootIcon(container, out Sprite bodyLootIcon))
            return bodyLootIcon;

        if (ContainerTileViewRegistry.Instance.TryGetViewByContainerInstanceId(
                container.InstanceId,
                out TileView tileView))
        {
            Sprite tileSprite = ResolveTileViewSprite(tileView);
            if (tileSprite != null)
                return tileSprite;
        }

        if (InventoryContainerRegistry.TryGetProviderByInstanceId(
                container.InstanceId,
                out IInventoryContainerProvider provider))
        {
            Sprite providerSprite = ResolveProviderSprite(provider);
            if (providerSprite != null)
                return providerSprite;
        }

        // 인벤 안 가방(중첩 컨테이너)은 담고 있는 아이템의 아이콘을 사용.
        if (session != null &&
            session.TryGetContainerItemStack(container, out _, out ItemStack stack) &&
            stack?.Item != null)
        {
            return ItemVisualPresenter.GetDisplayIcon(stack.Item.id);
        }

        // 착용 포켓: 세션 바디 스택에 없으므로 Wear에서 소유 스택 조회.
        if (WornPocketRules.TryFindOwnerStack(
                container,
                PlayerGearHost.Active?.Wear,
                out ItemStack wornOwner) &&
            wornOwner?.Item != null)
        {
            return ItemVisualPresenter.GetDisplayIcon(wornOwner.Item.id);
        }

        return null;
    }

    static Sprite ResolveCatalogIcon(string containerDefId)
    {
        ContainerIconCatalog catalog = ContainerIconCatalog.Active;
        if (catalog == null)
            return null;

        return catalog.Resolve(containerDefId);
    }

    /// <summary>
    /// 사이드바 탭 라벨. 가상 컨테이너(floor-loot·loot-aggregate) 포함 Definition/Loc SSOT.
    /// </summary>
    public static string GetDisplayLabel(InventoryContainer container)
    {
        if (container?.Definition == null)
            return string.Empty;

        if (TryResolveBodyLootLabel(container, out string bodyLootLabel))
            return bodyLootLabel;

        return UITextPresenter.GetContainerName(container.Definition);
    }

    static bool TryResolveBodyLootIcon(InventoryContainer container, out Sprite icon)
    {
        icon = null;
        if (!TryGetBodyLootHost(container, out PlayerInventoryHost host))
            return false;

        BodyLootDisplayKind kind = host.GetBodyLootDisplayKind();
        if (kind == BodyLootDisplayKind.None)
            return false;

        icon = ResolveCatalogIcon(host.ResolveBodyLootContainerDefId());
        if (icon != null)
            return true;

        if (CharacterBodyResolve.TryGetInBody(host, out CharacterAppearanceHost appearance) &&
            appearance.PortraitSprite != null)
        {
            icon = appearance.PortraitSprite;
            return true;
        }

        return false;
    }

    static bool TryResolveBodyLootLabel(InventoryContainer container, out string label)
    {
        label = null;
        if (!TryGetBodyLootHost(container, out PlayerInventoryHost host))
            return false;

        if (host.GetBodyLootDisplayKind() == BodyLootDisplayKind.None)
            return false;

        label = host.ResolveBodyLootDisplayName();
        return !string.IsNullOrEmpty(label);
    }

    static bool TryGetBodyLootHost(InventoryContainer container, out PlayerInventoryHost host)
    {
        host = null;
        if (container == null ||
            !PlayerInventoryHost.IsNpcBodyInstanceId(container.InstanceId))
        {
            return false;
        }

        if (!InventoryContainerRegistry.TryGetProviderByInstanceId(
                container.InstanceId,
                out IInventoryContainerProvider provider))
        {
            return false;
        }

        host = provider as PlayerInventoryHost;
        return host != null;
    }

    static Sprite ResolveTileViewSprite(TileView tileView)
    {
        if (tileView == null)
            return null;

        if (!string.IsNullOrEmpty(tileView.prefabId) &&
            TilePrefabDB.TryResolveDefinition(tileView.prefabId, out TileDefinition def) &&
            def != null &&
            def.thumbnail != null)
        {
            return def.thumbnail;
        }

        SpriteRenderer renderer = tileView.GetComponentInChildren<SpriteRenderer>(true);
        return renderer != null ? renderer.sprite : null;
    }

    static Sprite ResolveProviderSprite(IInventoryContainerProvider provider)
    {
        if (provider is ContainerInteractable interactable)
        {
            Sprite tileSprite = ResolveTileViewSprite(interactable.TileView);
            if (tileSprite != null)
                return tileSprite;
        }

        if (provider is Component component)
        {
            SpriteRenderer renderer = component.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer != null && renderer.sprite != null)
                return renderer.sprite;
        }

        return null;
    }
}
