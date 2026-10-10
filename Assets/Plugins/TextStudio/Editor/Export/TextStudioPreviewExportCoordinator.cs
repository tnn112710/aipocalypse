namespace TextStudio.Editor
{
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioPreviewExportCoordinator
    {
        internal static bool IsExternalExporting { get; private set; }

        internal static bool TryBeginExternal(out string error)
        {
            if (IsExternalExporting || TextStudioPreviewExportJob.IsExporting)
            {
                error = "Another Text Studio preview export is already running.";
                return false;
            }

            IsExternalExporting = true;
            error = string.Empty;
            return true;
        }

        internal static void EndExternal()
        {
            IsExternalExporting = false;
        }
    }
}
