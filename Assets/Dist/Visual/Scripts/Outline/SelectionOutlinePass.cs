using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// ============================================================
// SelectionOutlinePass
// 타일 SelectionMask → R8 실루엣 → screen-space edge composite.
// ============================================================
public class SelectionOutlinePass : ScriptableRenderPass
{
    const string k_MaskPassName = "Selection.Mask";
    const string k_CompositePassName = "Selection.OutlineComposite";
    const string k_CompatProfilerTag = "SelectionOutline.Compat";
    const string k_MaskTextureName = "_SelectionMaskTex";
    const string k_TempColorTextureName = "_SelectionTempColorTex";

    static readonly int s_MaskTexId = Shader.PropertyToID("_MaskTex");
    static readonly int s_OutlineColorId = Shader.PropertyToID("_OutlineColor");
    static readonly int s_ThicknessPxId = Shader.PropertyToID("_ThicknessPx");
    static readonly int s_MaskTempRtId = Shader.PropertyToID("_SelectionMaskTex");
    static readonly int s_TempColorRtId = Shader.PropertyToID("_SelectionTempColorTex");

    static readonly Vector4 s_ScaleBias = new Vector4(1f, 1f, 0f, 0f);
    static readonly ShaderTagId[] s_DefaultShaderTagIds =
    {
        new ShaderTagId("UniversalForward"),
        new ShaderTagId("UniversalForwardOnly"),
        new ShaderTagId("SRPDefaultUnlit"),
    };

    readonly Shader _tileMaskShader;
    readonly uint _tileRenderingLayerMask;
    readonly Material _outlineMaterial;
    readonly Color _outlineColor;
    readonly int _thicknessPx;

    public SelectionOutlinePass(
        Shader tileMaskShader,
        uint tileRenderingLayerMask,
        Material outlineMaterial,
        Color outlineColor,
        int thicknessPx)
    {
        _tileMaskShader = tileMaskShader;
        _tileRenderingLayerMask = tileRenderingLayerMask;
        _outlineMaterial = outlineMaterial;
        _outlineColor = outlineColor;
        _thicknessPx = Mathf.Max(1, thicknessPx);
    }

    class MaskPassData
    {
        public RendererListHandle tileRendererList;
        public bool drawTileMask;
    }

    class CompositePassData
    {
        public Material material;
        public TextureHandle source;
        public TextureHandle mask;
        public Color outlineColor;
        public int thicknessPx;
    }

    class CopyPassData
    {
        public TextureHandle source;
    }

    bool HasTileMaskLayer => _tileMaskShader != null && _tileRenderingLayerMask != 0u;

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (_outlineMaterial == null || !HasTileMaskLayer)
            return;

        ScriptableRenderer renderer = renderingData.cameraData.renderer;
        RTHandle cameraColor = renderer.cameraColorTargetHandle;
        RTHandle cameraDepth = renderer.cameraDepthTargetHandle;
        if (cameraColor == null || !cameraColor.rt)
            return;

        CommandBuffer cmd = CommandBufferPool.Get(k_CompatProfilerTag);
        RenderTextureDescriptor camDesc = renderingData.cameraData.cameraTargetDescriptor;
        int width = Mathf.Max(1, camDesc.width);
        int height = Mathf.Max(1, camDesc.height);

        var maskDesc = new RenderTextureDescriptor(width, height, GraphicsFormat.R8_UNorm, GraphicsFormat.None, 0)
        {
            msaaSamples = 1,
            useMipMap = false,
            autoGenerateMips = false,
        };
        var tempColorDesc = camDesc;
        tempColorDesc.depthBufferBits = 0;
        tempColorDesc.msaaSamples = 1;

        cmd.GetTemporaryRT(s_MaskTempRtId, maskDesc, FilterMode.Point);
        cmd.GetTemporaryRT(s_TempColorRtId, tempColorDesc, FilterMode.Bilinear);

        SortingCriteria sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;

        SetMaskTarget(cmd, cameraDepth, s_MaskTempRtId);
        cmd.ClearRenderTarget(false, true, Color.clear);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        DrawTileMaskLayer(context, ref renderingData, sortFlags);
        CompositeOutline(cmd, context, cameraColor, s_MaskTempRtId);

