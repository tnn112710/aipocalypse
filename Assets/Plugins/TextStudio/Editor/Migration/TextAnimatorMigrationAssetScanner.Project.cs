using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.SceneManagement;
using CompilationAssembly = UnityEditor.Compilation.Assembly;

namespace TextStudio.Editor
{
    internal enum TextAnimatorMigrationScanScope
    {
        Project,
        OpenScenes,
        Selection,
        Folder
    }

    /// <summary>A C# file that uses the Text Animator API and therefore needs a manual code change.</summary>
    internal readonly struct TextAnimatorMigrationScriptReference
    {
        public readonly string AssetPath;
        public readonly int Line;
        public readonly int ReferenceCount;
        public readonly string Snippet;

        public TextAnimatorMigrationScriptReference(string assetPath, int line, int referenceCount, string snippet)
        {
            AssetPath = assetPath ?? string.Empty;
            Line = Mathf.Max(1, line);
            ReferenceCount = Mathf.Max(1, referenceCount);
            Snippet = snippet ?? string.Empty;
        }
    }

    internal sealed class TextAnimatorMigrationScanResult
    {
        public TextAnimatorMigrationScanResult(
            TextAnimatorMigrationScanScope scope,
            string folder,
            IReadOnlyList<TextAnimatorMigrationCandidate> candidates,
            IReadOnlyList<TextAnimatorMigrationArtifactRecord> artifacts,
            IReadOnlyList<TextAnimatorMigrationScriptReference> scripts,
            int inspectedFileCount,
            float seconds,
            bool canceled)
        {
            Scope = scope;
            Folder = folder ?? string.Empty;
            Candidates = candidates ?? Array.Empty<TextAnimatorMigrationCandidate>();
            Artifacts = artifacts ?? Array.Empty<TextAnimatorMigrationArtifactRecord>();
            Scripts = scripts ?? Array.Empty<TextAnimatorMigrationScriptReference>();
            InspectedFileCount = Mathf.Max(0, inspectedFileCount);
            Seconds = Mathf.Max(0f, seconds);
            Canceled = canceled;
        }

        public TextAnimatorMigrationScanScope Scope { get; }
        public string Folder { get; }
        public IReadOnlyList<TextAnimatorMigrationCandidate> Candidates { get; }
        public IReadOnlyList<TextAnimatorMigrationArtifactRecord> Artifacts { get; }
        public IReadOnlyList<TextAnimatorMigrationScriptReference> Scripts { get; }
        public int InspectedFileCount { get; }
        public float Seconds { get; }
        public bool Canceled { get; }
    }

    internal static partial class TextAnimatorMigrationAssetScanner
    {
        const string ProgressTitle = "Finding Text Animator content";
        const double ProgressDelaySeconds = 0.3;
        const double ProgressIntervalSeconds = 0.05;

        // Matches qualified Text Animator API use such as "using Febucci.UI;" but not string literals or paths.
        static readonly Regex s_scriptReference = new Regex(
            @"(?<![\w""./\\])Febucci\.[A-Za-z]",
            RegexOptions.CultureInvariant);

        readonly struct ScriptInfo
        {
            public readonly bool IsTextAnimator;
            public readonly TextAnimatorMigrationArtifactKind ComponentKind;
            public readonly string Guid;

            public ScriptInfo(bool isTextAnimator, TextAnimatorMigrationArtifactKind componentKind, string guid)
            {
                IsTextAnimator = isTextAnimator;
                ComponentKind = componentKind;
                Guid = guid ?? string.Empty;
            }
        }

        sealed class ScanContext
        {
            public readonly List<TextAnimatorMigrationCandidate> Candidates = new List<TextAnimatorMigrationCandidate>();
            public readonly List<TextAnimatorMigrationArtifactRecord> Artifacts = new List<TextAnimatorMigrationArtifactRecord>();
            public readonly Dictionary<string, TextAnimatorMigrationArtifactRecord> ArtifactKeys =
                new Dictionary<string, TextAnimatorMigrationArtifactRecord>(StringComparer.OrdinalIgnoreCase);
            public readonly List<TextAnimatorMigrationScriptReference> Scripts = new List<TextAnimatorMigrationScriptReference>();
            public readonly Dictionary<string, ScriptInfo> ScriptInfos = new Dictionary<string, ScriptInfo>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> ScannedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly string SamplesFolder = TextAnimatorMigrationAvailability.GetSamplesFolder();
            public readonly double StartTime = EditorApplication.timeSinceStartup;
            public int InspectedFiles;

