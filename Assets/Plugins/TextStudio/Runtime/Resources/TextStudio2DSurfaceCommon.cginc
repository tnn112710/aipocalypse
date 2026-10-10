#ifndef TEXT_STUDIO_SURFACE_COMMON_INCLUDED
#define TEXT_STUDIO_SURFACE_COMMON_INCLUDED

#if defined(TEXT_STUDIO_TEXTURE_TRANSPORT)
Texture2D<float4> _TextStudio2DSurfaceHeadersData;
Texture2D<float4> _TextStudio2DSurfaceEdgesData;
Texture2D<float4> _TextStudio2DSurfaceInkSourcesData;
float4 TextStudioLoadSurfaceHeader(int texel) { return _TextStudio2DSurfaceHeadersData.Load(int3(texel & 1023, texel >> 10, 0)); }
float4 TextStudioLoadSurfaceEdge(int texel) { return _TextStudio2DSurfaceEdgesData.Load(int3(texel & 1023, texel >> 10, 0)); }
float4 TextStudioLoadSurfaceInkSource(int texel) { return _TextStudio2DSurfaceInkSourcesData.Load(int3(texel & 1023, texel >> 10, 0)); }
#else
StructuredBuffer<float4> _TextStudio2DSurfaceHeaders;
StructuredBuffer<float4> _TextStudio2DSurfaceEdges;
StructuredBuffer<float4> _TextStudio2DSurfaceInkSources;
float4 TextStudioLoadSurfaceHeader(int index) { return _TextStudio2DSurfaceHeaders[index]; }
float4 TextStudioLoadSurfaceEdge(int index) { return _TextStudio2DSurfaceEdges[index]; }
float4 TextStudioLoadSurfaceInkSource(int index) { return _TextStudio2DSurfaceInkSources[index]; }
#endif
float _TextStudio2DSurfacesEnabled;
#define TEXT_STUDIO_SURFACE_HEADER_STRIDE 6

float2 TextStudioSurfaceMetricPoint(float2 surfacePoint, float2 handle)
{
    if (_TextStudio2DSurfacesEnabled < .5 || handle.x < .5) return surfacePoint;
    int header = (int)round(handle.x) * TEXT_STUDIO_SURFACE_HEADER_STRIDE;
    float4 metadata = TextStudioLoadSurfaceHeader(header);
    if (abs(metadata.w - 1) > .5 || abs(metadata.z - handle.y) > .5) return surfacePoint;
    float4 homogeneousPoint = float4(surfacePoint, 0, 1);
    float w = dot(TextStudioLoadSurfaceHeader(header + 5), homogeneousPoint);
    return float2(dot(TextStudioLoadSurfaceHeader(header + 3), homogeneousPoint),
        dot(TextStudioLoadSurfaceHeader(header + 4), homogeneousPoint)) / max(w, .000001);
}

float2 TextStudioSurfacePoint(float2 shapeLocal, float2 handle)
{
    if (_TextStudio2DSurfacesEnabled < .5 || handle.x < .5) return 0;
    float4 bounds = TextStudioLoadSurfaceHeader((int)round(handle.x) * TEXT_STUDIO_SURFACE_HEADER_STRIDE + 1);
    return lerp(bounds.xy, bounds.zw, shapeLocal);
}

float TextStudioSurfaceEdgeDistance(float2 surfacePoint, int start, int count)
{
    float distanceSquared = 1e20;
    int winding = 0;
    [loop]
    for (int index = 0; index < count; index++)
    {
        float4 edge = TextStudioLoadSurfaceEdge(start + index);
        float2 direction = edge.zw - edge.xy;
        float2 delta = surfacePoint - edge.xy;
        float t = saturate(dot(delta, direction) / max(1e-16, dot(direction, direction)));
        float2 nearest = delta - direction * t;
        distanceSquared = min(distanceSquared, dot(nearest, nearest));
        float cross = direction.x * delta.y - direction.y * delta.x;
        if (edge.y <= surfacePoint.y && edge.w > surfacePoint.y && cross > 0) winding++;
        if (edge.y > surfacePoint.y && edge.w <= surfacePoint.y && cross < 0) winding--;
    }
    return sqrt(distanceSquared) * (winding == 0 ? 1 : -1);
}

#include "TextStudioNativeInkCoverage.cginc"

float TextStudioSurfaceDistanceAtPoint(float2 surfacePoint, float2 handle)
{
    if (_TextStudio2DSurfacesEnabled < .5 || handle.x < .5) return 1e6;
    int header = (int)round(handle.x) * TEXT_STUDIO_SURFACE_HEADER_STRIDE;
    float4 metadata = TextStudioLoadSurfaceHeader(header);
    if (abs(metadata.z - handle.y) > .5) return 1e6;
    int start = (int)round(metadata.x);
    int count = min(2048, (int)round(metadata.y));
    if (metadata.w < 1.5) return TextStudioSurfaceEdgeDistance(surfacePoint, start, count);
    float4 sources = TextStudioLoadSurfaceHeader(header + 3);
    int sourceStart = (int)round(sources.x);
    int sourceCount = min(count, (int)round(sources.y));
    float distance = 1e6;
    [loop]
    for (int index = 0; index < sourceCount; index++)
    {
        int record = sourceStart + index * 4;
        float4 source = TextStudioLoadSurfaceInkSource(record);
        int edgeStart = start + (int)round(source.x), edgeCount = (int)round(source.y);
        float4 fontPlane = TextStudioLoadSurfaceInkSource(record + 3);
        float nativeDistance = 1e6;
        if (fontPlane.z > 0 && fontPlane.w > 0)
            nativeDistance = TextStudioNativeInkDistance(surfacePoint, edgeStart, edgeCount, source,
                TextStudioLoadSurfaceInkSource(record + 1), TextStudioLoadSurfaceInkSource(record + 2), fontPlane);
        else nativeDistance = TextStudioSurfaceEdgeDistance(surfacePoint, edgeStart, edgeCount);
        distance = min(distance, nativeDistance);
    }
    return distance;
}

float TextStudioSurfaceDistance(float2 shapeLocal, float2 handle)
{
    if (_TextStudio2DSurfacesEnabled < .5 || handle.x < .5) return 1e6;
    return TextStudioSurfaceDistanceAtPoint(TextStudioSurfacePoint(shapeLocal, handle), handle);
}

float4 TextStudioSurfaceAdjustmentMask(float2 handle)
{
    if (_TextStudio2DSurfacesEnabled < .5 || handle.x < .5) return 0;
    int slot = (int)round(handle.x);
    float4 metadata = TextStudioLoadSurfaceHeader(slot * TEXT_STUDIO_SURFACE_HEADER_STRIDE);
    return abs(metadata.z - handle.y) <= .5 ? TextStudioLoadSurfaceHeader(slot * TEXT_STUDIO_SURFACE_HEADER_STRIDE + 2) : 0;
}
#endif
