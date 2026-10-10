#ifndef TEXT_STUDIO_2D_ADAPTIVE_DEPTH_COMMON_INCLUDED
#define TEXT_STUDIO_2D_ADAPTIVE_DEPTH_COMMON_INCLUDED

struct TextStudio2DDepthFieldMapping
{
    float2 atlasOrigin;
    float2 atlasAxisX;
    float2 atlasAxisY;
    float2 atlasDx;
    float2 atlasDy;
    float2 atlasLower;
    float2 atlasUpper;
    half threshold;
    half gradientScale;
    float pixelsPerEm;
    TextStudioPreparedField prepared;
};

float2 TextStudioDepthAtlasFromLocal(
    TextStudio2DDepthFieldMapping mapping,
    float2 local)
{
    return mapping.atlasOrigin +
           mapping.atlasAxisX * local.x +
           mapping.atlasAxisY * local.y;
}

float2 TextStudioDepthLocalDeltaFromAtlas(
    TextStudio2DDepthFieldMapping mapping,
    float2 atlasDelta)
{
    float determinant =
        mapping.atlasAxisX.x * mapping.atlasAxisY.y -
        mapping.atlasAxisX.y * mapping.atlasAxisY.x;
    if (abs(determinant) < .0000001)
        return 0;
    float inverse = 1.0 / determinant;
    return float2(
        (atlasDelta.x * mapping.atlasAxisY.y -
         atlasDelta.y * mapping.atlasAxisY.x) * inverse,
        (mapping.atlasAxisX.x * atlasDelta.y -
         mapping.atlasAxisX.y * atlasDelta.x) * inverse);
}

float2 TextStudioDepthEmOffset(TextStudio2DDepthFieldMapping mapping, float2 offsetEm)
{
    float2 atlasDelta = offsetEm * mapping.pixelsPerEm / max(float2(_TextureWidth, _TextureHeight), 1);
    return TextStudioDepthLocalDeltaFromAtlas(mapping, atlasDelta);
}

float TextStudioDepthAtlasPixelsPerOutputPixel(TextStudio2DDepthFieldMapping mapping)
{
    float2 size = max(float2(_TextureWidth, _TextureHeight), 1);
    return max(length(mapping.atlasDx * size), length(mapping.atlasDy * size));
}

half TextStudioDepthCoverageFromDistance(
    TextStudio2DDepthFieldMapping mapping,
    half signedDistancePixels,
    half softnessPixels,
    half silhouetteOutsetPixels,
    half continuationPixels)
{
    float2 textureSize = max(float2(_TextureWidth, _TextureHeight), 1.0);
    float2 derivativePixelsX = mapping.atlasDx * textureSize;
    float2 derivativePixelsY = mapping.atlasDy * textureSize;
    half antiAliasPixels = max(
        .5,
        .5 * max(length(derivativePixelsX), length(derivativePixelsY)));
    // Reserve the actual contour expansion before widening the coverage ramp.
    // Otherwise saturated exterior distances turn the padded glyph into a quad.
    float minimumField = mapping.prepared.decode.x > 0 ? .5 - .5 * mapping.prepared.decode.y : 0;
    half exteriorBand = max(.0001,
        (mapping.threshold - minimumField) * (2 * mapping.gradientScale) - max(0, silhouetteOutsetPixels));
    half continuation = min(max(0, continuationPixels), max(0, exteriorBand - antiAliasPixels));
    half transition = max(.0001,
        min(softnessPixels + antiAliasPixels, exteriorBand - continuation));
    return smoothstep(-transition, transition, signedDistancePixels + continuation);
}

half TextStudioDepthFieldSample(
    TextStudio2DDepthFieldMapping mapping,
    float2 local,
    half softnessPixels,
    half silhouetteOutsetPixels,
    out half signedDistancePixels)
{
    float2 atlasUv = TextStudioDepthAtlasFromLocal(mapping, local);
    float2 textureSize = max(float2(_TextureWidth, _TextureHeight), 1.0);
    float2 lower = mapping.atlasLower;
    float2 upper = mapping.atlasUpper;
    float2 safeUv = clamp(atlasUv, lower, upper);

    half field = 0;
    UNITY_BRANCH
    if (mapping.prepared.decode.x > 0) field = TextStudioSamplePreparedField(mapping.prepared, atlasUv).a;
    else field = tex2Dgrad(_MainTex, safeUv, mapping.atlasDx, mapping.atlasDy).a;
    half fieldSignedDistancePixels =
        (field - mapping.threshold) * (2 * mapping.gradientScale);
    float2 outside = max(lower - atlasUv, 0) +
                     max(atlasUv - upper, 0);
    float outsidePixels = length(outside * textureSize);
    float2 insideFromLower = (atlasUv - lower) * textureSize;
    float2 insideFromUpper = (upper - atlasUv) * textureSize;
    float insidePixels = min(
        min(insideFromLower.x, insideFromLower.y),
        min(insideFromUpper.x, insideFromUpper.y));
    half rectangleDistancePixels = outsidePixels > .00001
        ? -outsidePixels
        : insidePixels;
    signedDistancePixels = (mapping.prepared.decode.x > 0 ? fieldSignedDistancePixels : min(
        fieldSignedDistancePixels - outsidePixels, rectangleDistancePixels)) + max(0, silhouetteOutsetPixels);
    return TextStudioDepthCoverageFromDistance(
        mapping, signedDistancePixels, softnessPixels, silhouetteOutsetPixels, 0);
}

