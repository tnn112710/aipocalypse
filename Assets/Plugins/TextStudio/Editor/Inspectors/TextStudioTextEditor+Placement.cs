using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using TextStudio.BuiltIns;
using TMPro;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    internal partial class TextStudioTextEditor
    {
        const string SplinePackageName = "com.unity.splines";
        const string SplinePlacementProviderTypeName =
            "TextStudio.Splines.SplinePlacementProvider";
        static Type s_splinePlacementProviderType;
        static bool s_splinePlacementProviderTypeResolved;
        static readonly Vector3[] s_placementGuidePoints = new Vector3[65];
        static readonly PlacementMode[] s_placementModeOrder =
        {
            PlacementMode.Linear,
            PlacementMode.Arc,
            PlacementMode.Circle,
            PlacementMode.Curve,
            PlacementMode.Wave,
            PlacementMode.Grid,
            PlacementMode.Spline,
            PlacementMode.Extension
        };
        static readonly List<string> s_placementModeChoices = new List<string>
        {
            "Linear",
            "Arc",
            "Circle",
            "Curve",
            "Wave",
            "Grid",
            "Spline",
            "Extension"
        };

        VisualElement BuildUitkPlacementTab()
        {
            VisualElement root = new VisualElement();
            root.AddToClassList("ts-placement-tab");
            TextStudioText hub = target as TextStudioText;

            VisualElement placement = CreateSection("Placement");
            DropdownField placementModeField = CreatePlacementModeField();
            placement.Add(placementModeField);
            VisualElement linear = CreateAlignedHint(
                "Linear placement uses the active layout provider's positions unchanged.");
            VisualElement arc = BuildArcPlacementFields();
            VisualElement wave = BuildWavePlacementFields();
            VisualElement circle = BuildCirclePlacementFields();
            VisualElement grid = BuildGridPlacementFields();
            VisualElement curve = BuildCurvePlacementFields();
            VisualElement spline = BuildSplinePlacementFields(hub);
            VisualElement extension = BuildExtensionPlacementFields(hub);
            placement.Add(linear);
            placement.Add(arc);
            placement.Add(wave);
            placement.Add(circle);
            placement.Add(grid);
            placement.Add(curve);
            placement.Add(spline);
            placement.Add(extension);
            root.Add(placement);

            Foldout orientation = CreateFoldoutSection(
                "Orientation",
                "ts-placement-orientation-section",
                false);
            orientation.viewDataKey = "text-studio-placement-orientation";
            orientation.AddToClassList("ts-placement-orientation-section");
            AddRelativePropertyField(
                orientation,
                _placementRotation,
                "orientation",
                "Glyph Orientation");
            orientation.Add(CreateAlignedHint(
                "Placement Default keeps the active strategy's natural facing. Override it only when the design calls for path-following, inward, or outward glyphs."));
            Foldout orientationAdvanced = new Foldout
            {
                text = "Advanced",
                value = false,
                tooltip = "Show the optional local rotation correction."
            };
            AddRelativePropertyField(
                orientationAdvanced,
                _placementRotation,
                "fineRotation",
                "Fine Rotation");
            orientation.Add(orientationAdvanced);
            root.Add(orientation);

            root.Add(BuildSharedSpatialTargetSection());

            Action refreshPlacementMode = () =>
            {
                PlacementMode mode = (PlacementMode)_placementMode.intValue;
                int choiceIndex = Array.IndexOf(s_placementModeOrder, mode);
                placementModeField.showMixedValue = _placementMode.hasMultipleDifferentValues;
                placementModeField.SetValueWithoutNotify(
                    s_placementModeChoices[choiceIndex >= 0 ? choiceIndex : 0]);
                linear.style.display = mode == PlacementMode.Linear ? DisplayStyle.Flex : DisplayStyle.None;
                arc.style.display = mode == PlacementMode.Arc ? DisplayStyle.Flex : DisplayStyle.None;
                wave.style.display = mode == PlacementMode.Wave ? DisplayStyle.Flex : DisplayStyle.None;
                circle.style.display = mode == PlacementMode.Circle ? DisplayStyle.Flex : DisplayStyle.None;
                grid.style.display = mode == PlacementMode.Grid ? DisplayStyle.Flex : DisplayStyle.None;
                curve.style.display = mode == PlacementMode.Curve ? DisplayStyle.Flex : DisplayStyle.None;
                spline.style.display = mode == PlacementMode.Spline ? DisplayStyle.Flex : DisplayStyle.None;
                extension.style.display = mode == PlacementMode.Extension ? DisplayStyle.Flex : DisplayStyle.None;
            };
            root.TrackPropertyValue(_placementMode, _ => refreshPlacementMode());
            refreshPlacementMode();

            return root;
        }

        VisualElement BuildCurvePlacementFields()
        {
            VisualElement root = CreatePlacementModeGroup(
                "Bend selected text on a height profile while keeping its original spacing and line breaks.");
            root.name = "ts-curve-placement-settings";
            SerializedProperty source = _curvePlacement.FindPropertyRelative("_curve");
            SerializedProperty profile = source.FindPropertyRelative("_profile");
            SerializedProperty inline = source.FindPropertyRelative("_inline");
            SerializedProperty shape = inline.FindPropertyRelative("_curveType");
            SerializedProperty custom = inline.FindPropertyRelative("_customCurve");
            AddRelativePropertyField(root, source, "_profile", "Shared Profile");
            VisualElement inlineFields = new VisualElement();
            inlineFields.Add(CreateBoundPropertyField(shape, "Shape"));
            PropertyField customField = CreateBoundPropertyField(custom, "Custom Curve");
            inlineFields.Add(customField);
            root.Add(inlineFields);
            AddRelativePropertyField(root, _curvePlacement, "_height", "Height");
            root.Add(CreateAlignedHint("The curve input spans 0 to 1. Height uses this text renderer's local units."));
            Foldout selection = CreatePlacementAdvancedFoldout();
            selection.text = "Selection and Mapping";
            selection.value = true;
            AddRelativePropertyField(selection, _curvePlacement, "_mapping", "Mapping");
            selection.Add(TextStudioSelectionField.Create(_curvePlacement.FindPropertyRelative("_selection"), "Selection"));
            selection.Add(CreateAlignedHint("Ranges use exclusive ends. 0..5 selects the first five logical units. Within a phrase, the range starts at that phrase."));
            root.Add(selection);

            VisualElement actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.Add(new Button(MakeCurveLocal) { text = "Make Local", tooltip = "Copy the linked shape into this text, then unlink it." });
            actions.Add(new Button(SaveCurveProfile) { text = "Save as Profile", tooltip = "Create an independent reusable curve profile in the project." });
            Button sceneEdit = new Button(() =>
            {
                GameObject selectedObject = ((TextStudioText)target).gameObject;
                UnityEditor.Selection.activeGameObject = selectedObject;
                EditorApplication.delayCall += () =>
                {
                    if (selectedObject == null) return;
                    UnityEditor.Selection.activeGameObject = selectedObject;
                    ToolManager.SetActiveTool<TextStudioCurvePlacementTool>();
                    SceneView.lastActiveSceneView?.Focus();
                };
            }) { text = "Edit Curve in Scene", tooltip = "Adjust custom curve knots and tangents on the selected text in Scene view." };
            sceneEdit.SetEnabled(targets.Length == 1);
            root.Add(sceneEdit);
            root.Add(actions);
            void Refresh()
            {
                serializedObject.UpdateIfRequiredOrScript();
                bool linked = profile.objectReferenceValue != null;
                inlineFields.style.display = linked ? DisplayStyle.None : DisplayStyle.Flex;
                customField.style.display = !linked && shape.intValue == (int)CurveType.Custom
                    ? DisplayStyle.Flex : DisplayStyle.None;
                actions.Q<Button>().SetEnabled(linked);
            }
            root.TrackPropertyValue(profile, _ => Refresh());
            root.TrackPropertyValue(shape, _ => Refresh());
            Refresh();
            return root;
        }

        DropdownField CreatePlacementModeField()
        {
            PlacementMode current = (PlacementMode)_placementMode.intValue;
            int selected = Array.IndexOf(s_placementModeOrder, current);
            DropdownField field = new DropdownField(
                "Placement Mode",
                s_placementModeChoices,
                selected >= 0 ? selected : 0)
            {
                name = BuildPropertyFieldName(_placementMode),
                tooltip = "Choose how Text Studio positions glyphs."
            };
            field.AddToClassList("ts-property-field");
            field.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            field.showMixedValue = _placementMode.hasMultipleDifferentValues;
            field.RegisterValueChangedCallback(evt =>
            {
                int index = s_placementModeChoices.IndexOf(evt.newValue);
                if (index < 0 || index >= s_placementModeOrder.Length)
                    return;

                serializedObject.UpdateIfRequiredOrScript();
                _placementMode.intValue = (int)s_placementModeOrder[index];
                serializedObject.ApplyModifiedProperties();
            });
            return field;
        }

        void MakeCurveLocal()
        {
            foreach (Object selected in targets)
            {
                if (selected is not TextStudioText hub) continue;
                Undo.RecordObject(hub, "Make Curve Local");
                hub.MakeCurvePlacementLocal();
                PrefabUtility.RecordPrefabInstancePropertyModifications(hub);
                EditorUtility.SetDirty(hub);
            }
            serializedObject.Update();
        }

        void SaveCurveProfile()
        {
            if (targets.Length != 1 || target is not TextStudioText hub) return;
            serializedObject.Update();
            CurvePlacement settings = _curvePlacement.boxedValue as CurvePlacement;
            if (settings == null) return;
            string path = EditorUtility.SaveFilePanelInProject("Save Curve Profile", "New Curve", "asset",
                "Choose where to save this reusable curve shape.");
            if (string.IsNullOrEmpty(path)) return;
            TextStudioCurveProfile profile = ScriptableObject.CreateInstance<TextStudioCurveProfile>();
            profile.SetDefinition(settings.Curve.Definition);
            AssetDatabase.CreateAsset(profile, path);
            Undo.RegisterCreatedObjectUndo(profile, "Save Curve Profile");
            Undo.RecordObject(hub, "Link Curve Profile");
            hub.ConfigureCurvePlacement(profile, settings.Height, settings.Selection, settings.Mapping);
            PrefabUtility.RecordPrefabInstancePropertyModifications(hub);
            EditorUtility.SetDirty(hub);
            serializedObject.Update();
        }

        VisualElement BuildSplinePlacementFields(TextStudioText hub)
        {
            VisualElement root = CreatePlacementModeGroup(
                "Place glyphs along a Unity Spline while preserving Text Studio effects, reveal, and path rotation.");
            root.name = "ts-spline-placement-settings";

            MonoBehaviour provider = FindSplinePlacementProvider(hub);
            if (provider != null)
            {
                bool available = provider.isActiveAndEnabled;
                SerializedObject providerObject = new SerializedObject(provider);
                SerializedProperty container =
                    providerObject.FindProperty("_splineContainer");
                bool needsContainer =
                    available &&
                    container != null &&
                    container.objectReferenceValue == null;
                string message;
                HelpBoxMessageType messageType;
                if (!available)
                {
                    message =
                        "The Spline Placement Provider is attached but disabled. Enable it to use Spline placement.";
                    messageType = HelpBoxMessageType.Warning;
                }
                else if (needsContainer)
                {
                    message =
                        "Unity Splines is installed and the placement provider is active. Assign a Spline Container on the provider to complete setup.";
                    messageType = HelpBoxMessageType.Warning;
                }
                else
                {
                    message =
                        "Unity Splines is installed and the Spline Placement Provider is active.";
                    messageType = HelpBoxMessageType.Info;
                }

                HelpBox status = CreateHelpBox(message, messageType);
                status.name = "ts-spline-placement-status";
                root.Add(status);

                VisualElement actions = TextStudioInspectorElements.CreateButtonRow();
                if (!available)
                {
                    Button enable = new Button(() =>
                    {
                        Undo.RecordObject(provider, "Enable Spline Placement");
                        provider.enabled = true;
                        EditorUtility.SetDirty(provider);
                        hub.DiscoverRenderersAndProviders();
                        BuildUitkActiveTab(hub);
                    })
                    {
                        name = "ts-spline-placement-enable",
                        text = "Enable Provider",
                        tooltip = "Enable the attached Spline Placement Provider and reconnect it to this Text Studio object."
                    };
                    actions.Add(enable);
                }

                Button select = new Button(() => Selection.activeObject = provider)
                {
                    name = "ts-spline-placement-select",
                    text = "Select Provider",
                    tooltip = "Select the Spline Placement Provider to assign its container, spline index, and spacing."
                };
                actions.Add(select);
                root.Add(actions);
                return root;
            }

            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForPackageName(
                    SplinePackageName);
            if (package == null)
            {
                HelpBox missing = CreateHelpBox(
                    "Unity Splines is not installed. Install com.unity.splines, then return here to add Text Studio's Spline Placement Provider.",
                    HelpBoxMessageType.Warning);
                missing.name = "ts-spline-placement-status";
                root.Add(missing);
                return root;
            }

            Type providerType = ResolveSplinePlacementProviderType();
            if (providerType == null)
            {
                HelpBox unavailable = CreateHelpBox(
                    "Unity Splines " + package.version +
                    " is installed, but Text Studio's spline integration assembly is unavailable. Check the Console for compile errors.",
                    HelpBoxMessageType.Error);
                unavailable.name = "ts-spline-placement-status";
                root.Add(unavailable);
                return root;
            }

            HelpBox readyToAdd = CreateHelpBox(
                "Unity Splines " + package.version +
                " is installed. Add the Spline Placement Provider to connect this Text Studio object.",
                HelpBoxMessageType.Info);
            readyToAdd.name = "ts-spline-placement-status";
            root.Add(readyToAdd);

            Button add = new Button(() =>
            {
                MonoBehaviour existing = FindSplinePlacementProvider(hub);
                if (existing == null)
                {
                    Component created = Undo.AddComponent(
                        hub.gameObject,
                        providerType);
                    EditorUtility.SetDirty(hub.gameObject);
                    hub.DiscoverRenderersAndProviders();
                    if (created != null)
                        Selection.activeObject = hub;
                }
                BuildUitkActiveTab(hub);
            })
            {
                name = "ts-spline-placement-add",
                text = "Add Spline Placement Provider",
                tooltip = "Add and connect Text Studio's optional Unity Splines placement component."
            };
            root.Add(TextStudioInspectorElements.CreateButtonRow(add));
            return root;
        }

        static MonoBehaviour FindSplinePlacementProvider(TextStudioText hub)
        {
            if (hub == null)
                return null;

            MonoBehaviour[] components = hub.GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is IPlacementStrategy strategy &&
                    strategy.Mode == PlacementMode.Spline)
                {
                    return components[i];
                }
            }

            return null;
        }

        static Type ResolveSplinePlacementProviderType()
        {
            if (s_splinePlacementProviderTypeResolved)
                return s_splinePlacementProviderType;

            s_splinePlacementProviderTypeResolved = true;
            foreach (Type candidate in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
            {
                if (candidate != null &&
                    string.Equals(
                        candidate.FullName,
                        SplinePlacementProviderTypeName,
                        StringComparison.Ordinal))
                {
                    s_splinePlacementProviderType = candidate;
                    break;
                }
            }

            return s_splinePlacementProviderType;
        }

        VisualElement BuildExtensionPlacementFields(TextStudioText hub)
        {
            VisualElement root = CreatePlacementModeGroup(
                "Placement extensions use the same glyph placement, orientation, effects, and preview pipeline as Text Studio's built-in modes.");
            List<ITextStudioPlacementExtension> extensions = new List<ITextStudioPlacementExtension>();
            IReadOnlyList<IPlacementStrategy> strategies = hub.PlacementStrategies;
            for (int i = 0; i < strategies.Count; i++)
            {
                IPlacementStrategy strategy = strategies[i];
                if (strategy == null)
                    continue;
                if (strategy is Object unityObject && unityObject == null)
                    continue;
                if (strategy is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                    continue;
                if (strategy is ITextStudioPlacementExtension extension)
                    extensions.Add(extension);
            }

            if (extensions.Count == 0)
            {
                root.Add(CreateHelpBox(
                    "No placement extension is attached. Add a placement component such as Spatial 3D, then rescan the hub.",
                    HelpBoxMessageType.Warning));
                root.Add(new Button(() =>
                {
                    hub.DiscoverRenderersAndProviders();
                    BuildUitkActiveTab(hub);
                })
                {
                    text = "Rescan Components",
                    tooltip = "Discover newly attached renderer, layout, and placement components."
                });
                return root;
            }

            List<string> choices = new List<string>(extensions.Count);
            int selectedIndex = 0;
            for (int i = 0; i < extensions.Count; i++)
            {
                ITextStudioPlacementExtension extension = extensions[i];
                string dimension = extension.Dimension == TextStudioPlacementDimension.Spatial ? "3D" : "2D";
                choices.Add(extension.DisplayName + " (" + dimension + ")");
                if (string.Equals(extension.PlacementId, hub.ExtensionPlacementId, StringComparison.Ordinal))
                    selectedIndex = i;
            }

            PopupField<string> chooser = new PopupField<string>("Placement", choices, selectedIndex)
            {
                tooltip = "Select an attached placement extension. Its output remains compatible with shared Text Studio effects."
            };
            Label description = new Label();
            description.AddToClassList("ts-muted-text");
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.minWidth = 0f;
            Button select = new Button
            {
                text = "Select Placement Component",
                tooltip = "Select the component that owns the active placement's specialized settings."
            };

            Action<int> sync = index =>
            {
                int safeIndex = Mathf.Clamp(index, 0, extensions.Count - 1);
                ITextStudioPlacementExtension extension = extensions[safeIndex];
                description.text = extension.Description;
                select.clicked -= SelectActiveComponent;
                select.userData = extension;
                select.clicked += SelectActiveComponent;
            };

            void SelectActiveComponent()
            {
                if (select.userData is Object component)
                    Selection.activeObject = component;
            }

            chooser.RegisterValueChangedCallback(evt =>
            {
                int index = choices.IndexOf(evt.newValue);
                if (index < 0) return;
                Undo.RecordObject(hub, "Select Text Studio Placement");
                hub.SelectPlacementExtension(extensions[index].PlacementId);
                EditorUtility.SetDirty(hub);
                if (_extensionPlacementId != null)
                {
                    serializedObject.Update();
                    _extensionPlacementId.stringValue = extensions[index].PlacementId;
                    serializedObject.ApplyModifiedProperties();
                }
                sync(index);
            });

            root.Add(chooser);
            root.Add(description);
            root.Add(select);
            sync(selectedIndex);
            return root;
        }

        VisualElement BuildFrameTimingSettings()
        {
            VisualElement timing = CreateSection("Update Timing");
            timing.Add(CreateAlignedHint(
                "Clock source and update loop used by typewriter playback, effects, actions, and inspector previews.",
                "ts-update-timing-description"));
            AddRelativePropertyField(timing, _timing, "_clockMode", "Clock Source");
            AddRelativePropertyField(timing, _timing, "_updatePhase", "Update Phase");
            AddRelativePropertyField(timing, _timing, "_maxDeltaTime", "Max Delta Time");
            AddRelativePropertyField(timing, _timing, "_tickInEditMode", "Tick In Edit Mode");
            AddRelativePropertyField(timing, _timing, "_timeScale", "Time Scale");
            return timing;
        }

        VisualElement BuildArcPlacementFields()
        {
            VisualElement root = CreatePlacementModeGroup(
                "Arc bends each rendered line along a partial circle. Auto Fit keeps glyphs readable when the authored radius is too tight.");
            AddPlacementPresetPicker(
                root,
                new[] { "Balanced Arch", "Follow Arc", "Spiral Fan" },
                ApplyArcPreset);
            AddRelativePropertyField(root, _arcPlacement, "autoFitRadius", "Auto Fit Radius");
            VisualElement radius = AddRelativePropertyField(root, _arcPlacement, "radius", "Radius");
            VisualElement padding = AddRelativePropertyField(root, _arcPlacement, "autoFitPadding", "Auto Fit Padding");
            AddRelativePropertyField(root, _arcPlacement, "arcAngle", "Arc Angle");
            Foldout advanced = CreatePlacementAdvancedFoldout();
            AddRelativePropertyField(advanced, _arcPlacement, "pathPosition", "Path Position");
            AddRelativePropertyField(advanced, _arcPlacement, "startAngle", "Start Angle");
            AddRelativePropertyField(advanced, _arcPlacement, "spacing", "Spacing");
            VisualElement fill =
                AddRelativePropertyField(advanced, _arcPlacement, "fill", "Fill");
            AddRelativePropertyField(advanced, _arcPlacement, "reverse", "Reverse");
            AddRelativePropertyField(advanced, _arcPlacement, "radiusChange", "Radius Change");
            root.Add(advanced);
            ScheduleAutoFitRadiusDisplay(root, _arcPlacement, radius, padding);
            ScheduleArcFillDisplay(root, _arcPlacement, fill);
            return root;
        }

        Foldout BuildSharedSpatialTargetSection()
        {
            Foldout section = CreateFoldoutSection(
                "Shared Spatial Target",
                "ts-placement-shared-target-section",
                false);
            section.viewDataKey = "text-studio-placement-spatial-effects";
            section.tooltip =
                "Assign one optional moving Transform for Target Distance fields, effects that read it directly such as Lens Magnifier, and installed renderer interactions.";

            PropertyField targetField = CreateBoundPropertyField(
                _influenceTarget,
                "Target");
            targetField.tooltip =
                "Optional moving Transform used by Target Distance fields, effects that read it directly such as Lens Magnifier, and by default Text Studio 3D interactions.";
            section.Add(targetField);

            Label status = TextStudioInspectorElements.CreateStatus(
                string.Empty,
                CommonInspectorStatusType.Info,
                "ts-placement-shared-target-status");
            section.Add(status);
            Label consumers = TextStudioInspectorElements.CreateMutedText(
                string.Empty,
                "ts-placement-shared-target-consumers");
            section.Add(consumers);
            Label nextStep = TextStudioInspectorElements.CreateMutedText(
                string.Empty,
                "ts-placement-shared-target-next-step");
            section.Add(nextStep);
            section.Add(TextStudioInspectorElements.CreateMutedText(
                "Renderer interactions can also reuse this Target when their own Target is empty.",
                "ts-placement-shared-target-interaction-guidance"));

            Button createEffect = TextStudioInspectorElements.CreateButton(
                "Create Target Effect...",
                "Create and register a Composed Effect with a visible Position layer already scoped by Target Distance.",
                TextStudioSpatialTargetEffectUtility.CreateAndRegister);
            createEffect.name = "ts-placement-create-target-effect";
            Button browseEffects = TextStudioInspectorElements.CreateButton(
                "Browse Target Effects...",
                "Choose from Composed Effects that already use the Target, through a Target Distance field or a layer that reads it directly.",
                TextStudioSpatialTargetEffectUtility.ShowBrowser);
            browseEffects.name = "ts-placement-browse-target-effects";
            section.Add(TextStudioInspectorElements.CreateButtonRow(
                createEffect,
                browseEffects));

            Action refresh = () =>
            {
                if (_influenceTarget == null ||
                    _markup == null ||
                    _influenceTarget.serializedObject == null)
                    return;

                _influenceTarget.serializedObject.UpdateIfRequiredOrScript();
                if (_influenceTarget.hasMultipleDifferentValues ||
                    _markup.hasMultipleDifferentValues)
                {
                    TextStudioInspectorElements.SetStatus(
                        status,
                        "The selected Text Studio components use different content or target assignments.",
                        CommonInspectorStatusType.Info);
                    consumers.text =
                        "Content tags using Target: Varies across selection";
                    nextStep.text =
                        "Inspect one Text Studio component to see its exact consumers.";
                    return;
                }

                bool assigned = _influenceTarget.objectReferenceValue != null;
                TextStudioSpatialTargetConsumerReport report =
                    TextStudioSpatialTargetEffectUtility.AnalyzeContent(
                        _markup.stringValue);
                consumers.text = report.HasConsumers
                    ? "Content tags using Target: " + report.TagList
                    : "Content tags using Target: None";
                string rangeCount = report.TaggedRangeCount == 1
                    ? "1 tagged range"
                    : report.TaggedRangeCount + " tagged ranges";
                TextStudioInspectorElements.SetStatus(
                    status,
                    report.HasConsumers
                        ? assigned
                            ? "Target connected to " + rangeCount + "."
                            : rangeCount +
                              " can use a target, but no Target is assigned."
                        : assigned
                            ? "Target assigned, but no current content tag uses it."
                            : "No current content tag uses a Target.",
                    report.HasConsumers
                        ? assigned
                            ? CommonInspectorStatusType.Success
                            : CommonInspectorStatusType.Warning
                        : assigned
                            ? CommonInspectorStatusType.Warning
                            : CommonInspectorStatusType.Info);
                nextStep.text = report.HasConsumers
                    ? assigned
                        ? report.TargetFields.Count > 0
                            ? "Select this text or its Target to show the Scene radius. Move that radius over tagged glyphs, or increase Radius on the effect."
                            : "Move the Target over the tagged glyphs to see them respond."
                        : "Assign a Transform above to drive these tagged ranges."
                    : "Add a target-aware effect tag around the text you want it to control.";
            };
            section.TrackPropertyValue(_influenceTarget, _ => refresh());
            section.TrackPropertyValue(_markup, _ => refresh());
            refresh();
            return section;
        }

        VisualElement BuildWavePlacementFields()
        {
            VisualElement root = CreatePlacementModeGroup(
                "Wave offsets glyphs along a sine curve while preserving the normal text advance.");
            AddPlacementPresetPicker(
                root,
                new[] { "Calm Wave", "Follow Slope" },
                ApplyWavePreset);
            AddRelativePropertyField(root, _wavePlacement, "amplitude", "Amplitude");
            AddRelativePropertyField(root, _wavePlacement, "frequency", "Frequency");
            AddRelativePropertyField(root, _wavePlacement, "speed", "Speed");
            Foldout advanced = CreatePlacementAdvancedFoldout();
            AddRelativePropertyField(advanced, _wavePlacement, "phase", "Phase");
            AddRelativePropertyField(advanced, _wavePlacement, "depthAmplitude", "Depth Amplitude");
            AddRelativePropertyField(advanced, _wavePlacement, "spinSpeed", "Spin Speed");
            AddRelativePropertyField(advanced, _wavePlacement, "spinStagger", "Spin Stagger");
            root.Add(advanced);
            return root;
        }

        VisualElement BuildCirclePlacementFields()
        {
            VisualElement root = CreatePlacementModeGroup(
                "Circle distributes each rendered line around a full circle.");
            AddPlacementPresetPicker(
                root,
                new[]
                {
                    "Upright Ring",
                    "Follow Ring",
                    "Outward Carousel",
                    "Inward Carousel",
                    "Spiral Seal"
                },
                ApplyCirclePreset);
            AddRelativePropertyField(root, _circlePlacement, "autoFitRadius", "Auto Fit Radius");
            VisualElement radius = AddRelativePropertyField(root, _circlePlacement, "radius", "Radius");
            VisualElement padding = AddRelativePropertyField(root, _circlePlacement, "autoFitPadding", "Auto Fit Padding");
            AddRelativePropertyField(root, _circlePlacement, "startAngle", "Start Angle");
            Foldout advanced = CreatePlacementAdvancedFoldout();
            AddRelativePropertyField(advanced, _circlePlacement, "pathPosition", "Path Position");
            AddRelativePropertyField(advanced, _circlePlacement, "spacing", "Spacing");
            AddRelativePropertyField(advanced, _circlePlacement, "reverse", "Reverse");
            AddRelativePropertyField(advanced, _circlePlacement, "radiusChange", "Radius Change");
            root.Add(advanced);
            ScheduleAutoFitRadiusDisplay(root, _circlePlacement, radius, padding);
            return root;
        }

        VisualElement BuildGridPlacementFields()
        {
            VisualElement root = CreatePlacementModeGroup(
                "Grid places glyphs in bounds-aware cells by default, with fixed dimensions available for mechanical layouts.");
            AddPlacementPresetPicker(
                root,
                new[] { "Auto Typesetter", "Mechanical Board", "Column Flow" },
                ApplyGridPreset);
            AddRelativePropertyField(root, _gridPlacement, "columns", "Columns");
            AddRelativePropertyField(
                root,
                _gridPlacement,
                "cellSizing",
                "Cell Sizing");
            VisualElement cellSize = AddRelativePropertyField(
                root,
                _gridPlacement,
                "cellSize",
                "Cell Size");
            AddRelativePropertyField(root, _gridPlacement, "gap", "Gap");
            Foldout advanced = CreatePlacementAdvancedFoldout();
            AddRelativePropertyField(advanced, _gridPlacement, "flow", "Flow");
            AddRelativePropertyField(advanced, _gridPlacement, "reverse", "Reverse");
            VisualElement respectSourceLines = AddRelativePropertyField(
                advanced,
                _gridPlacement,
                "respectSourceLines",
                "Respect Source Lines");
            root.Add(advanced);

            SerializedProperty sizingProperty =
                _gridPlacement.FindPropertyRelative("cellSizing");
            SerializedProperty flowProperty =
                _gridPlacement.FindPropertyRelative("flow");
            Action refresh = () =>
            {
                GridCellSizing cellSizing = sizingProperty != null
                    ? (GridCellSizing)sizingProperty.enumValueIndex
                    : GridCellSizing.Auto;
                bool fixedSizing =
                    sizingProperty != null &&
                    TextStudioPlacementInspector.IsGridCellSizeVisible(
                        cellSizing,
                        sizingProperty.hasMultipleDifferentValues);
                cellSize.style.display =
                    fixedSizing ? DisplayStyle.Flex : DisplayStyle.None;
                GridFlow flow = flowProperty != null
                    ? (GridFlow)flowProperty.enumValueIndex
                    : GridFlow.RowsFirst;
                bool sourceLinesApplicable =
                    flowProperty != null &&
                    TextStudioPlacementInspector.IsGridRespectSourceLinesVisible(
                        flow,
                        flowProperty.hasMultipleDifferentValues);
                respectSourceLines.style.display = sourceLinesApplicable
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            };
            if (sizingProperty != null)
                root.TrackPropertyValue(sizingProperty, _ => refresh());
            if (flowProperty != null)
                root.TrackPropertyValue(flowProperty, _ => refresh());
            refresh();
            return root;
        }

        static VisualElement CreatePlacementModeGroup(string helpText)
        {
            VisualElement root = new VisualElement();
            root.AddToClassList("ts-property-group");
            root.AddToClassList("ts-placement-mode-group");
            root.Add(CreateAlignedHint(helpText));
            return root;
        }

        static Foldout CreatePlacementAdvancedFoldout()
        {
            return new Foldout
            {
                text = "Advanced",
                value = false,
                tooltip = "Show optional placement controls."
            };
        }

        void AddPlacementPresetPicker(
            VisualElement root,
            string[] names,
            Action<int> applyPreset)
        {
            PopupField<string> picker = new PopupField<string>(
                new List<string>(names),
                0)
            {
                tooltip = "Choose a starting point. Applying it changes ordinary editable settings."
            };
            picker.style.flexGrow = 1f;
            picker.style.minWidth = 0f;
            Button apply = new Button(() =>
            {
                int presetIndex = Array.IndexOf(names, picker.value);
                if (presetIndex < 0)
                    return;

                Undo.RecordObjects(targets, "Apply Placement Quick Start");
                serializedObject.Update();
                applyPreset(presetIndex);
                serializedObject.ApplyModifiedProperties();
                for (int i = 0; i < targets.Length; i++)
                {
                    if (targets[i] is TextStudioText hub)
                    {
                        hub.SchedulePlacementRefresh();
                        EditorUtility.SetDirty(hub);
                    }
                }
            })
            {
                text = "Apply",
                tooltip = "Apply the selected quick start without creating or linking an asset."
            };
            root.Add(TextStudioInspectorElements.CreateAlignedControlRow(
                "Quick Start",
                picker,
                "ts-placement-quick-start-row",
                apply));
        }

        void ApplyArcPreset(int preset)
        {
            SetBool(_arcPlacement, "reverse", false);
            SetFloat(_arcPlacement, "radiusChange", 0f);
            SetEnum(_arcPlacement, "pathPosition", (int)PathPosition.Above);
            SetEnum(_arcPlacement, "spacing", (int)PathSpacing.Even);
            SetFloat(_arcPlacement, "fill", 1f);
            switch (preset)
            {
                case 1:
                    SetFloat(_arcPlacement, "arcAngle", 160f);
                    SetFloat(_arcPlacement, "startAngle", -80f);
                    SetOrientation(TextStudioPlacementOrientation.FollowPath);
                    break;
                case 2:
                    SetFloat(_arcPlacement, "arcAngle", 220f);
                    SetFloat(_arcPlacement, "startAngle", -110f);
                    SetFloat(
                        _arcPlacement,
                        "radiusChange",
                        Mathf.Max(0.25f, ReadFloat(_arcPlacement, "radius") * 0.35f));
                    SetOrientation(TextStudioPlacementOrientation.FollowPath);
                    break;
                default:
                    SetFloat(_arcPlacement, "arcAngle", 160f);
                    SetFloat(_arcPlacement, "startAngle", -80f);
                    SetOrientation(TextStudioPlacementOrientation.PlacementDefault);
                    break;
            }
        }

        void ApplyWavePreset(int preset)
        {
            SetFloat(_wavePlacement, "frequency", preset == 1 ? 1.2f : 0.75f);
            SetFloat(_wavePlacement, "speed", preset == 1 ? 0f : 0.35f);
            SetFloat(_wavePlacement, "phase", 0f);
            SetFloat(_wavePlacement, "depthAmplitude", 0f);
            SetFloat(_wavePlacement, "spinSpeed", 0f);
            SetFloat(_wavePlacement, "spinStagger", 0f);
            SetOrientation(
                preset == 1
                    ? TextStudioPlacementOrientation.FollowPath
                    : TextStudioPlacementOrientation.PlacementDefault);
        }

        void ApplyCirclePreset(int preset)
        {
            SetEnum(_circlePlacement, "spacing", (int)PathSpacing.Even);
            SetBool(_circlePlacement, "reverse", false);
            SetFloat(_circlePlacement, "radiusChange", 0f);
            SetEnum(_circlePlacement, "pathPosition", (int)PathPosition.Above);
            switch (preset)
            {
                case 1:
                    SetOrientation(TextStudioPlacementOrientation.FollowPath);
                    break;
                case 2:
                    SetOrientation(TextStudioPlacementOrientation.FaceOutward);
                    break;
                case 3:
                    SetOrientation(TextStudioPlacementOrientation.FaceInward);
                    break;
                case 4:
                    SetEnum(_circlePlacement, "spacing", (int)PathSpacing.Proportional);
                    SetFloat(
                        _circlePlacement,
                        "radiusChange",
                        Mathf.Max(0.25f, ReadFloat(_circlePlacement, "radius") * 0.45f));
                    SetOrientation(TextStudioPlacementOrientation.FaceOutward);
                    break;
                default:
                    SetOrientation(TextStudioPlacementOrientation.PlacementDefault);
                    break;
            }
        }

        void ApplyGridPreset(int preset)
        {
            SetBool(_gridPlacement, "reverse", false);
            SetBool(_gridPlacement, "respectSourceLines", false);
            SetVector2(_gridPlacement, "gap", Vector2.zero);
            switch (preset)
            {
                case 1:
                    SetEnum(_gridPlacement, "cellSizing", (int)GridCellSizing.Fixed);
                    SetEnum(_gridPlacement, "flow", (int)GridFlow.RowsFirst);
                    break;
                case 2:
                    SetEnum(_gridPlacement, "cellSizing", (int)GridCellSizing.Auto);
                    SetEnum(_gridPlacement, "flow", (int)GridFlow.ColumnsFirst);
                    break;
                default:
                    SetEnum(_gridPlacement, "cellSizing", (int)GridCellSizing.Auto);
                    SetEnum(_gridPlacement, "flow", (int)GridFlow.RowsFirst);
                    SetBool(_gridPlacement, "respectSourceLines", true);
                    break;
            }
            SetOrientation(TextStudioPlacementOrientation.PlacementDefault);
        }

        void SetOrientation(TextStudioPlacementOrientation orientation)
        {
            SetEnum(_placementRotation, "orientation", (int)orientation);
            SetVector3(_placementRotation, "fineRotation", Vector3.zero);
        }

        static float ReadFloat(SerializedProperty parent, string name)
        {
            SerializedProperty property = parent?.FindPropertyRelative(name);
            return property != null ? property.floatValue : 0f;
        }

        static void SetFloat(SerializedProperty parent, string name, float value)
        {
            SerializedProperty property = parent?.FindPropertyRelative(name);
            if (property != null)
                property.floatValue = value;
        }

        static void SetBool(SerializedProperty parent, string name, bool value)
        {
            SerializedProperty property = parent?.FindPropertyRelative(name);
            if (property != null)
                property.boolValue = value;
        }

        static void SetEnum(SerializedProperty parent, string name, int value)
        {
            SerializedProperty property = parent?.FindPropertyRelative(name);
            if (property != null)
                property.enumValueIndex = value;
        }

        static void SetVector2(SerializedProperty parent, string name, Vector2 value)
        {
            SerializedProperty property = parent?.FindPropertyRelative(name);
            if (property != null)
                property.vector2Value = value;
        }

        static void SetVector3(SerializedProperty parent, string name, Vector3 value)
        {
            SerializedProperty property = parent?.FindPropertyRelative(name);
            if (property != null)
                property.vector3Value = value;
        }

        VisualElement AddRelativePropertyField(VisualElement parent, SerializedProperty property, string relativeName, string label)
        {
            SerializedProperty relative = property != null ? property.FindPropertyRelative(relativeName) : null;
            if (relative == null)
            {
                HelpBox missing = CreateHelpBox("Missing internal field: " + relativeName, HelpBoxMessageType.Warning);
                parent.Add(missing);
                return missing;
            }

            PropertyField field = CreateBoundPropertyField(relative, label);
            parent.Add(field);
            return field;
        }

        void ScheduleAutoFitRadiusDisplay(
            VisualElement root,
            SerializedProperty placement,
            VisualElement radius,
            VisualElement padding)
        {
            SerializedProperty autoFit = placement != null ? placement.FindPropertyRelative("autoFitRadius") : null;
            Action refresh = () =>
            {
                bool useAutoFit = autoFit != null && autoFit.boolValue;
                bool hasMixedValues =
                    autoFit != null && autoFit.hasMultipleDifferentValues;
                radius.style.display =
                    TextStudioPlacementInspector.IsManualRadiusVisible(
                        useAutoFit,
                        hasMixedValues)
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                padding.style.display =
                    TextStudioPlacementInspector.IsAutoFitPaddingVisible(
                        useAutoFit,
                        hasMixedValues)
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            };
            if (autoFit != null)
                root.TrackPropertyValue(autoFit, _ => refresh());
            refresh();
        }

        void ScheduleArcFillDisplay(
            VisualElement root,
            SerializedProperty placement,
            VisualElement fill)
        {
            SerializedProperty spacing =
                placement != null
                    ? placement.FindPropertyRelative("spacing")
                    : null;
            Action refresh = () =>
            {
                PathSpacing value = spacing != null
                    ? (PathSpacing)spacing.enumValueIndex
                    : PathSpacing.Even;
                bool visible =
                    spacing != null &&
                    TextStudioPlacementInspector.IsArcFillVisible(
                        value,
                        spacing.hasMultipleDifferentValues);
                fill.style.display =
                    visible ? DisplayStyle.Flex : DisplayStyle.None;
            };
            if (spacing != null)
                root.TrackPropertyValue(spacing, _ => refresh());
            refresh();
        }

        void OnSceneGUI()
        {
            if (target is not TextStudioText hub)
                return;

            using (new Handles.DrawingScope(
                       new Color(0.25f, 0.78f, 1f, 0.9f),
                       hub.transform.localToWorldMatrix))
            {
                switch (hub.PlacementMode)
                {
                    case PlacementMode.Arc:
                    {
                        ArcPlacement placement = hub.ArcPlacementSettings;
                        if (placement != null)
                            DrawCircularPlacementGuide(placement);
                        break;
                    }
                    case PlacementMode.Circle:
                    {
                        CirclePlacement placement = hub.CirclePlacementSettings;
                        if (placement != null)
                            DrawCircularPlacementGuide(placement);
                        break;
                    }
                    case PlacementMode.Grid:
                        DrawGridPlacementGuide(hub.GridPlacementSettings);
                        break;
                }
            }
        }

        static void DrawCircularPlacementGuide(ArcPlacement placement)
        {
            int runCount = placement.GuideRunCount;
            for (int runIndex = 0; runIndex < runCount; runIndex++)
            {
                if (!placement.TryBuildGuidePoints(
                        runIndex,
                        s_placementGuidePoints,
                        out _))
                    continue;

                DrawResolvedCircularPlacementGuide(runIndex == 0);
            }
        }

        static void DrawCircularPlacementGuide(CirclePlacement placement)
        {
            int runCount = placement.GuideRunCount;
            for (int runIndex = 0; runIndex < runCount; runIndex++)
            {
                if (!placement.TryBuildGuidePoints(
                        runIndex,
                        s_placementGuidePoints,
                        out _))
                    continue;

                DrawResolvedCircularPlacementGuide(runIndex == 0);
            }
        }

        static void DrawResolvedCircularPlacementGuide(bool drawDirection)
        {
            Handles.DrawAAPolyLine(2f, s_placementGuidePoints);
            if (!drawDirection)
                return;

            Vector3 arrowEnd = s_placementGuidePoints[5];
            DrawGuideArrow(
                s_placementGuidePoints[2],
                arrowEnd,
                HandleUtility.GetHandleSize(arrowEnd) * 0.08f);
            Vector3 start = s_placementGuidePoints[0];
            Handles.DrawSolidDisc(
                start,
                Vector3.forward,
                HandleUtility.GetHandleSize(start) * 0.035f);
        }

        static void DrawGridPlacementGuide(GridPlacement placement)
        {
            if (placement == null)
                return;

            int columns = Mathf.Max(1, placement.columns);
            Vector2 size = placement.cellSize;
            Vector2 gap = placement.gap;
            Vector2 step = new Vector2(
                Mathf.Max(0.0001f, Mathf.Abs(size.x) + gap.x),
                Mathf.Max(0.0001f, Mathf.Abs(size.y) + gap.y));
            const int guideRows = 3;
            Vector3 boundsSize = new Vector3(
                Mathf.Max(step.x, columns * step.x),
                guideRows * step.y,
                0f);
            Vector3 center = new Vector3(
                (columns - 1) * step.x * 0.5f,
                -(guideRows - 1) * step.y * 0.5f,
                0f);
            Handles.DrawWireCube(center, boundsSize);

            bool columnsFirst = placement.flow == GridFlow.ColumnsFirst;
            bool reverse = placement.reverse;
            Vector3 start = columnsFirst
                ? new Vector3(0f, 0f, 0f)
                : new Vector3(0f, 0f, 0f);
            Vector3 end = columnsFirst
                ? new Vector3(0f, -step.y, 0f)
                : new Vector3(step.x, 0f, 0f);
            if (reverse)
            {
                start = columnsFirst
                    ? new Vector3((columns - 1) * step.x, -(guideRows - 1) * step.y, 0f)
                    : new Vector3((columns - 1) * step.x, -(guideRows - 1) * step.y, 0f);
                end = columnsFirst
                    ? start + Vector3.up * step.y
                    : start + Vector3.left * step.x;
            }
            DrawGuideArrow(start, end, Mathf.Min(step.x, step.y) * 0.2f);
        }

        static void DrawGuideArrow(Vector3 start, Vector3 end, float size)
        {
            Handles.DrawLine(start, end, 2f);
            Vector3 direction = (end - start).normalized;
            if (direction.sqrMagnitude <= 0.000001f)
                return;
            Vector3 side = Vector3.Cross(Vector3.forward, direction);
            Handles.DrawLine(
                end,
                end - direction * size + side * size * 0.55f,
                2f);
            Handles.DrawLine(
                end,
                end - direction * size - side * size * 0.55f,
                2f);
        }

    }
}
