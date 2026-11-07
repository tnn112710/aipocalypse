//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    public enum PerformanceOverhead
    {
        None,
        Lightweight,
        Normal,
        Heavy
    }


    [CustomEditor(typeof(CozyStyle))]
    public class CozyStyleEditor : Editor
    {
        CozyStyle style;
        VisualElement root;
        ScrollView scrollView;


        /// <summary>
        /// This function is called when the object becomes enabled and active.
        /// </summary>
        void OnEnable()
        {
            if (!target)
                return;

            style = (CozyStyle)target;
        }

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/space.distantlands.cozyweather.core/Editor/UI/Globals.uss");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);

            TextField nameField = new TextField();
            nameField.label = "Name";
            nameField.SetValueWithoutNotify(style.name);
            nameField.RegisterCallback<BlurEvent>(evt =>
            {
                style.name = nameField.value;
                AssetDatabase.SaveAssets();
                EditorUtility.SetDirty(CozyPreferences.Instance);
            });
            nameField.style.marginTop = 4;
            nameField.style.marginBottom = 4;
            root.Add(nameField);

            scrollView = new ScrollView();
            root.Add(scrollView);


            // START GENERATED CODE
            #region Lighting
            DrawLayerHeader(serializedObject.FindProperty("useLighting"), "Lighting", ("Sun", "Calculate the sun lighting in your world using a directional light.", PerformanceOverhead.Lightweight), ("SunCloudShadows", "Adds shadows for the volumetric clouds to the sun's light cookie", PerformanceOverhead.Normal), ("SunLensFlare", "Draws a lens flare around the sun using Unity's SRP flare system.", PerformanceOverhead.Lightweight), ("Moon", "Calculate the moon lighting in your world using a directional light.", PerformanceOverhead.Lightweight), ("MoonLensFlare", "Draws a lens flare around the moon using Unity's SRP flare system.", PerformanceOverhead.Lightweight), ("LensFlareOcclusion", "Should lens flares be occluded by the clouds", PerformanceOverhead.Normal), ("Ambient", "Control the ambient light in your world with COZY.", PerformanceOverhead.Lightweight));
            #endregion
            #region Sky
            DrawLayerHeader(serializedObject.FindProperty("useSky"), "Sky", ("Gradient", "Applies a base gradient to your sky.", PerformanceOverhead.Lightweight), ("TwoTone", "Shades the horizon two different tones based on the sun direction.", PerformanceOverhead.Lightweight), ("Stars", "Draws stars onto your sky using a cubemap.", PerformanceOverhead.Lightweight), ("Constellations", "Draws a secondary star cubemap onto your sky to display constellations.", PerformanceOverhead.Lightweight), ("Galaxy", "Draws a dynamically colored galaxy onto your sky using a cubemap.", PerformanceOverhead.Lightweight), ("Rainbow", "Draws a rainbow when the current wetness in greater than 0.", PerformanceOverhead.Lightweight), ("LightColumns", "Draws a ribbon texture to simulate light scattering on the horizon.", PerformanceOverhead.Lightweight), ("ShootingStars", "Adds shooting stars to your sky.", PerformanceOverhead.Normal), ("SunDisk", "Draws a sun texture and halo in the sky.", PerformanceOverhead.Lightweight), ("MoonDisk", "Draws a moon texture and halo in the sky.", PerformanceOverhead.Normal));
            #endregion
            #region Fog
            DrawLayerHeader(serializedObject.FindProperty("useFog"), "Fog", ("Gradient", "Set the gradient color of the stylized fog.", PerformanceOverhead.Lightweight), ("SunLightFlare", "Adds a flare around the sun.", PerformanceOverhead.Lightweight), ("MoonLightFlare", "Adds a flare around the moon.", PerformanceOverhead.Lightweight), ("Volumetric", "Adds volumetric raymarching to the fog.", PerformanceOverhead.Heavy), ("TwoTone", "Splits the fog into two colors based on the sun direction.", PerformanceOverhead.Lightweight), ("Height", "Adds height fog to your world.", PerformanceOverhead.Normal), ("Base", "Control the base variables of the fog.", PerformanceOverhead.Lightweight));
            #endregion
            #region Clouds
            DrawLayerHeader(serializedObject.FindProperty("useClouds"), "Clouds", ("Volumetric", "Adds volumetric support to the clouds", PerformanceOverhead.Heavy), ("VolumetricHighlights", "Adds raymarched lighting to the clouds. Requires volumetric clouds", PerformanceOverhead.Heavy), ("VolumetricSelfShadowing", "Adds raymarched self-shadowing to the clouds. Requires volumetric clouds", PerformanceOverhead.Heavy), ("Cumulonimbus", "Massive dark storm clouds that form on the horizon. Requires volumetric clouds", PerformanceOverhead.Heavy), ("Cumulus", "Draws a 2D cloud layer based on the properties of cumulus clouds", PerformanceOverhead.Normal), ("Altocumulus", "Draws a 2D cloud layer based on the properties of altocumulus clouds", PerformanceOverhead.Lightweight), ("Cirrus", "Draws a 2D cloud layer based on the properties of cirrus clouds", PerformanceOverhead.Lightweight), ("Contrails", "Add 2D contrails to your skybox", PerformanceOverhead.Lightweight));
            #endregion
            #region Sunshafts
            DrawLayerHeader(serializedObject.FindProperty("useSunshafts"), "Sunshafts", ("Screenspace", "Adds a sunshafts post effect to your world.", PerformanceOverhead.Normal));
            #endregion
// END GENERATED CODE


            return root;
        }

        void PushToInstance()
        {
            if (CozyWeather.Instance != null)
                CozyWeather.Instance.ResetQuality();

            if (CozyWeather.Instance.GetModule(out CozyAtmosphereModule module))
                module.UpdateModel();
                
            CozyWeatherEditor.RedrawAllUI();
        }

        public void DrawLayerHeader(SerializedProperty property, string label, params (string, string, PerformanceOverhead)[] properties)
        {
            VisualElement header = new();
            header.AddToClassList("styleHeader");
            Label headerLabel = new Label(label);
            Toggle headerToggle = new Toggle();
            VisualElement container = new();
            headerToggle.SetValueWithoutNotify(property.boolValue);
            headerToggle.RegisterValueChangedCallback(evt =>
            {
                property.boolValue = evt.newValue;
                container.SetEnabled(property.boolValue);
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
                PushToInstance();
            });
            header.Add(headerLabel);
            header.Add(headerToggle);
            scrollView.Add(header);
            VisualElement activeToggleContainer = new VisualElement();
            activeToggleContainer.AddToClassList("active-toggle-container");
            Label toggleAll = new Label("ALL");
            Label toggleNone = new Label("NONE");
            toggleAll.RegisterCallback<ClickEvent>(evt =>
            {
                foreach ((string name, string desc, PerformanceOverhead overhead) prop in properties)
                    serializedObject.FindProperty($"use{label}{prop.name}").boolValue = true;
                container.Query<VisualElement>(className: "styleToggle").ForEach(evt => evt.AddToClassList("active"));
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
                PushToInstance();
            });
            toggleNone.RegisterCallback<ClickEvent>(evt =>
            {
                foreach ((string name, string desc, PerformanceOverhead overhead) prop in properties)
                    serializedObject.FindProperty($"use{label}{prop.name}").boolValue = false;

                container.Query<VisualElement>(className: "styleToggle").ForEach(evt => evt.RemoveFromClassList("active"));
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
                PushToInstance();
            });
            activeToggleContainer.Add(toggleAll);
            activeToggleContainer.Add(toggleNone);
            container.Add(activeToggleContainer);
            container.SetEnabled(property.boolValue);
            scrollView.Add(container);

            foreach ((string name, string desc, PerformanceOverhead overhead) prop in properties)
            {
                container.Add(FeatureToggle($"{label}{prop.name}", serializedObject.FindProperty($"use{label}{prop.name}"), prop.name, prop.desc, prop.overhead));
            }
        }

        public VisualElement FeatureToggle(string resourceName, SerializedProperty property, string featureName, string featureDescription, PerformanceOverhead overhead)
        {

            VisualElement container = new();
            container.AddToClassList("styleToggle");
            container.AddToClassList(overhead.ToString());

            if (property.boolValue)
            {
                container.AddToClassList("active");
            }

            container.RegisterCallback<ClickEvent>(evt =>
            {
                property.boolValue = !property.boolValue;
                if (property.boolValue)
                {
                    container.AddToClassList("active");
                }
                else
                    container.RemoveFromClassList("active");
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
                PushToInstance();
            });

            VisualElement checkmark = new VisualElement();
            checkmark.name = "checkmark";
            container.Add(checkmark);

            VisualElement icon = new VisualElement();
            icon.name = "icon";
            icon.style.backgroundImage = (Texture2D)Resources.Load($"COZY Icons/{resourceName}");
            VisualElement textContainer = new VisualElement();
            textContainer.name = "textContainer";
            VisualElement labelContainer = new VisualElement();
            labelContainer.style.flexDirection = FlexDirection.Row;
            labelContainer.style.alignItems = Align.Center;
            labelContainer.style.marginBottom = 4;
            textContainer.Add(labelContainer);
            Label label = new Label();
            label.name = "title";
            label.text = featureName;
            Label overheadLabel = new Label();
            overheadLabel.name = "overhead";
            overheadLabel.text = overhead.ToString();
            Label description = new Label();
            description.name = "description";
            description.text = featureDescription;
            labelContainer.Add(label);
            labelContainer.Add(overheadLabel);
            textContainer.Add(description);
            container.Add(icon);
            container.Add(textContainer);

            return container;

        }

    }
}