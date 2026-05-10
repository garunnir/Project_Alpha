using UnityEngine;

// ============================================================
// ShadeObjectController - 셰이더의 추가 조명 옵션 토글 제어
// ============================================================
public class ShadeObjectController : ShaderController
{
    private int _additionalLightEnabledId;
    private int _ghostAmountId;
    private int _characterOcclusionId;

    protected override void CachePropertyIDs()
    {
        _additionalLightEnabledId = Shader.PropertyToID("_AdditionalLightEnabled");
        _ghostAmountId = Shader.PropertyToID("_GhostAmount");
        _characterOcclusionId = Shader.PropertyToID("_CharacterOcclusion");
    }

    public void SetAdditionalLightEnabled(bool enabled)
    {
        if (Mat == null)
        {
            return;
        }
        Mat.SetFloat(_additionalLightEnabledId, enabled ? 1.0f : 0.0f);
    }

    // URP에서는 MaterialPropertyBlock 사용 시 SRP Batcher가 깨지므로
    // 머티리얼 인스턴스의 프로퍼티를 직접 변경한다.
    public void SetGhost(bool enabled)
    {
        SetGhostAmount(enabled ? 1.0f : 0.0f);
    }

    public void SetGhostAmount(float amount)
    {
        if (Mat == null)
        {
            return;
        }
        Mat.SetFloat(_ghostAmountId, Mathf.Clamp01(amount));
    }

    /// <summary>캐릭터에 가려질 때 알파 페이드(0 불투명 ~ 1 완전 투명).</summary>
    public void SetCharacterOcclusion(float occlusion01)
    {
        if (Mat == null)
        {
            return;
        }
        Mat.SetFloat(_characterOcclusionId, Mathf.Clamp01(occlusion01));
    }
}
