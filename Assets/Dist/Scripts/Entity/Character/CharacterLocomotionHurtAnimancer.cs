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
    /// <summary>Unused layer spacer (weight 0, non-SSOT). Work uses LayerWork = 8.</summary>
    public const int LayerUnused = 7;

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

    public int UnusedLayerIndex => LayerUnused;

    public void Ensure(AnimancerComponent animancer, AvatarMask headTorsoMask)
    {
        _ready = false;
        if (animancer == null || headTorsoMask == null)
            return;
        if (!animancer.IsGraphInitialized && animancer.Animator == null)
            return;

        AnimancerLayer flinch = ConfigureLayer(
            animancer,
            LayerFlinch,
            "Animancer Flinch",
            headTorsoMask,
            additive: true);
        AnimancerLayer hurt = ConfigureLayer(
            animancer,
            LayerHurt,
            "Animancer Hurt",
            mask: null,
            additive: false);

        if (flinch == null || hurt == null)
            return;

        AnimancerLayer unused = animancer.Layers[LayerUnused];
        unused.SetDebugName("Unused layer spacer");
        unused.Weight = 0f;

        _ready = true;
    }

    static AnimancerLayer ConfigureLayer(
        AnimancerComponent animancer,
        int index,
        string debugName,
        AvatarMask mask,
        bool additive)
    {
        AnimancerLayer layer = animancer.Layers[index];
        layer.SetDebugName(debugName);
        layer.SetLayerWeightOnPlay = false;
        layer.Mask = mask;
        layer.IsAdditive = additive;
        return layer;
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

    public void PlayFlinch(AnimancerComponent animancer, AnimationClip clip)
    {
        if (!_ready || animancer == null || !animancer.IsGraphInitialized || clip == null)
            return;

        AnimancerLayer layer = animancer.Layers[LayerFlinch];
        AnimancerState state = layer.Play(clip);
        state.Time = 0f;
        state.Speed = 1f;
        _flinchState = state;
        _flinchClip = clip;
        layer.Weight = 1f;
    }

    public void PlayStagger(AnimancerComponent animancer, AnimationClip clip)
    {
        if (_hurtMode == HurtMode.Dead || _hurtMode == HurtMode.PainDown)
            return;
        PlayHurtClip(animancer, clip, HurtMode.Stagger, loopHold: false);
    }

    public void PlayPainDown(AnimancerComponent animancer, AnimationClip clip)
    {
        if (_hurtMode == HurtMode.Dead)
            return;
        PlayHurtClip(animancer, clip, HurtMode.PainDown, loopHold: true);
    }

    public void PlayDead(AnimancerComponent animancer, AnimationClip clip)
    {
        PlayHurtClip(animancer, clip, HurtMode.Dead, loopHold: false);
    }

    void PlayHurtClip(
        AnimancerComponent animancer,
        AnimationClip clip,
        HurtMode mode,
        bool loopHold)
    {
        if (!_ready || animancer == null || !animancer.IsGraphInitialized || clip == null)
            return;

        AnimancerLayer layer = animancer.Layers[LayerHurt];
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

    public void ClearHurtIfMode(AnimancerComponent animancer, bool clearPain, bool clearDead)
    {
        if (!_ready || animancer == null || !animancer.IsGraphInitialized)
            return;

        if (_hurtMode == HurtMode.PainDown && clearPain)
            StopHurt(animancer);
        else if (_hurtMode == HurtMode.Dead && clearDead)
            StopHurt(animancer);
    }

    public void StopHurt(AnimancerComponent animancer)
    {
        if (animancer != null && animancer.IsGraphInitialized)
            animancer.Layers[LayerHurt].Weight = 0f;
        _hurtState = null;
        _hurtClip = null;
        _hurtMode = HurtMode.None;
    }

    public void StopFlinch(AnimancerComponent animancer)
    {
        if (animancer != null && animancer.IsGraphInitialized)
            animancer.Layers[LayerFlinch].Weight = 0f;
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

    public void TickEmptyWeights(AnimancerComponent animancer)
    {
        if (!_ready || animancer == null || !animancer.IsGraphInitialized)
            return;

        if (!IsFlinchPlaying() && animancer.Layers[LayerFlinch].Weight > 0f)
            StopFlinch(animancer);

        if (_hurtMode == HurtMode.Stagger && !IsHurtWeightActive())
            StopHurt(animancer);
    }

    public float GetLayerWeight(AnimancerComponent animancer, int animancerLayerIndex)
    {
        if (animancer == null || !animancer.IsGraphInitialized)
            return 0f;
        return animancer.Layers[animancerLayerIndex].Weight;
    }

    public void SetLayerWeight(AnimancerComponent animancer, int animancerLayerIndex, float weight)
    {
        if (animancer == null || !animancer.IsGraphInitialized)
            return;
        animancer.Layers[animancerLayerIndex].Weight = weight;
    }
}
