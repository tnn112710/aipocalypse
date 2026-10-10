using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    /// <summary>Keeps variable-length appearance parameters valid while preserving the array editor during value drags.</summary>
    internal sealed class TextStudio2DParameterArrayEditor : VisualElement
    {
        readonly SerializedObject _owner;
        readonly string _path;
        readonly bool _polygon;
        readonly VisualElement _entries;
        readonly BaseField<int> _count;
        readonly SerializedProperty _shape;
        int _displayedCount = -1;
        int _previousShape;

        internal string PropertyPath => _path;

        internal TextStudio2DParameterArrayEditor(SerializedProperty array, bool polygon)
        {
            _owner = array.serializedObject;
            _path = array.propertyPath;
            _polygon = polygon;
            name = polygon ? "ts-2d-custom-points" : "ts-2d-dash-pattern";
            Foldout foldout = EffectPropertyDrawerLayout.CreateFoldout(array,
                new GUIContent(polygon ? "Custom Points" : "Dash Pattern"));
            Add(foldout);
            _count = polygon
                ? new SliderInt("Point Count", 3, 8) { showInputField = true }
                : new IntegerField("Pattern Length") { isDelayed = true };
            _count.tooltip = polygon
                ? "A custom polygon contains 3 to 8 ordered points. Added points subdivide its closing edge without moving existing points."
                : "Number of alternating dash and gap lengths. Zero draws a continuous stroke. New entries start with a positive length.";
            _count.AddToClassList(BaseField<int>.alignedFieldUssClassName);
            _count.RegisterValueChangedCallback(evt => Resize(evt.newValue));
            foldout.Add(_count);
            _entries = new VisualElement();
            foldout.Add(_entries);
            if (polygon)
            {
                string parentPath = _path.Substring(0, _path.LastIndexOf('.'));
                _shape = _owner.FindProperty(parentPath + "._shape");
                _previousShape = _shape.intValue;
                this.TrackPropertyValue(_shape, _ =>
                {
                    bool newlySelected = _shape.intValue != _previousShape &&
                        _shape.intValue == (int)TextStudio2DDecorationShape.CustomPolygon;
                    _previousShape = _shape.intValue;
                    if (newlySelected && _owner.FindProperty(_path).arraySize < 3)
                        Resize(4);
                    Refresh();
                });
            }
            this.TrackPropertyValue(array.FindPropertyRelative("Array.size"), _ => Refresh());
            Refresh();
        }

        void Resize(int requested)
        {
            _owner.UpdateIfRequiredOrScript();
            SerializedProperty array = _owner.FindProperty(_path);
            int count = _polygon ? Mathf.Clamp(requested, 3, 8) : Mathf.Max(0, requested);
            int previousCount = array.arraySize;
            if (count == previousCount)
            {
                _count.SetValueWithoutNotify(count);
                return;
            }
            Vector2 first = _polygon && previousCount > 0 ? array.GetArrayElementAtIndex(0).vector2Value : Vector2.zero;
            Vector2 last = _polygon && previousCount > 0 ? array.GetArrayElementAtIndex(previousCount - 1).vector2Value : Vector2.zero;
            array.arraySize = count;
            if (_polygon && previousCount < 3)
            {
                for (int index = 0; index < count; index++)
                {
                    float angle = Mathf.PI * 0.5f - index * Mathf.PI * 2f / count;
                    array.GetArrayElementAtIndex(index).vector2Value = Vector2.one * 0.5f +
                        new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.5f;
                }
            }
            else
            {
                for (int index = previousCount; index < count; index++)
                {
                    SerializedProperty entry = array.GetArrayElementAtIndex(index);
                    if (_polygon)
                    {
                        float amount = (index - previousCount + 1f) / (count - previousCount + 1f);
                        entry.vector2Value = Vector2.Lerp(last, first, amount);
                    }
                    else if (!float.IsFinite(entry.floatValue) || entry.floatValue <= 0f)
                    {
                        entry.floatValue = 0.1f;
                    }
                }
            }
            _owner.ApplyModifiedProperties();
            Refresh();
        }

        void Refresh()
        {
            SerializedProperty array = _owner.FindProperty(_path);
            int count = array.arraySize;
            _count.SetValueWithoutNotify(count);
            bool active = !_polygon || _shape.intValue == (int)TextStudio2DDecorationShape.CustomPolygon;
            _count.SetEnabled(active);
            _entries.SetEnabled(active);
            if (_displayedCount == count)
                return;
            _displayedCount = count;
            _entries.Unbind();
            _entries.Clear();
            for (int index = 0; index < count; index++)
            {
                PropertyField field = new PropertyField(array.GetArrayElementAtIndex(index),
                    _polygon ? "Point " + (index + 1) : (index % 2 == 0 ? "Dash " : "Gap ") + (index / 2 + 1))
                {
                    tooltip = _polygon
                        ? "Ordered polygon point in normalized plate coordinates. Coordinates may extend outside the plate."
                        : "Positive length in the stroke's selected Units. Zero-length dashes and gaps are not supported."
                };
                field.AddToClassList("ts-property-field");
                _entries.Add(field);
            }
            _entries.Bind(_owner);
        }
    }
}
