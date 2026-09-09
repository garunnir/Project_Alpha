Shader "Custom/SpriteUV4Point"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _Color ("Tint", Color) = (1,1,1,1)
        _DarknessFactor ("시야 밖 어둠 강도", Range(0, 1)) = 0.85
        _AmbientLight ("최소 밝기", Range(0, 1)) = 0.15
        _AdditionalLightEnabled ("추가 라이트 사용", Range(0, 1)) = 1
        _GhostAmount ("고스트 블렌드", Range(0, 1)) = 0
        _EmphasisAdd ("쨍한 강조(Add)", Range(0, 1)) = 0
        _SightLineBuildingHidden ("야외 시선 차단 building 바닥", Range(0, 1)) = 0
        _CharacterOcclusion ("캐릭터 가림 디졸브 (0없음 ~ 1완전)", Range(0, 1)) = 0
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

        Cull Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One Zero

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

            #define SPRITE_TILE_LIT_FORWARD_PASS
            #include "Include/SpriteTileLitCommon.hlsl"
            #include "Include/SpriteUV4CornerWarp.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            Cull Off
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            #define SPRITE_TILE_LIT_SHADOW_PASS
            #include "Include/SpriteTileLitCommon.hlsl"
            #include "Include/SpriteUV4CornerWarp.hlsl"
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Lit-Default"
}
