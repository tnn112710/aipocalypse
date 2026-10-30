using VoxelLabs.Shared.Lib;

namespace VoxelLabs.UltimateThumbnails.Lib
{
    public static class Utils
    {
        // Forward compatibility calls to the shared source-only assembly.
        public static int GetEditorVersionIdentifier() => UnityEditorCompatibility.GetEditorVersionIdentifier();

        public static bool GetVerbosity() => UnityEditorCompatibility.GetVerbosity();

        public static object GetSelectionIds() => UnityEditorCompatibility.GetSelectionIds();
        
        public static void SetSelectionIds(object ids) => UnityEditorCompatibility.SetSelectionIds(ids);

        public static void ClearSelection() => UnityEditorCompatibility.ClearSelection();
        
        public static int GetIconSize(IconSize iconSize)
        {
            // Texture size should be multiple of 4 else the generated icon can be blurry
            switch (iconSize)
            {
                case IconSize.X64:
                    return 64;
                case IconSize.X128:
                    return 128;
                case IconSize.X256:
                    return 256;
                case IconSize.X512:
                    return 512;
                default:
                    return 128;
            }
        }
    }
}