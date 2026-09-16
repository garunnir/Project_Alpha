// ============================================================
// LocomotionVisualReview — deterministic rendered movement contact sheet (Editor only)
// ============================================================
#if UNITY_EDITOR
using System;
using System.IO;
using Animancer;
using UnityEditor;
using UnityEngine;

/// <summary>Run in a disposable Play session; writes only Temp/LocomotionReview/turns.png.</summary>
public static class LocomotionVisualReview
{
    public static string Capture()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play mode required.");
        CharacterLocomotionAnim owner = null;
        CharacterMotor motor = null;
        foreach (var candidate in UnityEngine.Object.FindObjectsByType<CharacterLocomotionAnim>(FindObjectsSortMode.None))
        {
            var candidateMotor = CharacterBodyResolve.GetInBody<CharacterMotor>(candidate);
            if (candidateMotor == null || !candidateMotor.IsPossessed) continue;
            owner = candidate;
            motor = candidateMotor;
            break;
        }
        if (owner == null) throw new InvalidOperationException("No possessed character to capture.");
        var host = owner.GetComponentInChildren<AnimancerComponent>();
        var facing = CharacterBodyResolve.GetInBody<CharacterLocomotionFacing>(owner);
        var state = CharacterBodyResolve.GetInBody<CharacterState>(owner);
        var rotator = owner.GetComponentInChildren<CharacterFacingRotator>();
        if (state.IsAiming) throw new InvalidOperationException("Stop aiming before capture.");
        Vector3 originalDirection = motor.Mover.WorldMoveDir;
        Vector3 originalFacing = facing.BodyFacingDir;
        bool originalSprint = motor.IsSprinting;
        var cameraObject = new GameObject("Locomotion review camera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = cameraObject.AddComponent<Camera>();
        if (Camera.main != null) camera.CopyFrom(Camera.main);
        camera.enabled = false;
        camera.orthographic = true;
        camera.aspect = 1f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.16f, 0.19f, 0.24f);
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 30f;
        const int size = 320;
        int[] frames = { 0, 6, 12, 20, 30, 45 };
        float[] angles = { 90f, -90f, 170f, -170f };
        var target = RenderTexture.GetTemporary(size, size, 24);
        var tile = new Texture2D(size, size, TextureFormat.RGB24, false);
        var sheet = new Texture2D(size * frames.Length, size * angles.Length, TextureFormat.RGB24, false);
        RenderTexture oldActive = RenderTexture.active;
        var samples = new System.Text.StringBuilder("angle,frame,motion,state,time,weight,leftFoot,rightFoot\n");
        try
        {
            camera.targetTexture = target;
            for (int row = 0; row < angles.Length; row++)
            {
                motor.Mover.SetSprinting(true);
                motor.SetDesiredWorldDir(Vector3.forward);
                motor.Mover.CalcConstantSpeedMove(6f, 1f / 60f);
                facing.SetAnimationTurn(Vector3.forward);
                facing.EndAnimationTurn();
                for (int i = 0; i < 120; i++) owner.TickAnimation(1f / 60f);
                var direction = Quaternion.AngleAxis(angles[row], Vector3.up) * Vector3.forward;
                int column = 0;
                for (int frame = 0; frame <= frames[frames.Length - 1]; frame++)
                {
                    motor.SetDesiredWorldDir(direction);
                    motor.Mover.CalcConstantSpeedMove(6f, 1f / 60f);
                    owner.TickAnimation(1f / 60f);
                    rotator.ApplyFacing();
                    if (frame != frames[column]) continue;
                    var active = host.Layers[0].CurrentState;
                    var left = owner.Animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                    var right = owner.Animator.GetBoneTransform(HumanBodyBones.RightFoot);
                    samples.AppendLine($"{angles[row]},{frame},{owner.MovementMotion},\"{active}\",{active.Time:F3},{active.Weight:F3},\"{owner.Animator.transform.InverseTransformPoint(left.position):F3}\",\"{owner.Animator.transform.InverseTransformPoint(right.position):F3}\"");
                    Bounds bounds = new Bounds(owner.Animator.transform.position + Vector3.up, Vector3.one);
                    bool hasBounds = false;
                    foreach (var renderer in owner.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        if (!renderer.enabled) continue;
                        if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                        else bounds.Encapsulate(renderer.bounds);
                    }
                    camera.orthographicSize = Mathf.Max(1f, bounds.size.y * 0.65f);
                    camera.transform.position = bounds.center + new Vector3(3f, 2f, -5f);
                    camera.transform.LookAt(bounds.center);
                    camera.Render();
                    RenderTexture.active = target;
                    tile.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                    tile.Apply();
                    sheet.SetPixels(column * size, (angles.Length - row - 1) * size, size, size, tile.GetPixels());
                    column++;
                }
            }
            sheet.Apply();
            string directory = Path.GetFullPath("Temp/LocomotionReview");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "turns.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            File.WriteAllText(Path.Combine(directory, "samples.csv"), samples.ToString());
            return path + "\nRows: +90, -90, +170, -170 degrees. Columns: frames 0,6,12,20,30,45 at 60 Hz.";
        }
        finally
        {
            motor.SetDesiredWorldDir(originalDirection);
            motor.Mover.SetSprinting(originalSprint);
            facing.SetAnimationTurn(originalFacing);
            facing.EndAnimationTurn();
            rotator.ApplyFacing();
            RenderTexture.active = oldActive;
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(sheet);
            UnityEngine.Object.DestroyImmediate(tile);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
}
#endif
