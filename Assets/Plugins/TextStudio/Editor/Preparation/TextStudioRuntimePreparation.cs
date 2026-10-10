using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TextStudio.Editor
{
    /// <summary>Shared preparation lifecycle for the Editor and explicit player or content build roots.</summary>
    [InitializeOnLoad]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static partial class TextStudioRuntimePreparation
    {
        static readonly List<ITextStudioRuntimePreparationProvider> s_providers = new List<ITextStudioRuntimePreparationProvider>();
        static readonly List<ITextStudioPreparationTextContributor> s_contributors = new List<ITextStudioPreparationTextContributor>();
        static readonly Dictionary<TextStudioText, string> s_observed = new Dictionary<TextStudioText, string>();
        static IEnumerator s_operation;
        static double s_due;
        static bool s_pending, s_blocking, s_automaticOperation;
        static int s_automationSuspensions;
        public static bool IsBusy => s_operation != null || s_blocking;
        public static TextStudioPreparationReport LastReport { get; private set; } = new TextStudioPreparationReport();
        public static bool FontEmbeddingAllowed => TextStudioRuntimePreparationSettings.instance.embeddingChoiceMade && TextStudioRuntimePreparationSettings.instance.allowFontEmbedding;

        static TextStudioRuntimePreparation()
        {
            Register(new TextStudioAppearancePreparationProvider());
            EditorApplication.update += Update;
            EditorApplication.hierarchyChanged += RequestRefresh;
            EditorApplication.projectChanged += RequestRefresh;
            ObjectChangeEvents.changesPublished += OnObjectsChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Cancel;
            EditorApplication.quitting += Cancel;
            RequestRefresh();
        }

        public static void Register(ITextStudioRuntimePreparationProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (s_providers.Any(existing => existing.Id == provider.Id)) return;
            s_providers.Add(provider);
            s_providers.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            RequestRefresh();
        }

        public static void RegisterTextContributor(ITextStudioPreparationTextContributor contributor)
        {
            if (contributor == null) throw new ArgumentNullException(nameof(contributor));
            if (!s_contributors.Contains(contributor)) s_contributors.Add(contributor);
            RequestRefresh();
        }

        internal static void CollectText(TextStudioText text, ICollection<string> samples)
        { foreach (ITextStudioPreparationTextContributor contributor in s_contributors) contributor.Collect(text, samples); }

        internal static void RequestRefresh() { s_pending = true; s_due = EditorApplication.timeSinceStartup + 1; }
        static void OnObjectsChanged(ref ObjectChangeEventStream changes) => RequestRefresh();

        public static TextStudioPreparationReport Analyze(IEnumerable<TextStudioText> texts)
        {
            if (texts == null) throw new ArgumentNullException(nameof(texts));
            TextStudioPreparationReport report = new TextStudioPreparationReport();
            foreach (TextStudioText text in texts.Where(text => text != null).Distinct())
                foreach (ITextStudioRuntimePreparationProvider provider in s_providers)
                {
                    int first = report.Items.Count;
                    try { provider.Analyze(text, report); }
                    catch (Exception exception) { report.Add(Failure(provider.Id, text, exception)); }
                    TagText(report, first, text, provider);
                }
            return report;
        }

        static void TagText(TextStudioPreparationReport report, int first, TextStudioText text, ITextStudioRuntimePreparationProvider provider)
        {
            for (int i = first; i < report.Items.Count; i++)
            {
                report.Items[i].SourceText = text;
                if (report.Items[i].Presentation == null && provider is ITextStudioPreparationPresentationProvider display)
                    report.Items[i].Presentation = display.Presentation;
            }
        }

        public static bool Prepare(IEnumerable<TextStudioText> texts)
        {
            if (texts == null) throw new ArgumentNullException(nameof(texts));
            if (IsBusy) return false;
            s_automaticOperation = false;
            LastReport = new TextStudioPreparationReport { IsComplete = false };
            s_operation = PrepareTexts(texts.Where(text => text != null).Distinct().ToArray(), EditorUserBuildSettings.activeBuildTarget, LastReport);
            return true;
        }

        static IEnumerator PrepareTexts(IEnumerable<TextStudioText> texts, BuildTarget target, TextStudioPreparationReport report)
        {
            foreach (TextStudioText text in texts)
            {
                if (text == null) continue;
                string sourceFingerprint = CaptureFingerprint(text);
                foreach (ITextStudioRuntimePreparationProvider provider in s_providers.ToArray())
                {
                    int first = report.Items.Count;
                    IEnumerator work;
                    try { work = provider.Prepare(text, target, report); }
                    catch (Exception exception) { report.Add(Failure(provider.Id, text, exception)); TagText(report, first, text, provider); continue; }
                    if (work == null) { TagText(report, first, text, provider); continue; }
                    using (work as IDisposable)
                    {
                        while (true)
                        {
                            bool more;
                            try { more = work.MoveNext(); TagText(report, first, text, provider); }
                            catch (Exception exception)
                            {
                                bool recorded = false;
                                foreach (TextStudioPreparationItem item in report.Items)
                                    if (item.Provider == provider.Id && item.Target == text.name && item.State == TextStudioPreparationState.Updating)
                                    { item.State = TextStudioPreparationState.NeedsAttention; item.Message = exception.Message; recorded = true; }
                                if (!recorded) report.Add(Failure(provider.Id, text, exception));
                                TagText(report, first, text, provider);
                                break;
                            }
                            if (!more) break;
                            yield return null;
                        }
                    }
                }
                if (text != null && !EditorSceneManager.IsPreviewScene(text.gameObject.scene))
                {
                    string currentFingerprint = CaptureFingerprint(text);
                    if (currentFingerprint == sourceFingerprint) s_observed[text] = currentFingerprint;
                    else RequestRefresh();
                }
                yield return null;
            }
        }

        static TextStudioPreparationItem Failure(string provider, TextStudioText text, Exception exception) =>
            new TextStudioPreparationItem(provider, text != null ? text.name : "Removed text", TextStudioPreparationState.NeedsAttention, exception.Message) { SourceText = text };

        public static void Cancel()
        {
            IEnumerator operation = s_operation; s_operation = null;
            (operation as IDisposable)?.Dispose();
            if (operation != null && !LastReport.IsComplete)
                LastReport.Add(new TextStudioPreparationItem("Scope check", "Current scope", TextStudioPreparationState.NeedsAttention,
                    "The operation was canceled before all content was checked. Run it again for a complete overview.")
                    { Consequence = "This overview is incomplete. A player build still checks its own content when build preparation is enabled." });
            foreach (TextStudioPreparationItem item in LastReport.Items)
                if (item.State == TextStudioPreparationState.Updating)
                { item.State = TextStudioPreparationState.NeedsAttention; item.Message = "Preparation canceled. Prepare again to finish remaining work."; }
        }

        static void Update()
        {
            if (s_blocking) return;
            if (s_operation != null)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) { Cancel(); return; }
                try { if (!s_operation.MoveNext()) { (s_operation as IDisposable)?.Dispose(); s_operation = null; LastReport.IsComplete = true; } }
                catch (Exception exception)
                {
                    LastReport.Add(new TextStudioPreparationItem("Preparation", "Current scope", TextStudioPreparationState.NeedsAttention, exception.Message));
                    Debug.LogException(exception); Cancel();
                }
                return;
            }
            if (TextStudioFontAppearancePreparation.IsBusy || TextStudioColorAppearancePreparation.IsBusy) return;
            if (s_automationSuspensions > 0 || !s_pending || EditorApplication.timeSinceStartup < s_due || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !TextStudioRuntimePreparationSettings.instance.automaticInEditor) return;
            s_pending = false;
            foreach (TextStudioText removed in s_observed.Keys.Where(text => text == null).ToArray()) s_observed.Remove(removed);
            TextStudioText[] changed = OpenTexts().Where(text => !s_observed.TryGetValue(text, out string previous) || previous != CaptureFingerprint(text)).ToArray();
            if (changed.Length > 0 && Prepare(changed)) s_automaticOperation = true;
        }

        internal static IDisposable SuspendAutomation()
        {
            s_automationSuspensions++;
            if (s_automaticOperation) Cancel();
            return new AutomationSuspension();
        }

        sealed class AutomationSuspension : IDisposable
        {
            bool _disposed;
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true; s_automationSuspensions--;
                RequestRefresh();
            }
        }

        internal static TextStudioText[] OpenTexts() => Resources.FindObjectsOfTypeAll<TextStudioText>()
            .Where(text => text != null && !EditorUtility.IsPersistent(text) && text.gameObject.scene.IsValid() &&
                text.gameObject.scene.isLoaded && !EditorSceneManager.IsPreviewScene(text.gameObject.scene) &&
                (text.hideFlags & HideFlags.DontSave) == 0).ToArray();

        internal static string CaptureFingerprint(TextStudioText text)
        {
            if (text == null) return string.Empty;
            string value = EditorJsonUtility.ToJson(text) + EditorJsonUtility.ToJson(TextStudioRuntimePreparationSettings.instance);
            HashSet<string> dependencies = new HashSet<string>(StringComparer.Ordinal);
            if (text.TMPText != null)
            {
                value += text.TMPText.text;
                string path = AssetDatabase.GetAssetPath(text.TMPText.font);
                value += path + (path.Length > 0 ? AssetDatabase.GetAssetDependencyHash(path).ToString() : string.Empty);
            }
            foreach (MonoBehaviour component in text.GetComponents<MonoBehaviour>())
            {
                if (component == null) continue;
                if (component != text) value += EditorJsonUtility.ToJson(component);
                SerializedObject serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null && EditorUtility.IsPersistent(property.objectReferenceValue))
                        dependencies.Add(AssetDatabase.GetAssetPath(property.objectReferenceValue));
            }
            foreach (string dependency in dependencies.OrderBy(path => path, StringComparer.Ordinal)) value += AssetDatabase.GetAssetDependencyHash(dependency);
            List<string> samples = new List<string>(); CollectText(text, samples);
            foreach (string sample in samples) value += sample;
            return Hash128.Compute(value).ToString();
        }

        /// <summary>Prepares explicit content roots synchronously, including dependencies. Custom content builders call this before dependency collection.
        /// It never saves open scenes. Scene copies receive generated bindings in the player scene callback.</summary>
        public static TextStudioPreparationReport PrepareAssets(IEnumerable<string> rootAssetPaths, BuildTarget target)
        {
            if (rootAssetPaths == null) throw new ArgumentNullException(nameof(rootAssetPaths));
            bool canceledOwnedOperation = s_operation != null;
            Cancel();
            if (canceledOwnedOperation)
            {
                DateTime deadline = DateTime.UtcNow.AddSeconds(30);
                while (TextStudioFontAppearancePreparation.IsBusy && DateTime.UtcNow < deadline)
                { TextStudioFontAppearancePreparation.Update(); Thread.Sleep(1); }
            }
            if (TextStudioFontAppearancePreparation.IsBusy || TextStudioColorAppearancePreparation.IsBusy)
                throw new InvalidOperationException("Finish or cancel the active appearance preparation before starting a content build.");
            if (s_blocking) throw new InvalidOperationException("A content preparation operation is already running.");
            s_blocking = true;
            TextStudioPreparationReport report = new TextStudioPreparationReport();
            try
            {
                string[] roots = rootAssetPaths.Where(path => !string.IsNullOrEmpty(path)).Distinct().ToArray();
                if (roots.Length == 0) return report;
                string[] paths = AssetDatabase.GetDependencies(roots, true).OrderBy(path => path, StringComparer.Ordinal).ToArray();
                string textScript = FindTextScript();
                foreach (string path in paths)
                {
                    if (!path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!AssetDatabase.GetDependencies(path, true).Contains(textScript)) continue;
                    if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    {
                        Scene scene = EditorSceneManager.OpenPreviewScene(path);
                        try { RunBlocking(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TextStudioText>(true)), target, report); }
                        finally { EditorSceneManager.ClosePreviewScene(scene); }
                    }
                    else if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    {
                        GameObject root = PrefabUtility.LoadPrefabContents(path);
                        try
                        {
                            TextStudioText[] texts = root.GetComponentsInChildren<TextStudioText>(true);
                            string before = string.Join("", texts.Select(text => EditorJsonUtility.ToJson(text)));
                            RunBlocking(texts, target, report);
                            foreach (TextStudioText text in texts) TextStudioRuntimePreparationBuildProcessor.RemoveStaleOptionalBindings(text);
                            if (before != string.Join("", texts.Select(text => EditorJsonUtility.ToJson(text)))) PrefabUtility.SaveAsPrefabAsset(root, path);
                        }
                        finally { PrefabUtility.UnloadPrefabContents(root); }
                    }
                }
                return report;
            }
            finally { LastReport = report; s_blocking = false; }
        }

        static void RunBlocking(IEnumerable<TextStudioText> texts, BuildTarget target, TextStudioPreparationReport report)
        {
            IEnumerator operation = PrepareTexts(texts, target, report);
            using (operation as IDisposable)
                while (operation.MoveNext()) Thread.Sleep(1);
        }
    }
}
