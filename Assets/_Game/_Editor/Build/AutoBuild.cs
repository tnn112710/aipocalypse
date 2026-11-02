using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Aipocalypse.Editor.Build
{
    // Builds a BuildConfig into _builds/<product>_<version>_<platform>_<distribution|development>[_profiler][_cheat][_pipeline],
    // zipped next to it on request. Command line: -executeMethod Aipocalypse.Editor.Build.AutoBuild.BuildFromCommandLine
    // -buildConfig <asset path> [-buildOutput <folder>].
    public static class AutoBuild
    {
        public const string CheatDefine = "ENABLE_CHEAT";
        // The Unity Pipeline's runtime toggles (Project Settings > Pipeline > Runtime): one for development, one for release
        public const string PipelineDevDefine = "ENABLE_RUNTIME_PIPELINE_DEV";
        public const string PipelineReleaseDefine = "ENABLE_RUNTIME_PIPELINE";
        public const string BuildsFolder = "_builds";

        const string LogTag = "[AutoBuild]";

        // Folders Unity writes next to an IL2CPP / Burst player for symbols only
        static readonly string[] DoNotShipSuffixes = { "_DoNotShip", "_ButDontShipItWithYourGame" };

        static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        [MenuItem("Autobuild/Open Builds Folder", priority = 20)]
        static void OpenBuildsFolder()
        {
            string folder = Path.Combine(ProjectRoot, BuildsFolder);
            Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder + Path.DirectorySeparatorChar);
        }

        public static string GetBuildName(BuildConfig config)
        {
            var name = new StringBuilder(ToFileName(PlayerSettings.productName));
            string version = ToFileName(PlayerSettings.bundleVersion);
            if (version.Length > 0)
                name.Append('_').Append(version);
            name.Append('_').Append(PlatformName(config.Target));
            name.Append(config.IsDevelopment ? "_development" : "_distribution");
            if (config.Profiler)
                name.Append("_profiler");
            if (config.HasCheat)
                name.Append("_cheat");
            if (config.Pipeline)
                name.Append("_pipeline");
            return name.ToString();
        }

        public static string GetOutputFolder(BuildConfig config) =>
            Path.GetFullPath(Path.Combine(ProjectRoot, BuildsFolder, GetBuildName(config)));

        // Builds after the current editor update: building inside OnGUI or an inspector button breaks their layout.
        public static void BuildLater(BuildConfig config)
        {
            EditorApplication.delayCall += () =>
            {
                if (Build(config, GetOutputFolder(config), out string revealPath))
                    EditorUtility.RevealInFinder(revealPath);
            };
        }

        public static void BuildFromCommandLine()
        {
            string configPath = GetArg("-buildConfig");
            var config = configPath == null ? null : AssetDatabase.LoadAssetAtPath<BuildConfig>(configPath);
            if (config == null)
            {
                Debug.LogError($"{LogTag} -buildConfig must name a BuildConfig asset (got '{configPath}')");
                EditorApplication.Exit(1);
                return;
            }
            string output = GetArg("-buildOutput");
            bool built = Build(config, output == null ? GetOutputFolder(config) : Path.GetFullPath(output), out _);
            EditorApplication.Exit(built ? 0 : 1);
        }

        // Cleans and builds into outputFolder (zipped when the config asks); revealPath is the zip, else the player.
        public static bool Build(BuildConfig config, string outputFolder, out string revealPath)
        {
            revealPath = null;
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError($"{LogTag} No scene enabled in Build Settings");
                return false;
            }

            string buildName = GetBuildName(config);
            if (Directory.Exists(outputFolder))
                Directory.Delete(outputFolder, true);

            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(config.Target));
            PlayerSettings.GetScriptingDefineSymbols(namedTarget, out string[] playerDefines);
            string[] buildDefines = BuildPlayerDefines(playerDefines, config);
            // The Pipeline's build step reads its toggle from Player Settings, so it can't come in as an extra define
            bool changeDefines = !buildDefines.SequenceEqual(playerDefines);
            if (changeDefines)
                PlayerSettings.SetScriptingDefineSymbols(namedTarget, buildDefines);

            BuildReport report;
            try
            {
                var added = buildDefines.Where(IsBuildDefine).Concat(config.HasCheat ? new[] { CheatDefine } : Array.Empty<string>());
                Debug.Log($"{LogTag} Building {buildName} (defines: {string.Join(", ", added.DefaultIfEmpty("none added"))})");
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    target = config.Target,
                    targetGroup = BuildPipeline.GetBuildTargetGroup(config.Target),
                    subtarget = (int)StandaloneBuildSubtarget.Player,
                    locationPathName = Path.Combine(outputFolder, ExecutableName(config.Target)),
                    extraScriptingDefines = config.HasCheat ? new[] { CheatDefine } : null,
                    options = (config.IsDevelopment ? BuildOptions.Development : BuildOptions.None)
                        | (config.Profiler ? BuildOptions.ConnectWithProfiler : BuildOptions.None),
                });
            }
            finally
            {
                if (changeDefines)
                    PlayerSettings.SetScriptingDefineSymbols(namedTarget, playerDefines);
            }

            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"{LogTag} {buildName}: {summary.result} with {summary.totalErrors} error(s)");
                return false;
            }
            Debug.Log($"{LogTag} {buildName} built in {summary.totalTime:mm\\:ss}: {outputFolder} ({summary.totalSize / (1024 * 1024)} MB)");

            revealPath = summary.outputPath;
            if (config.Zip)
                revealPath = Zip(outputFolder, buildName);
            return true;
        }

        // The Player Settings defines with the Pipeline toggle this config wants, and without a stray ENABLE_CHEAT
        // (cheats come only from the config, as an extra define).
        static string[] BuildPlayerDefines(string[] playerDefines, BuildConfig config)
        {
            var defines = playerDefines.Where(d => !IsBuildDefine(d) && d != CheatDefine).ToList();
            if (config.Pipeline)
                defines.Add(config.IsDevelopment ? PipelineDevDefine : PipelineReleaseDefine);
            return defines.ToArray();
        }

        static bool IsBuildDefine(string define) => define == PipelineDevDefine || define == PipelineReleaseDefine;

        // <build name>.zip next to the folder, the files inside a <build name>/ folder; null when it fails.
        static string Zip(string folder, string buildName)
        {
            string zipPath = Path.Combine(Path.GetDirectoryName(folder), buildName + ".zip");
            try
            {
                if (File.Exists(zipPath))
                    File.Delete(zipPath);
                using ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(folder, file);
                    if (IsDoNotShip(relative))
                        continue;
                    zip.CreateEntryFromFile(file, buildName + "/" + relative.Replace('\\', '/'), System.IO.Compression.CompressionLevel.Optimal);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogTag} Zipping {buildName} failed: {e.Message}");
                return null;
            }
            Debug.Log($"{LogTag} Zipped {zipPath} ({new FileInfo(zipPath).Length / (1024 * 1024)} MB)");
            return zipPath;
        }

        static bool IsDoNotShip(string relativePath)
        {
            IEnumerable<string> folders = relativePath.Split('\\', '/').SkipLast(1);
            return folders.Any(f => DoNotShipSuffixes.Any(s => f.EndsWith(s, StringComparison.Ordinal)));
        }

        static string PlatformName(BuildTarget target) => target switch
        {
            BuildTarget.StandaloneWindows64 or BuildTarget.StandaloneWindows => "windows",
            BuildTarget.StandaloneOSX => "macos",
            BuildTarget.StandaloneLinux64 => "linux",
            _ => target.ToString().ToLowerInvariant(),
        };

        static string ExecutableName(BuildTarget target)
        {
            string name = ToFileName(PlayerSettings.productName);
            if (name.Length == 0)
                name = "Game";
            return target switch
            {
                BuildTarget.StandaloneWindows64 or BuildTarget.StandaloneWindows => name + ".exe",
                BuildTarget.StandaloneOSX => name + ".app",
                BuildTarget.StandaloneLinux64 => name + ".x86_64",
                _ => name,
            };
        }

        // Keeps letters, digits, '.', '-' and '_'; any other run of characters becomes one '-'.
        static string ToFileName(string text)
        {
            var name = new StringBuilder();
            foreach (char c in text ?? "")
            {
                if (c < 128 && (char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_'))
                    name.Append(c);
                else if (name.Length > 0 && name[^1] != '-')
                    name.Append('-');
            }
            return name.ToString().Trim('-', '.');
        }

        static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
