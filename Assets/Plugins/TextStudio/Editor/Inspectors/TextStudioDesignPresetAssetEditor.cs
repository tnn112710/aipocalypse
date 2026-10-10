using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    [CustomEditor(typeof(TextStudioDesignPresetAsset)), CanEditMultipleObjects]
    internal sealed class TextStudioDesignPresetAssetEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot(
                "ts-design-preset-inspector");
            root.Add(TextStudioInspectorElements.CreateHelpBox(
                "A complete Design is an apply-once recipe over established Appearance, Writing, and Behavior Profile assets. It does not add a hidden styling owner, motion stack, or tag source."));
            root.Add(TextStudioInspectorElements.CreateSection(
                "Presentation",
                "ts-design-preset-presentation",
                Field("_displayName", "Display Name"),
                Field("_description", "Description"),
                Field(
                    "_sampleText",
                    "Gallery Sample",
                    "Sample copy used by galleries and preview generation. Applying the Design never replaces a text object's source.")));
            root.Add(TextStudioInspectorElements.CreateSection(
                "Established Building Blocks",
                "ts-design-preset-building-blocks",
                Field(
                    "_appearanceStyle",
                    "Appearance",
                    "Optional reusable whole-object visual finish."),
                Field(
                    "_behaviorProfile",
                    "Behavior Profile",
                    "Optional existing Typewriter and Default Effects ownership.")));

            VisualElement actions = TextStudioInspectorElements.CreateSection(
                "Apply",
                "ts-design-preset-actions");
            Label selection = TextStudioInspectorElements.CreateMutedText(
                string.Empty,
                "ts-design-preset-selection");
            actions.Add(selection);
            HelpBox backendSupport = TextStudioInspectorElements.CreateHelpBox(
                string.Empty,
                HelpBoxMessageType.Warning,
                "ts-design-preset-appearance-backend-support");
            backendSupport.style.display = DisplayStyle.None;
            actions.Add(backendSupport);

            VisualElement actionRow = new VisualElement
            {
                name = "ts-design-preset-action-row"
            };
            actionRow.style.flexDirection = FlexDirection.Row;
            Button apply = TextStudioInspectorElements.CreateButton(
                "Apply to Selection",
                "Assign this recipe to every selected TextStudioText without changing source, tags, Tag Stack, placement, or renderer selection.",
                ApplyToSelection,
                primary: true);
            apply.name = "ts-design-preset-apply";
            apply.style.flexGrow = 1f;
            actionRow.Add(apply);

            Button browse = TextStudioInspectorElements.CreateButton(
                "Open Gallery",
                "Browse all production-rendered visual designs and apply them in context.",
                TextGalleryWindow.ShowWindow);
            browse.name = "ts-design-preset-gallery";
            browse.style.marginLeft = 4f;
            actionRow.Add(browse);
            actions.Add(actionRow);
            root.Add(actions);

            System.Action refresh = () =>
            {
                TextStudioAppearanceBackendSummary backendSummary =
                    TextStudioAppearanceBackendSupport.EvaluateSelection();
                int selectedCount = backendSummary.TargetCount;
                bool singleRecipe = targets != null && targets.Length == 1;
                apply.SetEnabled(singleRecipe && selectedCount > 0);
                selection.text = !singleRecipe
                    ? "Select one Design recipe to apply it."
                    : selectedCount == 0
                        ? "Select one or more objects with TextStudioText to apply this recipe."
                        : "Ready to update " + selectedCount +
                          (selectedCount == 1
                              ? " selected Text Studio object."
                              : " selected Text Studio objects.");

                bool hasAppearance = singleRecipe &&
                    target is TextStudioDesignPresetAsset preset &&
                    preset.AppearanceStyle != null;
                bool appearanceInactive = hasAppearance &&
                    backendSummary.HasUnsupportedTargets;
                backendSupport.style.display = appearanceInactive
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                if (appearanceInactive)
                {
                    string affected = backendSummary.TargetCount > 1
                        ? backendSummary.UnsupportedCount + " of " +
                          backendSummary.TargetCount + " selected objects"
                        : "The selected object";
                    backendSupport.text = affected +
                        " will keep this Design's Appearance assigned but will not render it while the active backend owns glyph styling. Writing and Behavior Profile assignments still apply.";
                }
                apply.tooltip = appearanceInactive
                    ? "Apply the complete recipe. The Appearance stays assigned but inactive on affected backends; Writing and Behavior Profile assignments still apply."
                    : "Assign this recipe to every selected TextStudioText without changing source, tags, Tag Stack, placement, or renderer selection.";
            };
            refresh();
            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                Selection.selectionChanged -= refresh;
                Selection.selectionChanged += refresh;
                refresh();
            });
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
                Selection.selectionChanged -= refresh);
            return root;
        }

        PropertyField Field(
            string path,
            string label,
            string tooltip = null)
        {
            return TextStudioInspectorElements.CreatePropertyField(
                serializedObject,
                path,
                label,
                null,
                tooltip);
        }

        void ApplyToSelection()
        {
            if (targets == null || targets.Length != 1 ||
                target is not TextStudioDesignPresetAsset preset)
            {
                return;
            }

            List<TextStudioText> selected = CollectSelectedTargets();
            if (selected.Count == 0)
                return;

            Object[] undoTargets = new Object[selected.Count];
            for (int i = 0; i < selected.Count; i++)
                undoTargets[i] = selected[i];
            Undo.RecordObjects(undoTargets, "Apply Text Studio Design");
            for (int i = 0; i < selected.Count; i++)
            {
                TextStudioText text = selected[i];
                preset.ApplyTo(text);
                EditorUtility.SetDirty(text);
                PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                if (!Application.isPlaying && text.gameObject.scene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                        text.gameObject.scene);
                }
            }
        }

        static List<TextStudioText> CollectSelectedTargets()
        {
            GameObject[] selection = Selection.gameObjects;
            List<TextStudioText> result =
                new List<TextStudioText>(selection.Length);
            for (int i = 0; i < selection.Length; i++)
            {
                TextStudioText text = selection[i] != null
                    ? selection[i].GetComponent<TextStudioText>()
                    : null;
                if (text != null && !result.Contains(text))
                    result.Add(text);
            }
            return result;
        }
    }
}
