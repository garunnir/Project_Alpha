// ============================================================
// CharacterWorkLayerAnim — Work Layer 이름·재생·계약 검증 SSOT
// ============================================================

using UnityEngine;

/// <summary>
/// 농사·낚시·vault 공용 Work Layer. 컨트롤러 레이어·상태(클립 이름)는
/// <c>ArmOverlayAnimatorBuilder</c>가 카탈로그와 동기화한다.
/// </summary>
public static class CharacterWorkLayerAnim
{
    public const string LayerName = "Work Layer";

    public const string DefaultControllerPath =
        "Assets/Dist/Visual/Anim/CharacterAnimator/CharacterAnimController.controller";

    public static int ResolveLayerIndex(Animator animator)
    {
        if (animator == null)
            return -1;

        return animator.GetLayerIndex(LayerName);
    }

    public static bool HasLayer(Animator animator) => ResolveLayerIndex(animator) >= 0;

    public static bool TryPlay(Animator animator, ref int layerIndex, AnimationClip clip)
    {
        if (animator == null || clip == null)
            return false;

        if (layerIndex < 0)
            layerIndex = ResolveLayerIndex(animator);

        if (layerIndex < 0)
        {
            LogMissingLayer(animator);
            return false;
        }

        animator.SetLayerWeight(layerIndex, 1f);
        animator.Play(clip.name, layerIndex, 0f);
        return true;
    }

    public static void Play(Animator animator, int layerIndex, AnimationClip clip)
    {
        int index = layerIndex;
        TryPlay(animator, ref index, clip);
    }

    public static void Stop(Animator animator, int layerIndex)
    {
        if (animator == null || layerIndex < 0)
            return;

        animator.SetLayerWeight(layerIndex, 0f);
    }

    /// <summary>맵 바인드·LocomotionAnim 바인드 후 — 레이어 없으면 LogError.</summary>
    public static bool ValidateOrLog(Animator animator, Object context = null)
    {
        if (animator == null)
            return false;

        if (HasLayer(animator))
            return true;

        LogMissingLayer(animator, context);
        return false;
    }

    static void LogMissingLayer(Animator animator, Object context = null)
    {
        Object ctx = context != null ? context : animator;
        Debug.LogError(
            $"[CharacterWorkLayerAnim] Animator controller is missing layer '{LayerName}'. " +
            "Vault/Farm/Fish work clips will not play. " +
            "Fix: Unity menu Dist/MCP/Rebuild Arm Overlay Animator " +
            "(or Dist/MCP/Ensure Work Layer). " +
            $"Controller SSOT: {DefaultControllerPath}",
            ctx);
    }
}
