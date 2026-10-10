#ifndef TEXT_STUDIO_NOISE_COMMON_INCLUDED
#define TEXT_STUDIO_NOISE_COMMON_INCLUDED
half TextStudioHash21(float2 value)
{
    return frac(sin(dot(value, float2(12.9898, 78.233))) * 43758.5453);
}

half TextStudioValueNoise(float2 position)
{
    float2 cell = floor(position);
    float2 fraction = frac(position);
    float2 smoothFraction = fraction * fraction * (3.0 - 2.0 * fraction);
    half bottom = lerp(
        TextStudioHash21(cell),
        TextStudioHash21(cell + float2(1, 0)),
        smoothFraction.x);
    half top = lerp(
        TextStudioHash21(cell + float2(0, 1)),
        TextStudioHash21(cell + float2(1, 1)),
        smoothFraction.x);
    return lerp(bottom, top, smoothFraction.y);
}

#endif
