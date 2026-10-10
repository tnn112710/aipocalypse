#ifndef TEXT_STUDIO_WRITING_SOURCE_COVERAGE_INCLUDED
#define TEXT_STUDIO_WRITING_SOURCE_COVERAGE_INCLUDED

#if defined(TEXT_STUDIO_TEXTURE_TRANSPORT)
// Each packed sample travels as four exact 16-bit halves: low and high of x, then of y.
Texture2D<float4> _TextStudioWritingSamplesData;
uint2 TextStudioLoadWritingSample(uint texel)
{
    uint4 halves = (uint4)round(_TextStudioWritingSamplesData.Load(int3(texel & 1023u, texel >> 10, 0)));
    return uint2(halves.x | halves.y << 16, halves.z | halves.w << 16);
}
#else
StructuredBuffer<uint2> _TextStudioWritingSamples;
uint2 TextStudioLoadWritingSample(uint index) { return _TextStudioWritingSamples[index]; }
#endif
float _TextStudioWritingEnabled;

half TextStudioWritingSourceCoverage(float2 glyphLocal, float writingSample)
{
    if (_TextStudioWritingEnabled < .5 ||
        writingSample < .5)
    {
        return 1;
    }
    uint sampleIndex = (uint)round(writingSample - 1.0);
    uint2 packed = TextStudioLoadWritingSample(sampleIndex);
    half active = (packed.y >> 20) & 1u;
    if (active < .5)
        return 1;

    half progress = (packed.x & 0xffffu) / 65535.0;
    half opacity = (packed.x >> 16) / 65535.0;
    half sourceAlpha = (packed.y & 0x0fffu) / 4095.0;
    uint mode = (packed.y >> 12) & 0x0fu;
    uint direction = (packed.y >> 16) & 0x07u;
    half reverse = (packed.y >> 19) & 1u;
    half softness = ((packed.y >> 21) & 0xffu) / 255.0;
    float2 local = saturate(glyphLocal);
    half coordinate;
    if (direction == 1u)
        coordinate = 1 - local.x;
    else if (direction == 2u)
        coordinate = 1 - local.y;
    else if (direction == 3u)
        coordinate = local.y;
    else if (direction == 4u)
        coordinate = (local.x + 1 - local.y) * .5;
    else if (direction == 5u)
        coordinate = (1 - local.x + 1 - local.y) * .5;
    else if (direction == 6u)
        coordinate = (local.x + local.y) * .5;
    else if (direction == 7u)
        coordinate = (1 - local.x + local.y) * .5;
    else
        coordinate = local.x;
    coordinate = lerp(coordinate, 1 - coordinate, reverse);
    half width = max(
        fwidth(coordinate) * 1.5,
        max(.0005, softness * .25));
    half directional = 1 - smoothstep(
        progress - width,
        progress + width,
        coordinate);
    directional = progress <= 0 ? 0 : progress >= 1 ? 1 : directional;
    if (mode == 12u)
        directional = 1 - directional;
    half usesDirectional =
        mode == 1u || mode == 5u || mode == 12u;
    half coverage = sourceAlpha * lerp(1, directional, usesDirectional);
    coverage = lerp(1, coverage, opacity);
    return saturate(coverage);
}

#endif
