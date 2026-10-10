using UnityEngine;

namespace TextStudio
{
    /// <summary>Channel layout supplied by a flat-text backend for one glyph atlas.</summary>
    public enum TextStudioGlyphFieldFormat : byte
    {
        SingleDistance = 0,
        MultiDistance = 1
    }

    /// <summary>
    /// Explicit material contract for optional high-fidelity glyph fields. Multi Distance uses
    /// alpha for the Euclidean distance and the median RGB value for sharp corner distance.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class TextStudioGlyphFieldContract
    {
        public const string FormatProperty = "_TextStudioFieldFormat";
        public const string PixelsPerEmProperty = "_TextStudioFieldPixelsPerEm";
        public const string SafeDistanceProperty = "_TextStudioFieldSafeDistance";

        static readonly int s_formatId = Shader.PropertyToID(FormatProperty);
        static readonly int s_pixelsPerEmId = Shader.PropertyToID(PixelsPerEmProperty);
        static readonly int s_safeDistanceId = Shader.PropertyToID(SafeDistanceProperty);

        /// <summary>Reads the declared material field format, falling back to single-distance fields when no format property is available.</summary>
        public static TextStudioGlyphFieldFormat ResolveFormat(Material material)
        {
            if (material == null || !material.HasProperty(s_formatId))
                return TextStudioGlyphFieldFormat.SingleDistance;
            return material.GetFloat(s_formatId) >= 0.5f
                ? TextStudioGlyphFieldFormat.MultiDistance
                : TextStudioGlyphFieldFormat.SingleDistance;
        }

        /// <summary>Returns a positive material-declared field density, otherwise the fallback clamped to a nonnegative value.</summary>
        public static float ResolvePixelsPerEm(Material material, float fallback)
        {
            if (material != null && material.HasProperty(s_pixelsPerEmId))
            {
                float declared = material.GetFloat(s_pixelsPerEmId);
                if (declared > 0f)
                    return declared;
            }
            return Mathf.Max(0f, fallback);
        }

        /// <summary>Returns a positive material-declared safe distance in em units, otherwise the fallback clamped to a nonnegative value.</summary>
        public static float ResolveSafeDistanceEm(Material material, float fallback)
        {
            if (material != null && material.HasProperty(s_safeDistanceId))
            {
                float declared = material.GetFloat(s_safeDistanceId);
                if (declared > 0f)
                    return declared;
            }
            return Mathf.Max(0f, fallback);
        }
    }
}
