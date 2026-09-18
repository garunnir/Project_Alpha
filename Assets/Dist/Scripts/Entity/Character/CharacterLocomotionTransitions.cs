// ============================================================
// CharacterLocomotionTransitions — interruptible start/stop/pivot segments on the Move layer
// ============================================================
using Animancer;
using UnityEngine;

/// <summary>Interruptible motion segments; pivots also constrain voluntary motor travel.</summary>
public sealed class CharacterLocomotionTransitions
{
    public enum Motion { Loop, Start, Stop, Turn, Fall, LandSoft, LandHard, LandRoll }
    public Motion Current { get; private set; }
    public bool IsActive => Current != Motion.Loop;
    const float PivotTravelResume = 0.65f;
    uint _landingSequence;
    bool _airInitialized;
    float _airDelay;
    internal bool IsLanding => Current == Motion.LandSoft || Current == Motion.LandHard || Current == Motion.LandRoll;
    internal Vector3 CommittedLandingDirection => Current == Motion.LandRoll && !_fadingOut ? _segmentDirection : Vector3.zero;

    internal float GetMovementScale(Vector3 input)
    {
        if (IsLanding && _segment != null)
        {
            float elapsed = (_sourceTime - _segment.StartTime) / _segment.Speed;
            if (Current == Motion.LandSoft) return Mathf.SmoothStep(0.4f, 1f, elapsed / 0.25f);
            if (Current == Motion.LandHard) return Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(0.45f, 0.85f, Mathf.InverseLerp(_segment.StartTime, _segment.End, _sourceTime)));
            return 1f;
        }
        if (Current != Motion.Turn || _segment == null || _state == null || !_state.IsValid()
            || input.sqrMagnitude <= 0.0001f || Vector3.Angle(input, _turnTo) > 60f)
            return 1f;
        float progress = Mathf.InverseLerp(_segment.StartTime, _segment.End, _sourceTime);
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PivotTravelResume, 1f, progress));
    }
    AnimancerState _state;
    CharacterLocomotionMoveSet.MotionSegment _segment;
    bool _initialized;
    bool _wasMoving;
    bool _wasRunning;
    Vector3 _lastDirection;
    Vector3 _turnFrom;
    Vector3 _turnTo;
    float _turnAngle;
    float _cooldown;
    float _sourceTime;
    bool _fadingOut;
    bool _mirrored;
    Vector3 _segmentDirection;

    public void Reset(CharacterLocomotionFacing facing)
    {
        Current = Motion.Loop;
        _state = null;
        _segment = null;
        _initialized = false;
        _cooldown = 0f;
        _fadingOut = false;
        _airInitialized = false;
        _airDelay = 0f;
        facing?.EndAnimationTurn();
    }

    public void Tick(AnimancerLayer layer, Vector2MixerState loop,
        CharacterLocomotionMoveSet set, CharacterLocomotionFacing facing,
        Vector3 direction, bool running, bool allowed, float delta,
        Vector2 footOffset = default, bool hasFootPose = false,
        bool airborne = false, uint landingSequence = 0, float landingSpeed = 0f,
        float landingAirTime = 0f, Vector3 velocity = default, bool airAllowed = false)
    {
        if (delta <= 0f) return;
        bool moving = direction.sqrMagnitude > 0.0001f;
        direction = moving ? direction.normalized : Vector3.zero;
        _cooldown = Mathf.Max(0f, _cooldown - delta);

        if (TickAir(layer, loop, set, facing, airborne, landingSequence, landingSpeed,
            landingAirTime, velocity, direction, airAllowed, delta))
        {
            _initialized = true;
            _wasMoving = moving;
            _wasRunning = running;
            _lastDirection = direction;
            return;
        }

        if (!_initialized)
        {
            _initialized = true;
            _wasMoving = moving;
            _wasRunning = running;
            _lastDirection = direction;
        }

        if (!allowed)
        {
            ReturnToLoop(layer, loop, set, facing);
        }
        else
        {
            // Inputs interrupt one-shots immediately; do not wait for an end event.
            if (IsActive && (_state == null || !_state.IsValid()
                || (layer.CurrentState != _state && !(_fadingOut && layer.CurrentState == loop))
                || (Current == Motion.Stop && moving)
                || (Current != Motion.Stop && !moving)
                || (Current == Motion.Start && running != _wasRunning)
                || (Current == Motion.Start && Vector3.Angle(direction, _segmentDirection) > 60f)
                || (Current == Motion.Turn && Vector3.Angle(direction, _turnTo) > 60f)))
                ReturnToLoop(layer, loop, set, facing);

            bool wantTurn = moving && Current != Motion.Turn && _cooldown <= 0f && facing != null
                && Vector3.Angle(facing.BodyFacingDir, direction) >= set.PivotAngle
                && (!_wasMoving || Vector3.Angle(_lastDirection, direction) >= set.PivotAngle);
            if (wantTurn)
            {
                _turnFrom = facing.BodyFacingDir;
                _turnTo = direction;
                _turnAngle = Vector3.SignedAngle(_turnFrom, _turnTo, Vector3.up);
                bool turnRight = _turnAngle >= 0f;
                CharacterLocomotionMoveSet.MotionSegment turn = running
                    ? (turnRight ? set.RunTurnRight : set.RunTurnLeft)
                    : (turnRight ? set.WalkTurnRight : set.WalkTurnLeft);
                ReturnToLoop(layer, loop, set, facing);
                Play(layer, set, turn, Motion.Turn);
            }
            else if (moving && !_wasMoving)
            {
                ReturnToLoop(layer, loop, set, facing);
                Play(layer, set, running ? set.RunStart : set.WalkStart, Motion.Start);
                _segmentDirection = direction;
            }
            else if (!moving && _wasMoving)
            {
                ReturnToLoop(layer, loop, set, facing);
                var stop = _wasRunning ? set.RunStop : set.WalkStop;
                bool mirror = hasFootPose && stop != null && stop.HasPoseCalibration && stop.MirroredClip != null
                    && (footOffset + stop.EntryFootOffset).sqrMagnitude
                        < (footOffset - stop.EntryFootOffset).sqrMagnitude;
                Play(layer, set, stop, Motion.Stop, mirror);
            }

            if (IsActive)
            {
                // Keep turn ownership through the outgoing fade, including its last sample.
                // A fading Animancer state can stop ticking at zero weight; the segment clock cannot.
                _sourceTime = Mathf.Min(_segment.End, _sourceTime + delta * _segment.Speed);
                if (Current == Motion.Turn)
                {
                    float progress = Mathf.InverseLerp(_segment.StartTime, _segment.End, _sourceTime);
                    var curve = _segment.TurnProgress;
                    float start = curve != null ? curve.Evaluate(0f) : 0f;
                    float end = curve != null ? curve.Evaluate(1f) : 1f;
                    float rotationProgress = curve != null && end - start > 0.0001f
                        ? Mathf.Clamp01((curve.Evaluate(progress) - start) / (end - start)) : progress;
                    facing?.SetAnimationTurn(Quaternion.AngleAxis(
                        _turnAngle * rotationProgress, Vector3.up) * _turnFrom);
                }
                if (!_fadingOut && _sourceTime >= _segment.End - set.TransitionFade * _segment.Speed)
                {
                    if (moving && _segment.HasPoseCalibration)
                        loop.NormalizedTime = _mirrored ? _segment.MirroredExitPhase : _segment.ExitPhase;
                    layer.Play(loop, set.TransitionFade);
                    _fadingOut = true;
                }
                if (_sourceTime >= _segment.End)
                {
                    if (Current == Motion.Turn)
                    {
                        facing?.SetAnimationTurn(_turnTo);
                        _cooldown = set.PivotCooldown;
                    }
                    ReturnToLoop(layer, loop, set, facing);
                }
            }
        }

        _wasMoving = moving;
        _wasRunning = moving ? running : _wasRunning;
        if (moving) _lastDirection = direction;
    }

    bool Play(AnimancerLayer layer, CharacterLocomotionMoveSet set,
        CharacterLocomotionMoveSet.MotionSegment segment, Motion motion, bool mirrored = false)
    {
        if (segment == null || !segment.IsValid) return false;
        _state = layer.Play(mirrored ? segment.MirroredClip : segment.Clip, set.TransitionFade, FadeMode.FromStart);
        _state.Time = segment.StartTime;
        _state.Speed = segment.Speed;
        _segment = segment;
        _sourceTime = segment.StartTime;
        _fadingOut = false;
        _mirrored = mirrored;
        Current = motion;
        return true;
    }

    bool TickAir(AnimancerLayer layer, Vector2MixerState loop, CharacterLocomotionMoveSet set,
        CharacterLocomotionFacing facing, bool airborne, uint sequence, float impact,
        float airTime, Vector3 velocity, Vector3 input, bool allowed, float delta)
    {
        bool landed = _airInitialized && sequence != _landingSequence;
        _airInitialized = true;
        _landingSequence = sequence; // Consume even while hurt/swimming/vaulting to avoid stale landings.
        if (!allowed || set.FallLoop == null)
        {
            _airDelay = 0f;
            if (Current == Motion.Fall || IsLanding) ReturnToLoop(layer, loop, set, facing);
            return false;
        }
        if (airborne)
        {
            _airDelay += delta;
            if (Current != Motion.Fall || _state == null || !_state.IsValid() || layer.CurrentState != _state)
            {
                ReturnToLoop(layer, loop, set, facing);
                if (_airDelay < 0.06f) return true;
                _state = layer.Play(set.FallLoop, set.TransitionFade);
                _state.Time = 0f;
                _state.Speed = 1f;
                Current = Motion.Fall;
            }
            return true;
        }
        _airDelay = 0f;
        if (landed && airTime >= set.MinimumLandingAirTime)
        {
            ReturnToLoop(layer, loop, set, facing);
            bool hard = impact >= set.HardLandingSpeed;
            bool roll = hard && velocity.magnitude >= set.RollLandingSpeed
                && input.sqrMagnitude > 0.0001f && Vector3.Angle(input, velocity) < 60f
                && set.RollLanding != null && set.RollLanding.IsValid;
            var segment = roll ? set.RollLanding : hard ? set.HardLanding : set.SoftLanding;
            if (!Play(layer, set, segment, roll ? Motion.LandRoll : hard ? Motion.LandHard : Motion.LandSoft))
                return false;
            _segmentDirection = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : input;
        }
        else if (Current == Motion.Fall) ReturnToLoop(layer, loop, set, facing);
        if (!IsLanding) return false;
        if (_state == null || !_state.IsValid()
            || (layer.CurrentState != _state && !(_fadingOut && layer.CurrentState == loop)))
        { ReturnToLoop(layer, loop, set, facing); return false; }
        _sourceTime = Mathf.Min(_segment.End, _sourceTime + delta * _segment.Speed);
        if (Current == Motion.LandRoll && !_fadingOut) facing?.SetAnimationTurn(_segmentDirection);
        float elapsed = (_sourceTime - _segment.StartTime) / _segment.Speed;
        bool resumeSoft = Current == Motion.LandSoft && elapsed >= 0.25f && input.sqrMagnitude > 0.0001f;
        if (!_fadingOut && (resumeSoft || _sourceTime >= _segment.End - set.TransitionFade * _segment.Speed))
        {
            layer.Play(loop, set.TransitionFade);
            _fadingOut = true;
            if (resumeSoft) _sourceTime = Mathf.Max(_sourceTime, _segment.End - set.TransitionFade * _segment.Speed);
            facing?.EndAnimationTurn();
        }
        if (_sourceTime >= _segment.End) ReturnToLoop(layer, loop, set, facing);
        return true;
    }

    void ReturnToLoop(AnimancerLayer layer, Vector2MixerState loop,
        CharacterLocomotionMoveSet set, CharacterLocomotionFacing facing)
    {
        if (IsActive && !_fadingOut)
            layer.Play(loop, set.TransitionFade);
        Current = Motion.Loop;
        _state = null;
        _segment = null;
        _fadingOut = false;
        facing?.EndAnimationTurn();
    }
}
