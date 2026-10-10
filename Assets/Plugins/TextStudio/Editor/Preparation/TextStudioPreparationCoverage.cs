using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text;
using TextStudio.Geometry;
using TMPro;

namespace TextStudio.Editor
{
    internal static class TextStudioPreparationCoverage
    {
        internal static string GlyphCharacters(string sample, TextStudioMarkupSyntax syntax = null, bool richText = true)
        {
            if (string.IsNullOrEmpty(sample)) return string.Empty;
            if (richText)
            {
                TextStudioPipeline pipeline = new TextStudioPipeline();
                pipeline.Process(new StringTextSource(sample), false, syntax);
                sample = pipeline.Plan.ResolvedText;
            }
            StringBuilder characters = new StringBuilder(sample.Length);
            for (int i = 0; i < sample.Length; i++)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(sample, i);
                bool pair = char.IsHighSurrogate(sample[i]) && i + 1 < sample.Length && char.IsLowSurrogate(sample[i + 1]);
                // Layout controls and inline-source placeholders do not require glyph geometry.
                if (!char.IsWhiteSpace(sample[i]) && category != UnicodeCategory.Control && category != UnicodeCategory.Format && sample[i] != '\uFFFC')
                    characters.Append(sample, i, pair ? 2 : 1);
                if (pair) i++;
            }
            return characters.ToString();
        }

