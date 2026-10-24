#ifndef TEXT_STUDIO_2D_FIELD_COVERAGE_COMMON_INCLUDED
#define TEXT_STUDIO_2D_FIELD_COVERAGE_COMMON_INCLUDED

// Glyph-field coverage shared by the table-driven Field program and the uniform field program
// (TextStudio2DFieldUniform.shader): continued field sampling and bounds, fill, stroke, soft and shifted coverage. Both
// compile these definitions, so a row evaluated from material constants matches the same row read from the style table.
// Requires pixel_t, the derivative and sample structs and the atlas helpers of TextStudio2DProductionSDFCommon.cginc,
// which includes this file for its table programs.

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
    #ifdef TEXT_STUDIO_UNIFORM_EXTERNAL_FIELD
    // An engine row always resolves to an atlas page (TextStudioResolveExternalField), so the uniform program's
    // external-field variant samples it unconditionally and compiles no TMP atlas sampling. A branch attribute on a
    // constant condition is invalid (FXC X3554), so this variant is selected by the preprocessor.
    result = TextStudioSamplePreparedField(TextStudioPreparedForPixel(input), sampleUv).a * fieldScale;
    #else
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
    #endif
    }
    return result;
}

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
    // Family variants that never preserve pixel samples reduce this loop to one iteration; keep the loop for the others.
    #pragma warning (push)
    #pragma warning (disable : 3557)
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
#pragma warning (pop)

// The one sample TextStudioStyleSampleShiftedDistances takes without pixel samples, at the same atlas offset, inside
// glyph bounds the caller already resolved: the uniform program shares them with its face sample, which keeps its
// shadow row from holding a second set of bounds. Keep the offset identical to TextStudioStyleSampleShiftedDistances.
half TextStudioStyleSampleShiftedDistance(
    pixel_t input,
    float2 normalizedOffset,
    half fieldScale,
    half threshold,
    half units,
    TextStudioStyleDerivatives derivatives,
    float2 lower,
    float2 upper)
{
    float2 atlasOffset = units > .5
        ? -(derivatives.atlasDx * normalizedOffset.x +
            derivatives.atlasDy * normalizedOffset.y) * TextStudioStyleOutputPixelScale()
        : float2(
            -normalizedOffset.x * max(1, _TextStudioFieldPixelsPerEm) / max(1.0, _TextureWidth),
            -normalizedOffset.y * max(1, _TextStudioFieldPixelsPerEm) / max(1.0, _TextureHeight));
    return TextStudioSampleContinuedDistance(
        input, input.texcoord0.xy + atlasOffset, derivatives.atlasDx, derivatives.atlasDy,
        fieldScale, threshold, lower, upper);
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

#endif
