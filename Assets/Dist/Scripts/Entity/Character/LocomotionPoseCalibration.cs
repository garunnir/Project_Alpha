// ============================================================
// LocomotionPoseCalibration — sample retargeted leg poses for locomotion handoffs (Editor only)
// ============================================================
#if UNITY_EDITOR
using System;
using System.Text;
using Animancer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>No scene/menu changes. Samples the actual character avatar in a temporary preview scene.</summary>
public static class LocomotionPoseCalibration
{
    const string SetPath = "Assets/Dist/SOData/Locomotion/CharacterLocomotionMoveSet.asset";
    const string SourceRoot = "Assets/Dist/Visual/Anim/SourceRef/NewLocomotionPack/";
    const string ModelPath = "Assets/Dist/Visual/Models/gameunitsample.fbx";

    struct LegPose
    {
        public Vector3 LeftFoot, RightFoot, LeftKnee, RightKnee;
        public Vector2 Offset => new Vector2(LeftFoot.z - RightFoot.z, LeftFoot.y - RightFoot.y);
        public float Difference(LegPose other) =>
            (LeftFoot - other.LeftFoot).sqrMagnitude + (RightFoot - other.RightFoot).sqrMagnitude
            + 0.5f * ((LeftKnee - other.LeftKnee).sqrMagnitude + (RightKnee - other.RightKnee).sqrMagnitude);
    }

    public static string Bake()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Bake in Edit mode.");
        var set = AssetDatabase.LoadAssetAtPath<CharacterLocomotionMoveSet>(SetPath);
        if (set == null || !set.IsConfigured) throw new InvalidOperationException("Move set missing clips.");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) throw new InvalidOperationException("Character model is missing.");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            var animator = rig.GetComponent<Animator>();
            if (animator == null || !animator.isHuman) throw new InvalidOperationException("Valid humanoid required.");
            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = null;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var host = rig.AddComponent<AnimancerComponent>();
            host.Animator = animator;
            var leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            var leftKnee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            var rightKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            if (leftFoot == null || rightFoot == null || leftKnee == null || rightKnee == null)
                throw new InvalidOperationException("Avatar leg mapping is incomplete.");

            LegPose Sample(AnimationClip clip, float time)
            {
                var state = host.Play(clip);
                host.Graph.PauseGraph();
                state.Time = Mathf.Clamp(time, 0f, clip.length);
                state.Speed = 0f;
                host.Evaluate(0f);
                return new LegPose
                {
                    LeftFoot = animator.transform.InverseTransformPoint(leftFoot.position),
                    RightFoot = animator.transform.InverseTransformPoint(rightFoot.position),
                    LeftKnee = animator.transform.InverseTransformPoint(leftKnee.position),
                    RightKnee = animator.transform.InverseTransformPoint(rightKnee.position),
                };
            }

            float MatchPhase(LegPose source, AnimationClip loop)
            {
                float best = float.PositiveInfinity;
                float phase = 0f;
                for (int i = 0; i < 60; i++)
                {
                    float candidate = i / 60f;
                    float error = source.Difference(Sample(loop, candidate * loop.length));
                    if (error >= best) continue;
                    best = error;
                    phase = candidate;
                }
                return phase;
            }

            var serialized = new SerializedObject(set);
            var walk = serialized.FindProperty("_walkForward").objectReferenceValue as AnimationClip;
            var run = serialized.FindProperty("_runForward").objectReferenceValue as AnimationClip;
            // All sampling is done on this project's avatar, including mirrored variants.
            Undo.RecordObject(set, "Calibrate locomotion handoffs");
            set.WalkStop.MirroredClip = EnsureMirror("Female Stop And Start Walking");
            set.RunStop.MirroredClip = EnsureMirror("Run To Stop");
            var segments = new[] { set.WalkStart, set.WalkStop, set.WalkTurnRight, set.WalkTurnLeft,
                set.RunStart, set.RunStop, set.RunTurnRight, set.RunTurnLeft };
            var report = new StringBuilder();
            for (int i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (!segment.IsValid) throw new InvalidOperationException("Invalid segment " + i);
                var loop = i < 4 ? walk : run;
                segment.EntryFootOffset = Sample(segment.Clip, segment.StartTime).Offset;
                float outgoing = Mathf.Max(segment.StartTime, segment.End - set.TransitionFade * segment.Speed);
                segment.ExitPhase = MatchPhase(Sample(segment.Clip, outgoing), loop);
                if (segment.MirroredClip != null)
                {
                    var mirrored = Sample(segment.MirroredClip, segment.StartTime);
                    if ((mirrored.Offset + segment.EntryFootOffset).magnitude > 0.08f)
                        throw new InvalidOperationException("Mirror pose validation failed: " + segment.Clip.name);
                    segment.MirroredExitPhase = MatchPhase(Sample(segment.MirroredClip, outgoing), loop);
                }
                segment.HasPoseCalibration = true;
                report.AppendLine(segment.Clip.name + " entry feet=" + segment.EntryFootOffset
                    + " exit phase=" + segment.ExitPhase.ToString("F3"));
            }
            // A mirrored clip has the same time profile as the original, not a generic smoothstep.
            set.WalkTurnLeft.TurnProgress = new AnimationCurve(set.WalkTurnRight.TurnProgress.keys);
            set.RunTurnLeft.TurnProgress = new AnimationCurve(set.RunTurnRight.TurnProgress.keys);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            report.AppendLine("Pose calibration saved; original clips preserved.");
            return report.ToString();
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    static AnimationClip EnsureMirror(string stem)
    {
        string destination = SourceRoot + stem + " Mirrored.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(destination);
        if (clip == null)
        {
            if (!AssetDatabase.CopyAsset(SourceRoot + stem + ".anim", destination))
                throw new InvalidOperationException("Could not create " + destination);
            clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(destination);
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.mirror = true;
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }
}
#endif
