#ifndef TEXT_STUDIO_PREPARED_FIELD_COMMON_INCLUDED
#define TEXT_STUDIO_PREPARED_FIELD_COMMON_INCLUDED

UNITY_DECLARE_TEX2DARRAY(_TextStudioPreparedFields);
// External glyph banks: a text engine that owns its glyph atlas binds that atlas as _TextStudioPreparedFields on its own
// material and this RGBA32F table of per-glyph atlas transforms (scale, offset.xy, page), read by stable handle with Load.
// Relocations inside the engine rewrite one texel, so its glyph geometry never needs an update.
Texture2D<float4> _TextStudioExternalGlyphTable;

struct TextStudioPreparedField
{
    float4 transform;
    float4 bounds;
    float4 decode;
};

// Mapping rows of external glyphs carry decode.x < 0, transform = (table handle, 1 / field height in em, single channel,
// 0) and bounds = the em rectangle the engine stores field data for. Their native coordinates are em.
bool TextStudioIsExternalField(TextStudioPreparedField row)
{
    return row.decode.x < 0;
}

// Atlas transform of an external row (em scale, offset.xy, page); zero for prepared and native rows.
float4 TextStudioLoadExternalFieldTransform(TextStudioPreparedField row)
{
    float4 result = 0;
    UNITY_BRANCH
    if (TextStudioIsExternalField(row))
    {
        float handle = max(0.0, round(row.transform.x));
        float tableRow = floor(handle / 1024.0);
        float4 atlas = _TextStudioExternalGlyphTable.Load(int3(handle - tableRow * 1024.0, tableRow, 0));
        result = float4(atlas.x * row.transform.y, atlas.yzw);
    }
    return result;
}

// Resolves an external row into the prepared sampling form. Engine banks flag decode.w = -1 (single channel) or -2 (multi
// channel).
TextStudioPreparedField TextStudioResolveExternalField(TextStudioPreparedField row, float4 atlas)
{
    if (!TextStudioIsExternalField(row)) return row;
    TextStudioPreparedField result;
    result.transform = float4(atlas.xx, atlas.yz);
    result.bounds = row.bounds * atlas.xxxx + atlas.yzyz;
    result.decode = float4(atlas.w + 1, row.decode.y, row.decode.z, row.transform.z > .5 ? -1 : -2);
    UNITY_BRANCH
    if (atlas.x <= 0)
    {
        // A handle the engine has not published yet has no transform: read far outside every glyph so nothing draws.
        result.transform = float4(1, 1, 10000, 10000);
        result.bounds = 0;
    }
    return result;
}

TextStudioPreparedField TextStudioGetPreparedField(uint styleSlot, int glyphIndex)
{
    TextStudioPreparedField result = (TextStudioPreparedField)0;
    if (TEXT_STUDIO_MAPPING_TABLE_ENABLED < .5 || styleSlot == 0 || glyphIndex < 0) return result;
    TextStudio2DGpuMappingHeader header = TextStudioLoadMappingHeader(styleSlot);
    if (glyphIndex >= (int)round(header.metadata.y)) return result;
    TextStudio2DGpuGlyphMapping glyph = TextStudioLoadGlyphMapping((int)round(header.metadata.x) + glyphIndex);
    result.transform = glyph.preparedTransform;
    result.bounds = glyph.preparedBounds;
    result.decode = glyph.preparedDecode;
    return result;
}

float4 TextStudioSamplePreparedField(TextStudioPreparedField source, float2 nativeUv)
{
    float2 uv = nativeUv * source.transform.xy + source.transform.zw;
    float2 safeUv = clamp(uv, source.bounds.xy, source.bounds.zw);
    float4 field = UNITY_SAMPLE_TEX2DARRAY_LOD(_TextStudioPreparedFields, float3(safeUv, source.decode.x - 1), 0);
    // Engine banks store distance growing outward; flip them into the inside-positive contract. A single-channel bank
    // stores one true distance, which every channel of the multi-field contract reads.
    UNITY_BRANCH
    if (source.decode.w < 0) field = 1 - (source.decode.w > -1.5 ? field.rrrr : field);
    float4 distance = .5 + (field - .5) * source.decode.y;
    // Continue exterior distance beyond the isolated glyph rectangle, never into a neighbour's texels.
    float2 outsideNative = (uv - safeUv) / max(abs(source.transform.xy), .000001);
    float continuation = length(outsideNative * float2(_TextureWidth, _TextureHeight)) / max(2.0 * _GradientScale, .0001);
    return distance - continuation;
}

#endif
