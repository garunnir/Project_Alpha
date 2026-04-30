Shader "Custom/SpriteUV4Point"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _Color ("Tint", Color) = (1,1,1,1)
        _DarknessFactor ("시야 밖 어둠 강도", Range(0, 1)) = 0.85
        _AmbientLight ("최소 밝기", Range(0, 1)) = 0.15
        _AdditionalLightEnabled ("추가 라이트 사용", Range(0, 1)) = 1
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clipping", Float) = 0
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
            "Queue"             = "AlphaTest"
            "RenderType"        = "TransparentCutout"
            "RenderPipeline"    = "UniversalPipeline"
            "IgnoreProjector"   = "True"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Blend Off
        Cull Off
        ZWrite On

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _RendererColor;
                float  _DarknessFactor;
                float  _AmbientLight;
                float  _AdditionalLightEnabled;
                float  _Cutoff;
                float4 _UV00;
                float4 _UV10;
                float4 _UV01;
                float4 _UV11;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                OUT.color = IN.color * _Color * _RendererColor;

                // URP/Lit과 동일한 방식으로 main light shadow 좌표 생성
                // (TransformWorldToShadowCoord보다 GetShadowCoord 쪽이 variant/캐스케이드 처리와 더 잘 맞는 편)
                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.shadowCoord = GetShadowCoord(positions);
                return OUT;
            }

            half4 frag(Varyings IN, uint isFrontFace : SV_IsFrontFace) : SV_Target
            {
                float2 baseUV = saturate(IN.uv);

                float2 uvBottom = lerp(_UV00.xy, _UV10.xy, baseUV.x);
                float2 uvTop = lerp(_UV01.xy, _UV11.xy, baseUV.x);
                float2 warpedUV = lerp(uvBottom, uvTop, baseUV.y);
                warpedUV = warpedUV * _MainTex_ST.xy + _MainTex_ST.zw;

                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, warpedUV);
                half4 finalColor = texColor * IN.color;

                #ifdef _ALPHATEST_ON
                    clip(finalColor.a - _Cutoff);
                #endif

                half lightStrength = 0.0h;
                const half3 lumaWeights = half3(0.2126h, 0.7152h, 0.0722h);
                half3 normalWS = normalize(IN.normalWS);
                // 양면 렌더링일 때 백페이스 노멀을 반전시켜 NdotL 누수를 방지
                normalWS *= (isFrontFace != 0u) ? 1.0h : -1.0h;

                Light mainLight = GetMainLight(IN.shadowCoord);
                half mainNdotL = saturate(dot(normalWS, mainLight.direction));
                half mainLightIntensity = dot(mainLight.color, lumaWeights);
                lightStrength += mainNdotL * mainLight.distanceAttenuation * mainLight.shadowAttenuation * mainLightIntensity;

                #ifdef _ADDITIONAL_LIGHTS
                    if (_AdditionalLightEnabled > 0.001f)
                    {
                    // 중요:
                    // RealtimeLights.hlsl에는 GetAdditionalLight 오버로드가 2개 있다.
                    // 1) GetAdditionalLight(i, positionWS)
                    //    -> light.shadowAttenuation을 1.0으로 둔다(그림자 미적용).
                    // 2) GetAdditionalLight(i, positionWS, shadowMask)
                    //    -> AdditionalLightShadow(...)를 호출해 shadowAttenuation을 계산한다(그림자 적용).
                    //
                    // 즉, Spot/Point 추가 라이트 그림자를 받으려면 반드시 (2) 오버로드를 써야 한다.
                    // 현재 셰이더는 Lit의 InputData 전체를 구성하지 않으므로, shadowMask는 기본값(완전 비가림 없음)을 넘긴다.
                    half4 shadowMask = half4(1, 1, 1, 1);
                    uint lightCount = GetAdditionalLightsCount();
                    for (uint i = 0u; i < lightCount; i++)
                    {
                        // 이 오버로드를 써야 light.shadowAttenuation에 "추가 라이트 그림자"가 반영된다.
                        Light light = GetAdditionalLight(i, IN.positionWS, shadowMask);
                        half nDotL = 1.0h; //saturate(dot(normalWS, light.direction));
                        half lightIntensity = dot(light.color, lumaWeights);
                        lightStrength += nDotL * light.distanceAttenuation * light.shadowAttenuation * lightIntensity * _AdditionalLightEnabled;
                    }
                    }
                #endif
                lightStrength = saturate(lightStrength);

                half brightness = lerp(_AmbientLight, 1.0h, lightStrength);
                brightness = lerp(1.0h, brightness, _DarknessFactor);

                finalColor.rgb *= brightness;
                return finalColor;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            Cull Back
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // Shadow caster normal bias 계산용. URP가 런타임에 이 값을 채워 넣습니다.
            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _RendererColor;
                float  _DarknessFactor;
                float  _AmbientLight;
                float  _AdditionalLightEnabled;
                float  _Cutoff;
                float4 _UV00;
                float4 _UV10;
                float4 _UV01;
                float4 _UV11;
            CBUFFER_END

            float2 WarpUV(float2 inUV)
            {
                float2 baseUV = saturate(inUV);
                float2 uvBottom = lerp(_UV00.xy, _UV10.xy, baseUV.x);
                float2 uvTop = lerp(_UV01.xy, _UV11.xy, baseUV.x);
                float2 warpedUV = lerp(uvBottom, uvTop, baseUV.y);
                return warpedUV * _MainTex_ST.xy + _MainTex_ST.zw;
            }

            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                positionCS = ApplyShadowClamping(positionCS);
                return positionCS;
            }

            Varyings vertShadow(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.positionCS = GetShadowPositionHClip(IN);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 fragShadow(Varyings IN) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                #ifdef _ALPHATEST_ON
                    half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, WarpUV(IN.uv)).a;
                    clip(alpha - _Cutoff);
                #endif

                return 0;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Lit-Default"
}
