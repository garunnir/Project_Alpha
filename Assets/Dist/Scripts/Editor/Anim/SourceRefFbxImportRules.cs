// ============================================================
// SourceRefFbxImportRules — SourceRef Mixamo FBX → .anim 추출·FBX 삭제
// ============================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SourceRef 하위 FBX를 X Bot 휴머노이드 아바타로 임포트한 뒤 .anim으로 추출하고 FBX를 삭제한다.
/// Root Transform Rotation/Position(Y/XZ)은 모두 Original(keepOriginal*)로 고정한다.
/// </summary>
class SourceRefFbxImportRules : AssetPostprocessor
{
    const string SourceRefRoot = "Assets/Dist/Visual/Anim/SourceRef/";
    const string ReferenceAvatarFbx = SourceRefRoot + "X Bot Referance T-poseAvatar.asset";
    const string LocomotionSegment = "/Locomotion/";

    static readonly HashSet<string> PendingDelete = new();
    static readonly HashSet<string> SkipExtraction = new();

    void OnPreprocessModel()
    {
        if (!ShouldProcess(assetPath))
            return;

        var importer = (ModelImporter)assetImporter;
        Avatar sourceAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(ReferenceAvatarFbx);
        if (sourceAvatar == null)
        {
            Debug.LogError(
                "[SourceRefFbxImportRules] Reference avatar missing at '" + ReferenceAvatarFbx +
                "'. Skipping '" + assetPath + "'.");
            SkipExtraction.Add(assetPath);
            return;
        }

        SkipExtraction.Remove(assetPath);

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        importer.sourceAvatar = sourceAvatar;
        importer.importAnimation = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importBlendShapes = false;
        importer.importVisibility = false;
        importer.importCameras = false;
        importer.importLights = false;

        ConfigureClipAnimations(importer, IsLocomotionPath(assetPath));
    }

    static void ConfigureClipAnimations(ModelImporter importer, bool loop)
    {
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
            return;

        string stem = Path.GetFileNameWithoutExtension(importer.assetPath);
        for (int i = 0; i < clips.Length; i++)
        {
            clips[i].name = i == 0 ? stem : stem + " (" + i + ")";
            clips[i].keepOriginalOrientation = true;
            clips[i].keepOriginalPositionY = true;
            clips[i].keepOriginalPositionXZ = true; 
            clips[i].loopTime = loop;
        }

        importer.clipAnimations = clips;
    }

    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        for (int i = 0; i < importedAssets.Length; i++)
        {
            string path = importedAssets[i];
            if (!ShouldProcess(path))
                continue;
            if (SkipExtraction.Contains(path))
                continue;
            if (PendingDelete.Contains(path))
                continue;

            TryExtractAndScheduleDelete(path);
        }
    }

    static void TryExtractAndScheduleDelete(string fbxPath)
    {
        bool loop = IsLocomotionPath(fbxPath);
        string directory = Path.GetDirectoryName(fbxPath)?.Replace('\\', '/');
        string stem = Path.GetFileNameWithoutExtension(fbxPath);
        stem = stem.Replace("X Bot@","");

        if (string.IsNullOrEmpty(directory))
            return;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        int clipIndex = 0;
        int extracted = 0;

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is not AnimationClip sourceClip)
                continue;
            if (sourceClip.name.StartsWith("__"))
                continue;

            string clipStem = clipIndex == 0 ? stem : stem + " (" + clipIndex + ")";
            string destPath = ResolveAnimPath(directory, clipStem);
            if (destPath == null)
            {
                Debug.LogError(
                    "[SourceRefFbxImportRules] Could not resolve output path for '" + fbxPath + "'.");
                return;
            }

            var clip = Object.Instantiate(sourceClip);
            clip.name = Path.GetFileNameWithoutExtension(destPath);
            //ApplyClipSettings(clip, loop);
            AssetDatabase.CreateAsset(clip, destPath);
            extracted++;
            clipIndex++;
        }

        if (extracted == 0)
        {
            Debug.LogError(
                "[SourceRefFbxImportRules] No animation clips in '" + fbxPath + "'. FBX kept.");
            return;
        }

        AssetDatabase.SaveAssets();
        PendingDelete.Add(fbxPath);
        string pathToDelete = fbxPath;
        EditorApplication.delayCall += () => DeleteFbx(pathToDelete);
        Debug.Log(
            "[SourceRefFbxImportRules] Extracted " + extracted + " clip(s) from '" + fbxPath + "'.");
    }

    static void DeleteFbx(string fbxPath)
    {
        PendingDelete.Remove(fbxPath);
        if (AssetDatabase.LoadAssetAtPath<Object>(fbxPath) == null)
            return;

        if (!AssetDatabase.DeleteAsset(fbxPath))
            Debug.LogError("[SourceRefFbxImportRules] Failed to delete '" + fbxPath + "'.");
    }

    static string ResolveAnimPath(string directory, string stem)
    {
        string path = directory + "/" + stem + ".anim";
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) == null)
            return path;

        for (int i = 1; i < 1000; i++)
        {
            path = directory + "/" + stem + " (" + i + ").anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) == null)
                return path;
        }

        return null;
    }

    static void ApplyClipSettings(AnimationClip clip, bool loop)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.keepOriginalOrientation = true;
        settings.keepOriginalPositionY = true;
        settings.keepOriginalPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
    }

    static bool ShouldProcess(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        if (!path.StartsWith(SourceRefRoot))
            return false;
        if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            return false;
        if (path == ReferenceAvatarFbx)
            return false;
        return true;
    }

    static bool IsLocomotionPath(string path) => path.Contains(LocomotionSegment);

    [MenuItem("Dist/Anim/Extract SourceRef FBX Clips")]
    static void ReimportAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { SourceRefRoot.TrimEnd('/') });
        int count = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!ShouldProcess(path))
                continue;

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            count++;
        }

        Debug.Log("[SourceRefFbxImportRules] Reimport requested for " + count + " FBX(s).");
    }
}
#endif
