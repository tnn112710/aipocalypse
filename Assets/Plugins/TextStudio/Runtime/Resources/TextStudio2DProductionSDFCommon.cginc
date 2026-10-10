#ifndef TEXT_STUDIO_2D_PRODUCTION_SDF_COMMON_INCLUDED
#define TEXT_STUDIO_2D_PRODUCTION_SDF_COMMON_INCLUDED

#include "UnityCG.cginc"
#include "UnityUI.cginc"
#include "TextStudio2DVisualPrimitiveCommon.cginc"

// The including shaders declare TMP and Canvas compatibility keywords with dynamic_branch. Keep their feature checks as runtime if statements so a source edit does not restore the former static variant multiplication.

sampler2D _MainTex;
#ifdef TEXT_STUDIO_SINGLE_STYLE_OPERATION
float _TextStudioColorSourceMode;
#endif
sampler2D _FaceTex;
sampler2D _OutlineTex;
// Every material property lives in one UnityPerMaterial buffer and engine values in UnityPerDraw, so generated
// world renderers stay SRP Batcher compatible; per-text data travels through the shared tables, never property blocks.
CBUFFER_START(UnityPerMaterial)
float4 _FaceTex_ST;
float4 _OutlineTex_ST;
fixed4 _FaceColor;
float _FaceUVSpeedX;
float _FaceUVSpeedY;
float _FaceDilate;
fixed4 _OutlineColor;
float _OutlineUVSpeedX;
float _OutlineUVSpeedY;
float _OutlineWidth;
float _OutlineSoftness;
float _TextStudioNativeMaterialModel;
fixed4 _UnderlayColor;
float _UnderlayOffsetX;
float _UnderlayOffsetY;
float _UnderlayDilate;
float _UnderlaySoftness;
float _WeightNormal;
float _WeightBold;
float _ScaleRatioA;
float _ScaleRatioC;
float _TextureWidth;
float _TextureHeight;
float _GradientScale;
float _TextStudioFieldFormat;
float _TextStudioFieldPixelsPerEm;
float _TextStudioFieldSafeDistance;
float _ScaleX;
float _ScaleY;
float _PerspectiveFilter;
float _Sharpness;
float _VertexOffsetX;
float _VertexOffsetY;
float4 _ClipRect;
float _MaskSoftnessX;
float _MaskSoftnessY;
fixed4 _TextStudioGlowColor;
fixed4 _TextStudioSweepColor;
half _TextStudioSweepIntensity;
half _TextStudioChromaticIntensity;
half _TextStudioPayloadEnabled;
half _TextStudioPayloadFromTangent;
float4 _TextStudioSourceBounds;
float _TextStudioWritingSourceEnabled;
CBUFFER_END
// Canvas and compositor values, set outside material properties.
float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
int _UIVertexColorAlwaysGammaSpace;
#include "TextStudioWritingSourceCoverage.cginc"
float _TextStudio2DHeroTime;
int _TextStudio2DHeroMotionPolicy;

#ifdef TEXT_STUDIO_STYLE_TABLE
#include "TextStudio2DStylePaintCommon.cginc"
#include "TextStudioPreparedFieldCommon.cginc"
#include "TextStudio2DVisualModifierCommon.cginc"
#include "TextStudio2DAdaptiveDepthCommon.cginc"
#endif

struct vertex_t
{
    UNITY_VERTEX_INPUT_INSTANCE_ID
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    fixed4 color : COLOR;
    float4 texcoord0 : TEXCOORD0;
    float4 texcoord1 : TEXCOORD1;
    float4 textStudio0 : TEXCOORD2;
    float4 textStudio1 : TEXCOORD3;
    float4 textStudio2 : TEXCOORD4;
    float4 tangent : TANGENT;
};

struct pixel_t
{
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
    float4 vertex : SV_POSITION;
    fixed4 faceColor : COLOR;
    fixed4 outlineColor : COLOR1;
    float4 texcoord0 : TEXCOORD0;
    half4 param : TEXCOORD1;
    float4 mask : TEXCOORD2;
    float4 texcoord1 : TEXCOORD3;
    half2 underlayParam : TEXCOORD4;
    #ifdef TEXT_STUDIO_FULL_PRECISION_PAYLOAD
    float4 textStudio0 : TEXCOORD5;
    float4 textStudio1 : TEXCOORD6;
    #else
    half4 textStudio0 : TEXCOORD5;
    half4 textStudio1 : TEXCOORD6;
    #endif
    float4 textStudioLocal : TEXCOORD7;
    #ifdef TEXT_STUDIO_SEPARATE_SELECTION_PAYLOAD
    nointerpolation float4 textStudioSelection : TEXCOORD8;
    nointerpolation float2 textStudioGlyphPayload : TEXCOORD15;
    #endif
    // w carries the exact style identity from uv0.z, apart from continuous glyph-local coordinates.
    nointerpolation float4 textStudioWritingAndSquiggle : TEXCOORD9;
    #ifdef TEXT_STUDIO_STYLE_TABLE
    float4 textStudioObjectWorld : TEXCOORD10;
    float4 textStudioScreen : TEXCOORD11;
    #if defined(TEXT_STUDIO_TEXTURE_TRANSPORT)
    // OpenGL ES 3.0 and WebGL 2 guarantee 15 varyings: the fragment fetches the prepared record by exact glyph index.
    // An external glyph's atlas transform is read in the vertex stage, keeping its table off the 16 fragment texture units.
    #ifndef TEXT_STUDIO_SEPARATE_SELECTION_PAYLOAD
    nointerpolation float preparedGlyph : TEXCOORD12;
    nointerpolation float4 preparedExternal : TEXCOORD13;
    #endif
    #else
    nointerpolation float4 preparedTransform : TEXCOORD12;
    nointerpolation float4 preparedBounds : TEXCOORD13;
    nointerpolation float4 preparedDecode : TEXCOORD14;
    #endif
    #endif
};

// Matches TextStudio2DStyleIdentity: 10 slot bits, 8 operation bits of one fused span, 6 span bits. The value is
// exact in float and repeated at every quad vertex, so rounding removes interpolation error.
void TextStudioDecodeStyleIdentity(
    float identity,
    out uint styleSlot,
    out uint operationMask,
    out int operationSpanBase)
{
    uint packed = (uint)round(max(0.0, identity));
    styleSlot = packed & 1023u;
    operationMask = (packed >> 10) & 255u;
    operationSpanBase = (int)(packed >> 18) << 3;
}

// Producers clamp the pixel-cell portion to 0..64 inside a 128-wide glyph stride. Put the decoding boundary in the
// unused gap so Canvas's floating-point normal/tangent transform cannot move a zero-cell glyph into the preceding row.
int TextStudioPackedGlyphIndex(float value)
{
    return (int)floor((abs(value) + 32.0) / 128.0) - 1;
}

float TextStudioPackedPixelCellSize(float value, int glyphIndex)
{
    float packed = abs(value);
    return glyphIndex >= 0 ? max(0.0, packed - (glyphIndex + 1) * 128.0) : packed;
}

// Matches TextStudio2DPrimitiveSupport.IsIsolatedOperation: bevel, faux depth, material response, graphic and
// animated effects. They always receive a single-operation primitive, so the fused loop compiles without them.
bool TextStudioIsIsolatedOperationKind(int kind)
{
    return kind == 6 || kind == 7 || kind == 11 || kind == 12 || kind == 13;
}

#ifdef TEXT_STUDIO_STYLE_TABLE
TextStudioPreparedField TextStudioPreparedForPixel(pixel_t input)
{
    #if defined(TEXT_STUDIO_TEXTURE_TRANSPORT)
    #ifdef TEXT_STUDIO_SINGLE_STYLE_OPERATION
    uint slot = (uint)max(0, _TextStudio2DHeroStyleSlot);
    float packedGlyph = abs(input.textStudioGlyphPayload.x);
    int glyph = TextStudioPackedGlyphIndex(packedGlyph);
    // Hero layers have no varying left for the external transform; they read it here instead.
    TextStudioPreparedField row = TextStudioGetPreparedField(slot, glyph);
    return TextStudioResolveExternalField(row, TextStudioLoadExternalFieldTransform(row));
    #else
    uint slot = (uint)round(max(0.0, input.textStudioWritingAndSquiggle.w * saturate(_TextStudioPayloadEnabled))) & 1023u;
    int glyph = (int)round(input.preparedGlyph);
    return TextStudioResolveExternalField(TextStudioGetPreparedField(slot, glyph), input.preparedExternal);
    #endif
    #else
    TextStudioPreparedField source = (TextStudioPreparedField)0;
    source.transform = input.preparedTransform;
    source.bounds = input.preparedBounds;
    source.decode = input.preparedDecode;
    return source;
    #endif
}
#endif

#include "TextStudio2DNoiseCommon.cginc"
float2 TextStudioSquiggleUv(pixel_t input, float2 uv);

float4 TextStudioReadField(pixel_t input, float2 uv, float2 uvDx, float2 uvDy)
{
    float4 field = 0;
    #ifdef TEXT_STUDIO_SINGLE_STYLE_OPERATION
    if (_TextStudioColorSourceMode > 2.5)
    {
        float2 size = float2(_TextureWidth, _TextureHeight);
        float2 safeUv = clamp(uv, .5 / size, 1 - .5 / size);
        float distance = tex2Dgrad(_MainTex, safeUv, uvDx, uvDy).r - length((uv - safeUv) * size) / max(.0001, 2 * _GradientScale);
        field = float4(0, 0, 0, distance);
    }
    else
    #endif
    {
    #ifdef TEXT_STUDIO_STYLE_TABLE
    TextStudioPreparedField source = TextStudioPreparedForPixel(input);
    UNITY_BRANCH
    if (source.decode.x > 0) field = TextStudioSamplePreparedField(source, uv);
    else field = tex2Dgrad(_MainTex, uv, uvDx, uvDy);
    #else
    field = tex2Dgrad(_MainTex, uv, uvDx, uvDy);
    #endif
    }
    return field;
}

bool TextStudioHasMultiField(pixel_t input)
{
    #ifdef TEXT_STUDIO_SINGLE_STYLE_OPERATION
    if (_TextStudioColorSourceMode > .5) return false;
    #endif
    #ifdef TEXT_STUDIO_STYLE_TABLE
    if (TextStudioPreparedForPixel(input).decode.x > 0) return true;
    #endif
    return _TextStudioFieldFormat > .5;
}

struct TextStudioPixelSamples
{
    float2 uv0;
    float2 uv1;
    float2 uv2;
    float2 uv3;
    float2 uvDx;
    float2 uvDy;
    float2 centerLocal;
    half preserve;
};

struct TextStudioStyleDerivatives
{
    float2 localDx;
    float2 localDy;
    float2 atlasDx;
    float2 atlasDy;
    float2 glyphDx;
    float2 glyphDy;
    float2 textDx;
    float2 textDy;
    float2 objectDx;
    float2 objectDy;
    float2 screenDx;
    float2 screenDy;
    float2 worldDx;
    float2 worldDy;
};

bool TextStudioUsesNativeMaterial()
{
    #ifdef TEXT_STUDIO_SINGLE_STYLE_OPERATION
    if (TEXT_STUDIO_STYLE_TABLE_ENABLED < .5 || _TextStudio2DHeroStyleSlot <= 0 || _TextStudio2DHeroOperationIndex < 0) return false;
    uint slot = (uint)_TextStudio2DHeroStyleSlot;
    TextStudio2DGpuStyleHeader header = TextStudioLoadStyleHeader(slot);
    if ((int)round(header.metadata.x) != TEXT_STUDIO_STYLE_SCHEMA_VERSION || _TextStudio2DHeroOperationIndex >= (int)round(header.metadata.y)) return false;
    TextStudio2DGpuOperation operation = TextStudioLoadOperation((int)round(header.program.x) + _TextStudio2DHeroOperationIndex);
    return (int)round(operation.layerMetadata.x) == 0 && operation.parameters1.w > .5;
    #else
    return true;
    #endif
}

pixel_t VertShader(vertex_t input)
{
    pixel_t output;
    UNITY_INITIALIZE_OUTPUT(pixel_t, output);
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float bold = step(input.texcoord0.w, 0);
    float4 vert = input.vertex;
    vert.x += _VertexOffsetX;
    vert.y += _VertexOffsetY;
    float4 vPosition = UnityObjectToClipPos(vert);
    float2 pixelSize = vPosition.w;
    pixelSize /= float2(_ScaleX, _ScaleY) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
    float scale = rsqrt(dot(pixelSize, pixelSize));
    scale *= abs(input.texcoord0.w) * _GradientScale * (_Sharpness + 1);
    if (UNITY_MATRIX_P[3][3] == 0)
    {
        scale = lerp(
            abs(scale) * (1 - _PerspectiveFilter),
            scale,
            abs(dot(
                UnityObjectToWorldNormal(float3(0, 0, input.normal.z)),
                normalize(WorldSpaceViewDir(vert)))));
    }

    float weight = lerp(_WeightNormal, _WeightBold, bold) / 4.0;
    weight = (weight + _FaceDilate) * _ScaleRatioA * .5;
    float nativeScale = scale;
    float layerScale = scale;
    scale /= 1 + (_OutlineSoftness * _ScaleRatioA * scale);
    float bias = (.5 - weight) * scale - .5;
    float outline = _OutlineWidth * _ScaleRatioA * .5 * scale;

    if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
        input.color.rgb = UIGammaToLinear(input.color.rgb);
    float opacity = input.color.a;
    UNITY_BRANCH
    if ((UNDERLAY_ON || UNDERLAY_INNER) && TextStudioUsesNativeMaterial())
        opacity = 1;
    fixed4 faceColor = fixed4(input.color.rgb, opacity) * _FaceColor;
    faceColor.rgb *= faceColor.a;
    fixed4 outlineColor = _OutlineColor;
    outlineColor.a *= opacity;
    outlineColor.rgb *= outlineColor.a;
    // Mobile TMP interpolates the premixed vertex color, including its nonlinear scale-dependent weight.
    if (_TextStudioNativeMaterialModel < 1.5)
        outlineColor = lerp(faceColor, outlineColor, sqrt(min(1, outline * 2)));

    float layerBias = 0;
    float2 layerOffset = 0;
    UNITY_BRANCH
    if ((UNDERLAY_ON || UNDERLAY_INNER) && TextStudioUsesNativeMaterial())
    {
        layerScale /= 1 + ((_UnderlaySoftness * _ScaleRatioC) * layerScale);
        layerBias = (.5 - weight) * layerScale - .5 -
            ((_UnderlayDilate * _ScaleRatioC) * .5 * layerScale);
        layerOffset = float2(
            -(_UnderlayOffsetX * _ScaleRatioC) * _GradientScale / _TextureWidth,
            -(_UnderlayOffsetY * _ScaleRatioC) * _GradientScale / _TextureHeight);
    }

    float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
    output.vertex = vPosition;
    output.faceColor = faceColor;
    output.outlineColor = outlineColor;
    output.texcoord0 = float4(input.texcoord0.xy, input.texcoord1.xy);
    // Full TMP interpolates its unscaled bias and raw scale before applying outline softness.
    // The fragment restores param.w to the raw/soft scale ratio used by inner underlay.
    output.param = half4(scale, bias - outline, bias + outline,
        _TextStudioNativeMaterialModel > 1.5 ? nativeScale : nativeScale / max(.0001, scale));
    // Expanded quads extrapolate source coordinates. Clamp after interpolation in the fragment stage.
    output.mask = float4(
        input.normal.xy,
        vert.xy * 2 - clampedRect.xy - clampedRect.zw);
    output.texcoord1 = float4(input.texcoord0.xy + layerOffset, input.color.a,
        (.5 - weight) + .5 / max(.0001, nativeScale));
    output.underlayParam = half2(layerScale, layerBias);
    output.textStudio0 = input.textStudio0;
    output.textStudio1 = input.textStudio1;
    output.textStudioLocal = lerp(
        input.textStudio2,
        input.tangent,
        step(.5, _TextStudioPayloadFromTangent));
    #ifdef TEXT_STUDIO_SEPARATE_SELECTION_PAYLOAD
    output.textStudioSelection = input.textStudio2;
    output.textStudioGlyphPayload = output.textStudioLocal.zw;
    #endif
    float writingNormal = abs(input.normal.z);
    output.textStudioWritingAndSquiggle.x = writingNormal >= 1.5
        ? round(writingNormal) - 1.0
        : 0.0;
    output.textStudioWritingAndSquiggle.yz = input.texcoord1.zw;
    output.textStudioWritingAndSquiggle.w = _TextStudioPayloadEnabled > .5 ? input.texcoord0.z : 0;
    #ifdef TEXT_STUDIO_STYLE_TABLE
    output.textStudioObjectWorld = float4(
        vert.xy,
        mul(unity_ObjectToWorld, vert).xy);
    output.textStudioScreen = ComputeScreenPos(vPosition);
    #ifdef TEXT_STUDIO_SINGLE_STYLE_OPERATION
    uint preparedStyleSlot = (uint)max(0, _TextStudio2DHeroStyleSlot);
    #else
    uint preparedStyleSlot;
    uint preparedOperationMask;
    int preparedSpanBase;
    TextStudioDecodeStyleIdentity(output.textStudioWritingAndSquiggle.w, preparedStyleSlot, preparedOperationMask, preparedSpanBase);
    #endif
    #ifndef TEXT_STUDIO_SINGLE_STYLE_OPERATION
    // Canvas batches vertex positions in root space. Recover text-local coordinates from
    // the preserved source coordinates so word, line, object and range paints do not move.
    if (_TextStudioPayloadFromTangent > .5 && preparedStyleSlot > 0 && TEXT_STUDIO_MAPPING_TABLE_ENABLED > .5)
    {
        TextStudio2DGpuMappingHeader mappingHeader = TextStudioLoadMappingHeader(preparedStyleSlot);
        if (mappingHeader.metadata.y > 0)
        {
            float4 sourceBounds = TextStudioLoadRange((int)round(mappingHeader.metadata.w) - 1);
            output.textStudioObjectWorld.xy = lerp(sourceBounds.xy, sourceBounds.zw, input.normal.xy);
        }
    }
    #endif
    float packedGlyph = abs(output.textStudioLocal.z);
    int preparedGlyphIndex = TextStudioPackedGlyphIndex(packedGlyph);
    #if defined(TEXT_STUDIO_TEXTURE_TRANSPORT)
    #ifndef TEXT_STUDIO_SEPARATE_SELECTION_PAYLOAD
    output.preparedGlyph = preparedGlyphIndex;
    output.preparedExternal = TextStudioLoadExternalFieldTransform(TextStudioGetPreparedField(preparedStyleSlot, preparedGlyphIndex));
    #endif
    #else
    TextStudioPreparedField prepared = TextStudioGetPreparedField(preparedStyleSlot, preparedGlyphIndex);
    prepared = TextStudioResolveExternalField(prepared, TextStudioLoadExternalFieldTransform(prepared));
    output.preparedTransform = prepared.transform;
    output.preparedBounds = prepared.bounds;
    output.preparedDecode = prepared.decode;
    #endif
    #endif
    return output;
}

