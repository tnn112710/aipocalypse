using System;

namespace TextStudio
{
    internal interface ITextStudioEditorTickTarget
    {
        bool AdvanceEditorPreview(float deltaTime);
    }

    /// <summary>
    /// Static event hooks that allow the editor assembly to inject behavior into
    /// runtime components without requiring UnityEditor references in the runtime
    /// assembly. The editor assembly subscribes via <c>[InitializeOnLoad]</c>;
    /// runtime code invokes the hooks when editor integration is needed.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioEditorHooks
    {
        /// <summary>Fired from <see cref="TextStudioText.OnEnable"/>. Editor registers tick pump.</summary>
        internal static event Action<TextStudioText> HubEnabled;

        /// <summary>Fired from <see cref="TextStudioText.OnDisable"/>. Editor unregisters tick pump.</summary>
        internal static event Action<TextStudioText> HubDisabled;

        /// <summary>Request scene view repaint + player loop update (editor only, no-op at runtime).</summary>
        internal static event Action RepaintRequested;

        /// <summary>Fired before a direct backend write replaces canonical markup in edit mode.</summary>
        internal static event Action<TextStudioText> CanonicalSourceImporting;
        internal static event Action<ITextStudioEditorTickTarget> EditorTickTargetEnabled;
        internal static event Action<ITextStudioEditorTickTarget> EditorTickTargetDisabled;

        private static Action<TextStudioText> s_editorHubEnabled;
        private static Action<TextStudioText> s_editorHubDisabled;
        private static Action s_editorRepaintRequested;
        private static Action<UnityEngine.Object> s_editorDestroyObject;
        private static Action<TextStudioText> s_editorCanonicalSourceImporting;
        private static Action<ITextStudioEditorTickTarget> s_editorTickTargetEnabled;
        private static Action<ITextStudioEditorTickTarget> s_editorTickTargetDisabled;

        internal static void NotifyHubEnabled(TextStudioText hub) => HubEnabled?.Invoke(hub);
        internal static void NotifyHubDisabled(TextStudioText hub) => HubDisabled?.Invoke(hub);
        internal static void RequestRepaint() => RepaintRequested?.Invoke();
        internal static void NotifyCanonicalSourceImporting(TextStudioText hub) =>
            CanonicalSourceImporting?.Invoke(hub);
        internal static void NotifyEditorTickTargetEnabled(ITextStudioEditorTickTarget target) =>
            EditorTickTargetEnabled?.Invoke(target);
        internal static void NotifyEditorTickTargetDisabled(ITextStudioEditorTickTarget target) =>
            EditorTickTargetDisabled?.Invoke(target);

        internal static bool TryDestroyAfterEditorLifecycle(UnityEngine.Object value)
        {
            if (s_editorDestroyObject == null) return false;
            s_editorDestroyObject(value);
            return true;
        }

        internal static void SetEditorBridgeSubscribers(
            Action<TextStudioText> hubEnabled,
            Action<TextStudioText> hubDisabled,
            Action repaintRequested,
            Action<TextStudioText> canonicalSourceImporting,
            Action<UnityEngine.Object> destroyObject,
            Action<ITextStudioEditorTickTarget> tickTargetEnabled,
            Action<ITextStudioEditorTickTarget> tickTargetDisabled)
        {
            RemoveEditorBridgeSubscribers();

            s_editorHubEnabled = hubEnabled;
            s_editorHubDisabled = hubDisabled;
            s_editorRepaintRequested = repaintRequested;
            s_editorCanonicalSourceImporting = canonicalSourceImporting;
            s_editorDestroyObject = destroyObject;
            s_editorTickTargetEnabled = tickTargetEnabled;
            s_editorTickTargetDisabled = tickTargetDisabled;

            RestoreEditorBridgeSubscribers();
        }

        internal static void ResetEditorSubscribersForFastEnterPlayMode()
        {
            HubEnabled = null;
            HubDisabled = null;
            RepaintRequested = null;
            CanonicalSourceImporting = null;
            EditorTickTargetEnabled = null;
            EditorTickTargetDisabled = null;

            RestoreEditorBridgeSubscribers();
        }

        private static void RemoveEditorBridgeSubscribers()
        {
            if (s_editorHubEnabled != null) HubEnabled -= s_editorHubEnabled;
            if (s_editorHubDisabled != null) HubDisabled -= s_editorHubDisabled;
            if (s_editorRepaintRequested != null) RepaintRequested -= s_editorRepaintRequested;
            if (s_editorCanonicalSourceImporting != null)
                CanonicalSourceImporting -= s_editorCanonicalSourceImporting;
            if (s_editorTickTargetEnabled != null)
                EditorTickTargetEnabled -= s_editorTickTargetEnabled;
            if (s_editorTickTargetDisabled != null)
                EditorTickTargetDisabled -= s_editorTickTargetDisabled;
        }

        private static void RestoreEditorBridgeSubscribers()
        {
            if (s_editorHubEnabled != null) HubEnabled += s_editorHubEnabled;
            if (s_editorHubDisabled != null) HubDisabled += s_editorHubDisabled;
            if (s_editorRepaintRequested != null) RepaintRequested += s_editorRepaintRequested;
            if (s_editorCanonicalSourceImporting != null)
                CanonicalSourceImporting += s_editorCanonicalSourceImporting;
            if (s_editorTickTargetEnabled != null)
                EditorTickTargetEnabled += s_editorTickTargetEnabled;
            if (s_editorTickTargetDisabled != null)
                EditorTickTargetDisabled += s_editorTickTargetDisabled;
        }
    }
}
