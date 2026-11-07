//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomPropertyDrawer(typeof(CloudsVolumetricOverride))]
    public class VolumetricCloudsOverrideDrawer : AtmosphereOverrideDrawer
    {

        public override VisualElement DisplayProperties(SerializedProperty property)
        {
            VisualElement container = new VisualElement();


            Label placementSettings = new Label("Generation");
            placementSettings.AddToClassList("h2");
            container.Add(placementSettings);

            PropertyField mainTexture = new PropertyField(property.FindPropertyRelative("mainTexture"));
            container.Add(mainTexture);
            PropertyField placementHeight = new PropertyField(property.FindPropertyRelative("placementHeight"));
            placementHeight.style.marginTop = 10;
            container.Add(placementHeight);
            PropertyField placementScale1 = new PropertyField(property.FindPropertyRelative("placementScale1"));
            container.Add(placementScale1);
            PropertyField placementScale2 = new PropertyField(property.FindPropertyRelative("placementScale2"));
            container.Add(placementScale2);
            PropertyField height = new PropertyField(property.FindPropertyRelative("height"));
            height.style.marginTop = 10;
            container.Add(height);
            PropertyField ramp = new PropertyField(property.FindPropertyRelative("ramp"));
            container.Add(ramp);
            PropertyField density = new PropertyField(property.FindPropertyRelative("density"));
            container.Add(density);

            PropertyField windStrength = new PropertyField(property.FindPropertyRelative("windStrength"));
            container.Add(windStrength);

            Label noiseLabel = new Label("Noise");
            noiseLabel.AddToClassList("h2");
            container.Add(noiseLabel);

            PropertyField noiseTexture = new PropertyField(property.FindPropertyRelative("noiseTexture"));
            container.Add(noiseTexture);
            PropertyField noiseScale = new PropertyField(property.FindPropertyRelative("noiseScale"));
            container.Add(noiseScale);
            PropertyField noiseAmount = new PropertyField(property.FindPropertyRelative("noiseAmount"));
            container.Add(noiseAmount);

            Label coloringLabel = new Label("Coloring");
            coloringLabel.AddToClassList("h2");
            container.Add(coloringLabel);

            PropertyField litColor = new PropertyField(property.FindPropertyRelative("litColor"));
            container.Add(litColor);

            PropertyField shadeColor = new PropertyField(property.FindPropertyRelative("shadeColor"));
            container.Add(shadeColor);

            PropertyField darkeningIntensity = new PropertyField(property.FindPropertyRelative("darkeningIntensity"));
            darkeningIntensity.style.marginBottom = 10;
            container.Add(darkeningIntensity);

            PropertyField highlightBrightness = new PropertyField(property.FindPropertyRelative("highlightBrightness"));
            highlightBrightness.enabledSelf = CozyPreferences.CurrentStyle.UseCloudsVolumetricHighlights;
            container.Add(highlightBrightness);

            PropertyField highlightStepSize = new PropertyField(property.FindPropertyRelative("highlightStepSize"));
            highlightStepSize.enabledSelf = CozyPreferences.CurrentStyle.UseCloudsVolumetricHighlights;
            container.Add(highlightStepSize);

            Tooltip useHighlight = new Tooltip("Highlights are currently disabled in the style.", StatusType.Warning);
            useHighlight.style.display = !CozyPreferences.CurrentStyle.UseCloudsVolumetricHighlights ? DisplayStyle.Flex : DisplayStyle.None;
            container.Add(useHighlight);

            Label raymarchingLabel = new Label("Raymarching");
            raymarchingLabel.AddToClassList("h2");
            container.Add(raymarchingLabel);

            PropertyField steps = new PropertyField(property.FindPropertyRelative("steps"));
            container.Add(steps);

            PropertyField stepSize = new PropertyField(property.FindPropertyRelative("stepSize"));
            container.Add(stepSize);

            PropertyField lODSteps = new PropertyField(property.FindPropertyRelative("lODSteps"));
            container.Add(lODSteps);

            PropertyField lODCoefficient = new PropertyField(property.FindPropertyRelative("lODCoefficient"));
            container.Add(lODCoefficient);

            PropertyField blueNoise = new PropertyField(property.FindPropertyRelative("blueNoise"));
            container.Add(blueNoise);

            return container;
        }
    }
}