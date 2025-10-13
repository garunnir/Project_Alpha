using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using System;

public static class InspectorLockToggle
{
    [MenuItem("Tools/Toggle Inspector Lock %&l")] // Ctrl+Alt+L
    private static void ToggleInspectorLock()
    {
        var inspectorType = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
        var window = EditorWindow.GetWindow(inspectorType);
        var isLockedProp = inspectorType.GetProperty("isLocked", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        bool current = (bool)isLockedProp.GetValue(window, null);
        isLockedProp.SetValue(window, !current, null);
        window.Repaint();
    }
}
// Assets/Editor/ManualCompileWindow.cs
#if UNITY_EDITOR


public class ManualCompileWindow : EditorWindow
{
    const string MenuPath = "Tools/Manual Compile";
    const string AutoRefreshKey = "kAutoRefresh"; // Unity 내부 프리퍼런스 키 (비공식)

    bool autoRefresh;           // 현재 Auto Refresh 상태 캐시
    bool showLog = true;        // 완료 로그 출력 여부
    double lastClickTime;       // 버튼 연타 방지용 (선택)
    string status = "Idle";

    [MenuItem(MenuPath + "/Open Window")]
    public static void Open()
    {
        var win = GetWindow<ManualCompileWindow>("Manual Compile");
        win.minSize = new Vector2(320, 160);
        win.Show();
    }

    // 퀵 액션: 메뉴에서 바로 컴파일
    [MenuItem(MenuPath + "/Compile Now %#k")] // Ctrl/Cmd + Shift + K
    public static void CompileNowMenu()
    {
    if (EditorApplication.isCompiling)
        {
            Debug.LogWarning("[ManualCompile] Already compiling.");
            return;
        }

        // 저장 후 강제 리프레시 + 컴파일 요청
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        CompilationPipeline.RequestScriptCompilation();

        Debug.Log("[ManualCompile] Compile requested.");
    }

    void OnEnable()
    {
        // 현재 Auto Refresh 상태 읽기 (0=끄기, 1=켜기, 2=외부 변경 시 등일 수 있음)
        autoRefresh = EditorPrefs.GetInt(AutoRefreshKey, 1) != 0;

        CompilationPipeline.compilationStarted += OnCompilationStarted;
        CompilationPipeline.compilationFinished += OnCompilationFinished;
        AssemblyReloadEvents.afterAssemblyReload += OnAfterDomainReload;
        EditorApplication.update += Repaint; // 상태 갱신
    }

    void OnDisable()
    {
        CompilationPipeline.compilationStarted -= OnCompilationStarted;
        CompilationPipeline.compilationFinished -= OnCompilationFinished;
        AssemblyReloadEvents.afterAssemblyReload -= OnAfterDomainReload;
        EditorApplication.update -= Repaint;
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Manual Script Compilation", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Toggle("Is Compiling", EditorApplication.isCompiling);
        }

        EditorGUILayout.LabelField("Status", status);

        EditorGUILayout.Space(8);
        showLog = EditorGUILayout.ToggleLeft("Log on finish", showLog);

        EditorGUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save All", GUILayout.Height(26)))
        {
            AssetDatabase.SaveAssets();
            status = "Saved assets.";
        }

    using (new EditorGUI.DisabledScope(EditorApplication.isCompiling))
        {
            if (GUILayout.Button("Compile Now", GUILayout.Height(26)))
            {
                // 버튼 연타 약간 방지
                if (EditorApplication.timeSinceStartup - lastClickTime < 0.25f)
                    return;
                lastClickTime = EditorApplication.timeSinceStartup;

                AssetDatabase.SaveAssets();
                // 강제 리프레시: 변경된 에셋을 즉시 파이프라인에 반영
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                // 스크립트 컴파일 요청
                CompilationPipeline.RequestScriptCompilation();

                status = "Compile requested...";
                Debug.Log("[ManualCompile] Compile requested.");
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(12);
        EditorGUILayout.HelpBox(
            "‘Auto Refresh’를 끄면, 에셋 변경 시 자동으로 컴파일/리프레시하지 않습니다. "
          + "이 창에서 수동으로 ‘Compile Now’를 눌러 진행하세요.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        bool newAuto = EditorGUILayout.ToggleLeft("Auto Refresh (Editor Prefs)", autoRefresh);
        if (EditorGUI.EndChangeCheck())
        {
            autoRefresh = newAuto;
            // ⚠️ 내부 키 사용: Unity 공식 API는 아니므로 버전에 따라 다를 수 있습니다.
            EditorPrefs.SetInt(AutoRefreshKey, autoRefresh ? 1 : 0);
            status = $"Auto Refresh {(autoRefresh ? "On" : "Off")}";
        }

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Open Preferences > Asset Pipeline", GUILayout.Height(22)))
        {
            SettingsService.OpenUserPreferences("Preferences/Asset Pipeline");
        }
    }

    void OnCompilationStarted(object _)
    {
        status = "Compiling...";
        // 진행중 표시를 원한다면 임시 프로그레스바도 가능
        // EditorUtility.DisplayProgressBar("Compiling", "Script compilation in progress...", 0.5f);
    }

    void OnCompilationFinished(object _)
    {
        status = "Compilation finished (domain reload may occur).";
        // EditorUtility.ClearProgressBar(); // 위 DisplayProgressBar 썼다면 해제
        if (showLog)
            Debug.Log("[ManualCompile] Compilation finished.");
    }

    void OnAfterDomainReload()
    {
        // 어셈블리 리로드 후 콜백
        status = "Domain reloaded.";
        if (showLog)
            Debug.Log("[ManualCompile] Domain reloaded.");
    }
}
#endif
