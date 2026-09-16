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
            var component = candidate.GetComponentInChildren<AnimancerComponent>();
            if (component == null || !component.IsGraphInitialized) continue;
            if (component.Layers[0].CurrentState is not Vector2MixerState mixer) continue;
            owner = candidate;
            host = component;
            loop = mixer;
            break;
        }
        if (owner == null) throw new InvalidOperationException("No live character in locomotion loop.");
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
                Vector3 stride = Vector3.back * 0.12f;
                if (owner.MovementMotion != CharacterLocomotionTransitions.Motion.Turn
                    || motor.ConstrainPivotTravel(stride).sqrMagnitude > 0.000001f)
                    throw new InvalidOperationException("Live motor slides during pivot plant.");
                bool sawPartialTravel = false;
                float previousScale = 0f;
                for (int i = 0; i < 70; i++)
                {
                    owner.TickAnimation(0.016f);
                    float scale = motor.ConstrainPivotTravel(stride).magnitude / stride.magnitude;
                    if (scale + 0.0001f < previousScale || scale > 1.0001f)
                        throw new InvalidOperationException("Pivot travel must recover monotonically.");
                    sawPartialTravel |= scale > 0f && scale < 1f;
                    previousScale = scale;
                }
                if (!sawPartialTravel || Mathf.Abs(previousScale - 1f) > 0.0001f)
                    throw new InvalidOperationException("Pivot travel did not recover smoothly.");
                assertions += 2;
                report.AppendLine("PASS live motor pivot holds then smoothly restores travel");
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
