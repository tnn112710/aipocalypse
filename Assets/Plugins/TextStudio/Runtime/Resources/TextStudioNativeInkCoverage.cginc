#ifndef TEXT_STUDIO_NATIVE_INK_COVERAGE_INCLUDED
#define TEXT_STUDIO_NATIVE_INK_COVERAGE_INCLUDED

#include "TextStudioWritingSourceCoverage.cginc"

float TextStudioInkCross(float2 a, float2 b) { return a.x * b.y - a.y * b.x; }

float2 TextStudioInkUVAtU(float u, float2 q, float2 b, float2 c, float2 d)
{
    float2 tangent = c + d * u;
    return float2(u, dot(q - b * u, tangent) / max(1e-16, dot(tangent, tangent)));
}

float TextStudioInkUVPenalty(float2 uv)
{
    float2 outside = uv - saturate(uv);
    return dot(outside, outside);
}

bool TextStudioInkLocalCoordinates(float2 surfacePoint, float4 bottom, float4 top, out float2 uv,
    out float2 du, out float2 dv)
{
    float2 q = surfacePoint - bottom.xy;
    float2 b = bottom.zw - bottom.xy;
    float2 c = top.xy - bottom.xy;
    float2 d = top.zw - bottom.zw - c;
    float a = -TextStudioInkCross(b, d);
    float k = TextStudioInkCross(q, d) - TextStudioInkCross(b, c);
    float n = TextStudioInkCross(q, c);
    if (abs(a) < max(1e-16, abs(k) * 1e-6))
        uv = TextStudioInkUVAtU(abs(k) > 1e-16 ? -n / k : 0, q, b, c, d);
    else
    {
        float discriminant = sqrt(max(0, k * k - 4 * a * n));
        float2 first = TextStudioInkUVAtU((-k + discriminant) / (2 * a), q, b, c, d);
        float2 second = TextStudioInkUVAtU((-k - discriminant) / (2 * a), q, b, c, d);
        uv = TextStudioInkUVPenalty(first) <= TextStudioInkUVPenalty(second) ? first : second;
    }
    du = b + d * uv.y;
    dv = c + d * uv.x;
    return abs(TextStudioInkCross(du, dv)) > 1e-16;
}

int TextStudioNativeInkClip(float writingSample, float4 fontPlane, out float3 clipPlane)
{
    clipPlane = 0;
    if (_TextStudioWritingEnabled < .5 || writingSample < .5) return 1;
    uint2 packed = TextStudioLoadWritingSample((uint)round(writingSample - 1));
    // Partial opacity retains the original face over the whole glyph.
    if (((packed.y >> 20) & 1u) == 0u || (packed.x >> 16) < 65535u) return 1;
    if ((packed.y & 0x0fffu) == 0u) return 0;
    uint mode = (packed.y >> 12) & 0x0fu;
    if (mode != 1u && mode != 5u && mode != 12u) return 1;
    float progress = (packed.x & 0xffffu) / 65535.0;
    bool erase = mode == 12u;
    if (progress <= 0) return erase ? 1 : 0;
    if (progress >= 1) return erase ? 0 : 1;
    uint direction = (packed.y >> 16) & 0x07u;
    float2 coefficients = float2(1, 0);
    float offset = 0;
    if (direction == 1u) { coefficients = float2(-1, 0); offset = 1; }
    else if (direction == 2u) { coefficients = float2(0, -1); offset = 1; }
    else if (direction == 3u) coefficients = float2(0, 1);
    else if (direction == 4u) { coefficients = float2(.5, -.5); offset = .5; }
    else if (direction == 5u) { coefficients = float2(-.5, -.5); offset = 1; }
    else if (direction == 6u) coefficients = float2(.5, .5);
    else if (direction == 7u) { coefficients = float2(-.5, .5); offset = .5; }
    if (((packed.y >> 19) & 1u) != 0u) { coefficients = -coefficients; offset = 1 - offset; }
    clipPlane = float3(coefficients / fontPlane.zw, offset - progress - dot(coefficients, fontPlane.xy / fontPlane.zw));
    if (erase) clipPlane = -clipPlane;
    return 2;
}