fixed4 TextStudioApplyWritingCoverage(fixed4 color, pixel_t input)
{
    return _TextStudioWritingSourceEnabled < .5 ? color :
        color * TextStudioWritingSourceCoverage(input.textStudioLocal.xy, input.textStudioWritingAndSquiggle.x);
}

half TextStudioFaceAlpha(half scaledDistance, half bias, half focus)
{
    half source = scaledDistance - bias;
    half softness = 1 + saturate(focus) * 5;
    return saturate((source - .5) / softness + .5);
}

float2 TextStudioScreenDeltaFromLocal(float2 deltaLocal, float2 localDx, float2 localDy, float determinant)
{
    float inverse = 1.0 / determinant;
    return float2(
        (localDy.y * deltaLocal.x - localDy.x * deltaLocal.y) * inverse,
        (-localDx.y * deltaLocal.x + localDx.x * deltaLocal.y) * inverse);
}

TextStudioStyleDerivatives TextStudioBuildStyleDerivatives(
    pixel_t input,
    float2 glyphLocal)
{
    TextStudioStyleDerivatives derivatives;
    derivatives.localDx = ddx(input.textStudioLocal.xy);
    derivatives.localDy = ddy(input.textStudioLocal.xy);
    derivatives.atlasDx = ddx(input.texcoord0.xy);
    derivatives.atlasDy = ddy(input.texcoord0.xy);
    derivatives.glyphDx = ddx(glyphLocal);
    derivatives.glyphDy = ddy(glyphLocal);
    float2 textLocal = saturate(input.mask.xy);
    derivatives.textDx = ddx(textLocal);
    derivatives.textDy = ddy(textLocal);
    #ifdef TEXT_STUDIO_STYLE_TABLE
    derivatives.objectDx = ddx(input.textStudioObjectWorld.xy);
    derivatives.objectDy = ddy(input.textStudioObjectWorld.xy);
    float2 screenLocal =
        input.textStudioScreen.xy / max(input.textStudioScreen.w, .00001);
    derivatives.screenDx = ddx(screenLocal);
    derivatives.screenDy = ddy(screenLocal);
    derivatives.worldDx = ddx(input.textStudioObjectWorld.zw);
    derivatives.worldDy = ddy(input.textStudioObjectWorld.zw);
    #else
    derivatives.objectDx = 0;
    derivatives.objectDy = 0;
    derivatives.screenDx = 0;
    derivatives.screenDy = 0;
    derivatives.worldDx = 0;
    derivatives.worldDy = 0;
    #endif
    return derivatives;
}

float2 TextStudioVaryingFromLocalDerivatives(
    pixel_t input,
    float2 value,
    float2 targetLocal,
    TextStudioStyleDerivatives derivatives,
    float2 valueDx,
    float2 valueDy)
{
    float2 local = input.textStudioLocal.xy;
    float determinant = derivatives.localDx.x * derivatives.localDy.y -
        derivatives.localDy.x * derivatives.localDx.y;
    if (abs(determinant) < .0000001)
        return value;

    float2 screenDelta = TextStudioScreenDeltaFromLocal(
        targetLocal - local,
        derivatives.localDx,
        derivatives.localDy,
        determinant);
    return value + valueDx * screenDelta.x + valueDy * screenDelta.y;
}

float2 TextStudioVaryingFromLocal(pixel_t input, float2 value, float2 targetLocal)
{
    float2 local = input.textStudioLocal.xy;
    float2 localDx = ddx(local);
    float2 localDy = ddy(local);
    float determinant = localDx.x * localDy.y - localDy.x * localDx.y;
    if (abs(determinant) < 0.0000001)
        return value;

    float2 valueDx = ddx(value);
    float2 valueDy = ddy(value);
    float2 screenDelta = TextStudioScreenDeltaFromLocal(
        targetLocal - local,
        localDx,
        localDy,
        determinant);
    return value + valueDx * screenDelta.x + valueDy * screenDelta.y;
}

float TextStudioSafeAtlasPaddingPixels()
{
    float declaredSafePixels = max(0, _TextStudioFieldPixelsPerEm) *
        max(0, _TextStudioFieldSafeDistance);
    float safePixels = declaredSafePixels > .0001
        ? declaredSafePixels
        : max(0, _GradientScale - 1.0);
    // TMP's generated mesh already carries roughly one texel beyond the tight
    // glyph rectangle. Keep that texel in the budget instead of expanding past
    // the atlas region reserved for this glyph.
    return max(0, safePixels - 1.0);
}

void TextStudioResolveSafeAtlasBounds(
    float2 atlasMinimum,
    float2 atlasMaximum,
    out float2 lower,
    out float2 upper)
{
    float2 textureSize = max(float2(_TextureWidth, _TextureHeight), 1.0);
    float2 inset = .5 / textureSize;
    float2 padding = TextStudioSafeAtlasPaddingPixels() / textureSize;
    float2 midpoint = (atlasMinimum + atlasMaximum) * .5;
    lower = min(
        max(atlasMinimum - padding + inset, inset),
        midpoint);
    upper = max(
        min(atlasMaximum + padding - inset, 1.0 - inset),
        midpoint);
}

float2 TextStudioAtlasFromLocal(pixel_t input, float2 targetLocal)
{
    float2 local = input.textStudioLocal.xy;
    float2 localDx = ddx(local);
    float2 localDy = ddy(local);
    float determinant = localDx.x * localDy.y - localDy.x * localDx.y;
    if (abs(determinant) < 0.0000001)
        return input.texcoord0.xy;

    float2 atlas = input.texcoord0.xy;
    float2 atlasDx = ddx(atlas);
    float2 atlasDy = ddy(atlas);
    float2 screenForLocalX = TextStudioScreenDeltaFromLocal(float2(1, 0), localDx, localDy, determinant);
    float2 screenForLocalY = TextStudioScreenDeltaFromLocal(float2(0, 1), localDx, localDy, determinant);
    float2 atlasAxisX = atlasDx * screenForLocalX.x + atlasDy * screenForLocalX.y;
    float2 atlasAxisY = atlasDx * screenForLocalY.x + atlasDy * screenForLocalY.y;
    float2 atlasOrigin = atlas - atlasAxisX * local.x - atlasAxisY * local.y;
    float2 sampleUv = atlasOrigin + atlasAxisX * targetLocal.x + atlasAxisY * targetLocal.y;
    #ifdef TEXT_STUDIO_STYLE_TABLE
    if (TextStudioPreparedForPixel(input).decode.x > 0) return sampleUv;
    #endif

    float2 corner0 = atlasOrigin;
    float2 corner1 = atlasOrigin + atlasAxisX;
    float2 corner2 = atlasOrigin + atlasAxisY;
    float2 corner3 = atlasOrigin + atlasAxisX + atlasAxisY;
    float2 atlasMinimum = min(min(corner0, corner1), min(corner2, corner3));
    float2 atlasMaximum = max(max(corner0, corner1), max(corner2, corner3));
    float2 lower;
    float2 upper;
    TextStudioResolveSafeAtlasBounds(
        atlasMinimum,
        atlasMaximum,
        lower,
        upper);
    return clamp(sampleUv, lower, upper);
}

float2 TextStudioClampAtlasToGlyph(pixel_t input, float2 sampleUv)
{
    #ifdef TEXT_STUDIO_STYLE_TABLE
    if (TextStudioPreparedForPixel(input).decode.x > 0) return sampleUv;
    #endif
    float2 corner0 = TextStudioVaryingFromLocal(input, input.texcoord0.xy, float2(0, 0));
    float2 corner1 = TextStudioVaryingFromLocal(input, input.texcoord0.xy, float2(1, 0));
    float2 corner2 = TextStudioVaryingFromLocal(input, input.texcoord0.xy, float2(0, 1));
    float2 corner3 = TextStudioVaryingFromLocal(input, input.texcoord0.xy, float2(1, 1));
    float2 atlasMinimum = min(min(corner0, corner1), min(corner2, corner3));
    float2 atlasMaximum = max(max(corner0, corner1), max(corner2, corner3));
    float2 lower;
    float2 upper;
    TextStudioResolveSafeAtlasBounds(
        atlasMinimum,
        atlasMaximum,
        lower,
        upper);
    return clamp(sampleUv, lower, upper);
}

float2 TextStudioSquiggleUv(pixel_t input, float2 uv)
{
    float packed = input.textStudioWritingAndSquiggle.y;
    float strength = frac(packed) * saturate(_TextStudioPayloadEnabled);
    float2 local = input.textStudioLocal.xy;
    float edge = min(min(local.x, local.y), min(1 - local.x, 1 - local.y));
    float2 result = uv;
    UNITY_BRANCH
    if (strength > .00001 && edge > 0)
    {
        float detail = floor(packed);
        float seed = input.textStudioWritingAndSquiggle.z;
        float2 position = local * detail + float2(fmod(seed, 256.0), floor(seed / 256.0));
        float2 noise = float2(TextStudioValueNoise(position), TextStudioValueNoise(position + float2(31.7, 17.3))) * 2 - 1;
        // Fade at the quad perimeter so glyph bounds and neighbouring atlas entries remain untouched.
        float2 displaced = local + noise * strength * smoothstep(0, .12, edge);
        float2 offset = TextStudioAtlasFromLocal(input, displaced) - TextStudioAtlasFromLocal(input, local);
        result = TextStudioClampAtlasToGlyph(input, uv + offset);
    }
    return result;
}

float2 TextStudioClampAtlasToGlyphDerivatives(
    pixel_t input,
    float2 sampleUv,
    TextStudioStyleDerivatives derivatives)
{
    #ifdef TEXT_STUDIO_STYLE_TABLE
    if (TextStudioPreparedForPixel(input).decode.x > 0) return sampleUv;
    #endif
    float2 corner0 = TextStudioVaryingFromLocalDerivatives(
        input,
        input.texcoord0.xy,
        float2(0, 0),
        derivatives,
        derivatives.atlasDx,
        derivatives.atlasDy);
    float2 corner1 = TextStudioVaryingFromLocalDerivatives(
        input,
        input.texcoord0.xy,
        float2(1, 0),
        derivatives,
        derivatives.atlasDx,
        derivatives.atlasDy);
    float2 corner2 = TextStudioVaryingFromLocalDerivatives(
        input,
        input.texcoord0.xy,
        float2(0, 1),
        derivatives,
        derivatives.atlasDx,
        derivatives.atlasDy);
    float2 corner3 = TextStudioVaryingFromLocalDerivatives(
        input,
        input.texcoord0.xy,
        float2(1, 1),
        derivatives,
        derivatives.atlasDx,
        derivatives.atlasDy);
    float2 atlasMinimum = min(min(corner0, corner1), min(corner2, corner3));
    float2 atlasMaximum = max(max(corner0, corner1), max(corner2, corner3));
    float2 lower;
    float2 upper;
    TextStudioResolveSafeAtlasBounds(
        atlasMinimum,
        atlasMaximum,
        lower,
        upper);
    return clamp(sampleUv, lower, upper);
}

void TextStudioContinuedAtlasBounds(
    pixel_t input,
    TextStudioStyleDerivatives derivatives,
    out float2 lower,
    out float2 upper)
{
    float2 corner0 = TextStudioVaryingFromLocalDerivatives(
        input,
        input.texcoord0.xy,
        float2(0, 0),
        derivatives,
        derivatives.atlasDx,
        derivatives.atlasDy);
    float2 corner1 = TextStudioVaryingFromLocalDerivatives(
        input,
        input.texcoord0.xy,
        float2(1, 0),
        derivatives,
        derivatives.atlasDx,
        derivatives.atlasDy);
    float2 corner2 = TextStudioVaryingFromLocalDerivatives(
        input,
        input.texcoord0.xy,
        float2(0, 1),
        derivatives,
        derivatives.atlasDx,
        derivatives.atlasDy);
    float2 corner3 = corner1 + corner2 - corner0;
    float2 atlasMinimum = min(min(corner0, corner1), min(corner2, corner3));
    float2 atlasMaximum = max(max(corner0, corner1), max(corner2, corner3));
    TextStudioResolveSafeAtlasBounds(
        atlasMinimum,
        atlasMaximum,
        lower,
        upper);
}

half TextStudioSampleContinuedDistance(
    pixel_t input,
    float2 sampleUv,
    float2 sampleDx,
    float2 sampleDy,
    half fieldScale,
    half threshold,
    float2 lower,
    float2 upper)
{
    half result = 0;
    #ifdef TEXT_STUDIO_SINGLE_STYLE_OPERATION
    if (_TextStudioColorSourceMode > 2.5) result = TextStudioReadField(input, sampleUv, sampleDx, sampleDy).a * fieldScale;
    else
    #endif
    {
    #ifdef TEXT_STUDIO_STYLE_TABLE
    TextStudioPreparedField source = TextStudioPreparedForPixel(input);
    UNITY_BRANCH
    if (source.decode.x > 0) result = TextStudioSamplePreparedField(source, sampleUv).a * fieldScale;
    else
    #endif
    {
        float2 textureSize = max(float2(_TextureWidth, _TextureHeight), 1.0);
        float2 safeUv = clamp(sampleUv, lower, upper);
        half distance = tex2Dgrad(
            _MainTex,
            safeUv,
            sampleDx,
            sampleDy).a * fieldScale;
        half gradientScale = max(2 * _GradientScale, 1.0);
        half safeFieldScale = max(abs(fieldScale), .0001);
        half signedDistancePixels =
            (distance - threshold) * gradientScale / safeFieldScale;
        float2 outside = max(lower - sampleUv, 0) +
                         max(sampleUv - upper, 0);
        float outsidePixels = length(outside * textureSize);
        float2 insideFromLower = (sampleUv - lower) * textureSize;
        float2 insideFromUpper = (upper - sampleUv) * textureSize;
        float insidePixels = min(
            min(insideFromLower.x, insideFromLower.y),
            min(insideFromUpper.x, insideFromUpper.y));
        half rectangleDistancePixels = outsidePixels > .00001
            ? -outsidePixels
            : insidePixels;
        half continuedDistancePixels = min(
            signedDistancePixels - outsidePixels,
            rectangleDistancePixels);
        result = threshold +
               continuedDistancePixels * safeFieldScale / gradientScale;
    }
    }
    return result;
}

void TextStudioBuildScreenSamples(
    pixel_t input,
    float cellSize,
    half preserve,
    out TextStudioPixelSamples samples)
{
    samples = (TextStudioPixelSamples)0;
    float2 local = input.textStudioLocal.xy;
    float2 localDx = ddx(local);
    float2 localDy = ddy(local);
    float2 atlas = input.texcoord0.xy;
    float2 atlasDx = ddx(atlas);
    float2 atlasDy = ddy(atlas);
    float2 screenCenter = (floor(input.vertex.xy / cellSize) + .5) * cellSize;
    float2 screenDelta = screenCenter - input.vertex.xy;
    float2 centerLocal = local + localDx * screenDelta.x + localDy * screenDelta.y;
    centerLocal = saturate(centerLocal);
    float2 atlasCenter = atlas + atlasDx * screenDelta.x + atlasDy * screenDelta.y;
    float2 atlasOffsetX = atlasDx * (cellSize * .25);
    float2 atlasOffsetY = atlasDy * (cellSize * .25);
    samples.uv0 = TextStudioClampAtlasToGlyph(input, atlasCenter - atlasOffsetX - atlasOffsetY);
    samples.uv1 = TextStudioClampAtlasToGlyph(input, atlasCenter + atlasOffsetX - atlasOffsetY);
    samples.uv2 = TextStudioClampAtlasToGlyph(input, atlasCenter - atlasOffsetX + atlasOffsetY);
    samples.uv3 = TextStudioClampAtlasToGlyph(input, atlasCenter + atlasOffsetX + atlasOffsetY);
    samples.uvDx = atlasDx;
    samples.uvDy = atlasDy;
    samples.centerLocal = centerLocal;
    samples.preserve = preserve;
}

