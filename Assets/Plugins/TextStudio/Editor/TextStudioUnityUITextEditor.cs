using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;

namespace TextStudio.Editor
{
    /// <summary>Editor setup API for preserving and configuring legacy Unity UI Text targets.</summary>
    public static class TextStudioUnityUITextSetup
    {
        const string UndoName = "Configure Unity UI Text for Text Studio";

        /// <summary>Gets whether the Text is the editable field owned by a legacy Input Field.</summary>
        public static bool IsEditableInputFieldTarget(Text target)
        {
            if (target == null)
                return false;
            InputField input = target.GetComponentInParent<InputField>();
            return input != null && ReferenceEquals(input.textComponent, target);
        }

        /// <summary>Gets whether Text Studio executes before every downstream Canvas mesh modifier.</summary>
        public static bool HasValidModifierOrder(Text target)
        {
            if (target == null)
                return false;
            TextStudioUnityUITextBackend backend =
                target.GetComponent<TextStudioUnityUITextBackend>();
            if (backend == null)
                return false;

            Component[] components = target.GetComponents<Component>();
            int backendIndex = Array.IndexOf(components, backend);
            if (backendIndex < 0)
                return false;
            for (int i = 0; i < backendIndex; i++)
            {
                if (components[i] is IMeshModifier &&
                    !ReferenceEquals(components[i], backend))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Configures all compatible targets in one undoable operation.</summary>
        public static IReadOnlyList<TextStudioText> ConfigureTargets(
            IReadOnlyList<Text> targets,
            bool focusFirstConfigured = true,
            bool recordUndo = true)
        {
            if (targets == null || targets.Count == 0 ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return Array.Empty<TextStudioText>();
            }

            if (recordUndo)
            {
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName(UndoName);
            }
            int undoGroup = Undo.GetCurrentGroup();
            List<TextStudioText> configured = new List<TextStudioText>(targets.Count);
            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    TextStudioText hub = ConfigureTarget(targets[i], recordUndo);
                    if (hub != null && !configured.Contains(hub))
                        configured.Add(hub);
                }
            }
            finally
            {
                if (recordUndo)
                    Undo.CollapseUndoOperations(undoGroup);
            }

            if (focusFirstConfigured && configured.Count > 0)
            {
                Selection.activeObject = configured[0];
                EditorGUIUtility.PingObject(configured[0]);
            }
            return configured;
        }

        /// <summary>Configures one compatible target while preserving its visible text source.</summary>
        public static TextStudioText ConfigureTarget(Text target, bool recordUndo = true)
        {
            if (target == null ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                IsEditableInputFieldTarget(target))
            {
                return null;
            }

            GameObject gameObject = target.gameObject;
            TextStudioText hub = gameObject.GetComponent<TextStudioText>();
            string markup = hub != null && !string.IsNullOrEmpty(hub.Markup)
                ? hub.Markup
                : target.text ?? string.Empty;

            if (recordUndo)
                Undo.RecordObject(target, UndoName);
            if (hub == null)
            {
                hub = recordUndo
                    ? Undo.AddComponent<TextStudioText>(gameObject)
                    : gameObject.AddComponent<TextStudioText>();
            }

            TextStudioUnityUITextBackend backend =
                gameObject.GetComponent<TextStudioUnityUITextBackend>();
            if (backend == null)
            {
                backend = recordUndo
                    ? Undo.AddComponent<TextStudioUnityUITextBackend>(gameObject)
                    : gameObject.AddComponent<TextStudioUnityUITextBackend>();
            }
            else if (!backend.enabled)
            {
                if (recordUndo)
                    Undo.RecordObject(backend, UndoName);
                backend.enabled = true;
            }

            if (recordUndo)
            {
                Undo.RecordObject(hub, UndoName);
                Undo.RecordObject(backend, UndoName);
            }
            hub.SetTextDeferred(markup);
            hub.Backends.Refresh();
            EnsureModifierOrder(target);

            EditorUtility.SetDirty(target);
            EditorUtility.SetDirty(hub);
            EditorUtility.SetDirty(backend);
            RecordPrefabModifications(target);
            RecordPrefabModifications(hub);
            RecordPrefabModifications(backend);
            return hub;
        }

        /// <summary>Moves the backend before Outline, Shadow, and custom Canvas mesh modifiers.</summary>
        public static bool EnsureModifierOrder(Text target)
        {
            if (target == null)
                return false;
            TextStudioUnityUITextBackend backend =
                target.GetComponent<TextStudioUnityUITextBackend>();
            if (backend == null)
                return false;

            bool changed = false;
            while (!HasValidModifierOrder(target))
            {
                if (!ComponentUtility.MoveComponentUp(backend))
                    break;
                changed = true;
            }
            if (changed)
            {
                EditorUtility.SetDirty(backend);
                RecordPrefabModifications(backend);
            }
            return HasValidModifierOrder(target);
        }

