using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ImpossibleRobert.Common;
using TMPro;
using UnityEngine;

namespace TextStudio
{
    internal sealed partial class TextStudioTMPBridge
    {
        // Where a TMP line of the last laid out text starts in each pass of RunLayout, and the state those passes carry into it.
        struct LineResumePoint
        {
            // First unit of the line, or -1 when the unit pass met none.
            internal int Unit;
            internal float AutoFitGlyphMargin;
            // First TMP character of the line, or -1 when the record pass met none, with the record pass state before it.
            internal int Character;
            internal int LayoutFingerprint;
            internal VisualVersionHash Geometry;
            internal VisualVersionHash Atlas;
            internal int RenderedGlyphCount;
            // TMP's own placement of the line's first character, which an append that keeps the line leaves unchanged.
            internal float Origin;
            internal float Baseline;
        }

        // The resume point of every TMP line of the last laid out text, indexed by TMP line number.
        LineResumePoint[] _lineResumePoints = Array.Empty<LineResumePoint>();
        int _lineResumePointCount;
        // The layout the last pass published and what it depended on; an append layout requires all of it unchanged.
        TextStudioLayout _appendLayoutTarget;
        int _appendLayoutRevision = -1;
        uint _appendLayoutSourceVersion;
        int _appendLayoutGlyphCount;
        int _appendLayoutSettings;
        uint _appendLayoutMaterials;
        int _appendLayoutAtlases;
        // Fonts the last laid out text used, whose atlas updates change the atlas version of every glyph they hold.
        readonly List<TMP_FontAsset> _appendLayoutFonts = new List<TMP_FontAsset>();

        /// <summary>Layout passes that resumed after the TMP lines an append kept.</summary>
        internal int AppendLayoutCountForDebug { get; private set; }

        /// <summary>Why the last layout laid out in full, or null when it resumed.</summary>
        internal string AppendLayoutRejectionForDebug { get; private set; }

        void InvalidateAppendLayout()
        {
            _appendLayoutTarget = null;
            _appendLayoutRevision = -1;
            _lineResumePointCount = 0;
        }

        // Settings that place every TMP line. TMP lays out text in order, so an append moves no line before the last line it
        // kept while these hold: top alignment anchors the first line, per-line horizontal alignment reads only its own line,
        // and nothing sizes, truncates, pages or reverses the whole text.
        static bool AllowsAppendLayout(TMP_Text text)
        {
            if (text.verticalAlignment != VerticalAlignmentOptions.Top)
                return false;
            HorizontalAlignmentOptions horizontal = text.horizontalAlignment;
            if (horizontal != HorizontalAlignmentOptions.Left &&
                horizontal != HorizontalAlignmentOptions.Center &&
                horizontal != HorizontalAlignmentOptions.Right)
            {
                return false;
            }
            TextOverflowModes overflow = text.overflowMode;
            return (overflow == TextOverflowModes.Overflow ||
                    overflow == TextOverflowModes.Masking ||
                    overflow == TextOverflowModes.ScrollRect) &&
                   !text.enableAutoSizing &&
                   !text.isRightToLeftText &&
                   text.geometrySortingOrder == VertexSortingOrder.Normal &&
                   text.textPreprocessor == null &&
                   text.firstVisibleCharacter == 0 &&
                   text.maxVisibleCharacters == 99999 &&
                   text.maxVisibleWords == 99999 &&
                   text.maxVisibleLines == 99999;
        }

