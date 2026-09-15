// ============================================================
// CharacterLocomotionArmImpactAnimancer — Animancer Arm (L/R/2H) + Impact clip layers
// ============================================================
using Animancer;
using UnityEngine;

/// <summary>
/// Owns Animancer layers for RightArm / LeftArm / TwoHand poses and Impact Recoil/Blocked.
/// Thin semantic stays Entry→Catalog→clip via <see cref="ArmAnimSlotResolver"/> /
/// <see cref="ArmImpactSlotResolver"/>. Layer order: after Move (0), before Flinch/Hurt (5–6);
/// Layers[7] unused layer spacer (non-SSOT, weight 0).
/// </summary>
public sealed class CharacterLocomotionArmImpactAnimancer
{
    /// <summary>Matches <c>ArmOverlayAnimatorBuilder.AddExitAttack</c> exitTime.</summary>
    public const float AttackExitNormalizedTime = 0.85f;

    /// <summary>Hold↔Aim fade duration (legacy Mecanim transition parity).</summary>
    public const float PoseFadeDuration = 0.05f;

    public const int LayerRightArm = 1;
    public const int LayerLeftArm = 2;
    public const int LayerTwoHand = 3;
    public const int LayerImpact = 4;
    /// <summary>Unused layer spacer slot (weight 0, non-SSOT). Flinch=5 Hurt=6 Work=8.</summary>
    public const int LayerUnused = CharacterLocomotionHurtAnimancer.LayerUnused;

    readonly AnimancerState[] _handStates = new AnimancerState[3];
    readonly AnimationClip[] _handClips = new AnimationClip[3];
    readonly ArmAnimSlotResolver.PoseKind[] _handPoses = new ArmAnimSlotResolver.PoseKind[3];
    readonly bool[] _handAttackActive = new bool[3];

    AnimancerState _impactState;
    AnimationClip _impactClip;
    bool _ready;

    public bool IsReady => _ready;

    public int UnusedLayerIndex => LayerUnused;

    public void Ensure(
        AnimancerComponent animancer,
        AvatarMask rightArmMask,
        AvatarMask leftArmMask,
        AvatarMask twoHandMask)
    {
        _ready = false;
        if (animancer == null || rightArmMask == null || leftArmMask == null || twoHandMask == null)
            return;
        if (!animancer.IsGraphInitialized && animancer.Animator == null)
            return;

        // Touch Layers to ensure capacity; Play later connects states.
        AnimancerLayer right = ConfigureArmLayer(animancer, LayerRightArm, "Animancer RightArm", rightArmMask);
        AnimancerLayer left = ConfigureArmLayer(animancer, LayerLeftArm, "Animancer LeftArm", leftArmMask);
        AnimancerLayer twoHand = ConfigureArmLayer(animancer, LayerTwoHand, "Animancer TwoHand", twoHandMask);
        AnimancerLayer impact = ConfigureArmLayer(animancer, LayerImpact, "Animancer Impact", mask: null);

        if (right == null || left == null || twoHand == null || impact == null)
            return;

        AnimancerLayer unused = animancer.Layers[LayerUnused];
        unused.SetDebugName("Unused layer spacer");
        unused.Weight = 0f;

        _ready = true;
    }

    static AnimancerLayer ConfigureArmLayer(
        AnimancerComponent animancer,
        int index,
        string debugName,
        AvatarMask mask)
    {
        AnimancerLayer layer = animancer.Layers[index];
        layer.SetDebugName(debugName);
        layer.SetLayerWeightOnPlay = false;
        layer.Mask = mask;
        return layer;
    }

