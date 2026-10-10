#ifndef TEXT_STUDIO_MATERIAL_LIGHTING_COMMON_INCLUDED
#define TEXT_STUDIO_MATERIAL_LIGHTING_COMMON_INCLUDED
#include "TextStudio2DNoiseCommon.cginc"
half3 TextStudioStyleSpectrum(half phase)
{
    half3 wave = abs(frac(phase + half3(0, .6666667, .3333333)) * 6 - 3);
    return saturate(wave - 1);
}

// Full precision: fxc folds the early return into a bitwise select, which min16float registers reject on mobile and Web.
float TextStudioStyleSparkle(
    float2 glyphLocal,
    half amount,
    half seed,
    half glyphFootprint)
{
    if (amount <= .0001)
        return 0;

    float scale = lerp(28.0, 72.0, saturate(amount));
    float2 grid = glyphLocal * scale + float2(seed * .071, seed * .113);
    float2 cell = floor(grid);
    half eligible = step(1 - amount * .42, TextStudioHash21(cell + seed));
    float2 center = float2(
        TextStudioHash21(cell + seed * 1.37),
        TextStudioHash21(cell.yx + seed * 2.11));
    half distanceToPoint = length(frac(grid) - center);
    half radius = lerp(.045, .12, amount);
    half antialias = max(glyphFootprint * scale, .002);
    return eligible * (1 - smoothstep(radius, radius + antialias, distanceToPoint));
}

