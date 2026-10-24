#ifndef TEXT_STUDIO_2D_STYLE_PAINT_COMMON_INCLUDED
#define TEXT_STUDIO_2D_STYLE_PAINT_COMMON_INCLUDED

// Operations one fused primitive evaluates. Larger programs draw consecutive spans, each selecting eight rows.
#define TEXT_STUDIO_MAX_FUSED_OPERATIONS 8

#ifndef TEXT_STUDIO_MAX_GRADIENT_STOPS
#define TEXT_STUDIO_MAX_GRADIENT_STOPS 16
#endif

#define TEXT_STUDIO_STYLE_SCHEMA_VERSION 13

// Set only inside a Hero command-buffer coverage replay and reset before output.
#ifdef TEXT_STUDIO_HERO_PLAIN_ROW
// A plain Hero row never draws a coverage replay.
static const float _TextStudio2DGeometryCoveragePass = 0;
#else
float _TextStudio2DGeometryCoveragePass;
#endif

#ifndef TEXT_STUDIO_STYLE_HEADERS_BUFFER
#define TEXT_STUDIO_STYLE_HEADERS_BUFFER _TextStudio2DStyleHeaders
#endif

#ifndef TEXT_STUDIO_STYLE_OPERATIONS_BUFFER
#define TEXT_STUDIO_STYLE_OPERATIONS_BUFFER _TextStudio2DStyleOperations
#endif

#ifndef TEXT_STUDIO_STYLE_GRADIENTS_BUFFER
#define TEXT_STUDIO_STYLE_GRADIENTS_BUFFER _TextStudio2DGradientStops
#endif

#ifndef TEXT_STUDIO_STYLE_TABLE_ENABLED
#define TEXT_STUDIO_STYLE_TABLE_ENABLED _TextStudio2DStyleTableEnabled
#endif

#ifndef TEXT_STUDIO_MAPPING_HEADERS_BUFFER
#define TEXT_STUDIO_MAPPING_HEADERS_BUFFER _TextStudio2DMappingHeaders
#endif

#ifndef TEXT_STUDIO_MAPPING_GLYPHS_BUFFER
#define TEXT_STUDIO_MAPPING_GLYPHS_BUFFER _TextStudio2DGlyphMappings
#endif

#ifndef TEXT_STUDIO_MAPPING_RANGES_BUFFER
#define TEXT_STUDIO_MAPPING_RANGES_BUFFER _TextStudio2DRangeMappings
#endif

#ifndef TEXT_STUDIO_MAPPING_TABLE_ENABLED
#define TEXT_STUDIO_MAPPING_TABLE_ENABLED _TextStudio2DMappingTableEnabled
#endif

#ifndef TEXT_STUDIO_STYLE_TABLE_LAST_RECORDS
#define TEXT_STUDIO_STYLE_TABLE_LAST_RECORDS _TextStudio2DStyleTableLastRecords
#endif

#ifndef TEXT_STUDIO_MAPPING_TABLE_LAST_RECORDS
#define TEXT_STUDIO_MAPPING_TABLE_LAST_RECORDS _TextStudio2DMappingTableLastRecords
#endif

struct TextStudio2DGpuStyleHeader
{
    float4 metadata;
    float4 boundsExpansion;
    // x: operation record base, y: gradient stop record base of the interned program.
    float4 program;
};

struct TextStudio2DGpuOperation
{
    float4 layerMetadata;
    float4 executionMetadata;
    float4 paintMetadata;
    float4 color;
    float4 paintTransform0;
    float4 paintTransform1;
    float4 parameters0;
    float4 parameters1;
    float4 parameters2;
    float4 parameters3;
    float4 parameters4;
    float4 parameters5;
    float4 parameters6;
    float4 parameters7;
    float4 parameters8;
    float4 parameters9;
    float4 parameters10;
    float4 parameters11;
    float4 parameters12;
    float4 parameters13;
    float4 auxiliaryColor0;
    float4 auxiliaryColor1;
    float4 auxiliaryColor2;
    float4 auxiliaryColor3;
    float4 auxiliaryColor4;
    float4 auxiliaryColor5;
    float4 selectorMetadata;
};

struct TextStudio2DGpuGradientStop
{
    float4 metadata;
    float4 color;
};

struct TextStudio2DGpuMappingHeader
{
    float4 metadata;
    float4 objectBounds;
    // x: per-operation range row base, y: selection words (64 operations each) per glyph row.
    float4 program;
};

struct TextStudio2DGpuGlyphMapping
{
    float4 wordBounds;
    float4 lineBounds;
    float4 preparedTransform;
    float4 preparedBounds;
    float4 preparedDecode;
    float4 sourceMetadata;
    float4 appearanceBounds;
    float4 appearanceMetadata;
};

// One logical table contract with two transports, selected globally by TextStudio2DTransport. Structured buffers
// serve devices that admit them in every stage; full-float data textures addressed by integer texel serve WebGL 2
// and OpenGL ES 3.0. Records are sequences of float4; texel = record * size + field, stored at (texel & 1023, texel >> 10).
//
// Compiled programs read guarded records before their guards decide: fxc flattens small branches into unconditional
// loads and selects, so a disabled table is still read at a text's slot, and the empty header of an unused slot still
// indexes the record tables. Metal has no robust buffer access, unlike Direct3D and Vulkan, so every accessor clamps its
// record to the last record of the bound table, which the owning table publishes with each binding (style: x headers,
// y operations, z gradient stops, w text uniforms; mapping: x headers, y glyph mappings, z ranges). Fallbacks publish 0.
float4 TEXT_STUDIO_STYLE_TABLE_LAST_RECORDS;
float4 TEXT_STUDIO_MAPPING_TABLE_LAST_RECORDS;

// A negative record wraps to a large unsigned value and clamps to the last record as well.
uint TextStudioTableRecord(int record, float lastRecord) { return min((uint)record, (uint)lastRecord); }

