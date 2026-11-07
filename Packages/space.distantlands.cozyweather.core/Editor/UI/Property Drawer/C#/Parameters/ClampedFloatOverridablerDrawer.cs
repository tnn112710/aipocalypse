//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomPropertyDrawer(typeof(FloatRangeOverridable))]
    public class ClampedFloatParameterDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty overrideStateProp = property.FindPropertyRelative("overrideState");
            SerializedProperty valueProp = property.FindPropertyRelative("value");
            SerializedProperty maxProp = property.FindPropertyRelative("max");
            SerializedProperty minProp = property.FindPropertyRelative("min");

            VisualElement container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignContent = Align.Center;
            container.style.justifyContent = Justify.Center;

            Toggle overrideState = new Toggle();
            overrideState.BindProperty(overrideStateProp);
            overrideState.style.marginTop = 0;
            overrideState.style.marginLeft = 0;
            overrideState.style.marginRight = 0;
            overrideState.style.marginBottom = 0;
            container.Add(overrideState);

            Slider value = new Slider(minProp.floatValue, maxProp.floatValue);
            value.showInputField = true;
            value.SetEnabled(overrideStateProp.boolValue);
            value.BindProperty(valueProp);
            overrideState.RegisterValueChangedCallback(evt =>
            {
                value.SetEnabled(overrideStateProp.boolValue);
            });
            value.style.flexGrow = 1;
            value.style.marginLeft = 5;
            value.label = property.displayName;
            value.AddToClassList("unity-base-field__aligned");
            container.Add(value);

            return container;

        }
    }
}