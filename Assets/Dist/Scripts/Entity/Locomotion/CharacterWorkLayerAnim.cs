// ============================================================
// CharacterWorkLayerAnim — Work Layer 이름·재생·계약 검증 SSOT
// ============================================================

using Animancer;
using UnityEngine;

/// <summary>
/// 농사·낚시·vault 공용 Work Layer.
/// S6: 재생 ownership = Animancer <c>Layers[Work]</c> Play(clip).
/// 컨트롤러 상태 이름 = clip.name 계약은 재생에 더 이상 필요하지 않다.
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

        AnimancerComponent animancer = ResolveAnimancer(animator);
        if (animancer != null)
        {
            EnsureAnimancerWorkReady(animancer, animator);
            return CharacterLocomotionWorkAnimancer.LayerWork;
        }

        return animator.GetLayerIndex(LayerName);
    }

    public static bool HasLayer(Animator animator) => ResolveLayerIndex(animator) >= 0;

    public static bool TryPlay(Animator animator, ref int layerIndex, AnimationClip clip)
    {
        if (animator == null || clip == null)
            return false;

        AnimancerComponent animancer = ResolveAnimancer(animator);
        if (animancer != null)
        {
            EnsureAnimancerWorkReady(animancer, animator);
            layerIndex = CharacterLocomotionWorkAnimancer.LayerWork;
            ForceMecanimWorkWeightZero(animator);

            AnimancerLayer layer = animancer.Layers[layerIndex];
            AnimancerState state = layer.Play(clip);
            state.Time = 0f;
            state.Speed = 1f;
            layer.Weight = 1f;
            return true;
        }

        if (layerIndex < 0)
            layerIndex = animator.GetLayerIndex(LayerName);

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
        if (animator == null)
            return;

        AnimancerComponent animancer = ResolveAnimancer(animator);
        if (animancer != null)
        {
            EnsureAnimancerWorkReady(animancer, animator);
            int index = layerIndex >= 0 ? layerIndex : CharacterLocomotionWorkAnimancer.LayerWork;
            if (animancer.IsGraphInitialized)
                animancer.Layers[index].Weight = 0f;
            ForceMecanimWorkWeightZero(animator);
            return;
        }

        if (layerIndex < 0)
            return;

        animator.SetLayerWeight(layerIndex, 0f);
    }

    /// <summary>맵 바인드·LocomotionAnim 바인드 후 — Animancer Work(또는 Mecanim 폴백) 불가 시 LogError.</summary>
    public static bool ValidateOrLog(Animator animator, Object context = null)
    {
        if (animator == null)
            return false;

        AnimancerComponent animancer = ResolveAnimancer(animator);
        if (animancer != null)
        {
            EnsureAnimancerWorkReady(animancer, animator);
            if (animancer.IsGraphInitialized || animancer.Animator != null)
                return true;

            Object ctx = context != null ? context : animator;
            Debug.LogError(
                $"[CharacterWorkLayerAnim] Animancer Work layer '{LayerName}' not ready. " +
                "Vault/Farm/Fish work clips will not play. Prefab-wire AnimancerComponent.",
                ctx);
            return false;
        }

        if (HasLayer(animator))
            return true;

        LogMissingLayer(animator, context);
        return false;
    }

    static AnimancerComponent ResolveAnimancer(Animator animator)
    {
        if (animator == null)
            return null;

        if (animator.TryGetComponent(out AnimancerComponent onSelf))
            return onSelf;

        AnimancerComponent inChildren =
            animator.GetComponentInChildren<AnimancerComponent>(true);
        if (inChildren != null)
            return inChildren;

        return animator.GetComponentInParent<AnimancerComponent>();
    }

    static void EnsureAnimancerWorkReady(AnimancerComponent animancer, Animator animator)
    {
        if (animancer == null)
            return;

        if (animancer.Animator == null && animator != null)
            animancer.Animator = animator;

        // Touch Layers to init graph; no AnimatorController host.
        CharacterLocomotionWorkAnimancer.ConfigureLayer(animancer);

        if (animancer.IsGraphInitialized)
            animancer.Graph.PauseGraph();
    }

    static void ForceMecanimWorkWeightZero(Animator animator)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        int index = animator.GetLayerIndex(LayerName);
        if (index >= 0 && !Mathf.Approximately(animator.GetLayerWeight(index), 0f))
            animator.SetLayerWeight(index, 0f);
    }

    static void LogMissingLayer(Animator animator, Object context = null)
    {
        Object ctx = context != null ? context : animator;
        Debug.LogError(
            $"[CharacterWorkLayerAnim] Animator / Animancer missing Work capability '{LayerName}'. " +
            "Vault/Farm/Fish work clips will not play. " +
            "Wire AnimancerComponent on prefab for Play(clip) Work layer. " +
            $"Legacy controller path remnant: {DefaultControllerPath}",
            ctx);
    }
}
