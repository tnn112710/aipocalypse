using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TextStudio.Editor
{
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioPackagePaths
    {
        public const string BasePackageName = "com.wetzold.text-studio";
        public const string ThreeDPackageName = "com.wetzold.text-studio.3d";

        static Dictionary<string, string> s_PackageRoots;

        public static string FindFullRoot(string packageName)
        {
            if (string.IsNullOrEmpty(packageName)) return null;
            EnsurePackageRoots();
            if (s_PackageRoots.TryGetValue(packageName, out string root) &&
                Directory.Exists(root))
            {
                return root;
            }

            RefreshPackageRoots();
            if (s_PackageRoots.TryGetValue(packageName, out root) &&
                Directory.Exists(root))
            {
                return root;
            }

            // Not under Assets: installed through the Package Manager (registry, tarball, git or a local folder).
            UnityEditor.PackageManager.PackageInfo package = FindInstalledPackage(packageName);
            return package != null && Directory.Exists(package.resolvedPath)
                ? package.resolvedPath.Replace('\\', '/')
                : null;
        }

        /// <summary>
        /// The package root as an asset path: under Assets when its sources live in the project,
        /// "Packages/&lt;name&gt;" when it is installed through the Package Manager. A full path
        /// outside the project cannot be turned into an asset path any other way.
        /// </summary>
        public static string FindAssetRoot(string packageName)
        {
            string fullRoot = FindFullRoot(packageName);
            if (string.IsNullOrEmpty(fullRoot)) return null;

            string projectRelative = FileUtil.GetProjectRelativePath(fullRoot.Replace('\\', '/'));
            if (!string.IsNullOrEmpty(projectRelative) &&
                projectRelative.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return projectRelative;
            }

            UnityEditor.PackageManager.PackageInfo package = FindInstalledPackage(packageName);
            return package != null ? package.assetPath : null;
        }

        static UnityEditor.PackageManager.PackageInfo FindInstalledPackage(string packageName)
        {
            return UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + packageName);
        }

        static void EnsurePackageRoots()
        {
            if (s_PackageRoots != null) return;
            RefreshPackageRoots();
        }

        static void RefreshPackageRoots()
        {
            s_PackageRoots = new Dictionary<string, string>(StringComparer.Ordinal);
            string assetsRoot = Application.dataPath;
            if (string.IsNullOrEmpty(assetsRoot) || !Directory.Exists(assetsRoot)) return;

            string[] packageFiles = Directory.GetFiles(assetsRoot, "package.json", SearchOption.AllDirectories);
            for (int i = 0; i < packageFiles.Length; i++)
            {
                string packageFile = packageFiles[i];
                string json = File.ReadAllText(packageFile);
                if (!TryGetPackageName(json, out string packageName)) continue;
                string packageRoot = Path.GetDirectoryName(packageFile);
                if (!string.IsNullOrEmpty(packageRoot))
                    s_PackageRoots[packageName] = packageRoot.Replace('\\', '/');
            }
        }

        static bool TryGetPackageName(string json, out string packageName)
        {
            packageName = null;
            try
            {
                PackageManifest manifest = JsonUtility.FromJson<PackageManifest>(json);
                if (manifest == null || string.IsNullOrEmpty(manifest.name)) return false;
                packageName = manifest.name;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        [Serializable]
        private sealed class PackageManifest
        {
            public string name;
        }
    }
}