TextStudioPixelSamples TextStudioBuildSamples(pixel_t input, float cellSize, half glyphSpace, half preserve)
{
    cellSize = max(1.0, floor(cellSize + .5));
    TextStudioPixelSamples samples;
    samples.uv0 = 0;
    samples.uv1 = 0;
    samples.uv2 = 0;
    samples.uv3 = 0;
    samples.uvDx = 0;
    samples.uvDy = 0;
    samples.centerLocal = 0;
    samples.preserve = 0;
    if (glyphSpace < .5)
    {
        TextStudioBuildScreenSamples(input, cellSize, preserve, samples);
    }
    else
    {
        float2 local = input.textStudioLocal.xy;
        float2 localDx = ddx(local);
        float2 localDy = ddy(local);
        float determinant = localDx.x * localDy.y - localDy.x * localDx.y;
        if (abs(determinant) < .0000001)
        {
            TextStudioBuildScreenSamples(input, cellSize, preserve, samples);
        }
        else
        {
            float inverseDeterminant = 1.0 / abs(determinant);
            float pixelsPerLocalX = length(float2(localDy.y, -localDx.y)) * inverseDeterminant;
            float pixelsPerLocalY = length(float2(-localDy.x, localDx.x)) * inverseDeterminant;
            float2 localCell = float2(
                cellSize / max(pixelsPerLocalX, .0001),
                cellSize / max(pixelsPerLocalY, .0001));
            localCell = max(localCell, .0001);
            float2 centerLocal = (floor(local / localCell) + .5) * localCell;
            centerLocal = saturate(centerLocal);
            float2 quarterCell = localCell * .25;
            float2 atlas = input.texcoord0.xy;

            samples.uv0 = TextStudioAtlasFromLocal(input, saturate(centerLocal - quarterCell));
            samples.uv1 = TextStudioAtlasFromLocal(input, saturate(centerLocal + float2(quarterCell.x, -quarterCell.y)));
            samples.uv2 = TextStudioAtlasFromLocal(input, saturate(centerLocal + float2(-quarterCell.x, quarterCell.y)));
            samples.uv3 = TextStudioAtlasFromLocal(input, saturate(centerLocal + quarterCell));
            samples.uvDx = ddx(atlas);
            samples.uvDy = ddy(atlas);
            samples.centerLocal = centerLocal;
            samples.preserve = preserve;
        }
    }
    return samples;
}

fixed4 TextStudioSdfColor(
    half distance,
    fixed4 faceColor,
    fixed4 outlineColor,
    half4 parameters,
    half focus,
    half nativeSoftnessRatio)
{
    half faceAlpha = TextStudioFaceAlpha(distance, parameters.w, focus);
    fixed4 color = faceColor * faceAlpha;
    UNITY_BRANCH
    if (OUTLINE_ON)
    {
        half faceBlend = distance - parameters.z;
        // Full TMP softens coverage, while its face/outline color transition uses the raw distance.
        if (_TextStudioNativeMaterialModel > 1.5)
            faceBlend = faceBlend * nativeSoftnessRatio - .5 * (nativeSoftnessRatio - 1);
        color = lerp(outlineColor, faceColor, saturate(faceBlend));
        color *= saturate(distance - parameters.y);
    }
    return color;
}

half TextStudioAverageFaceAlpha(half4 distances, half4 parameters, half focus, half preserve)
{
    half result = TextStudioFaceAlpha(distances.x, parameters.w, focus);
    if (preserve > .5)
    {
        result += TextStudioFaceAlpha(distances.y, parameters.w, focus);
        result += TextStudioFaceAlpha(distances.z, parameters.w, focus);
        result += TextStudioFaceAlpha(distances.w, parameters.w, focus);
        result *= .25;
    }
    return result;
}

fixed4 TextStudioAverageSdfColor(
    half4 distances,
    fixed4 faceColor,
    fixed4 outlineColor,
    half4 parameters,
    half focus,
    half preserve,
    half nativeSoftnessRatio)
{
    fixed4 result = TextStudioSdfColor(distances.x, faceColor, outlineColor, parameters, focus, nativeSoftnessRatio);
    if (preserve > .5)
    {
        result += TextStudioSdfColor(distances.y, faceColor, outlineColor, parameters, focus, nativeSoftnessRatio);
        result += TextStudioSdfColor(distances.z, faceColor, outlineColor, parameters, focus, nativeSoftnessRatio);
        result += TextStudioSdfColor(distances.w, faceColor, outlineColor, parameters, focus, nativeSoftnessRatio);
        result *= .25;
    }
    return result;
}

half4 TextStudioSampleDistances(pixel_t input, TextStudioPixelSamples samples, float2 offset, half scale)
{
    half4 distances = 0;
    distances.x = TextStudioReadField(input, samples.uv0 + offset, samples.uvDx, samples.uvDy).a * scale;
    if (samples.preserve > .5)
    {
        distances.y = TextStudioReadField(input, samples.uv1 + offset, samples.uvDx, samples.uvDy).a * scale;
        distances.z = TextStudioReadField(input, samples.uv2 + offset, samples.uvDx, samples.uvDy).a * scale;
        distances.w = TextStudioReadField(input, samples.uv3 + offset, samples.uvDx, samples.uvDy).a * scale;
    }
    else
    {
        distances.yzw = distances.xxx;
    }
    return distances;
}

half TextStudioMedianDistance(half3 value)
{
    return max(min(value.r, value.g), min(max(value.r, value.g), value.b));
}

half TextStudioSampleSharpDistance(pixel_t input, float2 uv, half scale)
{
    half4 sampleValue = TextStudioReadField(input, uv, ddx(uv), ddy(uv));
    return TextStudioMedianDistance(sampleValue.rgb) * scale;
}

half4 TextStudioSampleSharpDistances(
    pixel_t input,
    TextStudioPixelSamples samples,
    float2 offset,
    half scale)
{
    half4 distances = 0;
    half4 sample0 = TextStudioReadField(
        input,
        samples.uv0 + offset,
        samples.uvDx,
        samples.uvDy);
    distances.x = TextStudioMedianDistance(sample0.rgb) * scale;
    if (samples.preserve > .5)
    {
        half4 sample1 = TextStudioReadField(
            input,
            samples.uv1 + offset,
            samples.uvDx,
            samples.uvDy);
        half4 sample2 = TextStudioReadField(
            input,
            samples.uv2 + offset,
            samples.uvDx,
            samples.uvDy);
        half4 sample3 = TextStudioReadField(
            input,
            samples.uv3 + offset,
            samples.uvDx,
            samples.uvDy);
        distances.y = TextStudioMedianDistance(sample1.rgb) * scale;
        distances.z = TextStudioMedianDistance(sample2.rgb) * scale;
        distances.w = TextStudioMedianDistance(sample3.rgb) * scale;
    }
    else
    {
        distances.yzw = distances.xxx;
    }
    return distances;
}

#include "TextStudio2DNoiseCommon.cginc"

half TextStudioTextNoise(pixel_t input)
{
    float2 boundsSize = _TextStudioSourceBounds.zw - _TextStudioSourceBounds.xy;
    float2 safeSize = max(abs(boundsSize), .0001);
    float2 normalized = saturate(input.mask.xy);
    float aspect = clamp(safeSize.x / safeSize.y, .125, 8.0);
    float2 position = float2(normalized.x * aspect, normalized.y) * 10.0;
    half largeNoise = TextStudioValueNoise(position);
    half detailNoise = TextStudioValueNoise(position * 2.07 + float2(17.13, 9.71));
    return saturate(largeNoise * .72 + detailNoise * .28);
}

#ifdef TEXT_STUDIO_STYLE_TABLE
half TextStudioStyleFillCoverage(
    half4 distances,
    half bias,
    half preserve)
{
    half4 coverage = saturate(distances - bias);
    half result = coverage.x;
    if (preserve > .5)
        result = dot(coverage, half4(.25, .25, .25, .25));
    return result;
}

float TextStudioEmToField()
{
    return _TextStudioFieldPixelsPerEm > 0 ? _TextStudioFieldPixelsPerEm / max(.0001, 2 * _GradientScale) : 1;
}

#if defined(TEXT_STUDIO_SINGLE_STYLE_OPERATION)
float _TextStudioHeroPixelScale;
#endif

float TextStudioStyleOutputPixelScale()
{
#if defined(TEXT_STUDIO_SINGLE_STYLE_OPERATION)
    return max(.0001, _TextStudioHeroPixelScale);
#else
    return 1;
#endif
}

half TextStudioStyleResolveFieldAmount(
    half value,
    half units,
    half fieldScale)
{
    return max(0, value) * (units > .5 ? TextStudioStyleOutputPixelScale() : fieldScale * TextStudioEmToField());
}

half TextStudioStyleSoftCoverage(half distance, half softness)
{
    if (softness <= .0001)
        return saturate(distance);
    half centered = distance - .5;
    half halfWidth = .5 + softness;
    return smoothstep(-halfWidth, halfWidth, centered);
}

half TextStudioStyleFieldSoftness(half softness, half bias, half outwardAmount)
{
    // A saturated empty atlas texel must stay transparent. Its finite distance
    // cannot support a transition wider than the remaining exterior field band.
    return min(softness, max(0, bias - outwardAmount));
}

half TextStudioStyleTreatedFillCoverage(
    half4 distances,
    half4 fieldParameters,
    half4 treatment,
    half preserve)
{
    half dilation = treatment.x *
        (treatment.z > .5 ? TextStudioStyleOutputPixelScale() : fieldParameters.x * TextStudioEmToField());
    half softness = TextStudioStyleResolveFieldAmount(
        treatment.y,
        treatment.z,
        fieldParameters.x);
    softness = TextStudioStyleFieldSoftness(softness, fieldParameters.y, dilation);
    half4 coverage = half4(
        TextStudioStyleSoftCoverage(
            distances.x - fieldParameters.w + dilation,
            softness),
        TextStudioStyleSoftCoverage(
            distances.y - fieldParameters.w + dilation,
            softness),
        TextStudioStyleSoftCoverage(
            distances.z - fieldParameters.w + dilation,
            softness),
        TextStudioStyleSoftCoverage(
            distances.w - fieldParameters.w + dilation,
            softness));
    if (preserve > .5)
        return dot(coverage, half4(.25, .25, .25, .25));
    return coverage.x;
}

half TextStudioStyleStrokeCoverageSingle(
    half distance,
    half bias,
    half exteriorBand,
    half fieldScale,
    half width,
    half alignment,
    half softness,
    half units)
{
    half amount = TextStudioStyleResolveFieldAmount(width, units, fieldScale);
    half softAmount = TextStudioStyleResolveFieldAmount(
        softness,
        units,
        fieldScale);
    half outsideFraction = saturate((alignment + 1.0) * .5);
    half outsideAmount = amount * outsideFraction;
    half insideAmount = amount - outsideAmount;
    // Keep the outer antialiasing ramp inside the representable exterior field.
    outsideAmount = min(outsideAmount, max(0, exteriorBand));
    softAmount = TextStudioStyleFieldSoftness(softAmount, exteriorBand, outsideAmount);
    half signedDistance = distance - bias;
    half outer = TextStudioStyleSoftCoverage(
        signedDistance + outsideAmount,
        softAmount);
    half inner = TextStudioStyleSoftCoverage(
        signedDistance - insideAmount,
        softAmount);
    return saturate(outer - inner);
}

half TextStudioStyleStrokeCoverage(
    half4 distances,
    half4 parameters,
    half width,
    half alignment,
    half softness,
    half units,
    half preserve)
{
    half result = TextStudioStyleStrokeCoverageSingle(
        distances.x,
        parameters.w,
        parameters.y,
        parameters.x,
        width,
        alignment,
        softness,
        units);
    if (preserve > .5)
    {
        result += TextStudioStyleStrokeCoverageSingle(
            distances.y,
            parameters.w,
            parameters.y,
            parameters.x,
            width,
            alignment,
            softness,
            units);
        result += TextStudioStyleStrokeCoverageSingle(
            distances.z,
            parameters.w,
            parameters.y,
            parameters.x,
            width,
            alignment,
            softness,
            units);
        result += TextStudioStyleStrokeCoverageSingle(
            distances.w,
            parameters.w,
            parameters.y,
            parameters.x,
            width,
            alignment,
            softness,
            units);
        result *= .25;
    }
    return saturate(result);
}

half4 TextStudioStyleSampleShiftedDistances(
    pixel_t input,
    TextStudioPixelSamples samples,
    half usePixelSamples,
    float2 normalizedOffset,
    half fieldScale,
    half threshold,
    half units,
    TextStudioStyleDerivatives derivatives)
{
    float2 atlasOffset = units > .5
        ? -(derivatives.atlasDx * normalizedOffset.x +
            derivatives.atlasDy * normalizedOffset.y) * TextStudioStyleOutputPixelScale()
        : float2(
            -normalizedOffset.x * max(1, _TextStudioFieldPixelsPerEm) / max(1.0, _TextureWidth),
            -normalizedOffset.y * max(1, _TextStudioFieldPixelsPerEm) / max(1.0, _TextureHeight));
    float2 lower;
    float2 upper;
    // Glyph bounds are invariant across the shifted samples.
    TextStudioContinuedAtlasBounds(input, derivatives, lower, upper);
    // Keep the continued-field sampler in one loop body. Inlining it at every pixel sample and effect call site makes the shader optimizer scale poorly.
    bool pixel = usePixelSamples > .5;
    float2 sampleDx = pixel ? samples.uvDx : derivatives.atlasDx;
    float2 sampleDy = pixel ? samples.uvDy : derivatives.atlasDy;
    float2 sampleUv = pixel ? samples.uv0 : input.texcoord0.xy;
    int sampleCount = pixel && samples.preserve > .5 ? 4 : 1;
    half4 distances = 0;
    [loop]
    for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
    {
        if (sampleIndex == 1) sampleUv = samples.uv1;
        else if (sampleIndex == 2) sampleUv = samples.uv2;
        else if (sampleIndex == 3) sampleUv = samples.uv3;
        half sampleDistance = TextStudioSampleContinuedDistance(
            input, sampleUv + atlasOffset, sampleDx, sampleDy,
            fieldScale, threshold, lower, upper);
        if (sampleIndex == 0) distances = sampleDistance.xxxx;
        else if (sampleIndex == 1) distances.y = sampleDistance;
        else if (sampleIndex == 2) distances.z = sampleDistance;
        else distances.w = sampleDistance;
    }
    return distances;
}

half TextStudioStyleSoftCoverageSingle(
    half distance,
    half bias,
    half exteriorBand,
    half fieldScale,
    half spread,
    half blur,
    half spreadDirection,
    half units)
{
    half signedDistance = distance - bias +
        TextStudioStyleResolveFieldAmount(spread, units, fieldScale) *
        spreadDirection;
    half softness = max(
        1.0,
        TextStudioStyleResolveFieldAmount(blur, units, fieldScale));
    if (spreadDirection > 0)
        softness = min(softness, max(.0001, exteriorBand - TextStudioStyleResolveFieldAmount(spread, units, fieldScale)));
    return smoothstep(-softness, softness, signedDistance);
}

half TextStudioStyleSoftCoverage(
    half4 distances,
    half4 fieldParameters,
    half spread,
    half blur,
    half spreadDirection,
    half units,
    half preserve)
{
    half result = TextStudioStyleSoftCoverageSingle(
        distances.x,
        fieldParameters.w,
        fieldParameters.y,
        fieldParameters.x,
        spread,
        blur,
        spreadDirection,
        units);
    if (preserve > .5)
    {
        result += TextStudioStyleSoftCoverageSingle(
            distances.y,
            fieldParameters.w,
            fieldParameters.y,
            fieldParameters.x,
            spread,
            blur,
            spreadDirection,
            units);
        result += TextStudioStyleSoftCoverageSingle(
            distances.z,
            fieldParameters.w,
            fieldParameters.y,
            fieldParameters.x,
            spread,
            blur,
            spreadDirection,
            units);
        result += TextStudioStyleSoftCoverageSingle(
            distances.w,
            fieldParameters.w,
            fieldParameters.y,
            fieldParameters.x,
            spread,
            blur,
            spreadDirection,
            units);
        result *= .25;
    }
    return saturate(result);
}

