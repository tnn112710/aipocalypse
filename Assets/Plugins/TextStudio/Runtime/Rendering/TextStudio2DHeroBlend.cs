using UnityEngine;

namespace TextStudio
{
    /// <summary>CPU reference for the premultiplied Hero compositor blend contract.</summary>
    internal static class TextStudio2DHeroBlend
    {
        const float MinimumAlpha = 0.000001f;

        internal static Vector4 Composite(
            Vector4 backdrop,
            Vector4 source,
            TextStudio2DBlendMode blendMode,
            float opacity = 1f)
        {
            float sourceOpacity = Mathf.Clamp01(opacity);
            source *= sourceOpacity;
            float sourceAlpha = Mathf.Clamp01(source.w);
            float backdropAlpha = Mathf.Clamp01(backdrop.w);
            Vector3 sourcePremultiplied = sourceAlpha > MinimumAlpha
                ? new Vector3(source.x, source.y, source.z)
                : Vector3.zero;
            Vector3 backdropPremultiplied = backdropAlpha > MinimumAlpha
                ? new Vector3(backdrop.x, backdrop.y, backdrop.z)
                : Vector3.zero;
            Vector3 sourceColor = sourceAlpha > MinimumAlpha
                ? sourcePremultiplied / sourceAlpha
                : Vector3.zero;
            Vector3 backdropColor = backdropAlpha > MinimumAlpha
                ? backdropPremultiplied / backdropAlpha
                : Vector3.zero;
            Vector3 blended = Blend(backdropColor, sourceColor, blendMode);
            float interaction = sourceAlpha * backdropAlpha;
            Vector3 premultiplied =
                backdropPremultiplied * (1f - sourceAlpha) +
                sourcePremultiplied * (1f - backdropAlpha) +
                blended * interaction;
            float alpha = sourceAlpha + backdropAlpha * (1f - sourceAlpha);
            return new Vector4(
                Mathf.Max(0f, premultiplied.x),
                Mathf.Max(0f, premultiplied.y),
                Mathf.Max(0f, premultiplied.z),
                Mathf.Clamp01(alpha));
        }

        internal static Vector3 Blend(
            Vector3 backdrop,
            Vector3 source,
            TextStudio2DBlendMode blendMode)
        {
            return new Vector3(
                BlendChannel(backdrop.x, source.x, blendMode),
                BlendChannel(backdrop.y, source.y, blendMode),
                BlendChannel(backdrop.z, source.z, blendMode));
        }

        internal static float BlendChannel(
            float backdrop,
            float source,
            TextStudio2DBlendMode blendMode)
        {
            if (blendMode == TextStudio2DBlendMode.Normal)
                return source;
            if (blendMode == TextStudio2DBlendMode.Add)
                return Mathf.Max(0f, backdrop + source);
            if (blendMode == TextStudio2DBlendMode.Subtract)
                return Mathf.Max(0f, backdrop - source);
            if (blendMode == TextStudio2DBlendMode.Difference)
                return Mathf.Abs(backdrop - source);
            if (blendMode == TextStudio2DBlendMode.Min)
                return Mathf.Min(backdrop, source);
            if (blendMode == TextStudio2DBlendMode.Max)
                return Mathf.Max(backdrop, source);

            float boundedBackdrop = Mathf.Clamp01(backdrop);
            float boundedSource = Mathf.Clamp01(source);
            switch (blendMode)
            {
                case TextStudio2DBlendMode.Multiply:
                    return boundedBackdrop * boundedSource;
                case TextStudio2DBlendMode.Screen:
                    return boundedBackdrop + boundedSource -
                           boundedBackdrop * boundedSource;
                case TextStudio2DBlendMode.Overlay:
                    return boundedBackdrop <= 0.5f
                        ? 2f * boundedBackdrop * boundedSource
                        : 1f - 2f * (1f - boundedBackdrop) *
                          (1f - boundedSource);
                case TextStudio2DBlendMode.SoftLight:
                    return SoftLight(boundedBackdrop, boundedSource);
                case TextStudio2DBlendMode.HardLight:
                    return boundedSource <= 0.5f
                        ? 2f * boundedBackdrop * boundedSource
                        : 1f - 2f * (1f - boundedBackdrop) *
                          (1f - boundedSource);
                case TextStudio2DBlendMode.ColorDodge:
                    if (boundedBackdrop <= 0f)
                        return 0f;
                    if (boundedSource >= 1f)
                        return 1f;
                    return Mathf.Min(1f, boundedBackdrop / (1f - boundedSource));
                case TextStudio2DBlendMode.ColorBurn:
                    if (boundedBackdrop >= 1f)
                        return 1f;
                    if (boundedSource <= 0f)
                        return 0f;
                    return 1f - Mathf.Min(
                        1f,
                        (1f - boundedBackdrop) / boundedSource);
                default:
                    return boundedSource;
            }
        }

        static float SoftLight(float backdrop, float source)
        {
            if (source <= 0.5f)
            {
                return backdrop -
                       (1f - 2f * source) * backdrop * (1f - backdrop);
            }

            float response = backdrop <= 0.25f
                ? ((16f * backdrop - 12f) * backdrop + 4f) * backdrop
                : Mathf.Sqrt(backdrop);
            return backdrop + (2f * source - 1f) * (response - backdrop);
        }
    }
}
