using UnityEditor;

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