half TextStudioStyleEffectCoverage(
    int layerKind,
    pixel_t input,
    TextStudioPixelSamples pixelSamples,
    half pixelActive,
    half4 distances,
    half4 fieldParameters,
    float4 operationParameters,
    float4 operationParameters1,
    half preserve,
    TextStudioStyleDerivatives derivatives)
{
    half face = TextStudioStyleFillCoverage(
        distances,
        fieldParameters.w,
        preserve);
    half coverage = 0;
    if (layerKind == 2 || layerKind == 3)
    {
        half4 shiftedDistances = TextStudioStyleSampleShiftedDistances(
            input,
            pixelSamples,
            pixelActive,
            operationParameters.xy,
            fieldParameters.x,
            fieldParameters.w,
            operationParameters1.y,
            derivatives);
        half shifted = TextStudioStyleSoftCoverage(
            shiftedDistances,
            fieldParameters,
            operationParameters.w,
            operationParameters.z,
            layerKind == 2 ? -1.0 : 1.0,
            operationParameters1.y,
            preserve);
        coverage = layerKind == 2
            ? saturate(face * (1 - shifted))
            : saturate(shifted - face);
    }
    else
    {
        half softened = TextStudioStyleSoftCoverage(
            distances,
            fieldParameters,
            operationParameters.y,
            operationParameters.x,
            layerKind == 4 ? -1.0 : 1.0,
            operationParameters1.y,
            preserve);
        coverage = layerKind == 4
            ? saturate(face * (1 - softened))
            : saturate(softened - face);
    }
    return coverage;
}

TextStudio2DDepthFieldMapping TextStudioStyleDepthFieldMapping(
    pixel_t input,
    half4 fieldParameters,
    TextStudioStyleDerivatives derivatives)
{
    TextStudio2DDepthFieldMapping mapping = (TextStudio2DDepthFieldMapping)0;
    float2 corner0 = TextStudioVaryingFromLocalDerivatives(
        input,
        input.texcoord0.xy,
        float2(0, 0),
        derivatives,
        derivatives.atlasDx,
        derivatives.atlasDy);
    float2 corner1 = TextStudioVaryingFromLocalDerivatives(
        input,
        input.texcoord0.xy,
        float2(1, 0),
        derivatives,
        derivatives.atlasDx,
        derivatives.atlasDy);
    float2 corner2 = TextStudioVaryingFromLocalDerivatives(
        input,
        input.texcoord0.xy,
        float2(0, 1),
        derivatives,
        derivatives.atlasDx,
        derivatives.atlasDy);
    mapping.atlasOrigin = corner0;
    mapping.atlasAxisX = corner1 - corner0;
    mapping.atlasAxisY = corner2 - corner0;
    mapping.atlasDx = derivatives.atlasDx;
    mapping.atlasDy = derivatives.atlasDy;
    float2 corner3 = corner1 + corner2 - corner0;
    float2 atlasMinimum = min(min(corner0, corner1), min(corner2, corner3));
    float2 atlasMaximum = max(max(corner0, corner1), max(corner2, corner3));
    TextStudioResolveSafeAtlasBounds(
        atlasMinimum,
        atlasMaximum,
        mapping.atlasLower,
        mapping.atlasUpper);
    mapping.threshold =
        (fieldParameters.w + .5) / max(fieldParameters.x, .0001);
    mapping.gradientScale = max(_GradientScale, 1.0);
    mapping.pixelsPerEm = max(1, _TextStudioFieldPixelsPerEm);
    mapping.prepared = TextStudioPreparedForPixel(input);
    return mapping;
}

half TextStudioStyleFauxDepthCoverage(
    pixel_t input,
    TextStudio2DGpuOperation operation,
    float2 glyphLocal,
    half4 fieldParameters,
    TextStudioStyleDerivatives derivatives,
    out float2 paintLocal,
    out half depthProgress,
    out half lighting)
{
    paintLocal = saturate(glyphLocal);
    depthProgress = 0;
    lighting = 1;
    TextStudio2DDepthFieldMapping mapping =
        TextStudioStyleDepthFieldMapping(
            input,
            fieldParameters,
            derivatives);
    half units = operation.parameters1.w;
    float2 offsetLocal = units > .5
        ? (derivatives.glyphDx * operation.parameters0.x +
          derivatives.glyphDy * operation.parameters0.y) * TextStudioStyleOutputPixelScale()
        : TextStudioDepthEmOffset(mapping, operation.parameters0.xy);
    float distancePixelsPerUnit = units > .5 ? TextStudioDepthAtlasPixelsPerOutputPixel(mapping) * TextStudioStyleOutputPixelScale() : mapping.pixelsPerEm;
    half softness = operation.parameters1.x * distancePixelsPerUnit;
    half silhouetteOutset = operation.parameters4.x * distancePixelsPerUnit;
    half coverage = TextStudioAdaptiveDepthCoverage(
        mapping,
        glyphLocal,
        offsetLocal,
        operation.parameters0.z * length(operation.parameters0.xy),
        operation.parameters0.w,
        softness,
        silhouetteOutset,
        operation.parameters1.y,
        operation.executionMetadata.y,
        paintLocal,
        depthProgress);
    // A subpixel sweep contributes fractional coverage instead of retaining a
    // full-strength source-edge fringe all the way down to a zero-length step.
    float determinant = derivatives.glyphDx.x * derivatives.glyphDy.y -
        derivatives.glyphDy.x * derivatives.glyphDx.y;
    if (abs(determinant) > .000000000001)
    {
        float2 screenOffset = TextStudioScreenDeltaFromLocal(
            offsetLocal, derivatives.glyphDx, derivatives.glyphDy, determinant);
        float glyphRadiusPixels = .5 * max(
            length(float2(derivatives.glyphDy.y, -derivatives.glyphDx.y)),
            length(float2(-derivatives.glyphDy.x, derivatives.glyphDx.x))) / abs(determinant);
        float taper = min(.95, operation.parameters0.z * length(operation.parameters0.xy));
        coverage *= saturate(length(screenOffset) + abs(taper) * glyphRadiusPixels);
    }
    lighting = TextStudioDepthLighting(
        mapping,
        paintLocal,
        depthProgress,
        operation.parameters3.x,
        operation.parameters3.y,
        operation.parameters3.z,
        operation.parameters3.w,
        operation.parameters2.w);
    if (operation.parameters1.z > .5)
        paintLocal = float2(.5, 1.0 - depthProgress);
    return coverage;
}

fixed4 TextStudioEvaluateStylePaintForGlyph(
    uint styleSlot,
    int operationIndex,
    int glyphIndex,
    TextStudio2DGpuOperation operation,
    pixel_t input,
    float2 glyphLocal,
    TextStudioStyleDerivatives derivatives,
    half distanceCoordinate)
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
            saturate(input.mask.xy),
            input.textStudioObjectWorld.xy,
            input.textStudioScreen.xy /
                max(input.textStudioScreen.w, .00001),
            input.textStudioObjectWorld.zw,
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

half TextStudioStyleDistanceCoordinate(
    TextStudio2DGpuOperation operation,
    int layerKind,
    half4 distances,
    half4 fieldParameters,
    half depthProgress)
{
    half signedDistance =
        (distances.x - fieldParameters.w) /
        max(fieldParameters.x * TextStudioEmToField(), .0001);
    int domain = (int)round(operation.parameters2.x);
    if (domain == 0)
    {
        domain = layerKind == 0
            ? 1
            : layerKind == 1
                ? 2
                : layerKind == 7
                    ? 6
                    : 3;
    }
    if (domain == 6)
        return depthProgress;
    if (domain == 1 || domain == 4)
        return max(0, signedDistance);
    if (domain == 2)
    {
        half strokeCenter = -operation.parameters0.y *
            max(0, operation.parameters0.x) * .5;
        return abs(signedDistance - strokeCenter);
    }
    return abs(signedDistance);
}

half2 TextStudioStyleFieldGradient(
    pixel_t input,
    TextStudioPixelSamples pixelSamples,
    half pixelActive)
{
    float2 sampleUv = pixelActive > .5
        ? pixelSamples.uv0
        : input.texcoord0.xy;
    float2 atlasDx = pixelActive > .5
        ? pixelSamples.uvDx
        : ddx(input.texcoord0.xy);
    float2 atlasDy = pixelActive > .5
        ? pixelSamples.uvDy
        : ddy(input.texcoord0.xy);
    float2 texel = 1.0 / max(float2(_TextureWidth, _TextureHeight), 1.0);
    half left = TextStudioReadField(
        input,
        TextStudioClampAtlasToGlyph(input, sampleUv - float2(texel.x, 0)),
        atlasDx,
        atlasDy).a;
    half right = TextStudioReadField(
        input,
        TextStudioClampAtlasToGlyph(input, sampleUv + float2(texel.x, 0)),
        atlasDx,
        atlasDy).a;
    half bottom = TextStudioReadField(
        input,
        TextStudioClampAtlasToGlyph(input, sampleUv - float2(0, texel.y)),
        atlasDx,
        atlasDy).a;
    half top = TextStudioReadField(
        input,
        TextStudioClampAtlasToGlyph(input, sampleUv + float2(0, texel.y)),
        atlasDx,
        atlasDy).a;
    half2 gradient = half2(right - left, top - bottom);
    half magnitude = length(gradient);
    half confidence = smoothstep(.006, .04, magnitude);
    return gradient * (confidence / max(magnitude, .0001));
}

half TextStudioStyleReliefCoverageSingle(
    half distance,
    half bias,
    half fieldScale,
    half width,
    half softness,
    half units)
{
    half inside = max(0, distance - bias);
    half fieldWidth = max(
        1.0,
        TextStudioStyleResolveFieldAmount(width, units, fieldScale));
    half fieldSoftness = max(
        .5,
        TextStudioStyleResolveFieldAmount(softness, units, fieldScale));
    half rim = 1 - smoothstep(
        max(0, fieldWidth - fieldSoftness),
        fieldWidth + fieldSoftness,
        inside);
    return saturate(distance - bias) * rim;
}

half TextStudioStyleReliefCoverage(
    half4 distances,
    half4 fieldParameters,
    half width,
    half softness,
    half units,
    half preserve)
{
    half coverage = TextStudioStyleReliefCoverageSingle(
        distances.x,
        fieldParameters.w,
        fieldParameters.x,
        width,
        softness,
        units);
    if (preserve > .5)
    {
        coverage += TextStudioStyleReliefCoverageSingle(
            distances.y,
            fieldParameters.w,
            fieldParameters.x,
            width,
            softness,
            units);
        coverage += TextStudioStyleReliefCoverageSingle(
            distances.z,
            fieldParameters.w,
            fieldParameters.x,
            width,
            softness,
            units);
        coverage += TextStudioStyleReliefCoverageSingle(
            distances.w,
            fieldParameters.w,
            fieldParameters.x,
            width,
            softness,
            units);
        coverage *= .25;
    }
    return saturate(coverage);
}

#include "TextStudio2DMaterialLightingCommon.cginc"

fixed4 TextStudioEvaluateStyleMaterialResponse(
    int layerKind,
    TextStudio2DGpuOperation operation,
    float2 glyphLocal,
    half4 distances,
    half4 fieldParameters,
    half preserve,
    half2 fieldGradient,
    TextStudioStyleDerivatives derivatives,
    fixed4 animatedTint,
    out half contributionCoverage)
{
    half faceCoverage = TextStudioStyleFillCoverage(
        distances,
        fieldParameters.w,
        preserve);
    half reliefCoverage = TextStudioStyleReliefCoverage(
        distances,
        fieldParameters,
        operation.parameters0.x,
        operation.parameters0.y,
        layerKind == 6 ? operation.parameters1.y : 0,
        preserve);
    half insideDistance = max(0, distances.x - fieldParameters.w);
    if (preserve > .5)
    {
        insideDistance += max(0, distances.y - fieldParameters.w);
        insideDistance += max(0, distances.z - fieldParameters.w);
        insideDistance += max(0, distances.w - fieldParameters.w);
        insideDistance *= .25;
    }
    half reliefWidth = max(
        1.0,
        TextStudioStyleResolveFieldAmount(
            operation.parameters0.x,
            layerKind == 6 ? operation.parameters1.y : 0,
            fieldParameters.x));
    return TextStudioEvaluateMaterialLighting(layerKind, operation, glyphLocal, faceCoverage,
        reliefCoverage, insideDistance, reliefWidth, fieldGradient,
        max(length(derivatives.glyphDx), length(derivatives.glyphDy)), animatedTint, contributionCoverage);
}

float2 TextStudioStyleRotate(float2 value, half angleDegrees)
{
    half radians = angleDegrees * .0174532925199433;
    half sineValue = sin(radians);
    half cosineValue = cos(radians);
    return float2(
        value.x * cosineValue - value.y * sineValue,
        value.x * sineValue + value.y * cosineValue);
}

half TextStudioStyleGraphicNoise(
    TextStudio2DGpuOperation operation,
    float2 coordinate,
    float2 coordinateDx,
    float2 coordinateDy)
{
    half scale = max(.01, operation.parameters0.y);
    return TextStudioStyleFilteredFractal(
        coordinate * scale,
        coordinateDx * scale,
        coordinateDy * scale,
        operation.parameters1.w,
        operation.executionMetadata.y,
        operation.parameters2.y,
        operation.parameters2.z);
}

fixed4 TextStudioStyleGraphicFinalize(
    TextStudio2DGpuOperation operation,
    fixed4 authored,
    half coverage,
    fixed4 animatedTint,
    out half contributionCoverage)
{
    half sourceAlpha = saturate(animatedTint.a);
    if (operation.layerMetadata.w > .5 && sourceAlpha > .0001)
        authored.rgb *= animatedTint.rgb / sourceAlpha;
    contributionCoverage = coverage * operation.layerMetadata.z * sourceAlpha;
    return authored;
}

fixed4 TextStudioStyleAlphaAwareColorMix(
    fixed4 first,
    fixed4 second,
    half amount)
{
    half alpha = lerp(first.a, second.a, amount);
    half3 premultiplied = lerp(
        first.rgb * first.a,
        second.rgb * second.a,
        amount);
    half inverseAlpha = alpha > .0001 ? rcp(alpha) : 0;
    return fixed4(premultiplied * inverseAlpha, alpha);
}

half TextStudioStyleHalftoneRadius(
    TextStudio2DGpuOperation operation,
    float2 mappedCoordinate,
    half fallbackRadius)
{
    float angleRadians = operation.parameters3.w * .0174532925199433;
    float2 rampDirection = float2(
        cos(angleRadians),
        sin(angleRadians));
    float rampExtent = max(
        .00001,
        .5 * (abs(rampDirection.x) + abs(rampDirection.y)));
    half rampCoordinate = saturate(
        dot(mappedCoordinate - .5, rampDirection) /
        (2.0 * rampExtent) + .5);
    half minimumRadius = saturate(operation.parameters3.y * 2.0) * .5;
    half maximumRadius = saturate(operation.parameters3.z * 2.0) * .5;
    half rampRadius = lerp(
        minimumRadius,
        maximumRadius,
        rampCoordinate);
    return lerp(
        fallbackRadius,
        rampRadius,
        step(.5, operation.parameters3.x));
}

