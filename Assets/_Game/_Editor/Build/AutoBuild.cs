using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Aipocalypse.Core.Bootstrap;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Aipocalypse.Editor.Build
{
    // Builds BuildConfigs into _builds/<product>_<version>_<platform>_<distribution|development>[_profiler][_cheat][_pipeline],
    // with a build_info.txt (BuildStamp) and, on request, a zip next to the folder. Command line:
    // -executeMethod Aipocalypse.Editor.Build.AutoBuild.BuildFromCommandLine -buildConfig <asset path>[,<asset path>…]
    // [-buildOutput <folder>, one config only].
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

        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        public static string BuildsRoot => Path.Combine(ProjectRoot, BuildsFolder);

        [MenuItem("Autobuild/Open Builds Folder", priority = 20)]
        static void OpenBuildsFolder()
        {
            Directory.CreateDirectory(BuildsRoot);
            EditorUtility.OpenWithDefaultApp(BuildsRoot);
        }

        public static string GetBuildName(BuildConfig config) =>
            GetBuildName(config, PlayerSettings.productName, PlayerSettings.bundleVersion);

        public static string GetBuildName(BuildConfig config, string product, string version)
        {
            var name = new StringBuilder(ToFileName(product));
            string fileVersion = ToFileName(version);
            if (fileVersion.Length > 0)
                name.Append('_').Append(fileVersion);
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

        public static string GetOutputFolder(BuildConfig config) => Path.GetFullPath(Path.Combine(BuildsRoot, GetBuildName(config)));

        // The Player Settings defines for this build: the Pipeline toggle the config wants and no other, and no stray
        // ENABLE_CHEAT (cheats come only from the config, through ExtraDefinesFor).
        public static string[] PlayerDefinesFor(string[] playerDefines, BuildConfig config)
        {
            var defines = playerDefines.Where(d => !IsBuildDefine(d)).ToList();
            if (config.Pipeline)
                defines.Add(config.IsDevelopment ? PipelineDevDefine : PipelineReleaseDefine);
            return defines.ToArray();
        }

        public static string[] ExtraDefinesFor(BuildConfig config) => config.HasCheat ? new[] { CheatDefine } : Array.Empty<string>();

        public static bool IsDoNotShip(string relativePath)
        {
            IEnumerable<string> folders = relativePath.Split('\\', '/').SkipLast(1);
            return folders.Any(f => DoNotShipSuffixes.Any(s => f.EndsWith(s, StringComparison.Ordinal)));
        }

        // After the current editor update (building inside OnGUI or an inspector button breaks their layout): checks with the
        // user, builds the configs one after another, then opens the build folder (or _builds/ for several).
        public static void BuildLater(params BuildConfig[] configs)
        {
            EditorApplication.delayCall += () =>
            {
                if (configs.Length == 0 || !ConfirmBuild(configs))
                    return;
                List<BuildOutcome> outcomes = BuildAll(configs);
                var built = outcomes.Where(o => o.Succeeded).ToList();
                if (built.Count == 1 && outcomes.Count == 1)
                    EditorUtility.OpenWithDefaultApp(built[0].Folder);
                else if (built.Count > 0)
                    EditorUtility.OpenWithDefaultApp(BuildsRoot);
            };
        }

        // No build from Play mode; unsaved scenes would be built as saved on disk, so offer to save them; a distribution
        // build carrying the Pipeline server needs a yes.
        static bool ConfirmBuild(IReadOnlyList<BuildConfig> configs)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Autobuild", "Stop Play mode before building.", "OK");
                return false;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;
            string[] releasePipeline = configs.Where(c => c.Pipeline && !c.IsDevelopment).Select(c => c.name).ToArray();
            return releasePipeline.Length == 0 || EditorUtility.DisplayDialog("Distribution build with the Pipeline server",
                $"{string.Join(", ", releasePipeline)} will carry the Unity Pipeline server: a local HTTP server that runs code. " +
                "Never hand this build out.", "Build", "Cancel");
        }

        // One after another, grouped by target so the editor switches platform at most once per target; stops after a cancel.
        public static List<BuildOutcome> BuildAll(IEnumerable<BuildConfig> configs)
        {
            var outcomes = new List<BuildOutcome>();
            foreach (BuildConfig config in configs.OrderBy(c => c.Target))
            {
                BuildOutcome outcome = Build(config, GetOutputFolder(config));
                AutoBuildHistory.Record(config, outcome);
                outcomes.Add(outcome);
                if (outcome.Result == BuildResult.Cancelled)
                    break;
            }
            if (outcomes.Count > 1)
                Debug.Log($"{LogTag} {outcomes.Count(o => o.Succeeded)}/{outcomes.Count} built: " +
                    string.Join(", ", outcomes.Select(o => $"{o.Name} {o.Result}")));
            return outcomes;
        }

        public static void BuildFromCommandLine()
        {
            string[] paths = (GetArg("-buildConfig") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);
            var configs = paths.Select(p => AssetDatabase.LoadAssetAtPath<BuildConfig>(p.Trim())).ToList();
            if (configs.Count == 0 || configs.Contains(null))
            {
                Debug.LogError($"{LogTag} -buildConfig must name BuildConfig assets, comma-separated (got '{GetArg("-buildConfig")}')");
                EditorApplication.Exit(1);
                return;
            }
            string output = GetArg("-buildOutput");
            bool built = output != null && configs.Count == 1
                ? Build(configs[0], Path.GetFullPath(output)).Succeeded
                : BuildAll(configs).All(o => o.Succeeded);
            EditorApplication.Exit(built ? 0 : 1);
        }

        // Cleans and builds into outputFolder, writes build_info.txt, zips when the config asks.
        public static BuildOutcome Build(BuildConfig config, string outputFolder)
        {
            string buildName = GetBuildName(config);
            var outcome = new BuildOutcome { Name = buildName, Folder = outputFolder, Result = BuildResult.Failed };
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError($"{LogTag} No scene enabled in Build Settings");
                return outcome;
            }

            if (Directory.Exists(outputFolder))
                Directory.Delete(outputFolder, true);

            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(config.Target));
            PlayerSettings.GetScriptingDefineSymbols(namedTarget, out string[] playerDefines);
            string[] buildDefines = PlayerDefinesFor(playerDefines, config);
            // The Pipeline's build step reads its toggle from Player Settings, so it can't come in as an extra define
            bool changeDefines = !buildDefines.SequenceEqual(playerDefines);
            if (changeDefines)
                PlayerSettings.SetScriptingDefineSymbols(namedTarget, buildDefines);

            string[] extraDefines = ExtraDefinesFor(config);
            BuildReport report;
            try
            {
                var added = buildDefines.Where(IsBuildDefine).Concat(extraDefines);
                Debug.Log($"{LogTag} Building {buildName} (defines: {string.Join(", ", added.DefaultIfEmpty("none added"))})");
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    target = config.Target,
                    targetGroup = BuildPipeline.GetBuildTargetGroup(config.Target),
                    subtarget = (int)StandaloneBuildSubtarget.Player,
                    locationPathName = Path.Combine(outputFolder, ExecutableName(config.Target)),
                    extraScriptingDefines = extraDefines,
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
            outcome.Result = summary.result;
            outcome.Time = summary.totalTime;
            outcome.Size = (long)summary.totalSize;
            outcome.Errors = summary.totalErrors;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"{LogTag} {buildName}: {summary.result} with {summary.totalErrors} error(s)");
                return outcome;
            }
            outcome.Executable = summary.outputPath;
            if (config.DeleteDoNotShipFolders)
                DeleteDoNotShipFolders(outputFolder);

            var stamp = new BuildStamp(buildName, config.name, GitCommit(), DateTime.Now.ToString("yyyy-MM-dd HH:mm zzz"));
            File.WriteAllText(Path.Combine(outputFolder, BuildStamp.FileName), stamp.ToFileText());
            Debug.Log($"{LogTag} {buildName} built in {summary.totalTime:mm\\:ss}: {outputFolder} ({summary.totalSize / (1024 * 1024)} MB, {stamp})");

            if (config.Zip)
                outcome.Zip = Zip(outputFolder, buildName);
            return outcome;
        }

        static bool IsBuildDefine(string define) => define == PipelineDevDefine || define == PipelineReleaseDefine || define == CheatDefine;

        static void DeleteDoNotShipFolders(string folder)
        {
            foreach (string directory in Directory.GetDirectories(folder))
            {
                if (!IsDoNotShip(Path.GetFileName(directory) + "/"))
                    continue;
                try
                {
                    Directory.Delete(directory, true);
                    Debug.Log($"{LogTag} Deleted {Path.GetFileName(directory)}");
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    Debug.LogWarning($"{LogTag} Couldn't delete {directory}: {e.Message}");
                }
            }
        }

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

        // Short hash of HEAD, "-dirty" with uncommitted changes to tracked files; null without git.
        static string GitCommit()
        {
            string hash = Git("rev-parse --short HEAD");
            if (string.IsNullOrEmpty(hash))
                return null;
            return string.IsNullOrEmpty(Git("status --porcelain --untracked-files=no")) ? hash : hash + "-dirty";
        }

        static string Git(string arguments)
        {
            try
            {
                var info = new System.Diagnostics.ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = ProjectRoot,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var process = System.Diagnostics.Process.Start(info);
                string output = process.StandardOutput.ReadToEnd();
                return process.WaitForExit(10000) && process.ExitCode == 0 ? output.Trim() : null;
            }
            catch (Exception)
            {
                return null;
            }
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
        public static string ToFileName(string text)
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

    public sealed class BuildOutcome
    {
        public string Name { get; set; }
        public BuildResult Result { get; set; }
        public string Folder { get; set; }
        public string Executable { get; set; }
        public string Zip { get; set; }
        public TimeSpan Time { get; set; }
        public long Size { get; set; }
        public int Errors { get; set; }

        public bool Succeeded => Result == BuildResult.Succeeded;
    }
}
