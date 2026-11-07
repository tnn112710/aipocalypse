//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomPropertyDrawer(typeof(PrecipitationChance))]
    public class RainChanceDrawer : PropertyDrawer
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

            PrecipitationChance chance = new PrecipitationChance(SetDigits(property.FindPropertyRelative("value").floatValue, 3));

            TextField chanceField = new TextField();
            chanceField.AddToClassList("unity-base-field__input");
            chanceField.AddToClassList("unity-property-field__input");
            chanceField.SetValueWithoutNotify(chance.ToString());
            chanceField.RegisterCallback<BlurEvent>(evt =>
            {
                chance = PrecipitationChance.Parse(chanceField.value);
                property.FindPropertyRelative("value").floatValue = (float)chance;
                property.serializedObject.ApplyModifiedProperties();
                chanceField.SetValueWithoutNotify(chance.ToString());
            });

            field.Add(label);
            field.Add(chanceField);

            float startX = 0;
            float startValue = 0;
            bool dragging = false;

            label.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0) // left click
                {
                    dragging = true;
                    startX = evt.mousePosition.x;
                    startValue = (float)chance;
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
                    float sensitivity = 0.005f;

                    float newValue = SetDigits(startValue + delta * sensitivity, 3);
                    chance = new PrecipitationChance(newValue);

                    property.FindPropertyRelative("value").floatValue = (float)chance;
                    property.serializedObject.ApplyModifiedProperties();

                    chanceField.SetValueWithoutNotify(chance.ToString());

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