fixed4 TextStudioEvaluateGraphicEffect(
    uint styleSlot,
    int operationIndex,
    int glyphIndex,
    TextStudio2DGpuOperation operation,
    pixel_t input,
    TextStudioPixelSamples pixelSamples,
    half pixelActive,
    float2 glyphLocal,
    half4 distances,
    half4 fieldParameters,
    half preserve,
    half2 fieldGradient,
    TextStudioStyleDerivatives derivatives,
    fixed4 animatedTint,
    out half contributionCoverage)
{
    contributionCoverage = 0;
    int effectKind = (int)round(operation.paintMetadata.y);
    int graphicMapping = (int)round(operation.paintMetadata.z);
    float2 coordinate;
    float2 coordinateDx;
    float2 coordinateDy;
    TextStudioStyleResolveMapping(
        styleSlot,
        operationIndex,
        glyphIndex,
        graphicMapping,
        glyphLocal,
        saturate(input.mask.xy),
        input.textStudioObjectWorld.xy,
        input.textStudioScreen.xy /
            max(input.textStudioScreen.w, .00001),
        input.textStudioObjectWorld.zw,
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
        coordinate,
        coordinateDx,
        coordinateDy);
    float2 mappedCoordinate = saturate(coordinate);
    float2 centered = coordinate - .5;
    // Text mapping stays continuous across the string while procedural cells retain
    // their screen-space proportions instead of stretching with the text bounds.
    TextStudioStyleMakeCoordinateIsotropic(
        graphicMapping,
        centered,
        coordinateDx,
        coordinateDy);
    coordinate = centered + .5;
    float2 rotated = TextStudioStyleRotate(
        centered,
        operation.parameters1.x);
    float2 rotatedDx = TextStudioStyleRotate(
        coordinateDx,
        operation.parameters1.x);
    float2 rotatedDy = TextStudioStyleRotate(
        coordinateDy,
        operation.parameters1.x);
    half intensity = max(0, operation.parameters0.x);
    half scale = max(.01, operation.parameters0.y);
    half width = max(0, operation.parameters0.z);
    half softness = max(0, operation.parameters0.w);
    fixed4 primary = operation.color;
    fixed4 secondary = operation.paintTransform0;
    half face = TextStudioStyleFillCoverage(
        distances,
        fieldParameters.w,
        preserve);
    fixed4 graphicResult = fixed4(0, 0, 0, 0);
    // Expensive samples resolve through one call site each; the effect branches only combine them. Every extra
    // inlined noise, relief or shifted-field sample multiplied this program's compile time.
    half graphicNoise = 0;
    UNITY_BRANCH
    if (effectKind >= 1 && effectKind <= 4)
        graphicNoise = TextStudioStyleGraphicNoise(operation, coordinate, coordinateDx, coordinateDy);
    half graphicRelief = 0;
    UNITY_BRANCH
    if (effectKind == 5 || effectKind >= 10)
        graphicRelief = TextStudioStyleReliefCoverage(distances, fieldParameters, width, softness, 0, preserve);
    // Misregistered plates: Offset uses one plate, CMYK three and RGB Split two.
    int plateCount = effectKind == 7 ? 1 : effectKind == 8 ? 3 : effectKind == 9 ? 2 : 0;
    float2 plateOffset = operation.parameters1.yz *
        (effectKind == 8 ? 1 + intensity * .35 : effectKind == 9 ? 1 + intensity * .4 : 1);
    half4 plateDistances0 = 0;
    half4 plateDistances1 = 0;
    half4 plateDistances2 = 0;
    [loop]
    for (int plate = 0; plate < plateCount; plate++)
    {
        float2 offset = plate == 0 ? plateOffset
            : effectKind == 9 ? -plateOffset
            : plate == 1 ? float2(-plateOffset.x, plateOffset.y) : float2(plateOffset.x, -plateOffset.y);
        half4 sampled = TextStudioStyleSampleShiftedDistances(
            input, pixelSamples, pixelActive, offset, fieldParameters.x, fieldParameters.w, 0, derivatives);
        if (plate == 0) plateDistances0 = sampled;
        else if (plate == 1) plateDistances1 = sampled;
        else plateDistances2 = sampled;
    }
    fixed4 graphicColor = 0;
    half graphicFace = 0;

    UNITY_BRANCH
    if (effectKind == 0)
    {
        float2 cell = frac(rotated * scale) - .5;
        half distanceToDot = length(cell);
        half radius = saturate(.16 + width * .65 + intensity * .12);
        radius = TextStudioStyleHalftoneRadius(
            operation,
            mappedCoordinate,
            radius);
        half antialias = max(
            max(length(rotatedDx * scale), length(rotatedDy * scale)),
            .001);
        half dotMask = 1 - smoothstep(
            radius - antialias,
            radius + antialias + softness * .15,
            distanceToDot);
        fixed4 color = TextStudioStyleAlphaAwareColorMix(
            secondary,
            primary,
            dotMask);
        graphicColor = color;
        graphicFace = face;
    }

    else if (effectKind == 1)
    {
        half noise = graphicNoise;
        half grain = saturate((noise - .5) * (1 + intensity * 3) + .5);
        fixed4 color = lerp(secondary, primary, grain);
        color.a *= saturate(.18 + intensity * .82);
        graphicColor = color;
        graphicFace = face;
    }

    else if (effectKind == 2)
    {
        half noise = graphicNoise;
        half directional = saturate(rotated.y + .5);
        half tone = smoothstep(
            .12,
            .88,
            lerp(directional, noise, saturate(operation.parameters1.w)));
        tone = saturate((tone - .5) * (1 + intensity) + .5);
        fixed4 color = lerp(secondary, primary, tone);
        graphicColor = color;
        graphicFace = face;
    }

    else if (effectKind == 3)
    {
        half noise = graphicNoise;
        half threshold = lerp(.2, .78, saturate(intensity * .72));
        half antialias = max(
            max(length(coordinateDx * scale), length(coordinateDy * scale)) * .15,
            .008) + softness * .08;
        half retainedInk = smoothstep(
            threshold - antialias,
            threshold + antialias,
            noise);
        fixed4 color = lerp(secondary, primary, retainedInk);
        graphicColor = color;
        graphicFace = face * retainedInk;
    }

    else if (effectKind == 4)
    {
        half noise = graphicNoise;
        half detail = saturate(operation.parameters1.w);
        half capillary = saturate((noise - .5) *
            lerp(.9, 1.45, detail) + .5);
        half variableSpread = width * lerp(.8, 1.2, capillary) *
            lerp(.82, 1.08, saturate(intensity));
        half absorbedInk = TextStudioStyleSoftCoverage(
            distances,
            fieldParameters,
            variableSpread,
            softness * .78,
            1,
            0,
            preserve);
        half haloSpread = variableSpread +
            width * lerp(.1, .2, detail);
        half featheredHalo = TextStudioStyleSoftCoverage(
            distances,
            fieldParameters,
            haloSpread,
            softness * 1.65 + width * .035,
            1,
            0,
            preserve);
        half capillaryEdge = saturate((absorbedInk - face) * 1.7);
        half absorptionHalo = saturate(
            (featheredHalo - absorbedInk) * lerp(1.6, 2.35, detail));
        half transfer = saturate(
            capillaryEdge * .58 + absorptionHalo * .38);
        fixed4 color = lerp(primary, secondary, transfer);
        color.a = lerp(
            primary.a,
            secondary.a,
            saturate(capillaryEdge + absorptionHalo));
        graphicColor = color;
        graphicFace = featheredHalo;
    }

    else if (effectKind == 5)
    {
        half relief = graphicRelief;
        half3 normal = normalize(half3(fieldGradient * (1 + intensity), 1));
        half lightAngle = operation.parameters1.x * .0174532925199433;
        half2 light = half2(cos(lightAngle), sin(lightAngle));
        half lighting = saturate(dot(normal.xy, light) * .5 + .5);
        fixed4 color = lerp(secondary, primary, lighting);
        graphicColor = color;
        graphicFace = relief * saturate(.35 + intensity);
    }

    else if (effectKind == 6)
    {
        half strip = abs(frac(rotated.x * scale + .5) - .5);
        half stripWidth = saturate(width * .5 + .06);
        half antialias = max(
            max(length(rotatedDx * scale), length(rotatedDy * scale)),
            .001) + softness * .03;
        half cutout = 1 - smoothstep(
            stripWidth - antialias,
            stripWidth + antialias,
            strip);
        half retained = saturate(1 - cutout * saturate(intensity));
        fixed4 color = lerp(secondary, primary, retained);
        graphicColor = color;
        graphicFace = face * retained;
    }

    else if (effectKind == 7)
    {
        half4 shiftedDistances = plateDistances0;
        half plate = TextStudioStyleSoftCoverage(
            shiftedDistances,
            fieldParameters,
            width,
            softness,
            1,
            0,
            preserve);
        half shiftedFace = TextStudioStyleFillCoverage(
            shiftedDistances,
            fieldParameters.w,
            preserve);
        fixed4 color = lerp(primary, secondary, shiftedFace * secondary.a);
        graphicColor = color;
        graphicFace = plate * saturate(intensity);
    }

    else if (effectKind == 8)
    {
        half cyan = TextStudioStyleFillCoverage(plateDistances0, fieldParameters.w, preserve);
        half magenta = TextStudioStyleFillCoverage(plateDistances1, fieldParameters.w, preserve);
        half yellow = TextStudioStyleFillCoverage(plateDistances2, fieldParameters.w, preserve);
        half black = face * secondary.a;
        half3 subtractive = (1 - half3(cyan, magenta, yellow) * saturate(intensity));
        half3 colorValue = lerp(1, primary.rgb, primary.a) * subtractive;
        colorValue = lerp(colorValue, secondary.rgb, black);
        half coverage = saturate(max(max(cyan, magenta), max(yellow, black)));
        graphicColor = fixed4(colorValue, max(primary.a, secondary.a));
        graphicFace = coverage;
    }

    else if (effectKind == 9)
    {
        half red = TextStudioStyleFillCoverage(plateDistances0, fieldParameters.w, preserve);
        half green = face;
        half blue = TextStudioStyleFillCoverage(plateDistances1, fieldParameters.w, preserve);
        half coverage = saturate(max(red, max(green, blue)));
        half3 separated = half3(red, green, blue) * primary.rgb;
        separated = lerp(separated, secondary.rgb * coverage, secondary.a * .25);
        half inverseCoverage = coverage > .0001 ? rcp(coverage) : 0;
        graphicColor = fixed4(separated * inverseCoverage, max(primary.a, secondary.a));
        graphicFace = coverage;
    }

    else if (effectKind == 10)
    {
        half rim = graphicRelief;
        half phase = dot(fieldGradient, half2(.61, .79)) * .22 +
            dot(rotated, half2(.73, -.41)) * scale +
            operation.parameters2.y * .00031;
        half3 spectrum = TextStudioStyleSpectrum(phase);
        fixed4 color = fixed4(
            lerp(secondary.rgb, spectrum * primary.rgb, saturate(intensity)),
            lerp(secondary.a, primary.a, saturate(intensity)));
        graphicColor = color;
        graphicFace = rim;
    }
    else
    {
        half contour = graphicRelief;
        half hatchCoordinate = abs(frac(rotated.x * scale + .5) - .5);
        half hatchWidth = max(
            max(length(rotatedDx * scale), length(rotatedDy * scale)),
            .001) + softness * .025;
        half hatch = 1 - smoothstep(
            .035 + width * .08,
            .035 + width * .08 + hatchWidth,
            hatchCoordinate);
        half lineCoverage = saturate(max(contour, face * hatch * saturate(intensity)));
        fixed4 engravedColor = lerp(secondary, primary, contour);
        graphicColor = engravedColor;
        graphicFace = lineCoverage;
    }

    graphicResult = TextStudioStyleGraphicFinalize(
        operation,
        graphicColor,
        graphicFace,
        animatedTint, contributionCoverage);
    return graphicResult;
}

half TextStudioAnimatedMotionRate(
    int motionPolicy,
    int reducedMotionBehavior,
    out half enabled)
{
    return TextStudioVisualMotionRate(
        motionPolicy,
        reducedMotionBehavior,
        enabled);
}

fixed4 TextStudioAnimatedFinalize(
    TextStudio2DGpuOperation operation,
    fixed4 authored,
    half coverage,
    fixed4 animatedTint,
    out half contributionCoverage)
{
    half sourceAlpha = saturate(animatedTint.a);
    if (operation.layerMetadata.w > .5 && sourceAlpha > .0001)
        authored.rgb *= animatedTint.rgb / sourceAlpha;
    contributionCoverage = coverage * operation.layerMetadata.z * sourceAlpha;
    return authored;
}

fixed4 TextStudioEvaluateAnimatedEffect(
    uint styleSlot,
    int operationIndex,
    int glyphIndex,
    TextStudio2DGpuOperation operation,
    pixel_t input,
    float2 glyphLocal,
    half4 distances,
    half4 fieldParameters,
    half preserve,
    TextStudioStyleDerivatives derivatives,
    fixed4 animatedTint,
    out half contributionCoverage)
{
    contributionCoverage = 0;
    int effectKind = (int)round(operation.paintMetadata.y);
    float continuousTime;
    half phaseOffset;
    half speed;
    int motionPolicy;
    int reducedMotionBehavior;
    half modifierValid;
    TextStudioResolveVisualModifier(
        styleSlot,
        operation,
        continuousTime,
        phaseOffset,
        speed,
        motionPolicy,
        reducedMotionBehavior,
        modifierValid);
    if (modifierValid < .5)
        return 0;
    half motionEnabled;
    half motionRate = TextStudioAnimatedMotionRate(
        motionPolicy,
        reducedMotionBehavior,
        motionEnabled);
    if (motionEnabled < .5)
    {
        if (effectKind == 8)
        {
            half staticFace = TextStudioStyleFillCoverage(
                distances,
                fieldParameters.w,
                preserve);
            return TextStudioAnimatedFinalize(
                operation,
                operation.color,
                staticFace,
                animatedTint, contributionCoverage);
        }
        return 0;
    }

    int mapping = (int)round(operation.paintMetadata.z);
    float2 coordinate;
    float2 coordinateDx;
    float2 coordinateDy;
    TextStudioStyleResolveMapping(
        styleSlot,
        operationIndex,
        glyphIndex,
        mapping,
        glyphLocal,
        saturate(input.mask.xy),
        input.textStudioObjectWorld.xy,
        input.textStudioScreen.xy /
            max(input.textStudioScreen.w, .00001),
        input.textStudioObjectWorld.zw,
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
        coordinate,
        coordinateDx,
        coordinateDy);
    float2 direction = operation.paintTransform1.xy;
    direction = TextStudioVisualNormalizeDirection(direction);
    float2 perpendicular = float2(-direction.y, direction.x);
    half amount = max(0, operation.parameters0.x);
    half phase = phaseOffset + continuousTime * speed * motionRate;
    half scale = max(.01, operation.parameters0.w);
    half width = max(0, operation.parameters1.x);
    half softness = max(.0005, operation.parameters1.y);
    half amplitude = max(0, operation.parameters1.z);
    half frequency = max(.01, operation.parameters1.w);
    fixed4 primary = operation.color;
    fixed4 secondary = operation.paintTransform0;
    half face = TextStudioStyleFillCoverage(
        distances,
        fieldParameters.w,
        preserve);
    half seedLow = operation.parameters2.y;
    half seedHigh = operation.parameters2.z;

    UNITY_BRANCH
    if (effectKind == 0)
    {
        half projected = dot(coordinate - .5, direction) + .5;
        half travel = frac(phase) * (1 + width * 2) - width;
        half footprint = max(
            abs(dot(coordinateDx, direction)),
            abs(dot(coordinateDy, direction)));
        half band = TextStudioVisualSweepBand(
            projected - travel,
            max(0, width * .28 - softness - footprint),
            width + softness + footprint);
        half core = pow(saturate(band), lerp(1.2, 3.5, saturate(amount * .5)));
        fixed4 color = lerp(secondary, primary, core);
        color.a *= saturate(core * amount);
        return TextStudioAnimatedFinalize(
            operation,
            color,
            face * core,
            animatedTint, contributionCoverage);
    }

    UNITY_BRANCH
    if (effectKind == 2)
    {
        half level = .5 + .36 * sin(phase * 6.28318530718);
        half wave = sin(
            dot(coordinate, perpendicular) * frequency * 6.28318530718 +
            phase * 6.28318530718) * amplitude;
        half signedFill = level + wave - coordinate.y;
        half antialias = max(
            max(length(coordinateDx), length(coordinateDy)),
            .001);
        half liquid = smoothstep(
            -softness - antialias,
            softness + antialias,
            signedFill);
        half meniscus = TextStudioVisualSweepBand(
            signedFill,
            width,
            width + softness + antialias);
        fixed4 color = lerp(secondary, primary, meniscus * saturate(amount));
        return TextStudioAnimatedFinalize(
            operation,
            color,
            face * saturate(max(liquid, meniscus)),
            animatedTint, contributionCoverage);
    }

    UNITY_BRANCH
    if (effectKind == 1 || effectKind == 3 || effectKind == 4)
    {
        float2 movingCoordinate = coordinate * scale +
            direction * phase * frequency +
            float2(seedLow, seedHigh) * .013;
        half noise = TextStudioStyleFilteredFractal(
            movingCoordinate,
            coordinateDx * scale,
            coordinateDy * scale,
            saturate(.35 + amplitude),
            operation.executionMetadata.y,
            seedLow,
            seedHigh);
        if (effectKind == 1)
        {
            half threshold = saturate(.96 - amount * .08);
            half sparkle = TextStudioVisualThresholdAbove(
                noise,
                threshold,
                softness);
            half pulse = .55 + .45 * sin(
                (noise * frequency + phase) * 6.28318530718);
            sparkle *= saturate(pulse + amplitude);
            fixed4 color = lerp(secondary, primary, sparkle);
            color.a *= saturate(sparkle * amount);
            return TextStudioAnimatedFinalize(
                operation,
                color,
                face * sparkle,
                animatedTint, contributionCoverage);
        }

        half variableWidth = width * lerp(.62, 1.28, noise) +
            amplitude * noise;
        half innerRim = TextStudioStyleReliefCoverage(
            distances,
            fieldParameters,
            variableWidth,
            softness,
            0,
            preserve);
        half outer = TextStudioStyleSoftCoverage(
            distances,
            fieldParameters,
            variableWidth,
            softness,
            1,
            0,
            preserve);
        half outerRim = saturate(outer - face);
        half pulse = effectKind == 3
            ? saturate(.42 + noise * .46 + amount * .18)
            : saturate(.52 + .48 * sin(
                (noise * frequency + phase) * 6.28318530718));
        half core = saturate(innerRim * (1.08 + amount * .38));
        half corona = outerRim * (effectKind == 3
            ? lerp(.42, 1, pulse)
            : lerp(.28, 1, pulse * pulse));
        half halo = 0;
        if (effectKind == 4)
        {
            half wideOuter = TextStudioStyleSoftCoverage(
                distances,
                fieldParameters,
                variableWidth * 1.72,
                softness * 1.35,
                1,
                0,
                preserve);
            halo = saturate(wideOuter - outer) *
                saturate(.18 + pulse * .24);
        }
        half coverage = saturate(max(core, max(corona, halo)));
        fixed4 color = lerp(
            secondary,
            primary,
            saturate(core + pulse * .48));
        color.a *= saturate(.58 + core * .42 + corona * .24);
        return TextStudioAnimatedFinalize(
            operation,
            color,
            coverage,
            animatedTint, contributionCoverage);
    }

    UNITY_BRANCH
    if (effectKind == 8)
    {
        half progress = .5 - .5 * cos(phase * 6.28318530718);
        float2 pixelCoordinate = coordinate - .5;
        TextStudioStyleMakeCoordinateIsotropic(
            mapping,
            pixelCoordinate,
            coordinateDx,
            coordinateDy);
        pixelCoordinate += .5;
        pixelCoordinate = floor(pixelCoordinate * scale) / scale;
        half pixelNoise = TextStudioStyleValueNoise(
            pixelCoordinate * scale + float2(seedLow, seedHigh));
        half antialias = max(
            max(length(coordinateDx * scale), length(coordinateDy * scale)),
            .002);
        half front = lerp(-.12, 1.12, progress);
        half signedRetention = front - pixelCoordinate.x +
            (pixelNoise - .5) * (.24 + amplitude * .55);
        half retained = smoothstep(
            -softness - antialias,
            softness + antialias,
            signedRetention);
        half edge = TextStudioVisualSweepBand(
            signedRetention,
            width,
            width + softness + antialias);
        half cellPulse = .72 + .28 * sin(
            (pixelNoise * 1.7 + phase * frequency) * 6.28318530718);
        fixed4 color = lerp(
            primary,
            secondary,
            edge * saturate(amount * cellPulse));
        return TextStudioAnimatedFinalize(
            operation,
            color,
            face * saturate(max(retained, edge)),
            animatedTint, contributionCoverage);
    }

    // Neighborhood and temporal families render a clean source layer here. The bounded Hero
    // compositor applies their spatial or history pass before ordinary premultiplied composition.
    fixed4 sourceColor = lerp(secondary, primary, saturate(amount));
    return TextStudioAnimatedFinalize(
        operation,
        sourceColor,
        face,
        animatedTint, contributionCoverage);
}