        // Every other input of TMP's line placement and of the records RunLayout publishes per glyph.
        int ComputeAppendLayoutSettings(
            TextStudioCompiledPlan plan,
            bool cacheVisualSourceData,
            bool requiresExternalMaterial,
            TextStudioSourceContextRequirements externalSourceRequirements,
            CompiledTextStudio2DStyle appearanceStyle,
            CompiledTextStudioWritingStyle writingStyle,
            CompiledTextStudioAppearancePlan appearancePlan)
        {
            TMP_Text text = _tmpText;
            RectTransform rectTransform = text.rectTransform;
            int hash = 17;
            hash = CombineHash(hash, text.font != null ? text.font.GetStableId() : 0);
            hash = CombineHash(hash, text.fontSize.GetHashCode());
            hash = CombineHash(hash, (int)text.fontStyle);
            hash = CombineHash(hash, (int)text.fontWeight);
            hash = CombineHash(hash, (int)text.horizontalAlignment);
            hash = CombineHash(hash, WrappingMode(text));
            hash = CombineHash(hash, (int)text.overflowMode);
            hash = CombineHash(hash, text.margin.GetHashCode());
            hash = CombineHash(hash, rectTransform.rect.size.GetHashCode());
            hash = CombineHash(hash, rectTransform.pivot.GetHashCode());
            hash = CombineHash(hash, text.characterSpacing.GetHashCode());
            hash = CombineHash(hash, text.wordSpacing.GetHashCode());
            hash = CombineHash(hash, text.lineSpacing.GetHashCode());
            hash = CombineHash(hash, text.lineSpacingAdjustment.GetHashCode());
            hash = CombineHash(hash, text.paragraphSpacing.GetHashCode());
            hash = CombineHash(hash, text.richText ? 1 : 0);
            hash = CombineHash(hash, text.parseCtrlCharacters ? 1 : 0);
            hash = CombineHash(hash, text.extraPadding ? 1 : 0);
            hash = CombineHash(hash, text.isOrthographic ? 1 : 0);
            hash = CombineHash(hash, text.spriteAsset != null ? text.spriteAsset.GetStableId() : 0);
            hash = CombineHash(hash, text.styleSheet != null ? text.styleSheet.GetStableId() : 0);
            TMP_Style textStyle = text.textStyle;
            hash = CombineHash(hash, textStyle != null ? textStyle.hashCode : 0);
            // The records of the kept glyphs hold TMP's vertex colors.
            hash = CombineHash(hash, text.color.GetHashCode());
            hash = CombineHash(hash, text.overrideColorTags ? 1 : 0);
            hash = CombineHash(hash, text.tintAllSprites ? 1 : 0);
            hash = CombineHash(hash, text.enableVertexGradient ? 1 : 0);
            if (text.enableVertexGradient)
            {
                TMP_ColorGradient preset = text.colorGradientPreset;
                if (preset != null)
                {
                    hash = CombineHash(hash, preset.GetStableId());
                    hash = CombineHash(hash, (int)preset.colorMode);
                    hash = CombineGradient(hash, preset.topLeft, preset.topRight, preset.bottomLeft, preset.bottomRight);
                }
                else
                {
                    VertexGradient gradient = text.colorGradient;
                    hash = CombineGradient(hash, gradient.topLeft, gradient.topRight, gradient.bottomLeft, gradient.bottomRight);
                }
            }
#if UNITY_2023_2_OR_NEWER
            List<UnityEngine.TextCore.OTL_FeatureTag> features = text.fontFeatures;
            int featureCount = features != null ? features.Count : -1;
            hash = CombineHash(hash, featureCount);
            for (int index = 0; index < featureCount; index++)
                hash = CombineHash(hash, (int)features[index]);
#else
            hash = CombineHash(hash, text.enableKerning ? 1 : 0);
#endif
            hash = CombineHash(hash, plan.IsLiteralMarkup ? 1 : 0);
            hash = CombineHash(hash, cacheVisualSourceData ? 1 : 0);
            hash = CombineHash(hash, requiresExternalMaterial ? 1 : 0);
            hash = CombineHash(hash, (int)externalSourceRequirements);
            hash = CombineHash(hash, (int)plan.SourceContextRequirements);
            hash = CombineHash(hash, RuntimeHelpers.GetHashCode(appearanceStyle));
            hash = CombineHash(hash, RuntimeHelpers.GetHashCode(writingStyle));
            // A hub compiles its appearance plan again whenever an append adds units. A plan without invocations applies only its
            // default style (TextStudioAppearancePlanCompiler.CanReuseDefaultOnly), so it stamps that style and its dependencies;
            // a plan with scoped appearances stamps itself.
            if (appearancePlan == null)
            {
                hash = CombineHash(hash, 0);
            }
            else if (appearancePlan.HasInvocations)
            {
                hash = CombineHash(hash, 1);
                hash = CombineHash(hash, RuntimeHelpers.GetHashCode(appearancePlan));
            }
            else
            {
                hash = CombineHash(hash, 2);
                hash = CombineHash(hash, RuntimeHelpers.GetHashCode(appearancePlan.DefaultStyle));
                hash = CombineHash(hash, appearancePlan.DependencyRevision.GetHashCode());
            }
            hash = CombineHash(hash, _materialEffectAdapter.HasPreparedState ? 1 : 0);
            hash = CombineHash(hash, _materialEffectAdapter.UsesTextMeshProRoute ? 1 : 0);
            return hash;
        }

        static int CombineGradient(int hash, Color topLeft, Color topRight, Color bottomLeft, Color bottomRight)
        {
            hash = CombineHash(hash, topLeft.GetHashCode());
            hash = CombineHash(hash, topRight.GetHashCode());
            hash = CombineHash(hash, bottomLeft.GetHashCode());
            return CombineHash(hash, bottomRight.GetHashCode());
        }

        static int WrappingMode(TMP_Text text)
        {
#if UNITY_2023_2_OR_NEWER
            return (int)text.textWrappingMode;
#else
            return text.enableWordWrapping ? 1 : 0;
#endif
        }

