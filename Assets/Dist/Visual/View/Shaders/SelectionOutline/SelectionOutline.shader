Shader "Hidden/Project/SelectionOutline"
{
    // 풀스크린 합성 셰이더.
    // _BlitTexture = 현재 카메라 컬러, _MaskTex = SelectionMask가 채운 R8 RT.
    // 본인 픽셀 mask가 0이고 이웃 중 하나라도 1이면 외곽선 색을 출력.
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    TEXTURE2D_X(_MaskTex);
    SAMPLER(sampler_MaskTex);
    float4 _MaskTex_TexelSize;

    float4 _OutlineColor;
    float _ThicknessPx;

    half4 FragOutline(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float2 uv = input.texcoord;

        half src = SAMPLE_TEXTURE2D_X(_MaskTex, sampler_MaskTex, uv).r;
        half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

        if (src > 0.5h)
        {
            return sceneColor;
        }

        float2 texel = _MaskTex_TexelSize.xy * _ThicknessPx;
        half neighbor = 0.0h;
        neighbor += SAMPLE_TEXTURE2D_X(_MaskTex, sampler_MaskTex, uv + float2( texel.x, 0)).r;
        neighbor += SAMPLE_TEXTURE2D_X(_MaskTex, sampler_MaskTex, uv + float2(-texel.x, 0)).r;
        neighbor += SAMPLE_TEXTURE2D_X(_MaskTex, sampler_MaskTex, uv + float2(0,  texel.y)).r;
        neighbor += SAMPLE_TEXTURE2D_X(_MaskTex, sampler_MaskTex, uv + float2(0, -texel.y)).r;
        neighbor += SAMPLE_TEXTURE2D_X(_MaskTex, sampler_MaskTex, uv + float2( texel.x,  texel.y)).r;
        neighbor += SAMPLE_TEXTURE2D_X(_MaskTex, sampler_MaskTex, uv + float2(-texel.x,  texel.y)).r;
        neighbor += SAMPLE_TEXTURE2D_X(_MaskTex, sampler_MaskTex, uv + float2( texel.x, -texel.y)).r;
        neighbor += SAMPLE_TEXTURE2D_X(_MaskTex, sampler_MaskTex, uv + float2(-texel.x, -texel.y)).r;

        if (neighbor > 0.5h)
        {
            return half4(_OutlineColor.rgb, 1);
        }

        return sceneColor;
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "SelectionOutline"

            Cull Off
            ZWrite Off
            ZTest Always
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragOutline
            ENDHLSL
        }
    }

    Fallback Off
}