fixed4 TextStudioFinalizeCoverageOperation(
    uint styleSlot,
    int operationIndex,
    int glyphIndex,
    int layerKind,
    TextStudio2DGpuOperation operation,
    pixel_t input,
    float2 paintGlyphLocal,
    half4 distances,
    half4 fieldParameters,
    half depthProgress,
    half paintLighting,
    half coverage,
    fixed4 animatedTint,
    TextStudioStyleDerivatives derivatives,
    out half contributionCoverage)
{
    if (layerKind == 7 && operation.parameters1.z > .5)
    {
        // Depth Progress supplies a canonical face-to-endpoint paint coordinate.
        // Do not let the paint's ordinary glyph/text/object mapping replace it.
        operation.paintMetadata.y = 0;
        float2 offsetLocal = operation.parameters1.w > .5
            ? (derivatives.glyphDx * operation.parameters0.x +
              derivatives.glyphDy * operation.parameters0.y) * TextStudioStyleOutputPixelScale()
            : TextStudioDepthEmOffset(TextStudioStyleDepthFieldMapping(input, fieldParameters, derivatives), operation.parameters0.xy);
        float inverseOffsetLengthSquared = rcp(max(
            dot(offsetLocal, offsetLocal),
            .000001));
        derivatives.glyphDx = float2(
            0,
            -dot(derivatives.glyphDx, offsetLocal) *
             inverseOffsetLengthSquared);
        derivatives.glyphDy = float2(
            0,
            -dot(derivatives.glyphDy, offsetLocal) *
             inverseOffsetLengthSquared);
    }
    half distanceCoordinate = TextStudioStyleDistanceCoordinate(
        operation,
        layerKind,
        distances,
        fieldParameters,
        depthProgress);
    fixed4 paint = 1;
    #ifdef TEXT_STUDIO_SINGLE_STYLE_OPERATION
    // The isolated depth pass evaluates its progress paint after tracing. Its
    // source carries coverage and animated tint, not a second copy of paint alpha.
    if (!(_TextStudio2DHeroSourceMode == 1 &&
          layerKind == 7 && operation.parameters1.z > .5))
    #endif
    paint = TextStudioEvaluateStylePaintForGlyph(
        styleSlot,
        operationIndex,
        glyphIndex,
        operation,
        input,
        paintGlyphLocal,
        derivatives,
        distanceCoordinate);
    paint.rgb *= paintLighting;
    half sourceAlpha = saturate(animatedTint.a);
    if (operation.layerMetadata.w > .5)
    {
        half inverseAlpha = sourceAlpha > .0001
            ? rcp(sourceAlpha)
            : 0;
        paint.rgb *= animatedTint.rgb * inverseAlpha;
    }
    contributionCoverage = coverage * operation.layerMetadata.z * sourceAlpha;
    return paint;
}

fixed4 TextStudioEvaluateStyleOperation(
    uint styleSlot,
    int operationIndex,
    int glyphIndex,
    TextStudio2DGpuOperation operation,
    pixel_t input,
    TextStudioPixelSamples pixelSamples,
    half pixelActive,
    float2 glyphLocal,
    half4 distances,
    half4 sharpDistances,
    half4 fieldParameters,
    half preserve,
    fixed4 animatedTint,
    half2 fieldGradient,
    TextStudioStyleDerivatives derivatives,
    bool allowHeavy,
    out half valid)
{
    #ifdef TEXT_STUDIO_SINGLE_STYLE_OPERATION
    if (_TextStudio2DHeroSourceMode == 5) operation.layerMetadata.z = 1;
    #endif
    valid = 0;
    fixed4 result = 0;
    int layerKind = (int)round(operation.layerMetadata.x);
    // Fused multi-operation primitives never carry isolated kinds; their own single-operation primitive draws them.
    if (!allowHeavy && TextStudioIsIsolatedOperationKind(layerKind))
    {
        valid = 1;
        return 0;
    }
    if (layerKind == 14 || layerKind == 15) valid = 1;
    if (layerKind == 7 && all(operation.parameters0.xy == 0))
    {
        valid = 1;
        return 0;
    }
    half coverage = 0;
    half contributionCoverage = 0;
    half usesCoverage = 0;
    float2 paintGlyphLocal = glyphLocal;
    half depthProgress = 0;
    half paintLighting = 1;
    #ifdef TEXT_STUDIO_SINGLE_STYLE_OPERATION
    if (_TextStudio2DHeroSourceMode > 0)
    {
        valid = 1;
        usesCoverage = 1;
        // Edge softness belongs to the source distance field. Remapping the
        // swept alpha instead would lower opacity throughout the solid body.
        coverage = layerKind == 7
            ? TextStudioStyleTreatedFillCoverage(
                distances,
                fieldParameters,
                half4(
                    operation.parameters4.x,
                    operation.parameters1.x,
                    operation.parameters1.w,
                    0),
                preserve)
            : TextStudioStyleFillCoverage(
                distances,
                fieldParameters.w,
                preserve);
        half sourceExtent = max(0, operation.parameters4.x) +
                            max(0, operation.parameters1.x);
        float2 localOutset = 0;
        if (layerKind == 7)
        {
            TextStudio2DDepthFieldMapping sourceMapping = TextStudioStyleDepthFieldMapping(input, fieldParameters, derivatives);
            localOutset = operation.parameters1.w > .5
                ? sourceExtent * (abs(derivatives.glyphDx) + abs(derivatives.glyphDy)) * TextStudioStyleOutputPixelScale()
                : abs(TextStudioDepthEmOffset(sourceMapping, float2(sourceExtent, 0))) +
                  abs(TextStudioDepthEmOffset(sourceMapping, float2(0, sourceExtent)));
        }
        half glyphBoundsCoverage =
            step(-localOutset.x, glyphLocal.x) *
            step(glyphLocal.x, 1 + localOutset.x) *
            step(-localOutset.y, glyphLocal.y) *
            step(glyphLocal.y, 1 + localOutset.y);
        coverage *= glyphBoundsCoverage;
        if (_TextStudio2DHeroSourceMode == 2)
        {
            return fixed4(coverage, coverage, coverage, coverage);
        }
    }
    else
    #endif
    if (layerKind == 0)
    {
        valid = 1;
        usesCoverage = 1;
        coverage = TextStudioStyleTreatedFillCoverage(
            abs(operation.parameters0.x) < .0001 && operation.parameters0.y < .0001 ? sharpDistances : distances,
            fieldParameters,
            operation.parameters0,
            preserve);
    }
    else if (layerKind == 1)
    {
        valid = 1;
        if (operation.parameters1.z <= .5 && operation.parameters0.w < .5)
        {
            usesCoverage = 1;
            half roundCoverage = TextStudioStyleStrokeCoverage(
                distances,
                fieldParameters,
                operation.parameters0.x,
                operation.parameters0.y,
                operation.parameters0.z,
                operation.parameters1.w,
                preserve);
            coverage = roundCoverage;

        }
    }
    else if (layerKind >= 2 && layerKind <= 5)
    {
        valid = 1;
        usesCoverage = 1;
        coverage = TextStudioStyleEffectCoverage(
            layerKind,
            input,
            pixelSamples,
            pixelActive,
            distances,
            fieldParameters,
            operation.parameters0,
            operation.parameters1,
            preserve,
            derivatives);
    }
    else if (allowHeavy && (layerKind == 6 || layerKind == 11))
    {
        valid = 1;
        result = TextStudioEvaluateStyleMaterialResponse(
            layerKind,
            operation,
            glyphLocal,
            distances,
            fieldParameters,
            preserve,
            fieldGradient,
            derivatives,
            animatedTint, contributionCoverage);
    }
    else if (allowHeavy && layerKind == 12)
    {
        valid = 1;
        result = TextStudioEvaluateGraphicEffect(
            styleSlot,
            operationIndex,
            glyphIndex,
            operation,
            input,
            pixelSamples,
            pixelActive,
            glyphLocal,
            distances,
            fieldParameters,
            preserve,
            fieldGradient,
            derivatives,
            animatedTint, contributionCoverage);
    }
    else if (allowHeavy && layerKind == 13)
    {
        valid = 1;
        result = TextStudioEvaluateAnimatedEffect(
            styleSlot,
            operationIndex,
            glyphIndex,
            operation,
            input,
            glyphLocal,
            distances,
            fieldParameters,
            preserve,
            derivatives,
            animatedTint, contributionCoverage);
    }
    else if (allowHeavy && layerKind == 7)
    {
        valid = 1;
        usesCoverage = 1;
        coverage = TextStudioStyleFauxDepthCoverage(
            input,
            operation,
            glyphLocal,
            fieldParameters,
            derivatives,
            paintGlyphLocal,
            depthProgress,
            paintLighting);
    }

    if (usesCoverage > .5)
    {
        result = TextStudioFinalizeCoverageOperation(
            styleSlot,
            operationIndex,
            glyphIndex,
            layerKind,
            operation,
            input,
            paintGlyphLocal,
            distances,
            fieldParameters,
            depthProgress,
            paintLighting,
            coverage,
            animatedTint,
            derivatives, contributionCoverage);
    }

    if (contributionCoverage <= 0) return 0;
    #ifdef TEXT_STUDIO_SINGLE_STYLE_OPERATION
    if (_TextStudio2DGeometryCoveragePass > .5 || _TextStudio2DHeroSourceMode == 5) return saturate(contributionCoverage);
    if (_TextStudio2DHeroSourceMode == 3) return half4(result.rgb * contributionCoverage, contributionCoverage);
    if (_TextStudio2DHeroSourceMode == 4) return half4(result.a * contributionCoverage, 0, 0, contributionCoverage);
    #endif
    #ifdef TEXT_STUDIO_SINGLE_STYLE_OPERATION
    if (!(_TextStudio2DHeroSourceMode == 1 && layerKind == 7 && operation.parameters1.z > .5))
    #endif
    result = TextStudioApplyColorAdjustments(styleSlot, operationIndex, glyphIndex, result);
    half alpha = saturate(result.a * contributionCoverage);
    return float4(result.rgb * alpha, alpha);
}

fixed4 TextStudioEvaluateSingleStyleOperation(
    uint styleSlot,
    int operationIndex,
    int glyphIndex,
    pixel_t input,
    TextStudioPixelSamples pixelSamples,
    half pixelActive,
    float2 glyphLocal,
    half4 distances,
    half4 sharpDistances,
    half4 fieldParameters,
    half preserve,
    fixed4 animatedTint,
    out half valid)
{
    valid = 0;
    if (TEXT_STUDIO_STYLE_TABLE_ENABLED < .5 || styleSlot == 0)
        return 0;
    TextStudio2DGpuStyleHeader header =
        TextStudioLoadStyleHeader(styleSlot);
    int operationCount = max(0, (int)round(header.metadata.y));
    if ((int)round(header.metadata.x) != TEXT_STUDIO_STYLE_SCHEMA_VERSION ||
        operationIndex < 0 ||
        operationIndex >= operationCount)
    {
        return 0;
    }
    TextStudio2DGpuOperation operation =
        TextStudioLoadOperation((int)round(header.program.x) + operationIndex);
    TextStudioStyleDerivatives derivatives =
        TextStudioBuildStyleDerivatives(input, glyphLocal);
    half2 fieldGradient = 0;
    uint operationFeatures = (uint)round(operation.executionMetadata.z);
    if ((operationFeatures & (1u << 15)) != 0)
    {
        fieldGradient = TextStudioStyleFieldGradient(
            input,
            pixelSamples,
            pixelActive);
    }
    return TextStudioEvaluateStyleOperation(
        styleSlot,
        operationIndex,
        glyphIndex,
        operation,
        input,
        pixelSamples,
        pixelActive,
        glyphLocal,
        distances,
        sharpDistances,
        fieldParameters,
        preserve,
        animatedTint,
        fieldGradient,
        derivatives,
        true,
        valid);
}

#ifndef TEXT_STUDIO_SINGLE_STYLE_OPERATION
fixed4 TextStudioEvaluateStyleProgram(
    uint styleSlot,
    uint operationMask,
    int spanBase,
    int glyphIndex,
    pixel_t input,
    TextStudioPixelSamples pixelSamples,
    half pixelActive,
    float2 glyphLocal,
    half4 distances,
    half4 sharpDistances,
    half4 fieldParameters,
    half preserve,
    fixed4 animatedTint,
    out half valid)
{
    valid = 0;
    if (TEXT_STUDIO_STYLE_TABLE_ENABLED < .5 || styleSlot == 0)
        return 0;

    TextStudio2DGpuStyleHeader header =
        TextStudioLoadStyleHeader(styleSlot);
    int operationCount = min(max(0, (int)round(header.metadata.y)) - spanBase, TEXT_STUDIO_MAX_FUSED_OPERATIONS);
    if ((int)round(header.metadata.x) != TEXT_STUDIO_STYLE_SCHEMA_VERSION || operationCount <= 0)
        return 0;

    TextStudioStyleDerivatives derivatives =
        TextStudioBuildStyleDerivatives(input, glyphLocal);
    uint styleFeatures = (uint)round(header.metadata.w);
    half2 fieldGradient = 0;
    if ((styleFeatures & (1u << 15)) != 0)
    {
        fieldGradient = TextStudioStyleFieldGradient(
            input,
            pixelSamples,
            pixelActive);
    }
    int operationStart = (int)round(header.program.x) + spanBase;
    // A primitive selecting one operation evaluates it outside the loop with every family available. The loop body
    // only compiles light families: inlining isolated families there made the program several times slower to compile.
    // A power of two selects one operation; its bit index is exact in float for an eight-bit mask. Shader model 3.5
    // has no countbits or firstbitlow.
    if (operationMask != 0u && (operationMask & (operationMask - 1u)) == 0u)
    {
        int singleIndex = (int)round(log2((float)operationMask));
        if (singleIndex >= operationCount)
            return 0;
        TextStudio2DGpuOperation single = TextStudioLoadOperation(operationStart + singleIndex);
        if ((int)round(single.executionMetadata.x) != 1)
        {
            valid = 1;
            return 0;
        }
        return TextStudioEvaluateStyleOperation(
            styleSlot, spanBase + singleIndex, glyphIndex, single, input, pixelSamples, pixelActive, glyphLocal, distances,
            sharpDistances, fieldParameters, preserve, animatedTint, fieldGradient, derivatives, true, valid);
    }
    fixed4 result = 0;
    [loop]
    for (int operationIndex = 0;
         operationIndex < TEXT_STUDIO_MAX_FUSED_OPERATIONS;
         operationIndex++)
    {
        if (operationIndex >= operationCount ||
            (operationMask & (1u << operationIndex)) == 0)
        {
            continue;
        }
        TextStudio2DGpuOperation operation =
            TextStudioLoadOperation(
                operationStart + operationIndex);
        // Retained and hero layers have their own renderers. Drawing them here
        // duplicates their coverage and clips the extra copy to the source quad.
        if ((int)round(operation.executionMetadata.x) != 1)
        {
            valid = 1;
            continue;
        }
        half operationValid = 0;
        fixed4 layer = TextStudioEvaluateStyleOperation(
            styleSlot,
            spanBase + operationIndex,
            glyphIndex,
            operation,
            input,
            pixelSamples,
            pixelActive,
            glyphLocal,
            distances,
            sharpDistances,
            fieldParameters,
            preserve,
            animatedTint,
            fieldGradient,
            derivatives,
            false,
            operationValid);
        valid = max(valid, operationValid);
        // Stack index zero is the visually topmost row. Compose later rows below it.
        result = result + layer * (1 - result.a);
    }
    return result;
}
#endif
#endif

