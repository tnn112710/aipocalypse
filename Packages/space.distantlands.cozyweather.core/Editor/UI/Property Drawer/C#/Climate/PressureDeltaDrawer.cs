//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomPropertyDrawer(typeof(PressureDelta))]
    public class PressureDeltaDrawer : PropertyDrawer
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

            PressureDelta pressureDelta = new PressureDelta(Mathf.Round(Pressure.ConvertToUnit(property.FindPropertyRelative("value").floatValue, CozyPreferences.PressureUnit) * 100) / 100, CozyPreferences.PressureUnit);

            TextField pressureField = new TextField();
            pressureField.AddToClassList("unity-base-field__input");
            pressureField.AddToClassList("unity-property-field__input");
            pressureField.SetValueWithoutNotify(pressureDelta.ToString());
            pressureField.RegisterCallback<BlurEvent>(evt =>
            {
                pressureDelta = PressureDelta.Parse(pressureField.value);
                property.FindPropertyRelative("value").floatValue = Pressure.ConvertFromUnit((float)pressureDelta, CozyPreferences.PressureUnit);
                property.serializedObject.ApplyModifiedProperties();
                pressureField.SetValueWithoutNotify(pressureDelta.ToString());
            });

            field.Add(label);
            field.Add(pressureField);

            float startX = 0;
            float startValue = 0;
            bool dragging = false;

            label.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0) // left click
                {
                    dragging = true;
                    startX = evt.mousePosition.x;
                    startValue = (float)pressureDelta;
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
                    pressureDelta = new PressureDelta(newValue, CozyPreferences.PressureUnit);

                    property.FindPropertyRelative("value").floatValue =
                        Pressure.ConvertFromUnit((float)pressureDelta, CozyPreferences.PressureUnit);
                    property.serializedObject.ApplyModifiedProperties();

                    pressureField.SetValueWithoutNotify(pressureDelta.ToString());

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