float2 TextStudioDepthSourceLocal(
    float2 outputLocal,
    float2 offsetLocal,
    half taper,
    float progress)
{
    half scale = max(.05, 1.0 - taper * progress);
    return .5 + (outputLocal - .5 - offsetLocal * progress) / scale;
}

half2 TextStudioDepthFieldNormal(
    TextStudio2DDepthFieldMapping mapping,
    float2 local)
{
    float2 textureSize = max(float2(_TextureWidth, _TextureHeight), 1.0);
    float horizontalPixels = max(
        1.0,
        length(mapping.atlasAxisX * textureSize));
    float verticalPixels = max(
        1.0,
        length(mapping.atlasAxisY * textureSize));
    float2 delta = float2(
        1.0 / horizontalPixels,
        1.0 / verticalPixels);
    half leftDistance = 0;
    half rightDistance = 0;
    half bottomDistance = 0;
    half topDistance = 0;
    TextStudioDepthFieldSample(
        mapping,
        local - float2(delta.x, 0),
        0,
        0,
        leftDistance);
    TextStudioDepthFieldSample(
        mapping,
        local + float2(delta.x, 0),
        0,
        0,
        rightDistance);
    TextStudioDepthFieldSample(
        mapping,
        local - float2(0, delta.y),
        0,
        0,
        bottomDistance);
    TextStudioDepthFieldSample(
        mapping,
        local + float2(0, delta.y),
        0,
        0,
        topDistance);
    half2 inward = half2(
        rightDistance - leftDistance,
        topDistance - bottomDistance);
    half magnitude = length(inward);
    return magnitude > .0001 ? -inward / magnitude : half2(0, 1);
}

half TextStudioDepthLighting(
    TextStudio2DDepthFieldMapping mapping,
    float2 surfaceLocal,
    half depthProgress,
    half lightAngleDegrees,
    half sideStrength,
    half frontBevelWidth,
    half frontBevelStrength,
    half farCapShade)
{
    half response = 1;
    if (sideStrength > .0001)
    {
        half2 normal = TextStudioDepthFieldNormal(mapping, surfaceLocal);
        half radians = lightAngleDegrees * .0174532925199433;
        half2 lightDirection = half2(cos(radians), sin(radians));
        half lambert = saturate(dot(normal, lightDirection) * .5 + .5);
        response *= lerp(1, lerp(.62, 1.28, lambert), saturate(sideStrength));
    }
    if (frontBevelWidth > .0001 && frontBevelStrength > .0001)
    {
        half nearFace = 1 - smoothstep(
            0,
            max(.0001, frontBevelWidth),
            depthProgress);
        response += nearFace * saturate(frontBevelStrength) * .42;
    }
    response *= 1 - smoothstep(.55, 1, depthProgress) *
        saturate(farCapShade) * .62;
    return max(0, response);
}

