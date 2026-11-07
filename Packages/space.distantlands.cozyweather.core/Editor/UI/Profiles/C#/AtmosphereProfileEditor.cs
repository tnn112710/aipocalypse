//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEditor.Experimental.GraphView;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(AtmosphereProfile))]
    [CanEditMultipleObjects]
    public class AtmosphereProfileEditor : Editor
    {
        AtmosphereProfile profile;
        VisualElement root;

        void OnEnable()
        {
            if (target == null || target is not AtmosphereProfile) return;
            
            profile = (AtmosphereProfile)target;
        }

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();

            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Profiles/UXML/atmosphere-profile-editor.uxml"
            );
            asset.CloneTree(root);
            root.style.marginLeft = -15;
            root.style.marginRight = -5;

            Label metaLabel = new Label("Metadata");
            metaLabel.AddToClassList("h1");
            root.Add(metaLabel);

            VisualElement metaSection = new VisualElement();
            metaSection.AddToClassList("section-bg");
            root.Add(metaSection);

            PropertyField icon = new PropertyField();
            icon.BindProperty(serializedObject.FindProperty("icon"));
            metaSection.Add(icon);

            PropertyField category = new PropertyField();
            category.BindProperty(serializedObject.FindProperty("category"));
            metaSection.Add(category);


            PropertyField description = new PropertyField();
            description.BindProperty(serializedObject.FindProperty("description"));
            metaSection.Add(description);

            Label presetLabel = new Label("Preset Settings");
            presetLabel.AddToClassList("h1");
            root.Add(presetLabel);

            // help.RegisterCallback<ClickEvent>(evt => Application.OpenURL($"https://distant-lands.gitbook.io/cozy-weather-4/using-cozy/features/{atmosphereOverride.Category.ToLower()}#{atmosphereOverride.Title}"));
            
            VisualElement parametersSection = new VisualElement();
            parametersSection.AddToClassList("section-bg");
            root.Add(parametersSection);

            Label colorsLabel = new Label("Colors");
            colorsLabel.AddToClassList("h2");
            parametersSection.Add(colorsLabel);

            PropertyField dayColor = new PropertyField();
            dayColor.BindProperty(serializedObject.FindProperty("presetParameters.dayColor"));
            parametersSection.Add(dayColor);

            PropertyField eveningColor = new PropertyField();
            eveningColor.BindProperty(serializedObject.FindProperty("presetParameters.eveningColor"));
            parametersSection.Add(eveningColor);

            PropertyField morningColor = new PropertyField();
            morningColor.BindProperty(serializedObject.FindProperty("presetParameters.morningColor"));
            parametersSection.Add(morningColor);

            PropertyField nightColor = new PropertyField();
            nightColor.BindProperty(serializedObject.FindProperty("presetParameters.nightColor"));
            parametersSection.Add(nightColor);

            Label lightingLabel = new Label("Lighting");
            lightingLabel.AddToClassList("h2");
            parametersSection.Add(lightingLabel);

            PropertyField sunColor = new PropertyField();
            sunColor.BindProperty(serializedObject.FindProperty("presetParameters.sunColor"));
            parametersSection.Add(sunColor);

            PropertyField moonColor = new PropertyField();
            moonColor.BindProperty(serializedObject.FindProperty("presetParameters.moonColor"));
            parametersSection.Add(moonColor);

            PropertyField lightingIntensity = new PropertyField();
            lightingIntensity.BindProperty(serializedObject.FindProperty("presetParameters.lightingIntensity"));
            parametersSection.Add(lightingIntensity);

            Label effectsLabel = new Label("Effects");
            effectsLabel.AddToClassList("h2");
            parametersSection.Add(effectsLabel);

            PropertyField starBrightness = new PropertyField();
            starBrightness.BindProperty(serializedObject.FindProperty("presetParameters.starBrightness"));
            parametersSection.Add(starBrightness);

            PropertyField fogginess = new PropertyField();
            fogginess.BindProperty(serializedObject.FindProperty("presetParameters.fogginess"));
            parametersSection.Add(fogginess);

            PropertyField fogSaturation = new PropertyField();
            fogSaturation.BindProperty(serializedObject.FindProperty("presetParameters.fogSaturation"));
            parametersSection.Add(fogSaturation);

            Label overridesLabel = new Label("Overrides");
            overridesLabel.AddToClassList("h1");
            root.Add(overridesLabel);

            PropertyField overrides = new PropertyField();
            overrides.BindProperty(serializedObject.FindProperty("overrides"));
            root.Add(overrides);
            

            Button addOverrideButton = new Button { text = "Add Override" };
            addOverrideButton.style.marginTop = 7;
            addOverrideButton.RegisterCallback<ClickEvent>(evt =>
            {
                AtmosphereModel model = new AtmosphereModel();
                model.UpdateModel(profile);
                AtmosphereOverrideSearchProvider provider = CreateInstance<AtmosphereOverrideSearchProvider>();
                provider.profile = profile;
                provider.model = model;
                provider.overridesList = profile.overrides;
                provider.arrayProperty = serializedObject.FindProperty("overrides");
                provider.action = () =>
                {
                    serializedObject.Update();
                    overrides.Bind(serializedObject);
                };
                SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)), provider);
            });
            root.Add(addOverrideButton);

            return root;
        }
    }
}