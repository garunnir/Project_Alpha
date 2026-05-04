Shader "Custom/SpriteFOVCutoutBillboard"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _Color ("Tint", Color) = (1,1,1,1)
        _DarknessFactor ("시야 밖 어둠 강도", Range(0, 1)) = 0.85
        _AmbientLight ("최소 밝기", Range(0, 1)) = 0.15
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        _Cutoff ("컷오프",Range(0,1)) = 1
        _BillboardForward ("빌보드 Forward (월드방향, XZ만 사용)", Vector) = (0,0,1,0)
        _DebugMode ("Debug Mode (0=Off,1=Forward,2=BillboardWS,3=BypassBillboard,4=FreezeForward)", Range(0,4)) = 0
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

            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
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
                float3 extraWS : TEXCOORD2;
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
                float  _Cutoff;
                float4 _BillboardForward;
                float  _DebugMode;
            CBUFFER_END

            float3 RotateAroundAxis(float3 value, float3 axis, float sinAngle, float cosAngle)
            {
                return (value * cosAngle) + (cross(axis, value) * sinAngle) + (axis * dot(axis, value) * (1.0 - cosAngle));
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);

                if (_DebugMode > 2.5 && _DebugMode < 3.5)
                {
                    OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                    OUT.extraWS = OUT.positionWS;
                    OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                    OUT.color = IN.color * _Color * _RendererColor;
                    return OUT;
                }

                float3 offsetOS = IN.positionOS.xyz; // pivotOS는 (0,0,0) 가정

                float3 targetForwardWS = float3(_BillboardForward.x, 0.0, _BillboardForward.z);
                float targetForwardLenSq = dot(targetForwardWS, targetForwardWS);
                targetForwardWS = (targetForwardLenSq > 1e-6) ? (targetForwardWS * rsqrt(targetForwardLenSq)) : float3(0.0, 0.0, 1.0);

                if (_DebugMode > 3.5)
                {
                    targetForwardWS = float3(0.0, 0.0, 1.0);
                }

                float3 col0 = float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20);
                float3 col1 = float3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21);
                float3 col2 = float3(unity_ObjectToWorld._m02, unity_ObjectToWorld._m12, unity_ObjectToWorld._m22);

                float3 rWS = col0;
                float rLenSq = dot(rWS, rWS);
                rWS = (rLenSq > 1e-6) ? (rWS * rsqrt(rLenSq)) : float3(1.0, 0.0, 0.0);

                float3 uWS = col1 - rWS * dot(col1, rWS);
                float uLenSq = dot(uWS, uWS);
                uWS = (uLenSq > 1e-6) ? (uWS * rsqrt(uLenSq)) : float3(0.0, 1.0, 0.0);

                float3 fWS = cross(rWS, uWS);
                float fLenSq = dot(fWS, fWS);
                fWS = (fLenSq > 1e-6) ? (fWS * rsqrt(fLenSq)) : float3(0.0, 0.0, 1.0);

                if (dot(fWS, col2) < 0.0)
                {
                    fWS = -fWS;
                }

                float3 targetForwardOS = float3(dot(targetForwardWS, rWS), dot(targetForwardWS, uWS), dot(targetForwardWS, fWS));
                targetForwardOS.y = 0.0;
                float targetForwardOSLenSq = dot(targetForwardOS, targetForwardOS);
                targetForwardOS = (targetForwardOSLenSq > 1e-6) ? (targetForwardOS * rsqrt(targetForwardOSLenSq)) : float3(0.0, 0.0, 1.0);

                float3 axisOS = float3(dot(float3(0.0, 1.0, 0.0), rWS), dot(float3(0.0, 1.0, 0.0), uWS), dot(float3(0.0, 1.0, 0.0), fWS));
                float axisLenSq = dot(axisOS, axisOS);
                axisOS = (axisLenSq > 1e-6) ? (axisOS * rsqrt(axisLenSq)) : float3(0.0, 1.0, 0.0);

                float3 currentForwardOS = float3(0.0, 0.0, 1.0);
                currentForwardOS = currentForwardOS - axisOS * dot(currentForwardOS, axisOS);
                float cfLenSq = dot(currentForwardOS, currentForwardOS);
                currentForwardOS = (cfLenSq > 1e-6) ? (currentForwardOS * rsqrt(cfLenSq)) : float3(0.0, 0.0, 1.0);

                float3 desiredForwardOS = targetForwardOS - axisOS * dot(targetForwardOS, axisOS);
                float dfLenSq = dot(desiredForwardOS, desiredForwardOS);
                desiredForwardOS = (dfLenSq > 1e-6) ? (desiredForwardOS * rsqrt(dfLenSq)) : currentForwardOS;

                float cosAngle = clamp(dot(currentForwardOS, desiredForwardOS), -1.0, 1.0);
                float sinAngle = dot(axisOS, cross(currentForwardOS, desiredForwardOS));

                float3 billboardOS = RotateAroundAxis(offsetOS, axisOS, sinAngle, cosAngle);

                OUT.positionCS = TransformObjectToHClip(billboardOS);
                OUT.extraWS = TransformObjectToWorld(billboardOS);

                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color      = IN.color * _Color * _RendererColor;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 finalColor = texColor * IN.color;

                if (_DebugMode > 0.5 && _DebugMode < 1.5)
                {
                    float3 f = float3(_BillboardForward.x, 0.0, _BillboardForward.z);
                    float fl = dot(f, f);
                    f = (fl > 1e-6) ? (f * rsqrt(fl)) : float3(0.0, 0.0, 1.0);
                    return half4(f.x * 0.5 + 0.5, 0.0, f.z * 0.5 + 0.5, 1.0);
                }
                if (_DebugMode > 1.5)
                {
                    float3 p = IN.extraWS * 0.1;
                    float3 c = frac(abs(p));
                    return half4(c, 1.0);
                }

                clip(finalColor.a - _Cutoff); 

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
