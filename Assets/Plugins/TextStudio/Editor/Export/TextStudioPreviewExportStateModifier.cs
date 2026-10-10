using System;

namespace TextStudio.Editor
{
    /// <summary>
    /// Neutral editor-only extension point for stateful add-ons that need to contribute to the
    /// established Text Studio MP4 and GIF export renderer.
    /// </summary>
    internal interface ITextStudioPreviewExportStateModifier : IDisposable
    {
        void Apply(
            float continuousTime,
            float lifecycleTime,
            TextStudioCompiledPlan plan,
            EvaluatedGlyphState[] states,
            int glyphCount,
            in TextStudioRenderPropertyWriter renderProperties);

        void ResetEvaluation();
    }
}
