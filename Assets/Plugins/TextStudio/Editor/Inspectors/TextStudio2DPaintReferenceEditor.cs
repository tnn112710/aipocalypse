using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    [CustomEditor(typeof(TextStudio2DPaintAsset))]
    internal sealed class TextStudio2DPaintAssetEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            return new TextStudio2DPaintAuthoringElement(serializedObject.FindProperty("_paint"), false);
        }
    }

    internal sealed partial class TextStudio2DPaintAuthoringElement
    {
        VisualElement _referenceDetails;
        Label _referenceSummary;
        ColorField _referenceSwatch;

        void AddReferenceControls()
        {
            PropertyField source = new PropertyField(_paint.FindPropertyRelative("_source"), "Shared Paint")
            {
                name = "ts-2d-paint-source",
                tooltip = "Reference a reusable paint asset. Leave empty to author this layer's paint locally."
            };
            ConfigureField(source);
            source.RegisterValueChangeCallback(_ =>
            {
                _paint.FindPropertyRelative("_useSource").boolValue = _paint.FindPropertyRelative("_source").objectReferenceValue != null;
                _paint.serializedObject.ApplyModifiedProperties();
                Refresh();
            });
            Add(source);
            _referenceDetails = new VisualElement { name = "ts-2d-paint-reference-details" };
            _referenceSummary = TextStudioInspectorElements.CreateMutedText(string.Empty);
            _referenceDetails.Add(_referenceSummary);
            _referenceSwatch = new ColorField("Source Tint") { hdr = true, showAlpha = true };
            ConfigureField(_referenceSwatch);
            _referenceSwatch.SetEnabled(false);
            _referenceDetails.Add(_referenceSwatch);
            PropertyField overrides = new PropertyField(_paint.FindPropertyRelative("_overrides"), "Local Overrides")
            {
                name = "ts-2d-paint-overrides",
                tooltip = "Only checked groups use this layer's values. All other values follow the shared paint."
            };
            ConfigureField(overrides);
            _referenceDetails.Add(overrides);
            VisualElement actions = new VisualElement();
            actions.AddToClassList("ts-inline-row");
            actions.Add(new Button(() => ChangeBinding(true)) { text = "Make Local", tooltip = "Copy the resolved paint and remove its shared reference." });
            actions.Add(new Button(() => ChangeBinding(false)) { text = "Reset Overrides", tooltip = "Use every value from the shared paint." });
            _referenceDetails.Add(actions);
            Add(_referenceDetails);
        }

        void ChangeBinding(bool makeLocal)
        {
            SerializedObject owner = _paint.serializedObject;
            owner.UpdateIfRequiredOrScript();
            TextStudio2DPaint paint = (TextStudio2DPaint)_paint.boxedValue;
            _paint.boxedValue = makeLocal ? paint.MakeLocal() : paint.WithOverrides(TextStudio2DPaintOverrides.None);
            owner.ApplyModifiedProperties();
            Refresh();
        }

        void RefreshReferenceControls()
        {
            TextStudio2DPaintAsset source = _paint.FindPropertyRelative("_source").objectReferenceValue as TextStudio2DPaintAsset;
            bool linked = source != null;
            if (_referenceDetails != null)
            {
                SetVisible(_referenceDetails, linked);
                if (linked)
                {
                    _referenceSummary.text = "Inherited from " + source.name + ". Edit the asset to update all its users.";
                    _referenceSwatch.SetValueWithoutNotify(source.Value.Color);
                }
            }
            SetVisible(_kindField, !linked);
            if (!linked) return;
            SetVisible(_advanced, false);
            SetVisible(_gradientRow, false);
            SetVisible(_fourCornerEditor, false);
            TextStudio2DPaintOverrides overrides = (TextStudio2DPaintOverrides)_paint.FindPropertyRelative("_overrides").intValue;
            foreach (System.Collections.Generic.KeyValuePair<string, PropertyField> field in _fields)
            {
                TextStudio2DPaintOverrides required = field.Key switch
                {
                    "_mapping" or "_rangeMapping" => TextStudio2DPaintOverrides.Mapping,
                    "_color" => TextStudio2DPaintOverrides.Tint,
                    "_angle" or "_center" or "_scale" or "_offset" => TextStudio2DPaintOverrides.Transform,
                    "_spread" => TextStudio2DPaintOverrides.Spread,
                    "_textureFit" => TextStudio2DPaintOverrides.TextureFit,
                    "_distanceDomain" or "_distanceRange" => TextStudio2DPaintOverrides.Distance,
                    _ => TextStudio2DPaintOverrides.None
                };
                if (required == TextStudio2DPaintOverrides.None || (overrides & required) == 0)
                    SetVisible(field.Value, false);
            }
        }
    }
}
