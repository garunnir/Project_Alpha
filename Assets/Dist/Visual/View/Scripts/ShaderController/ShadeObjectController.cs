using UnityEngine;

// ============================================================
// ShadeObjectController - 셰이더의 추가 조명 옵션 토글 제어
// ============================================================
public class ShadeObjectController : ShaderController
{
    private int _additionalLightEnabledId;

    protected override void CachePropertyIDs()
    {
        _additionalLightEnabledId = Shader.PropertyToID("_AdditionalLightEnabled");
    }

    public void SetAdditionalLightEnabled(bool enabled)
    {
        if (Mat == null)
        {
            return;
        }
        Debug.LogError("SetAdditionalLightEnabled: " + enabled);
        Debug.LogError("Mat: " + Mat);
        Debug.LogError("additionalLightEnabledId: " + _additionalLightEnabledId);
        Mat.SetFloat(_additionalLightEnabledId, enabled ? 1.0f : 0.0f);
    }
}