#if defined(TEXT_STUDIO_TEXTURE_TRANSPORT)
Texture2D<float4> _TextStudio2DStyleHeadersData;
Texture2D<float4> _TextStudio2DStyleOperationsData;
Texture2D<float4> _TextStudio2DGradientStopsData;
Texture2D<float4> _TextStudio2DMappingHeadersData;
Texture2D<float4> _TextStudio2DGlyphMappingsData;
Texture2D<float4> _TextStudio2DRangeMappingsData;

float4 TextStudioDataTexel(Texture2D<float4> data, int texel)
{
    return data.Load(int3(texel & 1023, texel >> 10, 0));
}

TextStudio2DGpuStyleHeader TextStudioLoadStyleHeader(uint slot)
{
    int texel = (int)TextStudioTableRecord((int)slot, TEXT_STUDIO_STYLE_TABLE_LAST_RECORDS.x) * 3;
    TextStudio2DGpuStyleHeader header;
    header.metadata = TextStudioDataTexel(_TextStudio2DStyleHeadersData, texel);
    header.boundsExpansion = TextStudioDataTexel(_TextStudio2DStyleHeadersData, texel + 1);
    header.program = TextStudioDataTexel(_TextStudio2DStyleHeadersData, texel + 2);
    return header;
}

TextStudio2DGpuOperation TextStudioLoadOperation(int index)
{
    int texel = (int)TextStudioTableRecord(index, TEXT_STUDIO_STYLE_TABLE_LAST_RECORDS.y) * 27;
    TextStudio2DGpuOperation operation;
    operation.layerMetadata = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel);
    operation.executionMetadata = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 1);
    operation.paintMetadata = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 2);
    operation.color = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 3);
    operation.paintTransform0 = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 4);
    operation.paintTransform1 = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 5);
    operation.parameters0 = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 6);
    operation.parameters1 = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 7);
    operation.parameters2 = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 8);
    operation.parameters3 = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 9);
    operation.parameters4 = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 10);
    operation.parameters5 = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 11);
    operation.parameters6 = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 12);
    operation.parameters7 = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 13);
    operation.parameters8 = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 14);
    operation.parameters9 = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 15);
    operation.parameters10 = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 16);
    operation.parameters11 = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 17);
    operation.parameters12 = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 18);
    operation.parameters13 = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 19);
    operation.auxiliaryColor0 = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 20);
    operation.auxiliaryColor1 = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 21);
    operation.auxiliaryColor2 = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 22);
    operation.auxiliaryColor3 = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 23);
    operation.auxiliaryColor4 = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 24);
    operation.auxiliaryColor5 = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 25);
    operation.selectorMetadata = TextStudioDataTexel(_TextStudio2DStyleOperationsData, texel + 26);
    return operation;
}

TextStudio2DGpuGradientStop TextStudioLoadGradientStop(int index)
{
    int texel = (int)TextStudioTableRecord(index, TEXT_STUDIO_STYLE_TABLE_LAST_RECORDS.z) * 2;
    TextStudio2DGpuGradientStop stop;
    stop.metadata = TextStudioDataTexel(_TextStudio2DGradientStopsData, texel);
    stop.color = TextStudioDataTexel(_TextStudio2DGradientStopsData, texel + 1);
    return stop;
}

TextStudio2DGpuMappingHeader TextStudioLoadMappingHeader(uint slot)
{
    int texel = (int)TextStudioTableRecord((int)slot, TEXT_STUDIO_MAPPING_TABLE_LAST_RECORDS.x) * 3;
    TextStudio2DGpuMappingHeader header;
    header.metadata = TextStudioDataTexel(_TextStudio2DMappingHeadersData, texel);
    header.objectBounds = TextStudioDataTexel(_TextStudio2DMappingHeadersData, texel + 1);
    header.program = TextStudioDataTexel(_TextStudio2DMappingHeadersData, texel + 2);
    return header;
}

TextStudio2DGpuGlyphMapping TextStudioLoadGlyphMapping(int index)
{
    int texel = (int)TextStudioTableRecord(index, TEXT_STUDIO_MAPPING_TABLE_LAST_RECORDS.y) * 8;
    TextStudio2DGpuGlyphMapping glyph;
    glyph.wordBounds = TextStudioDataTexel(_TextStudio2DGlyphMappingsData, texel);
    glyph.lineBounds = TextStudioDataTexel(_TextStudio2DGlyphMappingsData, texel + 1);
    glyph.preparedTransform = TextStudioDataTexel(_TextStudio2DGlyphMappingsData, texel + 2);
    glyph.preparedBounds = TextStudioDataTexel(_TextStudio2DGlyphMappingsData, texel + 3);
    glyph.preparedDecode = TextStudioDataTexel(_TextStudio2DGlyphMappingsData, texel + 4);
    glyph.sourceMetadata = TextStudioDataTexel(_TextStudio2DGlyphMappingsData, texel + 5);
    glyph.appearanceBounds = TextStudioDataTexel(_TextStudio2DGlyphMappingsData, texel + 6);
    glyph.appearanceMetadata = TextStudioDataTexel(_TextStudio2DGlyphMappingsData, texel + 7);
    return glyph;
}

float4 TextStudioLoadRange(int index)
{
    return TextStudioDataTexel(_TextStudio2DRangeMappingsData, (int)TextStudioTableRecord(index, TEXT_STUDIO_MAPPING_TABLE_LAST_RECORDS.z));
}
#else
StructuredBuffer<TextStudio2DGpuStyleHeader> TEXT_STUDIO_STYLE_HEADERS_BUFFER;
StructuredBuffer<TextStudio2DGpuOperation> TEXT_STUDIO_STYLE_OPERATIONS_BUFFER;
StructuredBuffer<TextStudio2DGpuGradientStop> TEXT_STUDIO_STYLE_GRADIENTS_BUFFER;
StructuredBuffer<TextStudio2DGpuMappingHeader> TEXT_STUDIO_MAPPING_HEADERS_BUFFER;
StructuredBuffer<TextStudio2DGpuGlyphMapping> TEXT_STUDIO_MAPPING_GLYPHS_BUFFER;
StructuredBuffer<float4> TEXT_STUDIO_MAPPING_RANGES_BUFFER;

