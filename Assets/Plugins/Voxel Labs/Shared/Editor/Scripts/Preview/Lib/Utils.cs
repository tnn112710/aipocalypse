using VoxelLabs.Shared.Lib;

namespace VoxelLabs.UltimatePreview
{
    public static class Utils
    {
        // Forward compatibility calls to the shared source-only assembly.
        public static int GetEditorVersionIdentifier() => UnityEditorCompatibility.GetEditorVersionIdentifier();

        public static bool GetVerbosity() => UnityEditorCompatibility.GetVerbosity();
    }
}