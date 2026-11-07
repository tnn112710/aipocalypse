//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomPropertyDrawer(typeof(Pressure))]
    public class PressureDrawer : PropertyDrawer
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

            Pressure temperature = new Pressure(Mathf.Round(Pressure.ConvertToUnit(property.FindPropertyRelative("value").floatValue, CozyPreferences.PressureUnit) * 100) / 100, CozyPreferences.PressureUnit);

            TextField temperatureField = new TextField();
            temperatureField.AddToClassList("unity-base-field__input");
            temperatureField.AddToClassList("unity-property-field__input");
            temperatureField.SetValueWithoutNotify(temperature.ToString());
            temperatureField.RegisterCallback<BlurEvent>(evt =>
            {
                temperature = Pressure.Parse(temperatureField.value);
                property.FindPropertyRelative("value").floatValue = Pressure.ConvertFromUnit((float)temperature, CozyPreferences.PressureUnit);
                property.serializedObject.ApplyModifiedProperties();
                temperatureField.SetValueWithoutNotify(temperature.ToString());
            });

            field.Add(label);
            field.Add(temperatureField);

            float startX = 0;
            float startValue = 0;
            bool dragging = false;

            label.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0) // left click
                {
                    dragging = true;
                    startX = evt.mousePosition.x;
                    startValue = (float)temperature;
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
                    float sensitivity = 0.1f;

                    float newValue = startValue + delta * sensitivity;
                    temperature = new Pressure(newValue, CozyPreferences.PressureUnit);

                    property.FindPropertyRelative("value").floatValue =
                        Pressure.ConvertFromUnit((float)temperature, CozyPreferences.PressureUnit);
                    property.serializedObject.ApplyModifiedProperties();

                    temperatureField.SetValueWithoutNotify(temperature.ToString());

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

    }
}