        int ComputeAppendLayoutAtlases()
        {
            int hash = 17;
            for (int index = 0; index < _appendLayoutFonts.Count; index++)
            {
                TMP_FontAsset font = _appendLayoutFonts[index];
                Texture2D[] atlases = font != null ? font.atlasTextures : null;
                hash = CombineHash(hash, atlases != null ? atlases.Length : -1);
                if (atlases == null)
                    continue;
                for (int atlas = 0; atlas < atlases.Length; atlas++)
                    hash = CombineHash(hash, atlases[atlas] != null ? unchecked((int)atlases[atlas].updateCount) : 0);
            }
            return hash;
        }

        void NoteAppendLayoutFont(TMP_FontAsset font)
        {
            for (int index = 0; index < _appendLayoutFonts.Count; index++)
            {
                if (ReferenceEquals(_appendLayoutFonts[index], font))
                    return;
            }
            _appendLayoutFonts.Add(font);
        }

        // Finds the TMP line an append layout resumes at: the line of the last glyph the append kept, laid out by the last pass,
        // whose lines before it this pass must reproduce. Returns false when the text needs a full layout.
        bool TryResumeAppendLayout(
            TextStudioCompiledPlan plan,
            TextStudioLayout layout,
            TextStudioKeptGlyphPrefix keptPrefix,
            TMP_TextInfo textInfo,
            int glyphCount,
            int settings,
            uint materials,
            out int resumeLine)
        {
            resumeLine = -1;
            int kept = keptPrefix.GlyphCount;
            if (!keptPrefix.IsDefined)
                return RejectAppendLayout("no kept prefix");
            if (!ReferenceEquals(layout, _appendLayoutTarget) || layout.Revision != _appendLayoutRevision)
                return RejectAppendLayout("no recorded layout");
            if (keptPrefix.BaseSourceVersion != _appendLayoutSourceVersion)
                return RejectAppendLayout("kept text not laid out last");
            if (kept > _appendLayoutGlyphCount || kept > glyphCount || layout.GlyphCount != _appendLayoutGlyphCount)
                return RejectAppendLayout("glyph counts");
            if (settings != _appendLayoutSettings)
                return RejectAppendLayout("settings changed");
            if (materials != _appendLayoutMaterials)
                return RejectAppendLayout("materials changed");
            if (_fontSourceCount != 0 || _colorSourceCount != 0)
                return RejectAppendLayout("font or color sources");
            if (ComputeAppendLayoutAtlases() != _appendLayoutAtlases)
                return RejectAppendLayout("font atlas changed");

            int line = layout.VisualGlyphs[kept - 1].LineIndex;
            if (line <= 0 || line >= _lineResumePointCount || line >= textInfo.lineCount)
                return RejectAppendLayout("kept line");
            ref readonly LineResumePoint resume = ref _lineResumePoints[line];
            if (resume.Unit <= 0 || resume.Unit >= kept || resume.Character <= 0 || resume.Character >= textInfo.characterCount)
                return RejectAppendLayout("kept line start");

            // Every kept line must still start where it started, at the same TMP placement.
            TMP_LineInfo[] lines = textInfo.lineInfo;
            TMP_CharacterInfo[] characters = textInfo.characterInfo;
            for (int index = 0; index <= line; index++)
            {
                ref readonly LineResumePoint point = ref _lineResumePoints[index];
                if (point.Character < 0 || lines[index].firstCharacterIndex != point.Character)
                    return RejectAppendLayout("kept line moved");
                ref readonly TMP_CharacterInfo first = ref characters[point.Character];
                if (first.lineNumber != index ||
                    BitConverter.SingleToInt32Bits(first.origin) != BitConverter.SingleToInt32Bits(point.Origin) ||
                    BitConverter.SingleToInt32Bits(first.baseLine) != BitConverter.SingleToInt32Bits(point.Baseline))
                {
                    return RejectAppendLayout("kept line moved");
                }
            }

            resumeLine = line;
            AppendLayoutRejectionForDebug = null;
            return true;
        }

        bool RejectAppendLayout(string reason)
        {
            AppendLayoutRejectionForDebug = reason;
            return false;
        }

        void EnsureLineResumePointCapacity(int lineCount)
        {
            if (_lineResumePoints.Length < lineCount)
                Array.Resize(ref _lineResumePoints, TextStudioBufferCapacity.ForCount(lineCount));
        }

        void RecordAppendLayoutState(
            TextStudioCompiledPlan plan,
            TextStudioLayout layout,
            int glyphCount,
            int settings,
            uint materials)
        {
            _appendLayoutTarget = layout;
            _appendLayoutRevision = layout.Revision;
            _appendLayoutSourceVersion = plan.SourceVersion;
            _appendLayoutGlyphCount = glyphCount;
            _appendLayoutSettings = settings;
            _appendLayoutMaterials = materials;
            _appendLayoutAtlases = ComputeAppendLayoutAtlases();
        }
    }
}
