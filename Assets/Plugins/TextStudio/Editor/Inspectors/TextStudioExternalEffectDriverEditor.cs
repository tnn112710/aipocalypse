using ImpossibleRobert.Common;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    [CustomEditor(typeof(TextStudioExternalEffectDriver))]
    internal sealed class TextStudioExternalEffectDriverEditor : UnityEditor.Editor
    {
        SerializedProperty _effect;
        SerializedProperty _progress;
        SerializedProperty _intensity;
        SerializedProperty _range;
        TextField _rangeField;
        string _rangeError = string.Empty;
        Label _status;

        public override VisualElement CreateInspectorGUI()
        {
            CacheProperties();
            VisualElement root = TextStudioInspectorElements.CreateRoot(
                "ts-external-effect-driver-editor-root");
            root.Add(CreateEffectSection());
            root.Add(CreateDriveSection());
            root.Add(CreateRangeSection());
            root.Add(CreateStatusSection());
            root.Bind(serializedObject);
            root.TrackSerializedObjectValue(serializedObject, _ => RefreshState());
            root.TrackPropertyValue(_range, _ => RefreshRangeField());
            root.schedule.Execute(RefreshState);
            return root;
        }

        void CacheProperties()
        {
            _effect = serializedObject.FindProperty("_effect");
            _progress = serializedObject.FindProperty("_progress");
            _intensity = serializedObject.FindProperty("_intensity");
            _range = serializedObject.FindProperty("_range");
        }

        VisualElement CreateEffectSection()
        {
            VisualElement section = TextStudioInspectorElements.CreateSection(
                "Effect",
                "ts-external-effect-driver-effect");
            section.Add(TextStudioInspectorElements.CreateMutedText(
                "Drive one existing Text Studio effect from Animator, Timeline, a tween, gameplay code, or an editor scrubber."));
            section.Add(new PropertyField(_effect, "Effect"));
            return section;
        }

        VisualElement CreateDriveSection()
        {
            VisualElement section = TextStudioInspectorElements.CreateSection(
                "Drive",
                "ts-external-effect-driver-drive");
            section.Add(new PropertyField(_progress, "Progress"));
            section.Add(new PropertyField(_intensity, "Intensity"));
            return section;
        }

        VisualElement CreateRangeSection()
        {
            VisualElement section = TextStudioInspectorElements.CreateSection(
                "Range",
                "ts-external-effect-driver-range");
            _rangeField = new TextField("Text units")
            {
                name = "ts-external-effect-driver-range-field",
                tooltip = "Use .. for all text or an exclusive semantic range such as 0..5, 3.., or ^5..^0."
            };
            _rangeField.RegisterValueChangedCallback(change =>
            {
                if (!TextStudioTextRange.TryParse(change.newValue, out TextStudioTextRange parsed, out _rangeError))
                {
                    RefreshState();
                    return;
                }
                serializedObject.Update();
                _range.boxedValue = parsed;
                serializedObject.ApplyModifiedProperties();
                RefreshState();
            });
            section.Add(_rangeField);
            section.Add(TextStudioInspectorElements.CreateMutedText(
                "The end is exclusive. Spaces count as text units; tags do not."));
            RefreshRangeField();
            return section;
        }

        void RefreshRangeField()
        {
            if (_rangeField == null) return;
            if (!string.IsNullOrEmpty(_rangeError)) return;
            serializedObject.UpdateIfRequiredOrScript();
            TextStudioTextRange value = (TextStudioTextRange)_range.boxedValue;
            _rangeField.SetValueWithoutNotify(value.ToString());
        }

        VisualElement CreateStatusSection()
        {
            VisualElement section = TextStudioInspectorElements.CreateSection(
                "Status",
                "ts-external-effect-driver-status-section");
            _status = TextStudioInspectorElements.CreateStatus(
                string.Empty,
                CommonInspectorStatusType.Info,
                "ts-external-effect-driver-status");
            section.Add(_status);
            return section;
        }

        void RefreshState()
        {
            if (_status == null || _rangeField == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            if (!string.IsNullOrEmpty(_rangeError))
            {
                TextStudioInspectorElements.SetStatus(_status, _rangeError, CommonInspectorStatusType.Error);
                return;
            }

            if (_effect.objectReferenceValue == null)
            {
                TextStudioInspectorElements.SetStatus(
                    _status,
                    "Assign an effect definition to begin driving a visual state.",
                    CommonInspectorStatusType.Warning);
                return;
            }
            if (_intensity.floatValue <= 0f)
            {
                TextStudioInspectorElements.SetStatus(
                    _status,
                    "Intensity is zero, so this driver contributes no effect.",
                    CommonInspectorStatusType.Info);
                return;
            }
            TextStudioInspectorElements.SetStatus(
                _status,
                "Ready. Progress is sampled directly and does not create a second playback clock.",
                CommonInspectorStatusType.Success);
        }
    }
}
