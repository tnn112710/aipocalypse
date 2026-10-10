using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    internal partial class TextStudioTextEditor
    {
        TextStudio2DStyleEditor _inlineAppearanceEditor;

        bool HasMixedAppearanceSources(TextStudioText hub)
        {
            foreach (Object owner in targets)
                if (owner is TextStudioText text && TextStudio2DStyleSelection.Choice(text) != TextStudio2DStyleSelection.Choice(hub))
                    return true;
            return false;
        }

        DropdownField BuildAppearanceSourceField(TextStudioText hub)
        {
            string renderedSource = TextStudio2DStyleSelection.Choice(hub);
            bool renderedMixed = HasMixedAppearanceSources(hub);
            DropdownField field = new DropdownField("Source", new List<string> { "Inline", "Catalog", "Custom" }, 0)
            {
                name = "ts-appearance-source",
                showMixedValue = renderedMixed,
                tooltip = "Inline stores local layers; an empty stack preserves native text. Catalog browses built-in and registered styles. Custom assigns an asset directly. Each source retains its selection."
            };
            field.AddToClassList(UnityAlignedFieldClass);
            field.SetValueWithoutNotify(renderedSource);
            field.RegisterValueChangedCallback(evt =>
            {
                ApplyAppearanceChange("Change Appearance Source", text => TextStudio2DStyleSelection.Select(text, evt.newValue));
                RequestUitkActiveTabRebuild(hub);
            });
            Action refresh = () =>
            {
                bool mixed = HasMixedAppearanceSources(hub);
                field.showMixedValue = mixed;
                string source = TextStudio2DStyleSelection.Choice(hub);
                field.SetValueWithoutNotify(source);
                if (source != renderedSource || mixed != renderedMixed)
                    RequestUitkActiveTabRebuild(hub);
            };
            field.TrackPropertyValue(serializedObject.FindProperty("_appearanceSource"), _ => refresh());
            field.TrackPropertyValue(_appearanceStyle, _ => refresh());
            field.TrackPropertyValue(serializedObject.FindProperty(TextStudioStyleSelection.ModeProperty), _ => refresh());
            return field;
        }

        Button CreateAppearanceBrowserControl()
        {
            return TextStudioStyleSelection.CreateBrowseButton(
                () =>
                {
                    serializedObject.ApplyModifiedProperties();
                    TextStudioStyleBrowserWindow.Show2D((Object[])targets.Clone());
                }, "ts-appearance-browse-styles");
        }

        void AddCatalogAppearanceControls(VisualElement section)
        {
            VisualElement field = TextStudioStyleSelection.CreateCatalogField("ts-appearance-catalog-style", out Label styleLabel, CreateAppearanceBrowserControl());
            Label unavailable = CreateAlignedHintLabel(string.Empty, "ts-appearance-catalog-unavailable");
            section.Add(field);
            section.Add(CreateAlignedHintRow(unavailable));
            Action refresh = () =>
            {
                TextStudio2DStyleAsset asset = _appearanceStyle.objectReferenceValue as TextStudio2DStyleAsset;
                bool mixed = _appearanceStyle.hasMultipleDifferentValues;
                styleLabel.text = mixed ? "Mixed" : asset != null ? asset.name : "None selected";
                styleLabel.tooltip = styleLabel.text;
                bool missing = !mixed && asset != null && !TextStudio2DStyleSelection.IsCatalogAsset(asset);
                unavailable.text = missing ? "This saved style is no longer in the catalog. Its live reference is retained." : string.Empty;
                unavailable.parent.style.display = missing ? DisplayStyle.Flex : DisplayStyle.None;
            };
            field.TrackPropertyValue(_appearanceStyle, _ => refresh());
            TextStudioInspectorElements.SchedulePeriodicRefresh(field, () =>
            {
                if (field.panel != null && target != null) refresh();
            }, 500);
            refresh();
        }

        void AddStyleAppearanceControls(VisualElement section)
        {
            ObjectField field = new ObjectField("Style Asset")
            {
                name = BuildPropertyFieldName(_appearanceStyle),
                objectType = typeof(TextStudio2DStyleAsset),
                allowSceneObjects = false,
                tooltip = "A live reusable appearance. Editing this asset updates every object using it."
            };
            field.AddToClassList("ts-property-field");
            field.AddToClassList(UnityAlignedFieldClass);
            field.RegisterValueChangedCallback(evt => ApplyAppearanceChange("Assign Appearance Style", text =>
                TextStudio2DStyleSelection.Assign(text, evt.newValue as TextStudio2DStyleAsset, TextStudioStyleSelectionMode.Custom)));
            Button create = TextStudioInspectorElements.CreateCompactCreateButton(
                CreateAppearanceStyleAsset, "ts-appearance-style-create-button",
                "Create and assign a reusable style from the retained inline appearance, or an empty appearance.");
            if (serializedObject.isEditingMultipleObjects)
                create.tooltip = "Select one object to create a reusable style from its inline appearance.";
            VisualElement row = TextStudioInspectorElements.CreateAssetReferenceRow(
                field, create, "ts-appearance-style-row");
            section.Add(row);
            Action refresh = () =>
            {
                field.showMixedValue = _appearanceStyle.hasMultipleDifferentValues;
                field.SetValueWithoutNotify(_appearanceStyle.objectReferenceValue);
                bool available = _appearanceStyle.objectReferenceValue == null && !_appearanceStyle.hasMultipleDifferentValues;
                TextStudioInspectorElements.SetCompactCreateButtonAvailable(create, available);
                create.SetEnabled(available && !serializedObject.isEditingMultipleObjects);
            };
            row.TrackPropertyValue(_appearanceStyle, _ => refresh());
            refresh();
        }

        Action AddAppearanceConversionControls(VisualElement section, TextStudioText hub)
        {
            Button duplicate = TextStudioStyleSelection.CreateDuplicateButton(DuplicateAppearanceStyle, "ts-appearance-duplicate-style");
            Button convert = TextStudioInspectorElements.CreateButton(
                "Make Inline",
                "Copy the complete appearance, including inherited layers, into each component. Future preset and base-style edits no longer affect it; texture references stay shared.",
                () =>
                {
                    serializedObject.ApplyModifiedProperties();
                    if (!TextStudioAppearancePresetConversion.ConvertToInline(targets, out string error))
                    {
                        Debug.LogWarning(error, hub);
                        return;
                    }
                    serializedObject.Update();
                    RequestUitkActiveTabRebuild(hub);
                    Repaint();
                });
            convert.name = "ts-appearance-convert-inline-button";
            Label unavailable = CreateAlignedHintLabel(string.Empty, "ts-appearance-conversion-unavailable");
            section.Add(TextStudioInspectorElements.CreateButtonRow(duplicate, convert));
            section.Add(CreateAlignedHintRow(unavailable));
            Action refresh = () =>
            {
                string error = TextStudioAppearancePresetConversion.GetUnavailableReason(targets);
                convert.SetEnabled(string.IsNullOrEmpty(error));
                duplicate.SetEnabled(targets.Length == 1 && hub.AppearanceSource == TextStudioAppearanceSource.Style && hub.AppearanceStyle != null);
                unavailable.text = error;
                unavailable.parent.style.display = string.IsNullOrEmpty(error) ? DisplayStyle.None : DisplayStyle.Flex;
            };
            refresh();
            return refresh;
        }

        void DuplicateAppearanceStyle()
        {
            if (targets.Length != 1 || target is not TextStudioText text || text.AppearanceStyle == null) return;
            string path = EditorUtility.SaveFilePanelInProject("Duplicate 2D Style", text.AppearanceStyle.name + " Copy", "asset",
                "Choose where to save the independent style copy.");
            DuplicateAppearanceStyleAsset(path);
        }

        internal void DuplicateAppearanceStyleAsset(string path)
        {
            if (string.IsNullOrEmpty(path) || targets.Length != 1 || target is not TextStudioText text || text.AppearanceStyle == null) return;
            if (string.Equals(path, AssetDatabase.GetAssetPath(text.AppearanceStyle), StringComparison.OrdinalIgnoreCase)) return;
            serializedObject.ApplyModifiedProperties();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Duplicate 2D Style");
            TextStudio2DStyleAsset duplicate = Object.Instantiate(text.AppearanceStyle);
            duplicate.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(duplicate, path);
            Undo.RegisterCreatedObjectUndo(duplicate, "Duplicate 2D Style");
            AssetDatabase.SaveAssetIfDirty(duplicate);
            ApplyAppearanceChange("Assign Duplicated Style", owner => TextStudio2DStyleSelection.Assign(owner, duplicate, TextStudioStyleSelectionMode.Custom));
            Undo.CollapseUndoOperations(group);
            RequestUitkActiveTabRebuild(text);
            EditorGUIUtility.PingObject(duplicate);
        }

        void ApplyAppearanceChange(string undoName, Action<TextStudioText> change)
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObjects(targets, undoName);
            foreach (Object owner in targets)
            {
                if (owner is not TextStudioText text)
                    continue;
                change(text);
                TextStudioStyleCatalogProvider.MarkChanged(text);
            }
            serializedObject.Update();
            Repaint();
        }
    }
}
