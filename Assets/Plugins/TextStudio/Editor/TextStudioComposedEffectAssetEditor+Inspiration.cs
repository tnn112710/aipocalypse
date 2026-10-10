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
        VisualElement CreateInspirationAction()
        {
            VisualElement inspiration = new VisualElement
            {
                name = "ts-composed-inspiration"
            };
            inspiration.AddToClassList("ts-composed-inspiration");

            VisualElement copy = new VisualElement();
            copy.AddToClassList("ts-composed-inspiration-copy");
            Label title = new Label("Try a fresh direction");
            title.AddToClassList("ts-composed-inspiration-title");
            copy.Add(title);
            Label description = TextStudioInspectorElements.CreateMutedText(
                "Generate a polished, fully editable recipe by replacing the Tag Stack and Visual Layers below. Undo restores the previous version.",
                "ts-composed-inspiration-description");
            copy.Add(description);
            inspiration.Add(copy);

            Button inspire = TextStudioInspectorElements.CreateButton(
                "Inspire Me",
                "Replace the Tag Stack and Visual Layers with a polished randomized recipe. The asset identity and preview choices stay unchanged, and Undo restores the previous recipe.",
                GenerateInspiration,
                primary: true);
            inspire.name = "ts-composed-inspire";
            inspire.AddToClassList("ts-composed-inspiration-button");
            if (target is TextStudioComposedEffectAsset effect &&
                !TextStudioAuthoringCapabilities.Supports(effect.RequiredRenderDomain))
            {
                inspire.SetEnabled(false);
                inspire.tooltip =
                    "Install the 3D add-on before generating a new recipe for this 3D Composed Effect.";
            }
            inspiration.Add(inspire);
            return inspiration;
        }

        internal void GenerateInspiration()
        {
            if (target is not TextStudioComposedEffectAsset effect)
                return;
            if (!TextStudioAuthoringCapabilities.Supports(effect.RequiredRenderDomain))
                return;

            int soloLayerIndex = -1;
            for (int i = 0; i < effect.Layers.Count; i++)
            {
                TextStudioCompositionLayer layer = effect.Layers[i];
                if (layer != null && string.Equals(
                        layer.StableId,
                        SoloLayerStableId,
                        StringComparison.Ordinal))
                {
                    soloLayerIndex = i;
                    break;
                }
            }

            Undo.RecordObject(effect, "Generate Inspired Composed Effect");
            TextStudioComposedEffectInspirationGenerator.Generate(effect);
            EditorUtility.SetDirty(effect);
            serializedObject.Update();

            if (soloLayerIndex >= 0 && effect.Layers.Count > 0)
            {
                _soloLayerStableId = effect.Layers[Mathf.Min(soloLayerIndex, effect.Layers.Count - 1)].StableId;
            }
            _expandedLayerStableId = string.Empty;
            if (_tagStackFoldout != null)
                _tagStackFoldout.value = effect.TagStack.Enabled;
            if (_visualLayersFoldout != null)
                _visualLayersFoldout.value = true;
            SessionState.SetBool(GetFoldoutStateKey("tag-stack"), effect.TagStack.Enabled);
            SessionState.SetBool(GetFoldoutStateKey("visual-layers"), true);

            RefreshPhaseButtons();
            RefreshSpeedButtons();
            RefreshPreviewRepeatButton();
            RefreshPreviewPlaybackButton();
            InvalidatePreviewPlan();
            RebuildLayerRows();
            UpdateValidation();
            RenderPreview();
        }
    }
}
