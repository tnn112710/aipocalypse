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
    internal partial class TextStudioComposedEffectAssetEditor
    {
        VisualElement CreateHeader(VisualElement inspectorRoot)
        {
            VisualElement section = CreateSection("Composed Effect");
            section.name = "ts-composed-header";
            PropertyField idField = CreateBoundField(_id, "Tag ID", "ts-composed-tag-id");
            section.Add(idField);

            VisualElement usageRow = new VisualElement
            {
                name = "ts-composed-usage-row"
            };
            usageRow.AddToClassList("ts-composed-usage-row");
            Label usage = new Label
            {
                name = "ts-composed-usage"
            };
            usage.AddToClassList("ts-tag-inline-help-example");
            usageRow.Add(usage);
            Button copyUsage = new Button(() =>
            {
                EditorGUIUtility.systemCopyBuffer = usage.text ?? string.Empty;
            })
            {
                name = "ts-composed-copy-usage",
                text = "Copy",
                tooltip = "Copy this Composed Effect's complete opening and closing markup."
            };
            copyUsage.AddToClassList("ts-composed-copy-usage");
            usageRow.Add(copyUsage);
            section.Add(usageRow);

            Action refreshUsage = () =>
            {
                string id = _id != null ? _id.stringValue.Trim() : string.Empty;
                string safeId = string.IsNullOrEmpty(id) ? "composed" : id;
                usage.text = "<" + safeId + ">text</" + safeId + ">";
            };
            idField.TrackPropertyValue(_id, _ => refreshUsage());
            refreshUsage();

            Label typewriterUsage = TextStudioInspectorElements.CreateMutedText(
                string.Empty,
                "ts-composed-typewriter-usage");
            typewriterUsage.tooltip =
                "A Typewriter treatment is phase-clamped by the Text Studio component. Active effects on the text continue after each glyph arrives.";
            typewriterUsage.text =
                TextStudioComposedLayerPresentation.BuildTypewriterUsage(
                    target as TextStudioComposedEffectAsset);
            section.Add(typewriterUsage);

            VisualElement catalog = new VisualElement();
            catalog.AddToClassList("ts-effect-catalog-status");
            Label catalogStatus = TextStudioInspectorElements.CreateStatus(
                string.Empty,
                CommonInspectorStatusType.Info);
            catalogStatus.AddToClassList("ts-effect-catalog-message");
            catalog.Add(catalogStatus);
            VisualElement buttons = new VisualElement();
            buttons.AddToClassList("ts-button-row");
            catalog.Add(buttons);
            Button catalogButton = new Button(() =>
            {
                TextStudioComposedEffectAsset effect = target as TextStudioComposedEffectAsset;
                if (effect == null)
                    return;
                if (TextStudioEffectCatalogUtility.Contains(effect))
                    TextStudioEffectCatalogUtility.Remove(effect);
                else
                    TextStudioEffectCatalogUtility.Add(effect);
            });
            catalogButton.name = "ts-composed-catalog-button";
            buttons.Add(catalogButton);
            Button openCatalog = new Button(() =>
            {
                TextStudioProjectSettings settings = TextStudioEffectCatalogUtility.FindSettings();
                if (settings == null)
                    settings = TextStudioEffectCatalogUtility.CreateSettingsNear(target as TextStudioEffectDefinitionAsset);
                if (settings != null)
                    Selection.activeObject = settings;
            })
            {
                text = "Open Catalog"
            };
            buttons.Add(openCatalog);
            section.Add(catalog);

            TextStudioInspectorElements.SchedulePeriodicRefresh(inspectorRoot, () =>
            {
                if (target == null || section.panel == null)
                    return;
                serializedObject.UpdateIfRequiredOrScript();
                TextStudioComposedEffectAsset effect = target as TextStudioComposedEffectAsset;
                typewriterUsage.text =
                    TextStudioComposedLayerPresentation.BuildTypewriterUsage(effect);
                bool inCatalog = TextStudioEffectCatalogUtility.Contains(effect);
                string validationError = string.Empty;
                bool valid = effect != null && effect.TryCompileForValidation(out validationError);
                string catalogMessage = !valid
                    ? "Resolve the composition issue before adding it to the catalog: " + validationError
                    : inCatalog
                        ? "Ready. This Composed Effect is in the effect catalog."
                        : "Add this Composed Effect to the catalog before using its tag in text.";
                CommonInspectorStatusType catalogStatusType = !valid
                    ? CommonInspectorStatusType.Warning
                    : inCatalog
                        ? CommonInspectorStatusType.Success
                        : CommonInspectorStatusType.Info;
                TextStudioInspectorElements.SetStatus(
                    catalogStatus,
                    catalogMessage,
                    catalogStatusType);
                catalogButton.text = inCatalog ? "Remove" : "Add to Catalog";
                catalogButton.SetEnabled(inCatalog || valid);
                openCatalog.text = TextStudioEffectCatalogUtility.FindSettings() == null
                    ? "Create Catalog"
                    : "Open Catalog";
            }, 250);
            return section;
        }

        VisualElement CreateEffectSettings()
        {
            VisualElement section = CreateSection("Effect Settings");
            section.name = "ts-composed-effect-settings";
            PopupField<string> authoringSpace = new PopupField<string>(
                "Authoring Space",
                new List<string> { "2D", "3D" },
                Mathf.Clamp(_authoringSpace.enumValueIndex, 0, 1))
            {
                name = "ts-composed-authoring-space",
                tooltip = "2D is the portable default and usually works on flat and extruded text. 3D enables spatial and extrusion-specific authoring and requires the 3D add-on."
            };
            authoringSpace.AddToClassList("ts-property-field");
            authoringSpace.RegisterValueChangedCallback(evt =>
            {
                int next = string.Equals(evt.newValue, "3D", StringComparison.Ordinal) ? 1 : 0;
                serializedObject.UpdateIfRequiredOrScript();
                if (_authoringSpace.enumValueIndex == next)
                    return;
                Undo.RecordObject(target, "Change Composed Effect Authoring Space");
                _authoringSpace.enumValueIndex = next;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                DisposeLivePreview();
                RebuildLayerRows();
                UpdateValidation();
                RenderPreview();
            });
            section.Add(authoringSpace);
            Label dependency = TextStudioInspectorElements.CreateStatus(
                "The 3D add-on is not installed. This asset's 3D setup is preserved, but 3D authoring and preview are unavailable.",
                CommonInspectorStatusType.Warning,
                "ts-composed-3d-dependency");
            section.Add(dependency);
            Action refreshAuthoringSpace = () =>
            {
                if (target == null)
                    return;
                serializedObject.UpdateIfRequiredOrScript();
                bool hasThreeD = TextStudioAuthoringCapabilities.Supports(
                    TextStudioRenderDomain.Extruded3D);
                bool isThreeD = _authoringSpace.enumValueIndex ==
                    (int)TextStudioCompositionAuthoringSpace.ThreeD;
                authoringSpace.SetValueWithoutNotify(isThreeD ? "3D" : "2D");
                authoringSpace.choices = hasThreeD
                    ? new List<string> { "2D", "3D" }
                    : new List<string> { isThreeD ? "3D" : "2D" };
                authoringSpace.style.display = hasThreeD
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                dependency.style.display = isThreeD && !hasThreeD
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            };
            section.TrackPropertyValue(_authoringSpace, _ => refreshAuthoringSpace());
            refreshAuthoringSpace();
            Slider revealBlend = new Slider("Reveal Blend", 0f, 5f)
            {
                name = "ts-composed-reveal-active-blend",
                bindingPath = _revealActiveBlendDuration.propertyPath,
                tooltip = "Stored on this Composed Effect and used wherever it runs. Crossfades from Reveal to Active during the final part of each glyph's Reveal motion. The value is in seconds; zero disables blending, and the effective window is capped by the Reveal motion duration."
            };
            revealBlend.AddToClassList("ts-property-field");
            section.Add(revealBlend);
            return section;
        }

        VisualElement CreateTagStack()
        {
            bool defaultExpanded = target is TextStudioComposedEffectAsset effect &&
                !string.IsNullOrWhiteSpace(effect.TagStack.OpeningMarkup);
            bool expanded = GetFoldoutState("tag-stack", defaultExpanded);
            VisualElement section = CreateFoldoutSection(
                "Tag Stack",
                "ts-composed-tag-stack",
                expanded,
                out Foldout content);
            _tagStackFoldout = content;
            RememberFoldoutState(content, "tag-stack");
            Label help = new Label("Optional: enter effect tags directly.");
            help.AddToClassList("ts-muted-text");
            content.Add(help);

            SerializedProperty enabled = _tagStack.FindPropertyRelative("_enabled");
            SerializedProperty opening = _tagStack.FindPropertyRelative("_openingMarkup");
            SerializedProperty closingMode = _tagStack.FindPropertyRelative("_closingMode");
            SerializedProperty customClosing = _tagStack.FindPropertyRelative("_customClosingMarkup");
            content.Add(CreateBoundField(enabled, "Enabled", "ts-composed-tag-stack-enabled"));
            VisualElement openingEditor = CreateAssistedTagField(
                opening,
                "Opening Tags",
                "ts-composed-opening-tags");
            content.Add(openingEditor);
            content.Add(CreateBoundField(closingMode, "Closing", "ts-composed-closing-mode"));
            VisualElement customClosingEditor = CreateAssistedTagField(
                customClosing,
                "Closing Tags",
                "ts-composed-custom-closing-tags");
            content.Add(customClosingEditor);

            Action refreshClosing = () =>
            {
                if (target == null)
                    return;
                serializedObject.UpdateIfRequiredOrScript();
                bool custom = closingMode.enumValueIndex == (int)TextStudioTagExpansionClosingMode.Custom;
                customClosingEditor.style.display = custom ? DisplayStyle.Flex : DisplayStyle.None;
                content.text = enabled.boolValue ? "Tag Stack" : "Tag Stack (Off)";
            };
            content.TrackPropertyValue(enabled, _ => refreshClosing());
            content.TrackPropertyValue(closingMode, _ => refreshClosing());
            refreshClosing();
            return section;
        }

        VisualElement CreateValidation()
        {
            _validationSection = CreateSection("Needs Attention");
            _validationSection.name = "ts-composed-validation";
            _validationLabel = TextStudioInspectorElements.CreateStatus(
                string.Empty,
                CommonInspectorStatusType.Warning,
                "ts-composed-validation-summary");
            _validationSection.Add(_validationLabel);
            _validationSection.style.display = DisplayStyle.None;
            return _validationSection;
        }

        void UpdateValidation()
        {
            if (_validationLabel == null || target is not TextStudioComposedEffectAsset effect)
                return;
            if (string.IsNullOrWhiteSpace(effect.Id))
            {
                SetValidation("Add a Tag ID before using this composition.", true);
                return;
            }
            if (!effect.TryCompileForValidation(out string error))
            {
                SetValidation(error, true);
                return;
            }

            if (TextStudioComposedLayerPresentation.TryGetWritingCoverageWarning(
                    effect,
                    out string writingWarning))
            {
                SetValidation(writingWarning, true);
                return;
            }

            SetValidation(string.Empty, false);
        }

        void SetValidation(string message, bool error)
        {
            TextStudioInspectorElements.SetStatus(
                _validationLabel,
                message,
                CommonInspectorStatusType.Warning);
            if (_validationSection != null)
                _validationSection.style.display = error ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