#ifdef TEXT_STUDIO_SEPARATE_SELECTION_PAYLOAD
bool TextStudioHeroSelectionEnabled(float4 selection, int selectorIndex)
{
    if (selectorIndex < 0)
        return true;
    int chunkIndex = clamp(selectorIndex >> 4, 0, 3);
    uint bits;
    if (chunkIndex == 0)
        bits = (uint)round(selection.x);
    else if (chunkIndex == 1)
        bits = (uint)round(selection.y);
    else if (chunkIndex == 2)
        bits = (uint)round(selection.z);
    else
        bits = (uint)round(selection.w);
    uint bit = 1u << (selectorIndex & 15);
    return (bits & bit) != 0;
}
#endif

#ifdef TEXT_STUDIO_SINGLE_STYLE_OPERATION
float4 TextStudioNativeColorFace(pixel_t input, uint styleSlot, int glyphIndex)
{
    TextStudio2DGpuOperation operation = TextStudioStyleOperation(styleSlot, _TextStudio2DHeroOperationIndex);
    if ((int)round(operation.layerMetadata.x) != 0 || operation.selectorMetadata.w > .5 || operation.parameters0.w > 1.5 || _TextStudio2DHeroSourceMode != 0) return 0;
    float4 original = tex2Dgrad(_MainTex, input.texcoord0.xy, ddx(input.texcoord0.xy), ddy(input.texcoord0.xy));
    // Alpha-changing adjustments may affect covered image pixels, never transparent atlas padding.
    if (original.a <= 0) return 0;
    if (_TextStudio2DGeometryCoveragePass > .5)
        return TextStudioApplyWritingCoverage(saturate(original.a * operation.layerMetadata.z * input.faceColor.a), input);
    if (_TextStudioColorSourceMode > 1.5) original.rgb /= original.a;
    if (operation.parameters0.w > .5)
    {
        TextStudioStyleDerivatives derivatives = TextStudioBuildStyleDerivatives(input, input.textStudioLocal.xy);
        original *= TextStudioEvaluateStylePaintForGlyph(styleSlot, _TextStudio2DHeroOperationIndex, glyphIndex, operation,
            input, input.textStudioLocal.xy, derivatives, 0);
    }
    float visibility = saturate(input.faceColor.a);
    if (operation.layerMetadata.w > .5 && visibility > .0001) original.rgb *= input.faceColor.rgb / visibility;
    original = TextStudioApplyColorAdjustments(styleSlot, _TextStudio2DHeroOperationIndex, glyphIndex, original);
    float alpha = saturate(original.a * operation.layerMetadata.z * visibility);
    return TextStudioApplyWritingCoverage(float4(original.rgb * alpha, alpha), input);
}
#endif