TextStudio2DGpuStyleHeader TextStudioLoadStyleHeader(uint slot)
{
    return TEXT_STUDIO_STYLE_HEADERS_BUFFER[TextStudioTableRecord((int)slot, TEXT_STUDIO_STYLE_TABLE_LAST_RECORDS.x)];
}
TextStudio2DGpuOperation TextStudioLoadOperation(int index)
{
    return TEXT_STUDIO_STYLE_OPERATIONS_BUFFER[TextStudioTableRecord(index, TEXT_STUDIO_STYLE_TABLE_LAST_RECORDS.y)];
}
TextStudio2DGpuGradientStop TextStudioLoadGradientStop(int index)
{
    return TEXT_STUDIO_STYLE_GRADIENTS_BUFFER[TextStudioTableRecord(index, TEXT_STUDIO_STYLE_TABLE_LAST_RECORDS.z)];
}
TextStudio2DGpuMappingHeader TextStudioLoadMappingHeader(uint slot)
{
    return TEXT_STUDIO_MAPPING_HEADERS_BUFFER[TextStudioTableRecord((int)slot, TEXT_STUDIO_MAPPING_TABLE_LAST_RECORDS.x)];
}
TextStudio2DGpuGlyphMapping TextStudioLoadGlyphMapping(int index)
{
    return TEXT_STUDIO_MAPPING_GLYPHS_BUFFER[TextStudioTableRecord(index, TEXT_STUDIO_MAPPING_TABLE_LAST_RECORDS.y)];
}
float4 TextStudioLoadRange(int index)
{
    return TEXT_STUDIO_MAPPING_RANGES_BUFFER[TextStudioTableRecord(index, TEXT_STUDIO_MAPPING_TABLE_LAST_RECORDS.z)];
}
#endif
UNITY_DECLARE_TEX2DARRAY(_TextStudio2DPaintTextures);
float TEXT_STUDIO_STYLE_TABLE_ENABLED;
float TEXT_STUDIO_MAPPING_TABLE_ENABLED;
float _TextStudio2DPaintTexturesEnabled;

#ifdef TEXT_STUDIO_UNIFORM_ROW
// Uniform Hero rows (TextStudio2DHeroUniformRow): the drawn operation's record and its gradient stops travel as material
// constants, packed by TextStudio2DGpuStylePacking exactly like their style-table records, so the row reads no style table.
// TextStudio2DHeroMaterials.SupportsUniformRow admits only rows whose program reads no other record.
#define TEXT_STUDIO_UNIFORM_ROW_STOPS 8
float4 _TextStudioHeroRowLayer;
float4 _TextStudioHeroRowExecution;
float4 _TextStudioHeroRowPaint;
float4 _TextStudioHeroRowColor;
float4 _TextStudioHeroRowPaintTransform0;
float4 _TextStudioHeroRowPaintTransform1;
float4 _TextStudioHeroRowParameters0;
float4 _TextStudioHeroRowParameters1;
float4 _TextStudioHeroRowParameters2;
// Positions of stops 0 to 3 and 4 to 7, then each stop's colour, from the paint's first stop.
float4 _TextStudioHeroRowStopPositions0;
float4 _TextStudioHeroRowStopPositions1;
float4 _TextStudioHeroRowStopColor0;
float4 _TextStudioHeroRowStopColor1;
float4 _TextStudioHeroRowStopColor2;
float4 _TextStudioHeroRowStopColor3;
float4 _TextStudioHeroRowStopColor4;
float4 _TextStudioHeroRowStopColor5;
float4 _TextStudioHeroRowStopColor6;
float4 _TextStudioHeroRowStopColor7;

TextStudio2DGpuOperation TextStudioUniformRowOperation()
{
    // Fill, stroke, shadow and glow records pack nothing beyond these fields; the others stay zero, as in the table.
    TextStudio2DGpuOperation operation = (TextStudio2DGpuOperation)0;
    operation.layerMetadata = _TextStudioHeroRowLayer;
    operation.executionMetadata = _TextStudioHeroRowExecution;
    operation.paintMetadata = _TextStudioHeroRowPaint;
    operation.color = _TextStudioHeroRowColor;
    operation.paintTransform0 = _TextStudioHeroRowPaintTransform0;
    operation.paintTransform1 = _TextStudioHeroRowPaintTransform1;
    operation.parameters0 = _TextStudioHeroRowParameters0;
    operation.parameters1 = _TextStudioHeroRowParameters1;
    operation.parameters2 = _TextStudioHeroRowParameters2;
    return operation;
}

// One step of the table's stop scan over the row's stops: true, with the colour before the operation tint, once the scan
// ends at stop index. Each caller passes its stop's own constants, so no stop is addressed dynamically.
bool TextStudioUniformRowGradientStep(
    int index,
    int stopCount,
    half coordinate,
    float position,
    float4 stopColor,
    inout float previousPosition,
    inout float4 previousColor,
    out float4 color)
{
    color = previousColor;
    if (index >= stopCount)
        return true;
    if (coordinate <= position)
    {
        half denominator = max(
            .00001,
            position - previousPosition);
        half amount = saturate(
            (coordinate - previousPosition) / denominator);
        color = lerp(previousColor, stopColor, amount);
        return true;
    }
    previousPosition = position;
    previousColor = stopColor;
    return false;
}
#endif

int TextStudioStyleOperationBase(uint styleSlot)
{
    return (int)round(TextStudioLoadStyleHeader(styleSlot).program.x);
}

TextStudio2DGpuOperation TextStudioStyleOperation(uint styleSlot, int operationIndex)
{
    return TextStudioLoadOperation(TextStudioStyleOperationBase(styleSlot) + operationIndex);
}

