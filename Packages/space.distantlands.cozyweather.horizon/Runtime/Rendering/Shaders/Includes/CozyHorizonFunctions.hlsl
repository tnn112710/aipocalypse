#include "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Data/Includes/CozyUtilities.hlsl"

TEXTURE2D(_InputBlit);
TEXTURE2D(_FogTex);
TEXTURE2D(_Texture);
TEXTURECUBE(_Cubemap);
SamplerState linear_clamp_sampler;
SamplerState linear_repeatU_clampV_sampler;

float _LayerType;
float4 _Color;
float _FogAmount;
float _PlacementHeight;
float _VerticalScale;
float _Angle;
float _Tiling;
float _Alpha;
float _Rows;
float _Columns;
float _Pitch;
float _Yaw;
float _Roll;
float _Scale;

#define HORIZON_FLIPBOOK_FPS 12.0
#define HORIZON_TWO_PI 6.28318530718

float2 HorizonRotate2D(float2 v, float degrees)
{
    float s, c;
    sincos(radians(degrees), s, c);
    return float2(c * v.x - s * v.y, s * v.x + c * v.y);
}

float3 HorizonInverseEuler(float3 v, float pitchDeg, float yawDeg, float rollDeg)
{
    float sy, cy, sp, cp, sr, cr;
    sincos(radians( -yawDeg), sy, cy);
    sincos(radians( -pitchDeg), sp, cp);
    sincos(radians( -rollDeg), sr, cr);

    v = float3(cy * v.x + sy * v.z, v.y, -sy * v.x + cy * v.z);
    v = float3(v.x, cp * v.y - sp * v.z, sp * v.y + cp * v.z);
    v = float3(cr * v.x - sr * v.y, sr * v.x + cr * v.y, v.z);
    return v;
}

float HorizonFog(float heightT)
{
    return lerp(1.0, smoothstep(0.0, 1.0, saturate(heightT)), saturate(_FogAmount));
}

float3 CalculateFog(float2 uv)
{
    return SAMPLE_TEXTURE2D(_FogTex, linear_clamp_sampler, uv).rgb;
}

float2 HorizonBillboardUV(float3 viewDir, out float facing)
{
    float sy, cy, sp, cp;
    sincos(radians(_Yaw), sy, cy);
    sincos(radians(_Pitch), sp, cp);

    float3 forward = float3(sy * cp, sp, cy * cp);
    float3 upRef = abs(forward.y) > 0.999 ? float3(0, 0, 1) : float3(0, 1, 0);
    float3 right = normalize(cross(upRef, forward));
    float3 up = cross(forward, right);

    float d = dot(viewDir, forward);
    facing = d > 1e-4 ? 1.0 : 0.0;

    float3 p = viewDir / max(d, 1e-4);
    float2 local = float2(dot(p, right), dot(p, up));
    local = HorizonRotate2D(local, _Roll);

    return local / max(_Scale, 1e-4) + 0.5;
}

// A texture wrapped around the horizon in a horizontal band.
float4 RenderRibbon(float2 screenUV, float3 viewDir)
{
    float tiling = max(1.0, round(_Tiling));

    float azimuth = atan2(viewDir.x, viewDir.z) / HORIZON_TWO_PI;
    float u = (azimuth + _Angle / 360.0) * tiling;

    float vRaw = (viewDir.y - _PlacementHeight) / max(_VerticalScale, 1e-4);
    float mask = (vRaw >= 0.0 && vRaw <= 1.0) ? 1.0 : 0.0;
    float v = saturate(vRaw);

    float2 xz = viewDir.xz;
    float invDenom = tiling / (max(dot(xz, xz), 1e-6) * HORIZON_TWO_PI);
    float2 dxzX = float2(ddx(viewDir.x), ddx(viewDir.z));
    float2 dxzY = float2(ddy(viewDir.x), ddy(viewDir.z));
    float gradUx = (xz.y * dxzX.x - xz.x * dxzX.y) * invDenom;
    float gradUy = (xz.y * dxzY.x - xz.x * dxzY.y) * invDenom;

    float4 tex = SAMPLE_TEXTURE2D_GRAD(_Texture, linear_repeatU_clampV_sampler, float2(u, v),
    float2(gradUx, ddx(vRaw)), float2(gradUy, ddy(vRaw)));

    float alpha = tex.a * _Color.a * _Alpha * mask;
    return float4(lerp(tex.rgb * _Color.rgb, CalculateFog(screenUV), _FogAmount), alpha);
}

