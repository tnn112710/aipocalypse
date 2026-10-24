using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    /// <summary>
    /// Editor extension point that adds Text Studio setup actions for selected TextMeshPro targets.
    /// Implementations must have a parameterless constructor.
    /// </summary>
    public interface ITextStudioTMPSetupProvider
    {
        /// <summary>Gets the user-facing setup action label.</summary>
        string DisplayName { get; }
        /// <summary>Gets guidance shown for the setup action.</summary>
        string Tooltip { get; }
        /// <summary>Gets the ordering among compatible setup providers.</summary>
        int SortOrder { get; }
        /// <summary>Reports whether the provider can configure the complete target selection.</summary>
        bool SupportsTargets(IReadOnlyList<TMP_Text> targets);
        /// <summary>Configures each compatible target as one undoable editor operation when requested.</summary>
        void AddToTargets(
            IReadOnlyList<TMP_Text> targets,
            bool focusFirstCreated,
            bool recordUndo);
    }

    /// <summary>Identifies TMP text input driven by an external authoring source without restricting its styling controls.</summary>
    public interface ITextStudioTMPTextInputProvider
    {
        /// <summary>Names the authoring source shown beside read-only displayed text.</summary>
        string SourceDisplayName { get; }
        /// <summary>Reports whether this TMP component's text is owned by the provider's external source.</summary>
        bool IsTextDriven(Component target);
    }

    /// <summary>Registers optional editor integrations that own a TMP target's displayed text.</summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class TextStudioTMPTextInputProviders
    {
        static readonly List<ITextStudioTMPTextInputProvider> s_providers =
            new List<ITextStudioTMPTextInputProvider>();

        public static void Register(ITextStudioTMPTextInputProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            if (!s_providers.Contains(provider))
                s_providers.Add(provider);
        }

        public static void Unregister(ITextStudioTMPTextInputProvider provider)
        {
            if (provider != null)
                s_providers.Remove(provider);
        }

        internal static bool TryGetProvider(
            UnityEngine.Object[] targets,
            out ITextStudioTMPTextInputProvider provider)
        {
            provider = null;
            if (targets == null || targets.Length == 0)
                return false;

            for (int i = 0; i < s_providers.Count; i++)
            {
                ITextStudioTMPTextInputProvider candidate = s_providers[i];
                bool allDriven = true;
                for (int j = 0; j < targets.Length; j++)
                {
                    if (targets[j] is not TMP_Text text || text == null || !candidate.IsTextDriven(text))
                    {
                        allDriven = false;
                        break;
                    }
                }
                if (!allDriven)
                    continue;

                provider = candidate;
                return true;
            }
            return false;
        }
    }

