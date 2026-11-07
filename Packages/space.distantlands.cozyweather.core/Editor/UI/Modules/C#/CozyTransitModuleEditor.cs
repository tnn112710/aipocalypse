//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System;
using System.Reflection;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace DistantLands.Cozy.EditorScripts
{
#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    [CustomEditor(typeof(CozyTransitModule))]
    public class CozyTransitModuleEditor : CozyModuleEditor
    {
        CozyTransitModule transitModule;

        public VisualElement Graph => root.Q<VisualElement>("current-curve-graph");
        public VisualElement TransitGraph => root.Q<VisualElement>("transit-wheel-graph");
        public VisualElement TransitGraphInfo => root.Q<VisualElement>("transit-wheel-graph-info");
        public VisualElement SunTransitContainer => root.Q<VisualElement>("sun-transit-container");
        public VisualElement SeasonalVariationContainer => root.Q<VisualElement>("seasonal-variation-container");

        static Gradient TransitDayColors = new Gradient()
        {
            colorKeys = new GradientColorKey[8] {
                new GradientColorKey(Branding.charcoal,0),
                new GradientColorKey(Branding.charcoal,0.2f),
                new GradientColorKey(Branding.red,0.25f),
                new GradientColorKey(Branding.blue,0.3f),
                new GradientColorKey(Branding.blue,0.7f),
                new GradientColorKey(Branding.orange,0.75f),
                new GradientColorKey(Branding.charcoal,0.8f),
                new GradientColorKey(Branding.charcoal,1)
            }
        };

        VisualElement root;


        public override ModuleWidget[] ModuleWidgets => new ModuleWidget[]
        {
            new(ModuleWidget.WidgetSize.Large, "Transit Graph", true,
                () => {
                    VisualElement container = new();
                    container.name = "graph";
                    container.style.bottom = 16;
                    container.style.left = 32;
                    container.style.right = 32;
                    container.style.top = 16;
                    container.style.position = Position.Absolute;

                    container.generateVisualContent += (ctx) =>
                    {
                        Painter2D painter = ctx.painter2D;
                        float width = container.contentRect.width;
                        float height = container.contentRect.height;

                        painter.strokeGradient = TransitDayColors;
                        painter.lineWidth = 4;
                        painter.BeginPath();
                        painter.MoveTo(new Vector2(0,height));
                        painter.BezierCurveTo(new Vector2(width * 0.25f, height), new Vector2(width * 0.25f, 0), new Vector2(width * 0.5f, 0));
                        painter.BezierCurveTo(new Vector2(width * 0.75f, 0), new Vector2(width * 0.75f, height), new Vector2(width, height));
                        painter.Stroke();

                        float t = transitModule.WeatherSphere.Time.ModifiedTime;

                        Vector2 p0 = new Vector2(0, height);
                        Vector2 p1 = new Vector2(width * 0.25f, height);
                        Vector2 p2 = new Vector2(width * 0.25f, 0);
                        Vector2 p3 = new Vector2(width * 0.5f, 0);

                        Vector2 p4 = new Vector2(width * 0.75f, 0);
                        Vector2 p5 = new Vector2(width * 0.75f, height);
                        Vector2 p6 = new Vector2(width, height);

                        Vector2 pointOnCurve;
                        float t_segment;

                        if (t < 0.5f) {
                            t_segment = t * 2.0f;
                            pointOnCurve = GetBezierPoint(t_segment, p0, p1, p2, p3);
                        } else {
                            t_segment = (t - 0.5f) * 2.0f;
                            pointOnCurve = GetBezierPoint(t_segment, p3, p4, p5, p6);
                        }

                        painter.strokeColor = Branding.white;
                        painter.fillColor = Branding.white;
                        painter.lineWidth = 1;
                        painter.BeginPath();
                        painter.Arc(pointOnCurve, 6f, 0, 360);
                        painter.Fill();

                        Vector2 GetBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3) {
                            float u = 1 - t;
                            return (u * u * u * p0) + (3 * u * u * t * p1) + (3 * u * t * t * p2) + (t * t * t * p3);
                        }
                    };

                    return container;
                },
                (widget) =>
                {
                    widget.container.Q<VisualElement>("graph").MarkDirtyRepaint();
                }
            ),
        };


        void OnEnable()
        {
            if (!target)
                return;

            transitModule = (CozyTransitModule)target;
            transitModule.GetModifiedDayPercent();
        }

        // public override void DrawDynamicIcon(VisualElement parent)
        // {
        //     VisualElement iconContainer = new VisualElement();
        //     iconContainer.AddToClassList("transit-graph");
        //     iconContainer.generateVisualContent += context =>
        //     {
        //         Painter2D painter = context.painter2D;
        //         Rect rect = iconContainer.contentRect;
        //         float width = rect.width;
        //         float height = rect.height;
        //         float t = Mathf.Clamp01(dynamicIconTime);

        //         Vector2 p0 = new(width * 0.08f, height * 0.65f);
        //         Vector2 p1 = new(width * 0.28f, height * 0.65f);
        //         Vector2 p2 = new(width * 0.28f, height * 0.35f);
        //         Vector2 p3 = new(width * 0.5f, height * 0.35f);
        //         Vector2 p4 = new(width * 0.72f, height * 0.35f);
        //         Vector2 p5 = new(width * 0.72f, height * 0.65f);
        //         Vector2 p6 = new(width * 0.92f, height * 0.65f);

        //         painter.lineWidth = 5;
        //         painter.strokeGradient = TransitDayColors;
        //         painter.BeginPath();
        //         painter.MoveTo(p0);
        //         painter.BezierCurveTo(p1, p2, p3);
        //         painter.BezierCurveTo(p4, p5, p6);
        //         painter.Stroke();

        //         float segmentTime = t < 0.5f ? t * 2 : (t - 0.5f) * 2;
        //         Vector2 marker = t < 0.5f
        //             ? GetBezierPoint(segmentTime, p0, p1, p2, p3)
        //             : GetBezierPoint(segmentTime, p3, p4, p5, p6);
        //         painter.fillColor = Branding.white;
        //         painter.BeginPath();
        //         painter.Arc(marker, 5f, 0, 360);
        //         painter.Fill();

        //         Vector2 GetBezierPoint(float value, Vector2 start, Vector2 control1, Vector2 control2, Vector2 end)
        //         {
        //             float inverse = 1 - value;
        //             return (inverse * inverse * inverse * start)
        //                 + (3 * inverse * inverse * value * control1)
        //                 + (3 * inverse * value * value * control2)
        //                 + (value * value * value * end);
        //         }
        //     };

        //     Type targetType = target.GetType();
        //     var moduleAttribute = targetType.GetCustomAttribute<CozyModuleAttribute>();

        //     parent.Add(iconContainer);
        //     Label label = new Label();
        //     label.text = moduleAttribute.name;
        //     iconContainer.Add(label);
        //     parent.tooltip = moduleAttribute.tooltip;
        //     parent.RegisterCallback<ClickEvent>(OpenModuleUI);
        //     parent.RegisterCallback<ContextClickEvent>((ContextClickEvent) => { OpenContextMenu(); });
        //     icon = iconContainer;

        // }

        // public override void UpdateDynamicIcon()
        // {
        //     if (!transitModule) return;

        //     if (transitModule.WeatherSphere.Time != null)
        //     {
        //         dynamicIconTime = transitModule.WeatherSphere.Time.CurrentTime;
        //         icon?.MarkDirtyRepaint();
        //     }
        // }

        public void DisplayBlockEditor(string blockName)
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty(blockName).FindPropertyRelative("start"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(blockName).FindPropertyRelative("end"));
            serializedObject.ApplyModifiedProperties();
        }

        public override VisualElement DisplayUI()
        {
            transitModule.GetModifiedDayPercent();

            root = new VisualElement();

            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Modules/UXML/transit-module-editor.uxml"
            );

            asset.CloneTree(root);

            ApplyChanges();

            RefreshTransitWheelGraph();
            RefreshTransitGraph();

            PropertyField timeCurveSettings = new PropertyField();
            VisualElement weightSettingsContainer = new VisualElement();

            timeCurveSettings.BindProperty(serializedObject.FindProperty("timeCurveSettings"));
            timeCurveSettings.RegisterValueChangeCallback(evt =>
            {
                ApplyChanges();
                UpdateWeightSettings();
            });
            SunTransitContainer.Add(timeCurveSettings);



            SunTransitContainer.Add(weightSettingsContainer);

            void UpdateWeightSettings()
            {
                weightSettingsContainer.Clear();

                weightSettingsContainer.Add(TimeCurveVertex(serializedObject.FindProperty("sunriseWeight"), "Sunrise"));
                weightSettingsContainer.Add(TimeCurveVertex(serializedObject.FindProperty("dayWeight"), "Day"));
                weightSettingsContainer.Add(TimeCurveVertex(serializedObject.FindProperty("sunsetWeight"), "Sunset"));
                weightSettingsContainer.Add(TimeCurveVertex(serializedObject.FindProperty("nightWeight"), "Night"));
            }

            PropertyField springDayLengthOffset = new PropertyField();
            springDayLengthOffset.BindProperty(serializedObject.FindProperty("springDayLengthOffset"));
            springDayLengthOffset.RegisterValueChangeCallback(evt =>
            {
                ApplyChanges();
            });
            SeasonalVariationContainer.Add(springDayLengthOffset);

            PropertyField summerDayLengthOffset = new PropertyField();
            summerDayLengthOffset.BindProperty(serializedObject.FindProperty("summerDayLengthOffset"));
            summerDayLengthOffset.RegisterValueChangeCallback(evt =>
            {
                ApplyChanges();
            });
            SeasonalVariationContainer.Add(summerDayLengthOffset);

            PropertyField fallDayLengthOffset = new PropertyField();
            fallDayLengthOffset.BindProperty(serializedObject.FindProperty("fallDayLengthOffset"));
            fallDayLengthOffset.RegisterValueChangeCallback(evt =>
            {
                ApplyChanges();
            });
            SeasonalVariationContainer.Add(fallDayLengthOffset);

            PropertyField winterDayLengthOffset = new PropertyField();
            winterDayLengthOffset.BindProperty(serializedObject.FindProperty("winterDayLengthOffset"));
            winterDayLengthOffset.RegisterValueChangeCallback(evt =>
            {
                ApplyChanges();
            });
            SeasonalVariationContainer.Add(winterDayLengthOffset);

            return root;

        }

        private void ApplyChanges()
        {
            transitModule.CalculateSunriseAndSunset();
            transitModule.GetModifiedDayPercent();

            RefreshTransitWheelGraph();
            RefreshTransitGraph();
        }

        public void RefreshTransitWheelGraph()
        {
            TransitGraphInfo.Clear();

            if (transitModule.WeatherSphere.Time == null) return;

            Label infoLabel = new Label();
            infoLabel.text = "Sun Angle";
            infoLabel.AddToClassList("h1");
            TransitGraphInfo.Add(infoLabel);

            VisualElement infoHolder = new VisualElement();
            infoHolder.AddToClassList("indented");
            TransitGraphInfo.Add(infoHolder);

            for (int i = 0; i < 8; i += 1)
            {
                VisualElement container = new VisualElement();
                container.AddToClassList("swatch");

                VisualElement swatch = new VisualElement();
                swatch.style.backgroundColor = TransitDayColors.Evaluate(transitModule.ModifyDayPercentage((i * 2 + 1) / 16f));
                container.Add(swatch);


                Label timeLabel = new Label
                {
                    text = $"{((MeridiemTime)(i / 8f)).ToString()} - {Mathf.Round(transitModule.sunMovementCurve.Evaluate(i / 8f))}°"
                };
                timeLabel.AddToClassList("font-bold");
                container.Add(timeLabel);

                infoHolder.Add(container);

            }

            VisualElement lastContainer = new VisualElement();
            lastContainer.AddToClassList("swatch");

            VisualElement lastSwatch = new VisualElement();
            lastSwatch.style.backgroundColor = TransitDayColors.Evaluate(1);
            lastContainer.Add(lastSwatch);


            Label lastTimeLabel = new Label
            {
                text = $"{((MeridiemTime)1).ToString()} - {Mathf.Round(transitModule.sunMovementCurve.Evaluate(1))}°"
            };
            lastTimeLabel.AddToClassList("font-bold");
            lastContainer.Add(lastTimeLabel);

            infoHolder.Add(lastContainer);


            TransitGraph.Clear();

            VisualElement graphHolder = new VisualElement();
            graphHolder.AddToClassList("graph-section");

            graphHolder.generateVisualContent += (MeshGenerationContext context) =>
            {
                float width = graphHolder.contentRect.width;
                float height = graphHolder.contentRect.height;

                var painter = context.painter2D;
                painter.lineWidth = 24;

                int spokes = 120;


                for (int i = 0; i < spokes; i++)
                {
                    painter.strokeColor = TransitDayColors.Evaluate(transitModule.ModifyDayPercentage(i / (float)spokes));
                    float start = 90 + (i / (float)spokes) * 360;

                    painter.BeginPath();
                    painter.Arc(new Vector2(width / 2, height / 2), height / 3, start, start + 2f, ArcDirection.Clockwise);
                    painter.Stroke();
                }

                painter.lineWidth = 3;
                painter.strokeColor = Branding.white;
                painter.BeginPath();
                painter.Arc(new Vector2(width / 2, height / 2), height / 3 + 18, 90 + transitModule.WeatherSphere.Time.CurrentTime * 360, 90 + transitModule.WeatherSphere.Time.CurrentTime * 360 + 3f, ArcDirection.Clockwise);
                painter.Stroke();
            };

            Label label = new Label();
            label.text = transitModule.WeatherSphere.Time.CurrentTime.ToString();
            label.AddToClassList("h1");
            graphHolder.Add(label);


            TransitGraph.Add(graphHolder);

        }

        public void RefreshTransitGraph()
        {
            Graph.Clear();

            if (transitModule.WeatherSphere.Time == null || transitModule.WeatherSphere.Calendar == null) return;

            VisualElement graphHolder = new VisualElement();
            graphHolder.AddToClassList("graph-section");
            graphHolder.style.marginLeft = new StyleLength(Length.Auto());
            graphHolder.style.marginRight = new StyleLength(Length.Auto());
            graphHolder.style.width = new Length(95, LengthUnit.Percent);

            graphHolder.generateVisualContent += (MeshGenerationContext context) =>
            {
                float width = graphHolder.contentRect.width;
                float height = graphHolder.contentRect.height;

                var painter = context.painter2D;

                for (int i = 0; i < 25; i++)
                {
                    if (i % 6 == 0)
                        painter.strokeColor = Branding.whiteAccent;
                    else
                        painter.strokeColor = Branding.lightGreyAccent;

                    painter.BeginPath();
                    painter.MoveTo(new Vector2(width * i / 24, 0));
                    painter.LineTo(new Vector2(width * i / 24, height));
                    painter.Stroke();
                }

                painter.strokeColor = Branding.whiteAccent;
                painter.BeginPath();
                painter.MoveTo(new Vector2(0, height / 2));
                painter.LineTo(new Vector2(width, height / 2));
                painter.Stroke();



                float offset = transitModule.yearWeightsCurve.Evaluate(transitModule.WeatherSphere.Calendar.CurrentDate.YearPercentage) / 5;

                painter.strokeColor = Branding.white;
                painter.lineWidth = 2;
                CreateCurveSection(offset);

                void CreateCurveSection(float offset)
                {

                    switch (transitModule.timeCurveSettings)
                    {
                        case CozyTransitModule.TimeCurveSettings.advancedCurve:
                            painter.BeginPath();
                            painter.MoveTo(new Vector2(0, height));
                            painter.BezierCurveTo(
                                new Vector2(width * transitModule.nightWeight.weight * 0.25f, height),
                                new Vector2((width * ((float)transitModule.sunriseWeight.time - offset)) - width * transitModule.sunriseWeight.weight * 0.25f, height * (1 - transitModule.sunriseWeight.sunHeight / 180)),
                                new Vector2(width * ((float)transitModule.sunriseWeight.time - offset), height * (1 - transitModule.sunriseWeight.sunHeight / 180))
                                );
                            painter.BezierCurveTo(
                                new Vector2((width * ((float)transitModule.sunriseWeight.time - offset)) + width * transitModule.sunriseWeight.weight * 0.25f, height * (1 - transitModule.sunriseWeight.sunHeight / 180)),
                                new Vector2((width * transitModule.dayWeight.time) - width * transitModule.dayWeight.weight * 0.25f, height * (1 - transitModule.dayWeight.sunHeight / 180)),
                                new Vector2(width * transitModule.dayWeight.time, height * (1 - transitModule.dayWeight.sunHeight / 180))
                                );
                            painter.BezierCurveTo(
                                new Vector2((width * transitModule.dayWeight.time) + width * transitModule.dayWeight.weight * 0.25f, height * (1 - transitModule.dayWeight.sunHeight / 180)),
                                new Vector2((width * ((float)transitModule.sunsetWeight.time + offset)) - width * transitModule.sunsetWeight.weight * 0.25f, height * (1 - (transitModule.sunsetWeight.sunHeight > 180 ? 360 - transitModule.sunsetWeight.sunHeight : transitModule.sunsetWeight.sunHeight) / 180)),
                                new Vector2(width * ((float)transitModule.sunsetWeight.time + offset), height * (1 - (transitModule.sunsetWeight.sunHeight > 180 ? 360 - transitModule.sunsetWeight.sunHeight : transitModule.sunsetWeight.sunHeight) / 180))
                                );
                            painter.BezierCurveTo(
                                new Vector2((width * ((float)transitModule.sunsetWeight.time + offset)) + width * transitModule.sunsetWeight.weight * 0.25f, height * (1 - (transitModule.sunsetWeight.sunHeight > 180 ? 360 - transitModule.sunsetWeight.sunHeight : transitModule.sunsetWeight.sunHeight) / 180)),
                                new Vector2(width - width * transitModule.nightWeight.weight * 0.25f, height),
                                new Vector2(width, height)
                                );
                            painter.Stroke();
                            break;
                        case CozyTransitModule.TimeCurveSettings.simpleCurve:
                            painter.BeginPath();
                            painter.MoveTo(new Vector2(0, height));
                            painter.BezierCurveTo(
                                new Vector2(width * transitModule.nightWeight.weight * 0.25f, height),
                                new Vector2((width * ((float)transitModule.sunriseWeight.time - offset)) - width * transitModule.sunriseWeight.weight * 0.25f, height * 0.5f),
                                new Vector2(width * ((float)transitModule.sunriseWeight.time - offset), height * 0.5f)
                                );
                            painter.BezierCurveTo(
                                new Vector2((width * ((float)transitModule.sunriseWeight.time - offset)) + width * transitModule.sunriseWeight.weight * 0.25f, height * 0.5f),
                                new Vector2((width * transitModule.dayWeight.time) - width * transitModule.dayWeight.weight * 0.25f, 0),
                                new Vector2(width * transitModule.dayWeight.time, 0)
                                );
                            painter.BezierCurveTo(
                                new Vector2((width * transitModule.dayWeight.time) + width * transitModule.dayWeight.weight * 0.25f, 0),
                                new Vector2((width * ((float)transitModule.sunsetWeight.time + offset)) - width * transitModule.sunsetWeight.weight * 0.25f, height * 0.5f),
                                new Vector2(width * ((float)transitModule.sunsetWeight.time + offset), height * 0.5f)
                                );
                            painter.BezierCurveTo(
                                new Vector2((width * ((float)transitModule.sunsetWeight.time + offset)) + width * transitModule.sunsetWeight.weight * 0.25f, height * 0.5f),
                                new Vector2(width - width * transitModule.nightWeight.weight * 0.25f, height),
                                new Vector2(width, height)
                                );
                            painter.Stroke();
                            break;
                        case CozyTransitModule.TimeCurveSettings.linearDay:
                            painter.BeginPath();
                            painter.MoveTo(new Vector2(0, height));
                            painter.LineTo(
                                new Vector2(width * ((float)transitModule.sunriseWeight.time - offset), height * (1 - transitModule.sunriseWeight.sunHeight / 180))
                                );
                            painter.LineTo(
                                new Vector2(width * transitModule.dayWeight.time, height * (1 - transitModule.dayWeight.sunHeight / 180))
                                );
                            painter.LineTo(
                                new Vector2(width * ((float)transitModule.sunsetWeight.time + offset), height * (1 - (transitModule.sunsetWeight.sunHeight > 180 ? 360 - transitModule.sunsetWeight.sunHeight : transitModule.sunsetWeight.sunHeight) / 180))
                                );
                            painter.LineTo(
                                new Vector2(width, height)
                                );
                            painter.Stroke();
                            break;
                    }
                }

                painter.strokeColor = Branding.yellow;
                painter.lineWidth = 1;
                CreateCurveSection(transitModule.springDayLengthOffset / 5);

                painter.strokeColor = Branding.orange;
                CreateCurveSection(transitModule.summerDayLengthOffset / 5);

                painter.strokeColor = Branding.red;
                CreateCurveSection(transitModule.fallDayLengthOffset / 5);

                painter.strokeColor = Branding.blue;
                CreateCurveSection(transitModule.winterDayLengthOffset / 5);

            };

            Graph.Add(graphHolder);

        }

        public VisualElement TimeCurveVertex(SerializedProperty property, string title)
        {
            VisualElement container = new VisualElement();

            Label label = new Label();
            label.text = title;
            label.AddToClassList("h2");
            container.Add(label);

            VisualElement indentedContainer = new VisualElement();
            indentedContainer.AddToClassList("indented");

            PropertyField time = new PropertyField();
            time.BindProperty(property.FindPropertyRelative("time"));
            time.RegisterValueChangeCallback(evt =>
            {
                ApplyChanges();
            });
            indentedContainer.Add(time);

            PropertyField sunHeight = new PropertyField();
            sunHeight.BindProperty(property.FindPropertyRelative("sunHeight"));
            sunHeight.style.display = (transitModule.timeCurveSettings == CozyTransitModule.TimeCurveSettings.advancedCurve) ? DisplayStyle.Flex : DisplayStyle.None;
            sunHeight.RegisterValueChangeCallback(evt =>
            {
                ApplyChanges();
            });
            indentedContainer.Add(sunHeight);

            PropertyField weight = new PropertyField();
            weight.BindProperty(property.FindPropertyRelative("weight"));
            weight.style.display = (transitModule.timeCurveSettings != CozyTransitModule.TimeCurveSettings.linearDay) ? DisplayStyle.Flex : DisplayStyle.None;
            weight.RegisterValueChangeCallback(evt =>
            {
                ApplyChanges();
            });
            indentedContainer.Add(weight);

            container.Add(indentedContainer);

            return container;
        }

        public VisualElement TimeBlockElement(SerializedProperty property, string title)
        {
            VisualElement container = new VisualElement();

            Label label = new Label();
            label.text = title;
            label.AddToClassList("h2");
            container.Add(label);

            VisualElement indentedContainer = new VisualElement();
            indentedContainer.AddToClassList("indented");

            PropertyField start = new PropertyField();
            start.BindProperty(property.FindPropertyRelative("start"));
            start.RegisterValueChangeCallback(evt =>
            {
                ApplyChanges();
            });
            indentedContainer.Add(start);

            PropertyField end = new PropertyField();
            end.BindProperty(property.FindPropertyRelative("end"));
            end.RegisterValueChangeCallback(evt =>
            {
                ApplyChanges();
            });
            indentedContainer.Add(end);

            container.Add(indentedContainer);

            return container;
        }


        public override void DrawGizmos()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && sceneView.camera != null)
            {
                Vector3 cameraPos = sceneView.camera.transform.position;

                //Draw Transit graph

                if (transitModule.WeatherSphere.GetModule(out CozyAtmosphereModule atmosphereModule))
                {

                    Vector3 sunPathDiscDirection = Quaternion.AngleAxis(CozyPreferences.SkyPitch, CozyPreferences.West) * CozyPreferences.North;
                    Vector3 remappedDown = Quaternion.AngleAxis(CozyPreferences.SkyPitch, CozyPreferences.West) * Vector3.down;

                    Handles.color = new Color(0.67f, 0.44f, 0.85f, 0.5f);

                    float dashSize = 7.5f;

                    if (dashSize < 0.1f)
                        return;

                    float scale = 0.45f;

                    for (float i = 0; i < 360; i += dashSize)
                    {
                        // float currentAngle = i;
                        float currentAngle = transitModule.ModifyDayPercentage(i / 360f) * 360f;

                        Handles.color = TransitDayColors.Evaluate(currentAngle / 360f);

                        Vector3 line1P1 = cameraPos - sunPathDiscDirection * scale * 0.04f + Quaternion.AngleAxis(currentAngle, sunPathDiscDirection) * remappedDown;
                        Vector3 line1P2 = cameraPos + sunPathDiscDirection * scale * 0.04f + Quaternion.AngleAxis(currentAngle, sunPathDiscDirection) * remappedDown;

                        Handles.DrawLine(line1P1, line1P2);

                        scale += 0.5f;
                        scale %= 1;

                    }

                    GUIStyle compassTextStyle = new GUIStyle();
                    compassTextStyle.normal.textColor = new Color(0.9f, 0.34f, 0.74f, 0.6f);
                    compassTextStyle.fontStyle = FontStyle.Bold;
                    compassTextStyle.alignment = TextAnchor.LowerCenter;


                    Handles.color = new Color(1, 1, 1, 0.1f);
                    Handles.Label(cameraPos - Vector3.up * 0.1f + CozyPreferences.West, $"Sunset: {transitModule.SunsetTime}", compassTextStyle);
                    Handles.Label(cameraPos - Vector3.up * 0.1f + CozyPreferences.East, $"Sunrise: {transitModule.SunriseTime}", compassTextStyle);
                }
            }
        }

        public override void OpenDocumentationURL()
        {
            Application.OpenURL("https://distant-lands.gitbook.io/cozy-stylized-weather-documentation/how-it-works/modules/transit-module");
        }


    }
}