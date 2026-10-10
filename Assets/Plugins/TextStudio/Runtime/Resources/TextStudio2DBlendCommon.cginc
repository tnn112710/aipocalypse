#ifndef TEXT_STUDIO_BLEND_COMMON_INCLUDED
#define TEXT_STUDIO_BLEND_COMMON_INCLUDED
half TextStudioSoftLight(half backdrop, half source)
{
    if (source <= .5)
    {
        return backdrop -
            (1 - 2 * source) * backdrop * (1 - backdrop);
    }
    half response = backdrop <= .25
        ? ((16 * backdrop - 12) * backdrop + 4) * backdrop
        : sqrt(backdrop);
    return backdrop + (2 * source - 1) * (response - backdrop);
}

half TextStudioBlendChannel(
    half backdrop,
    half source,
    int blendMode)
{
    if (blendMode == 0)
        return source;
    if (blendMode == 1)
        return max(0, backdrop + source);
    if (blendMode == 10)
        return max(0, backdrop - source);
    if (blendMode == 9)
        return abs(backdrop - source);
    if (blendMode == 11)
        return min(backdrop, source);
    if (blendMode == 12)
        return max(backdrop, source);

    half boundedBackdrop = saturate(backdrop);
    half boundedSource = saturate(source);
    if (blendMode == 2)
        return boundedBackdrop * boundedSource;
    if (blendMode == 3)
    {
        return boundedBackdrop + boundedSource -
            boundedBackdrop * boundedSource;
    }
    if (blendMode == 4)
    {
        return boundedBackdrop <= .5
            ? 2 * boundedBackdrop * boundedSource
            : 1 - 2 * (1 - boundedBackdrop) *
              (1 - boundedSource);
    }
    if (blendMode == 5)
        return TextStudioSoftLight(boundedBackdrop, boundedSource);
    if (blendMode == 6)
    {
        return boundedSource <= .5
            ? 2 * boundedBackdrop * boundedSource
            : 1 - 2 * (1 - boundedBackdrop) *
              (1 - boundedSource);
    }
    if (blendMode == 7)
    {
        if (boundedBackdrop <= 0)
            return 0;
        if (boundedSource >= 1)
            return 1;
        return min(1, boundedBackdrop / max(.000001, 1 - boundedSource));
    }
    if (blendMode == 8)
    {
        if (boundedBackdrop >= 1)
            return 1;
        if (boundedSource <= 0)
            return 0;
        return 1 - min(1, (1 - boundedBackdrop) / max(.000001, boundedSource));
    }
    return boundedSource;
}

half3 TextStudioBlend(
    half3 backdrop,
    half3 source,
    int blendMode)
{
    return half3(
        TextStudioBlendChannel(backdrop.r, source.r, blendMode),
        TextStudioBlendChannel(backdrop.g, source.g, blendMode),
        TextStudioBlendChannel(backdrop.b, source.b, blendMode));
}

half4 TextStudioBlendPremultiplied(half4 backdrop, half4 source, int blendMode)
{
    half sourceAlpha = saturate(source.a);
    half backdropAlpha = saturate(backdrop.a);
    source.rgb = sourceAlpha > .000001 ? source.rgb : 0;
    backdrop.rgb = backdropAlpha > .000001 ? backdrop.rgb : 0;
    half3 sourceColor = source.rgb / max(sourceAlpha, .000001);
    half3 backdropColor = backdrop.rgb / max(backdropAlpha, .000001);
    half3 blended = TextStudioBlend(backdropColor, sourceColor, blendMode);
    half3 rgb = backdrop.rgb * (1 - sourceAlpha) + source.rgb * (1 - backdropAlpha) + blended * (sourceAlpha * backdropAlpha);
    return half4(max(0, rgb), saturate(sourceAlpha + backdropAlpha * (1 - sourceAlpha)));
}

#endif