            HashSet<string> _derivedScriptPaths;

            // Project scripts that extend Text Animator types, such as a custom typewriter or effect.
            public HashSet<string> DerivedScriptPaths => _derivedScriptPaths ??= FindDerivedScriptPaths();

            double _lastProgress;
            bool _progressShown;

            // Short scans finish without a progress bar; longer scans show one that can be canceled.
            public void Report(string phase, float progress)
            {
                double now = EditorApplication.timeSinceStartup;
                if (now - StartTime < ProgressDelaySeconds || now - _lastProgress < ProgressIntervalSeconds)
                    return;

                _lastProgress = now;
                _progressShown = true;
                if (EditorUtility.DisplayCancelableProgressBar(ProgressTitle, phase, Mathf.Clamp01(progress)))
                    throw new OperationCanceledException();
            }

            public void ClearProgress()
            {
                if (_progressShown)
                    EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Finds Text Animator components, assets, markup and script references. Prefabs, scenes and assets are
        /// pre-filtered through their direct script dependencies, so only Text Animator content is loaded.
        /// </summary>
        public static TextAnimatorMigrationScanResult Scan(TextAnimatorMigrationScanScope scope, string folder = null)
        {
            ScanContext context = new ScanContext();
            bool canceled = false;
            try
            {
                switch (scope)
                {
                    case TextAnimatorMigrationScanScope.Project:
                        ScanOpenScenes(context);
                        ScanAssetFolder(context, "Assets");
                        ScanScripts(context, "Assets");
                        break;
                    case TextAnimatorMigrationScanScope.OpenScenes:
                        ScanOpenScenes(context);
                        break;
                    case TextAnimatorMigrationScanScope.Selection:
                        ScanSelection(context);
                        break;
                    case TextAnimatorMigrationScanScope.Folder:
                        if (IsProjectAssetFolder(folder))
                        {
                            ScanAssetFolder(context, folder);
                            ScanScripts(context, folder);
                        }
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                canceled = true;
            }
            finally
            {
                context.ClearProgress();
            }

            return new TextAnimatorMigrationScanResult(
                scope,
                folder,
                context.Candidates,
                context.Artifacts,
                context.Scripts,
                context.InspectedFiles,
                (float)(EditorApplication.timeSinceStartup - context.StartTime),
                canceled);
        }

        static bool IsProjectAssetFolder(string folder)
        {
            return !string.IsNullOrWhiteSpace(folder) &&
                   (string.Equals(folder, "Assets", StringComparison.Ordinal) ||
                    folder.StartsWith("Assets/", StringComparison.Ordinal)) &&
                   AssetDatabase.IsValidFolder(folder);
        }

        static void ScanOpenScenes(ScanContext context)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                if (!string.IsNullOrEmpty(scene.path))
                    context.ScannedPaths.Add(scene.path);

                context.Report("Open scenes: " + scene.name, 0f);
                GameObject[] roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    AddGameObjectCandidates(roots[r], TextAnimatorMigrationSourceKind.Scene, context.Candidates);
                    AddGameObjectArtifacts(roots[r], TextAnimatorMigrationSourceKind.Scene, context.Artifacts, context.ArtifactKeys);
                }
            }
        }

