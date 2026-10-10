using UnityEngine;

namespace TextStudio
{
    /// <summary>Canonical mapping from prepared Writing design space into shaped visual space.</summary>
    internal static class TextStudioWritingPathMapping
    {
        internal const float MinimumExtent = 0.0001f;

        internal static Vector2 MapRect(
            Vector2 point,
            Rect source,
            Vector2 targetMinimum,
            Vector2 targetMaximum)
        {
            float x = Mathf.Abs(source.width) > MinimumExtent
                ? (point.x - source.xMin) / source.width
                : 0.5f;
            float y = Mathf.Abs(source.height) > MinimumExtent
                ? (point.y - source.yMin) / source.height
                : 0.5f;
            return new Vector2(
                Mathf.LerpUnclamped(targetMinimum.x, targetMaximum.x, x),
                Mathf.LerpUnclamped(targetMinimum.y, targetMaximum.y, y));
        }

        internal static void ResolveGlyphBounds(
            in TextStudioFrame frame,
            int glyphIndex,
            out Vector2 minimum,
            out Vector2 maximum)
        {
            TextStudioVisualGlyph glyph =
                frame.VisualSnapshot.GetGlyph(glyphIndex);
            minimum = glyph.BoundsMin;
            maximum = glyph.BoundsMax;
            if (maximum.x - minimum.x < MinimumExtent)
            {
                minimum.x = Mathf.Min(
                    glyph.Origin.x,
                    glyph.Origin.x + glyph.Advance);
                maximum.x = Mathf.Max(
                    glyph.Origin.x,
                    glyph.Origin.x + glyph.Advance);
            }
            if (maximum.y - minimum.y < MinimumExtent)
            {
                float scale = frame.GetEffectUnitScale(glyphIndex);
                minimum.y = glyph.Baseline - scale * 0.2f;
                maximum.y = glyph.Baseline + scale * 0.8f;
            }
        }

        internal static int FindNearestGlyph(
            in TextStudioVisualSnapshot snapshot,
            Vector2 position)
        {
            int best = 0;
            float bestDistance = float.PositiveInfinity;
            for (int glyphIndex = 0;
                 glyphIndex < snapshot.GlyphCount;
                 glyphIndex++)
            {
                TextStudioVisualGlyph glyph = snapshot.GetGlyph(glyphIndex);
                Vector2 closest = new Vector2(
                    Mathf.Clamp(position.x, glyph.BoundsMin.x, glyph.BoundsMax.x),
                    Mathf.Clamp(position.y, glyph.BoundsMin.y, glyph.BoundsMax.y));
                float distance = (position - closest).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;
                bestDistance = distance;
                best = glyphIndex;
            }
            return best;
        }

        internal static float Normalize(
            float value,
            float minimum,
            float maximum)
        {
            return (value - minimum) /
                   Mathf.Max(MinimumExtent, maximum - minimum);
        }
    }
}
