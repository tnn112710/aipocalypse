//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomPropertyDrawer(typeof(TemperatureDelta))]
    public class TemperatureDeltaDrawer : PropertyDrawer
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

            TemperatureDelta temperatureDelta = new TemperatureDelta(Mathf.Round(TemperatureDelta.ConvertToUnit(property.FindPropertyRelative("value").floatValue, CozyPreferences.TemperatureUnit) * 100) / 100, CozyPreferences.TemperatureUnit);

            TextField temperatureField = new TextField();
            temperatureField.AddToClassList("unity-base-field__input");
            temperatureField.AddToClassList("unity-property-field__input");
            temperatureField.SetValueWithoutNotify(temperatureDelta.ToString());
            temperatureField.RegisterCallback<BlurEvent>(evt =>
            {
                temperatureDelta = TemperatureDelta.Parse(temperatureField.value);
                property.FindPropertyRelative("value").floatValue = Temperature.ConvertFromUnit((float)temperatureDelta, CozyPreferences.TemperatureUnit);
                property.serializedObject.ApplyModifiedProperties();
                temperatureField.SetValueWithoutNotify(temperatureDelta.ToString());
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
                    startValue = (float)temperatureDelta;
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
                    temperatureDelta = new TemperatureDelta(newValue, CozyPreferences.TemperatureUnit);

                    property.FindPropertyRelative("value").floatValue =
                        Temperature.ConvertFromUnit((float)temperatureDelta, CozyPreferences.TemperatureUnit);
                    property.serializedObject.ApplyModifiedProperties();

                    temperatureField.SetValueWithoutNotify(temperatureDelta.ToString());

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
