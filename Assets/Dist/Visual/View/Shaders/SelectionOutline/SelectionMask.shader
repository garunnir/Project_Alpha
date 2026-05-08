Shader "Hidden/Project/SelectionMask"
{
    // Selection 마스크 RT(R8)에 단색을 출력하기 위한 단순 패스.
    // RendererListDesc로 layerConfig.RenderingLayerMask 매칭 오브젝트만 그릴 때 사용한다.
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "SelectionMask"

            Cull Off
            ZWrite Off
            ZTest LEqual
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(1, 0, 0, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