        static void ScanSelection(ScanContext context)
        {
            UnityEngine.Object[] selection = Selection.objects;
            for (int i = 0; i < selection.Length; i++)
            {
                UnityEngine.Object selected = selection[i];
                if (selected == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(selected);
                if (IsProjectAssetFolder(path))
                {
                    ScanAssetFolder(context, path);
                    ScanScripts(context, path);
                    continue;
                }

                if (selected is SceneAsset)
                {
                    if (context.ScannedPaths.Add(path))
                        AddSceneFileArtifacts(context, path);
                    continue;
                }

                AddObjectCandidates(selected, TextAnimatorMigrationSourceKind.Selection, context.Candidates);
                AddObjectArtifacts(selected, TextAnimatorMigrationSourceKind.Selection, context.Artifacts, context.ArtifactKeys);
            }
        }

        static void ScanAssetFolder(ScanContext context, string folder)
        {
            string[] folders = { folder };

            string[] textGuids = AssetDatabase.FindAssets("t:TextAsset", folders);
            for (int i = 0; i < textGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(textGuids[i]);
                if (!IsSupportedTextAssetPath(path) || !context.ScannedPaths.Add(path))
                    continue;

                context.Report("Text files", 0.05f + 0.15f * i / Mathf.Max(1, textGuids.Length));
                context.InspectedFiles++;
                AddTextAssetCandidate(path, context.Candidates);
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", folders);
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                context.Report("Prefabs", 0.2f + 0.4f * i / Mathf.Max(1, prefabGuids.Length));
                if (!context.ScannedPaths.Add(path) || !HasTextAnimatorDependency(context, path))
                    continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                context.InspectedFiles++;
                AddGameObjectCandidates(prefab, TextAnimatorMigrationSourceKind.Prefab, context.Candidates);
                AddGameObjectArtifacts(prefab, TextAnimatorMigrationSourceKind.Prefab, context.Artifacts, context.ArtifactKeys);
            }

            string scriptableFilter = BuildTextAnimatorScriptableFilter();
            if (!string.IsNullOrEmpty(scriptableFilter))
            {
                string[] assetGuids = AssetDatabase.FindAssets(scriptableFilter, folders);
                for (int i = 0; i < assetGuids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
                    context.Report("Text Animator assets", 0.6f + 0.15f * i / Mathf.Max(1, assetGuids.Length));
                    if (!context.ScannedPaths.Add(path))
                        continue;

                    context.InspectedFiles++;
                    UnityEngine.Object[] objects = AssetDatabase.LoadAllAssetsAtPath(path);
                    for (int o = 0; o < objects.Length; o++)
                        AddScriptableArtifact(path, objects[o], TextAnimatorMigrationSourceKind.Folder, context.Artifacts, context.ArtifactKeys);
                }
            }

            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", folders);
            for (int i = 0; i < sceneGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                context.Report("Scenes", 0.75f + 0.2f * i / Mathf.Max(1, sceneGuids.Length));
                if (!context.ScannedPaths.Add(path) || !HasTextAnimatorDependency(context, path))
                    continue;

                AddSceneFileArtifacts(context, path);
            }
        }

        // Closed scenes are counted from their YAML so the user can decide which scene to open next.
        static void AddSceneFileArtifacts(ScanContext context, string path)
        {
            string absolutePath = Path.GetFullPath(path);
            if (!File.Exists(absolutePath))
                return;

            context.InspectedFiles++;
            string text = File.ReadAllText(absolutePath);
            int texts = 0;
            int typewriters = 0;
            int soundWriters = 0;
            string[] dependencies = AssetDatabase.GetDependencies(path, false);
            for (int i = 0; i < dependencies.Length; i++)
            {
                ScriptInfo info = GetScriptInfo(context, dependencies[i]);
                if (!info.IsTextAnimator || string.IsNullOrEmpty(info.Guid))
                    continue;

                int occurrences = CountOccurrences(text, "guid: " + info.Guid);
                switch (info.ComponentKind)
                {
                    case TextAnimatorMigrationArtifactKind.TextAnimatorComponent:
                        texts += occurrences;
                        break;
                    case TextAnimatorMigrationArtifactKind.TypewriterComponent:
                        typewriters += occurrences;
                        break;
                    case TextAnimatorMigrationArtifactKind.SoundWriterComponent:
                        soundWriters += occurrences;
                        break;
                }
            }

            string sceneName = Path.GetFileNameWithoutExtension(path);
            AddSceneCount(context, TextAnimatorMigrationArtifactKind.TextAnimatorComponent, "Text Animator texts in " + sceneName, path, texts);
            AddSceneCount(context, TextAnimatorMigrationArtifactKind.TypewriterComponent, "Text Animator typewriters in " + sceneName, path, typewriters);
            AddSceneCount(context, TextAnimatorMigrationArtifactKind.SoundWriterComponent, "Text Animator audio players in " + sceneName, path, soundWriters);
        }

        static void AddSceneCount(
            ScanContext context,
            TextAnimatorMigrationArtifactKind kind,
            string label,
            string scenePath,
            int count)
        {
            if (count <= 0)
                return;

            string key = kind + "|" + scenePath + "|closed";
            if (context.ArtifactKeys.ContainsKey(key))
                return;

            TextAnimatorMigrationArtifactRecord artifact = new TextAnimatorMigrationArtifactRecord(
                kind,
                label,
                string.Empty,
                scenePath,
                TextAnimatorMigrationSourceKind.Scene,
                null,
                "Open this scene to continue with its Text Animator objects.",
                count);
            context.ArtifactKeys.Add(key, artifact);
            context.Artifacts.Add(artifact);
        }

        static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int index = text.IndexOf(value, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                count++;
                index = text.IndexOf(value, index + value.Length, StringComparison.OrdinalIgnoreCase);
            }

            return count;
        }

