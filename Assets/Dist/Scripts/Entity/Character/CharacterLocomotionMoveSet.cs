// ============================================================
// CharacterLocomotionMoveSet — Idle + Walk/Run directional clips + blend thresholds (Move mixer SSOT)
// ============================================================
using Animancer;
using UnityEngine;

/// <summary>
/// Clip table + ring thresholds for Animancer <see cref="MixerTransition2D"/> Move ownership (S3).
/// Walk ring matches <c>CharacterAnimController</c> Locomotion blend tree positions (0.1);
/// docs speedNorm at walk is often ~0.26 — that is parameter magnitude, not tree child radius.
/// </summary>
[CreateAssetMenu(
    fileName = "CharacterLocomotionMoveSet",
    menuName = "Dist/Locomotion/Move Mixer Set")]
public sealed class CharacterLocomotionMoveSet : ScriptableObject
{
    /// <summary>Walk child radius in MoveX/MoveZ space (controller-matched).</summary>
    public const float DefaultWalkRing = 0.1f;

    /// <summary>Run child radius in MoveX/MoveZ space (controller-matched).</summary>
    public const float DefaultRunRing = 1f;

    [SerializeField] AnimationClip _idle;
    [SerializeField] AnimationClip _walkForward;
    [SerializeField] AnimationClip _walkBack;
    [SerializeField] AnimationClip _walkLeft;
    [SerializeField] AnimationClip _walkRight;
    [SerializeField] AnimationClip _runForward;
    [SerializeField] AnimationClip _runBack;
    [SerializeField] AnimationClip _runLeft;
    [SerializeField] AnimationClip _runRight;
    [SerializeField, Min(0f)] float _walkRing = DefaultWalkRing;
    [SerializeField, Min(0f)] float _runRing = DefaultRunRing;
    [SerializeField, Min(0f)] float _fadeDuration = 0.25f;

    public float WalkRing => _walkRing;
    public float RunRing => _runRing;

    public bool IsConfigured =>
        _idle != null
        && _walkForward != null
        && _walkBack != null
        && _walkLeft != null
        && _walkRight != null
        && _runForward != null
        && _runBack != null
        && _runLeft != null
        && _runRight != null;

    /// <summary>Builds or refreshes a Directional <see cref="MixerTransition2D"/> from this set.</summary>
    public bool TryConfigureMixer(MixerTransition2D mixer)
    {
        if (mixer == null || !IsConfigured)
            return false;

        float walk = _walkRing > 0f ? _walkRing : DefaultWalkRing;
        float run = _runRing > 0f ? _runRing : DefaultRunRing;

        mixer.FadeDuration = _fadeDuration;
        mixer.Type = MixerTransition2D.MixerType.Directional;
        mixer.Animations = new Object[]
        {
            _idle,
            _walkForward,
            _walkBack,
            _walkLeft,
            _walkRight,
            _runForward,
            _runBack,
            _runLeft,
            _runRight,
        };
        mixer.Thresholds = new Vector2[]
        {
            Vector2.zero,
            new Vector2(0f, walk),
            new Vector2(0f, -walk),
            new Vector2(-walk, 0f),
            new Vector2(walk, 0f),
            new Vector2(0f, run),
            new Vector2(0f, -run),
            new Vector2(-run, 0f),
            new Vector2(run, 0f),
        };
        return true;
    }
}
