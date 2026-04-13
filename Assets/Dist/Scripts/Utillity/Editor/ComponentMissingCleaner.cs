using UnityEditor;
using UnityEngine;

public class MissingScriptsCleaner : Editor
{
    [MenuItem("Tools/Clean Up Missing Scripts")]
    public static void CleanupMissingscripts()
    {
        // 현재 열려 있는 씬의 모든 게임 오브젝트를 가져옴
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        int count = 0;

        foreach (GameObject go in allObjects)
        {
            // Missing 스크립트 개수 확인 및 제거
            count += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        }
        Debug.Log($"{count}개의 유효하지 않은 스크립트를 정리했습니다.");
    }
}