        static bool HasTextAnimatorDependency(ScanContext context, string assetPath)
        {
            string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);
            for (int i = 0; i < dependencies.Length; i++)
            {
                if (GetScriptInfo(context, dependencies[i]).IsTextAnimator)
                    return true;
            }

            return false;
        }

        static ScriptInfo GetScriptInfo(ScanContext context, string path)
        {
            if (string.IsNullOrEmpty(path))
                return default;

            bool isSource = path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
            bool isLibrary = path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
            if (!isSource && !isLibrary)
                return default;

            if (context.ScriptInfos.TryGetValue(path, out ScriptInfo cached))
                return cached;

            ScriptInfo info;
            string guid = AssetDatabase.AssetPathToGUID(path);
            bool isSoundWriter = string.Equals(guid, TextAnimatorMigrationCompanionUtility.TAnimSoundWriterGuid, StringComparison.OrdinalIgnoreCase);
            bool isPackageScript = path.StartsWith(TextAnimatorMigrationAvailability.PackageRoot, StringComparison.OrdinalIgnoreCase) ||
                                   TextAnimatorMigrationAvailability.IsUnderFolder(path, context.SamplesFolder);
            if (isLibrary)
            {
                info = new ScriptInfo(
                    Path.GetFileName(path).StartsWith("Febucci.", StringComparison.Ordinal),
                    TextAnimatorMigrationArtifactKind.Other,
                    guid);
            }
            else if (!isSoundWriter && !isPackageScript && !context.DerivedScriptPaths.Contains(path))
            {
                // Most dependencies are unrelated scripts; they are never loaded.
                info = new ScriptInfo(false, TextAnimatorMigrationArtifactKind.Other, guid);
            }
            else
            {
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                Type type = script != null ? script.GetClass() : null;
                bool isTextAnimator = isSoundWriter || (type != null ? IsTextAnimatorRelatedType(type) : isPackageScript);
                TextAnimatorMigrationArtifactKind kind = isSoundWriter
                    ? TextAnimatorMigrationArtifactKind.SoundWriterComponent
                    : isTextAnimator && type != null
                        ? ClassifyComponentType(type)
                        : TextAnimatorMigrationArtifactKind.Other;
                info = new ScriptInfo(isTextAnimator, kind, guid);
            }

            context.ScriptInfos[path] = info;
            return info;
        }

