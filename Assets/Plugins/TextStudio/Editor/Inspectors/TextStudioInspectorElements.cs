using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using TextStudio.BuiltIns;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioInspectorElements
    {
        const string CodeFontResourcePath = "Fonts/RobotoMono/RobotoMono-Regular.ttf";
        const string TitlelessSectionClass = "ts-section--titleless";

        static Font s_CodeFont;

        sealed class AlignedControlField : BaseField<bool>
        {
            public AlignedControlField(string label, VisualElement input)
                : base(label, input)
            {
            }
        }

        internal sealed class LayerCardShell
        {
            readonly Image _visibilityIcon;

            internal VisualElement Card { get; }
            internal Foldout Foldout { get; }
            internal VisualElement Actions { get; }
            internal Button VisibilityButton { get; }
            internal Button SoloButton { get; }
            internal Button MoveUpButton { get; }
            internal Button MoveDownButton { get; }
            internal Button DeleteButton { get; }

            internal LayerCardShell(
                VisualElement card,
                Foldout foldout,
                VisualElement actions,
                Button visibilityButton,
                Image visibilityIcon,
                Button soloButton,
                Button moveUpButton,
                Button moveDownButton,
                Button deleteButton)
            {
                Card = card;
                Foldout = foldout;
                Actions = actions;
                VisibilityButton = visibilityButton;
                _visibilityIcon = visibilityIcon;
                SoloButton = soloButton;
                MoveUpButton = moveUpButton;
                MoveDownButton = moveDownButton;
                DeleteButton = deleteButton;
            }

            internal void Refresh(
                string title,
                bool enabled,
                bool solo,
                bool canMoveUp,
                bool canMoveDown)
            {
                Foldout.text = title ?? string.Empty;
                Card.EnableInClassList("ts-layer-card--disabled", !enabled);
                Card.EnableInClassList("ts-layer-card--solo", solo);
                Actions.EnableInClassList("ts-layer-card-actions--persistent", solo);

                GUIContent visibilityContent = EditorGUIUtility.IconContent(
                    enabled
                        ? "animationvisibilitytoggleon"
                        : "animationvisibilitytoggleoff");
                _visibilityIcon.image = visibilityContent.image;
                _visibilityIcon.style.display = visibilityContent.image != null
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                VisibilityButton.text = visibilityContent.image == null
                    ? (enabled ? "On" : "Off")
                    : string.Empty;
                VisibilityButton.tooltip = enabled
                    ? "Mute this layer without discarding its settings."
                    : "Unmute this layer.";
                VisibilityButton.EnableInClassList(
                    "ts-layer-card-header-button--off",
                    !enabled);

                SoloButton.tooltip = solo
                    ? "Show every enabled layer in this stack again."
                    : "Keep only this layer active. Solo is saved with the settings.";
                SoloButton.SetEnabled(enabled);
                SoloButton.EnableInClassList("ts-authoring-mode-active", solo);
                MoveUpButton.SetEnabled(canMoveUp);
                MoveDownButton.SetEnabled(canMoveDown);
            }
        }

        public const string CustomTimingPresetChoice = "Custom";

        public static VisualElement CreateRoot(string name)
        {
            VisualElement root = CommonInspectorElements.CreateRoot(
                name,
                TextStudioTextEditor.LoadTextStudioInspectorStyleSheet(),
                "ts-inspector-root");
            root.EnableInClassList("ts-theme-dark", EditorGUIUtility.isProSkin);
            root.EnableInClassList("ts-theme-light", !EditorGUIUtility.isProSkin);
            return root;
        }

        public static VisualElement CreateRoot(string name, StyleSheet productStyleSheet, string productClass)
        {
            VisualElement root = CreateRoot(name);
            CommonUITK.ApplyRoot(root, productStyleSheet, productClass);
            return root;
        }

        internal static void ConfigureCodeTextField(TextField field)
        {
            if (field == null)
                return;

            TextElement textElement = field.Q<TextElement>(
                className: TextElement.selectableUssClassName);
            if (textElement == null)
                return;

            textElement.enableRichText = false;
            ApplyCodeFont(textElement);
        }

        internal static void ApplyCodeFont(TextElement textElement)
        {
            if (textElement == null)
                return;

            if (s_CodeFont == null)
                s_CodeFont = EditorGUIUtility.Load(CodeFontResourcePath) as Font;

            if (s_CodeFont != null)
                textElement.style.unityFontDefinition = FontDefinition.FromFont(s_CodeFont);
        }

        public static StyleSheet LoadStyleSheetFromAnchor(
            string anchorAssetName,
            string anchorPathSuffix,
            string stylePathSuffix)
        {
            return CommonUITK.LoadStyleSheetFromAnchor(anchorAssetName, anchorPathSuffix, stylePathSuffix);
        }

        public static VisualElement CreateSection(string title, string name, params VisualElement[] children)
        {
            VisualElement section = CommonInspectorElements.CreateSection(title, name, children);
            section.AddToClassList("ts-section");
            section.EnableInClassList(TitlelessSectionClass, string.IsNullOrWhiteSpace(title));
            Label sectionTitle = section.Q<Label>(className: "common-inspector-section__title");
            sectionTitle?.AddToClassList("ts-section-title");
            return section;
        }

        public static Foldout CreateFoldoutSection(string title, string name, bool expanded, params VisualElement[] children)
        {
            Foldout section = CommonInspectorElements.CreateFoldoutSection(title, name, expanded, children);
            section.AddToClassList("ts-section");
            section.AddToClassList("ts-section-foldout");
            section.viewDataKey = "text-studio-" + MakeElementName(name ?? title);
            return section;
        }

        public static PropertyField CreatePropertyField(
            SerializedObject owner,
            string propertyPath,
            string label = null,
            string name = null,
            string tooltip = null)
        {
            string resolvedName = string.IsNullOrEmpty(name)
                ? "ts-field-" + MakeElementName(propertyPath)
                : name;
            PropertyField field = CommonInspectorElements.CreatePropertyField(
                owner,
                propertyPath,
                label,
                resolvedName,
                tooltip);
            if (field == null)
                return null;
            field.AddToClassList("ts-property-field");
            return field;
        }

        public static Label CreateMutedText(string text, string name = null)
        {
            Label label = CommonInspectorElements.CreateMutedText(text, name);
            label.AddToClassList("ts-muted-text");
            return label;
        }

        public static HelpBox CreateHelpBox(string text, HelpBoxMessageType type = HelpBoxMessageType.Info, string name = null)
        {
            HelpBox box = CommonInspectorElements.CreateHelpBox(text, type, name);
            box.AddToClassList("ts-help-box");
            return box;
        }

        public static void SetHelpBoxMessageType(HelpBox box, HelpBoxMessageType type)
        {
            CommonInspectorElements.SetHelpBoxMessageType(box, type);
        }

        public static Label CreateStatus(
            string text,
            CommonInspectorStatusType statusType,
            string name = null)
        {
            Label label = CommonInspectorElements.CreateStatus(text ?? string.Empty, statusType, name);
            label.AddToClassList("ts-status");
            return label;
        }

        public static void SetStatus(
            Label label,
            string text,
            CommonInspectorStatusType statusType)
        {
            CommonInspectorElements.SetStatus(label, text, statusType);
        }

        public static Button CreateButton(
            string text,
            string tooltip,
            Action click,
            bool primary = false,
            bool destructive = false)
        {
            Button button = CommonInspectorElements.CreateButton(text, tooltip, click, primary, destructive);
            button.AddToClassList("ts-button");
            if (primary)
                button.AddToClassList("ts-primary-button");
            if (destructive)
                button.AddToClassList("ts-destructive-button");
            return button;
        }

        public static VisualElement CreateLayerHeaderActions(
            VisualElement card,
            string name,
            int actionCount)
        {
            CommonInspectorElements.ConfigureCollectionCard(card, actionCount);
            card.AddToClassList("ts-layer-card");
            card.AddToClassList("ts-layer-card--actions-" + Math.Max(0, actionCount));
            VisualElement actions = CommonInspectorElements.CreateCollectionHeaderActions(card, name);
            actions.AddToClassList("ts-layer-card-actions");
            return actions;
        }

        internal static LayerCardShell CreateLayerCardShell(
            string name,
            string title,
            bool expanded,
            string tooltip,
            Action toggleVisibility,
            Action toggleSolo,
            Action moveUp,
            Action moveDown,
            Action delete)
        {
            VisualElement card = new VisualElement
            {
                name = name
            };
            card.AddToClassList("ts-layer-card");
            VisualElement actions = CreateLayerHeaderActions(
                card,
                name + "-actions",
                5);

            Foldout foldout = CommonInspectorElements.CreateCollectionFoldout(
                title,
                name + "-foldout",
                expanded,
                tooltip);
            foldout.AddToClassList("ts-layer-card-foldout");
            card.Insert(0, foldout);

            Image visibilityIcon = new Image
            {
                pickingMode = PickingMode.Ignore,
                scaleMode = ScaleMode.ScaleToFit
            };
            visibilityIcon.AddToClassList("ts-layer-card-header-icon");
            Button visibilityButton = CreateLayerHeaderButton(
                string.Empty,
                string.Empty,
                toggleVisibility);
            visibilityButton.name = name + "-visibility";
            visibilityButton.Add(visibilityIcon);
            actions.Add(visibilityButton);

            Button soloButton = CreateLayerHeaderButton(
                "S",
                string.Empty,
                toggleSolo);
            soloButton.name = name + "-solo";
            soloButton.AddToClassList("ts-layer-card-solo-button");
            actions.Add(soloButton);

            Button moveUpButton = CreateLayerHeaderButton(
                "↑",
                "Move this layer up.",
                moveUp);
            moveUpButton.name = name + "-up";
            actions.Add(moveUpButton);

            Button moveDownButton = CreateLayerHeaderButton(
                "↓",
                "Move this layer down.",
                moveDown);
            moveDownButton.name = name + "-down";
            actions.Add(moveDownButton);

            Button deleteButton = CreateLayerHeaderIconButton(
                "TreeEditor.Trash",
                "Del",
                "Delete this layer.",
                delete,
                true);
            deleteButton.name = name + "-delete";
            actions.Add(deleteButton);

            return new LayerCardShell(
                card,
                foldout,
                actions,
                visibilityButton,
                visibilityIcon,
                soloButton,
                moveUpButton,
                moveDownButton,
                deleteButton);
        }

        public static Button CreateLayerHeaderButton(
            string text,
            string tooltip,
            Action click,
            bool destructive = false)
        {
            Button button = CommonInspectorElements.CreateCollectionHeaderButton(text, tooltip, click, destructive);
            button.AddToClassList("ts-layer-card-header-button");
            if (destructive)
                button.AddToClassList("ts-layer-card-header-button--destructive");
            return button;
        }

        public static Button CreateLayerHeaderIconButton(
            string iconName,
            string fallbackText,
            string tooltip,
            Action click,
            bool destructive = false)
        {
            Button button = CommonInspectorElements.CreateCollectionHeaderIconButton(
                iconName,
                fallbackText,
                tooltip,
                click,
                destructive);
            button.AddToClassList("ts-layer-card-header-button");
            if (destructive)
                button.AddToClassList("ts-layer-card-header-button--destructive");
            Image image = button.Q<Image>();
            image?.AddToClassList("ts-layer-card-header-icon");
            return button;
        }

        public static VisualElement CreateLayerFooter(params VisualElement[] controls)
        {
            VisualElement footer = CommonInspectorElements.CreateCollectionFooter(controls);
            footer.AddToClassList("ts-layer-card-footer");
            return footer;
        }

        public static VisualElement CreateLayerAddArea(params VisualElement[] controls)
        {
            VisualElement addArea = CommonInspectorElements.CreateCollectionAddArea(controls);
            addArea.AddToClassList("ts-layer-stack-add-area");
            return addArea;
        }

        public static Button CreateCompactCreateButton(Action click, string name, string tooltip)
        {
            Button button = CommonInspectorElements.CreateCompactCreateButton(click, name, tooltip);
            button.AddToClassList("ts-compact-create-button");
            return button;
        }

        public static VisualElement CreateAssetReferenceRow(
            VisualElement field,
            Button createButton,
            string name)
        {
            VisualElement row = CommonInspectorElements.CreateAssetReferenceRow(field, createButton, name);
            row.AddToClassList("ts-form-row");
            row.AddToClassList("ts-asset-row");
            if (!string.IsNullOrEmpty(name))
                row.AddToClassList(name);
            if (field != null)
                field.AddToClassList("ts-form-field");
            return row;
        }

        public static VisualElement CreateAlignedControlRow(
            string label,
            VisualElement control,
            string name = null,
            params VisualElement[] trailingControls)
        {
            VisualElement input = CommonUITK.CreateContainer(
                "ts-aligned-control-row__input");
            AlignedControlField row = new AlignedControlField(
                label ?? string.Empty,
                input)
            {
                name = name
            };
            row.AddToClassList(BaseField<bool>.alignedFieldUssClassName);
            row.AddToClassList("ts-property-field");
            row.AddToClassList("ts-aligned-control-row");
            row.labelElement.AddToClassList("ts-aligned-control-row__label");
            if (control != null && !string.IsNullOrWhiteSpace(control.tooltip))
                row.labelElement.tooltip = control.tooltip;

            if (control != null)
            {
                control.AddToClassList("ts-aligned-control-row__control");
                input.Add(control);
            }

            for (int i = 0; i < trailingControls.Length; i++)
            {
                VisualElement trailing = trailingControls[i];
                if (trailing == null)
                    continue;

                trailing.AddToClassList("ts-aligned-control-row__trailing");
                input.Add(trailing);
            }

            return row;
        }

        public static VisualElement CreateAssetReferenceRow(
            SerializedObject owner,
            string propertyPath,
            string label,
            string rowName,
            string buttonName,
            string createTooltip,
            Action createAction)
        {
            SerializedProperty property = owner.FindProperty(propertyPath);
            PropertyField field = CreatePropertyField(owner, propertyPath, label);
            if (field == null)
                return null;

            Button createButton = CreateCompactCreateButton(createAction, buttonName, createTooltip);
            VisualElement row = CreateAssetReferenceRow(field, createButton, rowName);
            if (property != null)
                row.TrackPropertyValue(property, _ => SetCompactCreateButtonAvailable(createButton, property.objectReferenceValue == null));
            SetCompactCreateButtonAvailable(createButton, property == null || property.objectReferenceValue == null);
            return row;
        }

        public static void SetCompactCreateButtonAvailable(Button button, bool available)
        {
            CommonInspectorElements.SetCompactCreateButtonAvailable(button, available);
        }

        public static List<string> CreateTimingPresetChoices(out RevealTimingsPreset[] presets)
        {
            presets = (RevealTimingsPreset[])Enum.GetValues(typeof(RevealTimingsPreset));
            List<string> choices = new List<string>(presets.Length + 1);
            for (int i = 0; i < presets.Length; i++)
                choices.Add(BuiltInTimings.For(presets[i]).DisplayName);
            choices.Add(CustomTimingPresetChoice);
            return choices;
        }

        public static int GetTimingPresetChoiceIndex(
            IReadOnlyList<RevealTimingsPreset> presets,
            RevealTimingsPreset preset,
            bool hasCustomAsset)
        {
            if (hasCustomAsset)
                return presets.Count;

            for (int i = 0; i < presets.Count; i++)
            {
                if (presets[i] == preset)
                    return i;
            }

            for (int i = 0; i < presets.Count; i++)
            {
                if (presets[i] == RevealTimingsPreset.Uniform)
                    return i;
            }

            return 0;
        }

        public static string GetTimingPresetDescription(
            IReadOnlyList<RevealTimingsPreset> presets,
            int choiceIndex)
        {
            if (choiceIndex >= 0 && choiceIndex < presets.Count)
                return BuiltInTimings.For(presets[choiceIndex]).Description;
            if (choiceIndex == presets.Count)
                return "Reusable custom timing asset for project-specific character or word pacing.";
            return BuiltInTimings.Default.Description;
        }

        public static CharacterTimings CreateCharacterTimingsAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Timings",
                "Character Timings",
                "asset",
                "Choose where to save the typing timings.");
            if (string.IsNullOrEmpty(path))
                return null;

            CharacterTimings timings = ScriptableObject.CreateInstance<CharacterTimings>();
            AssetDatabase.CreateAsset(timings, path);
            AssetDatabase.SaveAssets();
            return timings;
        }

        public static VisualElement CreateButtonRow(params VisualElement[] controls)
        {
            VisualElement row = CommonInspectorElements.CreateButtonRow(controls);
            row.AddToClassList("ts-button-row");
            return row;
        }

        public static IVisualElementScheduledItem SchedulePeriodicRefresh(
            VisualElement inspectorRoot,
            Action refresh,
            long intervalMs)
        {
            return CommonInspectorElements.SchedulePeriodicRefresh(inspectorRoot, refresh, intervalMs);
        }

        public static IVisualElementScheduledItem SchedulePeriodicRefresh(
            VisualElement inspectorRoot,
            Action refresh,
            Action interactionRefresh,
            long intervalMs)
        {
            return CommonInspectorElements.SchedulePeriodicRefresh(
                inspectorRoot,
                refresh,
                interactionRefresh,
                intervalMs);
        }

        public static VisualElement CreateKeyValueRow(string label, string value, string name = null)
        {
            VisualElement row = CommonUITK.CreateKeyValueRow(
                label,
                value,
                "ts-key-value-row",
                "ts-key-value-label",
                "ts-key-value-value");
            row.name = name;
            return row;
        }

        public static VisualElement CreateDefaultProperties(SerializedObject owner, params string[] excludedPropertyPaths)
        {
            VisualElement container = new VisualElement();
            container.AddToClassList("ts-property-group");

            HashSet<string> excluded = new HashSet<string>(excludedPropertyPaths ?? System.Array.Empty<string>());
            SerializedProperty iterator = owner.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script" || excluded.Contains(iterator.propertyPath))
                    continue;

                PropertyField field = CreatePropertyField(owner, iterator.propertyPath);
                if (field != null)
                    container.Add(field);
            }

            return container;
        }

        static string MakeElementName(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath))
                return "property";

            char[] chars = propertyPath.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                    continue;

                chars[i] = '-';
            }

            return new string(chars);
        }
    }
}
