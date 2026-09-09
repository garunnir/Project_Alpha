using UnityEngine;

// ============================================================
// ShadeObjectController - 셰이더의 추가 조명 옵션 토글 제어
// ============================================================
public class ShadeObjectController : ShaderController
{
    private int _additionalLightEnabledId;
    private int _ghostAmountId;
    private int _sightLineBuildingHiddenId;
    private int _characterOcclusionId;
    private int _emphasisAddId;

    protected override void CachePropertyIDs()
    {
        _additionalLightEnabledId = Shader.PropertyToID("_AdditionalLightEnabled");
        _ghostAmountId = Shader.PropertyToID("_GhostAmount");
        _sightLineBuildingHiddenId = Shader.PropertyToID("_SightLineBuildingHidden");
        _characterOcclusionId = Shader.PropertyToID("_CharacterOcclusion");
        _emphasisAddId = Shader.PropertyToID("_EmphasisAdd");
    }

    public void SetAdditionalLightEnabled(bool enabled) =>
        SetAdditionalLightBlend(enabled ? 1f : 0f);

    public void SetAdditionalLightBlend(float blend01)
    {
        if (Mat == null)
            return;

        Mat.SetFloat(_additionalLightEnabledId, Mathf.Clamp01(blend01));
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

    /// <summary>야외 시선 차단 building의 MinCellY Floor를 어둡게 표시합니다.</summary>
    public void SetSightLineBuildingHidden(bool hidden)
    {
        if (Mat == null)
            return;

        Mat.SetFloat(_sightLineBuildingHiddenId, hidden ? 1f : 0f);
    }

    /// <summary>캐릭터에 가려질 때 화면 디더 디졸브(0 없음 ~ 1 완전 제거).</summary>
    public void SetCharacterOcclusion(float occlusion01)
    {
        if (Mat == null)
        {
            return;
        }
        Mat.SetFloat(_characterOcclusionId, Mathf.Clamp01(occlusion01));
    }

    /// <summary>RGB Add 강조 (0 없음 ~ 1 최대). 조준·액션 타겟 피드백.</summary>
    public void SetEmphasisAdd(float amount01)
    {
        if (Mat == null)
            return;

        Mat.SetFloat(_emphasisAddId, Mathf.Clamp01(amount01));
    }
}
