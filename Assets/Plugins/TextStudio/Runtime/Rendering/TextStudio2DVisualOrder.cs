using System;
using System.Collections.Generic;

namespace TextStudio
{
    /// <summary>Atomic text units follow their first source in backend draw order, including space-owned surfaces.</summary>
    internal static class TextStudio2DVisualOrder
    {
        internal static void Build(in TextStudioVisualSnapshot snapshot, List<int> destination, ref bool[] seen)
        {
            destination.Clear();
            if (seen.Length < snapshot.GlyphCount)
                seen = new bool[TextStudioBufferCapacity.ForCount(snapshot.GlyphCount)];
            Array.Clear(seen, 0, snapshot.GlyphCount);
            for (int index = 0; index < snapshot.RenderGlyphCount; index++)
            {
                ref readonly TextStudioVisualGlyph glyph = ref snapshot.RenderGlyphAt(index);
                int unit = glyph.TextUnitIndex;
                if (unit < 0 || unit >= snapshot.GlyphCount || seen[unit]) continue;
                seen[unit] = true;
                destination.Add(unit);
            }
        }
    }
}
