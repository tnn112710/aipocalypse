namespace TextStudio.Timeline
{
    internal static class TextStudioTimelineInfo
    {
        public const string Version = "1.2.0";

#if USE_TIMELINE
        public const bool TimelineAvailable = true;
#else
        public const bool TimelineAvailable = false;
#endif

    }
}