        cmd.ReleaseTemporaryRT(s_MaskTempRtId);
        cmd.ReleaseTemporaryRT(s_TempColorRtId);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    static void SetMaskTarget(CommandBuffer cmd, RTHandle cameraDepth, int maskRtId)
    {
        if (cameraDepth != null && cameraDepth.rt)
            cmd.SetRenderTarget(maskRtId, cameraDepth);
        else
            cmd.SetRenderTarget(maskRtId);
    }

    void CompositeOutline(CommandBuffer cmd, ScriptableRenderContext context, RTHandle cameraColor, int maskRtId)
    {
        _outlineMaterial.SetColor(s_OutlineColorId, _outlineColor);
        _outlineMaterial.SetFloat(s_ThicknessPxId, _thicknessPx);
        cmd.SetGlobalTexture(s_MaskTexId, maskRtId);
        cmd.SetRenderTarget(s_TempColorRtId);
        Blitter.BlitTexture(cmd, cameraColor, s_ScaleBias, _outlineMaterial, 0);
        cmd.Blit(s_TempColorRtId, cameraColor);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
    }

    void DrawTileMaskLayer(
        ScriptableRenderContext context,
        ref RenderingData renderingData,
        SortingCriteria sortFlags)
    {
        if (!HasTileMaskLayer)
            return;

        FilteringSettings filterSettings = new FilteringSettings(RenderQueueRange.all, ~0, _tileRenderingLayerMask);
        DrawingSettings drawSettings = RenderingUtils.CreateDrawingSettings(
            s_DefaultShaderTagIds[0], ref renderingData, sortFlags);
        for (int i = 1; i < s_DefaultShaderTagIds.Length; i++)
            drawSettings.SetShaderPassName(i, s_DefaultShaderTagIds[i]);
        drawSettings.overrideShader = _tileMaskShader;
        drawSettings.overrideShaderPassIndex = 0;

        context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filterSettings);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_outlineMaterial == null || !HasTileMaskLayer)
            return;

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
        UniversalLightData lightData = frameData.Get<UniversalLightData>();

        if (resourceData.isActiveTargetBackBuffer)
            return;

        TextureHandle cameraColor = resourceData.activeColorTexture;
        TextureHandle cameraDepth = resourceData.activeDepthTexture;
        if (!cameraColor.IsValid())
            return;

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

        using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>(k_MaskPassName, out var passData))
        {
            SortingCriteria sortFlags = cameraData.defaultOpaqueSortFlags;
            RenderQueueRange queueRange = RenderQueueRange.all;

            passData.drawTileMask = true;

            FilteringSettings tileFilter = new FilteringSettings(queueRange, ~0, _tileRenderingLayerMask);
            DrawingSettings tileDraw = CreateMaskDrawingSettings(
                renderingData, cameraData, lightData, sortFlags, _tileMaskShader);
            passData.tileRendererList = renderGraph.CreateRendererList(
                new RendererListParams(renderingData.cullResults, tileDraw, tileFilter));
            builder.UseRendererList(passData.tileRendererList);

            builder.SetRenderAttachment(maskHandle, 0, AccessFlags.Write);
            if (cameraDepth.IsValid())
                builder.SetRenderAttachmentDepth(cameraDepth, AccessFlags.Read);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc(static (MaskPassData data, RasterGraphContext ctx) =>
            {
                ctx.cmd.ClearRenderTarget(false, true, Color.clear);
                if (data.drawTileMask)
                    ctx.cmd.DrawRendererList(data.tileRendererList);
            });
        }

        RecordCompositePass(renderGraph, k_CompositePassName, cameraColor, maskHandle, tempColor);
    }

    void RecordCompositePass(
        RenderGraph renderGraph,
        string passName,
        TextureHandle cameraColor,
        TextureHandle maskHandle,
        TextureHandle tempColor)
    {
        using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>(passName, out var passData))
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

        using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>(passName + ".Copy", out var passData))
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

    static DrawingSettings CreateMaskDrawingSettings(
        UniversalRenderingData renderingData,
        UniversalCameraData cameraData,
        UniversalLightData lightData,
        SortingCriteria sortFlags,
        Shader maskShader)
    {
        DrawingSettings drawSettings = RenderingUtils.CreateDrawingSettings(
            s_DefaultShaderTagIds[0], renderingData, cameraData, lightData, sortFlags);
        for (int i = 1; i < s_DefaultShaderTagIds.Length; i++)
            drawSettings.SetShaderPassName(i, s_DefaultShaderTagIds[i]);
        drawSettings.overrideShader = maskShader;
        drawSettings.overrideShaderPassIndex = 0;

        return drawSettings;
    }
}
