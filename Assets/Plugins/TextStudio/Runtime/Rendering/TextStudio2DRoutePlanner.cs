using TMPro;

namespace TextStudio
{
    /// <summary>How the shared 2D renderer executes one bound appearance program.</summary>
    public enum TextStudio2DRoute : byte
    {
        /// <summary>No appearance: the source renderer draws unchanged.</summary>
        PassThrough = 0,
        /// <summary>The source glyph quads draw with one shared material, Text Studio's field material or TextMeshPro's own; no extra geometry or pass.</summary>
        NativeField = 1,
        /// <summary>Text Studio draws ordered primitives directly into the active target; no offscreen pass.</summary>
        OrderedPrimitives = 2,
        /// <summary>Isolated offscreen composition, required by a genuine image dependency.</summary>
        Composited = 3
    }

    /// <summary>The first condition that selected a route. Diagnostics report it verbatim.</summary>
    public enum TextStudio2DRouteReason : byte
    {
        None = 0,
        /// <summary>Every operation fits the source quad padding and one fused program.</summary>
        CompactField = 1,
        /// <summary>Shadow or depth translation, or depth taper, reaches beyond the source quad padding.</summary>
        GeometrySupport = 2,
        /// <summary>Bevel, depth, material, graphic or animated effect rows draw as single-operation primitives.</summary>
        IsolatedOperations = 3,
        /// <summary>More than eight operations draw as consecutive fused spans.</summary>
        OperationSpans = 4,
        /// <summary>Retained decoration, stroke or depth geometry draws in painter order with the field rows.</summary>
        RetainedGeometry = 5,
        /// <summary>A <see cref="TextStudioWorldBatchGroup"/> draws the text's primitives in its shared shards.</summary>
        WorldBatchGroup = 6,
        /// <summary>Scoped appearance occurrences draw their programs as ordered primitives.</summary>
        ScopedAppearance = 7,
        /// <summary>A text engine's published glyphs draw as ordered primitives in place of its native output.</summary>
        EngineOwnedOutput = 8,
        /// <summary>A TextMeshPro-expressible style draws through the text's own TextMeshPro SDF material, one pass per glyph.</summary>
        TextMeshProMaterial = 9,
        /// <summary>An exact lens magnifier renders the text in isolation.</summary>
        LensMagnifier = 16,
        /// <summary>An operation explicitly requests the compositor.</summary>
        CompositorExecution = 17,
        /// <summary>A non-normal blend reads the composed backdrop.</summary>
        BlendMode = 18,
        /// <summary>Groups or masks need an isolated surface.</summary>
        GroupOrMask = 19,
        /// <summary>Glyph-major painter order interleaves every operation per glyph across overlapping glyphs.</summary>
        GlyphMajorOrder = 20,
        /// <summary>Pixel-unit depth needs target-pixel coverage.</summary>
        PixelUnitDepth = 21,
        /// <summary>Inline sprites or media are composited with the styled text.</summary>
        InlineMedia = 22,
        /// <summary>A sampled field reach exceeds the available signed-distance range.</summary>
        FieldRange = 23,
        /// <summary>Scoped appearance opacity or tint applies to the composed occurrence as a group.</summary>
        OccurrenceOpacity = 24,
        /// <summary>An operation cannot run as a direct primitive.</summary>
        UnsupportedDirectOperation = 25,
        /// <summary>A native source face layered with other rows keeps its complete native contribution in isolation.</summary>
        NativeFaceLayering = 26,
        /// <summary>The text engine keeps its native output because engine-owned output cannot draw this route yet.</summary>
        EngineKeepsNativeOutput = 27
    }

    internal readonly struct TextStudio2DRouteDecision
    {
        internal readonly TextStudio2DRoute Route;
        internal readonly TextStudio2DRouteReason Reason;
        internal readonly TextStudio2DFieldPaddingTier PaddingTier;

        internal TextStudio2DRouteDecision(TextStudio2DRoute route, TextStudio2DRouteReason reason, TextStudio2DFieldPaddingTier paddingTier)
        {
            Route = route;
            Reason = reason;
            PaddingTier = paddingTier;
        }

        internal bool IsComposited => Route == TextStudio2DRoute.Composited;
        internal bool OwnsPrimitives => Route == TextStudio2DRoute.OrderedPrimitives;
    }

    /// <summary>
    /// Single route owner for bound appearance programs. Offscreen composition is selected only for a genuine image
    /// dependency; geometry reach, operation count and isolated effect families stay on direct ordered primitives.
    /// </summary>
    internal static class TextStudio2DRoutePlanner
    {
        /// <param name="forcedComposition">A condition outside the program, such as an exact lens or scoped opacity, that already requires composition.</param>
        internal static TextStudio2DRouteDecision Resolve(CompiledTextStudio2DStyle style, TMP_Text source,
            TextStudio2DRouteReason forcedComposition = TextStudio2DRouteReason.None)
        {
            if (forcedComposition != TextStudio2DRouteReason.None) return Composited(forcedComposition);
            if (style == null || !style.IsValid || style.IsPassThrough)
                return new TextStudio2DRouteDecision(TextStudio2DRoute.PassThrough, TextStudio2DRouteReason.None, TextStudio2DFieldPaddingTier.None);
            TextStudio2DRouteReason image = TextStudio2DHeroProgram.ResolveHeroReason(style);
            if (image != TextStudio2DRouteReason.None) return Composited(image);
            if (HasInlineMedia(source)) return Composited(TextStudio2DRouteReason.InlineMedia);
            if (!TextStudio2DFieldPadding.TryResolveFused(style, out TextStudio2DFieldPaddingTier tier, out bool extendsGeometry, source))
                return Composited(TextStudio2DRouteReason.FieldRange);
            TextStudio2DRouteReason ordered =
                extendsGeometry ? TextStudio2DRouteReason.GeometrySupport :
                style.OperationCount > TextStudio2DStyleTable.MaximumFusedOperations ? TextStudio2DRouteReason.OperationSpans :
                TextStudio2DPrimitiveSupport.HasIsolatedOperations(style) ? TextStudio2DRouteReason.IsolatedOperations :
                TextStudio2DRouteReason.None;
            if (ordered == TextStudio2DRouteReason.None)
                return new TextStudio2DRouteDecision(TextStudio2DRoute.NativeField, TextStudio2DRouteReason.CompactField, tier);
            // Ordered primitives expand around the tight glyph themselves. Keep the source quad
            // at its native padding so atlas isolation does not count the field envelope twice.
            return TextStudio2DOrderedRenderer.Supports(style)
                ? new TextStudio2DRouteDecision(TextStudio2DRoute.OrderedPrimitives, ordered, TextStudio2DFieldPaddingTier.None)
                : Composited(TextStudio2DRouteReason.UnsupportedDirectOperation);
        }

        static TextStudio2DRouteDecision Composited(TextStudio2DRouteReason reason) =>
            new TextStudio2DRouteDecision(TextStudio2DRoute.Composited, reason, TextStudio2DFieldPaddingTier.None);

        static bool HasInlineMedia(TMP_Text text)
        {
            if (text == null || text.textInfo == null) return false;
            TMP_TextInfo info = text.textInfo;
            int count = System.Math.Min(info.characterCount, info.characterInfo?.Length ?? 0);
            for (int index = 0; index < count; index++)
                if (info.characterInfo[index].elementType == TMP_TextElementType.Sprite && info.characterInfo[index].isVisible) return true;
            return false;
        }
    }
}
