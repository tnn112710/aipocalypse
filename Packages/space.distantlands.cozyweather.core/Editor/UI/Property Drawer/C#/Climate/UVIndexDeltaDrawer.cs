//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomPropertyDrawer(typeof(UVIndexDelta))]
    public class UVIndexDeltaDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement field = new VisualElement();
            field.AddToClassList("unity-base-field");
            field.AddToClassList("unity-text-field");
            field.AddToClassList("unity-base-field__aligned");
            field.AddToClassList("unity-base-field__inspector-field");

            Label label = new Label();
            label.text = preferredLabel;
            if (string.IsNullOrEmpty(preferredLabel)) label.style.display = DisplayStyle.None;
            label.AddToClassList("unity-base-field__label");
            label.AddToClassList("unity-property-field__label");
            label.AddToClassList("unity-label");
            label.AddToClassList("unity-base-field__label--with-dragger");

            UVIndexDelta uvDelta = new UVIndexDelta(SetDigits(property.FindPropertyRelative("value").floatValue, 0));

            TextField uvField = new TextField();
            uvField.AddToClassList("unity-base-field__input");
            uvField.AddToClassList("unity-property-field__input");
            uvField.SetValueWithoutNotify(uvDelta.ToString());
            uvField.RegisterCallback<BlurEvent>(evt =>
            {
                uvDelta = UVIndexDelta.Parse(uvField.value);
                property.FindPropertyRelative("value").floatValue = (float)uvDelta;
                property.serializedObject.ApplyModifiedProperties();
                uvField.SetValueWithoutNotify(uvDelta.ToString());
            });

            field.Add(label);
            field.Add(uvField);

            float startX = 0;
            float startValue = 0;
            bool dragging = false;

            label.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0) // left click
                {
                    dragging = true;
                    startX = evt.mousePosition.x;
                    startValue = (float)uvDelta;
                    label.CaptureMouse();
                    evt.StopPropagation();
                }
            });

            label.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (dragging && label.HasMouseCapture())
                {
                    float delta = evt.mousePosition.x - startX;

                    // Adjust sensitivity as needed
                    float sensitivity = 0.5f;

                    float newValue = SetDigits(startValue + delta * sensitivity, 1);
                    uvDelta = new UVIndexDelta(newValue);

                    property.FindPropertyRelative("value").floatValue = (float)uvDelta;
                    property.serializedObject.ApplyModifiedProperties();

                    uvField.SetValueWithoutNotify(uvDelta.ToString());

                    evt.StopPropagation();
                }
            });

            label.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (dragging && evt.button == 0)
                {
                    dragging = false;
                    label.ReleaseMouse();
                    evt.StopPropagation();
                }
            });


            return field;
        }

        float SetDigits(float input, int digitsBelowZero)
        {
            return Mathf.Round(input * Mathf.Pow(10, digitsBelowZero)) / Mathf.Pow(10, digitsBelowZero);
        }

    }
}
