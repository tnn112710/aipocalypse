using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace TextStudio.Editor
{
    internal enum TextStudioOnboardingStage
    {
        SeeIt = 0,
        UnderstandIt = 1,
        ExploreFurther = 2
    }

    internal interface ITextStudioOnboardingPreferenceStore
    {
        int GetInt(string key, int defaultValue);
        void SetInt(string key, int value);
    }

    internal sealed class TextStudioEditorPreferenceStore : ITextStudioOnboardingPreferenceStore
    {
        public int GetInt(string key, int defaultValue)
        {
            return EditorPrefs.GetInt(key, defaultValue);
        }

        public void SetInt(string key, int value)
        {
            EditorPrefs.SetInt(key, value);
        }
    }

#if UNITY_6000_7_OR_NEWER
    // The preference adapter is stateless and remains valid for the editor code-load lifetime.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioOnboardingState
    {
        internal const string ProgressPreferenceKey = "TextStudio.OnboardingProgress";

        static readonly ITextStudioOnboardingPreferenceStore EditorStore =
            new TextStudioEditorPreferenceStore();

        internal static ITextStudioOnboardingPreferenceStore DefaultStore => EditorStore;

        internal static TextStudioOnboardingStage GetProgress()
        {
            return GetProgress(EditorStore);
        }

        internal static TextStudioOnboardingStage GetProgress(
            ITextStudioOnboardingPreferenceStore store)
        {
            int value = store.GetInt(
                ProgressPreferenceKey,
                (int)TextStudioOnboardingStage.SeeIt);
            value = Math.Max(
                (int)TextStudioOnboardingStage.SeeIt,
                Math.Min((int)TextStudioOnboardingStage.ExploreFurther, value));
            return (TextStudioOnboardingStage)value;
        }

        internal static void AdvanceTo(TextStudioOnboardingStage stage)
        {
            AdvanceTo(stage, EditorStore);
        }

        internal static void AdvanceTo(
            TextStudioOnboardingStage stage,
            ITextStudioOnboardingPreferenceStore store)
        {
            TextStudioOnboardingStage current = GetProgress(store);
            if (stage > current)
                store.SetInt(ProgressPreferenceKey, (int)stage);
        }

        internal static void Restart()
        {
            Restart(EditorStore);
        }

        internal static void Restart(ITextStudioOnboardingPreferenceStore store)
        {
            store.SetInt(
                ProgressPreferenceKey,
                (int)TextStudioOnboardingStage.SeeIt);
        }
    }

    internal interface ITextStudioOnboardingSceneService
    {
        string ActiveScenePath { get; }
        bool SaveCurrentModifiedScenesIfUserWantsTo();
        void OpenScene(string path);
    }

    internal sealed class TextStudioOnboardingSceneService : ITextStudioOnboardingSceneService
    {
        public string ActiveScenePath => SceneManager.GetActiveScene().path;

        public bool SaveCurrentModifiedScenesIfUserWantsTo()
        {
            return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        public void OpenScene(string path)
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
    }

    internal static class TextStudioOnboardingActions
    {
        internal const string DropInLessonSceneName = "Text Studio Showcase";

        internal static bool IsDropInLessonOpen(ITextStudioOnboardingSceneService sceneService)
        {
            string path = sceneService.ActiveScenePath;
            return !string.IsNullOrEmpty(path) &&
                   path.Replace('\\', '/').EndsWith(
                       "/" + DropInLessonSceneName + ".unity",
                       StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryOpenDropInLesson(
            ITextStudioOnboardingSceneService sceneService,
            ITextStudioOnboardingPreferenceStore preferenceStore,
            out string error)
        {
            error = string.Empty;
            if (!TryFindDropInLesson(out TextStudioExampleEntry lesson))
            {
                error = "The packaged Text Studio Showcase could not be found.";
                return false;
            }

            string path = TextStudioExampleCatalog.ResolveScenePath(lesson);
            if (string.IsNullOrEmpty(path))
            {
                error = "The packaged Text Studio Showcase scene could not be resolved.";
                return false;
            }

            if (!sceneService.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                error = "The lesson was not opened because the current scene was left unchanged.";
                return false;
            }

            try
            {
                sceneService.OpenScene(path);
                TextStudioOnboardingState.AdvanceTo(
                    TextStudioOnboardingStage.UnderstandIt,
                    preferenceStore);
                return true;
            }
            catch (Exception exception)
            {
                error = "Unity could not open the lesson: " + exception.Message;
                return false;
            }
        }

        internal static bool TryFindDropInLesson(out TextStudioExampleEntry lesson)
        {
            System.Collections.Generic.IReadOnlyList<TextStudioExampleEntry> entries =
                TextStudioExampleCatalog.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                TextStudioExampleEntry candidate = entries[i];
                if (candidate.Category == TextStudioExampleCategory.Learn &&
                    string.Equals(
                        candidate.SceneName,
                        DropInLessonSceneName,
                        StringComparison.Ordinal))
                {
                    lesson = candidate;
                    return true;
                }
            }

            lesson = default;
            return false;
        }

        internal static void ContinueToLearnPath(
            ITextStudioOnboardingPreferenceStore preferenceStore,
            Action showLearnPath)
        {
            TextStudioOnboardingState.AdvanceTo(
                TextStudioOnboardingStage.ExploreFurther,
                preferenceStore);
            showLearnPath?.Invoke();
        }
    }
}