fixed4 TextStudioEvaluateMaterialLighting(
    int layerKind, TextStudio2DGpuOperation operation, float2 glyphLocal,
    half faceCoverage, half reliefCoverage, half insideDistance, half reliefWidth,
    half2 fieldGradient, half glyphFootprint, fixed4 animatedTint, out half contributionCoverage)
{
    half sourceAlpha = saturate(animatedTint.a);
    half depth = max(.001, operation.parameters0.z);
    half roughness = layerKind == 6 ? 0 : saturate(operation.parameters1.y);
    half metallic = saturate(operation.parameters1.z);
    half clearcoat = saturate(operation.parameters1.w);
    half surfaceProgress = saturate(insideDistance / reliefWidth);
    half edgeProfile = 1 - smoothstep(.05, .92, surfaceProgress);
    half surfaceSlope = depth * 3.65 * edgeProfile;
    half3 normal = normalize(half3(-fieldGradient * surfaceSlope, 1));
    int responseKind = layerKind == 6
        ? -1
        : (int)round(operation.paintMetadata.y);
    if (responseKind == 1)
        normal.xy = -normal.xy;

    half lightAngle = operation.parameters0.w * .0174532925199433;
    half lightElevation = operation.parameters1.x * .0174532925199433;
    half elevationCosine = cos(lightElevation);
    half3 lightDirection = normalize(half3(
        cos(lightAngle) * elevationCosine,
        sin(lightAngle) * elevationCosine,
        sin(lightElevation)));
    half3 viewDirection = half3(0, 0, 1);
    half3 halfDirection = normalize(lightDirection + viewDirection);
    half lightResponse = dot(normal, lightDirection);
    half lit = saturate(lightResponse);
    half shade = saturate(-lightResponse);
    half specularPower = exp2(lerp(7.5, 1.5, roughness * roughness));
    half specular = pow(saturate(dot(normal, halfDirection)), specularPower);
    half coat = clearcoat * pow(
        saturate(dot(normal, halfDirection)),
        lerp(128.0, 18.0, roughness));
    half fresnel = pow(saturate(1 - normal.z), 5);

    half anisotropy = operation.parameters2.x;
    half2 anisotropyAxis = half2(cos(lightAngle), sin(lightAngle));
    half anisotropicBand = pow(
        saturate(1 - abs(dot(normal.xy, anisotropyAxis))),
        lerp(10.0, 2.0, roughness));
    specular *= lerp(1, anisotropicBand * 1.8, abs(anisotropy));

    fixed4 highlight = operation.color;
    fixed4 shadow = operation.paintTransform0;
    fixed4 environment = operation.paintTransform1;
    half highlightWeight = lit * .72 + specular * lerp(.45, 1.15, metallic) + coat;
    half shadowWeight = shade * .72;
    half environmentWeight = fresnel * lerp(.12, .82, metallic);
    half coverage = reliefCoverage;

    if (responseKind == 2)
    {
        half satinCoordinate =
            dot(normal.xy, anisotropyAxis) * .34 +
            glyphLocal.y * .3 + .35;
        half broadSatin = pow(
            saturate(1 - abs(satinCoordinate - .56) * 2.5),
            lerp(1.35, .62, roughness));
        half satinBand = saturate(
            broadSatin * (.48 + .52 * lit) +
            anisotropicBand * .18);
        highlightWeight = satinBand * .86 + specular * .32 + coat * .18;
        shadowWeight = (1 - satinBand) * .34 + shade * .24;
        environmentWeight = satinBand * .24 + fresnel * .16;
        coverage = faceCoverage;
    }
    else if (responseKind == 3 || responseKind == 4)
    {
        half3 reflection = reflect(-viewDirection, normal);
        half reflectionCoordinate = saturate(
            .5 + reflection.y * .42 + (glyphLocal.y - .5) * .2);
        half primaryBand = pow(
            saturate(1 - abs(reflectionCoordinate - .7) *
                (responseKind == 3 ? 7.2 : 5.4)),
            1.35);
        half secondaryBand = pow(
            saturate(1 - abs(reflectionCoordinate - .28) *
                (responseKind == 3 ? 10.5 : 7.5)),
            1.7);
        half darkHorizon = pow(
            saturate(1 - abs(reflectionCoordinate - .49) * 8.5),
            1.25);
        half reflectionBand = saturate(
            primaryBand + secondaryBand * .48);
        highlightWeight =
            specular * 1.3 +
            reflectionBand * .82 +
            coat * .88;
        shadowWeight =
            darkHorizon * .76 +
            (1 - reflectionBand) * .2 +
            shade * .2;
        environmentWeight =
            .16 +
            primaryBand * .3 +
            secondaryBand * .16 +
            fresnel * .74;
        coverage = faceCoverage;
    }
    else if (responseKind == 5)
    {
        half enamelRoll = edgeProfile * (.2 + .8 * lit);
        highlightWeight = specular * .55 + coat * 1.18 + enamelRoll * .22;
        shadowWeight = shade * .24 + edgeProfile * .06;
        environmentWeight = fresnel * .46 + enamelRoll * .08;
        coverage = faceCoverage;
    }
    else if (responseKind == 6)
    {
        half3 opposingHalf = normalize(
            half3(-lightDirection.xy, max(.35, lightDirection.z)) +
            viewDirection);
        half secondarySpecular = pow(
            saturate(dot(normal, opposingHalf)),
            lerp(46.0, 9.0, roughness));
        half gelTransmission = pow(edgeProfile, .72);
        highlightWeight =
            specular * .84 +
            secondarySpecular * .34 +
            coat * 1.35 +
            fresnel * .55;
        shadowWeight = shade * .16 + gelTransmission * .08;
        environmentWeight =
            fresnel * .86 +
            gelTransmission * .3 +
            lit * .06;
        coverage = faceCoverage;
    }
    else if (responseKind == 7)
    {
        half edge = saturate(reliefCoverage * 1.35 + fresnel);
        highlightWeight = lit * .2 + specular * .5;
        shadowWeight = shade * .12;
        environmentWeight = edge * (1 + operation.parameters2.w);
        coverage = saturate(reliefCoverage + faceCoverage * .2);
    }
    else if (responseKind == 8)
    {
        half facet = TextStudioValueNoise(
            glyphLocal * 13.0 + operation.paintMetadata.z * .017);
        half crystalPlane = smoothstep(.45, .72, facet) * edgeProfile;
        highlightWeight =
            specular * 1.12 +
            coat +
            crystalPlane * lit * .42;
        shadowWeight = shade * .3 + (1 - facet) * edgeProfile * .12;
        environmentWeight = fresnel * .8 + facet * .2 + crystalPlane * .16;
        coverage = faceCoverage;
    }
    else if (responseKind == 9)
    {
        half transmissionRim = pow(edgeProfile, .68);
        half caustic = pow(
            saturate(1 - abs(dot(normal.xy, anisotropyAxis))),
            7.0);
        highlightWeight =
            specular * 1.32 +
            coat * 1.42 +
            caustic * transmissionRim * .26;
        shadowWeight = shade * .1 + transmissionRim * .045;
        environmentWeight =
            fresnel * 1.08 +
            transmissionRim * .42 +
            caustic * .12;
        coverage = faceCoverage;
    }
    else if (responseKind == 10)
    {
        half foilBand = pow(
            saturate(1 - abs(
                dot(normal.xy, anisotropyAxis) +
                (glyphLocal.y - .5) * .55)),
            lerp(3.2, 1.4, roughness));
        highlightWeight = specular * .78 + coat + foilBand * .32;
        shadowWeight = shade * .2 + (1 - foilBand) * .08;
        environmentWeight = .36 + fresnel * .76 + foilBand * .3;
        coverage = faceCoverage;
    }

    half bevelKey = edgeProfile * saturate(.32 + lit * .68);
    highlightWeight += bevelKey * lerp(.28, .14, roughness);
    shadowWeight += edgeProfile * shade * lerp(.18, .08, roughness);

    half iridescence = saturate(operation.parameters2.y);
    half spectrumPhase =
        dot(normal.xy, half2(.63, .77)) * .35 +
        glyphLocal.x * .38 - glyphLocal.y * .22 +
        operation.paintMetadata.z * .013;
    half3 spectrum = TextStudioStyleSpectrum(spectrumPhase);
    environment.rgb = lerp(environment.rgb, spectrum, iridescence);

    float sparkle = TextStudioStyleSparkle(
        glyphLocal,
        saturate(operation.parameters2.z),
        operation.paintMetadata.z,
        glyphFootprint);
    highlightWeight += sparkle * (1.2 + metallic);
    half emission = max(0, operation.parameters2.w);
    environmentWeight += emission * (.12 + reliefCoverage * .38);

    half highlightContribution = max(0, highlightWeight) * highlight.a;
    half shadowContribution = max(0, shadowWeight) * shadow.a;
    half environmentContribution = max(0, environmentWeight) * environment.a;
    half opacityScale = saturate(operation.layerMetadata.z) * sourceAlpha * coverage;
    half totalContribution =
        highlightContribution + shadowContribution + environmentContribution;
    half maximumOverlay = responseKind == 3 || responseKind == 4 || responseKind == 10
        ? .94
        : responseKind == 7
            ? .88
            : responseKind == 9
                ? .72
                : .82;
    half inverseContribution = totalContribution > .0001 ? rcp(totalContribution) : 0;
    float4 authored = float4((highlight.rgb * highlightContribution + shadow.rgb * shadowContribution +
        environment.rgb * environmentContribution) * inverseContribution, min(saturate(totalContribution), maximumOverlay));
    if (operation.layerMetadata.w > .5 && sourceAlpha > .0001) authored.rgb *= animatedTint.rgb / sourceAlpha;
    contributionCoverage = opacityScale;
    return authored;
}

#endif
