using UnityEditor;
using UnityEngine;

public static class ShadowCaster2DRefMenu
{
    [MenuItem("Component/Rendering/2D/Shadow Caster 2D (Proxy)", false, 2000)]
    private static void AddProxyShadowCaster2D()
    {
        foreach (var obj in Selection.gameObjects)
        {
            if (obj == null)
                continue;

            Undo.AddComponent<ShadowCaster2DRef>(obj);
        }
    }

    [MenuItem("Component/Rendering/2D/Shadow Caster 2D (Proxy)", true)]
    private static bool ValidateAddProxyShadowCaster2D()
    {
        return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
    }
}

