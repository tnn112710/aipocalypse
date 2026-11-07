//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using System.Reflection;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(CozyClimateModule))]
    public class CozyClimateModuleEditor : CozyBiomeModuleEditor
    {

        CozyClimateModule climateModule;
        float dynamicIconTemperature;

        public VisualElement ProfileContainer => root.Q<VisualElement>("profile-container");
        public VisualElement PrecipitationContainer => root.Q<VisualElement>("precipitation-container");
        public VisualElement RainChanceContainer => root.Q<VisualElement>("rain-chance-container");
        public VisualElement currentTemperatureWidget => root.Q<VisualElement>("current-temperature-widget");
        public VisualElement CurrentHumidityWidget => root.Q<VisualElement>("current-humidity-widget");

        VisualElement root;



        void OnEnable()
        {
            if (target)
                climateModule = (CozyClimateModule)target;

        }
        
        public override VisualElement DisplayBiomeUI()
        {

            root = new VisualElement();
            root.AddToClassList("section-bg");
            
            PropertyField temperatureOffset = new();
            temperatureOffset.BindProperty(serializedObject.FindProperty("temperatureOffset"));
            root.Add(temperatureOffset);
            
            PropertyField humidityOffset = new();
            humidityOffset.BindProperty(serializedObject.FindProperty("humidityOffset"));
            root.Add(humidityOffset);

            PropertyField pressureOffset = new();
            pressureOffset.BindProperty(serializedObject.FindProperty("pressureOffset"));
            root.Add(pressureOffset);

            PropertyField UVIndexOffset = new();
            UVIndexOffset.BindProperty(serializedObject.FindProperty("UVIndexOffset"));
            root.Add(UVIndexOffset);
            
            PropertyField AQIOffset = new();
            AQIOffset.BindProperty(serializedObject.FindProperty("AQIOffset"));
            root.Add(AQIOffset);

            return root;
        }

        public override VisualElement DisplayUI()
        {
            root = new VisualElement();

            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Modules/UXML/climate-module-editor.uxml"
            );

            asset.CloneTree(root);

            UpdateTemperatureWidget();
            UpdateHumidityWidget();

            PropertyField controlMethodElement = new();
            controlMethodElement.BindProperty(serializedObject.FindProperty("controlMethod"));
            ProfileContainer.Add(controlMethodElement);

            PropertyField rainChanceCalculationMode = new();
            rainChanceCalculationMode.BindProperty(serializedObject.FindProperty("rainChanceCalculationMode"));
            RainChanceContainer.Add(rainChanceCalculationMode);

            PropertyField convectiveRainChance = new();
            convectiveRainChance.BindProperty(serializedObject.FindProperty("convectiveRainChance"));
            RainChanceContainer.Add(convectiveRainChance);

            PropertyField stratiformRainChance = new();
            stratiformRainChance.BindProperty(serializedObject.FindProperty("stratiformRainChance"));
            RainChanceContainer.Add(stratiformRainChance);

            rainChanceCalculationMode.RegisterValueChangeCallback(evt =>
            {
                serializedObject.ApplyModifiedProperties();
                convectiveRainChance.style.display = serializedObject.FindProperty("rainChanceCalculationMode").enumValueIndex == 2 ? DisplayStyle.Flex : DisplayStyle.None;
                stratiformRainChance.style.display = serializedObject.FindProperty("rainChanceCalculationMode").enumValueIndex == 2 ? DisplayStyle.Flex : DisplayStyle.None;
            });

            CozyProfileField<ClimateProfile> profile = new(serializedObject.FindProperty("climateProfile"));
            profile.style.display = serializedObject.FindProperty("controlMethod").intValue == 0 ? DisplayStyle.None : DisplayStyle.Flex;

            VisualElement nativeSettingsContainer = new();
            nativeSettingsContainer.style.display = serializedObject.FindProperty("controlMethod").intValue == 0 ? DisplayStyle.Flex : DisplayStyle.None;

            PropertyField GetPropertyContainer(string prop)
            {

                string displayName = Regex.Replace(prop, "([A-Z])", " $1").Trim();
                displayName = char.ToUpper(displayName[0]) + displayName.Substring(1);

                PropertyField el = new PropertyField();
                el.label = displayName;
                el.BindProperty(serializedObject.FindProperty($"{prop}.current"));
                el.RegisterValueChangeCallback((evt) =>
                {
                    UpdateTemperatureWidget();
                    UpdateHumidityWidget();
                });
                return el;
            }

            nativeSettingsContainer.Add(GetPropertyContainer("temperature"));
            nativeSettingsContainer.Add(GetPropertyContainer("humidity"));
            nativeSettingsContainer.Add(GetPropertyContainer("pressure"));
            nativeSettingsContainer.Add(GetPropertyContainer("airQuality"));
            nativeSettingsContainer.Add(GetPropertyContainer("uvIndex"));

            ProfileContainer.Add(profile);
            ProfileContainer.Add(nativeSettingsContainer);

            PropertyField snowAmountElement = new();
            snowAmountElement.BindProperty(serializedObject.FindProperty("snowAmount"));
            PrecipitationContainer.Add(snowAmountElement);

            PropertyField snowMeltElement = new();
            snowMeltElement.BindProperty(serializedObject.FindProperty("snowMeltSpeed"));
            PrecipitationContainer.Add(snowMeltElement);

            PropertyField groundwaterAmountElement = new();
            groundwaterAmountElement.BindProperty(serializedObject.FindProperty("groundwaterAmount"));
            PrecipitationContainer.Add(groundwaterAmountElement);

            PropertyField dryingSpeedElement = new();
            dryingSpeedElement.BindProperty(serializedObject.FindProperty("dryingSpeed"));
            PrecipitationContainer.Add(dryingSpeedElement);


            InspectorElement inspector = new(climateModule.climateProfile);
            inspector.style.display = serializedObject.FindProperty("controlMethod").intValue == 1 ? DisplayStyle.None : DisplayStyle.Flex;
            inspector.AddToClassList("p-0");
            root.Add(inspector);

            controlMethodElement.RegisterCallback<ChangeEvent<string>>((ChangeEvent<string> evt) =>
            {
                inspector.style.display = serializedObject.FindProperty("controlMethod").intValue == 1 ? DisplayStyle.None : DisplayStyle.Flex;
                profile.style.display = serializedObject.FindProperty("controlMethod").intValue == 1 ? DisplayStyle.None : DisplayStyle.Flex;
                nativeSettingsContainer.style.display = serializedObject.FindProperty("controlMethod").intValue == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            });

            return root;

        }

        public void UpdateTemperatureWidget()
        {
            currentTemperatureWidget.Q<Label>("value").text = $"Temperature: {climateModule.Temperature.Current}";
            currentTemperatureWidget.Q<VisualElement>("graph").Clear();

            currentTemperatureWidget.Q<VisualElement>("graph").Add(
                DrawLineGraph(ClimateProfileEditor.temperatureGradient,
                                            climateModule.Temperature.Current.Normalized,
                                            climateModule.Temperature.Low.Normalized,
                                            climateModule.Temperature.High.Normalized));
        }
        public void UpdateHumidityWidget()
        {
            CurrentHumidityWidget.Q<Label>("value").text = $"Humidity: {Mathf.Round(climateModule.Humidity.Current)}%";
            CurrentHumidityWidget.Q<VisualElement>("graph").Clear();
            CurrentHumidityWidget.Q<VisualElement>("graph").Add(DrawLineGraph(ClimateProfileEditor.humidityGradient,
                                            climateModule.Humidity.Current.Normalized,
                                            climateModule.Humidity.Low.Normalized,
                                            climateModule.Humidity.High.Normalized));
        }

        public override ModuleWidget[] ModuleWidgets => new ModuleWidget[]
        {
            // Temperature
            new(
                ModuleWidget.WidgetSize.Medium, "Current Temperature", true,
                () => {
                    VisualElement container = new();
                    container.style.paddingBottom = 8;
                    container.style.paddingLeft = 8;
                    container.style.paddingRight = 8;
                    container.style.paddingTop = 8;
                    container.style.alignItems = Align.Stretch;
                    container.style.justifyContent = Justify.Center;
                    container.style.flexGrow = 1;

                    Label valueLabel = new();
                    valueLabel.name = "valueLabel";
                    valueLabel.text = $"Temperature: {climateModule.Temperature.Current}";
                    valueLabel.style.fontSize = 14;
                    valueLabel.style.marginBottom = 13;
                    valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    container.Add(valueLabel);

                    VisualElement graph = new();
                    graph.name = "graph";
                    graph.style.marginRight = 4;
                    graph.style.marginLeft = 4;
                    graph.Add(DrawLineGraph(ClimateProfileEditor.temperatureGradient,
                                            climateModule.Temperature.Current.Normalized,
                                            climateModule.Temperature.Low.Normalized,
                                            climateModule.Temperature.High.Normalized));
                    container.Add(graph);

                    return container;
                },
                (widget) =>
                {
                    widget.container.Q<Label>("valueLabel").text = $"Temperature: {climateModule.Temperature.Current}";
                    widget.container.Q<VisualElement>("graph").Clear();
                    widget.container.Q<VisualElement>("graph").Add(DrawLineGraph(ClimateProfileEditor.temperatureGradient,
                                                                                climateModule.Temperature.Current.Normalized,
                                                                                climateModule.Temperature.Low.Normalized,
                                                                                climateModule.Temperature.High.Normalized));

                }
            ),
            // Humidity
            new(
                ModuleWidget.WidgetSize.Medium, "Current Humidity", false,
                () => {
                    VisualElement container = new();
                    container.style.paddingBottom = 8;
                    container.style.paddingLeft = 8;
                    container.style.paddingRight = 8;
                    container.style.paddingTop = 8;
                    container.style.alignItems = Align.Stretch;
                    container.style.justifyContent = Justify.Center;
                    container.style.flexGrow = 1;

                    Label valueLabel = new();
                    valueLabel.name = "valueLabel";
                    valueLabel.text = $"Humidity: {Mathf.Round(climateModule.Humidity.Current)}%";
                    valueLabel.style.fontSize = 14;
                    valueLabel.style.marginBottom = 13;
                    valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    container.Add(valueLabel);

                    VisualElement graph = new();
                    graph.name = "graph";
                    graph.style.marginRight = 4;
                    graph.style.marginLeft = 4;
                    graph.Add(DrawLineGraph(ClimateProfileEditor.humidityGradient,
                                            climateModule.Humidity.Current.Normalized,
                                            climateModule.Humidity.Low.Normalized,
                                            climateModule.Humidity.High.Normalized));
                    container.Add(graph);

                    return container;
                },
                (widget) =>
                {
                    widget.container.Q<Label>("valueLabel").text = $"Humidity: {Mathf.Round(climateModule.Humidity.Current)}%";
                    widget.container.Q<VisualElement>("graph").Clear();
                    widget.container.Q<VisualElement>("graph").Add(DrawLineGraph(ClimateProfileEditor.humidityGradient,
                                                                                climateModule.Humidity.Current.Normalized,
                                                                                climateModule.Humidity.Low.Normalized,
                                                                                climateModule.Humidity.High.Normalized));

                }
            ),
            // Pressure
            new(
                ModuleWidget.WidgetSize.Small, "Current Pressure", false,
                () => {
                    VisualElement container = new();
                    container.style.paddingBottom = 8;
                    container.style.paddingLeft = 8;
                    container.style.paddingRight = 8;
                    container.style.paddingTop = 8;
                    container.style.alignItems = Align.Center;
                    container.style.justifyContent = Justify.Center;
                    container.style.flexGrow = 1;

                    Label valueLabel = new();
                    valueLabel.name = "valueLabel";
                    valueLabel.text = climateModule.Pressure.Current.ToString();
                    valueLabel.style.fontSize = 14;
                    valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    container.Add(valueLabel);

                    VisualElement graph = new();
                    graph.name = "graph";
                    graph.style.position = Position.Absolute;
                    graph.style.top = 8;
                    graph.style.left = 8;
                    graph.style.right = 8;
                    graph.style.bottom = 8;
                    graph.style.marginRight = 4;
                    graph.style.marginLeft = 4;
                    graph.Add(DrawArcGraph(ClimateProfileEditor.pressureGradient, climateModule.Pressure.Current.Normalized,
                                                                                climateModule.Pressure.Low.Normalized,
                                                                                climateModule.Pressure.High.Normalized));
                    container.Add(graph);

                    return container;
                },
                (widget) =>
                {
                    widget.container.Q<Label>("valueLabel").text = climateModule.Pressure.Current.ToString();
                    widget.container.Q<VisualElement>("graph").Clear();
                    widget.container.Q<VisualElement>("graph").Add(DrawArcGraph(ClimateProfileEditor.pressureGradient,
                                                                                climateModule.Pressure.Current.Normalized,
                                                                                climateModule.Pressure.Low.Normalized,
                                                                                climateModule.Pressure.High.Normalized));

                }
            ),

            //Precipitation chance widget
            new(
                ModuleWidget.WidgetSize.Medium, "Current Precipitation", false,
                () => {
                    VisualElement container = new();
                    container.style.alignItems = Align.Stretch;
                    container.style.justifyContent = Justify.Center;
                    container.style.flexDirection = FlexDirection.Row;
                    container.style.flexGrow = 1;

                    VisualElement lightPrecipitationChanceContainer = new();
                    lightPrecipitationChanceContainer.style.width = new Length(50, LengthUnit.Percent);
                    lightPrecipitationChanceContainer.style.paddingBottom = 8;
                    lightPrecipitationChanceContainer.style.paddingLeft = 8;
                    lightPrecipitationChanceContainer.style.paddingRight = 8;
                    lightPrecipitationChanceContainer.style.paddingTop = 8;
                    lightPrecipitationChanceContainer.style.alignItems = Align.Stretch;
                    lightPrecipitationChanceContainer.style.justifyContent = Justify.Center;
                    lightPrecipitationChanceContainer.style.flexDirection = FlexDirection.Row;
                    lightPrecipitationChanceContainer.style.flexGrow = 1;

                    VisualElement heavyPrecipitationChanceContainer = new();
                    heavyPrecipitationChanceContainer.style.width = new Length(50, LengthUnit.Percent);
                    heavyPrecipitationChanceContainer.style.paddingBottom = 8;
                    heavyPrecipitationChanceContainer.style.paddingLeft = 8;
                    heavyPrecipitationChanceContainer.style.paddingRight = 8;
                    heavyPrecipitationChanceContainer.style.paddingTop = 8;
                    heavyPrecipitationChanceContainer.style.alignItems = Align.Stretch;
                    heavyPrecipitationChanceContainer.style.justifyContent = Justify.Center;
                    heavyPrecipitationChanceContainer.style.flexDirection = FlexDirection.Row;
                    heavyPrecipitationChanceContainer.style.flexGrow = 1;


                    container.Add(lightPrecipitationChanceContainer);
                    container.Add(heavyPrecipitationChanceContainer);

                    VisualElement stratiformChanceGraph = new();
                    stratiformChanceGraph.style.position = Position.Absolute;
                    stratiformChanceGraph.style.top = 20;
                    stratiformChanceGraph.style.left = 20;
                    stratiformChanceGraph.style.right = 20;
                    stratiformChanceGraph.style.bottom = 20;
                    stratiformChanceGraph.style.marginRight = 4;
                    stratiformChanceGraph.style.marginLeft = 4;
                    stratiformChanceGraph.name = "stratiformChanceGraph";
                    stratiformChanceGraph.tooltip = $"Stratiform Chance: {(float)climateModule.StratiformRainChance:F0}%";
                    stratiformChanceGraph.Add(DrawArcGraph(ClimateProfileEditor.stratiformChanceGradient,
                                            climateModule.StratiformRainChance.Normalized,
                                            0,
                                            1));
                    lightPrecipitationChanceContainer.Add(stratiformChanceGraph);

                    VisualElement convectiveChanceGraph = new();
                    convectiveChanceGraph.style.position = Position.Absolute;
                    convectiveChanceGraph.style.top = 20;
                    convectiveChanceGraph.style.left = 20;
                    convectiveChanceGraph.style.right = 20;
                    convectiveChanceGraph.style.bottom = 20;
                    convectiveChanceGraph.name = "convectiveChanceGraph";
                    convectiveChanceGraph.tooltip = $"Convective Chance: {(float)climateModule.ConvectiveRainChance:F0}%";
                    convectiveChanceGraph.style.marginRight = 4;
                    convectiveChanceGraph.style.marginLeft = 4;
                    convectiveChanceGraph.Add(DrawArcGraph(ClimateProfileEditor.convectiveChanceGradient,
                                            climateModule.ConvectiveRainChance.Normalized,
                                            0,
                                            1));
                    heavyPrecipitationChanceContainer.Add(convectiveChanceGraph);

                    Label stratiformChanceLabel = new();
                    stratiformChanceLabel.name = "stratiformChanceLabel";
                    stratiformChanceLabel.text = $"{(float)climateModule.StratiformRainChance:F0}%";
                    stratiformChanceLabel.style.fontSize = 14;
                    stratiformChanceLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    stratiformChanceLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                    lightPrecipitationChanceContainer.Add(stratiformChanceLabel);

                    Label convectiveChanceLabel = new();
                    convectiveChanceLabel.name = "convectiveChanceLabel";
                    convectiveChanceLabel.text = $"{(float)climateModule.ConvectiveRainChance:F0}%";
                    convectiveChanceLabel.style.fontSize = 14;
                    convectiveChanceLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    convectiveChanceLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                    heavyPrecipitationChanceContainer.Add(convectiveChanceLabel);

                    return container;
                },
                (widget) =>
                {
                    widget.container.Q<Label>("stratiformChanceLabel").text = $"{climateModule.StratiformRainChance * 1:F0}%";
                    widget.container.Q<VisualElement>("stratiformChanceGraph").Clear();
                    widget.container.Q<VisualElement>("stratiformChanceGraph").tooltip = $"Stratiform Chance: {(float)climateModule.StratiformRainChance:F0}%";
                    widget.container.Q<VisualElement>("stratiformChanceGraph").Add(DrawArcGraph(ClimateProfileEditor.stratiformChanceGradient,
                                                                                    climateModule.StratiformRainChance.Normalized,
                                                                                    0,
                                                                                    1));

                    widget.container.Q<Label>("convectiveChanceLabel").text = $"{climateModule.ConvectiveRainChance * 1:F0}%";
                    widget.container.Q<VisualElement>("convectiveChanceGraph").Clear();
                    widget.container.Q<VisualElement>("convectiveChanceGraph").tooltip = $"Convective Chance: {(float)climateModule.ConvectiveRainChance:F0}%";
                    widget.container.Q<VisualElement>("convectiveChanceGraph").Add(DrawArcGraph(ClimateProfileEditor.convectiveChanceGradient,
                                                                                    climateModule.ConvectiveRainChance.Normalized,
                                                                                    0,
                                                                                    1));

                }
            ),
        };
        public VisualElement DrawLineGraph(Gradient gradient, float currentValue, float minValue, float maxValue)
        {
            VisualElement element = new();
            element.AddToClassList("graph-section");

            element.generateVisualContent += (MeshGenerationContext context) =>
            {
                float width = element.contentRect.width;
                float height = element.contentRect.height;
                float halfHeight = height * 0.5f;

                var painter = context.painter2D;

                Gradient gradientBackground = gradient;
                gradientBackground.alphaKeys = new GradientAlphaKey[1] { new(0.25f, 0) };

                var foregroundColorKeys = new List<GradientColorKey>
                    {
                        new(gradient.Evaluate(Mathf.Clamp01(minValue)), 0),
                        new(gradient.Evaluate(Mathf.Clamp01(maxValue)), 1)
                    };

                foreach (var colorKey in gradient.colorKeys)
                {
                    if (colorKey.time < minValue) continue;
                    if (colorKey.time > maxValue) break;

                    foregroundColorKeys.Add(new GradientColorKey(colorKey.color, Mathf.InverseLerp(minValue, maxValue, colorKey.time)));
                }

                Gradient gradientForeground = new()
                {
                    colorKeys = foregroundColorKeys.ToArray()
                };

                painter.lineWidth = 8;
                painter.strokeGradient = gradientBackground;
                painter.lineCap = LineCap.Round;

                painter.BeginPath();
                painter.MoveTo(new Vector2(0, halfHeight));
                painter.LineTo(new Vector2(width, halfHeight));
                painter.Stroke();
                painter.ClosePath();

                painter.strokeGradient = gradientForeground;

                painter.BeginPath();
                painter.MoveTo(new Vector2(width * Mathf.Max(minValue, 0), halfHeight));
                painter.LineTo(new Vector2(width * Mathf.Min(maxValue, 1), halfHeight));
                painter.Stroke();
                painter.ClosePath();

                painter.strokeColor = Color.white;

                painter.BeginPath();
                painter.Arc(new Vector2(width * currentValue, halfHeight), 1, 0, 360, ArcDirection.Clockwise);
                painter.Stroke();
                painter.ClosePath();
            };

            return element;
        }

        public VisualElement DrawArcGraph(Gradient gradient, float currentValue, float minValue, float maxValue)
        {
            VisualElement element = new();
            element.AddToClassList("graph-section");

            element.generateVisualContent += (MeshGenerationContext context) =>
            {
                float width = element.contentRect.width;
                float height = element.contentRect.height;
                float halfWidth = width * 0.5f;
                float halfHeight = height * 0.5f;

                var painter = context.painter2D;

                Gradient gradientBackground = gradient;
                gradientBackground.alphaKeys = new GradientAlphaKey[1] { new(0.25f, 0) };

                var foregroundColorKeys = new List<GradientColorKey>
                    {
                        new(gradient.Evaluate(Mathf.Clamp01(minValue)), 0),
                        new(gradient.Evaluate(Mathf.Clamp01(maxValue)), 1)
                    };

                foreach (var colorKey in gradient.colorKeys)
                {
                    if (colorKey.time < minValue) continue;
                    if (colorKey.time > maxValue) break;

                    foregroundColorKeys.Add(new GradientColorKey(colorKey.color, Mathf.InverseLerp(minValue, maxValue, colorKey.time)));
                }

                Gradient gradientForeground = new()
                {
                    colorKeys = foregroundColorKeys.ToArray()
                };

                painter.lineWidth = 8;
                painter.strokeGradient = gradientBackground;
                painter.lineCap = LineCap.Round;

                painter.BeginPath();
                painter.Arc(new Vector2(halfWidth, halfHeight), halfHeight, 120, 420, ArcDirection.Clockwise);
                painter.Stroke();
                painter.ClosePath();

                painter.strokeGradient = gradientForeground;

                painter.BeginPath();
                painter.Arc(new Vector2(halfWidth, halfHeight), halfHeight,
                                                                Mathf.Lerp(120, 420, Mathf.Max(minValue, 0)),
                                                                Mathf.Lerp(120, 420, Mathf.Min(maxValue, 1)),
                                                                ArcDirection.Clockwise);
                painter.Stroke();
                painter.ClosePath();

                painter.strokeColor = Color.white;

                painter.BeginPath();
                float rotation = (90 - Mathf.Lerp(120, 420, currentValue)) * MathF.PI / 180f;
                Vector2 coord = new Vector2(Mathf.Sin(rotation), Mathf.Cos(rotation)) * halfHeight;
                painter.Arc(new Vector2(halfWidth, halfHeight) + coord, 1, 0, 360, ArcDirection.Clockwise);
                painter.Stroke();
                painter.ClosePath();
            };

            return element;
        }

    }
}