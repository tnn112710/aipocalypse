//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomPropertyDrawer(typeof(HumidityDelta))]
    public class HumidityDeltaDrawer : PropertyDrawer
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

            HumidityDelta humidityDelta = new HumidityDelta(SetDigits(property.FindPropertyRelative("value").floatValue, 1));

            TextField humidityField = new TextField();
            humidityField.AddToClassList("unity-base-field__input");
            humidityField.AddToClassList("unity-property-field__input");
            humidityField.SetValueWithoutNotify(humidityDelta.ToString());
            humidityField.RegisterCallback<BlurEvent>(evt =>
            {
                humidityDelta = HumidityDelta.Parse(humidityField.value);
                property.FindPropertyRelative("value").floatValue = (float)humidityDelta;
                property.serializedObject.ApplyModifiedProperties();
                humidityField.SetValueWithoutNotify(humidityDelta.ToString());
            });

            field.Add(label);
            field.Add(humidityField);

            float startX = 0;
            float startValue = 0;
            bool dragging = false;

            label.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0) // left click
                {
                    dragging = true;
                    startX = evt.mousePosition.x;
                    startValue = (float)humidityDelta;
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
                    humidityDelta = new HumidityDelta(newValue);

                    property.FindPropertyRelative("value").floatValue = (float)humidityDelta;
                    property.serializedObject.ApplyModifiedProperties();

                    humidityField.SetValueWithoutNotify(humidityDelta.ToString());

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
