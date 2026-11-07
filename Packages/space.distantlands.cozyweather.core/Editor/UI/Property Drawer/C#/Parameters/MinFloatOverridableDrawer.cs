//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomPropertyDrawer(typeof(MinFloatOverridable))]
    public class MinFloatOverridableDrawer : PropertyDrawer
    {
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty overrideStateProp = property.FindPropertyRelative("overrideState");
            SerializedProperty valueProp = property.FindPropertyRelative("value");
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

            FloatField value = new FloatField();
            value.SetEnabled(overrideStateProp.boolValue);
            value.BindProperty(valueProp);
            value.RegisterValueChangedCallback(evt =>
            {
                if (valueProp.floatValue < minProp.floatValue)
                {
                    valueProp.floatValue = minProp.floatValue;
                    property.serializedObject.ApplyModifiedProperties();
                    value.value = minProp.floatValue;
                }
            });
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