// ============================================================
// CharacterLocomotionHurtAnimancer — Animancer Flinch (Additive) + Hurt Override clips
// ============================================================
using Animancer;
using UnityEngine;

/// <summary>
/// Owns Animancer layers for victim Flinch (Additive HeadTorso) and Hurt
/// (Stagger / PainDown / Dead Override). Layer order: after Impact (4), before unused remnant (7).
/// </summary>
public sealed class CharacterLocomotionHurtAnimancer
{
    public const int LayerFlinch = 5;
    public const int LayerHurt = 6;
    /// <summary>Unused Hybrid remnant (weight 0, non-SSOT). Work uses LayerWork = 8.</summary>
    public const int LayerHybrid = 7;

    enum HurtMode
    {
        None = 0,
        Stagger = 1,
        PainDown = 2,
        Dead = 3,
    }

    AnimancerState _flinchState;
    AnimationClip _flinchClip;
    AnimancerState _hurtState;
    AnimationClip _hurtClip;
    HurtMode _hurtMode;
    bool _ready;

    public bool IsReady => _ready;

    public int HybridLayerIndex => LayerHybrid;

    public void Ensure(HybridAnimancerComponent hybrid, AvatarMask headTorsoMask)
    {
        _ready = false;
        if (hybrid == null || headTorsoMask == null)
            return;
        if (!hybrid.IsGraphInitialized && hybrid.Animator == null)
            return;

        AnimancerLayer flinch = ConfigureLayer(
            hybrid,
            LayerFlinch,
            "Animancer Flinch",
            headTorsoMask,
            additive: true);
        AnimancerLayer hurt = ConfigureLayer(
            hybrid,
            LayerHurt,
            "Animancer Hurt",
            mask: null,
            additive: false);

        if (flinch == null || hurt == null)
            return;

        AnimancerLayer hybridLayer = hybrid.Layers[LayerHybrid];
        ClearForeignController(hybrid, LayerFlinch, hybridLayer);
        ClearForeignController(hybrid, LayerHurt, hybridLayer);

        _ready = true;
    }

    static AnimancerLayer ConfigureLayer(
        HybridAnimancerComponent hybrid,
        int index,
        string debugName,
        AvatarMask mask,
        bool additive)
    {
        AnimancerLayer layer = hybrid.Layers[index];
        layer.SetDebugName(debugName);
        layer.SetLayerWeightOnPlay = false;
        layer.Mask = mask;
        layer.IsAdditive = additive;
        return layer;
    }

    static void ClearForeignController(
        HybridAnimancerComponent hybrid,
        int layerIndex,
        AnimancerLayer hybridLayer)
    {
        if (hybrid == null || !hybrid.Controller.IsValid)
            return;

        AnimancerLayer layer = hybrid.Layers[layerIndex];
        AnimancerState current = layer.CurrentState;
        if (current != null && current == hybrid.Controller.State)
            hybridLayer.Play(hybrid.Controller.State);
    }

    public void Invalidate()
    {
        _ready = false;
        _flinchState = null;
        _flinchClip = null;
        _hurtState = null;
        _hurtClip = null;
        _hurtMode = HurtMode.None;
    }

    public void PlayFlinch(HybridAnimancerComponent hybrid, AnimationClip clip)
    {
        if (!_ready || hybrid == null || !hybrid.IsGraphInitialized || clip == null)
            return;

        AnimancerLayer layer = hybrid.Layers[LayerFlinch];
        AnimancerState state = layer.Play(clip);
        state.Time = 0f;
        state.Speed = 1f;
        _flinchState = state;
        _flinchClip = clip;
        layer.Weight = 1f;
    }

    public void PlayStagger(HybridAnimancerComponent hybrid, AnimationClip clip)
    {
        if (_hurtMode == HurtMode.Dead || _hurtMode == HurtMode.PainDown)
            return;
        PlayHurtClip(hybrid, clip, HurtMode.Stagger, loopHold: false);
    }

    public void PlayPainDown(HybridAnimancerComponent hybrid, AnimationClip clip)
    {
        if (_hurtMode == HurtMode.Dead)
            return;
        PlayHurtClip(hybrid, clip, HurtMode.PainDown, loopHold: true);
    }