        static void RecordPrefabModifications(UnityEngine.Object target)
        {
            if (target != null && PrefabUtility.IsPartOfPrefabInstance(target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }

#if !TEXTSTUDIO_DISABLE_UNITY_UI_TEXT_INSPECTOR_ENHANCEMENT
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class TextStudioUnityUITextInspectorHeader
    {
        static readonly List<Text> s_targets = new List<Text>(8);
        static readonly List<Text> s_configurable = new List<Text>(8);

        internal static void Draw(UnityEngine.Object[] inspectedTargets)
        {
            CollectTargets(inspectedTargets);
            if (s_targets.Count == 0)
                return;

            int inputFieldTargets = 0;
            int missingSetup = 0;
            int invalidOrder = 0;
            int disabledBackends = 0;
            s_configurable.Clear();
            for (int i = 0; i < s_targets.Count; i++)
            {
                Text target = s_targets[i];
                if (TextStudioUnityUITextSetup.IsEditableInputFieldTarget(target))
                {
                    inputFieldTargets++;
                    continue;
                }

                TextStudioText hub = target.GetComponent<TextStudioText>();
                TextStudioUnityUITextBackend backend =
                    target.GetComponent<TextStudioUnityUITextBackend>();
                if (hub == null || backend == null)
                {
                    missingSetup++;
                    s_configurable.Add(target);
                }
                else if (!backend.enabled)
                {
                    disabledBackends++;
                    s_configurable.Add(target);
                }
                else if (!TextStudioUnityUITextSetup.HasValidModifierOrder(target))
                {
                    invalidOrder++;
                    s_configurable.Add(target);
                }
            }

            if (missingSetup == 0 && invalidOrder == 0 &&
                inputFieldTargets == 0 && disabledBackends == 0)
            {
                return;
            }

            EditorGUILayout.Space(2f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Text Studio", EditorStyles.boldLabel);
                if (inputFieldTargets > 0)
                {
                    EditorGUILayout.HelpBox(
                        "Editable Input Field text is live user input, so Text Studio does not take ownership of it. Use a separate Text component for animated presentation text.",
                        MessageType.Warning);
                }
                if (disabledBackends > 0)
                {
                    EditorGUILayout.HelpBox(
                        "The Unity UI Text backend is disabled. Enable it to restore Text Studio output.",
                        MessageType.Warning);
                }
                if (missingSetup > 0)
                {
                    EditorGUILayout.LabelField(
                        missingSetup == 1
                            ? "Add Text Studio while preserving this Text component and its current text."
                            : "Add Text Studio to compatible selected Text components while preserving their current text.",
                        EditorStyles.wordWrappedLabel);
                }
                if (invalidOrder > 0)
                {
                    EditorGUILayout.HelpBox(
                        "Text Studio must run before Outline, Shadow, and custom mesh modifiers so those effects receive the animated glyph geometry.",
                        MessageType.Warning);
                }

                using (new EditorGUI.DisabledScope(
                           s_configurable.Count == 0 ||
                           EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    string label = missingSetup > 0
                        ? s_targets.Count == 1
                            ? "Add Text Studio"
                            : "Configure Compatible Objects"
                        : "Fix Effect Order";
                    GUIContent action = new GUIContent(
                        label,
                        missingSetup > 0
                            ? "Preserve the current Unity UI Text component and add its Text Studio hub and backend."
                            : "Move the Text Studio backend before downstream Canvas mesh effects.");
                    if (GUILayout.Button(action))
                    {
                        TextStudioUnityUITextSetup.ConfigureTargets(
                            s_configurable,
                            focusFirstConfigured: true,
                            recordUndo: true);
                    }
                }

                EditorGUILayout.LabelField(
                    "Reveal, placement, per-glyph transforms, colors, masks, CanvasGroup, Outline, and Shadow are supported. TMP material effects and Text Studio 2D Appearance are not available on this backend.",
                    EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.Space(2f);
        }

        static void CollectTargets(UnityEngine.Object[] inspectedTargets)
        {
            s_targets.Clear();
            if (inspectedTargets == null)
                return;
            for (int i = 0; i < inspectedTargets.Length; i++)
            {
                Text target = inspectedTargets[i] as Text;
                if (target != null && !s_targets.Contains(target))
                    s_targets.Add(target);
            }
        }
    }

    [CustomEditor(typeof(Text), true)]
    [CanEditMultipleObjects]
    internal sealed class TextStudioUnityUITextEditor : UnityEditor.UI.TextEditor
    {
        public override void OnInspectorGUI()
        {
            TextStudioUnityUITextInspectorHeader.Draw(targets);
            base.OnInspectorGUI();
        }
    }
}
#endif
