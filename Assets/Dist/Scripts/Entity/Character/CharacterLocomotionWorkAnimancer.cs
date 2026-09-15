// ============================================================
// CharacterLocomotionWorkAnimancer — Animancer Work Layer Play(clip)
// ============================================================
using Animancer;
using UnityEngine;

/// <summary>
/// Owns Animancer Work layer for Vault/Farm/Fish clips.
/// Clip plays directly — controller state name not required.
/// Layer order: after Flinch/Hurt (5–6) and unused spacer (7).
/// </summary>
public sealed class CharacterLocomotionWorkAnimancer
{
    /// <summary>
    /// Dedicated Work Play(clip) slot. Layers[7] unused spacer stays weight 0.
    /// </summary>
    public const int LayerWork = CharacterLocomotionHurtAnimancer.LayerUnused + 1;

    AnimancerState _state;
    AnimationClip _clip;
    bool _ready;

    public bool IsReady => _ready;

    public int WorkLayerIndex => LayerWork;

    public void Ensure(AnimancerComponent animancer)
    {
        _ready = false;
        if (animancer == null)
            return;
        if (!animancer.IsGraphInitialized && animancer.Animator == null)
            return;

        AnimancerLayer layer = ConfigureLayer(animancer);
        if (layer == null)
            return;

        _ready = true;
    }

    public static AnimancerLayer ConfigureLayer(AnimancerComponent animancer)
    {
        if (animancer == null)
            return null;

        AnimancerLayer layer = animancer.Layers[LayerWork];
        layer.SetDebugName(CharacterWorkLayerAnim.LayerName);
        layer.SetLayerWeightOnPlay = false;
        return layer;
    }

    public void Invalidate()
    {
        _ready = false;
        _state = null;
        _clip = null;
    }

    public void Play(AnimancerComponent animancer, AnimationClip clip)
    {
        if (animancer == null || !animancer.IsGraphInitialized || clip == null)
            return;

        if (!_ready)
            Ensure(animancer);
        if (!_ready)
            return;

        AnimancerLayer layer = animancer.Layers[LayerWork];
        AnimancerState state = layer.Play(clip);
        state.Time = 0f;
        state.Speed = 1f;
        _state = state;
        _clip = clip;
        layer.Weight = 1f;
    }

    public void Stop(AnimancerComponent animancer)
    {
        if (animancer != null && animancer.IsGraphInitialized)
            animancer.Layers[LayerWork].Weight = 0f;
        _state = null;
        _clip = null;
    }

    public float GetLayerWeight(AnimancerComponent animancer)
    {
        if (animancer == null || !animancer.IsGraphInitialized)
            return 0f;
        return animancer.Layers[LayerWork].Weight;
    }

    public bool IsWeightActive(AnimancerComponent animancer) =>
        GetLayerWeight(animancer) > 0.01f;
}
