//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Linq;
using System;
using System.Reflection;
using System.Collections.Generic;

#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(CozyWeatherModule))]
#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    public class CozyWeatherModuleEditor : CozyBiomeModuleEditor
    {

        CozyWeatherModule weatherModule;

        static Gradient WeatherKeyColors = new Gradient()
        {
            colorKeys = new GradientColorKey[8] {
                new GradientColorKey(Branding.deepBlue,0f/7f),
                new GradientColorKey(Branding.red,1f/7f),
                new GradientColorKey(Branding.yellow,2f/7f),
                new GradientColorKey(Branding.green,3f/7f),
                new GradientColorKey(Branding.purple,4f/7f),
                new GradientColorKey(Branding.blue,5f/7f),
                new GradientColorKey(Branding.orange,6f/7f),
                new GradientColorKey(Branding.deepBlue,1f)
            }
        };

        static readonly Color[] WeightPalette = new Color[]
{
            Branding.blue,
            Branding.orange,
            Branding.green,
            Branding.purple,
            Branding.yellow,
            Branding.red,
            Branding.deepBlue
};

        VisualElement root;
        VisualElement localWeatherWeightsContainer;
        VisualElement profileDisplayContainer;
        string lastWeightsSignature;
        WeatherProfile lastDisplayedProfile;
        int dynamicIconWeatherType;

        void OnEnable()
        {
            if (!target)
                return;

            weatherModule = (CozyWeatherModule)target;
        }


        public override ModuleWidget[] ModuleWidgets => new ModuleWidget[]
        {
            new(
                ModuleWidget.WidgetSize.Small, "Current Weather", true,
                () => {
                    VisualElement container = new();
                    container.style.alignItems = Align.Center;
                    container.style.justifyContent = Justify.Center;

                    container.RegisterCallback<ClickEvent>(evt =>
                    {
                        if (weatherModule.ForecastProfile == null)
                        {
                            Debug.Log("Select a Forecast Profile first");
                            return;
                        }

                        GenericMenu menu = new GenericMenu();

                        bool hasCurrentWeather = weatherModule.CurrentLocalProfile;

                        foreach (WeatherProfile profile in weatherModule.ForecastProfile.ForecastableWeatherProfiles)
                        {
                            if (profile == null) continue;
                            menu.AddItem(new GUIContent(profile.name), hasCurrentWeather & profile == weatherModule.CurrentLocalProfile, () =>
                            {
                                SetPreviewWeather(profile);
                                container.Q<VisualElement>("icon").style.backgroundImage = weatherModule.CurrentLocalProfile.icon;
                                container.Q<Label>("label").text = weatherModule.CurrentLocalProfile.name;
                            });
                        }

                        menu.ShowAsContext();
                    });

                    VisualElement icon = new();
                    icon.name = "icon";
                    icon.style.width = 48;
                    icon.style.height = 48;
                    icon.style.marginTop = 5;
                    icon.style.marginBottom = 7;
                    if (weatherModule.CurrentLocalProfile != null)
                        icon.style.backgroundImage = weatherModule.CurrentLocalProfile.icon;
                    container.Add(icon);

                    Label label = new Label();
                    label.name = "label";
                    label.style.unityFontStyleAndWeight = FontStyle.Bold;
                    if (weatherModule.CurrentLocalProfile != null)
                        label.text = weatherModule.CurrentLocalProfile.name;
                    else
                        label.text = "No Weather";
                    container.Add(label);

                    return container;
                },
                (widget) =>
                {
                    if (weatherModule.CurrentLocalProfile == null)
                    {
                        widget.container.Q<VisualElement>("icon").style.backgroundImage = null;
                        widget.container.Q<Label>("label").text = "No Weather";
                        return;
                    }
                    widget.container.Q<VisualElement>("icon").style.backgroundImage = weatherModule.CurrentLocalProfile.icon;
                    widget.container.Q<Label>("label").text = weatherModule.CurrentLocalProfile.name;
                }
            ),
        };
        public override VisualElement DisplayUI()
        {
            lastWeightsSignature = null;
            lastDisplayedProfile = null;
            root = new VisualElement();
            localWeatherWeightsContainer = new VisualElement();

            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Modules/UXML/weather-module-editor.uxml"
            );

            asset.CloneTree(root);

            profileDisplayContainer = new VisualElement();
            profileDisplayContainer.AddToClassList("profileDisplay");
            root.Add(profileDisplayContainer);

            VisualElement background = new VisualElement();
            background.name = "background";
            profileDisplayContainer.Add(background);

            VisualElement dropdown = new VisualElement();
            dropdown.name = "dropdown";
            profileDisplayContainer.Add(dropdown);

            VisualElement icon = new VisualElement();
            icon.name = "icon";
            profileDisplayContainer.Add(icon);

            Label title = new Label();
            title.name = "title";
            profileDisplayContainer.Add(title);

            Label description = new Label();
            description.name = "description";
            profileDisplayContainer.Add(description);

            profileDisplayContainer.RegisterCallback<ClickEvent>(evt =>
            {
                if (weatherModule.ForecastProfile == null)
                {
                    Debug.Log("Select a Forecast Profile first");
                    return;
                }

                GenericMenu menu = new GenericMenu();

                bool hasCurrentWeather = weatherModule.CurrentLocalProfile;

                foreach (WeatherProfile profile in weatherModule.ForecastProfile.ForecastableWeatherProfiles)
                {
                    if (profile == null) continue;

                    menu.AddItem(new GUIContent(profile.name), hasCurrentWeather & profile == weatherModule.CurrentLocalProfile, () =>
                    {
                        SetPreviewWeather(profile);
                        UpdateLocalWeatherWeightsSection();
                        RefreshInspector();
                    });
                }

                menu.ShowAsContext();
            });

            profileDisplayContainer.RegisterCallback<ContextClickEvent>(evt =>
            {
                if (weatherModule.CurrentLocalProfile)
                {
                    GenericMenu menu = new GenericMenu();

                    menu.AddItem(new GUIContent("Select in Project"), false, () =>
                    {
                        Selection.activeObject = weatherModule.CurrentLocalProfile;
                    });

                    menu.ShowAsContext();
                }
            });

            RefreshInspector();


            Label forecastProfileLabel = new Label("Forecast");
            forecastProfileLabel.AddToClassList("h1");
            root.Add(forecastProfileLabel);

            VisualElement forecastProfileContainer = new VisualElement();
            forecastProfileContainer.AddToClassList("section-bg");
            root.Add(forecastProfileContainer);

            PropertyField forecastProfile = new PropertyField();
            forecastProfile.BindProperty(serializedObject.FindProperty("forecastProfile"));
            forecastProfileContainer.Add(forecastProfile);

            Label localWeightsLabel = new Label("Local Weather Weights");
            localWeightsLabel.AddToClassList("h1");
            root.Add(localWeightsLabel);

            localWeatherWeightsContainer.name = "local-weather-weights";
            localWeatherWeightsContainer.AddToClassList("section-bg");
            root.Add(localWeatherWeightsContainer);
            UpdateLocalWeatherWeightsSection();

            Label modeLabel = new Label("Mode Controls");
            modeLabel.AddToClassList("h1");
            root.Add(modeLabel);

            VisualElement selectionContainer = new VisualElement();
            selectionContainer.AddToClassList("section-bg");
            root.Add(selectionContainer);

            PropertyField weatherChangeMode = new PropertyField();
            weatherChangeMode.BindProperty(serializedObject.FindProperty("weatherChangeMode"));
            selectionContainer.Add(weatherChangeMode);

            PropertyField weatherChangeTime = new PropertyField();
            weatherChangeTime.BindProperty(serializedObject.FindProperty("weatherChangeTime"));
            weatherChangeTime.style.marginLeft = 15;
            weatherChangeTime.style.display = ((WeatherChangeMode)serializedObject.FindProperty("weatherChangeMode").intValue == WeatherChangeMode.ChangeDaily) ? DisplayStyle.Flex : DisplayStyle.None;
            weatherChangeTime.RegisterValueChangeCallback(evt =>
            {
            });
            selectionContainer.Add(weatherChangeTime);

            PropertyField weatherSelectionMode = new PropertyField();
            weatherSelectionMode.BindProperty(serializedObject.FindProperty("nextWeatherSelectionMode"));
            weatherSelectionMode.RegisterValueChangeCallback(evt =>
            {
            });
            selectionContainer.Add(weatherSelectionMode);

            PropertyField weatherInitializationBehavior = new PropertyField();
            weatherInitializationBehavior.BindProperty(serializedObject.FindProperty("weatherInitializationBehavior"));
            selectionContainer.Add(weatherInitializationBehavior);

            PropertyField startingWeather = new PropertyField();
            startingWeather.BindProperty(serializedObject.FindProperty("startingWeather"));
            startingWeather.style.marginLeft = 15;
            startingWeather.style.display = ((WeatherInitializationBehavior)serializedObject.FindProperty("weatherInitializationBehavior").intValue == WeatherInitializationBehavior.SetWeather) ? DisplayStyle.Flex : DisplayStyle.None;
            startingWeather.RegisterValueChangeCallback(evt =>
            {
            });
            selectionContainer.Add(startingWeather);

            PropertyField startingForecast = new PropertyField();
            startingForecast.BindProperty(serializedObject.FindProperty("startingForecast"));
            startingForecast.style.marginLeft = 15;
            startingForecast.style.display = ((WeatherInitializationBehavior)serializedObject.FindProperty("weatherInitializationBehavior").intValue == WeatherInitializationBehavior.SetForecast) ? DisplayStyle.Flex : DisplayStyle.None;
            startingForecast.RegisterValueChangeCallback(evt =>
            {
            });
            selectionContainer.Add(startingForecast);

            PropertyField weatherSpeedMultiplier = new PropertyField();
            weatherSpeedMultiplier.BindProperty(serializedObject.FindProperty("timerSpeedMultiplier"));
            weatherSpeedMultiplier.style.marginTop = 10;
            selectionContainer.Add(weatherSpeedMultiplier);

            PropertyField weatherTransitionTime = new PropertyField();
            weatherTransitionTime.BindProperty(serializedObject.FindProperty("transitionTime"));
            selectionContainer.Add(weatherTransitionTime);

            weatherChangeMode.RegisterValueChangeCallback(evt =>
            {
                weatherChangeTime.style.display = ((WeatherChangeMode)serializedObject.FindProperty("weatherChangeMode").intValue == WeatherChangeMode.ChangeDaily) ? DisplayStyle.Flex : DisplayStyle.None;
            });

            weatherInitializationBehavior.RegisterValueChangeCallback(evt =>
            {
                startingWeather.style.display = ((WeatherInitializationBehavior)serializedObject.FindProperty("weatherInitializationBehavior").intValue == WeatherInitializationBehavior.SetWeather) ? DisplayStyle.Flex : DisplayStyle.None;
                startingForecast.style.display = ((WeatherInitializationBehavior)serializedObject.FindProperty("weatherInitializationBehavior").intValue == WeatherInitializationBehavior.SetForecast) ? DisplayStyle.Flex : DisplayStyle.None;
            });

            root.schedule.Execute(() =>
            {
                if (!Application.isPlaying || weatherModule == null) return;

                if (weatherModule.CurrentLocalProfile != lastDisplayedProfile)
                    RefreshInspector();

                if (ComputeWeightsSignature() != lastWeightsSignature)
                    UpdateLocalWeatherWeightsSection();
            }).Every(500);

            return root;
        }

        void SetPreviewWeather(WeatherProfile profile)
        {
            Undo.RecordObject(weatherModule, "Set Preview Weather");
            weatherModule.EditorPreviewProfile = profile;
            EditorUtility.SetDirty(weatherModule);
            weatherModule.SetProfile(profile);
        }


        public void RefreshInspector()
        {
            if (!weatherModule) return;

            if (!weatherModule.CurrentLocalProfile)
            {
                profileDisplayContainer.Q<Label>("title").text = "Select a Profile to Preview";
                profileDisplayContainer.Q<Label>("description").text = "Preview weather will not persist once the game is playing.";
                profileDisplayContainer.Q<VisualElement>("icon").style.backgroundImage = null;
                // profileDisplayContainer.Q<VisualElement>("background").style.backgroundImage = null;
                return;
            }

            profileDisplayContainer.Q<Label>("title").text = weatherModule.CurrentLocalProfile.name;
            profileDisplayContainer.Q<Label>("description").text = weatherModule.CurrentLocalProfile.description;
            profileDisplayContainer.Q<VisualElement>("icon").style.backgroundImage = weatherModule.CurrentLocalProfile.icon;
            profileDisplayContainer.Q<VisualElement>("background").style.backgroundImage = Resources.Load($"Weather Backgrounds/{weatherModule.CurrentLocalProfile.name}") as Texture2D;
        }
        string ComputeWeightsSignature()
        {
            if (weatherModule == null || weatherModule.LocalProfileWeights == null) return string.Empty;

            var parts = new List<string>();
            foreach (var kvp in weatherModule.LocalProfileWeights)
            {
                if (kvp.Key == null) continue;
                parts.Add($"{kvp.Key.GetEntityId()}:{Convert.ToSingle(kvp.Value.weight):0.###}");
            }
            return string.Join("|", parts);
        }

        void UpdateLocalWeatherWeightsSection()
        {
            if (localWeatherWeightsContainer == null) return;
            localWeatherWeightsContainer.Clear();
            lastWeightsSignature = ComputeWeightsSignature();

            if (weatherModule == null) return;

            if (weatherModule.ForecastProfile == null)
                return;

            var entries = new List<(WeatherProfile profile, float weight)>();
            if (weatherModule.LocalProfileWeights != null)
            {
                foreach (var kvp in weatherModule.LocalProfileWeights)
                {
                    if (kvp.Key == null) continue;
                    entries.Add((kvp.Key, Mathf.Max(0f, Convert.ToSingle(kvp.Value.weight))));
                }
            }

            if (entries.Count == 0)
            {
                Label label = new Label("No local weather weights available.");
                localWeatherWeightsContainer.Add(label);
                return;
            }

            entries = entries.OrderByDescending(e => e.weight).ToList();
            float total = entries.Sum(e => e.weight);

            var colorIndex = entries
                .Select(e => e.profile)
                .OrderBy(p => p.name, StringComparer.Ordinal)
                .Select((p, i) => (p, i))
                .ToDictionary(x => x.p, x => WeightPalette[x.i % WeightPalette.Length]);

            // Stacked proportional bar
            VisualElement bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.height = 12;
            bar.style.marginTop = 2;
            bar.style.marginBottom = 8;
            bar.style.overflow = Overflow.Hidden;
            bar.style.backgroundColor = new Color(0f, 0f, 0f, 0.3f);
            bar.style.borderBottomLeftRadius = 6;
            bar.style.borderBottomRightRadius = 6;
            bar.style.borderTopLeftRadius = 6;
            bar.style.borderTopRightRadius = 6;
            localWeatherWeightsContainer.Add(bar);

            foreach (var entry in entries)
            {
                if (entry.weight <= 0f) continue;

                float pct = total > 0f ? entry.weight / total : 0f;
                VisualElement segment = new VisualElement();
                segment.style.flexGrow = entry.weight;
                segment.style.flexBasis = 0;
                segment.style.backgroundColor = colorIndex[entry.profile];
                segment.tooltip = $"{entry.profile.name}: {pct * 100f:0.#}%";
                bar.Add(segment);
            }

            // Detail rows
            foreach (var entry in entries)
            {
                float pct = total > 0f ? entry.weight / total : 0f;
                Color color = colorIndex[entry.profile];

                VisualElement row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.paddingTop = 3;
                row.style.paddingBottom = 3;
                row.style.opacity = entry.weight > 0f ? 1f : 0.45f;
                row.tooltip = $"Raw weight: {entry.weight:0.##}";

                VisualElement swatch = new VisualElement();
                swatch.style.width = 8;
                swatch.style.height = 8;
                swatch.style.marginRight = 6;
                swatch.style.backgroundColor = color;
                swatch.style.borderBottomLeftRadius = 4;
                swatch.style.borderBottomRightRadius = 4;
                swatch.style.borderTopLeftRadius = 4;
                swatch.style.borderTopRightRadius = 4;
                row.Add(swatch);

                VisualElement profileIcon = new VisualElement();
                profileIcon.style.width = 16;
                profileIcon.style.height = 16;
                profileIcon.style.marginRight = 6;
                profileIcon.style.backgroundImage = entry.profile.icon;
                row.Add(profileIcon);

                Label profileName = new Label(entry.profile.name);
                profileName.style.flexGrow = 1;
                row.Add(profileName);

                VisualElement track = new VisualElement();
                track.style.width = 80;
                track.style.height = 4;
                track.style.marginLeft = 8;
                track.style.marginRight = 8;
                track.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
                track.style.borderBottomLeftRadius = 2;
                track.style.borderBottomRightRadius = 2;
                track.style.borderTopLeftRadius = 2;
                track.style.borderTopRightRadius = 2;

                VisualElement fill = new VisualElement();
                fill.style.height = Length.Percent(100);
                fill.style.width = Length.Percent(pct * 100f);
                fill.style.backgroundColor = color;
                fill.style.borderBottomLeftRadius = 2;
                fill.style.borderBottomRightRadius = 2;
                fill.style.borderTopLeftRadius = 2;
                fill.style.borderTopRightRadius = 2;
                track.Add(fill);
                row.Add(track);

                Label percent = new Label($"{pct * 100f:0.#}%");
                percent.style.minWidth = 44;
                percent.style.unityTextAlign = TextAnchor.MiddleRight;
                row.Add(percent);

                localWeatherWeightsContainer.Add(row);
            }
        }

        public override void OpenDocumentationURL()
        {
            Application.OpenURL("https://distant-lands.gitbook.io/cozy-weather-4/using-cozy/modules/weather-module");
        }
    }
}