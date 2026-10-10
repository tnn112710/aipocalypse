using System;
using System.Collections.Generic;
using System.IO;
using TextStudio.BuiltIns;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    [CustomEditor(typeof(TextStudioReactionAsset))]
    [CanEditMultipleObjects]
    internal sealed class TextStudioReactionAssetEditor : UnityEditor.Editor
    {
        TextStudioReactionLayerStackView _layerStack;
        TextStudioReactionPreviewElement _preview;

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-reactions-inspector");
            root.Add(TextStudioInspectorElements.CreateMutedText(
                "Respond to text events with shared effects, Audio Set cues, or contextual signals. Typewriter owns reveal timing."));
            if (!serializedObject.isEditingMultipleObjects)
                _preview = new TextStudioReactionPreviewElement((TextStudioReactionAsset)target,
                    serializedObject);
            _layerStack = new TextStudioReactionLayerStackView(serializedObject, "_layers",
                previewSolo: _preview == null ? null : _preview.ToggleSolo,
                currentPreviewSolo: () => _preview?.SoloLayerId);
            root.Add(_layerStack.Root);
            Foldout limits = new Foldout { text = "Limits", value = false };
            limits.Add(TextStudioInspectorElements.CreatePropertyField(serializedObject, "_capacity", "Concurrent Responses",
                tooltip: "Maximum simultaneous responses per text host. New responses are suppressed when this limit is reached."));
            limits.Add(TextStudioInspectorElements.CreatePropertyField(serializedObject, "_seed", "Seed",
                tooltip: "Stable random seed for chance and custom action variation. The same seed replays the same responses."));
            root.Add(limits);
            if (_preview != null) root.Add(_preview);
            _layerStack.BindStructureTracking();
            return root;
        }
    }

    internal static class TextStudioReactionRecipeInspector
    {
        static class EffectPickerOptions
        {
            internal static readonly string[] Choices;
            internal static readonly string[] Ids;

            static EffectPickerOptions()
            {
                List<string> choices = new List<string> { "None" };
                List<string> ids = new List<string> { string.Empty };
                TextStudioTagCatalog catalog = TextStudioTagCatalog.BuildDefault();
                for (int i = 0; i < catalog.Definitions.Count; i++)
                {
                    TextStudioTagDefinition definition = catalog.Definitions[i];
                    if (definition.Kind != TextStudioTagKind.TextStudioEffect || string.IsNullOrEmpty(definition.Id))
                        continue;
                    bool duplicate = false;
                    for (int option = 0; option < ids.Count; option++)
                        if (string.Equals(ids[option], definition.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            duplicate = true;
                            break;
                        }
                    if (duplicate)
                        continue;
                    ids.Add(definition.Id);
                    choices.Add(definition.DisplayName + " (" + definition.Id + ")");
                }
                Choices = choices.ToArray();
                Ids = ids.ToArray();
            }
        }

        /// <param name="hosts">
        /// Selected text hosts whose outputs filter the Built-In choices. Null keeps the complete catalog,
        /// as when a Behavior Profile is edited on its own.
        /// </param>
        internal static VisualElement Create(
            SerializedObject serializedObject,
            string recipePath,
            Action createCustomAsset,
            string namePrefix,
            IReadOnlyList<TextStudioText> hosts = null)
        {
            VisualElement root = new VisualElement { name = namePrefix + "-reaction-recipe" };
            SerializedProperty source = serializedObject.FindProperty(recipePath + ".Source");
            SerializedProperty presetId = serializedObject.FindProperty(recipePath + ".BuiltInPresetId");
            SerializedProperty customAsset = serializedObject.FindProperty(recipePath + ".CustomAsset");
            PropertyField sourceField = TextStudioInspectorElements.CreatePropertyField(
                serializedObject, source.propertyPath, "Source",
                tooltip: "Choose no reactions, a curated built-in recipe, a reusable asset, or responses stored on this host.");
            sourceField.name = namePrefix + "-source";
            root.Add(sourceField);
            VisualElement choiceBody = new VisualElement { name = namePrefix + "-choice" };
            root.Add(choiceBody);
            Label description = new Label { name = namePrefix + "-description" };
            description.AddToClassList("ts-muted-text");
            description.style.whiteSpace = WhiteSpace.Normal;
            choiceBody.Add(description);
            TextStudioReactionLayerStackView inlineLayers = new TextStudioReactionLayerStackView(
                serializedObject, recipePath + ".InlineLayers", namePrefix + "-inline");
            Button saveInline = TextStudioInspectorElements.CreateButton(
                "Save as Reaction Asset",
                "Create one reusable asset from each selected host's inline responses and use it as that host's Custom source.",
                () =>
                {
                    string path = EditorUtility.SaveFilePanelInProject(
                        "Save Inline Reactions", "Text Studio Reactions", "asset",
                        "Choose the asset name and folder. Multiple selected hosts create separate assets in this folder.");
                    if (string.IsNullOrEmpty(path))
                        return;
                    if (!TrySaveInlineAsAssets(serializedObject, recipePath, path,
                            out TextStudioReactionAsset[] saved, out string error))
                        Debug.LogWarning(error, serializedObject.targetObject);
                    else if (saved.Length > 0)
                        EditorGUIUtility.PingObject(saved[0]);
                });
            saveInline.name = namePrefix + "-save-inline";
            VisualElement capacity = new VisualElement { name = namePrefix + "-inline-limits" };
            capacity.Add(TextStudioInspectorElements.CreatePropertyField(
                serializedObject, recipePath + "._inlineCapacity", "Concurrent Responses",
                tooltip: "Maximum simultaneous inline responses per text host. New responses are suppressed at this limit."));
            capacity.Add(TextStudioInspectorElements.CreatePropertyField(
                serializedObject, recipePath + "._inlineSeed", "Seed",
                tooltip: "Stable random seed for this host's inline reaction chance and action variation."));
            Action refresh = null;
            VisualElement presetRow = null;
            void EnsurePresetPicker()
            {
                if (presetRow != null)
                    return;
                presetRow = BuildPresetPicker(serializedObject, presetId, namePrefix, hosts);
                presetRow.TrackPropertyValue(presetId, _ => refresh());
            }
            Button convert = TextStudioInspectorElements.CreateButton(
                "Convert to Inline",
                "Copy the selected preset or asset into editable reactions on this host.",
                () =>
                {
                    if (!ConvertToInline(serializedObject, recipePath, out string error))
                    {
                        Debug.LogWarning(error, serializedObject.targetObject);
                        return;
                    }
                    refresh();
                });
            convert.name = namePrefix + "-convert-inline";
            convert.AddToClassList("ts-secondary-button");
            Label conversionMessage = TextStudioInspectorElements.CreateMutedText(string.Empty);
            conversionMessage.name = namePrefix + "-convert-inline-message";

            ObjectField assetField = new ObjectField("Custom Asset")
            {
                name = namePrefix + "-custom-asset",
                objectType = typeof(TextStudioReactionAsset),
                tooltip = "Reusable reaction recipe shared by hosts and Behavior Profiles."
            };
            assetField.BindProperty(customAsset);
            Button createAsset = TextStudioInspectorElements.CreateCompactCreateButton(
                createCustomAsset, namePrefix + "-create-custom", "Create a reusable reaction asset.");
            VisualElement customRow = TextStudioInspectorElements.CreateAssetReferenceRow(
                assetField, createAsset, namePrefix + "-custom-row");
            customAsset.serializedObject.UpdateIfRequiredOrScript();
            customRow.TrackPropertyValue(customAsset, _ =>
                TextStudioInspectorElements.SetCompactCreateButtonAvailable(createAsset, customAsset.objectReferenceValue == null));
            TextStudioInspectorElements.SetCompactCreateButtonAvailable(createAsset, customAsset.objectReferenceValue == null);
            refresh = () =>
            {
                serializedObject.UpdateIfRequiredOrScript();
                TextStudioReactionSource current = (TextStudioReactionSource)source.enumValueIndex;
                choiceBody.Clear();
                if (source.hasMultipleDifferentValues)
                {
                    choiceBody.Add(TextStudioInspectorElements.CreateMutedText(
                        "Selected hosts use different reaction sources. Convert each current Built-In or Custom source independently, or choose one source for all."));
                    choiceBody.Add(convert);
                    conversionMessage.text = GetInlineConversionUnavailableReason(serializedObject.targetObjects, recipePath);
                    if (!string.IsNullOrEmpty(conversionMessage.text)) choiceBody.Add(conversionMessage);
                }
                else
                {
                    switch (current)
                    {
                        case TextStudioReactionSource.BuiltIn:
                        {
                            EnsurePresetPicker();
                            string selectedId = string.IsNullOrEmpty(presetId.stringValue)
                                ? BuiltInReactionPresets.Default.Id
                                : presetId.stringValue;
                            TextStudioBuiltInReactionPreset? selected = BuiltInReactionPresets.ForId(selectedId);
                            if (selected.HasValue)
                                choiceBody.Add(TextStudioInspectorElements.CreateMutedText(selected.Value.Description));
                            choiceBody.Add(presetRow);
                            choiceBody.Add(convert);
                            string builtInUnavailable = GetInlineConversionUnavailableReason(serializedObject.targetObjects, recipePath);
                            conversionMessage.text = builtInUnavailable;
                            if (!string.IsNullOrEmpty(builtInUnavailable)) choiceBody.Add(conversionMessage);
                            break;
                        }
                        case TextStudioReactionSource.Custom:
                            choiceBody.Add(customRow);
                            if (customAsset.objectReferenceValue == null)
                                choiceBody.Add(TextStudioInspectorElements.CreateMutedText(
                                    "Assign a reusable reaction asset or use + to create one."));
                            choiceBody.Add(convert);
                            string unavailable = GetInlineConversionUnavailableReason(serializedObject.targetObjects, recipePath);
                            conversionMessage.text = unavailable;
                            if (!string.IsNullOrEmpty(unavailable)) choiceBody.Add(conversionMessage);
                            break;
                        case TextStudioReactionSource.Inline:
                            choiceBody.Add(capacity);
                            choiceBody.Add(inlineLayers.Root);
                            choiceBody.Add(saveInline);
                            break;
                        default:
                            choiceBody.Add(TextStudioInspectorElements.CreateMutedText(
                                "Reactions are off. This host creates no reaction scheduler."));
                            break;
                    }
                }
                convert.SetEnabled(string.IsNullOrEmpty(
                    GetInlineConversionUnavailableReason(serializedObject.targetObjects, recipePath)));
            };
            sourceField.TrackPropertyValue(source, _ => refresh());
            assetField.TrackPropertyValue(customAsset, _ => refresh());
            refresh();
            inlineLayers.BindStructureTracking();
            return root;
        }

        /// <summary>
        /// Built-In recipe picker. With hosts, offers renderer-independent recipes plus those suiting every
        /// host's configured output. A saved recipe that no longer suits or exists stays selected with a
        /// warning and is not offered again once replaced.
        /// </summary>
        internal static VisualElement BuildPresetPicker(SerializedObject serializedObject,
            SerializedProperty presetId, string namePrefix, IReadOnlyList<TextStudioText> hosts)
        {
            VisualElement root = new VisualElement { name = namePrefix + "-built-in-picker" };
            DropdownField field = new DropdownField
            {
                name = namePrefix + "-built-in",
                tooltip = hosts == null
                    ? "Select a curated reaction recipe. Convert it to Inline to edit this host independently."
                    : "Select a curated reaction recipe suited to this host's text output. Convert it to Inline to edit this host independently."
            };
            VisualElement row = TextStudioInspectorElements.CreateAlignedControlRow("Built-In", field, namePrefix + "-built-in-row");
            root.Add(row);
            HelpBox warning = TextStudioInspectorElements.CreateHelpBox(string.Empty, HelpBoxMessageType.Warning,
                namePrefix + "-built-in-warning");
            root.Add(warning);
            Label outputHint = TextStudioInspectorElements.CreateMutedText(
                "Output-specific recipes appear once this host has a configured text output, such as TextMeshPro or a 3D renderer.",
                namePrefix + "-built-in-output-hint");
            root.Add(outputHint);

            List<string> labels = new List<string>();
            List<string> ids = new List<string>();
            TextStudioReactionOutputKinds[] outputs = hosts == null ? null : new TextStudioReactionOutputKinds[hosts.Count];
            long signature = long.MinValue;
            string renderedId = null;
            bool renderedMixed = false;
            bool refreshing = false;

            long ComputeContextSignature()
            {
                long value = BuiltInReactionPresets.CatalogRevision;
                if (outputs == null)
                    return value;
                for (int i = 0; i < outputs.Length; i++)
                {
                    outputs[i] = TextStudioReactionInspectorContext.ResolveOutputs(hosts[i]);
                    value = value * 31 + (int)outputs[i] + 1;
                }
                return value;
            }

            void Refresh()
            {
                signature = ComputeContextSignature();
                serializedObject.UpdateIfRequiredOrScript();
                SerializedProperty current = serializedObject.FindProperty(presetId.propertyPath);
                string currentId = EffectivePresetId(current.stringValue);
                renderedId = current.stringValue;
                renderedMixed = current.hasMultipleDifferentValues;
                IReadOnlyList<TextStudioBuiltInReactionPreset> presets = BuiltInReactionPresets.All();
                labels.Clear();
                ids.Clear();
                for (int i = 0; i < presets.Count; i++)
                    if (TextStudioReactionInspectorContext.Suits(presets[i], outputs))
                        AddChoice(presets[i].DisplayName, presets[i].Id);

                int selected = TextStudioReactionInspectorContext.IndexOfOrdinal(ids, currentId);
                TextStudioBuiltInReactionPreset? saved = BuiltInReactionPresets.ForId(currentId);
                if (selected < 0)
                {
                    AddChoice(saved.HasValue ? saved.Value.DisplayName : "Missing recipe (" + currentId + ")", currentId);
                    selected = ids.Count - 1;
                }

                string message = current.hasMultipleDifferentValues
                    ? DescribeUnsuitedSelection(serializedObject, presetId.propertyPath, outputs)
                    : !saved.HasValue
                        ? "The saved built-in recipe \"" + currentId + "\" no longer exists. It stays assigned until you choose another recipe."
                        : TextStudioReactionInspectorContext.Suits(saved.Value, outputs)
                            ? string.Empty
                            : DescribeUnsuitedPreset(saved.Value, outputs);
                warning.text = message;
                warning.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
                outputHint.style.display = HasUnknownOutput(outputs) ? DisplayStyle.Flex : DisplayStyle.None;

                refreshing = true;
                try
                {
                    field.choices = new List<string>(labels);
                    field.SetValueWithoutNotify(labels[selected]);
                    field.showMixedValue = current.hasMultipleDifferentValues;
                }
                finally
                {
                    refreshing = false;
                }
            }

            void AddChoice(string label, string id)
            {
                string unique = string.IsNullOrEmpty(label) ? id : label;
                if (TextStudioReactionInspectorContext.IndexOfOrdinal(labels, unique) >= 0)
                    unique += " (" + id + ")";
                labels.Add(unique);
                ids.Add(id);
            }

            field.RegisterValueChangedCallback(evt =>
            {
                if (refreshing)
                    return;
                int choice = TextStudioReactionInspectorContext.IndexOfOrdinal(labels, evt.newValue);
                if (choice < 0)
                    return;
                serializedObject.UpdateIfRequiredOrScript();
                SerializedProperty current = serializedObject.FindProperty(presetId.propertyPath);
                if (current.hasMultipleDifferentValues || !string.Equals(current.stringValue, ids[choice], StringComparison.Ordinal))
                {
                    current.stringValue = ids[choice];
                    serializedObject.ApplyModifiedProperties();
                }
                Refresh();
            });
            bool SavedValueChanged()
            {
                serializedObject.UpdateIfRequiredOrScript();
                SerializedProperty current = serializedObject.FindProperty(presetId.propertyPath);
                return current.hasMultipleDifferentValues != renderedMixed ||
                       !string.Equals(current.stringValue, renderedId, StringComparison.Ordinal);
            }

            row.TrackPropertyValue(presetId, _ => Refresh());
            // Output configuration has no serialized change event, and Undo or scripts may change the saved ID
            // while no binding repaint is pending.
            TextStudioInspectorElements.SchedulePeriodicRefresh(root, () =>
            {
                if (root.panel != null && serializedObject.targetObject != null &&
                    (ComputeContextSignature() != signature || SavedValueChanged()))
                    Refresh();
            }, 500);
            Refresh();
            return root;
        }

        static string EffectivePresetId(string id) =>
            string.IsNullOrEmpty(id) ? BuiltInReactionPresets.Default.Id : id;

        static bool HasUnknownOutput(TextStudioReactionOutputKinds[] outputs)
        {
            if (outputs == null)
                return false;
            for (int i = 0; i < outputs.Length; i++)
                if (outputs[i] == TextStudioReactionOutputKinds.RendererIndependent)
                    return true;
            return false;
        }

        static string DescribeUnsuitedPreset(in TextStudioBuiltInReactionPreset preset,
            IReadOnlyList<TextStudioReactionOutputKinds> outputs)
        {
            string designedFor = TextStudioReactionInspectorContext.Describe(preset.Outputs);
            string host = outputs.Count == 1
                ? "this host's " + (outputs[0] == TextStudioReactionOutputKinds.RendererIndependent
                    ? "unconfigured output"
                    : TextStudioReactionInspectorContext.Describe(outputs[0]) + " output")
                : "every selected host";
            return preset.DisplayName + " is designed for " + designedFor + " text and does not suit " + host +
                   ". It stays assigned until you choose another recipe.";
        }

        static string DescribeUnsuitedSelection(SerializedObject serializedObject, string presetPath,
            IReadOnlyList<TextStudioReactionOutputKinds> outputs)
        {
            UnityEngine.Object[] targets = serializedObject.targetObjects;
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;
                SerializedObject targetObject = new SerializedObject(targets[i]);
                string id = EffectivePresetId(targetObject.FindProperty(presetPath)?.stringValue);
                TextStudioBuiltInReactionPreset? preset = BuiltInReactionPresets.ForId(id);
                bool suits = preset.HasValue && (outputs == null || (targets.Length == outputs.Count
                    ? preset.Value.SuitsOutputs(outputs[i])
                    : TextStudioReactionInspectorContext.Suits(preset.Value, outputs)));
                if (!suits)
                    return "Some selected hosts keep a built-in recipe that is missing or does not suit their output. " +
                           "Each keeps its recipe until you choose one for all.";
            }
            return string.Empty;
        }

        internal static string GetInlineConversionUnavailableReason(UnityEngine.Object[] targets, string recipePath)
        {
            if (targets == null || targets.Length == 0)
                return "Select a host with a Built-In or Custom reaction source.";
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null)
                    return "A selected reaction host no longer exists.";
                SerializedObject targetObject = new SerializedObject(targets[i]);
                targetObject.UpdateIfRequiredOrScript();
                if (!TryGetSourceProperties(targetObject, recipePath,
                        out SerializedProperty source, out SerializedProperty presetId,
                        out SerializedProperty customAsset, out _))
                    return "The selected object does not contain the requested reaction recipe.";
                if (!TryResolveLayerSource(
                        (TextStudioReactionSource)source.enumValueIndex,
                        presetId.stringValue,
                        customAsset.objectReferenceValue as TextStudioReactionAsset,
                        out _, out _, out string error))
                    return error;
            }
            return string.Empty;
        }

        internal static bool ConvertToInline(SerializedObject serializedObject, string recipePath, out string error)
        {
            error = GetInlineConversionUnavailableReason(serializedObject?.targetObjects, recipePath);
            if (!string.IsNullOrEmpty(error))
                return false;

            UnityEngine.Object[] targets = serializedObject.targetObjects;
            TextStudioReactionLayer[][] copies = new TextStudioReactionLayer[targets.Length][];
            int[] capacities = new int[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                SerializedObject targetObject = new SerializedObject(targets[i]);
                targetObject.UpdateIfRequiredOrScript();
                if (!TryGetSourceProperties(targetObject, recipePath,
                        out SerializedProperty source, out SerializedProperty presetId,
                        out SerializedProperty customAsset, out error) ||
                    !TryCopyLayers((TextStudioReactionSource)source.enumValueIndex,
                        presetId.stringValue,
                        customAsset.objectReferenceValue as TextStudioReactionAsset,
                        out copies[i], out capacities[i], out error))
                    return false;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Convert Reactions to Inline");
            Undo.RecordObjects(targets, "Convert Reactions to Inline");
            for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
            {
                SerializedObject targetObject = new SerializedObject(targets[targetIndex]);
                targetObject.UpdateIfRequiredOrScript();
                SerializedProperty destination = targetObject.FindProperty(recipePath + ".InlineLayers");
                destination.arraySize = copies[targetIndex].Length;
                for (int i = 0; i < copies[targetIndex].Length; i++)
                    WriteLayer(destination.GetArrayElementAtIndex(i), copies[targetIndex][i]);
                targetObject.FindProperty(recipePath + "._inlineCapacity").intValue = capacities[targetIndex];
                TextStudioReactionAsset sourceAsset =
                    (TextStudioReactionSource)targetObject.FindProperty(recipePath + ".Source").enumValueIndex ==
                    TextStudioReactionSource.Custom
                        ? targetObject.FindProperty(recipePath + ".CustomAsset").objectReferenceValue as TextStudioReactionAsset
                        : null;
                targetObject.FindProperty(recipePath + "._inlineSeed").intValue =
                    sourceAsset != null ? (int)sourceAsset.Seed : 1;
                targetObject.FindProperty(recipePath + ".Source").enumValueIndex = (int)TextStudioReactionSource.Inline;
                targetObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(targets[targetIndex]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(targets[targetIndex]);
            }
            Undo.CollapseUndoOperations(undoGroup);
            serializedObject.UpdateIfRequiredOrScript();
            error = string.Empty;
            return true;
        }

        internal static bool TrySaveInlineAsAssets(SerializedObject serializedObject,
            string recipePath, string selectedPath,
            out TextStudioReactionAsset[] assets, out string error) =>
            TrySaveInlineAsAssets(serializedObject, recipePath, selectedPath, false,
                out assets, out error);

        internal static bool TrySaveInlineBindingAsAssets(SerializedObject serializedObject,
            string bindingPath, string selectedPath,
            out TextStudioReactionAsset[] assets, out string error) =>
            TrySaveInlineAsAssets(serializedObject, bindingPath, selectedPath, true,
                out assets, out error);

        static bool TrySaveInlineAsAssets(SerializedObject serializedObject,
            string recipePath, string selectedPath, bool uxmlBinding,
            out TextStudioReactionAsset[] assets, out string error)
        {
            assets = Array.Empty<TextStudioReactionAsset>();
            error = string.Empty;
            UnityEngine.Object[] targets = serializedObject?.targetObjects;
            if (targets == null || targets.Length == 0 || string.IsNullOrEmpty(recipePath))
            {
                error = "Select an Inline reaction host to save.";
                return false;
            }
            selectedPath = selectedPath?.Replace('\\', '/');
            string folder = Path.GetDirectoryName(selectedPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(selectedPath) || !selectedPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !string.Equals(Path.GetExtension(selectedPath), ".asset", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                error = "Choose an .asset path inside an existing Assets folder.";
                return false;
            }

            TextStudioReactionLayer[][] copies = new TextStudioReactionLayer[targets.Length][];
            int[] capacities = new int[targets.Length];
            int[] seeds = new int[targets.Length];
            string sourceName = uxmlBinding ? nameof(TextStudioBinding.ReactionSource) : "Source";
            string assetName = uxmlBinding ? nameof(TextStudioBinding.ReactionAsset) : "CustomAsset";
            string layersName = uxmlBinding ? nameof(TextStudioBinding.InlineReactionLayers) : "InlineLayers";
            string capacityName = uxmlBinding ? nameof(TextStudioBinding.InlineReactionCapacity) : "_inlineCapacity";
            string seedName = uxmlBinding ? nameof(TextStudioBinding.InlineReactionSeed) : "_inlineSeed";
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null)
                {
                    error = "A selected reaction host no longer exists.";
                    return false;
                }
                SerializedObject targetObject = new SerializedObject(targets[i]);
                targetObject.UpdateIfRequiredOrScript();
                SerializedProperty source = targetObject.FindProperty(recipePath + "." + sourceName);
                SerializedProperty customAsset = targetObject.FindProperty(recipePath + "." + assetName);
                SerializedProperty layers = targetObject.FindProperty(recipePath + "." + layersName);
                SerializedProperty capacity = targetObject.FindProperty(recipePath + "." + capacityName);
                SerializedProperty seed = targetObject.FindProperty(recipePath + "." + seedName);
                if (source == null || customAsset == null || layers == null || capacity == null || seed == null ||
                    (TextStudioReactionSource)source.enumValueIndex != TextStudioReactionSource.Inline)
                {
                    error = "Every selected host must use Inline Reactions before saving it as an asset.";
                    return false;
                }
                TextStudioReactionLayer[] originals = new TextStudioReactionLayer[layers.arraySize];
                for (int layer = 0; layer < originals.Length; layer++)
                    originals[layer] = ReadLayer(layers.GetArrayElementAtIndex(layer));
                copies[i] = TextStudioReactionLayer.CloneStack(originals);
                capacities[i] = Mathf.Clamp(capacity.intValue == 0 ? 64 : capacity.intValue, 1, 4096);
                seeds[i] = Mathf.Max(1, seed.intValue);
            }

            string[] paths = new string[targets.Length];
            string baseName = Path.GetFileNameWithoutExtension(selectedPath);
            for (int i = 0; i < paths.Length; i++)
            {
                string candidate = targets.Length == 1
                    ? selectedPath
                    : folder + "/" + baseName + " " + (i + 1) + ".asset";
                paths[i] = AssetDatabase.GenerateUniqueAssetPath(candidate);
            }

            TextStudioReactionAsset[] created = new TextStudioReactionAsset[targets.Length];
            string[] createdGuids = new string[targets.Length];
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Save Inline Reactions as Assets");
            try
            {
                for (int i = 0; i < created.Length; i++)
                {
                    TextStudioReactionAsset asset = ScriptableObject.CreateInstance<TextStudioReactionAsset>();
                    created[i] = asset;
                    SerializedObject assetObject = new SerializedObject(asset);
                    SerializedProperty assetLayers = assetObject.FindProperty("_layers");
                    assetLayers.arraySize = copies[i].Length;
                    for (int layer = 0; layer < copies[i].Length; layer++)
                    {
                        SerializedProperty destination = assetLayers.GetArrayElementAtIndex(layer);
                        if (copies[i][layer] == null) destination.boxedValue = null;
                        else WriteLayer(destination, copies[i][layer]);
                    }
                    assetObject.FindProperty("_capacity").intValue = capacities[i];
                    assetObject.FindProperty("_seed").intValue = seeds[i];
                    assetObject.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.CreateAsset(asset, paths[i]);
                    if (!AssetDatabase.Contains(asset))
                        throw new InvalidOperationException("Unity could not create " + paths[i]);
                    createdGuids[i] = AssetDatabase.AssetPathToGUID(paths[i]);
                    Undo.RegisterCreatedObjectUndo(asset, "Save Inline Reactions as Assets");
                }

                Undo.RecordObjects(targets, "Use Saved Reaction Assets");
                for (int i = 0; i < targets.Length; i++)
                {
                    SerializedObject targetObject = new SerializedObject(targets[i]);
                    targetObject.UpdateIfRequiredOrScript();
                    SerializedProperty assetReference = targetObject.FindProperty(recipePath + "." + assetName);
                    SerializedProperty source = targetObject.FindProperty(recipePath + "." + sourceName);
                    assetReference.objectReferenceValue = created[i];
                    source.enumValueIndex = (int)TextStudioReactionSource.Custom;
                    if (uxmlBinding)
                    {
                        TextStudioUIToolkitUxmlAuthoringStore.MarkAttributeOverridden(
                            targetObject, assetReference.propertyPath);
                        TextStudioUIToolkitUxmlAuthoringStore.MarkAttributeOverridden(
                            targetObject, source.propertyPath);
                    }
                    targetObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(targets[i]);
                    if (!uxmlBinding)
                        PrefabUtility.RecordPrefabInstancePropertyModifications(targets[i]);
                }
                Undo.CollapseUndoOperations(undoGroup);
                serializedObject.UpdateIfRequiredOrScript();
                assets = created;
                return true;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                for (int i = 0; i < created.Length; i++)
                {
                    if (!string.IsNullOrEmpty(createdGuids[i]) &&
                        string.Equals(AssetDatabase.AssetPathToGUID(paths[i]), createdGuids[i],
                            StringComparison.Ordinal))
                        AssetDatabase.DeleteAsset(paths[i]);
                    else if (created[i] != null && !AssetDatabase.Contains(created[i]))
                        UnityEngine.Object.DestroyImmediate(created[i]);
                }
                error = "Inline Reactions could not be saved: " + exception.Message;
                return false;
            }
        }

        internal static string GetBindingInlineConversionUnavailableReason(
            UnityEngine.Object[] targets, string bindingPath)
        {
            if (targets == null || targets.Length == 0 || string.IsNullOrEmpty(bindingPath))
                return "Select a UI Toolkit binding with a Built-In or Custom reaction source.";
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null)
                    return "A selected UI Toolkit binding no longer exists.";
                SerializedObject targetObject = new SerializedObject(targets[i]);
                targetObject.UpdateIfRequiredOrScript();
                if (!TryGetBindingSourceProperties(targetObject, bindingPath,
                        out SerializedProperty source, out SerializedProperty presetId,
                        out SerializedProperty customAsset, out _, out _, out _))
                    return "A selected object does not contain the requested UI Toolkit reaction binding.";
                if (!TryResolveLayerSource((TextStudioReactionSource)source.enumValueIndex,
                        presetId.stringValue,
                        customAsset.objectReferenceValue as TextStudioReactionAsset,
                        out _, out _, out string error))
                    return error;
            }
            return string.Empty;
        }

        internal static bool ConvertBindingToInline(
            SerializedObject serializedObject, string bindingPath, out string error)
        {
            error = GetBindingInlineConversionUnavailableReason(
                serializedObject?.targetObjects, bindingPath);
            if (!string.IsNullOrEmpty(error))
                return false;

            UnityEngine.Object[] targets = serializedObject.targetObjects;
            TextStudioReactionLayer[][] copies = new TextStudioReactionLayer[targets.Length][];
            int[] capacities = new int[targets.Length];
            int[] seeds = new int[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                SerializedObject targetObject = new SerializedObject(targets[i]);
                targetObject.UpdateIfRequiredOrScript();
                TryGetBindingSourceProperties(targetObject, bindingPath,
                    out SerializedProperty source, out SerializedProperty presetId,
                    out SerializedProperty customAsset, out _, out _, out _);
                TextStudioReactionSource kind = (TextStudioReactionSource)source.enumValueIndex;
                TextStudioReactionAsset asset = customAsset.objectReferenceValue as TextStudioReactionAsset;
                if (!TryCopyLayers(kind, presetId.stringValue, asset,
                        out copies[i], out capacities[i], out error))
                    return false;
                seeds[i] = kind == TextStudioReactionSource.Custom && asset != null
                    ? (int)asset.Seed
                    : 1;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Convert Reactions to Inline");
            Undo.RecordObjects(targets, "Convert Reactions to Inline");
            for (int i = 0; i < targets.Length; i++)
            {
                SerializedObject targetObject = new SerializedObject(targets[i]);
                targetObject.UpdateIfRequiredOrScript();
                TryGetBindingSourceProperties(targetObject, bindingPath,
                    out SerializedProperty source, out _, out _,
                    out SerializedProperty destination, out SerializedProperty capacity,
                    out SerializedProperty seed);
                destination.arraySize = copies[i].Length;
                for (int layer = 0; layer < copies[i].Length; layer++)
                    WriteLayer(destination.GetArrayElementAtIndex(layer), copies[i][layer]);
                capacity.intValue = capacities[i];
                seed.intValue = seeds[i];
                source.enumValueIndex = (int)TextStudioReactionSource.Inline;
                TextStudioUIToolkitUxmlAuthoringStore.MarkAttributeOverridden(targetObject, destination.propertyPath);
                TextStudioUIToolkitUxmlAuthoringStore.MarkAttributeOverridden(targetObject, capacity.propertyPath);
                TextStudioUIToolkitUxmlAuthoringStore.MarkAttributeOverridden(targetObject, seed.propertyPath);
                TextStudioUIToolkitUxmlAuthoringStore.MarkAttributeOverridden(targetObject, source.propertyPath);
                targetObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(targets[i]);
            }
            Undo.CollapseUndoOperations(undoGroup);
            serializedObject.UpdateIfRequiredOrScript();
            error = string.Empty;
            return true;
        }

        static bool TryGetBindingSourceProperties(
            SerializedObject targetObject, string bindingPath,
            out SerializedProperty source, out SerializedProperty presetId,
            out SerializedProperty customAsset, out SerializedProperty layers,
            out SerializedProperty capacity, out SerializedProperty seed)
        {
            source = targetObject.FindProperty(bindingPath + "." + nameof(TextStudioBinding.ReactionSource));
            presetId = targetObject.FindProperty(bindingPath + "." + nameof(TextStudioBinding.BuiltInReactionPresetId));
            customAsset = targetObject.FindProperty(bindingPath + "." + nameof(TextStudioBinding.ReactionAsset));
            layers = targetObject.FindProperty(bindingPath + "." + nameof(TextStudioBinding.InlineReactionLayers));
            capacity = targetObject.FindProperty(bindingPath + "." + nameof(TextStudioBinding.InlineReactionCapacity));
            seed = targetObject.FindProperty(bindingPath + "." + nameof(TextStudioBinding.InlineReactionSeed));
            return source != null && presetId != null && customAsset != null &&
                   layers != null && capacity != null && seed != null;
        }

        internal static bool TryCopyLayers(
            TextStudioReactionSource source,
            string presetId,
            TextStudioReactionAsset customAsset,
            out TextStudioReactionLayer[] layers,
            out int capacity,
            out string error)
        {
            layers = Array.Empty<TextStudioReactionLayer>();
            capacity = 64;
            if (!TryResolveLayerSource(source, presetId, customAsset,
                    out IReadOnlyList<TextStudioReactionLayer> sourceLayers, out capacity, out error))
                return false;
            layers = TextStudioReactionLayer.CloneStack(sourceLayers);
            return true;
        }

        static bool TryResolveLayerSource(
            TextStudioReactionSource source,
            string presetId,
            TextStudioReactionAsset customAsset,
            out IReadOnlyList<TextStudioReactionLayer> layers,
            out int capacity,
            out string error)
        {
            layers = Array.Empty<TextStudioReactionLayer>();
            capacity = 64;
            error = string.Empty;
            switch (source)
            {
                case TextStudioReactionSource.BuiltIn:
                    string resolvedId = string.IsNullOrEmpty(presetId) ? BuiltInReactionPresets.Default.Id : presetId;
                    TextStudioBuiltInReactionPreset? preset = BuiltInReactionPresets.ForId(resolvedId);
                    if (!preset.HasValue)
                    {
                        error = "The selected built-in reaction is unavailable. Choose another preset before converting.";
                        return false;
                    }
                    layers = preset.Value.RuntimeLayers;
                    capacity = preset.Value.Capacity;
                    return true;
                case TextStudioReactionSource.Custom:
                    if (customAsset == null)
                    {
                        error = "Assign a reusable reaction asset on every selected host before converting.";
                        return false;
                    }
                    layers = customAsset.Layers;
                    capacity = customAsset.Capacity;
                    return true;
                default:
                    error = "Each selected host must use a Built-In or Custom reaction source to convert it to Inline.";
                    return false;
            }
        }

        static bool TryGetSourceProperties(
            SerializedObject targetObject,
            string recipePath,
            out SerializedProperty source,
            out SerializedProperty presetId,
            out SerializedProperty customAsset,
            out string error)
        {
            source = targetObject.FindProperty(recipePath + ".Source");
            presetId = targetObject.FindProperty(recipePath + ".BuiltInPresetId");
            customAsset = targetObject.FindProperty(recipePath + ".CustomAsset");
            error = source != null && presetId != null && customAsset != null
                ? string.Empty
                : "The selected object does not contain the requested reaction recipe.";
            return string.IsNullOrEmpty(error);
        }

        internal static void WriteLayer(SerializedProperty destination, TextStudioReactionLayer layer)
        {
            layer ??= new TextStudioReactionLayer();
            if (FindLayerField(destination, "_id") == null)
                destination.boxedValue = destination.propertyPath.Contains(".InlineReactionLayers.Array.data[")
                    ? (object)new TextStudioReactionLayer.UxmlSerializedData()
                    : new TextStudioReactionLayer();
            FindLayerField(destination, "_id").stringValue = layer.Id;
            FindLayerField(destination, "_name").stringValue = layer.Name;
            FindLayerField(destination, "_enabled").boolValue = layer.Enabled;
            FindLayerField(destination, "_when").enumValueIndex = (int)layer.When;
            FindLayerField(destination, "_triggerName").stringValue = layer.TriggerName;
            FindLayerField(destination, "_which").enumValueIndex = (int)layer.Which;
            FindLayerField(destination, "_action").enumValueIndex = (int)layer.Action;
            FindLayerField(destination, "_effectSource").enumValueIndex = (int)layer.EffectSource;
            FindLayerField(destination, "_effect").objectReferenceValue = layer.Effect;
            FindLayerField(destination, "_effectId").stringValue = layer.EffectId;
            FindLayerField(destination, "_signal").stringValue = layer.Signal;
            FindLayerField(destination, "_audioCue").stringValue = layer.AudioCue;
            FindLayerField(destination, "_customAction").objectReferenceValue = layer.CustomAction;
            FindLayerField(destination, "_targetBindingKey").stringValue = layer.TargetBindingKey;
            FindLayerField(destination, "_enabledInputKey").stringValue = layer.EnabledInputKey;
            FindLayerField(destination, "_delay").floatValue = layer.Delay;
            FindLayerField(destination, "_duration").floatValue = layer.Duration;
            FindLayerField(destination, "_intensity").floatValue = layer.Intensity;
            FindLayerField(destination, "_chance").floatValue = layer.Chance;
            FindLayerField(destination, "_cooldown").floatValue = layer.Cooldown;
            FindLayerField(destination, "_overlap").enumValueIndex = (int)layer.Overlap;
            FindLayerField(destination, "_dependsOnId").stringValue = layer.DependsOnId;
            FindLayerField(destination, "_dependencyEvent").enumValueIndex = (int)layer.DependencyEvent;
            MarkLayerFieldsOverridden(destination);
        }

        internal static SerializedProperty FindLayerField(SerializedProperty layer, string fieldName)
        {
            SerializedProperty field = layer?.FindPropertyRelative(fieldName);
            if (field != null || string.IsNullOrEmpty(fieldName) || fieldName[0] != '_')
                return field;
            string uxmlName = char.ToUpperInvariant(fieldName[1]) + fieldName.Substring(2);
            return layer.FindPropertyRelative(uxmlName);
        }

        internal static TextStudioReactionLayer ReadLayer(SerializedProperty source)
        {
            if (source == null)
                return null;
            SerializedProperty id = FindLayerField(source, "_id");
            if (id == null)
                return null;
            return new TextStudioReactionLayer
            {
                Id = id.stringValue,
                Name = FindLayerField(source, "_name").stringValue,
                Enabled = FindLayerField(source, "_enabled").boolValue,
                When = (TextStudioReactionTrigger)FindLayerField(source, "_when").enumValueIndex,
                TriggerName = FindLayerField(source, "_triggerName").stringValue,
                Which = (TextStudioReactionTarget)FindLayerField(source, "_which").enumValueIndex,
                Action = (TextStudioReactionAction)FindLayerField(source, "_action").enumValueIndex,
                EffectSource = (TextStudioReactionEffectSource)FindLayerField(source, "_effectSource").enumValueIndex,
                Effect = FindLayerField(source, "_effect").objectReferenceValue as TextStudioEffectDefinitionAsset,
                EffectId = FindLayerField(source, "_effectId").stringValue,
                Signal = FindLayerField(source, "_signal").stringValue,
                AudioCue = FindLayerField(source, "_audioCue").stringValue,
                CustomAction = FindLayerField(source, "_customAction").objectReferenceValue as TextStudioReactionActionAsset,
                TargetBindingKey = FindLayerField(source, "_targetBindingKey").stringValue,
                EnabledInputKey = FindLayerField(source, "_enabledInputKey").stringValue,
                Delay = FindLayerField(source, "_delay").floatValue,
                Duration = FindLayerField(source, "_duration").floatValue,
                Intensity = FindLayerField(source, "_intensity").floatValue,
                Chance = FindLayerField(source, "_chance").floatValue,
                Cooldown = FindLayerField(source, "_cooldown").floatValue,
                Overlap = (TextStudioReactionOverlap)FindLayerField(source, "_overlap").enumValueIndex,
                DependsOnId = FindLayerField(source, "_dependsOnId").stringValue,
                DependencyEvent = (TextStudioReactionDependencyEvent)FindLayerField(source, "_dependencyEvent").enumValueIndex
            };
        }

        internal static void MarkLayerFieldsOverridden(SerializedProperty layer)
        {
            if (layer == null || layer.FindPropertyRelative("Id") == null)
                return;
            TextStudioUIToolkitUxmlAuthoringStore.MarkAttributeOverridden(
                layer.serializedObject, layer.propertyPath);
            foreach (string fieldName in s_layerFieldNames)
            {
                SerializedProperty field = FindLayerField(layer, fieldName);
                TextStudioUIToolkitUxmlAuthoringStore.MarkAttributeOverridden(
                    layer.serializedObject, field.propertyPath);
            }
        }

        static readonly string[] s_layerFieldNames =
        {
            "_id", "_name", "_enabled", "_when", "_triggerName", "_which", "_action",
            "_effectSource", "_effect", "_effectId", "_signal", "_audioCue", "_customAction",
            "_targetBindingKey", "_enabledInputKey", "_delay", "_duration", "_intensity",
            "_chance", "_cooldown", "_overlap", "_dependsOnId", "_dependencyEvent"
        };

        internal static DropdownField CreateEffectPicker(SerializedObject serializedObject, SerializedProperty effectId)
        {
            int selected = Array.IndexOf(EffectPickerOptions.Ids, effectId.stringValue);
            if (selected < 0)
                selected = 0;
            DropdownField field = new DropdownField(new List<string>(EffectPickerOptions.Choices), selected)
            {
                tooltip = "Registered Text Studio effect used by this response. Choose Custom Asset for an authored effect asset."
            };
            field.RegisterValueChangedCallback(evt =>
            {
                serializedObject.UpdateIfRequiredOrScript();
                SerializedProperty current = serializedObject.FindProperty(effectId.propertyPath);
                int choice = EffectPickerOptions.Choices.Length > 0
                    ? Array.IndexOf(EffectPickerOptions.Choices, evt.newValue)
                    : 0;
                current.stringValue = choice > 0 && choice < EffectPickerOptions.Ids.Length
                    ? EffectPickerOptions.Ids[choice]
                    : string.Empty;
                TextStudioUIToolkitUxmlAuthoringStore.MarkAttributeOverridden(
                    serializedObject, current.propertyPath);
                serializedObject.ApplyModifiedProperties();
            });
            return field;
        }
    }

    internal sealed class TextStudioReactionLayerStackView
    {
        static SerializedProperty F(SerializedProperty layer, string fieldName) =>
            TextStudioReactionRecipeInspector.FindLayerField(layer, fieldName);

        readonly SerializedObject _serializedObject;
        readonly string _layersPath;
        readonly VisualElement _stack = new VisualElement { name = "ts-reaction-layers" };
        readonly HelpBox _validation = new HelpBox(string.Empty, HelpBoxMessageType.Error);
        readonly Button _add;
        readonly List<Action> _refreshDependencyPickers = new List<Action>();
        readonly Action<string> _previewSolo;
        readonly Func<string> _currentPreviewSolo;
        string _structure;

        internal VisualElement Root { get; }

        internal TextStudioReactionLayerStackView(SerializedObject serializedObject, string layersPath,
            string name = null, Action<string> previewSolo = null,
            Func<string> currentPreviewSolo = null)
        {
            _serializedObject = serializedObject;
            _layersPath = layersPath;
            _previewSolo = previewSolo;
            _currentPreviewSolo = currentPreviewSolo;
            Root = new VisualElement { name = name ?? "ts-reaction-stack" };
            _stack.name = (name ?? "ts-reaction") + "-layers";
            _validation.name = (name ?? "ts-reaction") + "-validation";
            Root.Add(_validation);
            Root.Add(_stack);
            _add = TextStudioInspectorElements.CreateButton("Add Reaction",
                "Add a finite response to a text lifecycle event.", AddLayer);
            _add.name = (name ?? "ts-reaction") + "-add";
            Root.Add(_add);
            Rebuild();
        }

        internal void BindStructureTracking()
        {
            Root.TrackSerializedObjectValue(_serializedObject, _ =>
            {
                if (_structure != Structure())
                    Rebuild();
                else
                    RefreshValidation();
            });
        }

        string Structure()
        {
            SerializedProperty layers = _serializedObject.FindProperty(_layersPath);
            string result = layers.arraySize + ":" + CanEditLayers();
            for (int i = 0; i < layers.arraySize; i++)
                result += ":" + F(layers.GetArrayElementAtIndex(i), "_id").stringValue;
            return result;
        }

        void Rebuild()
        {
            _stack.Unbind();
            _stack.Clear();
            _refreshDependencyPickers.Clear();
            _serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty layers = _serializedObject.FindProperty(_layersPath);
            _structure = Structure();
            bool canEdit = CanEditLayers();
            _add.SetEnabled(canEdit);
            if (!canEdit)
            {
                _stack.Add(new HelpBox("Selected hosts have different reactions. Select one host to edit them.", HelpBoxMessageType.Info));
                return;
            }
            if (layers.arraySize == 0)
                _stack.Add(TextStudioInspectorElements.CreateMutedText("No reactions configured."));
            for (int i = 0; i < layers.arraySize; i++)
                AddCard(layers.GetArrayElementAtIndex(i), i, layers.arraySize);
            RefreshValidation();
            _stack.Bind(_serializedObject);
        }

        void RefreshValidation()
        {
            if (!CanEditLayers())
            {
                _validation.style.display = DisplayStyle.None;
                return;
            }
            SerializedProperty layers = _serializedObject.FindProperty(_layersPath);
            for (int i = 0; i < _refreshDependencyPickers.Count; i++)
                _refreshDependencyPickers[i]();
            TextStudioReactionLayer[] definitions = new TextStudioReactionLayer[layers.arraySize];
            for (int i = 0; i < definitions.Length; i++)
                definitions[i] = TextStudioReactionRecipeInspector.ReadLayer(layers.GetArrayElementAtIndex(i));
            string error = TextStudioReactionRuntime.ValidateDependencies(definitions, 64);
            _validation.text = error ?? string.Empty;
            _validation.style.display = error != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        bool CanEditLayers()
        {
            if (!_serializedObject.isEditingMultipleObjects)
                return true;
            SerializedProperty layers = _serializedObject.FindProperty(_layersPath);
            SerializedProperty size = _serializedObject.FindProperty(_layersPath + ".Array.size");
            if (size != null && size.hasMultipleDifferentValues)
                return false;
            if (layers.arraySize > 0 && F(layers.GetArrayElementAtIndex(0), "_id").hasMultipleDifferentValues)
                return false;
            for (int i = 0; i < layers.arraySize; i++)
                if (F(layers.GetArrayElementAtIndex(i), "_id").hasMultipleDifferentValues)
                    return false;
            return true;
        }

        void AddCard(SerializedProperty layer, int index, int count)
        {
            string id = F(layer, "_id").stringValue;
            TextStudioInspectorElements.LayerCardShell shell = TextStudioInspectorElements.CreateLayerCardShell(
                "ts-reaction-" + id, "Reaction", index == 0, "A finite response to one semantic event.",
                () => EditLayer(id, property =>
                {
                    SerializedProperty enabled = F(property, "_enabled");
                    enabled.boolValue = !enabled.boolValue;
                }), _previewSolo == null ? null : () => { _previewSolo(id); Rebuild(); },
                () => Move(id, -1), () => Move(id, 1), () => Remove(id));
            shell.Foldout.viewDataKey = "reaction-" + id;
            shell.SoloButton.style.display = _previewSolo == null ? DisplayStyle.None : DisplayStyle.Flex;
            shell.SoloButton.name = "ts-reaction-preview-solo-" + id;
            shell.VisibilityButton.name = "ts-reaction-enabled-" + id;
            shell.MoveUpButton.name = "ts-reaction-up-" + id;
            shell.MoveDownButton.name = "ts-reaction-down-" + id;
            shell.DeleteButton.name = "ts-reaction-remove-" + id;
            shell.Foldout.Add(Field(layer, "_name", "Name", "Label for this reaction."));
            shell.Foldout.Add(Field(layer, "_when", "When", "Semantic event that starts this response. Layout rebuilds do not trigger reactions."));
            PropertyField signalName = Field(layer, "_triggerName", "Signal Name",
                "Exact, case-sensitive name sent by gameplay with PlayReaction. Leave empty to respond to every signal name.");
            signalName.name = "ts-reaction-signal-name-" + id;
            PropertyField markerName = Field(layer, "_triggerName", "Marker Name",
                "Exact, case-sensitive marker name from the text markup. Leave empty to respond to every marker.");
            markerName.name = "ts-reaction-marker-name-" + id;
            shell.Foldout.Add(signalName);
            shell.Foldout.Add(markerName);
            shell.Foldout.Add(Field(layer, "_action", "Action", "Play an effect, play an Audio Set cue, send a signal, or run a custom action provider."));
            PropertyField which = Field(layer, "_which", "Which", "Select the text, event unit, word, line, or incoming units changed by Text Changed. Word and line need an event unit.");
            PropertyField effectSource = Field(layer, "_effectSource", "Effect Source", "Choose a registered effect or an authored effect asset.");
            PropertyField effect = Field(layer, "_effect", "Custom Effect", "Shared effect or composition evaluated over the response duration.");
            DropdownField effectId = TextStudioReactionRecipeInspector.CreateEffectPicker(
                _serializedObject, F(layer, "_effectId"));
            effectId.AddToClassList("ts-layer-aligned-field");
            PropertyField signal = Field(layer, "_signal", "Outgoing Signal", "Callback name this reaction sends to OnReactionSignal listeners after the rendered state is committed. Resolve its semantic unit with TryGetReactionAnchor on the host. Outgoing signals do not trigger reactions.");
            PropertyField audioCue = Field(layer, "_audioCue", "Audio Cue", "Route played by a Text Studio Audio Player targeting this text host. The route must exist in its Audio Set.");
            PropertyField customAction = Field(layer, "_customAction", "Custom Action", "Reusable action definition compiled before playback. Scene targets are configured on the host.");
            PropertyField targetBinding = Field(layer, "_targetBindingKey", "Target Binding", "Scene-local target key resolved on the text host, never stored as an object reference on this recipe.");
            PropertyField duration = Field(layer, "_duration", "Duration", "Length of this response in host-clock seconds. Does not change Typewriter timing.");
            shell.Foldout.Add(which);
            shell.Foldout.Add(effectSource);
            shell.Foldout.Add(effectId);
            shell.Foldout.Add(effect);
            shell.Foldout.Add(signal);
            shell.Foldout.Add(audioCue);
            shell.Foldout.Add(customAction);
            shell.Foldout.Add(targetBinding);
            shell.Foldout.Add(duration);
            HelpBox incomplete = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            shell.Foldout.Add(incomplete);
            Foldout advanced = new Foldout { text = "Timing, Inputs and Limits", value = false };
            advanced.Add(Field(layer, "_enabledInputKey", "Boolean Input", "Optional named Boolean input. A matching false value suppresses new responses and cancels active ones. An absent input leaves the reaction enabled."));
            advanced.Add(Field(layer, "_delay", "Delay", "Seconds on the host clock before the response starts."));
            advanced.Add(Field(layer, "_intensity", "Intensity", "Response strength from zero to one, multiplied by event intensity."));
            advanced.Add(Field(layer, "_chance", "Chance", "Probability from zero to one. Uses the recipe seed and a stable reaction identity."));
            advanced.Add(Field(layer, "_cooldown", "Cooldown", "Minimum host-clock seconds between accepted triggers for this reaction."));
            advanced.Add(Field(layer, "_overlap", "Overlap", "Allow concurrent responses, restart the existing response, or ignore triggers while one is running."));
            advanced.Add(CreateDependencyPicker(id));
            PropertyField dependencyEvent = Field(layer, "_dependencyEvent", "After Event", "Completion is available for built-in actions. Arrival and Impact require an action provider with that event capability.");
            advanced.Add(dependencyEvent);
            shell.Foldout.Add(advanced);
            Button duplicate = TextStudioInspectorElements.CreateButton(
                "Duplicate Reaction",
                "Create an independent copy directly after this reaction.",
                () => Duplicate(id));
            duplicate.name = "ts-reaction-duplicate-" + id;
            shell.Foldout.Add(TextStudioInspectorElements.CreateLayerFooter(duplicate));
            Action refresh = () =>
            {
                SerializedProperty current = Find(id);
                if (current == null) return;
                TextStudioReactionTrigger trigger = (TextStudioReactionTrigger)F(current, "_when").enumValueIndex;
                TextStudioReactionAction action = (TextStudioReactionAction)F(current, "_action").enumValueIndex;
                TextStudioReactionEffectSource source = (TextStudioReactionEffectSource)F(current, "_effectSource").enumValueIndex;
                bool isEffect = action == TextStudioReactionAction.PlayEffect;
                bool isSignal = action == TextStudioReactionAction.SendSignal;
                bool isAudioCue = action == TextStudioReactionAction.PlayAudioCue;
                bool isCustom = action == TextStudioReactionAction.Custom;
                shell.Refresh(F(current, "_name").stringValue + " (" + ObjectNames.NicifyVariableName(trigger.ToString()) + ")",
                    F(current, "_enabled").boolValue, _currentPreviewSolo?.Invoke() == id,
                    index > 0, index < count - 1);
                shell.SoloButton.style.display = _previewSolo == null ? DisplayStyle.None : DisplayStyle.Flex;
                if (_previewSolo != null)
                    shell.SoloButton.tooltip = _currentPreviewSolo?.Invoke() == id
                        ? "Show every enabled reaction in this preview again."
                        : "Preview only this reaction. Solo is temporary and does not change the asset.";
                signalName.style.display = trigger == TextStudioReactionTrigger.Signal ? DisplayStyle.Flex : DisplayStyle.None;
                markerName.style.display = trigger == TextStudioReactionTrigger.Marker ? DisplayStyle.Flex : DisplayStyle.None;
                which.style.display = DisplayStyle.Flex;
                duration.style.display = isEffect || isCustom ? DisplayStyle.Flex : DisplayStyle.None;
                effectSource.style.display = isEffect ? DisplayStyle.Flex : DisplayStyle.None;
                effectId.style.display = isEffect && source == TextStudioReactionEffectSource.BuiltIn ? DisplayStyle.Flex : DisplayStyle.None;
                effect.style.display = isEffect && source == TextStudioReactionEffectSource.CustomAsset ? DisplayStyle.Flex : DisplayStyle.None;
                signal.style.display = isSignal ? DisplayStyle.Flex : DisplayStyle.None;
                audioCue.style.display = isAudioCue ? DisplayStyle.Flex : DisplayStyle.None;
                customAction.style.display = isCustom ? DisplayStyle.Flex : DisplayStyle.None;
                targetBinding.style.display = isCustom ? DisplayStyle.Flex : DisplayStyle.None;
                SerializedProperty selectedDependency = F(current, "_dependsOnId");
                dependencyEvent.style.display = selectedDependency.hasMultipleDifferentValues ||
                    !string.IsNullOrEmpty(selectedDependency.stringValue)
                    ? DisplayStyle.Flex : DisplayStyle.None;
                bool missing = isEffect
                    ? source == TextStudioReactionEffectSource.BuiltIn
                        ? string.IsNullOrEmpty(F(current, "_effectId").stringValue)
                        : F(current, "_effect").objectReferenceValue == null
                    : isSignal
                        ? string.IsNullOrEmpty(F(current, "_signal").stringValue)
                        : isAudioCue
                            ? string.IsNullOrEmpty(F(current, "_audioCue").stringValue)
                            : isCustom && F(current, "_customAction").objectReferenceValue == null;
                incomplete.text = isEffect ? "Choose a registered effect or assign a custom effect asset." :
                    isSignal ? "Enter a signal name to enable this response." :
                    isAudioCue ? "Enter an Audio Set cue route to enable this response." :
                    "Assign a custom reaction action to enable this response.";
                incomplete.style.display = missing ? DisplayStyle.Flex : DisplayStyle.None;
            };
            shell.Card.TrackPropertyValue(layer, _ => refresh());
            refresh();
            _stack.Add(shell.Card);
        }

        static PropertyField Field(SerializedProperty layer, string child, string label, string tooltip)
        {
            PropertyField field = TextStudioInspectorElements.CreatePropertyField(owner: layer.serializedObject,
                propertyPath: F(layer, child).propertyPath, label: label, tooltip: tooltip);
            field.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
            {
                SerializedProperty changed = evt.changedProperty;
                if (changed == null)
                    return;
                TextStudioUIToolkitUxmlAuthoringStore.MarkAttributeOverridden(
                    changed.serializedObject, changed.propertyPath);
                changed.serializedObject.ApplyModifiedProperties();
            });
            field.AddToClassList("ts-layer-aligned-field");
            return field;
        }

        DropdownField CreateDependencyPicker(string layerId)
        {
            DropdownField field = new DropdownField("After")
            {
                name = "ts-reaction-after-" + layerId,
                tooltip = "Start immediately, or wait for another reaction to complete or emit the selected After Event. Eligible reactions listen to the same trigger and name."
            };
            field.AddToClassList("ts-layer-aligned-field");
            List<string> labels = new List<string>();
            List<string> ids = new List<string>();
            bool refreshing = false;
            void Refresh()
            {
                refreshing = true;
                try
                {
                    SerializedProperty current = Find(layerId);
                    if (current == null) return;
                    SerializedProperty when = F(current, "_when");
                    SerializedProperty triggerName = F(current, "_triggerName");
                    SerializedProperty dependency = F(current, "_dependsOnId");
                    bool mixedTrigger = when.hasMultipleDifferentValues ||
                                        triggerName.hasMultipleDifferentValues;
                    field.SetEnabled(!mixedTrigger);
                    labels.Clear();
                    ids.Clear();
                    labels.Add("Start immediately");
                    ids.Add(string.Empty);
                    SerializedProperty layers = _serializedObject.FindProperty(_layersPath);
                    for (int i = 0; i < layers.arraySize && !mixedTrigger; i++)
                    {
                        SerializedProperty candidate = layers.GetArrayElementAtIndex(i);
                        string candidateId = F(candidate, "_id").stringValue;
                        SerializedProperty candidateWhen = F(candidate, "_when");
                        SerializedProperty candidateName = F(candidate, "_triggerName");
                        SerializedProperty candidateEnabled = F(candidate, "_enabled");
                        SerializedProperty candidateTarget = F(candidate, "_which");
                        if (candidateId == layerId ||
                            candidateEnabled.hasMultipleDifferentValues ||
                            !candidateEnabled.boolValue ||
                            candidateTarget.hasMultipleDifferentValues ||
                            candidateTarget.enumValueIndex ==
                                (int)TextStudioReactionTarget.ChangedOutgoingUnits ||
                            candidateWhen.hasMultipleDifferentValues ||
                            candidateName.hasMultipleDifferentValues ||
                            candidateWhen.enumValueIndex != when.enumValueIndex ||
                            candidateName.stringValue != triggerName.stringValue ||
                            WouldCreateDependencyCycle(candidateId, layerId, layers))
                            continue;
                        SerializedProperty name = F(candidate, "_name");
                        labels.Add((i + 1) + ". " + (name.hasMultipleDifferentValues
                            ? "Mixed names" : string.IsNullOrWhiteSpace(name.stringValue)
                                ? "Reaction" : name.stringValue));
                        ids.Add(candidateId);
                    }
                    string selectedId = dependency.stringValue;
                    int selected = ids.IndexOf(selectedId);
                    if (dependency.hasMultipleDifferentValues)
                    {
                        labels.Add("Mixed dependencies");
                        ids.Add(null);
                        selected = labels.Count - 1;
                    }
                    else if (selected < 0)
                    {
                        labels.Add("Unavailable dependency (" + selectedId + ")");
                        ids.Add(selectedId);
                        selected = labels.Count - 1;
                    }
                    field.choices = new List<string>(labels);
                    field.SetValueWithoutNotify(labels[selected]);
                }
                finally
                {
                    refreshing = false;
                }
            }
            field.RegisterValueChangedCallback(evt =>
            {
                if (refreshing) return;
                int choice = labels.IndexOf(evt.newValue);
                if (choice < 0 || choice >= ids.Count) return;
                string selectedId = ids[choice];
                if (selectedId == null) return;
                EditLayer(layerId, property =>
                    F(property, "_dependsOnId").stringValue = selectedId);
                RefreshValidation();
            });
            _refreshDependencyPickers.Add(Refresh);
            Refresh();
            return field;
        }

        static bool WouldCreateDependencyCycle(string candidateId, string layerId, SerializedProperty layers)
        {
            string cursor = candidateId;
            for (int step = 0; step < layers.arraySize && !string.IsNullOrEmpty(cursor); step++)
            {
                if (cursor == layerId)
                    return true;
                string next = null;
                for (int i = 0; i < layers.arraySize; i++)
                {
                    SerializedProperty candidate = layers.GetArrayElementAtIndex(i);
                    if (F(candidate, "_id").stringValue != cursor)
                        continue;
                    SerializedProperty dependency = F(candidate, "_dependsOnId");
                    if (dependency.hasMultipleDifferentValues)
                        return true;
                    next = dependency.stringValue;
                    break;
                }
                cursor = next;
            }
            return false;
        }

        SerializedProperty Find(string id)
        {
            SerializedProperty layers = _serializedObject.FindProperty(_layersPath);
            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (F(layer, "_id").stringValue == id)
                    return layer;
            }
            return null;
        }

        void EditLayer(string id, Action<SerializedProperty> edit)
        {
            _serializedObject.UpdateIfRequiredOrScript();
            if (!CanEditLayers()) return;
            SerializedProperty layer = Find(id);
            if (layer == null) return;
            edit(layer);
            TextStudioReactionRecipeInspector.MarkLayerFieldsOverridden(layer);
            MarkListOverridden();
            _serializedObject.ApplyModifiedProperties();
        }

        void MarkListOverridden() =>
            TextStudioUIToolkitUxmlAuthoringStore.MarkAttributeOverridden(
                _serializedObject, _layersPath);

        void AddLayer()
        {
            _serializedObject.UpdateIfRequiredOrScript();
            if (!CanEditLayers()) return;
            SerializedProperty layers = _serializedObject.FindProperty(_layersPath);
            int index = layers.arraySize;
            layers.InsertArrayElementAtIndex(index);
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            TextStudioReactionRecipeInspector.WriteLayer(layer, new TextStudioReactionLayer());
            MarkListOverridden();
            _serializedObject.ApplyModifiedProperties();
            Rebuild();
        }

        void Move(string id, int direction)
        {
            _serializedObject.UpdateIfRequiredOrScript();
            if (!CanEditLayers()) return;
            SerializedProperty layers = _serializedObject.FindProperty(_layersPath);
            for (int i = 0; i < layers.arraySize; i++)
                if (F(layers.GetArrayElementAtIndex(i), "_id").stringValue == id)
                {
                    int next = i + direction;
                    if (next < 0 || next >= layers.arraySize) return;
                    layers.MoveArrayElement(i, next);
                    MarkListOverridden();
                    _serializedObject.ApplyModifiedProperties();
                    Rebuild();
                    return;
            }
        }

        void Duplicate(string id)
        {
            _serializedObject.UpdateIfRequiredOrScript();
            if (!CanEditLayers()) return;
            SerializedProperty layers = _serializedObject.FindProperty(_layersPath);
            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty source = layers.GetArrayElementAtIndex(i);
                if (F(source, "_id").stringValue != id) continue;
                TextStudioReactionLayer original = TextStudioReactionRecipeInspector.ReadLayer(source);
                if (original == null) return;
                TextStudioReactionLayer copy = original.Clone();
                copy.Name = string.IsNullOrWhiteSpace(copy.Name)
                    ? "Reaction Copy"
                    : copy.Name + " Copy";
                int duplicateIndex = i + 1;
                layers.InsertArrayElementAtIndex(duplicateIndex);
                TextStudioReactionRecipeInspector.WriteLayer(
                    layers.GetArrayElementAtIndex(duplicateIndex), copy);
                MarkListOverridden();
                _serializedObject.ApplyModifiedProperties();
                Rebuild();
                return;
            }
        }

        void Remove(string id)
        {
            _serializedObject.UpdateIfRequiredOrScript();
            if (!CanEditLayers()) return;
            SerializedProperty layers = _serializedObject.FindProperty(_layersPath);
            for (int i = 0; i < layers.arraySize; i++)
                if (F(layers.GetArrayElementAtIndex(i), "_id").stringValue == id)
                {
                    layers.DeleteArrayElementAtIndex(i);
                    MarkListOverridden();
                    _serializedObject.ApplyModifiedProperties();
                    Rebuild();
                    return;
                }
        }
    }
}
