using System;
using UnityEditor.PackageManager;

namespace TextStudio.Editor
{
    internal static class TextAnimatorMigrationAvailability
    {
        internal const string TextAnimatorPackageName = "com.febucci.text-animator-unity";
        internal const string PackageRoot = "Packages/" + TextAnimatorPackageName + "/";
        const string DefaultDisplayName = "Text Animator for Unity by Febucci";

        public static bool IsInstalled
        {
            get
            {
                return PackageInfo.FindForPackageName(TextAnimatorPackageName) != null;
            }
        }

        /// <summary>Folder where Package Manager imports Text Animator samples: <c>Assets/Samples/&lt;display name&gt;/</c>.</summary>
        public static string GetSamplesFolder()
        {
            PackageInfo package = PackageInfo.FindForPackageName(TextAnimatorPackageName);
            string displayName = package != null && !string.IsNullOrEmpty(package.displayName)
                ? package.displayName
                : DefaultDisplayName;
            return "Assets/Samples/" + displayName + "/";
        }

        public static bool IsUnderFolder(string assetPath, string folder)
        {
            return !string.IsNullOrEmpty(assetPath) &&
                   !string.IsNullOrEmpty(folder) &&
                   assetPath.Replace('\\', '/').StartsWith(folder, StringComparison.OrdinalIgnoreCase);
        }
    }
}