int TextStudioInkWinding(float4 edge, float2 samplePosition)
{
    float cross = TextStudioInkCross(edge.zw - edge.xy, samplePosition - edge.xy);
    if (edge.y <= samplePosition.y && edge.w > samplePosition.y && cross > 0) return 1;
    if (edge.y > samplePosition.y && edge.w <= samplePosition.y && cross < 0) return -1;
    return 0;
}

float TextStudioNativeInkDistance(float2 surfacePoint, int start, int count, float4 source,
    float4 bottom, float4 top, float4 fontPlane)
{
    float3 clipPlane = 0;
    int visibility = TextStudioNativeInkClip(source.z, fontPlane, clipPlane);
    if (visibility == 0) return 1e6;
    float2 uv = 0, du = 0, dv = 0;
    if (!TextStudioInkLocalCoordinates(surfacePoint, bottom, top, uv, du, dv)) return 1e6;
    float2 samplePosition = fontPlane.xy + uv * fontPlane.zw;
    float clipSide = dot(clipPlane.xy, samplePosition) + clipPlane.z;
    float2 projected = samplePosition - clipPlane.xy * clipSide / max(1e-16, dot(clipPlane.xy, clipPlane.xy));
    float distanceSquared = 1e20;
    float2 nearest = samplePosition;
    int winding = 0, projectedWinding = 0;
    [loop]
    for (int index = 0; index < count; index++)
    {
        float4 edge = TextStudioLoadSurfaceEdge(start + index);
        winding += TextStudioInkWinding(edge, samplePosition);
        if (visibility == 2)
        {
            projectedWinding += TextStudioInkWinding(edge, projected);
            float a = dot(clipPlane.xy, edge.xy) + clipPlane.z;
            float b = dot(clipPlane.xy, edge.zw) + clipPlane.z;
            if (a > 0 && b > 0) continue;
            if ((a > 0) != (b > 0))
            {
                float2 intersection = lerp(edge.xy, edge.zw, a / (a - b));
                if (a > 0) edge.xy = intersection;
                else edge.zw = intersection;
            }
        }
        float2 direction = edge.zw - edge.xy;
        float t = saturate(dot(samplePosition - edge.xy, direction) / max(1e-16, dot(direction, direction)));
        float2 candidate = edge.xy + direction * t;
        float2 delta = samplePosition - candidate;
        float candidateSquared = dot(delta, delta);
        if (candidateSquared < distanceSquared) { distanceSquared = candidateSquared; nearest = candidate; }
    }
    // The clipped polygon also owns the part of the reveal front inside the original contours, including their holes.
    if (visibility == 2 && projectedWinding != 0)
    {
        float2 delta = samplePosition - projected;
        float candidateSquared = dot(delta, delta);
        if (candidateSquared < distanceSquared) { distanceSquared = candidateSquared; nearest = projected; }
    }
    if (distanceSquared >= 1e19) return 1e6;
    float distance = sqrt(distanceSquared);
    bool inside = winding != 0 && (visibility != 2 || clipSide <= 0);
    float2 gradient = (samplePosition - nearest) / max(1e-8, distance);
    du /= fontPlane.z; dv /= fontPlane.w;
    float determinant = TextStudioInkCross(du, dv);
    float2 localGradient = float2(gradient.x * dv.y - gradient.y * du.y, gradient.y * du.x - gradient.x * dv.x) / determinant;
    float scale = distance > 1e-8 ? rcp(max(1e-8, length(localGradient))) : sqrt(abs(determinant));
    // Clearance is applied after clipping in canonical em space, then converted to the posed local metric.
    return ((inside ? -distance : distance) - source.w) * scale;
}

#endif
