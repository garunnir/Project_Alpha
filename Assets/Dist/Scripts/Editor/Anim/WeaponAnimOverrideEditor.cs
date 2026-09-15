// ============================================================
// WeaponAnimOverrideEditor — 클립 배속 테이블 (연출 클립은 동작 줄)
// ============================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AnimatorOverrideController))]
public sealed class WeaponAnimOverrideEditor : Editor
{
    const string CharacterControllerGuid = "340c7b66e1595a44db858530dc6283b7";

    public override void OnInspectorGUI()
    {
        var ovr = (AnimatorOverrideController)target;
        if (ovr == null || !IsCharacterArmOverride(ovr))
        {
            DrawDefaultInspector();
            return;
        }

        EditorGUILayout.HelpBox(
            "Animancer 재생 경로: Hold/Aim/Attack/Recoil/Blocked 클립은 무기 동작 줄(또는 Catalog 폴백).\n" +
            "이 Override는 동작 슬롯장이 아니라 Clip Speeds(배속) 호스트입니다. " +
            "연출 클립으로 모든 동작을 덮지 마세요.",
            MessageType.Info);

        WeaponAnimClipSpeeds speeds = FindClipSpeeds(ovr);
        if (speeds != null)
        {
            EditorGUILayout.LabelField("Clip Speeds", EditorStyles.boldLabel);
            var speedsSo = new SerializedObject(speeds);
            speedsSo.Update();
            EditorGUILayout.PropertyField(speedsSo.FindProperty("_entries"), true);
            if (speedsSo.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(speeds);
                WirePresentations(ovr, speeds);
            }
        }

        if (GUILayout.Button("Clear leftover clip mappings"))
        {
            Undo.RecordObject(ovr, "Clear leftover override mappings");
            ovr.ApplyOverrides(new List<KeyValuePair<AnimationClip, AnimationClip>>());
            EditorUtility.SetDirty(ovr);
        }
    }

    static WeaponAnimClipSpeeds FindClipSpeeds(AnimatorOverrideController ovr)
    {
        string path = AssetDatabase.GetAssetPath(ovr);
        if (string.IsNullOrEmpty(path))
            return null;
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is WeaponAnimClipSpeeds found)
                return found;
        }

        return null;
    }

    static void WirePresentations(AnimatorOverrideController ovr, WeaponAnimClipSpeeds speeds)
    {
        if (ovr == null || speeds == null)
            return;

        string[] guids = AssetDatabase.FindAssets("t:WeaponPresentation");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var presentation = AssetDatabase.LoadAssetAtPath<WeaponPresentation>(path);
            if (presentation == null || presentation.AnimatorOverride != ovr)
                continue;
            if (ReferenceEquals(presentation.AnimClipSpeeds, speeds))
                continue;
            Undo.RecordObject(presentation, "Wire anim clip speeds");
            presentation.SetAnimClipSpeeds(speeds);
            EditorUtility.SetDirty(presentation);
        }
    }

    static bool IsCharacterArmOverride(AnimatorOverrideController ovr)
    {
        RuntimeAnimatorController root = ovr.runtimeAnimatorController;
        if (root == null)
            return false;
        string path = AssetDatabase.GetAssetPath(root);
        if (string.IsNullOrEmpty(path))
            return false;
        return AssetDatabase.AssetPathToGUID(path) == CharacterControllerGuid;
    }
}
