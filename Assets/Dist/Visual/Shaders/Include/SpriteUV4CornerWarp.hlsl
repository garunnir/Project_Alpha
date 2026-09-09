#ifndef SPRITE_UV4_CORNER_WARP_INCLUDED
#define SPRITE_UV4_CORNER_WARP_INCLUDED

// 4코너 UV 워프. 호출 측 CBUFFER에 _UV00~11, _MainTex_ST가 있어야 한다.
float2 SpriteUV4CornerWarp(float2 inUV)
{
    float2 baseUV = saturate(inUV);
    float2 uvBottom = lerp(_UV00.xy, _UV10.xy, baseUV.x);
    float2 uvTop = lerp(_UV01.xy, _UV11.xy, baseUV.x);
    float2 warpedUV = lerp(uvBottom, uvTop, baseUV.y);
    return warpedUV * _MainTex_ST.xy + _MainTex_ST.zw;
}

#endif
