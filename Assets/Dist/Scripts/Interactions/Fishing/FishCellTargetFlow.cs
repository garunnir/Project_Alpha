// ============================================================
// FishCellTargetFlow — 컨텍스트 메뉴 Execute → 낚시 타겟팅 세션 시작
// ============================================================

using UnityEngine;

public static class FishCellTargetFlow
{
    public static void BeginCast(ItemStack stack, InventoryContainer container) =>
        FishCellTargetSession.TryBegin(FishCellActionKind.Cast, stack, container);

    public static void BeginDeployTrap(ItemStack stack, InventoryContainer container) =>
        FishCellTargetSession.TryBegin(FishCellActionKind.DeployTrap, stack, container);

    public static void BeginCollectTrap(Vector3Int cell)
    {
        CharacterActionHost host = ResolveActionHost();
        host?.TryRunFish(FishCellActionKind.CollectTrap, cell, null, null);
    }

    static CharacterActionHost ResolveActionHost()
    {
        CharacterActionHost session = CharacterSessionHub.SessionActionHost;
        if (session != null)
        {
            PlayerGearHost gear = PlayerGearHost.Active;
            if (gear != null)
            {
                CharacterBodyRoot bodyRoot = gear.GetComponentInParent<CharacterBodyRoot>();
                if (bodyRoot != null)
                    CharacterWorkAnimBinder.BindBody(bodyRoot.gameObject);
            }

            return session;
        }

        PlayerGearHost activeGear = PlayerGearHost.Active;
        if (activeGear != null)
        {
            CharacterActionHost fromGear = activeGear.GetBodyComponent<CharacterActionHost>();
            CharacterBodyRoot bodyRoot = activeGear.GetComponentInParent<CharacterBodyRoot>();
            if (bodyRoot != null)
                CharacterWorkAnimBinder.BindBody(bodyRoot.gameObject);
            return fromGear;
        }

        return PlayerInventoryRuntime.Active?.Host != null
            ? PlayerInventoryRuntime.Active.Host.GetBodyComponent<CharacterActionHost>()
            : null;
    }
}
