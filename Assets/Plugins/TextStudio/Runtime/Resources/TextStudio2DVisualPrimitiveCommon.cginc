#ifndef TEXT_STUDIO_2D_VISUAL_PRIMITIVE_COMMON_INCLUDED
#define TEXT_STUDIO_2D_VISUAL_PRIMITIVE_COMMON_INCLUDED

#define TEXT_STUDIO_VISUAL_MINIMUM_POSITIVE .000001

half TextStudioVisualMotionRate(
    int motionPolicy,
    int reducedMotionBehavior,
    out half enabled)
{
    if (motionPolicy == 0)
        motionPolicy = 1;
    if (motionPolicy == 1 || reducedMotionBehavior == 0)
    {
        enabled = 1;
        return 1;
    }
    if (motionPolicy == 2 && reducedMotionBehavior == 1)
    {
        enabled = 1;
        return .25;
    }

    enabled = 0;
    return 0;
}

float2 TextStudioVisualNormalizeDirection(float2 direction)
{
    float magnitude = length(direction);
    return magnitude > .0001 ? direction / magnitude : float2(1, 0);
}

half TextStudioVisualSweepBand(
    half distance,
    half innerWidth,
    half outerWidth)
{
    half inner = max(0, innerWidth);
    half outer = max(inner + TEXT_STUDIO_VISUAL_MINIMUM_POSITIVE, outerWidth);
    return 1 - smoothstep(inner, outer, abs(distance));
}

half TextStudioVisualThresholdAbove(
    half sampleValue,
    half threshold,
    half softness)
{
    half extent = max(TEXT_STUDIO_VISUAL_MINIMUM_POSITIVE, abs(softness));
    return smoothstep(threshold - extent, threshold + extent, sampleValue);
}

#endif