int TextStudioOriginalSourceGlyph(uint styleSlot, int glyphIndex)
{
    if (TEXT_STUDIO_MAPPING_TABLE_ENABLED < .5 || styleSlot == 0 || glyphIndex < 0) return glyphIndex;
    TextStudio2DGpuMappingHeader header = TextStudioLoadMappingHeader(styleSlot);
    if (glyphIndex >= (int)round(header.metadata.y)) return glyphIndex;
    TextStudio2DGpuGlyphMapping glyph = TextStudioLoadGlyphMapping((int)round(header.metadata.x) + glyphIndex);
    return glyph.appearanceMetadata.x > .5 ? (int)round(glyph.appearanceMetadata.x) - 1 : glyphIndex;
}

bool TextStudioAppearanceBounds(uint styleSlot, int glyphIndex, out float4 bounds)
{
    bounds = 0;
    if (TEXT_STUDIO_MAPPING_TABLE_ENABLED < .5 || styleSlot == 0 || glyphIndex < 0) return false;
    TextStudio2DGpuMappingHeader header = TextStudioLoadMappingHeader(styleSlot);
    if (glyphIndex >= (int)round(header.metadata.y)) return false;
    TextStudio2DGpuGlyphMapping glyph = TextStudioLoadGlyphMapping((int)round(header.metadata.x) + glyphIndex);
    bounds = glyph.appearanceBounds;
    return glyph.appearanceMetadata.y > .5;
}

int TextStudioSourceUnit(uint styleSlot, int glyphIndex)
{
    if (TEXT_STUDIO_MAPPING_TABLE_ENABLED < .5 || styleSlot == 0 || glyphIndex < 0) return glyphIndex;
    TextStudio2DGpuMappingHeader header = TextStudioLoadMappingHeader(styleSlot);
    if (glyphIndex >= (int)round(header.metadata.y)) return glyphIndex;
    return (int)round(TextStudioLoadGlyphMapping((int)round(header.metadata.x) + glyphIndex).sourceMetadata.x);
}

// Selection is a companion of the bound source, independent of the eight-bit draw mask. A non-negative
// selectionRow addresses the glyph's first 64-operation word in the mapping rows and programs beyond 64
// operations continue in the following words; otherwise firstWord holds the only word.
float4 TextStudioApplyColorAdjustmentsCore(uint styleSlot, int operationIndex, float4 firstWord, int selectionRow, float4 straight)
{
    if (styleSlot == 0 || operationIndex <= 0)
        return straight;
    TextStudio2DGpuStyleHeader header = TextStudioLoadStyleHeader(styleSlot);
    if (((uint)round(header.metadata.w) & (1u << 19)) == 0) return straight;
    int operationBase = (int)round(header.program.x);
    float4 result = straight;
    #ifdef UNITY_COLORSPACE_GAMMA
    result.rgb = GammaToLinearSpace(result.rgb);
    #endif
    [loop]
    for (int index = operationIndex - 1; index >= 0; index--)
    {
        TextStudio2DGpuOperation adjustment = TextStudioLoadOperation(operationBase + index);
        if ((int)round(adjustment.layerMetadata.x) != 14) continue;
        float4 selection = selectionRow >= 0
            ? TextStudioLoadRange(selectionRow + (index >> 6))
            : index < 64 ? firstWord : 0;
        uint selected = (uint)round(selection[(index >> 4) & 3]);
        // Contributions beyond the 64-row mask belong to root-scope adjustments, which affect every later row.
        bool contributes = operationIndex < 64
            ? ((uint)round(adjustment.parameters8[operationIndex >> 4]) & (1u << (operationIndex & 15))) != 0
            : adjustment.parameters7.w > .5;
        if ((selected & (1u << (index & 15))) == 0 || !contributes) continue;
        float4 transformed = float4(dot(adjustment.parameters2, result), dot(adjustment.parameters3, result),
            dot(adjustment.parameters4, result), dot(adjustment.parameters5, result)) + adjustment.parameters6;
        if (adjustment.parameters7.y > .5) transformed = saturate(transformed);
        result = lerp(result, transformed, saturate(adjustment.parameters7.x * adjustment.layerMetadata.z));
    }
    #ifdef UNITY_COLORSPACE_GAMMA
    result.rgb = LinearToGammaSpace(result.rgb);
    #endif
    return result;
}

// Surface and compositor callers carry one 64-operation selection word directly.
float4 TextStudioApplyColorAdjustmentsForSelection(uint styleSlot, int operationIndex, float4 selection, float4 straight)
{
    return TextStudioApplyColorAdjustmentsCore(styleSlot, operationIndex, selection, -1, straight);
}

float4 TextStudioApplyColorAdjustments(uint styleSlot, int operationIndex, int glyphIndex, float4 straight)
{
    #ifdef TEXT_STUDIO_UNIFORM_ROW
    // Uniform rows belong to styles without colour adjustments, whose mapping rows hold no selection masks.
    return straight;
    #else
    if (styleSlot == 0 || operationIndex <= 0 || glyphIndex < 0 || TEXT_STUDIO_MAPPING_TABLE_ENABLED < .5) return straight;
    TextStudio2DGpuMappingHeader mapping = TextStudioLoadMappingHeader(styleSlot);
    int selectionStart = (int)round(mapping.metadata.z) - 1;
    if (selectionStart < 0 || glyphIndex >= (int)round(mapping.metadata.y)) return straight;
    return TextStudioApplyColorAdjustmentsCore(styleSlot, operationIndex, 0,
        selectionStart + glyphIndex * max(1, (int)round(mapping.program.y)), straight);
    #endif
}

