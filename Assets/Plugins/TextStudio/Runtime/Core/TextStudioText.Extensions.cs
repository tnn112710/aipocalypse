using UnityEngine;

namespace TextStudio
{
    public sealed partial class TextStudioText : ITextStudioGlyphColorEncoder
    {
        TextStudioExtensionRegistry _extensions;

        Color32 ITextStudioGlyphColorEncoder.EncodeGlyphColor(Color32 authoredColor)
        {
            if (_activeTextBackend is ITextStudioGlyphColorEncoder encoder)
                return encoder.EncodeGlyphColor(authoredColor);
            // TMP converts its world-space mesh colors to linear, while Canvas keeps sRGB vertices.
            return !UsesCustomTextBackend && TMPText is TMPro.TextMeshPro && QualitySettings.activeColorSpace == ColorSpace.Linear
                ? (Color32)((Color)authoredColor).linear
                : authoredColor;
        }

        /// <summary>
        /// Neutral target-scoped extension registry. Accessing this property creates the registry;
        /// Text Studio pays no registry cost when no extension uses it.
        /// </summary>
        public TextStudioExtensionRegistry Extensions =>
            _extensions ?? (_extensions = new TextStudioExtensionRegistry(this));

        /// <summary>The built-in text backend target for independent frame composition.</summary>
        public TextStudioFrameTarget TextBackendFrameTarget =>
            new TextStudioFrameTarget(this, this, TextStudioFrameTargetKind.TextBackend);

        /// <summary>Creates a target identity for one renderer currently attached to this hub.</summary>
        public bool TryGetRendererFrameTarget(
            ITextStudioRenderer renderer,
            out TextStudioFrameTarget target)
        {
            if (renderer == null || !_renderers.Contains(renderer))
            {
                target = default;
                return false;
            }

            target = new TextStudioFrameTarget(
                this,
                renderer,
                TextStudioFrameTargetKind.ExternalRenderer);
            return true;
        }

        internal TextStudioFrameTarget GetRendererFrameTarget(ITextStudioRenderer renderer)
        {
            return new TextStudioFrameTarget(
                this,
                renderer,
                TextStudioFrameTargetKind.ExternalRenderer);
        }
    }
}
