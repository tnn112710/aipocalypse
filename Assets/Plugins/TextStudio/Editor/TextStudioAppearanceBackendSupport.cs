using UnityEditor;
using UnityEngine;

namespace TextStudio.Editor
{
    internal readonly struct TextStudioAppearanceBackendSummary
    {
        public TextStudioAppearanceBackendSummary(
            int targetCount,
            int unsupportedCount,
            string backendName)
        {
            TargetCount = targetCount;
            UnsupportedCount = unsupportedCount;
            BackendName = backendName ?? string.Empty;
        }

        public int TargetCount { get; }
        public int UnsupportedCount { get; }
        public string BackendName { get; }
        public bool HasUnsupportedTargets => UnsupportedCount > 0;
        public bool HasMixedSupport => UnsupportedCount > 0 && UnsupportedCount < TargetCount;
    }

    internal static class TextStudioAppearanceBackendSupport
    {
        public static TextStudioAppearanceBackendSummary EvaluateEditorTargets(
            Object[] targets)
        {
            if (targets == null || targets.Length == 0)
                return default;

            int targetCount = 0;
            int unsupportedCount = 0;
            string backendName = null;
            bool hasMultipleBackendNames = false;
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] is not TextStudioText text || text == null)
                    continue;

                targetCount++;
                RegisterTarget(
                    text,
                    ref unsupportedCount,
                    ref backendName,
                    ref hasMultipleBackendNames);
            }

            return new TextStudioAppearanceBackendSummary(
                targetCount,
                unsupportedCount,
                hasMultipleBackendNames ? string.Empty : backendName);
        }

        public static TextStudioAppearanceBackendSummary EvaluateSelection()
        {
            GameObject[] selection = Selection.gameObjects;
            int targetCount = 0;
            int unsupportedCount = 0;
            string backendName = null;
            bool hasMultipleBackendNames = false;
            for (int i = 0; i < selection.Length; i++)
            {
                TextStudioText text = selection[i] != null
                    ? selection[i].GetComponent<TextStudioText>()
                    : null;
                if (text == null)
                    continue;

                targetCount++;
                RegisterTarget(
                    text,
                    ref unsupportedCount,
                    ref backendName,
                    ref hasMultipleBackendNames);
            }

            return new TextStudioAppearanceBackendSummary(
                targetCount,
                unsupportedCount,
                hasMultipleBackendNames ? string.Empty : backendName);
        }

        internal static string GetAppearanceAssignmentNotice(TextStudioAppearanceBackendSummary summary)
        {
            if (!summary.HasUnsupportedTargets) return string.Empty;
            string affected = summary.TargetCount > 1
                ? summary.UnsupportedCount + " of " + summary.TargetCount + " selected objects"
                : "The selected object";
            string mixed = summary.HasMixedSupport
                ? " This style still renders on the other selected objects."
                : string.Empty;
            return affected + " will preserve this style without rendering it while the active backend owns glyph styling." + mixed;
        }

        static void RegisterTarget(
            TextStudioText text,
            ref int unsupportedCount,
            ref string backendName,
            ref bool hasMultipleBackendNames)
        {
            ITextStudioTextBackend backend = text.Backends.ActiveBackend;
            if (backend is not ITextStudioTextBackendAppearanceSupport support ||
                support.SupportsTextStudio2DAppearance)
            {
                return;
            }

            unsupportedCount++;
            string displayName = string.IsNullOrWhiteSpace(backend.DisplayName)
                ? "The active text backend"
                : backend.DisplayName;
            if (backendName == null)
                backendName = displayName;
            else if (backendName != displayName)
                hasMultipleBackendNames = true;
        }
    }
}
