using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.IO;

public static class ScreenRaycaster
{
    private const string AgentDebugLogPath = "debug-849359.log";

    public static bool TryGetMouseWorldPosition(Camera cam, float yLevel, out Vector3 worldPos)
    {
        worldPos = Vector3.zero;
        if (cam == null) return false;
        var mousePos = Pointer.current?.position.ReadValue() ?? Vector2.zero;
        Ray ray = cam.ScreenPointToRay(mousePos);
        // #region agent log
        if (Time.frameCount % 10 == 0)
        {
            AgentDebugLog(
                runId: "pre-fix",
                hypothesisId: "H2_RAY_PROJECTION",
                location: "ScreenRaycaster.cs:18",
                message: "screen ray generated",
                dataJson: $"{{\"frame\":{Time.frameCount},\"camName\":\"{Safe(cam.name)}\",\"mousePos\":\"{Safe(mousePos.ToString("F2"))}\",\"rayOrigin\":\"{Safe(ray.origin.ToString("F3"))}\",\"rayDir\":\"{Safe(ray.direction.ToString("F3"))}\",\"yLevel\":{yLevel.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}}}");
        }
        // #endregion
        if (Mathf.Abs(ray.direction.y) < 1e-6f) return false;
        float t = (yLevel - ray.origin.y) / ray.direction.y;
        if (t < 0f) return false;
        worldPos = ray.origin + ray.direction * t;
        // #region agent log
        if (Time.frameCount % 10 == 0)
        {
            AgentDebugLog(
                runId: "pre-fix",
                hypothesisId: "H2_RAY_PROJECTION",
                location: "ScreenRaycaster.cs:31",
                message: "screen ray projected to plane",
                dataJson: $"{{\"frame\":{Time.frameCount},\"t\":{t.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)},\"worldPos\":\"{Safe(worldPos.ToString("F3"))}\"}}");
        }
        // #endregion
        return true;
    }

    private static void AgentDebugLog(string runId, string hypothesisId, string location, string message, string dataJson)
    {
        try
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string line =
                "{\"sessionId\":\"849359\",\"runId\":\"" + runId +
                "\",\"hypothesisId\":\"" + hypothesisId +
                "\",\"location\":\"" + location +
                "\",\"message\":\"" + message +
                "\",\"data\":" + dataJson +
                ",\"timestamp\":" + timestamp + "}";
            File.AppendAllText(AgentDebugLogPath, line + Environment.NewLine);
        }
        catch
        {
        }
    }

    private static string Safe(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
