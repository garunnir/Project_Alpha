Shader "Hidden/Project/SelectionMask"
{
    // Selection 마스크 RT(R8): 원본 머티리얼 프로퍼티를 유지한 채(overrideShader)
    // 스프라이트 알파 실루엣만 기록한다. UV 워프는 SpriteUV4Point와 동일.
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        _Cutoff ("컷오프", Range(0,1)) = 0.5

        _UV00 ("UV Corner 00 (Left-Bottom)", Vector) = (0,0,0,0)
        _UV10 ("UV Corner 10 (Right-Bottom)", Vector) = (1,0,0,0)
        _UV01 ("UV Corner 01 (Left-Top)", Vector) = (0,1,0,0)
        _UV11 ("UV Corner 11 (Right-Top)", Vector) = (1,1,0,0)
    }

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
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _RendererColor;
                float  _Cutoff;
                float4 _UV00;
                float4 _UV10;
                float4 _UV01;
                float4 _UV11;
            CBUFFER_END

            #include "../Include/SpriteUV4CornerWarp.hlsl"

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                o.color = (half4)(input.color * _Color * _RendererColor);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // SpriteUV4Point ForwardLit과 동일 워프 (실루엣 정렬)
                float2 warpedUV = SpriteUV4CornerWarp(input.uv);

                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, warpedUV).a * input.color.a;
                clip(alpha - (half)_Cutoff);

                return half4(1, 0, 0, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