    public void Invalidate()
    {
        _ready = false;
        for (int i = 0; i < _handStates.Length; i++)
        {
            _handStates[i] = null;
            _handClips[i] = null;
            _handAttackActive[i] = false;
        }

        _impactState = null;
        _impactClip = null;
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

    public float MaxArmImpactWeight(AnimancerComponent animancer)
    {
        if (animancer == null || !animancer.IsGraphInitialized || !_ready)
            return 0f;
        float max = 0f;
        max = Mathf.Max(max, animancer.Layers[LayerRightArm].Weight);
        max = Mathf.Max(max, animancer.Layers[LayerLeftArm].Weight);
        max = Mathf.Max(max, animancer.Layers[LayerTwoHand].Weight);
        max = Mathf.Max(max, animancer.Layers[LayerImpact].Weight);
        return max;
    }

    public int AnimancerLayerForHand(WieldHand hand)
    {
        if (hand == WieldHand.Left)
            return LayerLeftArm;
        if (hand == WieldHand.TwoHand)
            return LayerTwoHand;
        return LayerRightArm;
    }

    static int HandIndex(WieldHand hand)
    {
        if (hand == WieldHand.Left)
            return 0;
        if (hand == WieldHand.TwoHand)
            return 2;
        return 1;
    }

    public void SyncPose(
        AnimancerComponent animancer,
        WieldHand hand,
        AnimationClip clip,
        float speed,
        ArmAnimSlotResolver.PoseKind pose,
        bool restartAttack)
    {
        if (!_ready || animancer == null || !animancer.IsGraphInitialized || clip == null)
            return;

        int hi = HandIndex(hand);
        int layerIndex = AnimancerLayerForHand(hand);
        AnimancerLayer layer = animancer.Layers[layerIndex];

        bool sameClip = ReferenceEquals(_handClips[hi], clip)
            && _handStates[hi] != null
            && _handStates[hi].IsValid()
            && _handStates[hi].Layer == layer;

        if (restartAttack)
        {
            AnimancerState state = layer.Play(clip);
            state.Time = 0f;
            state.Speed = speed;
            _handStates[hi] = state;
            _handClips[hi] = clip;
            _handPoses[hi] = ArmAnimSlotResolver.PoseKind.Attack;
            _handAttackActive[hi] = true;
            return;
        }

        if (pose == ArmAnimSlotResolver.PoseKind.Attack)
        {
            if (sameClip && _handAttackActive[hi] && _handPoses[hi] == ArmAnimSlotResolver.PoseKind.Attack)
            {
                if (_handStates[hi] != null)
                    _handStates[hi].Speed = speed;
                TickAttackExit(hi);
                return;
            }

            AnimancerState state = layer.Play(clip);
            if (!sameClip || !_handAttackActive[hi])
                state.Time = 0f;
            state.Speed = speed;
            _handStates[hi] = state;
            _handClips[hi] = clip;
            _handPoses[hi] = ArmAnimSlotResolver.PoseKind.Attack;
            _handAttackActive[hi] = true;
            return;
        }

        if (sameClip && _handPoses[hi] == pose)
        {
            if (_handStates[hi] != null)
                _handStates[hi].Speed = speed;
            return;
        }

        float fade = PoseFadeDuration;
        AnimancerState next = fade > 0f
            ? layer.Play(clip, fade)
            : layer.Play(clip);
        next.Speed = speed;
        _handStates[hi] = next;
        _handClips[hi] = clip;
        _handPoses[hi] = pose;
        _handAttackActive[hi] = false;
    }

    void TickAttackExit(int handIndex)
    {
        if (!_handAttackActive[handIndex])
            return;
        AnimancerState state = _handStates[handIndex];
        if (state == null || !state.IsValid())
        {
            _handAttackActive[handIndex] = false;
            return;
        }

        if (state.NormalizedTime >= AttackExitNormalizedTime)
            _handAttackActive[handIndex] = false;
    }

    public void TickAttackExits()
    {
        for (int i = 0; i < _handAttackActive.Length; i++)
            TickAttackExit(i);
    }

    public bool IsInAttack(WieldHand hand)
    {
        int hi = HandIndex(hand);
        if (!_handAttackActive[hi])
            return false;
        AnimancerState state = _handStates[hi];
        if (state == null || !state.IsValid() || !state.IsPlaying)
            return false;
        return state.NormalizedTime < AttackExitNormalizedTime;
    }

    public bool TryGetAttackNormalizedTime(WieldHand hand, out float normalizedTime)
    {
        normalizedTime = 0f;
        if (!IsInAttack(hand))
            return false;
        AnimancerState state = _handStates[HandIndex(hand)];
        if (state == null)
            return false;
        normalizedTime = state.NormalizedTime;
        return true;
    }

    public void PlayImpact(
        AnimancerComponent animancer,
        AnimationClip clip,
        float speed)
    {
        if (!_ready || animancer == null || !animancer.IsGraphInitialized || clip == null)
            return;

        AnimancerLayer layer = animancer.Layers[LayerImpact];
        AnimancerState state = layer.Play(clip);
        state.Time = 0f;
        state.Speed = speed;
        _impactState = state;
        _impactClip = clip;
        layer.Weight = 1f;
    }

    public bool IsImpactPlaying()
    {
        if (_impactState == null || !_impactState.IsValid() || !_impactState.IsPlaying)
            return false;
        if (_impactClip != null && !_impactClip.isLooping && _impactState.NormalizedTime >= 1f)
            return false;
        return true;
    }

    public void StopImpactWeight(AnimancerComponent animancer)
    {
        if (animancer == null || !animancer.IsGraphInitialized)
            return;
        animancer.Layers[LayerImpact].Weight = 0f;
        _impactState = null;
        _impactClip = null;
    }
}