half TextStudioAdaptiveDepthCoverage(
    TextStudio2DDepthFieldMapping mapping,
    float2 outputLocal,
    float2 offsetLocal,
    half taper,
    half falloff,
    half softnessPixels,
    half silhouetteOutsetPixels,
    half endFade,
    half qualityTier,
    out float2 paintLocal,
    out half depthProgress)
{
    paintLocal = saturate(outputLocal);
    depthProgress = 0;
    float offsetLength = length(offsetLocal);
    if (offsetLength <= 0)
        return 0;
    taper = min(.95, taper);
    softnessPixels = max(0, softnessPixels);
    silhouetteOutsetPixels = max(0, silhouetteOutsetPixels);
    half frontSignedDistance = 0;
    half frontCoverage = TextStudioDepthFieldSample(
        mapping,
        outputLocal,
        softnessPixels,
        silhouetteOutsetPixels,
        frontSignedDistance);
    if (frontCoverage >= 1)
        return frontCoverage;

    float2 textureSize = max(float2(_TextureWidth, _TextureHeight), 1.0);
    float2 offsetAtlas =
        mapping.atlasAxisX * offsetLocal.x +
        mapping.atlasAxisY * offsetLocal.y;
    float rayPixels = length(offsetAtlas * textureSize);
    float glyphRadiusPixels = .5 * max(
        length(mapping.atlasAxisX * textureSize),
        length(mapping.atlasAxisY * textureSize));
    rayPixels += abs(taper) * glyphRadiusPixels;
    rayPixels = max(rayPixels, .000001);
    int iterationBudget = qualityTier < .5
        ? 16
        : qualityTier < 1.5
            ? 32
            : 64;
    float sampledRayPixels = rayPixels;
    #ifndef TEXT_STUDIO_SINGLE_STYLE_OPERATION
    // Direct primitives draw at output resolution. On a magnified glyph, slices spaced by field pixels land several
    // output pixels apart and the swept edge shows each one as a flat, hard-cut band; Hero gets this from its target.
    sampledRayPixels = max(rayPixels, rayPixels / max(.0001, TextStudioDepthAtlasPixelsPerOutputPixel(mapping)));
    #endif
    int iterationLimit = min(
        iterationBudget,
        max(6, (int)ceil(sampledRayPixels * .75)));
    // A complete ray must fit inside the selected quality budget. The former
    // quarter-step floor could spend every iteration near the source edge and
    // never visit most of a long extrusion, leaving detached endpoint islands.
    float minimumStep = 1.0 / iterationLimit;

    // Keep the face-adjacent slice in the swept silhouette. Higher stack rows
    // such as Fill and Stroke occlude it through normal painter order. Removing
    // the source slice here can expose a seam whenever the depth silhouette is
    // expanded farther than the face layer's outward antialiasing extent.
    half bestCoverage = frontCoverage;
    float depthMoment = 0;
    float2 surfaceMoment = saturate(outputLocal) * frontCoverage;
    float previousT = 0;
    float t = minimumStep * .5;
    // Signed-distance steps cannot skip the silhouette; only the part of a gap forced beyond that safe distance can.
    float safeStep = abs(frontSignedDistance) / rayPixels * .72;
    [loop]
    for (int iteration = 0; iteration < 64; iteration++)
    {
        if (iteration >= iterationLimit || t > 1.0)
            break;

        float2 sourceLocal = TextStudioDepthSourceLocal(
            outputLocal,
            offsetLocal,
            taper,
            t);
        half signedDistancePixels = 0;
        half sampledCoverage = TextStudioDepthFieldSample(
            mapping,
            sourceLocal,
            softnessPixels,
            silhouetteOutsetPixels,
            signedDistancePixels);
        // The bounded iteration budget can place neighboring ray samples more
        // than one atlas pixel apart on long extrusions. Continue each sample
        // halfway across the part of its neighbouring gaps that was forced past
        // the safe signed distance, so the swept silhouette stays connected
        // without emitting visible depth slices. Continuing across safe gaps
        // inflated every slice sideways into a flat, hard-cut fringe.
        float currentSafeStep = abs(signedDistancePixels) / rayPixels * .72;
        float forcedGap = max(
            max(0, (t - previousT) - safeStep),
            max(0, minimumStep - currentSafeStep));
        half continuationPixels = min(2.25, rayPixels * forcedGap * .52);
        half coverage = max(
            sampledCoverage,
            TextStudioDepthCoverageFromDistance(
                mapping,
                signedDistancePixels,
                softnessPixels,
                silhouetteOutsetPixels,
                continuationPixels));
        coverage *= saturate(1.0 - falloff * t);
        if (endFade > .0001)
        {
            coverage *= 1 - smoothstep(
                max(0, 1.0 - saturate(endFade)),
                1.0,
                t);
        }
        // Match isolated depth: a farther slice fills only the remaining
        // coverage. Its weighted progress varies continuously at grazing hits,
        // and its terminal fade cannot remove any nearer slice's soft edge.
        half contribution = max(0, coverage - bestCoverage);
        bestCoverage += contribution;
        depthMoment += contribution * t;
        surfaceMoment += contribution * saturate(sourceLocal);
        if (sampledCoverage > .995)
            break;

        previousT = t;
        safeStep = currentSafeStep;
        t += clamp(currentSafeStep, minimumStep, .2);
    }

    if (endFade <= .0001 && (t <= 1.0 || bestCoverage < .995))
    {
        float2 endLocal = TextStudioDepthSourceLocal(
            outputLocal,
            offsetLocal,
            taper,
            1.0);
        half endSignedDistance = 0;
        half endCoverage = TextStudioDepthFieldSample(
            mapping,
            endLocal,
            softnessPixels,
            silhouetteOutsetPixels,
            endSignedDistance) * saturate(1.0 - falloff);
        half contribution = max(0, endCoverage - bestCoverage);
        bestCoverage += contribution;
        depthMoment += contribution;
        surfaceMoment += contribution * saturate(endLocal);
    }

    if (bestCoverage > .00001)
    {
        float inverseCoverage = rcp(bestCoverage);
        paintLocal = surfaceMoment * inverseCoverage;
        depthProgress = saturate(depthMoment * inverseCoverage);
    }
    return saturate(bestCoverage);
}

#endif
