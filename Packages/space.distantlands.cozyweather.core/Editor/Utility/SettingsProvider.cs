//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine;
using System.Reflection;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DistantLands.Cozy.EditorScripts
{
    // Register a SettingsProvider using UIElements for the drawing framework:
    static class CozySettingsRegister
    {

        public static PropertyField NewPropertyField(SerializedObject serializedObject, string propertyName)
        {
            PropertyField field = new PropertyField(serializedObject.FindProperty(propertyName));
            field.RegisterValueChangeCallback(evt => CozyWeatherEditor.RedrawAllUI());
            return field;
        }

        public static System.Action<string, VisualElement> RuntimePreferencesProvider(string searchContext, VisualElement rootElement)
        {
            var settings = CozyPreferences.GetSerializedSettings();

            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Window/UXML/CozyPreferences.uxml"
            );
            asset.CloneTree(rootElement);

            ScrollView scroll = new ScrollView();
            rootElement.Add(scroll);

            Label orientation = new Label("Orientation");
            orientation.AddToClassList("h1");
            scroll.Add(orientation);

            var orientationProperties = new VisualElement();
            scroll.Add(orientationProperties);

            orientationProperties.Add(NewPropertyField(settings, "skyPitch"));
            orientationProperties.Add(NewPropertyField(settings, "skyRotation"));

            Label lighting = new Label("Lighting");
            lighting.AddToClassList("h1");
            scroll.Add(lighting);

            var lightingProperties = new VisualElement();
            scroll.Add(lightingProperties);

            lightingProperties.Add(NewPropertyField(settings, "handleSceneLighting"));
            lightingProperties.Add(NewPropertyField(settings, "staticLighting"));
            PropertyField staticLightingRotation = NewPropertyField(settings, "staticLightingRotation");
            staticLightingRotation.style.display = CozyPreferences.StaticLighting ? DisplayStyle.Flex : DisplayStyle.None;
            lightingProperties.Add(staticLightingRotation);
            lightingProperties.Add(NewPropertyField(settings, "minLightAngle"));
            var quantizeLightingField = NewPropertyField(settings, "quantizeLighting");
            lightingProperties.Add(quantizeLightingField);
            PropertyField quantizeLightingAngle = NewPropertyField(settings, "quantizeLightingAngle");
            quantizeLightingAngle.style.display = CozyPreferences.QuantizeLighting ? DisplayStyle.Flex : DisplayStyle.None;
            quantizeLightingField.RegisterValueChangeCallback(evt =>
            {
                quantizeLightingAngle.style.display = CozyPreferences.QuantizeLighting ? DisplayStyle.Flex : DisplayStyle.None;
            });
            lightingProperties.Add(quantizeLightingAngle);

            Label time = new Label("Time");
            time.AddToClassList("h1");
            scroll.Add(time);

            var timeProperties = new VisualElement();
            scroll.Add(timeProperties);

            PropertyField monthsField = new PropertyField(settings.FindProperty("months"));
            monthsField.RegisterValueChangeCallback(evt =>
            {
                var months = settings.FindProperty("months");
                bool corrected = false;
                if (months.arraySize == 0)
                {
                    months.arraySize = 1;
                    corrected = true;
                }
                for (int i = 0; i < months.arraySize; i++)
                {
                    var days = months.GetArrayElementAtIndex(i).FindPropertyRelative("days");
                    if (days.intValue < 1)
                    {
                        days.intValue = 1;
                        corrected = true;
                    }
                }
                if (corrected)
                    Debug.LogWarning("A COZY calendar must have at least one month, with at least one day in each month.");
                settings.ApplyModifiedProperties();
                settings.Update();
                CozyPreferences.ResetDaysPerYear();
                CozyWeatherEditor.RedrawAllUI();
            });
            timeProperties.Add(monthsField);
            timeProperties.Add(NewPropertyField(settings, "startingWeekday"));

            timeProperties.Add(NewPropertyField(settings, "dawnTimes"));
            timeProperties.Add(NewPropertyField(settings, "morningTimes"));
            timeProperties.Add(NewPropertyField(settings, "dayTimes"));
            timeProperties.Add(NewPropertyField(settings, "afternoonTimes"));
            timeProperties.Add(NewPropertyField(settings, "eveningTimes"));
            timeProperties.Add(NewPropertyField(settings, "twilightTimes"));
            timeProperties.Add(NewPropertyField(settings, "nightTimes"));

            Label fx = new Label("FX");
            fx.AddToClassList("h1");
            scroll.Add(fx);

            var fxBlocking = new VisualElement();
            scroll.Add(fxBlocking);
            fxBlocking.Add(NewPropertyField(settings, "fxRenderDistance"));
            fxBlocking.Add(NewPropertyField(settings, "fxRenderHeight"));
            fxBlocking.Add(NewPropertyField(settings, "renderFXBlocking"));

            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset != null)
            {
                var propertyInfo = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
                var rendererDataList = propertyInfo?.GetValue(urpAsset) as ScriptableRendererData[];

                var choices = new List<string>();

                if (rendererDataList != null)
                {
                    for (int i = 0; i < rendererDataList.Length; i++)
                    {
                        string rendererName = rendererDataList[i] != null ? rendererDataList[i].name : "Null";
                        choices.Add($"{i}: {rendererName}");
                    }
                }

                int currentIndex = System.Math.Clamp(settings.FindProperty("fxBlockingRendererIndex").intValue, 0, System.Math.Max(0, choices.Count - 1));

                var dropdown = new DropdownField();
                dropdown.label = "Renderer Index";
                dropdown.choices = choices;
                dropdown.index = settings.FindProperty("fxBlockingRendererIndex").intValue;
                dropdown.RegisterValueChangedCallback(evt =>
                {
                    settings.Update();
                    settings.FindProperty("fxBlockingRendererIndex").intValue = choices.IndexOf(evt.newValue);
                    settings.ApplyModifiedProperties();
                });

                fxBlocking.Add(dropdown);
            }


            fxBlocking.Add(NewPropertyField(settings, "fxBlockingLayerMask"));

            Label rendering = new Label("Rendering");
            rendering.AddToClassList("h1");
            scroll.Add(rendering);

            VisualElement renderingSettings = new VisualElement();
            scroll.Add(renderingSettings);

            renderingSettings.Add(NewPropertyField(settings, "fogDownsampling"));
            renderingSettings.Add(NewPropertyField(settings, "cloudDownsampling"));
            renderingSettings.Add(NewPropertyField(settings, "skyFogAmount"));
            renderingSettings.Add(NewPropertyField(settings, "cloudsFogAmount"));

            rootElement.Bind(settings);

            return null;
        }

        [SettingsProvider]
        public static SettingsProvider CozyPreferencesProvider()
        {

            var provider = new SettingsProvider("Project/COZY: Stylized Weather 4/Runtime", SettingsScope.Project)
            {
                label = "Runtime",
                activateHandler = (searchContext, rootElement) => RuntimePreferencesProvider(searchContext, rootElement),
                keywords = new HashSet<string>(new[] { "COZY", "Weather", "Temperature", "Gizmos", "Calendar" })
            };

            return provider;
        }
        [SettingsProvider]
        public static SettingsProvider CozyEditorProvider()
        {

            var provider = new SettingsProvider("Project/COZY: Stylized Weather 4/Editor", SettingsScope.Project)
            {
                label = "Editor",
                activateHandler = (searchContext, rootElement) =>
                {
                    var settings = CozyPreferences.GetSerializedSettings();

                    VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                        "Packages/space.distantlands.cozyweather.core/Editor/UI/Window/UXML/CozyPreferences.uxml"
                    );
                    asset.CloneTree(rootElement);

                    ScrollView scroll = new ScrollView();
                    rootElement.Add(scroll);

                    Label display = new Label("Editor Display");
                    display.AddToClassList("h1");
                    scroll.Add(display);

                    scroll.Add(NewPropertyField(settings, "updateInEditMode"));
                    scroll.Add(NewPropertyField(settings, "dontDestroyWeatherOnLoad"));
                    scroll.Add(NewPropertyField(settings, "fog"));
                    scroll.Add(NewPropertyField(settings, "followCamera"));
                    scroll.Add(NewPropertyField(settings, "gizmos"));
                    scroll.Add(NewPropertyField(settings, "tooltips"));
                    scroll.Add(NewPropertyField(settings, "graphs"));

                    Label units = new Label("Units");
                    units.AddToClassList("h1");
                    scroll.Add(units);

                    var unitProperties = new VisualElement();
                    scroll.Add(unitProperties);

                    unitProperties.Add(NewPropertyField(settings, "temperatureUnit"));
                    unitProperties.Add(NewPropertyField(settings, "pressureUnit"));
                    unitProperties.Add(NewPropertyField(settings, "timeUnit"));
                    unitProperties.Add(NewPropertyField(settings, "showSeconds"));
                    unitProperties.Add(NewPropertyField(settings, "showMilliseconds"));
                    unitProperties.Add(NewPropertyField(settings, "dateFormat"));

                    Label calendar = new Label("Display Calendars");
                    calendar.AddToClassList("h1");
                    scroll.Add(calendar);

                    var calendarProperties = new VisualElement();
                    scroll.Add(calendarProperties);

                    var weatherEventsField = NewPropertyField(settings, "displayWeatherEvents");
                    weatherEventsField.label = "Weather Events";
                    calendarProperties.Add(weatherEventsField);

                    var holidayEventsField = NewPropertyField(settings, "displayHolidayEvents");
                    holidayEventsField.label = "Holiday Events";
                    calendarProperties.Add(holidayEventsField);

                    var statusEventsField = NewPropertyField(settings, "displayStatusEvents");
                    statusEventsField.label = "Status Events";
                    calendarProperties.Add(statusEventsField);

                    var worldEventsField = NewPropertyField(settings, "displayWorldEventEvents");
                    worldEventsField.label = "World Events";
                    calendarProperties.Add(worldEventsField);

                    var playerEventsField = NewPropertyField(settings, "displayPlayerEvents");
                    playerEventsField.label = "Player Events";
                    calendarProperties.Add(playerEventsField);

                    var npcEventsField = NewPropertyField(settings, "displayNPCEvents");
                    npcEventsField.label = "NPC Events";
                    calendarProperties.Add(npcEventsField);

                    var enemyEventsField = NewPropertyField(settings, "displayEnemyEvents");
                    enemyEventsField.label = "Enemy Events";
                    calendarProperties.Add(enemyEventsField);

                    var ambientEventsField = NewPropertyField(settings, "displayAmbientEvents");
                    ambientEventsField.label = "Ambient Events";
                    calendarProperties.Add(ambientEventsField);

                    var miscEventsField = NewPropertyField(settings, "displayMiscEvents");
                    miscEventsField.label = "Misc. Events";
                    calendarProperties.Add(miscEventsField);

                    rootElement.Bind(settings);

                },
                keywords = new HashSet<string>(new[] { "COZY", "Weather", "Temperature", "Gizmos", "Calendar" })
            };

            return provider;
        }

        [SettingsProvider]
        public static SettingsProvider CozyProvider()
        {
            var provider = new SettingsProvider("Project/COZY: Stylized Weather 4", SettingsScope.Project)
            {
                label = "COZY: Stylized Weather 4",
                activateHandler = (searchContext, rootElement) => RuntimePreferencesProvider(searchContext, rootElement),
                keywords = new HashSet<string>(new[] { "COZY", "Weather", "Styles", "Quality", "Clouds", "Sky", "Lighting" })
            };

            return provider;
        }

        [SettingsProvider]
        public static SettingsProvider CozyStylesProvider()
        {
            var provider = new SettingsProvider("Project/COZY: Stylized Weather 4/Features", SettingsScope.Project)
            {
                label = "Features",
                activateHandler = (searchContext, rootElement) =>
                {
                    var settings = CozyPreferences.GetSerializedSettings();

                    VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                        "Packages/space.distantlands.cozyweather.core/Editor/UI/Window/UXML/CozyStyles.uxml"
                    );
                    asset.CloneTree(rootElement);
                    rootElement.Bind(settings);

                    TabView tabView = rootElement.Q<TabView>();

                    RegenerateStyleSelector();
                    tabView.selectedTabIndex = System.Array.IndexOf(CozyPreferences.Styles, CozyPreferences.CurrentStyle);
                    tabView.activeTabChanged += (oldTab, newTab) =>
                    {
                        if (newTab == null)
                            return;

                        CozyPreferences.SelectStyle(tabView.selectedTabIndex);
                        if (CozyWeather.Instance != null)
                            CozyWeather.Instance.ResetQuality();

                        CozyWeatherEditor.RedrawAllUI();
                    };

                    void RegenerateStyleSelector()
                    {
                        tabView.Clear();

                        for (int i = 0; i < CozyPreferences.Styles.Length; i++)
                        {
                            var tab = new Tab();
                            int index = i;
                            CozyStyle style = CozyPreferences.Styles[index];
                            tab.label = style.name;

                            var editor = Editor.CreateEditor(style);
                            VisualElement inspector = editor.CreateInspectorGUI();
                            inspector.Bind(new SerializedObject(style));

                            tab.Add(inspector);


                            Button button = new Button();
                            button.text = "Delete";
                            button.RegisterCallback<ClickEvent>(evt =>
                            {
                                var parent = CozyPreferences.Instance;
                                settings.Update();

                                // Get the object to remove
                                var elementProp = settings.FindProperty("styles").GetArrayElementAtIndex(index);
                                var toRemove = style;

                                // Remove from the serialized array first
                                settings.FindProperty("styles").DeleteArrayElementAtIndex(index);
                                settings.ApplyModifiedProperties();

                                // If it was a sub-asset, delete it from the parent asset
                                if (toRemove != null)
                                {
                                    Object.DestroyImmediate(toRemove, true);
                                    AssetDatabase.SaveAssets();
                                    AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(parent)); // refresh parent
                                }

                                EditorUtility.SetDirty(parent);
                                RegenerateStyleSelector();
                            });
                            tab.Add(button);

                            tabView.Add(tab);
                        }
                    }

                    VisualElement button = rootElement.Query<VisualElement>("addButton");
                    button.RegisterCallback<ClickEvent>(evt =>
                    {
                        var newOverride = ScriptableObject.CreateInstance(typeof(CozyStyle));
                        newOverride.name = "New Style";

                        AssetDatabase.AddObjectToAsset(newOverride, CozyPreferences.Instance);
                        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(CozyPreferences.Instance)); // refresh
                        AssetDatabase.SaveAssets();

                        settings.Update();
                        settings.FindProperty("styles").arraySize++;
                        settings.FindProperty("styles").GetArrayElementAtIndex(settings.FindProperty("styles").arraySize - 1).objectReferenceValue = newOverride;
                        settings.ApplyModifiedProperties();

                        EditorUtility.SetDirty(CozyPreferences.Instance);
                        RegenerateStyleSelector();
                    });

                },

                // Populate the search keywords to enable smart search filtering and label highlighting:
                keywords = new HashSet<string>(new[] { "COZY", "Weather", "Styles", "Quality", "Clouds", "Sky", "Lighting" })
            };

            return provider;
        }

        [SettingsProvider]
        public static SettingsProvider CozyIntegrationsProvider()
        {
            var provider = new SettingsProvider("Project/COZY: Stylized Weather 4/Integrations", SettingsScope.Project)
            {
                label = "Integrations",

                activateHandler = (searchContext, rootElement) =>
                {
                    var settings = CozyIntegrations.GetSerializedSettings();

                    VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                        "Packages/space.distantlands.cozyweather.core/Editor/UI/Window/UXML/CozyIntegrations.uxml"
                    );
                    asset.CloneTree(rootElement);
                    rootElement.Bind(settings);

                    var integrationsProp = settings.FindProperty("integrations");
                    if (integrationsProp == null) return;

                    if (integrationsProp.arraySize == 0)
                    {
                        Tooltip tooltip = new("No integrations installed currently!", StatusType.Pass);
                        rootElement.Add(tooltip);

                        return;
                    }

                    ScrollView scrollView = new ScrollView();
                    rootElement.Add(scrollView);

                    VisualElement activeContainer = new VisualElement();
                    VisualElement disabledContainer = new VisualElement();

                    scrollView.Add(activeContainer);
                    scrollView.Add(disabledContainer);

                    for (int i = 0; i < integrationsProp.arraySize; i++)
                    {
                        VisualElement container = new VisualElement();
                        container.AddToClassList("integration");

                        var integrationProp = integrationsProp.GetArrayElementAtIndex(i);
                        CozyIntegration integrationRef = (CozyIntegration)integrationProp.managedReferenceValue;
                        CozyIntegrationAttribute attribute = null;

                        if (integrationRef != null)
                        {
                            System.Type currentType = integrationRef.GetType();
                            attribute = currentType.GetCustomAttribute<CozyIntegrationAttribute>();

                            if (attribute == null)
                            {
                                Debug.Log("Integration class requires CozyIntegrationAttribute attribute.");
                                continue;
                            }
                        }

                        container.enabledSelf = integrationRef.PackageInstalled;

                        Foldout foldout = new Foldout();
                        foldout.value = false;
                        container.Add(foldout);

                        VisualElement header = new VisualElement();
                        header.name = "header";
                        foldout.Q<VisualElement>(className: "unity-base-field__input").Add(header);

                        VisualElement titleContainer = new VisualElement();
                        titleContainer.name = "titleContainer";
                        header.Add(titleContainer);

                        VisualElement icon = new VisualElement() { name = "icon" };
                        icon.style.backgroundImage = (Texture2D)Resources.Load(attribute.IconPath);
                        titleContainer.Add(icon);

                        Label label = new Label();
                        label.text = attribute.Name;
                        titleContainer.Add(label);

                        Label statusBar = new Label();
                        statusBar.AddToClassList("integrationsStatus");
                        statusBar.style.marginLeft = 10;
                        if (integrationRef.PackageInstalled)
                        {
                            statusBar.text = "In Project";
                            statusBar.AddToClassList("console-status-good");
                        }
                        else
                            statusBar.style.display = DisplayStyle.None;
                        titleContainer.Add(statusBar);

                        VisualElement statusContainer = new VisualElement();
                        statusContainer.name = "statusContainer";
                        statusContainer.style.justifyContent = Justify.Center;
                        statusContainer.style.alignItems = Align.Center;
                        header.Add(statusContainer);

                        VisualElement statusIcon = new VisualElement();
                        statusIcon.AddToClassList("integrationStatusIcon");
                        var status = integrationRef.CheckStatus(out string message);

                        switch (status)
                        {
                            default:
                                statusIcon.style.display = DisplayStyle.None;
                                break;
                            case StatusType.Info:
                                statusIcon.tooltip = $"{message} \nIntegration for {attribute.Name} will continue to run.";
                                statusIcon.AddToClassList("info");
                                break;
                            case StatusType.Warning:
                                statusIcon.tooltip = $"{message} \nIntegration for {attribute.Name} will continue to run but this warning should be addressed.";
                                statusIcon.AddToClassList("warning");
                                break;
                            case StatusType.Fail:
                                statusIcon.tooltip = $"{message} \nIntegration for {attribute.Name} will not run until this error is resolved.";
                                statusIcon.AddToClassList("error");
                                break;
                        }
                        statusContainer.Add(statusIcon);

                        Button button = new Button();
                        button.style.display = attribute.DisplayApplyButton ? DisplayStyle.Flex : DisplayStyle.None;
                        button.text = integrationRef.IntegrationInstalled ? "Remove" : "Install";
                        button.style.paddingLeft = 5;
                        button.style.paddingRight = 5;
                        button.RegisterCallback<ClickEvent>(evt =>
                        {
                            if (integrationRef.IntegrationInstalled)
                                integrationRef.UnapplyIntegration();
                            else
                                integrationRef.ApplyIntegration();

                            settings.ApplyModifiedProperties();
                            settings.Update();
                        });
                        statusContainer.Add(button);

                        Toggle toggle = new Toggle();
                        toggle.style.display = attribute.DisplayToggle ? DisplayStyle.Flex : DisplayStyle.None;
                        toggle.value = integrationRef.Enabled;
                        toggle.style.marginBottom = 0;
                        toggle.style.marginTop = 0;
                        toggle.RegisterValueChangedCallback(evt =>
                        {
                            integrationRef.SetEnabled(!integrationRef.Enabled);
                            settings.ApplyModifiedProperties();
                            settings.Update();
                        });
                        statusContainer.Add(toggle);

                        VisualElement details = new VisualElement();
                        details.name = "detailsContainer";
                        foldout.Add(details);

                        var property = integrationProp.Copy();
                        var endProperty = property.GetEndProperty();

                        int properties = 0;

                        if (property.NextVisible(true))
                        {
                            do
                            {
                                if (SerializedProperty.EqualContents(property, endProperty))
                                    break;

                                PropertyField propertyField = new PropertyField(property, property.displayName);
                                propertyField.BindProperty(property);
                                details.Add(propertyField);
                                properties++;
                            }
                            while (property.NextVisible(false));
                        }

                        if (properties == 0)
                        {
                            Tooltip tooltip = new Tooltip("No properties for this integration.", StatusType.Info);
                            details.Add(tooltip);
                        }

                        if (integrationRef.PackageInstalled)
                            activeContainer.Add(container);
                        else
                            disabledContainer.Add(container);
                    }

                },

                // Populate the search keywords to enable smart search filtering and label highlighting:
                keywords = new HashSet<string>(new[] { "COZY", "Weather", "Integrations" })
            };

            return provider;
        }

    }

}