using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    [CustomEditor(typeof(TextStudioFontAppearanceData))]
    internal sealed class TextStudioFontAppearanceDataEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            TextStudioFontAppearanceData data = (TextStudioFontAppearanceData)target;
            VisualElement root = TextStudioPreparedDataInspector.Root("ts-font-appearance-data");
            bool valid = TextStudioFontAppearanceBuildValidation.ValidateAsset(data, out string error);
            root.Add(TextStudioPreparedDataInspector.Summary("Prepared font appearance", valid, error, "Reusable font shapes and detail reduce work when your text appears. Your original font atlas stays unchanged."));
            HashSet<uint> glyphs = new HashSet<uint>();
            long bytes = 0;
            for (int index = 0; index < data.TierCount; index++)
            {
                TextStudioPreparedFieldTier tier = data.GetTier(index);
                if (tier == null) continue;
                bytes += tier.TextureBytes;
                for (int glyph = 0; glyph < tier.GlyphCount; glyph++) glyphs.Add(tier.GetGlyph(glyph).GlyphIndex);
            }
            root.Add(TextStudioPreparedDataInspector.Metrics(("Prepared glyphs", glyphs.Count.ToString("N0"), "Distinct glyphs with prepared texture fields across all detail levels. Embedded outlines may cover additional glyphs."), ("Detail levels", data.TierCount.ToString(), "Saved resolutions available for quality selection."), ("Field textures", EditorUtility.FormatBytes(bytes), "Estimated uncompressed field texture payload. This excludes font bytes, metadata, and runtime caches.")));
            root.Add(TextStudioInspectorElements.CreateSection("Source font", "prepared-font-source", TextStudioPreparedDataInspector.Source("Font", data.Font), TextStudioPreparedDataInspector.Copy(data.SourceName)));
            if (data.TierCount == 0) root.Add(TextStudioPreparedDataInspector.Copy("Font outlines are available without texture fields. Additional detail is prepared when an appearance needs it."));
            for (int index = 0; index < data.TierCount; index++)
            {
                TextStudioPreparedFieldTier tier = data.GetTier(index);
                if (tier == null) continue;
                Foldout level = TextStudioPreparedDataInspector.Details("Detail level " + (index + 1) + " · " + tier.GlyphCount + " glyphs", "prepared-font-tier-" + index);
                level.Add(TextStudioPreparedDataInspector.Copy("Resolution: " + tier.PixelsPerEm.ToString("0.#") + " pixels per em"));
                level.Add(TextStudioPreparedDataInspector.Copy("Effect reach: " + tier.SafeDistanceEm.ToString("0.###") + " em on either side of the edge"));
                level.Add(TextStudioPreparedDataInspector.Copy(tier.PageCount + " texture pages · " + EditorUtility.FormatBytes(tier.TextureBytes)));
                root.Add(level);
            }
            Foldout details = TextStudioPreparedDataInspector.Details("Source details", "prepared-font-details");
            details.Add(TextStudioPreparedDataInspector.Copy("Embedded font: " + EditorUtility.FormatBytes(data.SourceBytes)));
            details.Add(TextStudioPreparedDataInspector.Copy("Font face: " + data.FaceIndex));
            details.Add(TextStudioPreparedDataInspector.Copy("Generator: " + data.Generator));
            details.Add(TextStudioPreparedDataInspector.Copy("Source hash: " + data.SourceHash)); root.Add(details);
            TextStudioPreparedDataInspector.Storage(root, data);
            return root;
        }
    }
}
