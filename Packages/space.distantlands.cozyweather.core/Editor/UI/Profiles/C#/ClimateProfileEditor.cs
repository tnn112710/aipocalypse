//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System;
using System.Collections.Generic;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(ClimateProfile))]
#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    public class ClimateProfileEditor : Editor
    {
        ClimateProfile climateProfile;
        VisualElement root;

        public VisualElement SeasonalGraph => root.Q<VisualElement>("seasonal-graph");
        public TabView SeasonTabs => root.Q<TabView>();
        public DropdownField GraphType => root.Q<DropdownField>("graphType");

        float dragStartX = 0;
        float initialTime = 0;
        float trackedTime = 0;
        bool isDragging = false;

        public static Gradient temperatureGradient = new Gradient()
        {
            colorKeys = new GradientColorKey[5] {
            new GradientColorKey(Branding.deepRed, 1f),
            new GradientColorKey(Branding.orange, 0.75f),
            new GradientColorKey(Branding.green, 0.5f),
            new GradientColorKey(Branding.blue, 0.25f),
            new GradientColorKey(Branding.deepBlue, 0f)
            }
        };
        public static Gradient springGradient = new Gradient()
        {
            colorKeys = new GradientColorKey[2] {
            new GradientColorKey(Branding.deepGreen, 0f),
            new GradientColorKey(Branding.green, 1f)
            }
        };
        public static Gradient springDisabledGradient = new Gradient()
        {
            colorKeys = new GradientColorKey[2] {
            new GradientColorKey(Disabled(Branding.deepGreen), 0f),
            new GradientColorKey(Disabled(Branding.green), 1f)
            }
        };
        public static Gradient summerGradient = new Gradient()
        {
            colorKeys = new GradientColorKey[2] {
            new GradientColorKey(Branding.orange, 0f),
            new GradientColorKey(Branding.yellow, 1f)
            }
        };
        public static Gradient summerDisabledGradient = new Gradient()
        {
            colorKeys = new GradientColorKey[2] {
            new GradientColorKey(Disabled(Branding.orange), 0f),
            new GradientColorKey(Disabled(Branding.yellow), 1f)
            }
        };
        public static Gradient fallGradient = new Gradient()
        {
            colorKeys = new GradientColorKey[2] {
            new GradientColorKey(Branding.red, 0f),
            new GradientColorKey(Branding.orange, 1f)
            }
        };
        public static Gradient fallDisabledGradient = new Gradient()
        {
            colorKeys = new GradientColorKey[2] {
            new GradientColorKey(Disabled(Branding.red), 0f),
            new GradientColorKey(Disabled(Branding.orange), 1f)
            }
        };
        public static Gradient winterGradient = new Gradient()
        {
            colorKeys = new GradientColorKey[2] {
            new GradientColorKey(Branding.deepBlue, 0f),
            new GradientColorKey(Branding.blue, 1f)
            }
        };
        public static Gradient winterDisabledGradient = new Gradient()
        {
            colorKeys = new GradientColorKey[2] {
            new GradientColorKey(Disabled(Branding.deepBlue), 0f),
            new GradientColorKey(Disabled(Branding.blue), 1f)
            }
        };

        public static Gradient humidityGradient = new Gradient()
        {
            colorKeys = new GradientColorKey[2] {
                new GradientColorKey(Branding.deepBlue, 1),
                new GradientColorKey(Branding.green, 0)
            }
        };
        public static Gradient pressureGradient = new Gradient()
        {
            colorKeys = new GradientColorKey[2] {
                new GradientColorKey(Branding.purple, 1),
                new GradientColorKey(Branding.pink, 0)
            }
        };
        public static Gradient convectiveChanceGradient = new Gradient()
        {
            colorKeys = new GradientColorKey[2] {
                new GradientColorKey(Branding.purple, 1),
                new GradientColorKey(Branding.blue, 0)
            }
        };
        public static Gradient stratiformChanceGradient = new Gradient()
        {
            colorKeys = new GradientColorKey[2] {
                new GradientColorKey(Branding.deepGreen, 1),
                new GradientColorKey(Branding.blue, 0)
            }
        };

        static Color Disabled(Color input)
        {
            return new Color(input.r * 0.7f, input.g * 0.7f, input.b * 0.7f);
        }

        void OnEnable()
        {
            climateProfile = (ClimateProfile)target;
        }

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();

            // 1. Build the UI hierarchy via C#
            BuildNativeUI(root);

            HelpBox invalidDates = new HelpBox(
                "Some season dates are outside the current calendar. The nearest valid dates are used until you correct them in the season tabs.",
                HelpBoxMessageType.Warning);
            root.Insert(0, invalidDates);
            void UpdateDateWarning()
            {
                bool invalid = !climateProfile.spring.startDay.IsValidForCalendar ||
                    !climateProfile.summer.startDay.IsValidForCalendar ||
                    !climateProfile.fall.startDay.IsValidForCalendar ||
                    !climateProfile.winter.startDay.IsValidForCalendar;
                invalidDates.style.display = invalid ? DisplayStyle.Flex : DisplayStyle.None;
            }
            UpdateDateWarning();
            invalidDates.TrackSerializedObjectValue(serializedObject, _ => UpdateDateWarning());

            // 2. Bind the serialized object to the newly created UI
            root.Bind(serializedObject);

            // 3. Set up listeners and interactivity
            if (GraphType != null)
            {
                GraphType.RegisterValueChangedCallback(evt => SeasonalGraph.MarkDirtyRepaint());
            }

            root.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (SeasonalGraph != null) SeasonalGraph.MarkDirtyRepaint();
            });

            int selectedSeason = 0;

            if (CozyWeather.Instance && CozyWeather.Instance.Calendar != null)
            {
                selectedSeason = 0;
                MeridiemDate date = CozyWeather.Instance.Calendar.CurrentDate.Yearless;

                if (date >= climateProfile.spring.StartDay && date < climateProfile.summer.StartDay)
                {
                    selectedSeason = 1;
                }
                if (date >= climateProfile.summer.StartDay && date < climateProfile.fall.StartDay)
                    selectedSeason = 2;
                if (date >= climateProfile.fall.StartDay && date < climateProfile.winter.StartDay)
                    selectedSeason = 3;
            }

            if (SeasonTabs != null)
            {
                SeasonTabs.selectedTabIndex = selectedSeason;
                SeasonTabs.activeTabChanged += (oldTab, newTab) =>
                {
                    SeasonalGraph.MarkDirtyRepaint();
                };
            }

            CreateGraphTabs();

            if (CozyWeather.Instance && CozyWeather.Instance.Calendar != null)
            {
                float width = SeasonalGraph.contentRect.width;
                float height = SeasonalGraph.contentRect.height;

                VisualElement playhead = new VisualElement();
                playhead.name = "Playhead";
                playhead.style.left = new Length((96 * CozyWeather.Instance.Calendar.CurrentDate.YearPercentage) - 2, LengthUnit.Percent);
                playhead.style.width = new Length(15, LengthUnit.Pixel);
                playhead.style.height = new Length(100, LengthUnit.Percent);
                playhead.style.position = Position.Absolute;
                SeasonalGraph.Add(playhead);

                playhead.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    isDragging = true;
                    dragStartX = evt.position.x;
                    initialTime = CozyWeather.Instance.Calendar.CurrentDate.YearPercentage;
                    trackedTime = initialTime;
                    playhead.CapturePointer(evt.pointerId);
                    evt.StopPropagation();
                });

                playhead.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (!isDragging) return;

                    float deltaX = evt.position.x - dragStartX;
                    float timelineWidth = SeasonalGraph.resolvedStyle.width;

                    if (timelineWidth > 0)
                    {
                        float normalizedDelta = deltaX / timelineWidth;
                        trackedTime = Mathf.Clamp01(initialTime + normalizedDelta);
                        CozyWeather.Instance.Calendar.CurrentDate = new MeridiemDate(Mathf.RoundToInt(CozyPreferences.DaysPerYear * trackedTime))
                        {
                            year = CozyWeather.Instance.Calendar.CurrentDate.year
                        };
                        playhead.style.left = new Length(trackedTime * 96, LengthUnit.Percent);
                        SeasonalGraph.MarkDirtyRepaint();
                    }

                    selectedSeason = 0;
                    MeridiemDate date = CozyWeather.Instance.Calendar.CurrentDate.Yearless;

                    if (date >= climateProfile.spring.StartDay && date < climateProfile.summer.StartDay)
                    {
                        selectedSeason = 1;
                    }
                    if (date >= climateProfile.summer.StartDay && date < climateProfile.fall.StartDay)
                        selectedSeason = 2;
                    if (date >= climateProfile.fall.StartDay && date < climateProfile.winter.StartDay)
                        selectedSeason = 3;

                    if (SeasonTabs != null) SeasonTabs.selectedTabIndex = selectedSeason;

                    evt.StopPropagation();
                });

                playhead.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    isDragging = false;
                    CozyWeather.Instance.Calendar.CurrentDate = new MeridiemDate(Mathf.RoundToInt(CozyPreferences.DaysPerYear * trackedTime))
                    {
                        year = CozyWeather.Instance.Calendar.CurrentDate.year
                    };
                    playhead.ReleasePointer(evt.pointerId);
                    evt.StopPropagation();
                });
            }

            void CreateGraphTabs()
            {
                VisualElement CreateSection(Action action)
                {
                    VisualElement sectionElement = new VisualElement();
                    sectionElement.style.position = Position.Absolute;
                    sectionElement.style.top = 0;
                    sectionElement.style.bottom = 0;
                    sectionElement.RegisterCallback<ClickEvent>(evt => action.Invoke());
                    SeasonalGraph.Add(sectionElement);
                    return sectionElement;
                }

                var sections = new[]
                {
                    CreateSection(() => { if (SeasonTabs != null) SeasonTabs.selectedTabIndex = 0; }),
                    CreateSection(() => { if (SeasonTabs != null) SeasonTabs.selectedTabIndex = 1; }),
                    CreateSection(() => { if (SeasonTabs != null) SeasonTabs.selectedTabIndex = 2; }),
                    CreateSection(() => { if (SeasonTabs != null) SeasonTabs.selectedTabIndex = 3; }),
                    CreateSection(() => { if (SeasonTabs != null) SeasonTabs.selectedTabIndex = 0; })
                };
                void UpdateSections()
                {
                    float[] boundaries = { 0f, climateProfile.spring.StartDay.YearPercentage,
                        climateProfile.summer.StartDay.YearPercentage, climateProfile.fall.StartDay.YearPercentage,
                        climateProfile.winter.StartDay.YearPercentage, 1f };
                    for (int i = 0; i < sections.Length; i++)
                    {
                        sections[i].style.left = new Length(boundaries[i] * 100f, LengthUnit.Percent);
                        sections[i].style.right = new Length((1f - boundaries[i + 1]) * 100f, LengthUnit.Percent);
                    }
                    SeasonalGraph.MarkDirtyRepaint();
                }
                UpdateSections();
                root.TrackSerializedObjectValue(serializedObject, _ => UpdateSections());
            }

            SeasonalGraph.generateVisualContent += (MeshGenerationContext context) =>
            {
                float width = SeasonalGraph.contentRect.width;
                float height = SeasonalGraph.contentRect.height;

                var painter = context.painter2D;

                painter.lineWidth = 1;
                painter.strokeColor = new Color(1, 1, 1, 0.2f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(0, height * (1 - 0.32f)));
                painter.LineTo(new Vector2(width, height * (1 - 0.32f)));
                painter.Stroke();
                painter.ClosePath();
                painter.BeginPath();
                painter.MoveTo(new Vector2(0, height * (1 - 0.75f)));
                painter.LineTo(new Vector2(width, height * (1 - 0.75f)));
                painter.Stroke();
                painter.ClosePath();

                int selectedSeason = SeasonTabs != null ? SeasonTabs.selectedTabIndex : 0;

                float stems = Math.Max(1, Math.Min(50, CozyPreferences.DaysPerYear));
                painter.lineWidth = 5;
                painter.lineCap = LineCap.Round;
                for (int i = 0; i < stems; i++)
                {
                    MeridiemDate date = new((int)(i / stems * CozyPreferences.DaysPerYear));
                    painter.strokeGradient = selectedSeason == 0 ? winterGradient : winterDisabledGradient;

                    if (date >= climateProfile.spring.StartDay && date < climateProfile.summer.StartDay)
                        painter.strokeGradient = selectedSeason == 1 ? springGradient : springDisabledGradient;
                    if (date >= climateProfile.summer.StartDay && date < climateProfile.fall.StartDay)
                        painter.strokeGradient = selectedSeason == 2 ? summerGradient : summerDisabledGradient;
                    if (date >= climateProfile.fall.StartDay && date < climateProfile.winter.StartDay)
                        painter.strokeGradient = selectedSeason == 3 ? fallGradient : fallDisabledGradient;

                    if (CozyWeather.Instance && CozyWeather.Instance.Calendar != null)
                    {
                        date.year = CozyWeather.Instance.Calendar.CurrentDate.year;
                    }

                    climateProfile.GetClimateRanges(date, out ClimateRanges climate);

                    float low = 0;
                    float high = 0;

                    if (GraphType != null)
                    {
                        switch (GraphType.index)
                        {
                            case 0:
                                low = climate.temperature.low.Normalized;
                                high = climate.temperature.high.Normalized;
                                break;
                            case 1:
                                low = climate.humidity.low.Normalized;
                                high = climate.humidity.high.Normalized;
                                break;
                            case 2:
                                low = climate.pressure.low.Normalized;
                                high = climate.pressure.high.Normalized;
                                break;
                            case 3:
                                low = climate.uvIndex.low.Normalized;
                                high = climate.uvIndex.high.Normalized;
                                break;
                            case 4:
                                low = climate.airQuality.low.Normalized;
                                high = climate.airQuality.high.Normalized;
                                break;
                        }
                    }

                    painter.BeginPath();
                    painter.MoveTo(new Vector2(width * i / stems + width / stems / 2, height * (1f - low)));
                    painter.LineTo(new Vector2(width * i / stems + width / stems / 2, height * (1f - high)));
                    painter.Stroke();
                    painter.ClosePath();
                }

                if (CozyWeather.Instance && CozyWeather.Instance.Time != null)
                {
                    painter.strokeColor = new Color(1, 1, 1, 0.5f);
                    painter.lineWidth = 2;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(width * CozyWeather.Instance.Calendar.CurrentDate.YearPercentage, height));
                    painter.LineTo(new Vector2(width * CozyWeather.Instance.Calendar.CurrentDate.YearPercentage, 0));
                    painter.Stroke();
                    painter.ClosePath();
                }
            };

            return root;
        }

        private void BuildNativeUI(VisualElement container)
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/space.distantlands.cozyweather.core/Editor/UI/Globals.uss");
            if (styleSheet != null) container.styleSheets.Add(styleSheet);

            VisualElement graphElement = new VisualElement();
            VisualElement seasonalGraph = new VisualElement() { name = "seasonal-graph" };
            seasonalGraph.AddToClassList("section-bg");
            seasonalGraph.style.height = 120;
            graphElement.Add(seasonalGraph);
            container.Add(graphElement);

            DropdownField graphType = new DropdownField(
                null,
                new List<string> { "Temperature", "Humidity", "Pressure", "UV Index", "Air Quality" },
                0
            );
            graphType.name = "graphType";
            container.Add(graphType);

            Label seasonsLabel = new Label("Seasons");
            seasonsLabel.AddToClassList("h1");
            container.Add(seasonsLabel);

            TabView seasonTabs = new TabView();
            seasonTabs.AddToClassList("climate-tabs");

            CreateSeasonTab(seasonTabs, "Winter", "winter");
            CreateSeasonTab(seasonTabs, "Spring", "spring");
            CreateSeasonTab(seasonTabs, "Summer", "summer");
            CreateSeasonTab(seasonTabs, "Fall", "fall");
            container.Add(seasonTabs);

            VisualElement smoothingContainer = new VisualElement();
            smoothingContainer.AddToClassList("section-bg");
            smoothingContainer.Add(new PropertyField() { bindingPath = "temperatureSmoothness", label = "Season Smoothing" });
            container.Add(smoothingContainer);

        }

        private void CreateSeasonTab(TabView parentTabs, string tabName, string bindingPrefix)
        {
            Tab tab = new Tab();
            tab.label = tabName;
            tab.iconImage = Resources.Load($"COZY Icons/{tabName}") as Texture2D;

            Label h2Label = new Label(tabName);
            h2Label.AddToClassList("h2");
            tab.Add(h2Label);

            VisualElement bg = new VisualElement();
            bg.AddToClassList("section-bg");
            bg.style.flexGrow = 1;

            bg.Add(new PropertyField() { bindingPath = $"{bindingPrefix}.startDay" });
            bg.Add(SeasonPropertyField("Temperature", $"{bindingPrefix}.climateInformation.temperature"));
            bg.Add(SeasonPropertyField("Humidity", $"{bindingPrefix}.climateInformation.humidity"));
            bg.Add(SeasonPropertyField("Pressure", $"{bindingPrefix}.climateInformation.pressure"));
            bg.Add(SeasonPropertyField("UV Index", $"{bindingPrefix}.climateInformation.uvIndex", false));
            bg.Add(SeasonPropertyField("Air Quality", $"{bindingPrefix}.climateInformation.airQuality"));

            tab.Add(bg);
            parentTabs.Add(tab);
        }

        private VisualElement SeasonPropertyField(string title, string binding, bool multiProp = true)
        {

            Foldout foldout = new Foldout();
            foldout.text = title;
            foldout.value = false;
            foldout.AddToClassList("climateFoldout");

            VisualElement propertyContainer = new VisualElement();
            PropertyField high = new PropertyField();
            PropertyField low = new PropertyField();
            high.BindProperty(serializedObject.FindProperty($"{binding}.high"));
            low.BindProperty(serializedObject.FindProperty($"{binding}.low"));
            high.label = "Average High";
            low.label = "Average Low";
            propertyContainer.Add(high);
            if (multiProp)
                propertyContainer.Add(low);

            PropertyField diurnalCurve = new PropertyField();
            diurnalCurve.BindProperty(serializedObject.FindProperty($"{binding}.diurnalCurve"));
            propertyContainer.Add(diurnalCurve);

            PropertyField variationAmplitude = new PropertyField();
            variationAmplitude.BindProperty(serializedObject.FindProperty($"{binding}.variationAmplitude"));
            propertyContainer.Add(variationAmplitude);

            PropertyField variationPeriod = new PropertyField();
            variationPeriod.BindProperty(serializedObject.FindProperty($"{binding}.variationPeriod"));
            propertyContainer.Add(variationPeriod);

            PropertyField variationType = new PropertyField();
            variationType.BindProperty(serializedObject.FindProperty($"{binding}.variationType"));
            propertyContainer.Add(variationType);

            

            VisualElement foldoutPropertyContainer = new VisualElement() { name = "foldoutPropertyContainer" };
            foldoutPropertyContainer.style.flexDirection = FlexDirection.Row;
            PropertyField foldoutHigh = new PropertyField();
            PropertyField foldoutLow = new PropertyField();
            foldoutHigh.BindProperty(serializedObject.FindProperty($"{binding}.high"));
            foldoutLow.BindProperty(serializedObject.FindProperty($"{binding}.low"));
            foldoutHigh.label = "";
            foldoutLow.label = "";
            foldoutPropertyContainer.Add(foldoutHigh);
            if (multiProp)
                foldoutPropertyContainer.Add(foldoutLow);
            foldout.Q<VisualElement>(className: "unity-foldout__input").Add(foldoutPropertyContainer);

            foldout.RegisterValueChangedCallback(evt =>
            {
                foldoutPropertyContainer.style.display = foldout.value ? DisplayStyle.None : DisplayStyle.Flex;
            });

            foldout.Add(propertyContainer);


            return foldout;
        }

    }
}