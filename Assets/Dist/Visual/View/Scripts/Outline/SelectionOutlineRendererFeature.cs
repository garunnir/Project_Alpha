using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

// ============================================================
// SelectionOutlineRendererFeature
// URP RendererFeature: SelectionLayerConfig가 가리키는 RenderingLayer 비트로 표시된
// 렌더러를 화면공간 외곽선으로 그린다. 머티리얼 인스턴스화/머티리얼 스왑 없이
// 오브젝트별 토글이 가능하여 SRP Batcher와 호환된다.
// ============================================================
public class SelectionOutlineRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private SelectionLayerConfig _layerConfig;
    [SerializeField] private Shader _maskShader;
    [SerializeField] private Shader _outlineShader;
    [SerializeField] private Color _outlineColor = Color.yellow;
    [SerializeField, Range(1, 8)] private int _outlineThicknessPx = 2;
    [SerializeField] private RenderPassEvent _passEvent = RenderPassEvent.AfterRenderingTransparents;

    private Material _maskMaterial;
    private Material _outlineMaterial;
    private SelectionOutlinePass _pass;

    public override void Create()
    {
        DisposeMaterials();

        if (_maskShader == null || _outlineShader == null || _layerConfig == null)
        {
            return;
        }

        _maskMaterial = CoreUtils.CreateEngineMaterial(_maskShader);
        _outlineMaterial = CoreUtils.CreateEngineMaterial(_outlineShader);

        _pass = new SelectionOutlinePass(
            _maskMaterial,
            _outlineMaterial,
            _layerConfig.RenderingLayerMask,
            _outlineColor,
            _outlineThicknessPx)
        {
            renderPassEvent = _passEvent,
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_pass == null) return;
        if (renderingData.cameraData.cameraType != CameraType.Game)
        {
            return;
        }
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        DisposeMaterials();
        _pass = null;
    }

    private void DisposeMaterials()
    {
        if (_maskMaterial != null)
        {
            CoreUtils.Destroy(_maskMaterial);
            _maskMaterial = null;
        }
        if (_outlineMaterial != null)
        {
            CoreUtils.Destroy(_outlineMaterial);
            _outlineMaterial = null;
        }
    }
}