float TextStudioStyleLineCoordinate(uint styleSlot, int operationIndex, int glyphIndex, float textCoordinateX, bool restartPerLine)
{
    if (styleSlot == 0 || glyphIndex < 0 || TEXT_STUDIO_MAPPING_TABLE_ENABLED < .5)
        return textCoordinateX;
    TextStudio2DGpuMappingHeader header = TextStudioLoadMappingHeader(styleSlot);
    int glyphCount = max(0, (int)round(header.metadata.y));
    if (glyphIndex >= glyphCount) return textCoordinateX;
    float4 sourceBounds = TextStudioLoadRange((int)round(header.metadata.w) - 1);
    float objectX = sourceBounds.x + textCoordinateX * (sourceBounds.z - sourceBounds.x);
    int glyphStart = max(0, (int)round(header.metadata.x));
    if (restartPerLine)
        return objectX - TextStudioLoadGlyphMapping(glyphStart + glyphIndex).lineBounds.x;
    float4 encoded = TextStudioLoadRange((int)round(header.program.x) + operationIndex);
    int patternStart = (int)round(header.metadata.w + encoded.z) + glyphIndex;
    float2 pattern = TextStudioLoadRange(patternStart).xy;
    return objectX * pattern.y + pattern.x;
}

void TextStudioStyleResolveMapping(
    uint styleSlot,
    int operationIndex,
    int glyphIndex,
    int mapping,
    float2 glyphLocal,
    float2 textLocal,
    float2 objectLocal,
    float2 screenLocal,
    float2 worldLocal,
    float2 glyphDx,
    float2 glyphDy,
    float2 textDx,
    float2 textDy,
    float2 objectDx,
    float2 objectDy,
    float2 screenDx,
    float2 screenDy,
    float2 worldDx,
    float2 worldDy,
    out float2 coordinate,
    out float2 coordinateDx,
    out float2 coordinateDy)
{
    coordinate = glyphLocal;
    coordinateDx = glyphDx;
    coordinateDy = glyphDy;
    UNITY_BRANCH
    if (mapping == 0)
        return;
    #ifdef TEXT_STUDIO_UNIFORM_ROW
    // Uniform rows draw texts without appearance occurrences, whose glyph rows carry no appearance bounds.
    #else
    float4 appearanceBounds;
    if ((mapping == 3 || mapping == 4 || mapping == 7) && TextStudioAppearanceBounds(styleSlot, glyphIndex, appearanceBounds))
    {
        float2 extent = max(appearanceBounds.zw - appearanceBounds.xy, float2(.0001, .0001));
        coordinate = (objectLocal - appearanceBounds.xy) / extent;
        coordinateDx = objectDx / extent;
        coordinateDy = objectDy / extent;
        return;
    }
    #endif
    if (mapping == 3)
    {
        coordinate = textLocal;
        coordinateDx = textDx;
        coordinateDy = textDy;
        return;
    }
    if (mapping == 5)
    {
        coordinate = screenLocal;
        coordinateDx = screenDx;
        coordinateDy = screenDy;
        return;
    }
    if (mapping == 6)
    {
        coordinate = worldLocal;
        coordinateDx = worldDx;
        coordinateDy = worldDy;
        return;
    }
    if (TEXT_STUDIO_MAPPING_TABLE_ENABLED < .5 || styleSlot == 0)
        return;

    TextStudio2DGpuMappingHeader header =
        TextStudioLoadMappingHeader(styleSlot);
    int glyphStart = max(0, (int)round(header.metadata.x));
    int glyphCount = max(0, (int)round(header.metadata.y));
    float4 bounds = header.objectBounds;
    if (mapping == 7 || mapping == 8)
    {
        int rangeIndex = (int)round(header.program.x) + max(0, operationIndex);
        bounds = TextStudioLoadRange(rangeIndex);
        if (mapping == 8)
        {
            if (glyphIndex < 0 || glyphIndex >= glyphCount)
                return;
            int matchIndex = (int)round(header.metadata.w + bounds.x) + glyphIndex;
            bounds = TextStudioLoadRange(matchIndex);
        }
    }
    else if (mapping != 4)
    {
        if (glyphIndex < 0 || glyphIndex >= glyphCount)
            return;
        TextStudio2DGpuGlyphMapping glyph =
            TextStudioLoadGlyphMapping(glyphStart + glyphIndex);
        bounds = mapping == 1 ? glyph.wordBounds : glyph.lineBounds;
    }

    float2 extent = bounds.zw - bounds.xy;
    extent.x = abs(extent.x) > .00001
        ? extent.x
        : (extent.x < 0 ? -.00001 : .00001);
    extent.y = abs(extent.y) > .00001
        ? extent.y
        : (extent.y < 0 ? -.00001 : .00001);
    coordinate = (objectLocal - bounds.xy) / extent;
    coordinateDx = objectDx / extent;
    coordinateDy = objectDy / extent;
}

float2 TextStudioStylePaintDelta(
    TextStudio2DGpuOperation operation,
    float2 coordinate)
{
    float2 scale = operation.paintTransform1.xy;
    scale.x = abs(scale.x) > .00001 ? scale.x : (scale.x < 0 ? -.00001 : .00001);
    scale.y = abs(scale.y) > .00001 ? scale.y : (scale.y < 0 ? -.00001 : .00001);
    float2 center = operation.paintTransform0.yz;
    float2 offset = operation.paintTransform1.zw;
    return (coordinate - center - offset) / scale;
}

