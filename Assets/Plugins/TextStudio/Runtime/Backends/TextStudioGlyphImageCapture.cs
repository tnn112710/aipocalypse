using System;

namespace TextStudio
{
    /// <summary>Captures exact images at the shaping boundary on the main thread. No Unity texture, pooled callback array or backend is retained by a successful result.</summary>
    public static class TextStudioGlyphImageCapture
    {
        /// <summary>Captures owned pixels for a current shaped glyph and rejects requests that change during capture.</summary>
        public static TextStudioGlyphImageStatus Capture(ITextStudioShapedGlyphBackend backend, int shapedGlyphIndex,
            ITextStudioGlyphImageProvider preparedProvider, out TextStudioGlyphImageSnapshot image, out string diagnostic)
        {
            image = null;
            if (!(backend is ITextStudioShapedGlyphImageBackend images))
            {
                diagnostic = "The shaping backend does not expose exact glyph-image requests and owned RGBA capture through a public API. Use a prepared image provider with an image-capable shaping source, or explicitly choose monochrome geometry.";
                return TextStudioGlyphImageStatus.Unavailable;
            }
            long imageRevision = backend is ITextStudioGlyphImageRevisionProvider revisionProvider ? revisionProvider.GlyphImageRevision : 0;
            TextStudioGlyphImageStatus status = images.GetGlyphImageRequest(shapedGlyphIndex, out TextStudioGlyphImageRequest request, out diagnostic);
            if (status != TextStudioGlyphImageStatus.Ready) return status;
            if (!request.IsValid || request.ShapedGlyphIndex != shapedGlyphIndex || !request.IsCurrentFor(backend))
            { diagnostic = "The exact image request does not belong to the current shaped occurrence."; return TextStudioGlyphImageStatus.Stale; }
            status = (preparedProvider ?? images).CaptureGlyphImage(in request, out TextStudioGlyphImageSnapshot captured, out diagnostic);
            if (status != TextStudioGlyphImageStatus.Ready) return status;
            if (captured == null || !captured.Request.Equals(request))
            { diagnostic = "The image provider returned pixels for a different shaped glyph or cluster occurrence."; return TextStudioGlyphImageStatus.InvalidData; }
            if (!request.IsCurrentFor(backend) ||
                (backend is ITextStudioGlyphImageRevisionProvider currentRevision && currentRevision.GlyphImageRevision != imageRevision))
            { diagnostic = "Shaping changed while its image was being captured. Retry after the complete layout revision is ready."; return TextStudioGlyphImageStatus.Stale; }
            image = captured; diagnostic = string.Empty;
            return TextStudioGlyphImageStatus.Ready;
        }
    }
}
