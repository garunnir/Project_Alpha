using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MultiLayerTilemapToJson))]
public class MultiLayerTilemapToJsonEditor : Editor
{
    public override void OnInspectorGUI()

    {
        DrawDefaultInspector();
        var tilemapScript = (MultiLayerTilemapToJson)target;
        EditorGUILayout.Space();

        if (GUILayout.Button("자식 Tilemap 자동 할당"))
        {
            tilemapScript.AutoAssignTilemapsFromChildren();
            EditorUtility.SetDirty(tilemapScript);
        }

        if (GUILayout.Button("타일맵 내보내기 (JSON 저장)"))
        {
            string json = tilemapScript.AllTilemapsToJson();
            System.IO.File.WriteAllText(tilemapScript.jsonFilePath, json);
            AssetDatabase.Refresh();
            Debug.Log($"타일맵이 {tilemapScript.jsonFilePath}에 저장되었습니다.");
        }

        if (GUILayout.Button("타일맵 불러오기 (JSON → 콘솔 출력)"))
        {
            if (System.IO.File.Exists(tilemapScript.jsonFilePath))
            {
                string json = System.IO.File.ReadAllText(tilemapScript.jsonFilePath);
                Debug.Log($"불러온 JSON:\n{json}");
            }
            else
            {
                Debug.LogWarning("지정한 경로에 JSON 파일이 없습니다.");
            }
        }
                EditorGUILayout.Space();

        if (GUILayout.Button("타일맵 불러오기 및 배치 (JSON → Tilemap)"))
        {
            if (System.IO.File.Exists(tilemapScript.jsonFilePath))
            {
                string json = System.IO.File.ReadAllText(tilemapScript.jsonFilePath);
                tilemapScript.LoadMapFromJson(json);
                Debug.Log($"타일맵이 {tilemapScript.jsonFilePath}에서 씬에 배치되었습니다.");
            }
            else
            {
                Debug.LogWarning("지정한 경로에 JSON 파일이 없습니다.");
            }
        }
    }
}
