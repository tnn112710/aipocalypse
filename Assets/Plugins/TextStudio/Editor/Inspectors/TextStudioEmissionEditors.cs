using ImpossibleRobert.Common;
using TMPro;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    [CustomEditor(typeof(TextStudioEmitter))]
    internal sealed class TextStudioEmitterEditor : UnityEditor.Editor
    {
        SerializedProperty _defaultHostPrefab;
        SerializedProperty _defaultPreset;
        SerializedProperty _instanceParent;
        SerializedProperty _registeredPresets;
        SerializedProperty _prewarmCount;
        SerializedProperty _maximumInactivePerHost;
        SerializedProperty _maximumActiveEmissions;
        SerializedProperty _overflowPolicy;
        SerializedProperty _outputSpace;
        SerializedProperty _followTargets;
        SerializedProperty _faceCamera;
        SerializedProperty _camera;
        SerializedProperty _stackMatchingAnchors;
        SerializedProperty _stackSpacing;

        Label _readiness;
        Label _runtimeStatus;

        public override VisualElement CreateInspectorGUI()
        {
            CacheProperties();
            VisualElement root =
                TextStudioInspectorElements.CreateRoot("ts-emitter-editor-root");
            root.Add(CreateSetupSection());
            root.Add(CreatePlacementSection());
            root.Add(CreateCapacitySection());
            root.Add(CreateRuntimeSection());
            root.Bind(serializedObject);
            root.TrackSerializedObjectValue(serializedObject, _ => RefreshStatus());
            root.schedule.Execute(RefreshStatus);
            return root;
        }

        void CacheProperties()
        {
            _defaultHostPrefab = serializedObject.FindProperty("_defaultHostPrefab");
            _defaultPreset = serializedObject.FindProperty("_defaultPreset");
            _instanceParent = serializedObject.FindProperty("_instanceParent");
            _registeredPresets = serializedObject.FindProperty("_registeredPresets");
            _prewarmCount = serializedObject.FindProperty("_prewarmCount");
            _maximumInactivePerHost =
                serializedObject.FindProperty("_maximumInactivePerHost");
            _maximumActiveEmissions =
                serializedObject.FindProperty("_maximumActiveEmissions");
            _overflowPolicy = serializedObject.FindProperty("_overflowPolicy");
            _outputSpace = serializedObject.FindProperty("_outputSpace");
            _followTargets = serializedObject.FindProperty("_followTargets");
            _faceCamera = serializedObject.FindProperty("_faceCamera");
            _camera = serializedObject.FindProperty("_camera");
            _stackMatchingAnchors =
                serializedObject.FindProperty("_stackMatchingAnchors");
            _stackSpacing = serializedObject.FindProperty("_stackSpacing");
        }

        VisualElement CreateSetupSection()
        {
            VisualElement section = TextStudioInspectorElements.CreateSection(
                "Emit",
                "ts-emitter-setup");
            section.Add(TextStudioInspectorElements.CreateMutedText(
                "Assign one complete host and one presentation preset. Then EmitValue(value, target) is the full default workflow."));
            section.Add(CreateAssetRow(
                _defaultPreset,
                "Default Preset",
                "ts-emitter-default-preset",
                "Create and assign an editable emission preset.",
                CreatePreset,
                true));
            section.Add(CreateAssetRow(
                _defaultHostPrefab,
                "Default Host",
                "ts-emitter-default-host",
                "Create and assign a complete world-space TMP host prefab.",
                CreateHostPrefab));
            section.Add(new PropertyField(_instanceParent, "Instance Parent"));
            section.Add(new PropertyField(_registeredPresets, "Prewarmed Preset Overrides"));
            _readiness = TextStudioInspectorElements.CreateStatus(
                string.Empty,
                CommonInspectorStatusType.Info,
                "ts-emitter-readiness");
            section.Add(_readiness);
            return section;
        }

        VisualElement CreatePlacementSection()
        {
            VisualElement section = TextStudioInspectorElements.CreateSection(
                "Placement",
                "ts-emitter-placement");
            section.Add(new PropertyField(_outputSpace, "Output Space"));
            section.Add(new PropertyField(_followTargets, "Follow Targets"));
            section.Add(new PropertyField(_faceCamera, "Face Camera"));
            section.Add(new PropertyField(_camera, "Camera"));
            section.Add(new PropertyField(_stackMatchingAnchors, "Stack Matching Anchors"));
            section.Add(new PropertyField(_stackSpacing, "Stack Spacing"));
            return section;
        }

        VisualElement CreateCapacitySection()
        {
            VisualElement section = TextStudioInspectorElements.CreateSection(
                "Capacity",
                "ts-emitter-capacity");
            section.Add(TextStudioInspectorElements.CreateMutedText(
                "Active capacity is a gameplay bound. Inactive capacity controls only how many returned hosts Unity's pool retains."));
            section.Add(new PropertyField(_prewarmCount, "Prewarm Per Host"));
            section.Add(new PropertyField(_maximumInactivePerHost, "Maximum Inactive Per Host"));
            section.Add(new PropertyField(_maximumActiveEmissions, "Maximum Active"));
            section.Add(new PropertyField(_overflowPolicy, "Overflow"));
            return section;
        }

        VisualElement CreateRuntimeSection()
        {
            Foldout section = TextStudioInspectorElements.CreateFoldoutSection(
                "Runtime",
                "ts-emitter-runtime",
                false);
            _runtimeStatus = TextStudioInspectorElements.CreateStatus(
                "Runtime counts are available in Play Mode.",
                CommonInspectorStatusType.Info,
                "ts-emitter-runtime-status");
            section.Add(_runtimeStatus);
            Button release = TextStudioInspectorElements.CreateButton(
                "Release All",
                "Immediately return every active temporary host.",
                ReleaseAll);
            section.Add(release);
            return section;
        }

        VisualElement CreateAssetRow(
            SerializedProperty property,
            string label,
            string rowName,
            string createTooltip,
            System.Action createAction,
            bool availableWhenAssigned = false)
        {
            PropertyField field = new PropertyField(property, label);
            Button create = TextStudioInspectorElements.CreateCompactCreateButton(
                createAction,
                rowName + "-create",
                createTooltip);
            VisualElement row = TextStudioInspectorElements.CreateAssetReferenceRow(
                field,
                create,
                rowName);
            row.TrackPropertyValue(
                property,
                changed => TextStudioInspectorElements.SetCompactCreateButtonAvailable(
                    create,
                    availableWhenAssigned ||
                    changed.objectReferenceValue == null));
            TextStudioInspectorElements.SetCompactCreateButtonAvailable(
                create,
                availableWhenAssigned ||
                property.objectReferenceValue == null);
            return row;
        }

        void RefreshStatus()
        {
            if (_readiness == null)
                return;
            serializedObject.UpdateIfRequiredOrScript();
            GameObject host = _defaultHostPrefab.objectReferenceValue as GameObject;
            TextStudioEmissionPreset preset =
                _defaultPreset.objectReferenceValue as TextStudioEmissionPreset;
            if (host == null || preset == null)
            {
                TextStudioInspectorElements.SetStatus(
                    _readiness,
                    "Assign a default preset and complete host before emitting.",
                    CommonInspectorStatusType.Warning);
            }
            else if (host.GetComponentInChildren<TextStudioText>(true) == null)
            {
                TextStudioInspectorElements.SetStatus(
                    _readiness,
                    "The default host needs a Text Studio Text component.",
                    CommonInspectorStatusType.Error);
            }
            else if (!preset.HasValidNumericTemplate)
            {
                TextStudioInspectorElements.SetStatus(
                    _readiness,
                    "The preset template needs exactly one {value} placeholder.",
                    CommonInspectorStatusType.Error);
            }
            else
            {
                TextStudioInspectorElements.SetStatus(
                    _readiness,
                    "Ready for one-call EmitValue.",
                    CommonInspectorStatusType.Success);
            }

            TextStudioEmitter emitter = target as TextStudioEmitter;
            if (_runtimeStatus != null && emitter != null && Application.isPlaying)
            {
                TextStudioInspectorElements.SetStatus(
                    _runtimeStatus,
                    "Active " + emitter.ActiveCount +
                    "  •  Inactive " + emitter.InactiveCount +
                    "  •  Created " + emitter.CreatedCount,
                    CommonInspectorStatusType.Info);
            }
        }

        void CreatePreset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Emission Preset",
                "Damage",
                "asset",
                "Choose where to save the editable Text Studio emission preset.");
            if (string.IsNullOrEmpty(path))
                return;

            TextStudioEmissionPreset preset =
                ScriptableObject.CreateInstance<TextStudioEmissionPreset>();
            preset.NumberFormat = TextStudioNumberFormat.Default;
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            AssignObject(_defaultPreset, preset, "Assign Emission Preset");
        }

        void CreateHostPrefab()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Emission Host",
                "World Text Emission Host",
                "prefab",
                "Choose where to save the complete Text Studio host prefab.");
            if (string.IsNullOrEmpty(path))
                return;

            GameObject root = new GameObject("World Text Emission Host");
            TextMeshPro text = root.AddComponent<TextMeshPro>();
            text.text = "123";
            text.fontSize = 6f;
            text.alignment = TextAlignmentOptions.Center;
            text.richText = true;
            TextStudioText hub = root.AddComponent<TextStudioText>();
            hub.SetText("123");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssignObject(_defaultHostPrefab, prefab, "Assign Emission Host");
        }

        void AssignObject(
            SerializedProperty property,
            Object value,
            string undoName)
        {
            Undo.RecordObjects(targets, undoName);
            serializedObject.Update();
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedProperties();
            for (int i = 0; i < targets.Length; i++)
                EditorUtility.SetDirty(targets[i]);
        }

        void ReleaseAll()
        {
            TextStudioEmitter emitter = target as TextStudioEmitter;
            if (emitter != null)
                emitter.ReleaseAll();
            RefreshStatus();
        }
    }

    [CustomEditor(typeof(TextStudioEmissionPreset))]
    internal sealed class TextStudioEmissionPresetEditor : UnityEditor.Editor
    {
        Label _validation;
        VisualElement _formattedPreviewContainer;
        Label _formattedPreview;

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root =
                TextStudioInspectorElements.CreateRoot("ts-emission-preset-editor-root");
            root.Add(CreatePresentationSection());
            root.Add(CreateLifetimeSection());
            root.Add(CreateMergeSection());
            root.Add(CreatePreviewSection());
            root.Bind(serializedObject);
            root.TrackSerializedObjectValue(serializedObject, _ => RefreshValidation());
            root.schedule.Execute(RefreshValidation);
            return root;
        }

        VisualElement CreatePresentationSection()
        {
            VisualElement section = TextStudioInspectorElements.CreateSection(
                "Presentation",
                "ts-emission-preset-presentation");
            section.Add(TextStudioInspectorElements.CreateMutedText(
                "The behavior profile owns renderer-neutral behavior. The optional complete host override owns renderer-specific appearance."));
            section.Add(new PropertyField(
                serializedObject.FindProperty("_hostPrefabOverride"),
                "Host Override"));
            section.Add(new PropertyField(
                serializedObject.FindProperty("_behaviorProfile"),
                "Behavior Profile"));
            section.Add(new PropertyField(
                serializedObject.FindProperty("_numericTemplate"),
                "Numeric Template"));
            section.Add(new PropertyField(
                serializedObject.FindProperty("_numberFormat"),
                "Number Format"));
            _formattedPreviewContainer = new VisualElement
            {
                name = "ts-emission-preset-format-preview"
            };
            _formattedPreviewContainer.AddToClassList(
                "ts-emission-preset-format-preview");
            Label previewCaption = TextStudioInspectorElements.CreateMutedText(
                "Formatted Example",
                "ts-emission-preset-format-preview-caption");
            _formattedPreviewContainer.Add(previewCaption);
            _formattedPreview = new Label
            {
                name = "ts-emission-preset-format-preview-value",
                enableRichText = true
            };
            _formattedPreview.AddToClassList(
                "ts-emission-preset-format-preview-value");
            _formattedPreviewContainer.Add(_formattedPreview);
            section.Add(_formattedPreviewContainer);
            _validation = TextStudioInspectorElements.CreateStatus(
                string.Empty,
                CommonInspectorStatusType.Info,
                "ts-emission-preset-validation");
            section.Add(_validation);
            return section;
        }

        VisualElement CreateLifetimeSection()
        {
            VisualElement section = TextStudioInspectorElements.CreateSection(
                "Lifetime",
                "ts-emission-preset-lifetime");
            section.Add(new PropertyField(
                serializedObject.FindProperty("_manualDismissal"),
                "Manual Dismissal"));
            section.Add(new PropertyField(
                serializedObject.FindProperty("_activeDuration"),
                "Active Duration"));
            return section;
        }

        VisualElement CreateMergeSection()
        {
            VisualElement section = TextStudioInspectorElements.CreateSection(
                "Aggregation",
                "ts-emission-preset-merge");
            section.Add(new PropertyField(
                serializedObject.FindProperty("_mergeMode"),
                "Merge"));
            section.Add(new PropertyField(
                serializedObject.FindProperty("_mergeWindow"),
                "Merge Window"));
            section.Add(new PropertyField(
                serializedObject.FindProperty("_restartEffectsOnMerge"),
                "Restart Effects On Merge"));
            return section;
        }

        VisualElement CreatePreviewSection()
        {
            VisualElement section = TextStudioInspectorElements.CreateSection(
                "Preview",
                "ts-emission-preset-preview");
            section.Add(TextStudioInspectorElements.CreateMutedText(
                "In Play Mode this uses the first active Text Studio Emitter, including its real host, profile, lifecycle, projection, and pool."));
            section.Add(TextStudioInspectorElements.CreateButton(
                "Preview 1,234",
                "Emit a representative numeric value through an active scene emitter.",
                PreviewPreset,
                true));
            return section;
        }

        void RefreshValidation()
        {
            TextStudioEmissionPreset preset = target as TextStudioEmissionPreset;
            if (_validation == null ||
                _formattedPreviewContainer == null ||
                _formattedPreview == null ||
                preset == null)
                return;
            if (!preset.HasValidNumericTemplate)
            {
                _formattedPreviewContainer.style.display = DisplayStyle.None;
                _validation.style.display = DisplayStyle.Flex;
                TextStudioInspectorElements.SetStatus(
                    _validation,
                    "Use exactly one {value} placeholder.",
                    CommonInspectorStatusType.Error);
                return;
            }

            GameObject host = preset.HostPrefabOverride;
            _formattedPreviewContainer.style.display = DisplayStyle.Flex;
            _formattedPreview.text = preset.FormatValue(1234d);
            if (host != null &&
                host.GetComponentInChildren<TextStudioText>(true) == null)
            {
                _validation.style.display = DisplayStyle.Flex;
                TextStudioInspectorElements.SetStatus(
                    _validation,
                    "The host override needs a Text Studio Text component.",
                    CommonInspectorStatusType.Error);
                return;
            }

            _validation.style.display = DisplayStyle.None;
        }

        void PreviewPreset()
        {
            if (!Application.isPlaying)
            {
                Debug.Log("Enter Play Mode to preview this emission preset.");
                return;
            }

            TextStudioEmitter emitter = UnityUtils.FindAny<TextStudioEmitter>();
            if (emitter == null)
            {
                Debug.LogWarning(
                    "Add an active Text Studio Emitter to preview this preset.");
                return;
            }
            TextStudioEmissionPreset preset = target as TextStudioEmissionPreset;
            emitter.EmitValue(preset, 1234d, emitter.transform);
        }
    }
}
