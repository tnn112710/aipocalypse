using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed partial class TextStudio2DStyleEditor
    {
        TextStudioTagInsertionCapture _appearanceInsertion;
        void AttachAppearanceInsertion()
        {
            _appearanceInsertion?.Dispose();
            _appearanceInsertion = null;
            if (Target is not TextStudioText) return;
            TextField source = _root?.panel?.visualTree.Q<TextField>("ts-source-field-" + Target.GetStableId());
            if (source != null) _appearanceInsertion = new TextStudioTagInsertionCapture(source, _root);
        }

        void AddFillTreatment(VisualElement parent, SerializedProperty layer)
        {
            SerializedProperty fill = layer.FindPropertyRelative("_fill");
            SerializedProperty native = fill?.FindPropertyRelative("_preservesNativeAppearance");
            if (native == null)
            {
                parent.Add(CreatePaintEditor(layer.FindPropertyRelative("_paint")));
                AddApplicableTreatmentChildren(parent, fill, TextStudio2DLayerKind.Fill);
                return;
            }
            parent.Add(CreateField(native, "Native Appearance", "Preserve the text backend's authored face, outline, texture and underlay. Custom paint, expansion and softness are inactive in this mode."));
            Label explanation = TextStudioInspectorElements.CreateMutedText("Uses the original text appearance, including native material details. Disable Native Appearance to paint a custom fill.");
            parent.Add(explanation);
            VisualElement custom = new VisualElement { name = "ts-fill-custom-appearance" };
            custom.Add(CreatePaintEditor(layer.FindPropertyRelative("_paint")));
            SerializedProperty child = fill.Copy();
            SerializedProperty end = child.GetEndProperty();
            bool enter = true;
            while (child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end))
            {
                enter = false;
                if (child.depth == fill.depth + 1 && child.name != "_preservesNativeAppearance") custom.Add(CreateField(child.Copy(), Nicify(child.name), "Adjust the custom fill treatment."));
            }
            parent.Add(custom);
            void Refresh()
            {
                custom.style.display = native.boolValue ? DisplayStyle.None : DisplayStyle.Flex;
                explanation.style.display = native.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            }
            parent.TrackPropertyValue(native, _ => Refresh());
            Refresh();
        }

        void AddInlineStyle()
        {
            MutateLayers("Add Inline Appearance Style", layers =>
            {
                TextStudio2DLayer group = TextStudio2DLayer.CreateGroup("Inline Style").WithAppearanceActivation(TextStudioAppearanceActivation.Inline(UniqueTagId(layers, "style")));
                int index = layers.arraySize;
                layers.arraySize += 2;
                layers.GetArrayElementAtIndex(index).boxedValue = group;
                layers.GetArrayElementAtIndex(index + 1).boxedValue = CreateDefaultLayerForAuthoring(TextStudio2DLayerKind.Fill).WithRelationships(group.ItemId);
                _expandedLayerId = group.ItemId.Value;
            });
        }

        VisualElement CreateActivationEditor(SerializedProperty property, int index)
        {
            TextStudio2DLayer layer = (TextStudio2DLayer)property.boxedValue;
            TextStudioAppearanceActivation activation = layer.AppearanceActivation;
            Foldout section = CreateGroupFoldout("Activation", "appearance-activation", true);
            EnumField mode = new EnumField("Activate", activation.Mode) { name = "appearance-activation-mode", tooltip = "Always contributes to the enclosing appearance. Tagged stays dormant until this style is invoked in markup." };
            mode.AddToClassList("unity-base-field__aligned");
            mode.AddToClassList("ts-appearance-responsive-field");
            bool topLevel = string.IsNullOrEmpty(layer.ParentId.Value);
            mode.SetEnabled(topLevel);
            mode.RegisterValueChangedCallback(evt => ChangeActivation(index, value => value.WithMode((TextStudioAppearanceActivationMode)evt.newValue)));
            section.Add(mode);
            if (!topLevel) section.Add(TextStudioInspectorElements.CreateMutedText("Tag activation is available on top-level groups. Nested groups remain part of their owning style."));
            if (activation.Mode != TextStudioAppearanceActivationMode.Tagged) return section;
            TextField id = new TextField("Tag ID") { name = "appearance-tag-id", value = activation.Id, isDelayed = true, tooltip = "A unique ID within this appearance. Invoke with <$id>text</$id>. IDs are case-insensitive." };
            id.AddToClassList("unity-base-field__aligned");
            id.AddToClassList("ts-appearance-responsive-field");
            Label validation = TextStudioInspectorElements.CreateMutedText(string.Empty);
            id.RegisterValueChangedCallback(evt =>
            {
                string proposed = evt.newValue.Trim();
                SerializedProperty layers = FindProperty("_layers");
                if (!TextStudioAppearanceTagSyntax.IsValidId(proposed) || UniqueTagId(layers, proposed, index) != proposed)
                {
                    validation.text = "Use a valid, unique local Tag ID. This definition keeps its previous ID.";
                    id.SetValueWithoutNotify(activation.Id);
                    return;
                }
                ChangeActivation(index, value => value.WithId(proposed));
            });
            section.Add(id);
            section.Add(validation);
            EnumField source = new EnumField("Content", activation.Source) { name = "appearance-definition-source", tooltip = "Use the authored group subtree or reference a reusable appearance asset." };
            source.AddToClassList("unity-base-field__aligned");
            source.AddToClassList("ts-appearance-responsive-field");
            source.RegisterValueChangedCallback(evt => ChangeActivation(index, value => value.WithSource((TextStudioAppearanceDefinitionSource)evt.newValue, value.Asset)));
            section.Add(source);
            if (activation.Source == TextStudioAppearanceDefinitionSource.Asset)
            {
                ObjectField asset = new ObjectField("Style Asset") { name = "appearance-definition-asset", objectType = typeof(TextStudio2DStyleAsset), allowSceneObjects = false, value = activation.Asset, tooltip = "This asset's appearance and nested definitions are available inside this invocation." };
                asset.AddToClassList("unity-base-field__aligned");
                asset.AddToClassList("ts-appearance-responsive-field");
                asset.RegisterValueChangedCallback(evt => ChangeActivation(index, value => value.WithSource(TextStudioAppearanceDefinitionSource.Asset, evt.newValue as TextStudio2DStyleAsset)));
                section.Add(asset);
                Toggle overrideMode = new Toggle("Override Mode") { value = activation.OverrideApplyMode, tooltip = "Use a local mode instead of the referenced asset's default." };
                overrideMode.AddToClassList("unity-base-field__aligned");
                overrideMode.RegisterValueChangedCallback(evt => ChangeActivation(index, value => value.WithApplyMode(value.ApplyMode, evt.newValue)));
                section.Add(overrideMode);
            }
            if (activation.Source == TextStudioAppearanceDefinitionSource.Inline || activation.OverrideApplyMode)
            {
                EnumField apply = new EnumField("Default Mode", activation.ApplyMode) { tooltip = "Replace substitutes enclosing appearance layers. Overlay composites the full style over them." };
                apply.AddToClassList("unity-base-field__aligned");
                apply.AddToClassList("ts-appearance-responsive-field");
                apply.RegisterValueChangedCallback(evt => ChangeActivation(index, value => value.WithApplyMode((TextStudioAppearanceApplyMode)evt.newValue)));
                section.Add(apply);
            }
            section.Add(TextStudioInspectorElements.CreateMutedText("Dormant until invoked. Apply Where can narrow this style after it becomes active."));
            Button copy = TextStudioInspectorElements.CreateButton("Copy Tag", "Copy this style's matched opening and closing tags.", () => EditorGUIUtility.systemCopyBuffer = TextStudioAppearanceAuthoringMetadata.CreateSnippet(activation.Id));
            Button insert = TextStudioInspectorElements.CreateButton("Insert Tag", "Wrap the current source selection, or insert an empty matched pair at the caret.", () => InsertAppearanceTag(activation.Id));
            insert.SetEnabled(Target is TextStudioText && TextStudioAppearanceTagSyntax.IsValidId(activation.Id));
            Button preview = TextStudioInspectorElements.CreateButton("Preview Style", "Preview this dormant definition without changing enabled or Solo flags.", () => TextStudioAppearancePreviewWindow.Open(ReadSettings(), activation.Id));
            Button save = TextStudioInspectorElements.CreateButton("Save as Asset", "Copy this definition with its stable identities and replace its local content source with the new asset in one Undo operation. Catalog registration remains explicit.", () => SaveInlineStyle(index));
            save.SetEnabled(activation.Source == TextStudioAppearanceDefinitionSource.Inline);
            section.Add(TextStudioInspectorElements.CreateButtonRow(copy, insert, preview, save));
            if (activation.Source == TextStudioAppearanceDefinitionSource.Inline)
                section.Add(TextStudioInspectorElements.CreateButton("Add Layer to This Style", "Add a treatment inside this tagged group.", () => ShowAddLayerMenuForGroup(layer.ItemId)));
            return section;
        }

        void ChangeActivation(int index, Func<TextStudioAppearanceActivation, TextStudioAppearanceActivation> change)
        {
            MutateLayers("Change Appearance Tag Definition", layers =>
            {
                SerializedProperty property = layers.GetArrayElementAtIndex(index);
                TextStudio2DLayer layer = (TextStudio2DLayer)property.boxedValue;
                TextStudioAppearanceActivation activation = change(layer.AppearanceActivation);
                if (activation.Mode == TextStudioAppearanceActivationMode.Tagged && string.IsNullOrEmpty(activation.Id)) activation = activation.WithId(UniqueTagId(layers, "style", index));
                property.boxedValue = layer.WithAppearanceActivation(activation);
            });
        }

        TextStudio2DStyleSettings ReadSettings()
        {
            if (Target is TextStudio2DStyleAsset asset) return asset.Settings;
            SerializedProperty settings = _serializedObject.FindProperty(_propertyRoot);
            return settings != null ? (TextStudio2DStyleSettings)settings.boxedValue : default;
        }

        void InsertAppearanceTag(string id)
        {
            TextField source = _root?.panel?.visualTree.Q<TextField>("ts-source-field-" + Target.GetStableId());
            if (source == null) return;
            TextStudioTagInsertionSnapshot snapshot = _appearanceInsertion?.Consume() ?? TextStudioTagInsertionSnapshot.Capture(source);
            TextStudioTagSourceEditor.EditResult edit = snapshot.CreateEdit(TextStudioAppearanceAuthoringMetadata.CreateSnippet(id));
            source.value = edit.Text;
            source.Focus();
            source.cursorIndex = edit.SelectionEnd;
            source.selectIndex = edit.SelectionStart;
        }

        void SaveInlineStyle(int index)
        {
            TextStudio2DStyleSettings settings = ReadSettings();
            TextStudio2DLayer group = settings.GetLayer(index);
            if (!TextStudioAppearanceDefinitions.Resolve(settings).TryGet(group.AppearanceActivation.Id, out TextStudioAppearanceDefinition definition) || !definition.IsValid) return;
            string path = EditorUtility.SaveFilePanelInProject("Save Appearance Style", group.AppearanceActivation.Id, "asset", "Choose where to save this inline definition.");
            if (string.IsNullOrEmpty(path)) return;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Save Inline Appearance as Asset");
            TextStudio2DStyleAsset asset = ScriptableObject.CreateInstance<TextStudio2DStyleAsset>();
            AssetDatabase.CreateAsset(asset, path);
            Undo.RegisterCreatedObjectUndo(asset, "Create Appearance Style Asset");
            ConvertInlineStyleToAsset(index, asset);
            Undo.CollapseUndoOperations(undoGroup);
            EditorGUIUtility.PingObject(asset);
        }

        internal bool ConvertInlineStyleToAsset(int index, TextStudio2DStyleAsset asset)
        {
            TextStudio2DStyleSettings settings = ReadSettings();
            TextStudio2DLayer group = settings.GetLayer(index);
            if (asset == null || group.AppearanceActivation.Source != TextStudioAppearanceDefinitionSource.Inline ||
                !TextStudioAppearanceDefinitions.Resolve(settings).TryGet(group.AppearanceActivation.Id, out TextStudioAppearanceDefinition definition) || !definition.IsValid) return false;
            TextStudio2DStyleSettings saved = definition.Settings;
            Undo.RecordObject(asset, "Save Inline Appearance as Asset");
            asset.ApplySettings(saved);
            asset.SetTagIdentity(definition.Id, definition.DefaultApplyMode);
            EditorUtility.SetDirty(asset);
            MutateLayers("Reference Saved Appearance Style", layers =>
            {
                if (TextStudioAppearanceDefinitions.IsIdentityContainer(group, saved.PainterOrder))
                {
                    layers.GetArrayElementAtIndex(index).boxedValue = group.WithAppearanceActivation(group.AppearanceActivation.WithSource(TextStudioAppearanceDefinitionSource.Asset, asset));
                    return;
                }
                // The saved asset already contains this group's semantics. A new neutral
                // tag owner keeps the original group and stable references as the inline form.
                TextStudioStackItemId wrapperId = TextStudioStackItemId.CreateUnique("style-reference");
                TextStudio2DLayer wrapper = TextStudio2DLayer.CreateGroup(group.DisplayName).WithIdentity(wrapperId).WithState(group.IsEnabled, group.IsSolo)
                    .WithAppearanceActivation(group.AppearanceActivation.WithSource(TextStudioAppearanceDefinitionSource.Asset, asset));
                TextStudio2DLayer retained = group.WithState(true).WithAppearanceActivation(default).WithRelationships(wrapperId, group.MaskId);
                layers.InsertArrayElementAtIndex(index);
                layers.GetArrayElementAtIndex(index).boxedValue = wrapper;
                layers.GetArrayElementAtIndex(index + 1).boxedValue = retained;
                _expandedLayerId = wrapperId.Value;
            });
            return true;
        }

        static readonly Dictionary<string, TextStudioAppearanceProperty> ExposablePaths = new Dictionary<string, TextStudioAppearanceProperty>
        {
            { "_opacity", TextStudioAppearanceProperty.Opacity }, { "_enabled", TextStudioAppearanceProperty.Enabled },
            { "_participatesInAnimatedTint", TextStudioAppearanceProperty.AnimatedTint }, { "_blendMode", TextStudioAppearanceProperty.BlendMode },
            { "_paint._color", TextStudioAppearanceProperty.PaintColor }, { "_paint._noiseSeed", TextStudioAppearanceProperty.NoiseSeed }, { "_stroke._width", TextStudioAppearanceProperty.StrokeWidth },
            { "_stroke._softness", TextStudioAppearanceProperty.StrokeSoftness }, { "_stroke._alignment", TextStudioAppearanceProperty.StrokeAlignment },
            { "_shadow._blur", TextStudioAppearanceProperty.ShadowBlur }, { "_shadow._spread", TextStudioAppearanceProperty.ShadowSpread },
            { "_glow._radius", TextStudioAppearanceProperty.GlowRadius }, { "_glow._spread", TextStudioAppearanceProperty.GlowSpread },
            { "_fill._dilation", TextStudioAppearanceProperty.FillDilation }, { "_fill._softness", TextStudioAppearanceProperty.FillSoftness }
        };

        void AddExposureActions(VisualElement card, SerializedProperty layerProperty, int index)
        {
            TextStudio2DLayer layer = (TextStudio2DLayer)layerProperty.boxedValue;
            card.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                VisualElement clicked = evt.target as VisualElement;
                PropertyField field = clicked as PropertyField ?? clicked?.GetFirstAncestorOfType<PropertyField>();
                if (field == null || !field.bindingPath.StartsWith(layerProperty.propertyPath + ".", StringComparison.Ordinal)) return;
                string relative = field.bindingPath.Substring(layerProperty.propertyPath.Length + 1);
                TextStudioStackItemId treatmentId = default;
                TextStudio2DLayer target = layer;
                int treatmentStart = relative.IndexOf("_surfaceTreatments.Array.data[", StringComparison.Ordinal);
                if (treatmentStart >= 0)
                {
                    int end = relative.IndexOf(']', treatmentStart);
                    SerializedProperty treatment = layerProperty.FindPropertyRelative(relative.Substring(0, end + 1));
                    treatmentId = TextStudioStackItemId.Create(GetItemId(treatment.FindPropertyRelative("_itemId")));
                    if (!TextStudioAppearanceProperties.TryGetTarget(layer, treatmentId, out target)) return;
                    relative = relative.Substring(end + 2);
                }
                if (relative == "_shadow._offset")
                {
                    evt.menu.AppendAction("Expose to Tag/Horizontal offset", _ => ExposeProperty(index, TextStudioAppearanceProperty.ShadowX, treatmentId: treatmentId));
                    evt.menu.AppendAction("Expose to Tag/Vertical offset", _ => ExposeProperty(index, TextStudioAppearanceProperty.ShadowY, treatmentId: treatmentId));
                }
                if (ExposablePaths.TryGetValue(relative, out TextStudioAppearanceProperty key) && TextStudioAppearanceProperties.IsSupported(target, key))
                    evt.menu.AppendAction("Expose to Tag", _ => ExposeProperty(index, key, treatmentId: treatmentId));
            }));
            Foldout exposed = CreateGroupFoldout("Tag Parameters", "tag-parameters", false);
            SerializedProperty exposures = FindExposureArray(index);
            if (exposures == null) return;
            for (int i = 0; i < exposures.arraySize; i++)
            {
                int exposureIndex = i;
                SerializedProperty bindingProperty = exposures.GetArrayElementAtIndex(i);
                TextStudioAppearanceExposure binding = (TextStudioAppearanceExposure)bindingProperty.boxedValue;
                if (binding.LayerId != layer.ItemId) continue;
                VisualElement row = new VisualElement();
                TextField name = new TextField("Name") { name = "exposure-name-" + binding.Property + "-" + binding.TreatmentId.Value + "-" + binding.Name, value = binding.Name, isDelayed = true, tooltip = "Named parameter accepted by this style's markup. Reserved names are ref, mode, tint and opacity." };
                name.AddToClassList("unity-base-field__aligned");
                name.AddToClassList("ts-appearance-responsive-field");
                name.RegisterValueChangedCallback(evt =>
                {
                    string next = evt.newValue.Trim();
                    if (!TextStudioAppearanceTagSyntax.IsValidId(next) || UniqueExposureName(exposures, next, exposureIndex) != next) { name.SetValueWithoutNotify(binding.Name); return; }
                    MutateLayers("Rename Exposed Appearance Parameter", _ => FindExposureArray(index).GetArrayElementAtIndex(exposureIndex).FindPropertyRelative("_name").stringValue = next, false);
                });
                row.Add(name);
                bool valid = TextStudioAppearanceProperties.TryGet(binding.Property, out TextStudioAppearancePropertyDescriptor descriptor) && TextStudioAppearanceProperties.TryGetTarget(layer, binding.TreatmentId, out TextStudio2DLayer bindingTarget) && TextStudioAppearanceProperties.TryRead(bindingTarget, binding.Property, out _);
                string value = string.Empty;
                if (valid && TextStudioAppearanceProperties.TryGetTarget(layer, binding.TreatmentId, out TextStudio2DLayer readTarget)) TextStudioAppearanceProperties.TryRead(readTarget, binding.Property, out value);
                row.Add(TextStudioInspectorElements.CreateMutedText(valid ? descriptor.Label + " · " + descriptor.ValueType + (string.IsNullOrEmpty(descriptor.Unit) ? "" : " (" + descriptor.Unit + ")") + " · Default: " + value : "Unavailable binding. Repair or remove this parameter."));
                row.Add(TextStudioInspectorElements.CreateButtonRow(
                    TextStudioInspectorElements.CreateButton("Repair Binding", "Choose a supported typed field without guessing another target.", () => ShowExposureMenu(index, exposureIndex)),
                    TextStudioInspectorElements.CreateButton("Remove", "Remove this exposed parameter; authored layer values stay unchanged.", () => MutateLayers("Remove Exposed Parameter", _ => FindExposureArray(index).DeleteArrayElementAtIndex(exposureIndex)))));
                exposed.Add(row);
            }
            exposed.Add(TextStudioInspectorElements.CreateButton("Expose Field to Tag", "Choose a supported color, number, boolean or enum field. Bindings use stable identities and survive reordering.", () => ShowExposureMenu(index)));
            card.Add(exposed);
        }

        SerializedProperty FindExposureArray(int layerIndex)
        {
            SerializedProperty layers = FindProperty("_layers");
            TextStudio2DLayer layer = (TextStudio2DLayer)layers.GetArrayElementAtIndex(layerIndex).boxedValue;
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            while (true)
            {
                if (layer.AppearanceActivation.Mode == TextStudioAppearanceActivationMode.Tagged)
                    return FindLayerById(layers, layer.ItemId.Value)?.FindPropertyRelative("_appearanceActivation._exposures");
                if (string.IsNullOrEmpty(layer.ParentId.Value) || !seen.Add(layer.ParentId.Value)) return FindProperty("_exposures");
                SerializedProperty parent = FindLayerById(layers, layer.ParentId.Value);
                if (parent == null) return FindProperty("_exposures");
                layer = (TextStudio2DLayer)parent.boxedValue;
            }
        }

        void ShowExposureMenu(int index, int replace = -1)
        {
            TextStudio2DLayer layer = (TextStudio2DLayer)FindProperty("_layers").GetArrayElementAtIndex(index).boxedValue;
            GenericMenu menu = new GenericMenu();
            foreach (TextStudioAppearancePropertyDescriptor descriptor in TextStudioAppearanceProperties.All)
            {
                TextStudioAppearanceProperty property = descriptor.Property;
                if (!TextStudioAppearanceProperties.IsSupported(layer, property)) continue;
                menu.AddItem(new GUIContent(descriptor.Label + " (" + descriptor.ValueType + ")"), false, () => ExposeProperty(index, property, replace));
            }
            for (int treatmentIndex = 0; treatmentIndex < layer.Decoration.TreatmentCount; treatmentIndex++)
            {
                TextStudio2DSurfaceTreatment treatment = layer.Decoration.GetTreatment(treatmentIndex);
                if (!TextStudioAppearanceProperties.TryGetTarget(layer, treatment.ItemId, out TextStudio2DLayer treatmentLayer)) continue;
                foreach (TextStudioAppearancePropertyDescriptor descriptor in TextStudioAppearanceProperties.All)
                {
                    TextStudioAppearanceProperty property = descriptor.Property;
                    if (!TextStudioAppearanceProperties.IsSupported(treatmentLayer, property)) continue;
                    TextStudioStackItemId treatmentId = treatment.ItemId;
                    menu.AddItem(new GUIContent(treatment.DisplayName + "/" + descriptor.Label + " (" + descriptor.ValueType + ")"), false, () => ExposeProperty(index, property, replace, treatmentId));
                }
            }
            menu.ShowAsContext();
        }

        void ExposeProperty(int index, TextStudioAppearanceProperty key, int replace = -1, TextStudioStackItemId treatmentId = default)
        {
            MutateLayers("Expose Appearance Field to Tag", layers =>
            {
                TextStudio2DLayer layer = (TextStudio2DLayer)layers.GetArrayElementAtIndex(index).boxedValue;
                SerializedProperty exposures = FindExposureArray(index);
                if (exposures == null) return;
                int target = replace >= 0 ? replace : exposures.arraySize++;
                string name = replace >= 0 ? ((TextStudioAppearanceExposure)exposures.GetArrayElementAtIndex(target).boxedValue).Name : UniqueExposureName(exposures, char.ToLowerInvariant(key.ToString()[0]) + key.ToString().Substring(1), target);
                exposures.GetArrayElementAtIndex(target).boxedValue = new TextStudioAppearanceExposure(name, layer.ItemId, key, treatmentId);
            });
        }
        int AddBrokenExposureBindings()
        {
            int count = 0;
            SerializedProperty layers = FindProperty("_layers");
            if (layers == null) return count;
            TextStudio2DStyleSettings settings = ReadSettings();
            TextStudio2DLayer[] effective = TextStudio2DStyleCompiler.FlattenAuthoredSettings(in settings).CloneAuthoredLayers();
            int[] domains = TextStudioAppearanceDomains.Build(effective);
            List<(SerializedProperty Property, TextStudioStackItemId Domain)> arrays = new List<(SerializedProperty, TextStudioStackItemId)>();
            SerializedProperty rootExposures = FindProperty("_exposures");
            if (rootExposures != null) arrays.Add((rootExposures, default));
            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty authored = layers.GetArrayElementAtIndex(i);
                TextStudio2DLayer layer = (TextStudio2DLayer)authored.boxedValue;
                if (!layer.IsTagDefinition || layer.AppearanceActivation.Source != TextStudioAppearanceDefinitionSource.Inline) continue;
                SerializedProperty local = authored.FindPropertyRelative("_appearanceActivation._exposures");
                if (local != null) arrays.Add((local, layer.ItemId));
            }
            foreach (var entry in arrays)
            {
                SerializedProperty array = entry.Property;
                bool InDomain(int index) => entry.Domain.IsValid ? domains[index] >= 0 && effective[domains[index]].ItemId.Equals(entry.Domain) : domains[index] < 0;
                for (int i = 0; i < array.arraySize; i++)
                {
                    TextStudioAppearanceExposure exposure = (TextStudioAppearanceExposure)array.GetArrayElementAtIndex(i).boxedValue;
                    bool valid = false;
                    for (int index = 0; index < effective.Length; index++)
                    {
                        TextStudio2DLayer owner = effective[index];
                        if (!InDomain(index) || !owner.ItemId.Equals(exposure.LayerId)) continue;
                        valid = TextStudioAppearanceProperties.TryGetTarget(owner, exposure.TreatmentId, out TextStudio2DLayer leaf) && TextStudioAppearanceProperties.IsSupported(leaf, exposure.Property);
                        break;
                    }
                    if (valid) continue;
                    count++;
                    string path = array.propertyPath;
                    int bindingIndex = i;
                    _validation.Add(TextStudioInspectorElements.CreateStatus("Parameter '" + exposure.Name + "' has an unavailable target. Its binding was retained for repair.", CommonInspectorStatusType.Warning));
                    _validation.Add(TextStudioInspectorElements.CreateButtonRow(
                        TextStudioInspectorElements.CreateButton("Repair " + exposure.Name, "Choose an explicit new typed target for this binding.", () =>
                        {
                            GenericMenu menu = new GenericMenu();
                            for (int index = 0; index < effective.Length; index++)
                            {
                                if (!InDomain(index)) continue;
                                TextStudio2DLayer layer = effective[index];
                                AddTarget(layer, default, layer.DisplayName);
                                for (int treatmentIndex = 0; treatmentIndex < layer.Decoration.TreatmentCount; treatmentIndex++)
                                {
                                    TextStudio2DSurfaceTreatment treatment = layer.Decoration.GetTreatment(treatmentIndex);
                                    if (TextStudioAppearanceProperties.TryGetTarget(layer, treatment.ItemId, out TextStudio2DLayer treatmentLayer))
                                        AddTarget(treatmentLayer, treatment.ItemId, layer.DisplayName + "/" + treatment.DisplayName);
                                }
                                void AddTarget(TextStudio2DLayer leaf, TextStudioStackItemId treatmentId, string label)
                                {
                                    foreach (TextStudioAppearancePropertyDescriptor descriptor in TextStudioAppearanceProperties.All)
                                    {
                                        if (!TextStudioAppearanceProperties.IsSupported(leaf, descriptor.Property)) continue;
                                        TextStudioStackItemId targetId = layer.ItemId;
                                        TextStudioAppearanceProperty property = descriptor.Property;
                                        menu.AddItem(new GUIContent(label + "/" + descriptor.Label + " (" + descriptor.ValueType + ")"), false, () => MutateLayers("Repair Appearance Parameter", _ =>
                                            _serializedObject.FindProperty(path).GetArrayElementAtIndex(bindingIndex).boxedValue = new TextStudioAppearanceExposure(exposure.Name, targetId, property, treatmentId)));
                                    }
                                }
                            }
                            menu.ShowAsContext();
                        }),
                        TextStudioInspectorElements.CreateButton("Remove " + exposure.Name, "Remove only this broken exposed parameter.", () => MutateLayers("Remove Broken Appearance Parameter", _ => _serializedObject.FindProperty(path).DeleteArrayElementAtIndex(bindingIndex)))));
                }
            }
            return count;
        }
    }
}