fixed4 PixShader(pixel_t input) : SV_Target
{
    #ifdef TEXT_STUDIO_SEPARATE_SELECTION_PAYLOAD
    // Glyph IDs and pixel-grid settings are constant across a quad. Perspective
    // interpolation can otherwise round a packed glyph ID below its integer boundary.
    input.textStudioLocal.zw = input.textStudioGlyphPayload;
    #endif
    UNITY_SETUP_INSTANCE_ID(input);

    // Unbound mesh channels can contain backend-specific defaults. The bridge
    // enables payload reads only after it has uploaded zero-initialized custom
    // streams for every rendered material slot.
    half payloadValid = saturate(_TextStudioPayloadEnabled);
    #ifdef TEXT_STUDIO_FULL_PRECISION_PAYLOAD
    float4 textStudio0 = input.textStudio0 * payloadValid;
    float4 textStudio1 = input.textStudio1 * payloadValid;
    #else
    half4 textStudio0 = input.textStudio0 * payloadValid;
    half4 textStudio1 = input.textStudio1 * payloadValid;
    #endif
    float4 textStudioLocal = input.textStudioLocal * payloadValid;
    #ifdef TEXT_STUDIO_STYLE_TABLE
    uint textStudioStyleSlot;
    uint textStudioOperationMask;
    int textStudioOperationSpanBase = 0;
    int textStudioNativeOperation = -1;
    half textStudioNativeOpacity = 1;
    #ifdef TEXT_STUDIO_SINGLE_STYLE_OPERATION
    textStudioStyleSlot = (uint)max(0, _TextStudio2DHeroStyleSlot);
    textStudioOperationMask = 0u;
    #else
    TextStudioDecodeStyleIdentity(
        input.textStudioWritingAndSquiggle.w * payloadValid,
        textStudioStyleSlot,
        textStudioOperationMask,
        textStudioOperationSpanBase);
    #endif
    input.textStudioLocal = textStudioLocal;
    #ifdef TEXT_STUDIO_SINGLE_STYLE_OPERATION
    #ifdef TEXT_STUDIO_SEPARATE_SELECTION_PAYLOAD
    if (!TextStudioHeroSelectionEnabled(
            input.textStudioSelection,
            _TextStudio2DHeroSelectorIndex) ||
        !TextStudioHeroSelectionEnabled(
            input.textStudioSelection,
            (int)round(_TextStudio2DHeroScopeSelectors0.x)) ||
        !TextStudioHeroSelectionEnabled(
            input.textStudioSelection,
            (int)round(_TextStudio2DHeroScopeSelectors0.y)) ||
        !TextStudioHeroSelectionEnabled(
            input.textStudioSelection,
            (int)round(_TextStudio2DHeroScopeSelectors0.z)) ||
        !TextStudioHeroSelectionEnabled(
            input.textStudioSelection,
            (int)round(_TextStudio2DHeroScopeSelectors0.w)) ||
        !TextStudioHeroSelectionEnabled(
            input.textStudioSelection,
            (int)round(_TextStudio2DHeroScopeSelectors1.x)) ||
        !TextStudioHeroSelectionEnabled(
            input.textStudioSelection,
            (int)round(_TextStudio2DHeroScopeSelectors1.y)) ||
        !TextStudioHeroSelectionEnabled(
            input.textStudioSelection,
            (int)round(_TextStudio2DHeroScopeSelectors1.z)) ||
        !TextStudioHeroSelectionEnabled(
            input.textStudioSelection,
            (int)round(_TextStudio2DHeroScopeSelectors1.w)))
    {
        return 0;
    }
    #else
    int selectionChunk = clamp(_TextStudio2DHeroSelectorIndex >> 4, 0, 3);
    uint selectionBits = (uint)round(textStudio0[selectionChunk]);
    textStudio0 = 0;
    textStudio1 = 0;
    uint selectionBit = 1u << (_TextStudio2DHeroSelectorIndex & 15);
    if ((selectionBits & selectionBit) == 0)
        return 0;
    #endif
    #endif
    #endif
    if (_TextStudioNativeMaterialModel > 1.5)
    {
        // Multiplication must follow interpolation: lerp(1 / scale) * lerp(scale) is not 1 in perspective.
        // Express Full TMP's fragment coverage in the canonical scaled-distance form shared below.
        float nativeScale = input.param.w;
        float nativeSoftness = _OutlineSoftness * _ScaleRatioA * nativeScale;
        float nativeSoftnessRatio = 1 + nativeSoftness;
        float nativeFieldScale = nativeScale / nativeSoftnessRatio;
        float nativeBias = (input.texcoord1.w * nativeScale - 1 - .5 * nativeSoftness) / nativeSoftnessRatio;
        float nativeOutline = _OutlineWidth * _ScaleRatioA * .5 * nativeFieldScale;
        input.param = half4(nativeFieldScale, nativeBias - nativeOutline, nativeBias + nativeOutline, nativeSoftnessRatio);
    }
    // Resolve local deformation once before any variable-count appearance sampling loops.
    // All face, outline, underlay and Hero samples then share the same warped source coordinates.
    float2 squiggleOffset = TextStudioSquiggleUv(input, input.texcoord0.xy) - input.texcoord0.xy;
    input.texcoord0.xy += squiggleOffset;
    input.texcoord1.xy += squiggleOffset;
    #ifdef TEXT_STUDIO_SINGLE_STYLE_OPERATION
    if (_TextStudioColorSourceMode > .5 && _TextStudioColorSourceMode < 2.5)
    {
        int colorGlyphIndex = max(0, TextStudioPackedGlyphIndex(textStudioLocal.z));
        int originalColorGlyphIndex = TextStudioOriginalSourceGlyph(textStudioStyleSlot, colorGlyphIndex);
        if (originalColorGlyphIndex < _TextStudio2DHeroSourceRange.x || originalColorGlyphIndex >= _TextStudio2DHeroSourceRange.y) return 0;
        if (_TextStudio2DHeroGlyphFilter >= 0 && TextStudioSourceUnit(textStudioStyleSlot, colorGlyphIndex) != _TextStudio2DHeroGlyphFilter) return 0;
        return TextStudioNativeColorFace(input, textStudioStyleSlot, colorGlyphIndex);
    }
    TextStudioStyleDerivatives continuedDerivatives =
        TextStudioBuildStyleDerivatives(input, textStudioLocal.xy);
    // Offscreen glyph coverage uses its own raster derivatives, independent of the last camera's _ScreenParams.
    float2 atlasSize = float2(_TextureWidth, _TextureHeight);
    float atlasPixelsPerRasterPixel = max(length(continuedDerivatives.atlasDx * atlasSize), length(continuedDerivatives.atlasDy * atlasSize));
    float rasterFieldScale = 2 * _GradientScale / max(.0001, atlasPixelsPerRasterPixel);
    if (TextStudioUsesNativeMaterial())
    {
        // Native TMP coverage is measured in output pixels. The Hero target may be
        // several times denser; using its texels makes unchanged native text alias.
        float2 atlasPixelFootprint = float2(
            length(continuedDerivatives.atlasDx * atlasSize) / max(.0001, abs(_ScaleX)),
            length(continuedDerivatives.atlasDy * atlasSize) / max(.0001, abs(_ScaleY)));
        float nativeScale = 2 * _GradientScale * (_Sharpness + 1) /
            max(.0001, length(atlasPixelFootprint) * TextStudioStyleOutputPixelScale());
        float nativeFaceScale = nativeScale / (1 + _OutlineSoftness * _ScaleRatioA * nativeScale);
        float nativeCenter = (input.param.y + input.param.z + 1) * .5 / max(.0001, input.param.x);
        float scaleRatio = nativeFaceScale / max(.0001, input.param.x);
        input.param.yz = (input.param.yz + .5) * scaleRatio - .5;
        input.param.x = nativeFaceScale;
        input.param.w = nativeScale / max(.0001, nativeFaceScale);
        float underlayScale = nativeScale / (1 + _UnderlaySoftness * _ScaleRatioC * nativeScale);
        input.underlayParam = float2(underlayScale,
            (nativeCenter - _UnderlayDilate * _ScaleRatioC * .5) * underlayScale - .5);
        if (_TextStudioNativeMaterialModel < 1.5)
        {
            half nativeOpacity = (UNDERLAY_ON || UNDERLAY_INNER) ? 1 : input.texcoord1.z;
            fixed4 nativeOutline = _OutlineColor;
            nativeOutline.a *= nativeOpacity;
            nativeOutline.rgb *= nativeOutline.a;
            input.outlineColor = lerp(input.faceColor, nativeOutline,
                sqrt(min(1, _OutlineWidth * _ScaleRatioA * nativeFaceScale)));
        }
    }
    else
    {
        float scaleRatio = rasterFieldScale / max(.0001, input.param.x);
        input.param.yz = (input.param.yz + .5) * scaleRatio - .5;
        input.param.x = rasterFieldScale;
        input.param.w = 1;
    }
    #endif
    half sdfBias = (input.param.y + input.param.z) * .5;
    half4 sdfParameters = half4(input.param.xyz, sdfBias);

    #ifdef TEXT_STUDIO_SINGLE_STYLE_OPERATION
    float2 continuedLower;
    float2 continuedUpper;
    TextStudioContinuedAtlasBounds(input, continuedDerivatives, continuedLower, continuedUpper);
    half originalDistance = TextStudioSampleContinuedDistance(
        input,
        input.texcoord0.xy,
        continuedDerivatives.atlasDx,
        continuedDerivatives.atlasDy,
        input.param.x,
        sdfBias,
        continuedLower,
        continuedUpper);
    #elif defined(TEXT_STUDIO_STYLE_TABLE)
    // Direct primitives can extend beyond the padded source quad. The face then continues
    // the isolated glyph field instead of reading neighbouring atlas entries.
    TextStudioStyleDerivatives continuedDerivatives =
        TextStudioBuildStyleDerivatives(input, textStudioLocal.xy);
    float2 continuedLower;
    float2 continuedUpper;
    TextStudioContinuedAtlasBounds(input, continuedDerivatives, continuedLower, continuedUpper);
    half originalDistance = TextStudioSampleContinuedDistance(
        input,
        input.texcoord0.xy,
        continuedDerivatives.atlasDx,
        continuedDerivatives.atlasDy,
        input.param.x,
        sdfBias,
        continuedLower,
        continuedUpper);
    #else
    half originalDistance = TextStudioReadField(input, input.texcoord0.xy, ddx(input.texcoord0.xy), ddy(input.texcoord0.xy)).a * input.param.x;
    #endif
    float packedGlyphAndPixel = abs(textStudioLocal.z);
    int textStudioGlyphIndex = TextStudioPackedGlyphIndex(packedGlyphAndPixel);
    #ifdef TEXT_STUDIO_SINGLE_STYLE_OPERATION
    int originalSourceGlyphIndex = TextStudioOriginalSourceGlyph((uint)max(0, _TextStudio2DHeroStyleSlot), textStudioGlyphIndex);
    if (originalSourceGlyphIndex < _TextStudio2DHeroSourceRange.x || originalSourceGlyphIndex >= _TextStudio2DHeroSourceRange.y) return 0;
    if (_TextStudio2DHeroGlyphFilter >= 0 &&
        TextStudioSourceUnit((uint)max(0, _TextStudio2DHeroStyleSlot), textStudioGlyphIndex) != _TextStudio2DHeroGlyphFilter)
    {
        return 0;
    }
    #endif
    float decodedPixelCellSize = TextStudioPackedPixelCellSize(packedGlyphAndPixel, textStudioGlyphIndex);
    half pixelAmount = saturate(abs(textStudioLocal.w));
    float pixelCellSize = max(1.0, floor(decodedPixelCellSize + .5));
    half pixelActive = pixelAmount > .0001 && pixelCellSize > 1.0001;
    TextStudioPixelSamples pixelSamples = (TextStudioPixelSamples)0;
    half4 pixelDistances = originalDistance.xxxx;
    half originalSharpDistance = originalDistance;
    if (TextStudioHasMultiField(input))
    {
        originalSharpDistance = TextStudioSampleSharpDistance(
            input,
            TextStudioClampAtlasToGlyph(input, input.texcoord0.xy),
            input.param.x);
    }
    half4 pixelSharpDistances = originalSharpDistance.xxxx;
    float2 materialLocal = textStudioLocal.xy;
    if (pixelActive > .5)
    {
        half glyphSpace = textStudioLocal.z < 0 ? 1 : 0;
        half preserve = textStudioLocal.w >= 0 ? 1 : 0;
        pixelSamples = TextStudioBuildSamples(input, pixelCellSize, glyphSpace, preserve);
        pixelDistances = TextStudioSampleDistances(input, pixelSamples, 0, input.param.x);
        pixelSharpDistances = pixelDistances;
        if (TextStudioHasMultiField(input))
        {
            pixelSharpDistances = TextStudioSampleSharpDistances(
                input,
                pixelSamples,
                0,
                input.param.x);
        }
        materialLocal = lerp(materialLocal, pixelSamples.centerLocal, pixelAmount);
    }

    float2 materialTextureUv = pixelActive > .5
        ? TextStudioVaryingFromLocal(input, input.texcoord0.zw, materialLocal)
        : input.texcoord0.zw;
    float2 faceTextureUv = materialTextureUv * _FaceTex_ST.xy + _FaceTex_ST.zw +
        float2(_FaceUVSpeedX, _FaceUVSpeedY) * _Time.y;
    float2 outlineTextureUv = materialTextureUv * _OutlineTex_ST.xy + _OutlineTex_ST.zw +
        float2(_OutlineUVSpeedX, _OutlineUVSpeedY) * _Time.y;
    fixed4 faceTexture = tex2D(_FaceTex, faceTextureUv);
    fixed4 outlineTexture = tex2D(_OutlineTex, outlineTextureUv);
    fixed4 faceColor = input.faceColor;
    faceColor.rgb *= faceTexture.rgb * faceTexture.a;
    faceColor.a *= faceTexture.a;
    fixed4 outlineColor = input.outlineColor;
    outlineColor.rgb *= outlineTexture.rgb * outlineTexture.a;
    outlineColor.a *= outlineTexture.a;
    // Full TMP mixes independently textured colors in the fragment stage; Mobile was mixed per vertex.
    if (_TextStudioNativeMaterialModel > 1.5)
        outlineColor = lerp(faceColor, outlineColor, sqrt(min(1, (sdfParameters.z - sdfParameters.y) * input.param.w)));

    half originalFaceAlpha = TextStudioFaceAlpha(originalSharpDistance, sdfBias, textStudio0.x);
    fixed4 originalColor = TextStudioSdfColor(
        originalSharpDistance,
        faceColor,
        outlineColor,
        sdfParameters,
        textStudio0.x,
        input.param.w);
    half faceAlpha = originalFaceAlpha;
    fixed4 c = originalColor;
    if (pixelActive > .5)
    {
        half preserve = pixelSamples.preserve;
        half pixelFaceAlpha = TextStudioAverageFaceAlpha(
            pixelSharpDistances,
            sdfParameters,
            textStudio0.x,
            preserve);
        fixed4 pixelColor = TextStudioAverageSdfColor(
            pixelSharpDistances,
            faceColor,
            outlineColor,
            sdfParameters,
            textStudio0.x,
            preserve,
            input.param.w);
        faceAlpha = lerp(originalFaceAlpha, pixelFaceAlpha, pixelAmount);
        c = lerp(originalColor, pixelColor, pixelAmount);
    }

    #ifdef TEXT_STUDIO_STYLE_TABLE
    half4 styleSdfParameters = sdfParameters;
    float4 preparedDecode = TextStudioPreparedForPixel(input).decode;
    float availableRange = preparedDecode.x > 0 ? preparedDecode.y : 1;
    styleSdfParameters.y = sdfBias + .5 * (availableRange - 1) * input.param.x;
    half styleProgramValid;
    if (TEXT_STUDIO_STYLE_TABLE_ENABLED > .5 && textStudioStyleSlot > 0)
    {
        TextStudio2DGpuStyleHeader nativeHeader = TextStudioLoadStyleHeader(textStudioStyleSlot);
        #ifdef TEXT_STUDIO_SINGLE_STYLE_OPERATION
        int nativeIndex = _TextStudio2DHeroOperationIndex;
        bool nativeSelected = nativeIndex >= 0 && nativeIndex < (int)round(nativeHeader.metadata.y);
        #else
        int nativeIndex = 0;
        bool nativeSelected = (int)round(nativeHeader.metadata.y) == 1 && textStudioOperationSpanBase == 0 && (textStudioOperationMask & 1u) != 0;
        #endif
        if (nativeSelected && (int)round(nativeHeader.metadata.x) == TEXT_STUDIO_STYLE_SCHEMA_VERSION)
        {
            TextStudio2DGpuOperation nativeOperation = TextStudioLoadOperation(
                (int)round(nativeHeader.program.x) + nativeIndex);
            if ((int)round(nativeOperation.layerMetadata.x) == 0 && nativeOperation.parameters1.w > .5)
            {
                textStudioNativeOperation = nativeIndex;
                textStudioNativeOpacity = nativeOperation.layerMetadata.z;
            }
        }
    }
    // Native fills retain TMP face/outline textures, underlay and material effects below.
    // Appearance paint evaluation would replace that complete native contribution with a flat fill.
    if (textStudioNativeOperation < 0)
    {
    #ifdef TEXT_STUDIO_SINGLE_STYLE_OPERATION
    fixed4 styleColor = TextStudioEvaluateSingleStyleOperation(
        textStudioStyleSlot,
        _TextStudio2DHeroOperationIndex,
        textStudioGlyphIndex,
        input,
        pixelSamples,
        pixelActive,
        materialLocal,
        pixelActive > .5 ? pixelDistances : originalDistance.xxxx,
        pixelActive > .5 ? pixelSharpDistances : originalSharpDistance.xxxx,
        styleSdfParameters,
        pixelActive > .5 ? pixelSamples.preserve : 0,
        faceColor,
        styleProgramValid);
    return TextStudioApplyWritingCoverage(
        styleProgramValid > .5 ? styleColor : 0,
        input);
    #else
    fixed4 styleColor = TextStudioEvaluateStyleProgram(
        textStudioStyleSlot,
        textStudioOperationMask,
        textStudioOperationSpanBase,
        textStudioGlyphIndex,
        input,
        pixelSamples,
        pixelActive,
        materialLocal,
        pixelActive > .5 ? pixelDistances : originalDistance.xxxx,
        pixelActive > .5 ? pixelSharpDistances : originalSharpDistance.xxxx,
        styleSdfParameters,
        pixelActive > .5 ? pixelSamples.preserve : 0,
        faceColor,
        styleProgramValid);
    if (styleProgramValid > .5)
        c = styleColor;
    #endif
    }
    #endif

    half authoredOutline = max(0, textStudio0.w);
    half authoredGlow = max(0, textStudio1.x);
    if (authoredOutline > 0 || authoredGlow > 0)
    {
        half originalOuter = saturate(
            originalDistance - sdfBias + authoredOutline * input.param.x * .2);
        half outer = originalOuter;
        if (pixelActive > .5)
        {
            half pixelOuter = saturate(
                pixelDistances.x - sdfBias + authoredOutline * input.param.x * .2);
            if (pixelSamples.preserve > .5)
            {
                pixelOuter += saturate(pixelDistances.y - sdfBias + authoredOutline * input.param.x * .2);
                pixelOuter += saturate(pixelDistances.z - sdfBias + authoredOutline * input.param.x * .2);
                pixelOuter += saturate(pixelDistances.w - sdfBias + authoredOutline * input.param.x * .2);
                pixelOuter *= .25;
            }
            outer = lerp(originalOuter, pixelOuter, pixelAmount);
        }
        half ring = saturate(outer - faceAlpha);
        half glowAlpha = ring * saturate(authoredOutline + authoredGlow);
        c.rgb += _TextStudioGlowColor.rgb * glowAlpha * _TextStudioGlowColor.a;
        c.a = max(c.a, glowAlpha);
    }

    half chromatic = textStudio1.w * _TextStudioChromaticIntensity;
    if (abs(chromatic) > .0001)
    {
        float2 atlasOffset = float2(chromatic * _GradientScale * 3 / max(1, _TextureWidth), 0);
        half originalRed = TextStudioReadField(input, input.texcoord0.xy + atlasOffset, ddx(input.texcoord0.xy), ddy(input.texcoord0.xy)).a * input.param.x;
        half originalBlue = TextStudioReadField(input, input.texcoord0.xy - atlasOffset, ddx(input.texcoord0.xy), ddy(input.texcoord0.xy)).a * input.param.x;
        half redAlpha = TextStudioFaceAlpha(originalRed, sdfBias, textStudio0.x);
        half blueAlpha = TextStudioFaceAlpha(originalBlue, sdfBias, textStudio0.x);
        if (pixelActive > .5)
        {
            half4 pixelRed = TextStudioSampleDistances(input, pixelSamples, atlasOffset, input.param.x);
            half4 pixelBlue = TextStudioSampleDistances(input, pixelSamples, -atlasOffset, input.param.x);
            redAlpha = lerp(
                redAlpha,
                TextStudioAverageFaceAlpha(pixelRed, sdfParameters, textStudio0.x, pixelSamples.preserve),
                pixelAmount);
            blueAlpha = lerp(
                blueAlpha,
                TextStudioAverageFaceAlpha(pixelBlue, sdfParameters, textStudio0.x, pixelSamples.preserve),
                pixelAmount);
        }
        half3 splitAlpha = half3(redAlpha, faceAlpha, blueAlpha);
        c.rgb = max(0, c.rgb + faceColor.rgb * (splitAlpha - faceAlpha.xxx));
        c.a = max(c.a, max(splitAlpha.r, splitAlpha.b) * faceColor.a);
    }

    half sweepBrightness = max(0, textStudio1.z);
    if (sweepBrightness > .0001)
    {
        half sweepDistance = abs(textStudio1.y);
        half band = TextStudioVisualSweepBand(
            sweepDistance,
            .35,
            1);
        c.rgb += _TextStudioSweepColor.rgb * band * c.a *
            _TextStudioSweepColor.a * _TextStudioSweepIntensity * sweepBrightness;
    }

    UNITY_BRANCH
    if (UNDERLAY_ON && TextStudioUsesNativeMaterial())
    {
        float2 underlayOffset = input.texcoord1.xy - input.texcoord0.xy;
        half originalUnderlayDistance = TextStudioReadField(input, input.texcoord1.xy, ddx(input.texcoord0.xy), ddy(input.texcoord0.xy)).a * input.underlayParam.x;
        half underlayCoverage = saturate(originalUnderlayDistance - input.underlayParam.y);
        if (pixelActive > .5)
        {
            half4 pixelUnderlay = TextStudioSampleDistances(
                input,
                pixelSamples,
                underlayOffset,
                input.underlayParam.x);
            half pixelUnderlayCoverage = saturate(pixelUnderlay.x - input.underlayParam.y);
            if (pixelSamples.preserve > .5)
            {
                pixelUnderlayCoverage += saturate(pixelUnderlay.y - input.underlayParam.y);
                pixelUnderlayCoverage += saturate(pixelUnderlay.z - input.underlayParam.y);
                pixelUnderlayCoverage += saturate(pixelUnderlay.w - input.underlayParam.y);
                pixelUnderlayCoverage *= .25;
            }
            underlayCoverage = lerp(underlayCoverage, pixelUnderlayCoverage, pixelAmount);
        }
        c += float4(_UnderlayColor.rgb * _UnderlayColor.a, _UnderlayColor.a) *
            underlayCoverage * (1 - c.a);
    }
    UNITY_BRANCH
    if (UNDERLAY_INNER && TextStudioUsesNativeMaterial())
    {
        float2 underlayOffset = input.texcoord1.xy - input.texcoord0.xy;
        bool fullNativeMaterial = _TextStudioNativeMaterialModel > 1.5;
        half originalInnerMask = fullNativeMaterial
            ? saturate((originalDistance - sdfBias - .5) * input.param.w + .5)
            : saturate(originalDistance - input.param.z);
        half originalUnderlayDistance = TextStudioReadField(input, input.texcoord1.xy, ddx(input.texcoord0.xy), ddy(input.texcoord0.xy)).a * input.underlayParam.x;
        half underlayCoverage =
            (1 - saturate(originalUnderlayDistance - input.underlayParam.y)) *
            originalInnerMask;
        if (pixelActive > .5)
        {
            half4 pixelUnderlay = TextStudioSampleDistances(
                input,
                pixelSamples,
                underlayOffset,
                input.underlayParam.x);
            half pixelCoverage =
                (1 - saturate(pixelUnderlay.x - input.underlayParam.y)) *
                (fullNativeMaterial
                        ? saturate((pixelDistances.x - sdfBias - .5) * input.param.w + .5)
                        : saturate(pixelDistances.x - input.param.z));
            if (pixelSamples.preserve > .5)
            {
                pixelCoverage +=
                    (1 - saturate(pixelUnderlay.y - input.underlayParam.y)) *
                    (fullNativeMaterial
                        ? saturate((pixelDistances.y - sdfBias - .5) * input.param.w + .5)
                        : saturate(pixelDistances.y - input.param.z));
                pixelCoverage +=
                    (1 - saturate(pixelUnderlay.z - input.underlayParam.y)) *
                    (fullNativeMaterial
                        ? saturate((pixelDistances.z - sdfBias - .5) * input.param.w + .5)
                        : saturate(pixelDistances.z - input.param.z));
                pixelCoverage +=
                    (1 - saturate(pixelUnderlay.w - input.underlayParam.y)) *
                    (fullNativeMaterial
                        ? saturate((pixelDistances.w - sdfBias - .5) * input.param.w + .5)
                        : saturate(pixelDistances.w - input.param.z));
                pixelCoverage *= .25;
            }
            underlayCoverage = lerp(underlayCoverage, pixelCoverage, pixelAmount);
        }
        c += float4(_UnderlayColor.rgb * _UnderlayColor.a, _UnderlayColor.a) *
            underlayCoverage * (1 - c.a);
    }

    half encodedDissolveThreshold = textStudio0.y;
    half dissolveThreshold = abs(encodedDissolveThreshold);
    if (dissolveThreshold > .0001)
    {
        half noise;
        if (encodedDissolveThreshold > 0)
        {
            half2 cell = floor(materialLocal * 18 + input.texcoord0.xy * 127);
            noise = frac(sin(dot(cell, half2(12.9898, 78.233))) * 43758.5453);
        }
        else
        {
            noise = TextStudioTextNoise(input);
        }
        half encodedSoftness = textStudio0.z;
        half softness = max(.001, abs(encodedSoftness));
        half coverage = TextStudioVisualThresholdAbove(
            noise,
            dissolveThreshold,
            softness);
        if (encodedSoftness >= 0)
        {
            c *= coverage;
        }
        else
        {
            half signedEdge = noise - dissolveThreshold;
            half hotBand = smoothstep(-softness * .08, softness * .12, signedEdge) *
                (1 - smoothstep(softness * .18, softness * .42, signedEdge));
            half emberBand = smoothstep(-softness * .42, -softness * .08, signedEdge) *
                (1 - smoothstep(softness * .32, softness * 1.25, signedEdge));
            half sourceAlpha = c.a;
            fixed4 burned = c * coverage;
            half emberAlpha = sourceAlpha * emberBand * .92;
            half hotAlpha = sourceAlpha * hotBand;
            burned.rgb += fixed3(1.0, .16, .018) * emberAlpha;
            burned.rgb += fixed3(1.0, .86, .28) * hotAlpha;
            burned.a = max(burned.a, max(emberAlpha, hotAlpha));
            c = burned;
        }
    }

    UNITY_BRANCH
    if (UNITY_UI_CLIP_RECT)
    {
        const half2 maskSoftness = half2(
            max(_UIMaskSoftnessX, _MaskSoftnessX),
            max(_UIMaskSoftnessY, _MaskSoftnessY));
        float2 localPixelSize = max(.00001, fwidth(input.mask.zw) * .5);
        half2 maskScale = .25 / (.25 * maskSoftness + localPixelSize);
        half2 m = saturate(
            (_ClipRect.zw - _ClipRect.xy - abs(input.mask.zw)) *
            maskScale);
        c *= m.x * m.y;
    }
    UNITY_BRANCH
    if ((UNDERLAY_ON || UNDERLAY_INNER) && TextStudioUsesNativeMaterial())
        c *= input.texcoord1.z;
    #ifdef TEXT_STUDIO_STYLE_TABLE
    if (textStudioNativeOperation >= 0)
    {
        #ifdef TEXT_STUDIO_SINGLE_STYLE_OPERATION
        if (_TextStudio2DGeometryCoveragePass > .5 || _TextStudio2DHeroSourceMode == 5)
            return TextStudioApplyWritingCoverage(saturate(c.a), input);
        #endif
        half nativeCoverage = c.a;
        fixed4 nativeStraight = fixed4(c.a > .0001 ? c.rgb / c.a : 0, c.a);
        nativeStraight = TextStudioApplyColorAdjustments(textStudioStyleSlot, textStudioNativeOperation, textStudioGlyphIndex, nativeStraight);
        c = fixed4(nativeStraight.rgb * nativeStraight.a, nativeStraight.a) * textStudioNativeOpacity;
        #ifdef TEXT_STUDIO_SINGLE_STYLE_OPERATION
        if (_TextStudio2DHeroSourceMode == 4) c = half4(c.a, 0, 0, nativeCoverage);
        #endif
    }
    #endif
    UNITY_BRANCH
    if (UNITY_UI_ALPHACLIP)
        clip(c.a - .001);
    return TextStudioApplyWritingCoverage(c, input);
}

#ifdef TEXT_STUDIO_STYLE_TABLE
fixed4 TextStudioWorldShadowFragment(pixel_t input) : SV_Target
{
    uint styleSlot;
    uint operationMask;
    int operationSpanBase;
    TextStudioDecodeStyleIdentity(
        input.textStudioWritingAndSquiggle.w * saturate(_TextStudioPayloadEnabled),
        styleSlot,
        operationMask,
        operationSpanBase);
    if (TEXT_STUDIO_STYLE_TABLE_ENABLED < .5 || styleSlot == 0)
        clip(-1);

    TextStudio2DGpuStyleHeader header =
        TextStudioLoadStyleHeader(styleSlot);
    uint styleFeatures = (uint)round(header.metadata.w);
    const uint worldShadowCasterFeature = 1u << 20;
    if ((int)round(header.metadata.x) != TEXT_STUDIO_STYLE_SCHEMA_VERSION ||
        (styleFeatures & worldShadowCasterFeature) == 0)
    {
        clip(-1);
    }

    fixed4 styled = PixShader(input);
    clip(styled.a - .001);
    return 0;
}
#endif

#endif