    public void PlayDead(HybridAnimancerComponent hybrid, AnimationClip clip)
    {
        PlayHurtClip(hybrid, clip, HurtMode.Dead, loopHold: false);
    }

    void PlayHurtClip(
        HybridAnimancerComponent hybrid,
        AnimationClip clip,
        HurtMode mode,
        bool loopHold)
    {
        if (!_ready || hybrid == null || !hybrid.IsGraphInitialized || clip == null)
            return;

        AnimancerLayer layer = hybrid.Layers[LayerHurt];
        bool same = ReferenceEquals(_hurtClip, clip)
            && _hurtState != null
            && _hurtState.IsValid()
            && _hurtState.Layer == layer
            && _hurtMode == mode;

        if (same && mode == HurtMode.PainDown)
        {
            layer.Weight = 1f;
            return;
        }

        AnimancerState state = layer.Play(clip);
        if (!same || mode == HurtMode.Stagger || mode == HurtMode.Dead)
            state.Time = 0f;
        state.Speed = 1f;
        _hurtState = state;
        _hurtClip = clip;
        _hurtMode = mode;
        layer.Weight = 1f;
        _ = loopHold;
    }

    public void ClearHurtIfMode(HybridAnimancerComponent hybrid, bool clearPain, bool clearDead)
    {
        if (!_ready || hybrid == null || !hybrid.IsGraphInitialized)
            return;

        if (_hurtMode == HurtMode.PainDown && clearPain)
            StopHurt(hybrid);
        else if (_hurtMode == HurtMode.Dead && clearDead)
            StopHurt(hybrid);
    }

    public void StopHurt(HybridAnimancerComponent hybrid)
    {
        if (hybrid != null && hybrid.IsGraphInitialized)
            hybrid.Layers[LayerHurt].Weight = 0f;
        _hurtState = null;
        _hurtClip = null;
        _hurtMode = HurtMode.None;
    }

    public void StopFlinch(HybridAnimancerComponent hybrid)
    {
        if (hybrid != null && hybrid.IsGraphInitialized)
            hybrid.Layers[LayerFlinch].Weight = 0f;
        _flinchState = null;
        _flinchClip = null;
    }

    public bool IsFlinchPlaying()
    {
        if (_flinchState == null || !_flinchState.IsValid() || !_flinchState.IsPlaying)
            return false;
        if (_flinchClip != null && !_flinchClip.isLooping && _flinchState.NormalizedTime >= 1f)
            return false;
        return true;
    }

    public bool IsHurtWeightActive()
    {
        if (_hurtMode == HurtMode.None)
            return false;
        if (_hurtMode == HurtMode.PainDown || _hurtMode == HurtMode.Dead)
            return true;
        // Stagger: active while clip plays
        if (_hurtState == null || !_hurtState.IsValid() || !_hurtState.IsPlaying)
            return false;
        if (_hurtClip != null && !_hurtClip.isLooping && _hurtState.NormalizedTime >= 1f)
            return false;
        return true;
    }

    public void TickEmptyWeights(HybridAnimancerComponent hybrid)
    {
        if (!_ready || hybrid == null || !hybrid.IsGraphInitialized)
            return;

        if (!IsFlinchPlaying() && hybrid.Layers[LayerFlinch].Weight > 0f)
            StopFlinch(hybrid);

        if (_hurtMode == HurtMode.Stagger && !IsHurtWeightActive())
            StopHurt(hybrid);
    }

    public float GetLayerWeight(HybridAnimancerComponent hybrid, int animancerLayerIndex)
    {
        if (hybrid == null || !hybrid.IsGraphInitialized)
            return 0f;
        return hybrid.Layers[animancerLayerIndex].Weight;
    }

    public void SetLayerWeight(HybridAnimancerComponent hybrid, int animancerLayerIndex, float weight)
    {
        if (hybrid == null || !hybrid.IsGraphInitialized)
            return;
        hybrid.Layers[animancerLayerIndex].Weight = weight;
    }
}
