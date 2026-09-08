// ============================================================
// WeaponPresentationCatalogEditor — Resolve 미리보기 (Data Definitions)
// ============================================================

#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponPresentationCatalog))]
public sealed class WeaponPresentationCatalogEditor : OdinEditor
{
    string _previewItemId;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Resolve 미리보기", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Item id → 베이스 Presentation + Quality 합산 Leaf. bake 결과 SO 없음.",
            MessageType.None);
        _previewItemId = EditorGUILayout.TextField("Item id", _previewItemId ?? string.Empty);
        EditorGUILayout.TextArea(
            WeaponPresentationResolvePreview.BuildReport(_previewItemId),
            GUILayout.MinHeight(120f));
    }
}
#endif