        static HashSet<string> FindDerivedScriptPaths()
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddDerivedScriptPaths(TypeCache.GetTypesDerivedFrom<MonoBehaviour>(), paths);
            AddDerivedScriptPaths(TypeCache.GetTypesDerivedFrom<ScriptableObject>(), paths);
            return paths;
        }

        static void AddDerivedScriptPaths(TypeCache.TypeCollection types, HashSet<string> paths)
        {
            foreach (Type type in types)
            {
                if (type == null || type.IsAbstract || type.IsGenericTypeDefinition || type.FullName == null ||
                    type.FullName.StartsWith("Febucci.", StringComparison.Ordinal) || !IsTextAnimatorRelatedType(type))
                {
                    continue;
                }

                string[] guids = AssetDatabase.FindAssets(type.Name + " t:MonoScript");
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                    if (script != null && script.GetClass() == type)
                        paths.Add(path);
                }
            }
        }

        static bool IsTextAnimatorRelatedType(Type type)
        {
            for (Type cursor = type; cursor != null; cursor = cursor.BaseType)
            {
                string fullName = cursor.FullName;
                if (fullName != null && fullName.StartsWith("Febucci.", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        static TextAnimatorMigrationArtifactKind ClassifyComponentType(Type type)
        {
            if (!typeof(MonoBehaviour).IsAssignableFrom(type))
                return TextAnimatorMigrationArtifactKind.Other;

            for (Type cursor = type; cursor != null && cursor != typeof(MonoBehaviour); cursor = cursor.BaseType)
            {
                string name = cursor.Name;
                if (name.IndexOf("Action", StringComparison.Ordinal) >= 0)
                    return TextAnimatorMigrationArtifactKind.Other;
                if (name.IndexOf("Typewriter", StringComparison.OrdinalIgnoreCase) >= 0)
                    return TextAnimatorMigrationArtifactKind.TypewriterComponent;
                if (string.Equals(name, "TextAnimatorComponentBase", StringComparison.Ordinal) ||
                    name.StartsWith("TextAnimator_", StringComparison.Ordinal))
                {
                    return TextAnimatorMigrationArtifactKind.TextAnimatorComponent;
                }
            }

            return TextAnimatorMigrationArtifactKind.Other;
        }

        // One search with every Text Animator ScriptableObject type replaces loading each asset in the project.
        static string BuildTextAnimatorScriptableFilter()
        {
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (Type type in TypeCache.GetTypesDerivedFrom<ScriptableObject>())
            {
                if (type == null || type.IsAbstract || type.IsGenericTypeDefinition || !IsTextAnimatorRelatedType(type))
                    continue;

                names.Add(type.Name);
            }

            if (names.Count == 0)
                return string.Empty;

            StringBuilder builder = new StringBuilder(names.Count * 24);
            foreach (string name in names)
            {
                if (builder.Length > 0)
                    builder.Append(' ');
                builder.Append("t:").Append(name);
            }

            return builder.ToString();
        }

        // Only assemblies that reference Text Animator can use its API, which keeps the source scan small.
        static void ScanScripts(ScanContext context, string folder)
        {
            string prefix = folder.TrimEnd('/') + "/";
            HashSet<string> files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CompilationAssembly[] assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            for (int i = 0; i < assemblies.Length; i++)
            {
                CompilationAssembly assembly = assemblies[i];
                if (assembly == null || assembly.name.StartsWith("Febucci", StringComparison.Ordinal) ||
                    !ReferencesTextAnimator(assembly))
                {
                    continue;
                }

                string[] sourceFiles = assembly.sourceFiles;
                for (int f = 0; f < sourceFiles.Length; f++)
                {
                    string file = sourceFiles[f].Replace('\\', '/');
                    if (file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                        !TextAnimatorMigrationAvailability.IsUnderFolder(file, context.SamplesFolder))
                    {
                        files.Add(file);
                    }
                }
            }

            int index = 0;
            foreach (string file in files)
            {
                context.Report("Scripts", 0.95f + 0.05f * index++ / Mathf.Max(1, files.Count));
                if (TryFindScriptReference(file, out TextAnimatorMigrationScriptReference reference))
                    context.Scripts.Add(reference);
            }

            context.Scripts.Sort((first, second) => string.Compare(first.AssetPath, second.AssetPath, StringComparison.OrdinalIgnoreCase));
        }

        // Text Animator's precompiled DLLs are referenced by every assembly automatically, so only references to its
        // component assemblies identify code that can call the Text Animator API.
        static bool ReferencesTextAnimator(CompilationAssembly assembly)
        {
            CompilationAssembly[] references = assembly.assemblyReferences;
            for (int i = 0; i < references.Length; i++)
            {
                if (references[i] != null && references[i].name.StartsWith("Febucci.TextAnimator", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        static bool TryFindScriptReference(string path, out TextAnimatorMigrationScriptReference reference)
        {
            reference = default;
            string absolutePath = Path.GetFullPath(path);
            if (!File.Exists(absolutePath))
                return false;

            string text = File.ReadAllText(absolutePath);
            if (text.IndexOf("Febucci.", StringComparison.Ordinal) < 0)
                return false;

            string[] lines = text.Split('\n');
            int count = 0;
            int firstLine = 0;
            string snippet = string.Empty;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();
                if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("*", StringComparison.Ordinal))
                    continue;

                int matches = CountCodeReferences(line);
                if (matches == 0)
                    continue;

                count += matches;
                if (firstLine == 0)
                {
                    firstLine = i + 1;
                    snippet = trimmed.Length > 120 ? trimmed.Substring(0, 117) + "..." : trimmed;
                }
            }

            if (count == 0)
                return false;

            reference = new TextAnimatorMigrationScriptReference(path, firstLine, count, snippet);
            return true;
        }

        // Type names inside string literals, such as reflection lookups, keep compiling without Text Animator.
        internal static int CountCodeReferences(string line)
        {
            int count = 0;
            MatchCollection matches = s_scriptReference.Matches(line);
            for (int i = 0; i < matches.Count; i++)
            {
                int quotes = 0;
                int index = matches[i].Index;
                for (int c = 0; c < index; c++)
                {
                    if (line[c] == '"' && (c == 0 || line[c - 1] != '\\'))
                        quotes++;
                }

                if (quotes % 2 == 0)
                    count++;
            }

            return count;
        }
    }
}