fixed4 TextStudioStyleGradientColor(
    uint styleSlot,
    TextStudio2DGpuOperation operation,
    half coordinate)
{
    #ifdef TEXT_STUDIO_UNIFORM_ROW
    // The table scan below over the row's own stops, which the material holds from stop zero.
    int uniformStopCount = clamp((int)round(operation.paintMetadata.w), 0, TEXT_STUDIO_UNIFORM_ROW_STOPS);
    if (uniformStopCount <= 0)
        return operation.color;
    float previousPosition = _TextStudioHeroRowStopPositions0.x;
    float4 previousColor = _TextStudioHeroRowStopColor0;
    if (uniformStopCount == 1 || coordinate <= previousPosition)
        return previousColor * operation.color;
    // HLSL evaluates both operands of ||, so each step returns on its own.
    float4 stopColor;
    if (TextStudioUniformRowGradientStep(1, uniformStopCount, coordinate, _TextStudioHeroRowStopPositions0.y,
            _TextStudioHeroRowStopColor1, previousPosition, previousColor, stopColor))
        return stopColor * operation.color;
    if (TextStudioUniformRowGradientStep(2, uniformStopCount, coordinate, _TextStudioHeroRowStopPositions0.z,
            _TextStudioHeroRowStopColor2, previousPosition, previousColor, stopColor))
        return stopColor * operation.color;
    if (TextStudioUniformRowGradientStep(3, uniformStopCount, coordinate, _TextStudioHeroRowStopPositions0.w,
            _TextStudioHeroRowStopColor3, previousPosition, previousColor, stopColor))
        return stopColor * operation.color;
    if (TextStudioUniformRowGradientStep(4, uniformStopCount, coordinate, _TextStudioHeroRowStopPositions1.x,
            _TextStudioHeroRowStopColor4, previousPosition, previousColor, stopColor))
        return stopColor * operation.color;
    if (TextStudioUniformRowGradientStep(5, uniformStopCount, coordinate, _TextStudioHeroRowStopPositions1.y,
            _TextStudioHeroRowStopColor5, previousPosition, previousColor, stopColor))
        return stopColor * operation.color;
    if (TextStudioUniformRowGradientStep(6, uniformStopCount, coordinate, _TextStudioHeroRowStopPositions1.z,
            _TextStudioHeroRowStopColor6, previousPosition, previousColor, stopColor))
        return stopColor * operation.color;
    if (TextStudioUniformRowGradientStep(7, uniformStopCount, coordinate, _TextStudioHeroRowStopPositions1.w,
            _TextStudioHeroRowStopColor7, previousPosition, previousColor, stopColor))
        return stopColor * operation.color;
    return previousColor * operation.color;
    #else
    TextStudio2DGpuStyleHeader header = TextStudioLoadStyleHeader(styleSlot);
    int programStops = max(0, (int)round(header.metadata.z));
    int stopStart = clamp((int)round(operation.paintMetadata.z), 0, max(0, programStops - 1));
    int stopCount = clamp(
        (int)round(operation.paintMetadata.w),
        0,
        min(TEXT_STUDIO_MAX_GRADIENT_STOPS, programStops - stopStart));
    if (stopCount <= 0)
        return operation.color;

    int tableStart = (int)round(header.program.y) + stopStart;
    TextStudio2DGpuGradientStop previous =
        TextStudioLoadGradientStop(tableStart);
    if (stopCount == 1 || coordinate <= previous.metadata.x)
        return previous.color * operation.color;

    [loop]
    for (int stopIndex = 1;
         stopIndex < TEXT_STUDIO_MAX_GRADIENT_STOPS;
         stopIndex++)
    {
        if (stopIndex >= stopCount)
            break;
        TextStudio2DGpuGradientStop current =
            TextStudioLoadGradientStop(tableStart + stopIndex);
        if (coordinate <= current.metadata.x)
        {
            half denominator = max(
                .00001,
                current.metadata.x - previous.metadata.x);
            half amount = saturate(
                (coordinate - previous.metadata.x) / denominator);
            return lerp(previous.color, current.color, amount) * operation.color;
        }
        previous = current;
    }
    return previous.color * operation.color;
    #endif
}

half TextStudioStyleGradientSpread(half coordinate, half spread)
{
    if (spread < .5)
        return saturate(coordinate);
    if (spread < 1.5)
        return frac(coordinate);
    return 1.0 - abs(frac(coordinate * .5) * 2.0 - 1.0);
}

fixed4 TextStudioStyleFourCornerColor(
    uint styleSlot,
    TextStudio2DGpuOperation operation,
    float2 coordinate)
{
    #ifdef TEXT_STUDIO_UNIFORM_ROW
    // The row's own first four stops, held by the material from stop zero.
    int uniformStopCount = clamp((int)round(operation.paintMetadata.w), 0, TEXT_STUDIO_UNIFORM_ROW_STOPS);
    if (uniformStopCount < 4)
        return operation.color;
    fixed4 topLeft = _TextStudioHeroRowStopColor0;
    fixed4 topRight = _TextStudioHeroRowStopColor1;
    fixed4 bottomLeft = _TextStudioHeroRowStopColor2;
    fixed4 bottomRight = _TextStudioHeroRowStopColor3;
    #else
    TextStudio2DGpuStyleHeader header = TextStudioLoadStyleHeader(styleSlot);
    int programStops = max(0, (int)round(header.metadata.z));
    int stopStart = clamp((int)round(operation.paintMetadata.z), 0, max(0, programStops - 1));
    int stopCount = clamp(
        (int)round(operation.paintMetadata.w),
        0,
        min(TEXT_STUDIO_MAX_GRADIENT_STOPS, programStops - stopStart));
    if (stopCount < 4)
        return operation.color;

    int tableStart = (int)round(header.program.y) + stopStart;
    fixed4 topLeft = TextStudioLoadGradientStop(tableStart).color;
    fixed4 topRight = TextStudioLoadGradientStop(tableStart + 1).color;
    fixed4 bottomLeft = TextStudioLoadGradientStop(tableStart + 2).color;
    fixed4 bottomRight = TextStudioLoadGradientStop(tableStart + 3).color;
    #endif
    float2 uv = saturate(coordinate);
    fixed4 bottom = lerp(bottomLeft, bottomRight, uv.x);
    fixed4 top = lerp(topLeft, topRight, uv.x);
    return lerp(bottom, top, uv.y) * operation.color;
}

float TextStudioStyleHash12(float2 value)
{
    float3 state = frac(float3(value.xyx) * .1031);
    state += dot(state, state.yzx + 33.33);
    return frac((state.x + state.y) * state.z);
}

half TextStudioStyleValueNoise(float2 position)
{
    float2 cell = floor(position);
    float2 fraction = frac(position);
    float2 smoothFraction = fraction * fraction * fraction *
        (fraction * (fraction * 6 - 15) + 10);
    half bottom = lerp(
        TextStudioStyleHash12(cell),
        TextStudioStyleHash12(cell + float2(1, 0)),
        smoothFraction.x);
    half top = lerp(
        TextStudioStyleHash12(cell + float2(0, 1)),
        TextStudioStyleHash12(cell + float2(1, 1)),
        smoothFraction.x);
    return lerp(bottom, top, smoothFraction.y);
}

