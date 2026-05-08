using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// ============================================================
// SelectionOutlinePass
// URP 17 (Unity 6) RenderGraph API 기반 외곽선 합성 패스.
// 1) Mask Pass : 지정된 RenderingLayer 비트가 켜진 렌더러만 단색으로 R8 RT에 렌더
// 2) Composite Pass : 카메라 컬러 + 마스크를 읽어 임시 컬러 RT에 외곽선 합성
// 3) CopyBack Pass  : 임시 컬러 RT를 카메라 컬러로 복사
// ============================================================
public class SelectionOutlinePass : ScriptableRenderPass
{
    private const string k_MaskPassName = "Selection.Mask";
    private const string k_CompositePassName = "Selection.OutlineComposite";
    private const string k_CopyBackPassName = "Selection.OutlineCopyBack";
    private const string k_MaskTextureName = "_SelectionMaskTex";
    private const string k_TempColorTextureName = "_SelectionTempColorTex";

    private static readonly int s_MaskTexId = Shader.PropertyToID("_MaskTex");
    private static readonly int s_OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int s_ThicknessPxId = Shader.PropertyToID("_ThicknessPx");

    private static readonly Vector4 s_ScaleBias = new Vector4(1f, 1f, 0f, 0f);
    private static readonly ShaderTagId[] s_DefaultShaderTagIds =
    {
        new ShaderTagId("UniversalForward"),
        new ShaderTagId("UniversalForwardOnly"),
        new ShaderTagId("SRPDefaultUnlit"),
    };

    private readonly Material _maskMaterial;
    private readonly Material _outlineMaterial;
    private readonly uint _renderingLayerMask;
    private readonly Color _outlineColor;
    private readonly int _thicknessPx;

    public SelectionOutlinePass(Material maskMaterial, Material outlineMaterial, uint renderingLayerMask, Color outlineColor, int thicknessPx)
    {
        _maskMaterial = maskMaterial;
        _outlineMaterial = outlineMaterial;
        _renderingLayerMask = renderingLayerMask;
        _outlineColor = outlineColor;
        _thicknessPx = Mathf.Max(1, thicknessPx);
    }

    private class MaskPassData
    {
        public RendererListHandle rendererList;
    }

    private class CompositePassData
    {
        public Material material;
        public TextureHandle source;
        public TextureHandle mask;
        public Color outlineColor;
        public int thicknessPx;
    }

    private class CopyPassData
    {
        public TextureHandle source;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_maskMaterial == null || _outlineMaterial == null) return;
        if (_renderingLayerMask == 0u) return;

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
        UniversalLightData lightData = frameData.Get<UniversalLightData>();

        if (resourceData.isActiveTargetBackBuffer) return;

        TextureHandle cameraColor = resourceData.activeColorTexture;
        TextureHandle cameraDepth = resourceData.activeDepthTexture;
        if (!cameraColor.IsValid()) return;

        var camDesc = cameraData.cameraTargetDescriptor;
        int width = Mathf.Max(1, camDesc.width);
        int height = Mathf.Max(1, camDesc.height);

        var maskDesc = new RenderTextureDescriptor(width, height, GraphicsFormat.R8_UNorm, GraphicsFormat.None, 0)
        {
            msaaSamples = 1,
            useMipMap = false,
            autoGenerateMips = false,
        };
        TextureHandle maskHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, maskDesc, k_MaskTextureName, true);

        var tempColorDesc = camDesc;
        tempColorDesc.depthBufferBits = 0;
        tempColorDesc.msaaSamples = 1;
        TextureHandle tempColor = UniversalRenderer.CreateRenderGraphTexture(renderGraph, tempColorDesc, k_TempColorTextureName, true);

        // ---- Pass 1: Mask ----
        using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>(k_MaskPassName, out var passData))
        {
            SortingCriteria sortFlags = cameraData.defaultOpaqueSortFlags;
            RenderQueueRange queueRange = RenderQueueRange.all;
            FilteringSettings filterSettings = new FilteringSettings(queueRange, ~0, _renderingLayerMask);

            DrawingSettings drawSettings = RenderingUtils.CreateDrawingSettings(
                s_DefaultShaderTagIds[0], renderingData, cameraData, lightData, sortFlags);
            for (int i = 1; i < s_DefaultShaderTagIds.Length; i++)
            {
                drawSettings.SetShaderPassName(i, s_DefaultShaderTagIds[i]);
            }
            drawSettings.overrideMaterial = _maskMaterial;
            drawSettings.overrideMaterialPassIndex = 0;

            var listParams = new RendererListParams(renderingData.cullResults, drawSettings, filterSettings);
            passData.rendererList = renderGraph.CreateRendererList(listParams);

            builder.UseRendererList(passData.rendererList);
            builder.SetRenderAttachment(maskHandle, 0, AccessFlags.Write);
            if (cameraDepth.IsValid())
            {
                builder.SetRenderAttachmentDepth(cameraDepth, AccessFlags.Read);
            }
            builder.AllowPassCulling(false);

            builder.SetRenderFunc(static (MaskPassData data, RasterGraphContext ctx) =>
            {
                ctx.cmd.ClearRenderTarget(false, true, Color.clear);
                ctx.cmd.DrawRendererList(data.rendererList);
            });
        }

        // ---- Pass 2: Composite (camera color + mask -> tempColor) ----
        using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>(k_CompositePassName, out var passData))
        {
            passData.material = _outlineMaterial;
            passData.source = cameraColor;
            passData.mask = maskHandle;
            passData.outlineColor = _outlineColor;
            passData.thicknessPx = _thicknessPx;

            builder.UseTexture(cameraColor, AccessFlags.Read);
            builder.UseTexture(maskHandle, AccessFlags.Read);
            builder.SetRenderAttachment(tempColor, 0, AccessFlags.Write);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc(static (CompositePassData data, RasterGraphContext ctx) =>
            {
                data.material.SetColor(s_OutlineColorId, data.outlineColor);
                data.material.SetFloat(s_ThicknessPxId, data.thicknessPx);
                data.material.SetTexture(s_MaskTexId, data.mask);
                Blitter.BlitTexture(ctx.cmd, data.source, s_ScaleBias, data.material, 0);
            });
        }

        // ---- Pass 3: Copy back to camera color ----
        using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>(k_CopyBackPassName, out var passData))
        {
            passData.source = tempColor;

            builder.UseTexture(tempColor, AccessFlags.Read);
            builder.SetRenderAttachment(cameraColor, 0, AccessFlags.Write);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc(static (CopyPassData data, RasterGraphContext ctx) =>
            {
                Blitter.BlitTexture(ctx.cmd, data.source, s_ScaleBias, 0, false);
            });
        }
    }
}