#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioTMPInspectorHeader
    {
        static readonly GUIContent s_addSingleContent = new GUIContent(
            "Add Text Studio",
            "Add a TextStudioText hub to this TextMeshPro object while preserving the current TMP text.");

        static readonly GUIContent s_addMultipleContent = new GUIContent(
            "Add Text Studio",
            "Add TextStudioText hubs to selected TextMeshPro objects that do not have one yet.");

        static readonly GUIContent s_drivenTextContent = new GUIContent("Displayed Text");
        static readonly GUIContent s_rightToLeftContent = new GUIContent(
            "Right To Left",
            "Use TextMeshPro's right-to-left text layout.");
        static readonly GUIContent s_textStyleContent = new GUIContent(
            "Text Style",
            "Apply a TextMeshPro style from the assigned style sheet.");

        static readonly List<TMP_Text> s_missingTargets = new List<TMP_Text>(8);
        static List<ITextStudioTMPSetupProvider> s_setupProviders;

        internal static VisualElement CreateSetupControlsForTargets(UnityEngine.Object[] targets)
        {
            VisualElement container = new VisualElement
            {
                name = "ts-tmp-integration"
            };
            container.AddToClassList("ts-tmp-integration");

            Action rebuild = null;
            rebuild = () =>
            {
                container.Clear();
                List<TMP_Text> missingTargets = new List<TMP_Text>(8);
                CollectMissingTargets(targets, missingTargets);

                bool hasMissingTargets = missingTargets.Count > 0;
                container.style.display = hasMissingTargets ? DisplayStyle.Flex : DisplayStyle.None;
                if (hasMissingTargets)
                    container.Add(CreateSetupControls(missingTargets, rebuild));
            };
            rebuild();
            return container;
        }

        internal static void DrawSetupControlsForTargets(UnityEngine.Object[] targets)
        {
            if (targets == null || targets.Length == 0)
                return;

            s_missingTargets.Clear();
            CollectMissingTargets(targets, s_missingTargets);

            if (s_missingTargets.Count > 0)
                DrawSetupControls(s_missingTargets);
        }

        internal static bool DrawDrivenTextInput(
            SerializedProperty text,
            SerializedProperty rightToLeft,
            SerializedProperty styleHash,
            GUIContent[] styleNames,
            IReadOnlyList<TMP_Style> styles,
            Dictionary<int, int> styleIndexes,
            ITextStudioTMPTextInputProvider provider)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(s_drivenTextContent, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Driven by " + provider.SourceDisplayName + ". Change displayed text at its source.",
                EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(text, GUIContent.none);

            EditorGUILayout.PropertyField(rightToLeft, s_rightToLeftContent);
            if (styleHash == null || styleNames == null || styles == null || styles.Count == 0)
                return false;

            styleIndexes.TryGetValue(styleHash.intValue, out int selectedStyle);
            Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            EditorGUI.BeginProperty(rect, s_textStyleContent, styleHash);
            bool previousMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = styleHash.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            selectedStyle = EditorGUI.Popup(rect, s_textStyleContent, selectedStyle, styleNames);
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = previousMixed;
            EditorGUI.EndProperty();
            if (!changed || selectedStyle < 0 || selectedStyle >= styles.Count)
                return false;

            styleHash.intValue = styles[selectedStyle].hashCode;
            return true;
        }

        internal static void RefreshDrivenTextStyles(UnityEngine.Object[] targets)
        {
            // Serialized style changes also need TMP's public setter to refresh its cached style and layout.
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] is TMP_Text text && text != null)
                {
                    TMP_Style style = text.textStyle;
                    text.textStyle = style;
                }
            }
        }

        static void CollectMissingTargets(
            UnityEngine.Object[] targets,
            List<TMP_Text> missingTargets)
        {
            if (targets == null)
                return;

            for (int i = 0; i < targets.Length; i++)
            {
                TMP_Text tmpText = targets[i] as TMP_Text;
                if (tmpText == null)
                    continue;

                if (tmpText.GetComponent<TextStudioText>() == null)
                    missingTargets.Add(tmpText);
            }
        }

        static VisualElement CreateSetupControls(
            IReadOnlyList<TMP_Text> missingTargets,
            Action rebuild)
        {
            List<ITextStudioTMPSetupProvider> setupProviders =
                new List<ITextStudioTMPSetupProvider>();
            CollectApplicableSetupProviders(missingTargets, setupProviders);
            string description = missingTargets.Count == 1
                ? "Add Text Studio while preserving the current TextMeshPro text as the editable source."
                : "Add Text Studio to the selected TextMeshPro objects that do not have it yet.";
            VisualElement section = TextStudioInspectorElements.CreateSection(
                "Text Studio",
                "ts-tmp-setup",
                TextStudioInspectorElements.CreateMutedText(description));
            VisualElement actions = TextStudioInspectorElements.CreateButtonRow();
            actions.AddToClassList("ts-tmp-setup-actions");
            Button add = TextStudioInspectorElements.CreateButton(
                missingTargets.Count == 1 ? "Add Text Studio" : "Add to Missing Objects",
                missingTargets.Count == 1 ? s_addSingleContent.tooltip : s_addMultipleContent.tooltip,
                () =>
                {
                    AddTextStudioToMissingTargets(missingTargets, true);
                    rebuild?.Invoke();
                },
                primary: true);
            add.name = "ts-tmp-add-text-studio";
            add.AddToClassList("ts-tmp-setup-action");
            add.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
            actions.Add(add);

            for (int i = 0; i < setupProviders.Count; i++)
            {
                ITextStudioTMPSetupProvider provider = setupProviders[i];
                Button extension = TextStudioInspectorElements.CreateButton(
                    "Add " + provider.DisplayName,
                    provider.Tooltip,
                    () =>
                    {
                        provider.AddToTargets(missingTargets, true, true);
                        rebuild?.Invoke();
                    },
                    primary: true);
                extension.name = "ts-tmp-add-extension-" + i;
                extension.AddToClassList("ts-tmp-setup-action");
                extension.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
                actions.Add(extension);
            }

            section.Add(actions);
            return section;
        }

        static void DrawSetupControls(IReadOnlyList<TMP_Text> missingTargets)
        {
            if (missingTargets == null || missingTargets.Count == 0)
                return;

            List<ITextStudioTMPSetupProvider> setupProviders =
                new List<ITextStudioTMPSetupProvider>();
            CollectApplicableSetupProviders(missingTargets, setupProviders);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Text Studio", EditorStyles.miniBoldLabel);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    GUIContent content = missingTargets.Count == 1 ? s_addSingleContent : s_addMultipleContent;
                    if (GUILayout.Button(content))
                        AddTextStudioToMissingTargets(missingTargets, true);

                    for (int i = 0; i < setupProviders.Count; i++)
                    {
                        ITextStudioTMPSetupProvider provider = setupProviders[i];
                        GUIContent extensionContent = new GUIContent(
                            "Add " + provider.DisplayName,
                            provider.Tooltip);
                        if (GUILayout.Button(extensionContent))
                            provider.AddToTargets(missingTargets, true, true);
                    }
                }
            }
        }

        static void CollectApplicableSetupProviders(
            IReadOnlyList<TMP_Text> targets,
            List<ITextStudioTMPSetupProvider> providers)
        {
            EnsureSetupProviders();
            providers.Clear();
            for (int i = 0; i < s_setupProviders.Count; i++)
            {
                ITextStudioTMPSetupProvider provider = s_setupProviders[i];
                try
                {
                    if (provider.SupportsTargets(targets))
                        providers.Add(provider);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[Text Studio] TMP setup provider failed: " +
                        provider.GetType().FullName + ". " + exception.Message);
                }
            }
        }

        static void EnsureSetupProviders()
        {
            if (s_setupProviders != null)
                return;

            s_setupProviders = new List<ITextStudioTMPSetupProvider>();
            foreach (Type providerType in TypeCache.GetTypesDerivedFrom<ITextStudioTMPSetupProvider>())
            {
                if (providerType == null || providerType.IsAbstract || providerType.IsInterface)
                    continue;

                try
                {
                    if (Activator.CreateInstance(providerType, true) is ITextStudioTMPSetupProvider provider)
                        s_setupProviders.Add(provider);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[Text Studio] Could not load TMP setup provider " +
                        providerType.FullName + ". " + exception.Message);
                }
            }

            s_setupProviders.Sort((first, second) =>
            {
                int order = first.SortOrder.CompareTo(second.SortOrder);
                return order != 0
                    ? order
                    : string.Compare(first.DisplayName, second.DisplayName, StringComparison.Ordinal);
            });
        }

        internal static IReadOnlyList<TextStudioText> AddTextStudioToMissingTargets(
            IReadOnlyList<TMP_Text> tmpTexts,
            bool focusFirstCreated,
            bool recordUndo = true)
        {
            if (tmpTexts == null || tmpTexts.Count == 0 || EditorApplication.isPlayingOrWillChangePlaymode)
                return Array.Empty<TextStudioText>();

            List<TextStudioText> created = new List<TextStudioText>();
            for (int i = 0; i < tmpTexts.Count; i++)
            {
                TMP_Text tmpText = tmpTexts[i];
                if (tmpText == null)
                    continue;

                GameObject gameObject = tmpText.gameObject;
                TextStudioText existing = gameObject.GetComponent<TextStudioText>();
                if (existing != null)
                    continue;

                string markup = tmpText.text ?? string.Empty;
                if (recordUndo)
                    Undo.RecordObject(tmpText, "Add Text Studio");

                TextStudioText hub = recordUndo
                    ? Undo.AddComponent<TextStudioText>(gameObject)
                    : gameObject.AddComponent<TextStudioText>();

                if (recordUndo)
                    Undo.RecordObject(hub, "Configure Text Studio");

                hub.SetTextDeferred(markup);
                if (tmpText.text != markup)
                {
                    tmpText.text = markup;
                    EditorUtility.SetDirty(tmpText);
                    RecordPrefabModifications(tmpText);
                }
                EditorUtility.SetDirty(hub);
                RecordPrefabModifications(hub);
                created.Add(hub);
            }

            if (focusFirstCreated && created.Count > 0)
                FocusHub(created[0]);

            return created;
        }

        static void FocusHub(TextStudioText hub)
        {
            if (hub == null)
                return;

            Selection.activeObject = hub;
            EditorGUIUtility.PingObject(hub);
        }

        static void RecordPrefabModifications(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (PrefabUtility.IsPartOfPrefabInstance(target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }
}
