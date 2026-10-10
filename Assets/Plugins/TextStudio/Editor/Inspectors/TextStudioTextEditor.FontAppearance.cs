using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal partial class TextStudioTextEditor
    {
        VisualElement BuildFontAppearanceSection(TextStudioText hub)
        {
            VisualElement section = CreateSection("Runtime Preparation");
            section.name = "ts-runtime-preparation";
            section.Add(CreateAlignedHint("This appearance uses prepared font and image data in Play Mode and builds. Text Studio prepares it automatically; the overview shows its status."));
            section.Add(TextStudioInspectorElements.CreateButtonRow(TextStudioInspectorElements.CreateButton("Open Runtime Preparation",
                "See what is prepared in your project and prepare the rest, with this text highlighted.",
                () => TextStudioRuntimePreparationWindow.Open(FindAppearancePreparationTarget() ?? hub))));
            return section;
        }

        TextStudioText FindAppearancePreparationTarget()
        {
            foreach (UnityEngine.Object owner in targets)
                if (owner is TextStudioText text &&
                    (TextStudioAppearancePreparationProvider.NeedsSources(text) || TextStudioImagePreparationProvider.HasApplicableSources(text)))
                    return text;
            return null;
        }

        internal VisualElement BuildFontAppearanceAdvanced(TextStudioText hub)
        {
            VisualElement section = new VisualElement();
            section.name = "ts-font-appearance-data";
            section.Add(CreateAlignedHint("Prepare reusable source contours and sharper glyph fields for this text. Preparation creates separate assets and preserves the font's atlas."));
            PropertyField bindings = new PropertyField(serializedObject.FindProperty("_appearanceFonts"), "Prepared Fonts")
            { tooltip = "Shared appearance data for primary and fallback fonts. Each asset keeps its own glyph coverage and quality tiers." };
            bindings.RegisterCallback<SerializedPropertyChangeEvent>(_ => hub.ScheduleTopologyRebuild());
            section.Add(bindings);
            Foldout colorSources = new Foldout { text = "Inline Image Silhouettes", value = false };
            PropertyField alphaThreshold = new PropertyField(serializedObject.FindProperty("_inlineColorAlphaThreshold"), "Alpha Threshold")
            { tooltip = "The silhouette normally follows alpha 0.5. Lower the threshold for translucent art. The original image keeps its own RGB and fractional opacity." };
            alphaThreshold.RegisterCallback<SerializedPropertyChangeEvent>(_ => hub.ScheduleTopologyRebuild());
            colorSources.Add(alphaThreshold);
            PropertyField images = new PropertyField(serializedObject.FindProperty("_appearanceImages"), "Prepared Images")
            { tooltip = "Shared static image silhouettes included through serialized asset references. Matching images use them immediately, without runtime readback." };
            images.RegisterCallback<SerializedPropertyChangeEvent>(_ => hub.ScheduleTopologyRebuild());
            colorSources.Add(images);
            PopupField<int> imageDensity = new PopupField<int>("Pixels Per Em", new List<int> { 64, 128, 256, 512, 1024 }, 2)
            { tooltip = "Prepared silhouette detail. Each source is capped at 1024 pixels per axis. Larger values increase saved texture memory." };
            imageDensity.AddToClassList(BaseField<int>.alignedFieldUssClassName);
            FloatField imagePadding = new FloatField("Padding (em)") { value = .5f, tooltip = "Distance outside the image available to strokes, shadows, and glows. Larger runtime requests can prepare an expanded replacement." };
            imagePadding.AddToClassList(BaseField<float>.alignedFieldUssClassName);
            Button prepareImages = new Button(() => TextStudioColorAppearancePreparation.StartForText(hub, imageDensity.value, imagePadding.value))
            { text = "Prepare Image Silhouettes", tooltip = "Prepare the current visible images into shared data, preserving previous coverage when updating an existing asset." };
            Button cancelImages = new Button(TextStudioColorAppearancePreparation.Cancel) { text = "Cancel Image Preparation" };
            ProgressBar imageProgress = new ProgressBar { title = "Preparing Images", lowValue = 0, highValue = 100 };
            HelpBox imageStatus = new HelpBox(string.Empty, HelpBoxMessageType.Error);
            colorSources.Add(imageDensity); colorSources.Add(imagePadding); colorSources.Add(prepareImages);
            colorSources.Add(imageProgress); colorSources.Add(cancelImages); colorSources.Add(imageStatus);
            void RefreshImages()
            {
                bool busy = TextStudioColorAppearancePreparation.IsBusy;
                imageDensity.SetEnabled(!busy); imagePadding.SetEnabled(!busy);
                prepareImages.SetEnabled(!busy && !serializedObject.isEditingMultipleObjects && hub.ColorSources.Length > 0 && imagePadding.value > 0 && imagePadding.value <= 16);
                imageProgress.style.display = busy ? DisplayStyle.Flex : DisplayStyle.None;
                imageProgress.value = TextStudioColorAppearancePreparation.Completion * 100;
                cancelImages.style.display = busy ? DisplayStyle.Flex : DisplayStyle.None;
                imageStatus.text = TextStudioColorAppearancePreparation.LastError;
                imageStatus.style.display = imageStatus.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            RefreshImages();
            TextStudioInspectorElements.SchedulePeriodicRefresh(colorSources, () => { if (CanRunScheduledInspectorUpdate(section, hub)) RefreshImages(); }, 250);
            section.Add(colorSources);
            TMP_Text text = hub.TMPText;
            PopupField<string> scope = new PopupField<string>("Prepare", new List<string> { "Fonts Used by Text", "Selected Font" }, 0)
            { tooltip = "Prepare the actual primary and fallback fonts in the current text, or choose one font explicitly." };
            scope.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            section.Add(scope);
            ObjectField font = new ObjectField("Font") { objectType = typeof(TMP_FontAsset), allowSceneObjects = false, value = text != null ? text.font : null };
            font.tooltip = "The primary or fallback font to prepare when Selected Font is chosen.";
            font.AddToClassList(BaseField<UnityEngine.Object>.alignedFieldUssClassName);
            section.Add(font);
            Toggle inventory = new Toggle("Include Font Inventory")
            { tooltip = "Include every glyph currently stored in each font asset, in addition to the current shaped text. Existing prepared coverage from the same source is always preserved." };
            inventory.AddToClassList(BaseField<bool>.alignedFieldUssClassName);
            section.Add(inventory);
            FloatField density = new FloatField("Pixels per Em") { value = 256,
                tooltip = "Detail in the sharpest prepared tier. Larger text needs more detail. Use 16 to 1,024; more detail increases preparation time and memory." };
            FloatField reach = new FloatField("Distance Reach (em)") { value = .3f,
                tooltip = "Distance available on either side of the glyph edge for outlines, shadows and glow. Use 0.01 to 2 em. Wider reach increases memory and reduces distance precision." };
            density.AddToClassList(BaseField<float>.alignedFieldUssClassName);
            reach.AddToClassList(BaseField<float>.alignedFieldUssClassName);
            section.Add(density); section.Add(reach);
            HelpBox status = new HelpBox(string.Empty, HelpBoxMessageType.Error) { name = "ts-font-preparation-status" };
            Label summary = new Label();
            summary.style.whiteSpace = WhiteSpace.Normal;
            summary.style.minWidth = 0;
            ProgressBar progress = new ProgressBar { title = "Preparing Fonts", lowValue = 0, highValue = 100 };
            Button prepare = new Button(() =>
            {
                TextStudioFontAppearancePreparation.StartForText(hub, inventory.value, scope.index == 1 ? font.value as TMP_FontAsset : null,
                    pixelsPerEm: density.value, safeDistanceEm: reach.value);
            }) { text = "Prepare Appearance Data", tooltip = "Prepare shared appearance data in the background, one font at a time. Completed fonts are assigned to this text." };
            Button cancel = new Button(TextStudioFontAppearancePreparation.Cancel)
            { text = "Cancel Preparation", tooltip = "Stop the remaining preparation. Fonts already completed remain available." };
            section.Add(prepare); section.Add(progress); section.Add(cancel); section.Add(summary); section.Add(status);
            void Refresh()
            {
                bool busy = TextStudioFontAppearancePreparation.IsBusy;
                density.SetEnabled(!busy); reach.SetEnabled(!busy);
                bool selected = scope.index == 1;
                scope.SetEnabled(!busy);
                font.style.display = selected ? DisplayStyle.Flex : DisplayStyle.None;
                font.SetEnabled(!busy);
                inventory.SetEnabled(!busy);
                prepare.SetEnabled(float.IsFinite(density.value) && density.value >= 16 && density.value <= 1024 &&
                    float.IsFinite(reach.value) && reach.value >= .01f && reach.value <= 2 && !busy && text != null && (!selected || font.value != null) && !serializedObject.isEditingMultipleObjects && TextStudioRuntimePreparation.FontEmbeddingAllowed);
                progress.style.display = busy ? DisplayStyle.Flex : DisplayStyle.None;
                cancel.style.display = busy ? DisplayStyle.Flex : DisplayStyle.None;
                progress.value = TextStudioFontAppearancePreparation.Completion * 100f;
                progress.title = (TextStudioFontAppearancePreparation.CompletedFontCount + 1) + " / " + TextStudioFontAppearancePreparation.RequestedFontCount + " Fonts";
                string error = !TextStudioRuntimePreparation.FontEmbeddingAllowed ? "Choose whether this project may embed font sources in Runtime Preparation before generating new font data." : TextStudioFontAppearancePreparation.LastError;
                status.text = error;
                status.style.display = !string.IsNullOrEmpty(error) ? DisplayStyle.Flex : DisplayStyle.None;
                summary.text = busy
                    ? TextStudioFontAppearancePreparation.CurrentSourceName + (TextStudioFontAppearancePreparation.EstimatedTextureBytes > 0
                        ? " · " + EditorUtility.FormatBytes(TextStudioFontAppearancePreparation.EstimatedTextureBytes) : " · Reading font curves")
                    : TextStudioFontAppearancePreparation.CompletedFontCount > 0
                        ? "Prepared " + TextStudioFontAppearancePreparation.CompletedFontCount + " of " + TextStudioFontAppearancePreparation.RequestedFontCount + " fonts."
                        : string.Empty;
                summary.style.display = summary.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            scope.RegisterValueChangedCallback(_ => Refresh());
            density.RegisterValueChangedCallback(_ => Refresh()); reach.RegisterValueChangedCallback(_ => Refresh());
            Refresh();
            TextStudioInspectorElements.SchedulePeriodicRefresh(section, () => { if (CanRunScheduledInspectorUpdate(section, hub)) Refresh(); }, 250);
            return section;
        }
    }
}
