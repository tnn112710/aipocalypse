namespace TextStudio
{
    internal static class TextStudio2DSourceApplicabilityUtility
    {
        internal static bool Includes(in TextStudio2DCompiledOperation operation, TextStudioVisualSourceKind source)
        {
            TextStudio2DSourceApplicability applicability = operation.SourceApplicability;
            if (source == TextStudioVisualSourceKind.FontGlyph) return applicability != TextStudio2DSourceApplicability.ColorOnly;
            if (applicability == TextStudio2DSourceApplicability.TextGlyphsOnly) return false;
            if (applicability != TextStudio2DSourceApplicability.Automatic) return true;
            return operation.Kind == TextStudio2DLayerKind.Fill || operation.Kind == TextStudio2DLayerKind.OuterShadow ||
                operation.Kind == TextStudio2DLayerKind.InnerGlow || operation.Kind == TextStudio2DLayerKind.OuterGlow ||
                operation.Kind == TextStudio2DLayerKind.Decoration || operation.Kind == TextStudio2DLayerKind.Mask ||
                operation.Kind == TextStudio2DLayerKind.Group || operation.Kind == TextStudio2DLayerKind.ColorAdjustment ||
                operation.Kind == TextStudio2DLayerKind.CompositeAdjustment;
        }
    }
}
