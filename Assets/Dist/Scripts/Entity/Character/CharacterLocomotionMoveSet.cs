// ============================================================
// CharacterLocomotionMoveSet — Idle + Walk/Run directional clips + blend thresholds (Move mixer SSOT)
// ============================================================
using Animancer;
using UnityEngine;

/// <summary>
/// Clip table + ring thresholds for Animancer <see cref="MixerTransition2D"/> Move ownership (S3).
/// Walk ring uses the default walk/run speed ratio (3/12); tune with character movement speeds.
/// </summary>
[CreateAssetMenu(
    fileName = "CharacterLocomotionMoveSet",
    menuName = "Dist/Locomotion/Move Mixer Set")]
public sealed class CharacterLocomotionMoveSet : ScriptableObject
{
    /// <summary>Walk child radius in MoveX/MoveZ space.</summary>
    public const float DefaultWalkRing = 0.25f;

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

    [System.Serializable]
    public sealed class MotionSegment
    {
        public AnimationClip Clip;
        [Min(0f)] public float StartTime;
        [Min(0f)] public float EndTime;
        [Min(0.01f)] public float Speed = 1f;
        public AnimationCurve TurnProgress = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public AnimationClip MirroredClip;
        [HideInInspector] public bool HasPoseCalibration;
        [HideInInspector] public Vector2 EntryFootOffset;
        [HideInInspector] public float ExitPhase;
        [HideInInspector] public float MirroredExitPhase;

        public float End => Clip == null ? 0f : Mathf.Min(EndTime > 0f ? EndTime : Clip.length, Clip.length);
        public bool IsValid => Clip != null && StartTime >= 0f && End > StartTime && Speed > 0f;
    }

    [Header("Interruptible movement transitions (seconds in source clip)")]
    [SerializeField] MotionSegment _walkStart = new MotionSegment();
    [SerializeField] MotionSegment _walkStop = new MotionSegment();
    [SerializeField] MotionSegment _runStart = new MotionSegment();
    [SerializeField] MotionSegment _runStop = new MotionSegment();
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("_walkTurn")]
    MotionSegment _walkTurnRight = new MotionSegment();
    [SerializeField] MotionSegment _walkTurnLeft = new MotionSegment();
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("_runTurn")]
    MotionSegment _runTurnRight = new MotionSegment();
    [SerializeField] MotionSegment _runTurnLeft = new MotionSegment();
    [SerializeField, Min(0.01f)] float _transitionFade = 0.12f;
    [SerializeField, Range(90f, 180f)] float _pivotAngle = 140f;
    [SerializeField, Min(0f)] float _pivotCooldown = 0.4f;
    [SerializeField, Min(0f)] float _parameterSmoothTime = 0.08f;
    [Header("Air and landing")]
    [SerializeField] AnimationClip _fallLoop;
    [SerializeField] MotionSegment _softLanding = new MotionSegment();
    [SerializeField] MotionSegment _hardLanding = new MotionSegment();
    [SerializeField] MotionSegment _rollLanding = new MotionSegment();
    [SerializeField, Min(0f)] float _minimumLandingAirTime = 0.12f;
    [SerializeField, Min(0f)] float _hardLandingSpeed = 7f;
    [SerializeField, Min(0f)] float _rollLandingSpeed = 4.5f;
    internal AnimationClip FallLoop => _fallLoop;
    internal MotionSegment SoftLanding => _softLanding;
    internal MotionSegment HardLanding => _hardLanding;
    internal MotionSegment RollLanding => _rollLanding;
    internal float MinimumLandingAirTime => _minimumLandingAirTime;
    internal float HardLandingSpeed => _hardLandingSpeed;
    internal float RollLandingSpeed => _rollLandingSpeed;

    public MotionSegment WalkStart => _walkStart;
    public MotionSegment WalkStop => _walkStop;
    public MotionSegment RunStart => _runStart;
    public MotionSegment RunStop => _runStop;
    public MotionSegment WalkTurnRight => _walkTurnRight;
    public MotionSegment WalkTurnLeft => _walkTurnLeft;
    public MotionSegment RunTurnRight => _runTurnRight;
    public MotionSegment RunTurnLeft => _runTurnLeft;
    public float TransitionFade => Mathf.Max(0.01f, _transitionFade);
    public float PivotAngle => _pivotAngle;
    public float PivotCooldown => _pivotCooldown;
    public float ParameterSmoothTime => _parameterSmoothTime;

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
        // Idle has no gait phase and must not slow the synchronized walking/running cycle.
        mixer.SynchronizeChildren = new[] { false, true, true, true, true, true, true, true, true };
        return true;
    }
}