half TextStudioStyleFilteredFractal(
    float2 position,
    float2 positionDx,
    float2 positionDy,
    half detail,
    half qualityTier,
    half seedLow,
    half seedHigh)
{
    float2 seedOffset = float2(
        seedLow * .754877666,
        seedHigh * .569840296) +
        float2(seedHigh, seedLow) * .013;
    position += seedOffset;
    float2 baseDx = positionDx;
    float2 baseDy = positionDy;
    half persistence = lerp(.28, .62, saturate(detail));
    int octaveCount = qualityTier < .5
        ? 2
        : qualityTier < 1.5
            ? 3
            : 4;
    half value = 0;
    half weight = 0;
    half amplitude = 1;
    float frequency = 1;
    [loop]
    for (int octave = 0; octave < 4; octave++)
    {
        if (octave >= octaveCount)
            break;
        float footprint = max(
            length(baseDx * frequency),
            length(baseDy * frequency));
        half resolved = 1 - smoothstep(.25, 1.0, footprint);
        half sampleValue = TextStudioStyleValueNoise(position * frequency);
        value += lerp(.5, sampleValue, resolved) * amplitude;
        weight += amplitude;
        amplitude *= persistence;
        frequency *= 2.03;
    }
    half normalized = weight > .0001 ? value / weight : .5;
    half contrast = lerp(1.25, 2.25, saturate(detail));
    return saturate((normalized - .5) * contrast + .5);
}

half TextStudioStyleFilteredNoise(
    TextStudio2DGpuOperation operation,
    float2 delta,
    float2 deltaDx,
    float2 deltaDy)
{
    half noiseScale = max(.01, operation.paintTransform0.w);
    return TextStudioStyleFilteredFractal(
        delta * noiseScale,
        deltaDx * noiseScale,
        deltaDy * noiseScale,
        operation.parameters2.x,
        operation.executionMetadata.y,
        operation.parameters2.y,
        operation.parameters2.z);
}

void TextStudioStyleMakeCoordinateIsotropic(
    int mapping,
    inout float2 coordinate,
    inout float2 coordinateDx,
    inout float2 coordinateDy)
{
    if (mapping == 0 || mapping == 6)
        return;

    float horizontalFootprint = length(float2(coordinateDx.x, coordinateDy.x));
    float verticalFootprint = length(float2(coordinateDx.y, coordinateDy.y));
    float aspect = clamp(
        verticalFootprint / max(horizontalFootprint, .000001),
        .125,
        64.0);
    coordinate.x *= aspect;
    coordinateDx.x *= aspect;
    coordinateDy.x *= aspect;
}

fixed4 TextStudioEvaluateStylePaintWithDerivatives(
    uint styleSlot,
    TextStudio2DGpuOperation operation,
    int mapping,
    float2 mappingCoordinate,
    half distanceCoordinate,
    float2 coordinateDx,
    float2 coordinateDy)
{
    fixed4 result = 0;
    int paintKind = (int)round(operation.paintMetadata.x);
    half coordinate = 0;
    half usesGradient = 0;
    UNITY_BRANCH
    if (paintKind == 0)
    {
        result = operation.color;
    }
    else if (paintKind == 8)
    {
        half rangeStart = operation.parameters2.y;
        half rangeEnd = operation.parameters2.z;
        half rangeSpan = rangeEnd - rangeStart;
        coordinate = abs(rangeSpan) > .00001
            ? (distanceCoordinate - rangeStart) / rangeSpan
            : 0;
        coordinate = TextStudioStyleGradientSpread(
            coordinate,
            operation.paintTransform0.w);
        usesGradient = 1;
    }
    else
    {
        float2 delta = TextStudioStylePaintDelta(
            operation,
            mappingCoordinate);
        // Uniform Hero rows admit no texture, pattern or noise paint (TextStudio2DHeroMaterials.SupportsUniformRow).
        #if !defined(TEXT_STUDIO_APPEARANCE_BASIC) && !defined(TEXT_STUDIO_UNIFORM_ROW)
        if (paintKind == 6)
        {
            TextStudioStyleMakeCoordinateIsotropic(
                mapping,
                delta,
                coordinateDx,
                coordinateDy);
        }
        #endif
        float2 paintScale = operation.paintTransform1.xy;
        paintScale.x = abs(paintScale.x) > .00001
            ? paintScale.x
            : (paintScale.x < 0 ? -.00001 : .00001);
        paintScale.y = abs(paintScale.y) > .00001
            ? paintScale.y
            : (paintScale.y < 0 ? -.00001 : .00001);
        float2 deltaDx = coordinateDx / paintScale;
        float2 deltaDy = coordinateDy / paintScale;
        #if !defined(TEXT_STUDIO_APPEARANCE_BASIC) && !defined(TEXT_STUDIO_UNIFORM_ROW)
        if (paintKind == 4 || paintKind == 5)
        {
            if (_TextStudio2DPaintTexturesEnabled > .5)
            {
                float2 textureUv = delta + .5;
                int fit = (int)round(operation.paintTransform0.w);
                half fitMask = 1;
                if (fit == 3)
                {
                    textureUv = frac(textureUv);
                }
                else if (fit == 1 || fit == 2)
                {
                    float horizontalFootprint = length(float2(
                        coordinateDx.x,
                        coordinateDy.x));
                    float verticalFootprint = length(float2(
                        coordinateDx.y,
                        coordinateDy.y));
                    float destinationAspect = clamp(
                        verticalFootprint / max(horizontalFootprint, .000001),
                        .015625,
                        64.0);
                    float sourceAspect = clamp(
                        operation.parameters2.w,
                        .015625,
                        64.0);
                    if (fit == 1)
                    {
                        float2 contentScale = destinationAspect > sourceAspect
                            ? float2(sourceAspect / destinationAspect, 1)
                            : float2(1, destinationAspect / sourceAspect);
                        textureUv = (textureUv - .5) /
                            max(contentScale, .00001) + .5;
                        deltaDx /= max(contentScale, .00001);
                        deltaDy /= max(contentScale, .00001);
                        fitMask =
                            step(0, textureUv.x) * step(textureUv.x, 1) *
                            step(0, textureUv.y) * step(textureUv.y, 1);
                    }
                    else
                    {
                        float2 cropScale = destinationAspect > sourceAspect
                            ? float2(1, sourceAspect / destinationAspect)
                            : float2(destinationAspect / sourceAspect, 1);
                        textureUv = (textureUv - .5) * cropScale + .5;
                        deltaDx *= cropScale;
                        deltaDy *= cropScale;
                    }
                }
                else
                {
                    textureUv = saturate(textureUv);
                }
                int textureSlice = clamp(
                    (int)round(operation.paintMetadata.z),
                    0,
                    63);
                float footprint = max(
                    length(deltaDx * 512.0),
                    length(deltaDy * 512.0));
                float lod = max(0, log2(max(1, footprint)));
                result = UNITY_SAMPLE_TEX2DARRAY_LOD(
                    _TextStudio2DPaintTextures,
                    float3(textureUv, textureSlice),
                    lod) * operation.color * fitMask;
            }
        }
        else if (paintKind == 6)
        {
            half noise = TextStudioStyleFilteredNoise(
                operation,
                delta,
                deltaDx,
                deltaDy);
            result = fixed4(
                operation.color.rgb,
                operation.color.a * noise);
        }
        else
        #endif
        if (paintKind == 7)
        {
            result = TextStudioStyleFourCornerColor(
                styleSlot,
                operation,
                delta + .5);
        }
        else
        {
            if (paintKind == 1)
            {
                half radians =
                    operation.paintTransform0.x * .0174532925199433;
                float2 direction = float2(cos(radians), sin(radians));
                coordinate = dot(delta, direction) + .5;
            }
            else if (paintKind == 2)
            {
                coordinate = length(delta) * 2.0;
            }
            else
            {
                half angle = atan2(delta.y, delta.x) /
                    6.283185307179586;
                coordinate = frac(
                    angle - operation.paintTransform0.x / 360.0 + 1.0);
            }
            if (paintKind != 3)
            {
                coordinate = TextStudioStyleGradientSpread(
                    coordinate,
                    operation.paintTransform0.w);
            }
            usesGradient = 1;
        }
    }

    if (usesGradient > .5)
        result = TextStudioStyleGradientColor(styleSlot, operation, coordinate);
    return result;
}

