// ============================================================
// CharacterGearSetupMenu — Dist/MCP Gear 모듈 존재 확인 (에이전트용)
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CharacterGearSetupMenu
{
    [MenuItem(DistMcpMenus.CharacterEnsurePlayerGearComponents)]
    static void EnsurePlayerGearComponents()
    {
        CharacterBodyRefs refs = Object.FindAnyObjectByType<CharacterBodyRefs>();
        if (refs == null)
        {
            Debug.LogError("[CharacterGearSetupMenu] CharacterBodyRefs not found in scene.");
            return;
        }

        refs.ResolveFromHierarchy();
        if (refs.GearHost == null || refs.TimedMoveHost == null)
        {
            Debug.LogError(
                "[CharacterGearSetupMenu] Gear/TimedMove modules missing on CharacterBodyRefs.",
                refs);
            return;
        }

        Debug.Log(
            "[CharacterGearSetupMenu] Gear/Inventory modules live on CharacterBodyRefs (plain).",
            refs);
    }
}
#endif