// A single billboard placed in the sky at (yaw, pitch).
float4 RenderSprite(float2 screenUV, float3 viewDir)
{
    float facing;
    float2 uv = HorizonBillboardUV(viewDir, facing);

    float2 ddxUV = ddx(uv);
    float2 ddyUV = ddy(uv);

    float inside = (uv.x >= 0.0 && uv.x <= 1.0 && uv.y >= 0.0 && uv.y <= 1.0) ? 1.0 : 0.0;

    float4 tex = SAMPLE_TEXTURE2D_GRAD(_Texture, linear_clamp_sampler, saturate(uv), ddxUV, ddyUV);

    float fog = HorizonFog(viewDir.y * 4.0 - _PlacementHeight);
    float alpha = tex.a * _Color.a * _Alpha * inside * facing;
    return float4(lerp(tex.rgb * _Color.rgb, CalculateFog(screenUV), _FogAmount), alpha);
}

// A billboard like the sprite, but the texture is an atlas of _Columns x _Rows frames
// played left-to-right, top-to-bottom.
float4 RenderFlipbook(float2 screenUV, float3 viewDir)
{
    float facing;
    float2 uv = HorizonBillboardUV(viewDir, facing);

    float2 cells = float2(max(_Columns, 1.0), max(_Rows, 1.0));
    float2 ddxUV = ddx(uv) / cells;
    float2 ddyUV = ddy(uv) / cells;

    float inside = (uv.x >= 0.0 && uv.x <= 1.0 && uv.y >= 0.0 && uv.y <= 1.0) ? 1.0 : 0.0;

    float frameCount = cells.x * cells.y;
    float frame = fmod(floor(_Time.y * HORIZON_FLIPBOOK_FPS), frameCount);
    float col = fmod(frame, cells.x);
    float row = floor(frame / cells.x);

    // Atlas row 0 is the top row; UV v = 0 is the bottom.
    float2 cellOffset = float2(col, cells.y - 1.0 - row);
    float2 atlasUV = (saturate(uv) + cellOffset) / cells;

    float4 tex = SAMPLE_TEXTURE2D_GRAD(_Texture, linear_clamp_sampler, atlasUV, ddxUV, ddyUV);

    float fog = HorizonFog(viewDir.y * 4.0 - _PlacementHeight);
    float alpha = tex.a * _Color.a * _Alpha * inside * facing;
    return float4(lerp(tex.rgb * _Color.rgb, CalculateFog(screenUV), _FogAmount), alpha);
}

// A full-sky cubemap, rotated by pitch/yaw/roll. Fades in above _PlacementHeight over _VerticalScale.
float4 RenderCubemap(float2 screenUV, float3 viewDir)
{
    float3 dir = HorizonInverseEuler(viewDir, _Pitch, _Yaw, _Roll);
    float4 tex = SAMPLE_TEXTURECUBE(_Cubemap, linear_clamp_sampler, dir);

    float heightT = (viewDir.y - _PlacementHeight) / max(_VerticalScale, 1e-4);
    float mask = saturate(heightT);

    float alpha = tex.a * _Color.a * _Alpha * mask;
    return float4(lerp(tex.rgb * _Color.rgb, CalculateFog(screenUV), _FogAmount), alpha);
}

void RenderHorizon_float(float2 uv, out float4 outputColor)
{
    float4 renderedResult = float4(0, 0, 0, 0);
    float4 inputColor = SAMPLE_TEXTURE2D(_InputBlit, linear_clamp_sampler, uv);

    float3 viewDirection = BetterViewDirection(uv);

    if (_LayerType == 0)
        renderedResult = RenderCubemap(uv, viewDirection);
    if (_LayerType == 1)
        renderedResult = RenderRibbon(uv, viewDirection);
    if (_LayerType == 2)
        renderedResult = RenderSprite(uv, viewDirection);
    if (_LayerType == 3)
        renderedResult = RenderFlipbook(uv, viewDirection);

    outputColor = lerp(inputColor, renderedResult, saturate(renderedResult.a));
}
