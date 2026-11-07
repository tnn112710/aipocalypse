//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomPropertyDrawer(typeof(LightingAmbientOverride))]
    public class LightingAmbientOverrideDrawer : AtmosphereOverrideDrawer
    {

        public override VisualElement DisplayProperties(SerializedProperty property)
        {
            VisualElement container = new VisualElement();


            SerializedProperty ambientMode = property.FindPropertyRelative("ambientMode");
            int ambientModeIndex = property.FindPropertyRelative("ambientMode").FindPropertyRelative("constantValue").FindPropertyRelative("value").enumValueIndex;

            PropertyField mode = new PropertyField(ambientMode);
            container.Add(mode);


            PropertyField zenithColor = new PropertyField(property.FindPropertyRelative("zenithColor"));
            zenithColor.style.display = ambientModeIndex == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            container.Add(zenithColor);

            PropertyField color = new PropertyField(property.FindPropertyRelative("horizonColor"));
            container.Add(color);

            PropertyField groundColor = new PropertyField(property.FindPropertyRelative("groundColor"));
            groundColor.style.display = ambientModeIndex == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            container.Add(groundColor);

            PropertyField strength = new PropertyField(property.FindPropertyRelative("intensity"));
            container.Add(strength);

            mode.RegisterValueChangeCallback(evt =>
            {
                ambientModeIndex = property.FindPropertyRelative("ambientMode").FindPropertyRelative("constantValue").FindPropertyRelative("value").enumValueIndex;
                zenithColor.style.display = ambientModeIndex == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                groundColor.style.display = ambientModeIndex == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                color.label = ambientModeIndex == 0 ? "Horizon Color" : "Color";
            });

            return container;
        }
    }
}