struct TextStudioPaintDerivatives
{
    float2 glyphDx, glyphDy;
    float2 textDx, textDy;
    float2 objectDx, objectDy;
    float2 screenDx, screenDy;
    float2 worldDx, worldDy;
};

TextStudioPaintDerivatives TextStudioBuildPaintDerivatives(
    float2 glyphLocal, float2 textLocal, float2 objectLocal, float2 screenLocal, float2 worldLocal)
{
    TextStudioPaintDerivatives derivatives;
    derivatives.glyphDx = ddx(glyphLocal);
    derivatives.glyphDy = ddy(glyphLocal);
    derivatives.textDx = ddx(textLocal);
    derivatives.textDy = ddy(textLocal);
    derivatives.objectDx = ddx(objectLocal);
    derivatives.objectDy = ddy(objectLocal);
    derivatives.screenDx = ddx(screenLocal);
    derivatives.screenDy = ddy(screenLocal);
    derivatives.worldDx = ddx(worldLocal);
    derivatives.worldDy = ddy(worldLocal);
    return derivatives;
}

fixed4 TextStudioEvaluateStylePaint(
    uint styleSlot,
    int operationIndex,
    int glyphIndex,
    TextStudio2DGpuOperation operation,
    float2 glyphLocal,
    float2 textLocal,
    float2 objectLocal,
    float2 screenLocal,
    float2 worldLocal,
    half distanceCoordinate,
    TextStudioPaintDerivatives derivatives)
{
    int mapping = (int)round(operation.paintMetadata.y);
    float2 mappingCoordinate = glyphLocal;
    float2 coordinateDx = derivatives.glyphDx;
    float2 coordinateDy = derivatives.glyphDy;
    int paintKind = (int)round(operation.paintMetadata.x);
    UNITY_BRANCH
    if (paintKind != 0 && paintKind != 8)
    {
        TextStudioStyleResolveMapping(
            styleSlot,
            operationIndex,
            glyphIndex,
            mapping,
            glyphLocal,
            textLocal,
            objectLocal,
            screenLocal,
            worldLocal,
            derivatives.glyphDx,
            derivatives.glyphDy,
            derivatives.textDx,
            derivatives.textDy,
            derivatives.objectDx,
            derivatives.objectDy,
            derivatives.screenDx,
            derivatives.screenDy,
            derivatives.worldDx,
            derivatives.worldDy,
            mappingCoordinate,
            coordinateDx,
            coordinateDy);
    }
    return TextStudioEvaluateStylePaintWithDerivatives(
        styleSlot,
        operation,
        mapping,
        mappingCoordinate,
        distanceCoordinate,
        coordinateDx,
        coordinateDy);
}

fixed4 TextStudioEvaluateStylePaint(
    uint styleSlot, int operationIndex, int glyphIndex, TextStudio2DGpuOperation operation,
    float2 glyphLocal, float2 textLocal, float2 objectLocal, float2 screenLocal, float2 worldLocal,
    half distanceCoordinate)
{
    TextStudioPaintDerivatives derivatives = TextStudioBuildPaintDerivatives(
        glyphLocal, textLocal, objectLocal, screenLocal, worldLocal);
    return TextStudioEvaluateStylePaint(styleSlot, operationIndex, glyphIndex, operation,
        glyphLocal, textLocal, objectLocal, screenLocal, worldLocal, distanceCoordinate, derivatives);
}

#endif
