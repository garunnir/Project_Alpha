// ============================================================
// CharacterWorkAnimBinder — Work Layer 소비자 스폰 시 1회 명시 바인딩
// ============================================================

using UnityEngine;

public static class CharacterWorkAnimBinder
{
    public static void BindBody(GameObject root)
    {
        if (root == null)
            return;

        CharacterBodyRefs refs = CharacterBodyRefs.EnsureResolved(root);
        CharacterLocomotionAnim locomotion = refs.LocomotionAnim;
        if (locomotion == null)
        {
            Debug.LogError(
                $"[CharacterWorkAnimBinder] '{root.name}' needs CharacterLocomotionAnim.",
                root);
            return;
        }

        if (!locomotion.TryGetWorkAnim(out Animator animator, out int workLayerIndex))
        {
            Debug.LogError(
                $"[CharacterWorkAnimBinder] '{root.name}' Work Layer not ready on animator.",
                locomotion);
            return;
        }

        CharacterVaultHost vault = refs.VaultHost;
        if (refs.Motor != null && vault == null)
        {
            Debug.LogError(
                $"[CharacterWorkAnimBinder] '{root.name}' with CharacterMotor needs CharacterVaultHost on prefab.",
                root);
        }
        else if (vault != null)
        {
            vault.Bind(locomotion);
        }

        CharacterActionHost action = refs.ActionHost;
        if (action != null)
            action.BindCellWorkAnim(animator, workLayerIndex);
    }
}
