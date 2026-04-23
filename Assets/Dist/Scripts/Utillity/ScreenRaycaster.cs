using UnityEngine;
using UnityEngine.InputSystem;

public static class ScreenRaycaster
{
    public static bool TryGetMouseWorldPosition(Camera cam, float yLevel, out Vector3 worldPos)
    {
        worldPos = Vector3.zero;
        if (cam == null) return false;
        var mousePos = Pointer.current?.position.ReadValue() ?? Vector2.zero;
        Ray ray = cam.ScreenPointToRay(mousePos);
        if (Mathf.Abs(ray.direction.y) < 1e-6f) return false;
        float t = (yLevel - ray.origin.y) / ray.direction.y;
        if (t < 0f) return false;
        worldPos = ray.origin + ray.direction * t;
        return true;
    }
}
