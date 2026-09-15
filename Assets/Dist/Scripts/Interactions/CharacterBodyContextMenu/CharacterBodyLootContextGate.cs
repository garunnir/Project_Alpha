// ============================================================
// CharacterBodyLootContextGate — 쓰러진 NPC 몸 RMB 루팅/벗기 게이트
// ============================================================

using UnityEngine;

public static class CharacterBodyLootContextGate
{
    public static bool Passes(PlayerInventoryHost victim)
    {
        if (victim == null || victim.Container == null)
            return false;

        if (!PlayerInventoryHost.IsNpcBodyInstanceId(victim.ContainerId))
            return false;

        GameObject interactor = ResolveInteractor();
        if (interactor == null)
            return false;

        if (!victim.IsAvailableToPlayer(interactor))
            return false;

        if (CharacterBodyResolve.IsSameBodyRoot(interactor.transform, victim.BodyRefs))
            return false;

        return true;
    }

    static GameObject ResolveInteractor()
    {
        PlayerInventoryHost host = PlayerInventoryRuntime.Active?.Host;
        return host != null && host.BodyRefs != null ? host.BodyRefs.gameObject : null;
    }
}
