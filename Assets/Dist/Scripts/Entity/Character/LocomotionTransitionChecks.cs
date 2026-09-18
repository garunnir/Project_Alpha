// ============================================================
// LocomotionTransitionChecks — Play-mode regression checks against the live Animancer graph
// ============================================================
#if UNITY_EDITOR
using System;
using System.Text;
using Animancer;
using UnityEditor;
using UnityEngine;

/// <summary>Invoke Run in Play mode. No menu or scene setup; restores the selected Move layer.</summary>
public static class LocomotionTransitionChecks
{
    public static string CheckAir()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play mode required.");
        var set = AssetDatabase.LoadAssetAtPath<CharacterLocomotionMoveSet>("Assets/Dist/SOData/Locomotion/CharacterLocomotionMoveSet.asset");
        var owner = UnityEngine.Object.FindFirstObjectByType<CharacterLocomotionAnim>();
        if (owner == null) throw new InvalidOperationException("No live character; run in the gameplay scene.");
        var host = owner.GetComponentInChildren<AnimancerComponent>();
        var layer = host.Layers[0];
        var mixer = new MixerTransition2D();
        set.TryConfigureMixer(mixer);
        var loop = (Vector2MixerState)layer.Play(mixer);
        var facing = CharacterBodyResolve.GetInBody<CharacterLocomotionFacing>(owner);
        var driver = new CharacterLocomotionTransitions();
        int count = 0;
        void Assert(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); count++; }
        void Tick(bool air, uint sequence, float impact = 4f, float duration = 0.5f,
            Vector3 velocity = default, Vector3 input = default, bool enabled = true, float delta = 0.02f)
        {
            driver.Tick(layer, loop, set, facing, input, false, !air && enabled, delta,
                airborne: air, landingSequence: sequence, landingSpeed: impact,
                landingAirTime: duration, velocity: velocity, airAllowed: enabled);
            host.Evaluate(delta);
        }
        try
        {
            Tick(false, 0);
            Tick(true, 0); Tick(true, 0); Tick(true, 0); Tick(true, 0);
            Assert(driver.Current == CharacterLocomotionTransitions.Motion.Fall && layer.CurrentState.Clip.isLooping, "Fall must loop");
            Tick(true, 0, input: Vector3.back);
            Assert(driver.Current == CharacterLocomotionTransitions.Motion.Fall, "Air reversal must not pivot");
            Tick(false, 1);
            Assert(driver.Current == CharacterLocomotionTransitions.Motion.LandSoft, "Soft landing selection");
            float pausedTime = layer.CurrentState.Time;
            Tick(false, 1, delta: 0f);
            Assert(Mathf.Abs(layer.CurrentState.Time - pausedTime) < 0.001f, "Pause must hold landing");
            for (int i = 0; i < 45; i++) Tick(false, 1, input: Vector3.forward);
            Assert(driver.Current == CharacterLocomotionTransitions.Motion.Loop, "Moving soft landing must recover");
            Tick(false, 2, impact: 9f);
            Assert(driver.Current == CharacterLocomotionTransitions.Motion.LandHard && driver.GetMovementScale(Vector3.forward) == 0f, "Hard landing plant");
            for (int i = 0; i < 100; i++) Tick(false, 2);
            Assert(driver.Current == CharacterLocomotionTransitions.Motion.Loop, "Hard landing completes");
            Tick(false, 3, impact: 9f, velocity: Vector3.forward * 6f, input: Vector3.forward);
            Assert(driver.Current == CharacterLocomotionTransitions.Motion.LandRoll, "Fast forward landing rolls");
            Tick(false, 3, input: Vector3.back);
            Assert(Vector3.Dot(driver.CommittedLandingDirection, Vector3.forward) > 0.99f, "Roll must keep momentum direction");
            Tick(true, 3);
            Assert(driver.CommittedLandingDirection == Vector3.zero, "Leaving edge releases roll commitment");
            Tick(false, 4, impact: 9f, velocity: Vector3.forward * 6f, input: Vector3.back);
            Assert(driver.Current == CharacterLocomotionTransitions.Motion.LandHard, "Opposed input must brace instead of roll");
            Tick(false, 5, enabled: false);
            Assert(driver.Current == CharacterLocomotionTransitions.Motion.Loop, "Work/hurt/swim cancellation");
            Tick(false, 5);
            Assert(driver.Current == CharacterLocomotionTransitions.Motion.Loop, "Suppressed landing must not replay");
            Tick(false, 6, duration: 0.04f);
            Assert(driver.Current == CharacterLocomotionTransitions.Motion.Loop, "Tiny step must not land");
            var mover = new KinematicMover();
            mover.SetWorldDirection(Vector3.back);
            var move = mover.ApplyLandingRoll(Vector3.forward * 6f, Vector3.forward, 0.02f);
            Assert(move.z > 0f && mover.Velocity.z < 6f, "Roll motor retains and reduces forward momentum");
            return "PASS " + count + " airborne/landing checks";
        }
        finally { driver.Reset(facing); layer.Play(loop, 0.12f); }
    }

    static CharacterMotor _dropMotor;
    static CharacterLocomotionAnim _dropAnimation;
    static Vector3 _dropPosition;
    static uint _dropSequence;
    static double _dropDeadline;
    static bool _sawFall, _sawLanding;
    static CharacterLocomotionTransitions.Motion _observedLanding;
    static string _dropReport;
    public static string BeginDrop(float height)
    {
        if (!Application.isPlaying || _dropMotor != null) throw new InvalidOperationException("Requires idle Play session.");
        foreach (var motor in UnityEngine.Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None))
            if (motor.IsPossessed) { _dropMotor = motor; break; }
        if (_dropMotor == null) throw new InvalidOperationException("No possessed motor.");
        _dropAnimation = CharacterBodyResolve.GetInBody<CharacterLocomotionAnim>(_dropMotor);
        _dropPosition = _dropMotor.GetComponent<Rigidbody>().position;
        _dropSequence = _dropMotor.LandingSequence;
        _sawFall = _sawLanding = false;
        _dropReport = "Running";
        _dropDeadline = EditorApplication.timeSinceStartup + 20;
        _dropMotor.GetComponent<Rigidbody>().position = _dropPosition + Vector3.up * height;
        EditorApplication.update += ObserveDrop;
        return "Drop started at " + height + "m; use DropReport after settling.";
    }
    public static string DropReport() => _dropReport;
    static void ObserveDrop()
    {
        if (!Application.isPlaying || _dropMotor == null || _dropAnimation == null) { EditorApplication.update -= ObserveDrop; _dropMotor = null; return; }
        var motion = _dropAnimation.MovementMotion;
        _sawFall |= motion == CharacterLocomotionTransitions.Motion.Fall;
        _sawLanding |= motion == CharacterLocomotionTransitions.Motion.LandSoft || motion == CharacterLocomotionTransitions.Motion.LandHard;
        if (motion == CharacterLocomotionTransitions.Motion.LandSoft || motion == CharacterLocomotionTransitions.Motion.LandHard) _observedLanding = motion;
        bool complete = _sawLanding && motion == CharacterLocomotionTransitions.Motion.Loop;
        if (!complete && EditorApplication.timeSinceStartup < _dropDeadline) return;
        _dropReport = (complete && _sawFall && _dropMotor.LandingSequence != _dropSequence ? "PASS" : "FAIL")
            + " live drop: fall=" + _sawFall + " landing=" + _sawLanding
            + " motion=" + _observedLanding + " impact=" + _dropMotor.LandingSpeed + " airTime=" + _dropMotor.LandingAirTime;
        _dropMotor.GetComponent<Rigidbody>().position = _dropPosition;
        EditorApplication.update -= ObserveDrop;
        _dropMotor = null;
    }

    public static string CheckMomentum()
    {
        var mover = new KinematicMover();
        mover.SetWorldDirection(Vector3.back);
        Vector3 initial = Vector3.forward * 12f;
        Vector3 Tick(Vector3 previous, bool air, float scale = 1f, float dt = 0.02f)
        {
            mover.CalcConstantSpeedMove(12f, dt);
            mover.ApplyTurnMomentum(previous, dt, air, scale, 3f, 28f, 3f);
            return mover.Velocity;
        }
        var ground = Tick(initial, false, 0f);
        if (ground.z <= 0f || ground.z >= initial.z) throw new InvalidOperationException("Ground reversal lost braking momentum.");
        var air = Tick(initial, true);
        if (air.z <= ground.z || air.z >= initial.z) throw new InvalidOperationException("Air steering is not weaker than ground braking.");
        mover.SetWorldDirection(Vector3.zero);
        if ((Tick(initial, true) - initial).sqrMagnitude > 0.000001f) throw new InvalidOperationException("Air release removed momentum.");
        mover.SetWorldDirection(Vector3.back);
        if ((Tick(initial, true, dt: 0f) - initial).sqrMagnitude > 0.000001f) throw new InvalidOperationException("Pause changed momentum.");
        var landed = Tick(air, false);
        if (landed.z <= 0f || landed.z >= air.z) throw new InvalidOperationException("Landing did not brake residual velocity.");
        var velocity = initial;
        for (int i = 0; i < 40; i++) velocity = Tick(velocity, false, 0f);
        if (velocity.sqrMagnitude > 0.000001f) throw new InvalidOperationException("Pivot braking failed to stop.");
        velocity = Tick(velocity, false);
        if (velocity.z >= 0f) throw new InvalidOperationException("Travel failed to resume after pivot.");
        var fine = initial;
        var coarse = initial;
        for (int i = 0; i < 10; i++) fine = Tick(fine, true, dt: 0.01f);
        for (int i = 0; i < 5; i++) coarse = Tick(coarse, true, dt: 0.02f);
        if ((fine - coarse).magnitude > 0.001f) throw new InvalidOperationException("Air steering depends on tick frequency.");
        return "PASS 8 momentum checks: ground braking, weak air steering, air release, pause, landing, plant stop, resume, timestep consistency.";
    }
    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play mode required.");
        var set = AssetDatabase.LoadAssetAtPath<CharacterLocomotionMoveSet>(
            "Assets/Dist/SOData/Locomotion/CharacterLocomotionMoveSet.asset");
        CharacterLocomotionAnim owner = null;
        AnimancerComponent host = null;
        Vector2MixerState loop = null;
        foreach (var candidate in UnityEngine.Object.FindObjectsByType<CharacterLocomotionAnim>(FindObjectsSortMode.None))
        {
            var candidateMotor = CharacterBodyResolve.GetInBody<CharacterMotor>(candidate);
            if (candidateMotor == null || candidateMotor.IsAirborne) continue;
            var component = candidate.GetComponentInChildren<AnimancerComponent>();
            if (component == null || !component.IsGraphInitialized) continue;
            if (component.Layers[0].CurrentState is not Vector2MixerState mixer) continue;
            owner = candidate;
            host = component;
            loop = mixer;
            break;
        }
        if (owner == null) throw new InvalidOperationException("No grounded character in locomotion loop. Wait for spawn landing before running ground checks.");
        var layer = host.Layers[0];
        var facing = CharacterBodyResolve.GetInBody<CharacterLocomotionFacing>(owner);
        var characterState = CharacterBodyResolve.GetInBody<CharacterState>(owner);
        var renderRotator = owner.GetComponentInChildren<CharacterFacingRotator>();
        if (renderRotator == null || renderRotator.BoundState != characterState)
            throw new InvalidOperationException("Render pivot is not bound to this character's state.");
        Vector3 originalFacing = facing != null ? facing.BodyFacingDir : Vector3.forward;
        Vector3 originalMoveDirection = characterState.MoveDir;
        var driver = new CharacterLocomotionTransitions();
        var report = new StringBuilder();
        int assertions = 0;
        void Step(Vector3 direction, bool run = false, bool allowed = true, float delta = 0.016f)
        {
            if (direction == Vector3.zero) characterState.ClearMoveDir();
            else characterState.SetMoveDir(direction);
            driver.Tick(layer, loop, set, facing, direction, run, allowed, delta);
            facing.TickFacing(delta);
            host.Evaluate(delta);
            renderRotator.ApplyFacing();
        }
        void Check(CharacterLocomotionTransitions.Motion expected, string label)
        {
            if (driver.Current != expected)
                throw new InvalidOperationException(label + ": expected " + expected + ", got " + driver.Current);
            assertions++;
            report.AppendLine("PASS " + label);
        }
        try
        {
            facing.SetAnimationTurn(Vector3.forward);
            facing.EndAnimationTurn();
            Step(Vector3.zero);
            Step(Vector3.forward);
            Check(CharacterLocomotionTransitions.Motion.Start, "walk start");
            for (int i = 0; i < 110; i++) Step(Vector3.forward);
            Check(CharacterLocomotionTransitions.Motion.Loop, "walk start completes");
            Step(Vector3.zero);
            Check(CharacterLocomotionTransitions.Motion.Stop, "walk stop segment");
            Step(Vector3.forward);
            Check(CharacterLocomotionTransitions.Motion.Start, "reinput interrupts stop");
            Step(Vector3.forward, allowed: false);
            Check(CharacterLocomotionTransitions.Motion.Loop, "blocked transitions cancel");
            Step(Vector3.zero);
            Step(Vector3.forward, true);
            Check(CharacterLocomotionTransitions.Motion.Start, "run start");
            Step(Vector3.zero, true);
            Check(CharacterLocomotionTransitions.Motion.Stop, "release interrupts start");
            for (int i = 0; i < 90; i++) Step(Vector3.zero, true);
            Check(CharacterLocomotionTransitions.Motion.Loop, "run stop completes");
            Step(Vector3.forward, true);
            for (int i = 0; i < 80; i++) Step(Vector3.forward, true);
            Step(Vector3.back, true);
            Check(CharacterLocomotionTransitions.Motion.Turn, "180 pivot");
            if (driver.GetMovementScale(Vector3.back) != 0f)
                throw new InvalidOperationException("Pivot plant must hold travel.");
            assertions++;
            report.AppendLine("PASS pivot plant holds travel");
            Step(Vector3.back, true, delta: 0f);
            Check(CharacterLocomotionTransitions.Motion.Turn, "pause holds transition");
            if (driver.GetMovementScale(Vector3.back) != 0f
                || driver.GetMovementScale(Vector3.zero) != 1f
                || driver.GetMovementScale(Vector3.right) != 1f)
                throw new InvalidOperationException("Pause/release/redirect travel gate failed.");
            assertions++;
            report.AppendLine("PASS pause holds gate; release and redirect unlock");
            Step(Vector3.right, true);
            Check(CharacterLocomotionTransitions.Motion.Loop, "redirect cancels pivot");
            driver.Reset(facing);
            facing.SetAnimationTurn(Vector3.forward);
            facing.EndAnimationTurn();
            layer.Play(loop, set.TransitionFade);
            Step(Vector3.forward, true);
            for (int i = 0; i < 80; i++) Step(Vector3.forward, true);
            Vector3 leftBack = Quaternion.AngleAxis(-170f, Vector3.up) * Vector3.forward;
            Step(leftBack, true);
            Check(CharacterLocomotionTransitions.Motion.Turn, "left 170 pivot");
            Vector3 facingBeforeLeftTurn = facing.BodyFacingDir;
            for (int i = 0; i < 15; i++) Step(leftBack, true);
            if (Vector3.SignedAngle(facingBeforeLeftTurn, facing.BodyFacingDir, Vector3.up) >= 0f)
                throw new InvalidOperationException("Left pivot rotated along the right-hand arc.");
            report.AppendLine("PASS left pivot uses shortest signed arc");
            assertions++;
            for (int i = 0; i < 70; i++) Step(leftBack, true);
            Check(CharacterLocomotionTransitions.Motion.Loop, "pivot finishes outgoing fade");
            if (Vector3.Angle(renderRotator.transform.forward, leftBack) > 0.5f)
                throw new InvalidOperationException("Rendered model does not face the final pivot target.");
            assertions++;
            report.AppendLine("PASS rendered pivot ends on target");
            foreach (float angle in new[] { -120f, -90f, -45f, 45f, 90f, 120f })
            {
                driver.Reset(facing);
                facing.SetAnimationTurn(Vector3.forward);
                facing.EndAnimationTurn();
                Vector3 target = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
                for (int i = 0; i < 90; i++) Step(target);
                if (Vector3.Angle(renderRotator.transform.forward, target) > 0.5f)
                    throw new InvalidOperationException("Visible turn failed: " + angle);
                assertions++;
                report.AppendLine("PASS visible " + angle + " degree turn");
            }
            foreach (bool mirrored in new[] { false, true })
            {
                driver.Reset(facing);
                layer.Play(loop, set.TransitionFade);
                driver.Tick(layer, loop, set, facing, Vector3.forward, false, true, 0.016f);
                Vector2 feet = set.WalkStop.EntryFootOffset * (mirrored ? -1f : 1f);
                driver.Tick(layer, loop, set, facing, Vector3.zero, false, true, 0.016f, feet, true);
                AnimationClip expected = mirrored ? set.WalkStop.MirroredClip : set.WalkStop.Clip;
                if (layer.CurrentState.Clip != expected)
                    throw new InvalidOperationException("Stop chose the wrong leading foot.");
                assertions++;
                report.AppendLine("PASS stop leading foot, mirrored=" + mirrored);
            }
            if (owner.Animator.applyRootMotion)
                throw new InvalidOperationException("Root motion must not move the character.");
            report.AppendLine("PASS root motion disabled");
            var motor = CharacterBodyResolve.GetInBody<CharacterMotor>(owner);
            if (motor == null) throw new InvalidOperationException("Missing character motor.");
            driver.Reset(facing);
            facing.SetAnimationTurn(Vector3.forward);
            facing.EndAnimationTurn();
            layer.Play(loop, set.TransitionFade);
            Vector3 originalInput = motor.Mover.WorldMoveDir;
            bool originalSprint = motor.IsSprinting;
            Vector3 originalPosition = owner.Animator.transform.position;
            try
            {
                motor.SetDesiredWorldDir(Vector3.zero);
                motor.Mover.CalcConstantSpeedMove(3f, 0.02f);
                owner.TickAnimation(0.016f);
                motor.SetDesiredWorldDir(Vector3.forward);
                motor.Mover.SetSprinting(false);
                motor.Mover.CalcConstantSpeedMove(3f, 0.02f);
                owner.TickAnimation(0.016f);
                if (owner.MovementMotion != CharacterLocomotionTransitions.Motion.Start)
                    throw new InvalidOperationException("Live host did not start movement transition. delta="
                        + TimeScaleService.Delta(TimeScaleChannel.Player) + " work=" + host.Layers[8].Weight
                        + " hurt=" + host.Layers[6].Weight + " direction=" + motor.Mover.WorldMoveDir
                        + " speed=" + motor.CurrentSpeed + " state=" + owner.MovementMotion);
                owner.TickAnimation(0.016f);
                if (layer.CurrentState == loop)
                    throw new InvalidOperationException("Ensure graph stole the one-shot state.");
                motor.SetDesiredWorldDir(Vector3.right);
                owner.TickAnimation(0.016f);
                if (Mathf.Abs(loop.Parameter.x) > 0.0001f)
                    throw new InvalidOperationException("Free movement still selects a strafe gait.");
                characterState.SetAimDir(Vector3.forward, owner.transform.position + Vector3.forward, 2f);
                owner.TickAnimation(0.016f);
                if (loop.Parameter.x <= 0f)
                    throw new InvalidOperationException("Aim movement lost directional strafe.");
                characterState.ClearAim();
                assertions += 2;
                report.AppendLine("PASS free forward gait and aimed strafe");
                motor.SetDesiredWorldDir(Vector3.zero);
                motor.Mover.CalcConstantSpeedMove(3f, 0.02f);
                owner.TickAnimation(0.016f);
                if (owner.MovementMotion != CharacterLocomotionTransitions.Motion.Stop)
                    throw new InvalidOperationException("Live host did not stop movement.");
                if (Vector3.Distance(originalPosition, owner.Animator.transform.position) > 0.001f)
                    throw new InvalidOperationException("Animation displaced the character root.");
                report.AppendLine("PASS live host start/stop, graph ownership and stationary animation root");
                assertions += 3;
                facing.SetAnimationTurn(Vector3.forward);
                facing.EndAnimationTurn();
                motor.SetDesiredWorldDir(Vector3.forward);
                motor.Mover.SetSprinting(true);
                for (int i = 0; i < 120; i++) owner.TickAnimation(0.016f);
                motor.SetDesiredWorldDir(Vector3.back);
                owner.TickAnimation(0.016f);
                if (owner.MovementMotion != CharacterLocomotionTransitions.Motion.Turn
                    || owner.GetPivotMovementScale(motor.Mover.WorldMoveDir) > 0.000001f)
                    throw new InvalidOperationException("Pivot target travel must be zero during plant.");
                bool sawPartialTravel = false;
                float previousScale = 0f;
                for (int i = 0; i < 70; i++)
                {
                    owner.TickAnimation(0.016f);
                    float scale = owner.GetPivotMovementScale(motor.Mover.WorldMoveDir);
                    if (scale + 0.0001f < previousScale || scale > 1.0001f)
                        throw new InvalidOperationException("Pivot travel must recover monotonically.");
                    sawPartialTravel |= scale > 0f && scale < 1f;
                    previousScale = scale;
                }
                if (!sawPartialTravel || Mathf.Abs(previousScale - 1f) > 0.0001f)
                    throw new InvalidOperationException("Pivot travel did not recover smoothly.");
                assertions += 2;
                report.AppendLine("PASS live animation pivot target holds then smoothly restores travel");
            }
            finally
            {
                motor.SetDesiredWorldDir(originalInput);
                motor.Mover.SetSprinting(originalSprint);
            }
            report.AppendLine("Checks passed: " + (assertions + 1));
            return report.ToString();
        }
        finally
        {
            Step(Vector3.zero, allowed: false);
            driver.Reset(facing);
            facing?.SetAnimationTurn(originalFacing);
            facing?.EndAnimationTurn();
            characterState.ClearMoveDir();
            characterState.SetMoveDir(originalMoveDirection);
            renderRotator.ApplyFacing();
        }
    }
}
#endif