        // TMP's last layout supplies substituted glyph IDs. Source cmap coverage additionally covers all
        // authored text, including pages and unrevealed characters, without populating a font atlas.
        internal static Dictionary<TMP_FontAsset, HashSet<uint>> Collect(TextStudioText hub, TMP_FontAsset onlyFont)
        {
            TMP_Text text = hub.TMPText;
            Dictionary<TMP_FontAsset, HashSet<uint>> result = new Dictionary<TMP_FontAsset, HashSet<uint>>();
            void Add(TMP_FontAsset font)
            {
                if (font == null || result.ContainsKey(font)) return;
                result.Add(font, new HashSet<uint>());
                if (font.fallbackFontAssetTable != null) foreach (TMP_FontAsset fallback in font.fallbackFontAssetTable) Add(fallback);
                if (font.fontWeightTable != null) foreach (TMP_FontWeightPair weight in font.fontWeightTable)
                { Add(weight.regularTypeface); Add(weight.italicTypeface); }
            }
            Add(onlyFont != null ? onlyFont : text.font);
            if (onlyFont == null)
            {
                if (TMP_Settings.fallbackFontAssets != null) foreach (TMP_FontAsset fallback in TMP_Settings.fallbackFontAssets) Add(fallback);
                Add(TMP_Settings.defaultFontAsset);
            }
            if (text.textInfo != null)
                for (int i = 0; i < text.textInfo.characterCount; i++)
                {
                    TMP_CharacterInfo character = text.textInfo.characterInfo[i];
                    if (character.elementType != TMP_TextElementType.Character || character.textElement == null || character.fontAsset == null ||
                        onlyFont != null && character.fontAsset != onlyFont) continue;
                    Add(character.fontAsset);
                    result[character.fontAsset].Add(character.textElement.glyphIndex);
                }
            List<string> samples = new List<string> { text.text ?? string.Empty, TextStudioRuntimePreparationSettings.instance.additionalText };
            TextStudioRuntimePreparation.CollectText(hub, samples);
            HashSet<int> scalars = new HashSet<int>();
            foreach (string characters in TextStudioRuntimeCharacters.Samples(TextStudioRuntimePreparationSettings.instance.runtimeCharacters))
                foreach (char character in characters)
                    if (!char.IsWhiteSpace(character) && !char.IsControl(character)) scalars.Add(character);
            foreach (string rawSample in samples)
            {
                string sample = GlyphCharacters(rawSample, hub.MarkupSyntaxOverride != null ? hub.MarkupSyntaxOverride : TextStudioProjectSettings.ActiveMarkupSyntax, text.richText);
                if (string.IsNullOrEmpty(sample)) continue;
                for (int i = 0; i < sample.Length; i++)
                {
                    if (char.IsHighSurrogate(sample[i]) && i + 1 < sample.Length && char.IsLowSurrogate(sample[i + 1]))
                        scalars.Add(char.ConvertToUtf32(sample[i], sample[++i]));
                    else if (!char.IsSurrogate(sample[i])) scalars.Add(sample[i]);
                }
            }
            Dictionary<TMP_FontAsset, ICurveFontFace> faces = new Dictionary<TMP_FontAsset, ICurveFontFace>();
            ICurveFontFace Face(TMP_FontAsset font)
            {
                if (faces.TryGetValue(font, out ICurveFontFace cached)) return cached;
                string source = TextStudioFontAppearancePreparation.ResolveSourcePath(font);
                ICurveFontFace face = !string.IsNullOrEmpty(source) && File.Exists(source)
                    ? FontCurveSourceCache.GetOrCreate(File.ReadAllBytes(source), TextStudioFontAppearancePreparation.ResolveFaceIndex(font))
                    : hub.FindAppearanceFont(font)?.GetCurveSource();
                faces.Add(font, face);
                return face;
            }
            foreach (KeyValuePair<TMP_FontAsset, HashSet<uint>> entry in result)
            {
                if (entry.Value.Count == 0) continue;
                ICurveFontFace face = Face(entry.Key);
                if (face != null) entry.Value.RemoveWhere(id => id >= face.GlyphCount || face.GetGlyph((int)id)?.ContourCount == 0);
            }
            HashSet<TMP_FontAsset> roots = new HashSet<TMP_FontAsset> { onlyFont != null ? onlyFont : text.font };
            foreach (KeyValuePair<TMP_FontAsset, HashSet<uint>> observed in result)
                if (observed.Value.Count > 0) roots.Add(observed.Key);
            bool styled = (text.fontStyle & (FontStyles.Bold | FontStyles.Italic)) != 0 || text.fontWeight != FontWeight.Regular ||
                samples.Any(sample => sample != null && (sample.Contains("<b") || sample.Contains("<i") || sample.Contains("<font-weight")));
            if (styled)
                foreach (TMP_FontAsset font in result.Keys)
                    if (font.fontWeightTable != null) foreach (TMP_FontWeightPair pair in font.fontWeightTable)
                    { if (pair.regularTypeface != null) roots.Add(pair.regularTypeface); if (pair.italicTypeface != null) roots.Add(pair.italicTypeface); }
            bool Resolve(TMP_FontAsset font, int scalar, HashSet<TMP_FontAsset> visited)
            {
                if (font == null || !visited.Add(font)) return false;
                if (result.ContainsKey(font))
                {
                    ICurveFontFace face = Face(font);
                    if (face != null && face.TryGetGlyphIndex(scalar, out int glyph))
                    {
                        if (face.GetGlyph(glyph)?.ContourCount > 0) result[font].Add((uint)glyph);
                        return true;
                    }
                    if (face == null && font.characterLookupTable != null && font.characterLookupTable.TryGetValue((uint)scalar, out TMP_Character character))
                    {
                        if (scalar > char.MaxValue || !char.IsWhiteSpace((char)scalar)) result[font].Add(character.glyphIndex);
                        return true;
                    }
                }
                if (font.fallbackFontAssetTable != null)
                    foreach (TMP_FontAsset fallback in font.fallbackFontAssetTable) if (Resolve(fallback, scalar, visited)) return true;
                return false;
            }
            HashSet<TMP_FontAsset> visited = new HashSet<TMP_FontAsset>();
            foreach (TMP_FontAsset root in roots)
                foreach (int scalar in scalars)
                {
                    visited.Clear();
                    if (Resolve(root, scalar, visited) || onlyFont != null) continue;
                    bool found = false;
                    if (TMP_Settings.fallbackFontAssets != null)
                        foreach (TMP_FontAsset fallback in TMP_Settings.fallbackFontAssets)
                            if (Resolve(fallback, scalar, visited)) { found = true; break; }
                    if (!found) Resolve(TMP_Settings.defaultFontAsset, scalar, visited);
                }
            foreach (TMP_FontAsset font in result.Keys.ToArray())
                if (result[font].Count == 0 || onlyFont != null && font != onlyFont) result.Remove(font);
            return result;
        }
    }
}
