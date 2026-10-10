using System;

namespace TextStudio.Editor
{
    internal sealed class TextGalleryCaptureDescriptor
    {
        internal const float DefaultDurationSeconds = 2.8f;
        internal const float DefaultPosterTimeNormalized = 0.65f;

        internal TextGalleryCaptureDescriptor(
            string markup,
            RevealMode revealMode,
            bool loopReveal,
            float fontSizeOverride = 0f,
            Action<TextStudioText> configureRuntime = null,
            TextStudioPreviewSegment segments = TextStudioPreviewSegment.All,
            float durationSeconds = DefaultDurationSeconds,
            float posterTimeNormalized = DefaultPosterTimeNormalized,
            bool useSnapshotRenderer = false)
        {
            Markup = markup ?? string.Empty;
            RevealMode = revealMode;
            LoopReveal = loopReveal;
            FontSizeOverride = fontSizeOverride;
            ConfigureRuntime = configureRuntime;
            Segments = segments;
            DurationSeconds = durationSeconds < 0.1f ? 0.1f : durationSeconds;
            PosterTimeNormalized = posterTimeNormalized < 0f
                ? 0f
                : posterTimeNormalized > 1f
                    ? 1f
                    : posterTimeNormalized;
            UseSnapshotRenderer = useSnapshotRenderer;
        }

        internal string Markup { get; }

        internal RevealMode RevealMode { get; }

        internal bool LoopReveal { get; }

        internal float FontSizeOverride { get; }

        internal Action<TextStudioText> ConfigureRuntime { get; }

        internal TextStudioPreviewSegment Segments { get; }

        internal float DurationSeconds { get; }

        internal float PosterTimeNormalized { get; }

        internal bool UseSnapshotRenderer { get; }
    }
}
