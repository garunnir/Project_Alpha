Shader "Custom/SpriteFieldOfView"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _Color ("Tint", Color) = (1,1,1,1)
        _DarknessFactor ("시야 밖 어둠 강도", Range(0, 1)) = 0.85
        _AmbientLight ("최소 밝기", Range(0, 1)) = 0.15
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "RenderType"        = "Transparent"
            "RenderPipeline"    = "UniversalPipeline"
            "IgnoreProjector"   = "True"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                float4 color      : COLOR;
                float3 positionWS : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _RendererColor;
                float  _DarknessFactor;
                float  _AmbientLight;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color      = IN.color * _Color * _RendererColor;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 finalColor = texColor * IN.color;

                // 완전 투명 픽셀 제거
                clip(finalColor.a - 0.001);

                // 추가 라이트의 감쇠값을 누적해 가시 영역 밝기를 만든다.
                half lightStrength = 0.0h;

                #ifdef _ADDITIONAL_LIGHTS
                    uint lightCount = GetAdditionalLightsCount();
                    for (uint i = 0u; i < lightCount; i++)
                    {
                        Light light = GetAdditionalLight(i, IN.positionWS);
                        lightStrength += light.distanceAttenuation * light.shadowAttenuation;
                    }
                #endif
                lightStrength = saturate(lightStrength);

                // 시야 안: 밝게 / 시야 밖: 어둡게
                half brightness = lerp(_AmbientLight, 1.0h, lightStrength);
                brightness = lerp(1.0h, brightness, _DarknessFactor);

                finalColor.rgb *= brightness;
                return finalColor;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Lit-Default"
}
