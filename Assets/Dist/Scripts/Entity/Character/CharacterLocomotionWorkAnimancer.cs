// ============================================================
// CharacterLocomotionWorkAnimancer — Animancer Work Layer Play(clip)
// ============================================================
using Animancer;
using UnityEngine;

/// <summary>
/// Owns Animancer Work layer for Vault/Farm/Fish clips.
/// Clip plays directly — controller state name not required.
/// Layer order: after Flinch/Hurt (5–6) and unused Hybrid remnant (7).
/// </summary>
public sealed class CharacterLocomotionWorkAnimancer
{
    /// <summary>
    /// Dedicated Work Play(clip) slot. Layers[7] unused Hybrid remnant stays weight 0 (non-SSOT).
    /// </summary>
    public const int LayerWork = CharacterLocomotionHurtAnimancer.LayerHybrid + 1;

    AnimancerState _state;
    AnimationClip _clip;
    bool _ready;

    public bool IsReady => _ready;

    public int WorkLayerIndex => LayerWork;

    public void Ensure(HybridAnimancerComponent hybrid)
    {
        _ready = false;
        if (hybrid == null)
            return;
        if (!hybrid.IsGraphInitialized && hybrid.Animator == null)
            return;

        AnimancerLayer layer = ConfigureLayer(hybrid);
        if (layer == null)
            return;

        ClearForeignController(hybrid, layer);
        _ready = true;
    }

    public static AnimancerLayer ConfigureLayer(HybridAnimancerComponent hybrid)
    {
        if (hybrid == null)
            return null;

        AnimancerLayer layer = hybrid.Layers[LayerWork];
        layer.SetDebugName(CharacterWorkLayerAnim.LayerName);
        layer.SetLayerWeightOnPlay = false;
        return layer;
    }

    static void ClearForeignController(HybridAnimancerComponent hybrid, AnimancerLayer workLayer)
    {
        if (hybrid == null || workLayer == null || !hybrid.Controller.IsValid)
            return;

        AnimancerState current = workLayer.CurrentState;
        if (current != null && current == hybrid.Controller.State)
        {
            // Leave Controller.State alive for param bridge; Work owns this layer's weight/play.
            workLayer.Weight = 0f;
        }
    }

    public void Invalidate()
    {
        _ready = false;
        _state = null;
        _clip = null;
    }

    public void Play(HybridAnimancerComponent hybrid, AnimationClip clip)
    {
        if (hybrid == null || !hybrid.IsGraphInitialized || clip == null)
            return;

        if (!_ready)
            Ensure(hybrid);
        if (!_ready)
            return;

        AnimancerLayer layer = hybrid.Layers[LayerWork];
        AnimancerState state = layer.Play(clip);
        state.Time = 0f;
        state.Speed = 1f;
        _state = state;
        _clip = clip;
        layer.Weight = 1f;
    }

    public void Stop(HybridAnimancerComponent hybrid)
    {
        if (hybrid != null && hybrid.IsGraphInitialized)
            hybrid.Layers[LayerWork].Weight = 0f;
        _state = null;
        _clip = null;
    }

    public float GetLayerWeight(HybridAnimancerComponent hybrid)
    {
        if (hybrid == null || !hybrid.IsGraphInitialized)
            return 0f;
        return hybrid.Layers[LayerWork].Weight;
    }

    public bool IsWeightActive(HybridAnimancerComponent hybrid) =>
        GetLayerWeight(hybrid) > 0.01f;
}
