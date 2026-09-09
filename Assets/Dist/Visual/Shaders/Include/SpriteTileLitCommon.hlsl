#ifndef SPRITE_TILE_LIT_COMMON_INCLUDED
#define SPRITE_TILE_LIT_COMMON_INCLUDED

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    float4 _Color;
    float4 _RendererColor;
    float  _DarknessFactor;
    float  _AmbientLight;
    float  _AdditionalLightEnabled;
    float  _GhostAmount;
    float  _EmphasisAdd;
    float  _SightLineBuildingHidden;
    float  _CharacterOcclusion;
    float  _Cutoff;
    float4 _UV00;
    float4 _UV10;
    float4 _UV01;
    float4 _UV11;
CBUFFER_END

// 화면 픽셀 Bayer — 노이즈 텍스처 없이 occlusion 디졸브 미리보기.
float CharacterOcclusionBayer4x4(uint2 pix)
{
    const float kBayer[16] =
    {
        0.0 / 16.0,  8.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0,
        12.0 / 16.0, 4.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0,
        3.0 / 16.0, 11.0 / 16.0,  1.0 / 16.0,  9.0 / 16.0,
        15.0 / 16.0, 7.0 / 16.0, 13.0 / 16.0,  5.0 / 16.0
    };
    return kBayer[(pix.x & 3u) + (pix.y & 3u) * 4u];
}

// SpriteUV4CornerWarp.hlsl 구현은 CBUFFER 선언 이후에 include한다.
float2 SpriteUV4CornerWarp(float2 inUV);

#ifdef SPRITE_TILE_LIT_FORWARD_PASS

void SpriteTileAlphaClip(half alpha)
{
    #ifdef _ALPHATEST_ON
        clip(alpha - _Cutoff);
    #else
        clip(alpha - 0.001h);
    #endif
}

half SpriteTileComputeLightStrength(float3 positionWS, float3 normalWS, float4 shadowCoord, uint isFrontFace)
{
    half lightStrength = 0.0h;
    const half3 lumaWeights = half3(0.2126h, 0.7152h, 0.0722h);

    normalWS = normalize(normalWS);
    // 양면 렌더링일 때 백페이스 노멀을 반전시켜 NdotL 누수를 방지
    normalWS *= (isFrontFace != 0u) ? 1.0h : -1.0h;

    Light mainLight = GetMainLight(shadowCoord);
    half mainNdotL = saturate(dot(normalWS, mainLight.direction));
    half mainLightIntensity = dot(mainLight.color, lumaWeights);
    lightStrength += mainNdotL * mainLight.distanceAttenuation * mainLight.shadowAttenuation * mainLightIntensity;

    #ifdef _ADDITIONAL_LIGHTS
        if (_AdditionalLightEnabled > 0.001f)
        {
            // Spot/Point 추가 라이트 그림자를 받으려면 shadowMask 오버로드를 써야 한다.
            half4 shadowMask = half4(1, 1, 1, 1);
            uint lightCount = GetAdditionalLightsCount();
            for (uint i = 0u; i < lightCount; i++)
            {
                Light light = GetAdditionalLight(i, positionWS, shadowMask);
                half nDotL = 1.0h;
                half lightIntensity = dot(light.color, lumaWeights);
                lightStrength += nDotL * light.distanceAttenuation * light.shadowAttenuation * lightIntensity * _AdditionalLightEnabled;
            }
        }
    #endif

    return saturate(lightStrength);
}

half4 SpriteTileShadeAndPost(half4 finalColor, half baseAlpha, float4 positionCS, half lightStrength)
{
    half brightness = lerp(_AmbientLight, 1.0h, lightStrength);
    brightness = lerp(1.0h, brightness, _DarknessFactor);
    finalColor.rgb *= brightness;

    half ghostAmt = saturate((half)_GhostAmount);
    finalColor.rgb *= lerp(1.0h, 0.74h, ghostAmt);

    half sightHidden = saturate((half)_SightLineBuildingHidden);
    finalColor.rgb = lerp(finalColor.rgb, half3(0.02h, 0.02h, 0.02h), sightHidden);

    finalColor.a = baseAlpha;

    half occlusion = saturate((half)_CharacterOcclusion);
    if (occlusion > 0.0h)
    {
        float dither = CharacterOcclusionBayer4x4(uint2(floor(positionCS.xy)));
        clip(dither - occlusion);
    }

    half vivid = saturate((half)_EmphasisAdd);
    finalColor.rgb += vivid;

    return finalColor;
}


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

Varyings vert(Attributes IN)
{
    Varyings OUT;
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

    VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);
    OUT.positionWS = positions.positionWS;
    OUT.positionCS = positions.positionCS;
    OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
    OUT.uv = IN.uv;
    OUT.color = IN.color * _Color * _RendererColor;

    // URP/Lit과 동일한 방식으로 main light shadow 좌표 생성
    OUT.shadowCoord = GetShadowCoord(positions);
    return OUT;
}

half4 frag(Varyings IN, uint isFrontFace : SV_IsFrontFace) : SV_Target
{
    float2 sampleUV = SpriteUV4CornerWarp(IN.uv);
    half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV);
    half4 finalColor = texColor * IN.color;
    half baseAlpha = finalColor.a;

    SpriteTileAlphaClip(baseAlpha);

    half lightStrength = SpriteTileComputeLightStrength(IN.positionWS, IN.normalWS, IN.shadowCoord, isFrontFace);
    return SpriteTileShadeAndPost(finalColor, baseAlpha, IN.positionCS, lightStrength);
}

#endif // SPRITE_TILE_LIT_FORWARD_PASS

#ifdef SPRITE_TILE_LIT_SHADOW_PASS

// Shadow caster normal bias 계산용. URP가 런타임에 이 값을 채워 넣습니다.
float3 _LightDirection;
float3 _LightPosition;

struct AttributesShadow
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VaryingsShadow
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

float4 GetShadowPositionHClip(AttributesShadow input)
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

VaryingsShadow vertShadow(AttributesShadow IN)
{
    VaryingsShadow OUT;
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
    OUT.positionCS = GetShadowPositionHClip(IN);
    OUT.uv = IN.uv;
    return OUT;
}

half4 fragShadow(VaryingsShadow IN) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(IN);

    #ifdef _ALPHATEST_ON
        half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, SpriteUV4CornerWarp(IN.uv)).a;
        clip(alpha - _Cutoff);
    #endif

    return 0;
}

#endif // SPRITE_TILE_LIT_SHADOW_PASS

#endif // SPRITE_TILE_LIT_COMMON_INCLUDED
