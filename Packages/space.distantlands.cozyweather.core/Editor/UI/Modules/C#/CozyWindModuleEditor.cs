//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

#if ZEPHYR
using DistantLands.Zephyr;
#endif
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(CozyWindModule))]
#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    public class CozyWindModuleEditor : CozyBiomeModuleEditor
    {
        CozyWindModule windModule;
        public VisualElement GraphInformationContainer => root.Q<VisualElement>("graph-information-container");
        public VisualElement SelectionContainer => root.Q<VisualElement>("selection-container");
        public VisualElement GlobalSettingsContainer => root.Q<VisualElement>("global-settings-container");

        public Label Direction => root.Q<Label>("direction");
        public Label MainWind => root.Q<Label>("main-wind");
        public Label PulseMagnitude => root.Q<Label>("pulse-magnitude");
        public Label PulseFrequency => root.Q<Label>("pulse-frequency");
        VisualElement root;

        private float startingAngle;
        private float startingWindAngle;
        public bool draggingHandles;
        private bool dragging;
        private float mouseAngle;

        float time = 0;
        private static double lastTickTime;

        readonly static Color WHEEL_BG = new Color(0.35f, 0.35f, 0.35f);

        readonly static Gradient compassGradient = new()
        {
            colorKeys = new GradientColorKey[7] {
                new(Color.orangeRed, 0),
                new(Color.bisque, 0.15f),
                new(Color.purple, 0.3f),
                new(Color.softYellow, 0.55f),
                new(Color.orange, 0.7f),
                new(Color.blanchedAlmond, 0.82f),
                new(Color.orangeRed, 1)
            }
        };

        VisualElement compass;

        public override ModuleWidget[] ModuleWidgets => new ModuleWidget[]
        {
            new(ModuleWidget.WidgetSize.Small, "Wind Direction", true,
                () => {
                    DraggableElement draggable = new DraggableElement();
                    draggable.pixelThreshold = 1;
                    draggable.OnDrag += (v) => {
                        float heading = (serializedObject.FindProperty("heading").FindPropertyRelative("value").floatValue + (v * 10f));
                        windModule.Heading = heading;
                        weatherEditor.UpdateWidgets();
                    };
                    VisualElement container = draggable.NewContainer();
                    container.style.paddingBottom = 8;
                    container.style.paddingLeft = 8;
                    container.style.paddingRight = 8;
                    container.style.paddingTop = 8;
                    container.style.alignItems = Align.Center;
                    container.style.justifyContent = Justify.Center;
                    container.style.flexGrow = 1;

                    string[] directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
                    int index = Mathf.RoundToInt(serializedObject.FindProperty("heading").FindPropertyRelative("value").floatValue / 45f) % 8;

                    Label clockLabel = new Label();
                    clockLabel.pickingMode = PickingMode.Ignore;
                    clockLabel.name = "clockLabel";
                    clockLabel.text = directions[index];
                    clockLabel.style.fontSize = 24;
                    clockLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    clockLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                    clockLabel.style.color = Branding.orange;
                    container.Add(clockLabel);

                    Label fuzzyTimeLabel = new Label();
                    fuzzyTimeLabel.pickingMode = PickingMode.Ignore;
                    fuzzyTimeLabel.name = "fuzzyTimeLabel";
                    fuzzyTimeLabel.style.marginTop = 2;
                    fuzzyTimeLabel.text = $"{serializedObject.FindProperty("heading").FindPropertyRelative("value").floatValue % 360:F0}° (± {windModule.HeadingVariance:F0}°)";
                    container.Add(fuzzyTimeLabel);

                    return container;
                },
                (widget) =>
                {
                    string[] directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
                    int index = Mathf.RoundToInt(serializedObject.FindProperty("heading").FindPropertyRelative("value").floatValue / 45f) % 8;

                    widget.container.Q<Label>("clockLabel").text = directions[index];
                    widget.container.Q<Label>("fuzzyTimeLabel").text = $"{serializedObject.FindProperty("heading").FindPropertyRelative("value").floatValue % 360:F0}° (± {windModule.HeadingVariance:F0}°)";
                }
            ),
        };

        void OnEnable()
        {
            if (!target)
                return;

            windModule = (CozyWindModule)target;
        }

        public override VisualElement DisplayBiomeUI()
        {
            return BuildUI(isBiomeUI: true);
        }

        public override VisualElement DisplayUI()
        {
            return BuildUI(isBiomeUI: false);
        }

        private VisualElement BuildUI(bool isBiomeUI)
        {
            root = new VisualElement();

            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Globals.uss"
            ));

            CreateCompassUI(out Label directionLabel, out Label directionInfo);

            void SetLabels()
            {
                if (!windModule) return;

                string[] directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
                int index = Mathf.RoundToInt(windModule.Heading / 45f) % 8;

                directionLabel.text = directions[index];
                directionInfo.text = $"{serializedObject.FindProperty("heading").FindPropertyRelative("value").floatValue % 360:F0}° (± {windModule.HeadingVariance:F0}°)";
            }

            CreateWindSettings(SetLabels);

            if (!isBiomeUI)
            {
                // Additional sections specific to standard UI
                Label windZoneLabel = new Label { text = "Wind Zone" };
                windZoneLabel.AddToClassList("h1");
                root.Add(windZoneLabel);

                VisualElement windZoneContainer = new VisualElement();
                windZoneContainer.AddToClassList("section-bg");
                root.Add(windZoneContainer);

                PropertyField windZone = new PropertyField();
                windZone.BindProperty(serializedObject.FindProperty("windZone"));
                windZoneContainer.Add(windZone);

                PropertyField windZoneMultiplier = new PropertyField();
                windZoneMultiplier.BindProperty(serializedObject.FindProperty("windZoneMultiplier"));
                windZoneContainer.Add(windZoneMultiplier);

                Label zephyrLabel = new Label { text = "Zephyr" };
                zephyrLabel.AddToClassList("h1");
                root.Add(zephyrLabel);

                VisualElement zephyrContainer = new VisualElement();
                zephyrContainer.AddToClassList("section-bg");
                root.Add(zephyrContainer);

#if ZEPHYR
                if (ZephyrWind.Instance)
                {
                    Tooltip tooltip = new Tooltip("Setup Zephyr integration settings in the Integrations window!");
                    zephyrContainer.Add(tooltip);
                }
                else
                {
                    Tooltip tooltip = new Tooltip("Zephyr is not currently setup in this scene. Please set up Zephyr to use Zephyr with COZY!");
                    zephyrContainer.Add(tooltip);
                }
#else
                Tooltip tooltip = new Tooltip("Zephyr is not currently installed in your project.");
                zephyrContainer.Add(tooltip);
#endif

                Label shaderLabel = new Label { text = "Shaders" };
                shaderLabel.AddToClassList("h1");
                root.Add(shaderLabel);

                VisualElement shaderContainer = new VisualElement();
                shaderContainer.AddToClassList("section-bg");
                root.Add(shaderContainer);

                PropertyField useShaders = new PropertyField();
                useShaders.BindProperty(serializedObject.FindProperty("useShaders"));
                shaderContainer.Add(useShaders);

                PropertyField globalShaderProperties = new PropertyField();
                globalShaderProperties.BindProperty(serializedObject.FindProperty("globalShaderProperties"));
                globalShaderProperties.enabledSelf = serializedObject.FindProperty("useShaders").boolValue;
                shaderContainer.Add(globalShaderProperties);
            }

            SetLabels();
            return root;
        }

        private void CreateCompassUI(out Label directionLabel, out Label directionInfo)
        {
            VisualElement compassContainer = new VisualElement();
            compassContainer.style.justifyContent = Justify.Center;
            compassContainer.style.flexDirection = FlexDirection.Row;
            compassContainer.style.backgroundColor = new Color(0, 0, 0, 0.35f);
            compassContainer.style.marginBottom = 10;
            root.Add(compassContainer);

            compass = new VisualElement();
            compass.style.height = 300;
            compass.style.width = 300;
            compass.style.marginTop = 10;
            compass.style.marginBottom = 10;
            compassContainer.Add(compass);

            VisualElement infoContainer = new VisualElement();
            infoContainer.style.position = Position.Absolute;
            infoContainer.style.top = 0;
            infoContainer.style.left = 0;
            infoContainer.style.right = 0;
            infoContainer.style.bottom = 0;
            infoContainer.style.alignContent = Align.Center;
            infoContainer.style.justifyContent = Justify.Center;
            infoContainer.style.unityTextAlign = TextAnchor.MiddleCenter;
            compass.Add(infoContainer);

            directionLabel = new Label();
            infoContainer.Add(directionLabel);
            directionLabel.style.color = Color.orange;
            directionLabel.style.fontSize = 60;
            directionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

            directionInfo = new Label();
            infoContainer.Add(directionInfo);
            directionInfo.style.unityFontStyleAndWeight = FontStyle.Bold;
            directionInfo.text = "Wind Heading";

            compass.schedule.Execute(() =>
            {
                time += 0.003f * windModule.WindStrength;

                if (compass != null)
                    compass.MarkDirtyRepaint();

            }).Every(50);

            compass.generateVisualContent += ctx =>
            {
                var painter = ctx.painter2D;
                float width = compass.contentRect.width;
                float height = compass.contentRect.height;
                Vector2 center = compass.contentRect.center;
                float radius = width * 0.35f;

                painter.lineCap = LineCap.Round;
                painter.strokeColor = WHEEL_BG;
                painter.lineWidth = 2f;

                // Draw Compass Ticks
                painter.BeginPath(); painter.MoveTo(new Vector2(0.7f * width, 0.5f * height)); painter.LineTo(new Vector2(0.85f * width, 0.5f * height)); painter.Stroke();
                painter.BeginPath(); painter.MoveTo(new Vector2(0.15f * width, 0.5f * height)); painter.LineTo(new Vector2(0.3f * width, 0.5f * height)); painter.Stroke();
                painter.BeginPath(); painter.MoveTo(new Vector2(0.5f * width, 0.3f * height)); painter.LineTo(new Vector2(0.5f * width, 0.15f * height)); painter.Stroke();
                painter.BeginPath(); painter.MoveTo(new Vector2(0.5f * width, 0.7f * height)); painter.LineTo(new Vector2(0.5f * width, 0.85f * height)); painter.Stroke();

                painter.lineWidth = 20;
                painter.BeginPath();
                painter.Arc(center, radius, 0, 360, ArcDirection.Clockwise);
                painter.Stroke();

                float heading = serializedObject.FindProperty("heading").FindPropertyRelative("value").floatValue;
                float headingVariance = Mathf.Max(windModule.HeadingVariance, 6);

                Gradient gradient = new()
                {
                    colorKeys = new GradientColorKey[3]
                    {
                        new(compassGradient.Evaluate(Mathf.Repeat(time, 1)), 0),
                        new(compassGradient.Evaluate(Mathf.Repeat(time + 0.1f, 1)), 0.5f),
                        new(compassGradient.Evaluate(Mathf.Repeat(time + 0.2f, 1)), 1)
                    }
                };

                void DrawTriangle(float angle)
                {
                    float ARROW_WIDTH = 0.07f;
                    float ARROW_DEPTH = 0.1f;
                    Vector2[] points = new Vector2[3] {
                        new Vector2(0.37f * width, -ARROW_WIDTH * height),
                        new Vector2(0.37f * width,  ARROW_WIDTH * height),
                        new Vector2((0.37f + ARROW_DEPTH) * width, 0f)
                    };

                    Vector2[] rotated = new Vector2[3];
                    for (int i = 0; i < 3; i++)
                        rotated[i] = RotatePoint(points[i] + center, center, angle - 90f);

                    painter.fillColor = gradient.Evaluate(Mathf.Abs(Mathf.DeltaAngle(heading - 90f, 45f) / 180f));
                    painter.strokeColor = painter.fillColor;

                    painter.BeginPath();
                    painter.MoveTo(rotated[0]);
                    painter.LineTo(rotated[1]);
                    painter.LineTo(rotated[2]);
                    painter.ClosePath();
                    painter.Fill();
                }

                DrawTriangle(heading);

                painter.BeginPath();
                painter.lineCap = LineCap.Round;
                painter.strokeGradient = new Gradient()
                {
                    colorKeys = new GradientColorKey[3] {
                        new(gradient.Evaluate(Mathf.Abs(Mathf.DeltaAngle(heading - headingVariance - 90f, 45f) / 180f)), 0),
                        new(gradient.Evaluate(Mathf.Abs(Mathf.DeltaAngle(heading - 90f, 45f) / 180f)), 0.5f),
                        new(gradient.Evaluate(Mathf.Abs(Mathf.DeltaAngle(heading + headingVariance - 90f, 45f) / 180f)), 1)
                    }
                };
                painter.Arc(center, radius, heading - headingVariance - 90f, heading + headingVariance - 90f, ArcDirection.Clockwise);
                painter.Stroke();

                painter.strokeColor = new Color(1, 1, 1, 0.3f);

                // Highlight Handles
                if (Mathf.Abs(Mathf.DeltaAngle(heading - headingVariance, mouseAngle)) < 5)
                {
                    painter.BeginPath();
                    painter.lineCap = LineCap.Round;
                    painter.Arc(center, radius, heading - headingVariance - 90f, heading - headingVariance - 89f, ArcDirection.Clockwise);
                    painter.Stroke();
                }

                if (Mathf.Abs(Mathf.DeltaAngle(heading + headingVariance, mouseAngle)) < 5)
                {
                    painter.BeginPath();
                    painter.lineCap = LineCap.Round;
                    painter.Arc(center, radius, heading + headingVariance - 91f, heading + headingVariance - 90f, ArcDirection.Clockwise);
                    painter.Stroke();
                }

                painter.lineWidth = 14;
                painter.strokeColor = new Color(0, 0, 0, 0.3f);
                painter.lineCap = LineCap.Butt;

                painter.BeginPath(); painter.Arc(center, radius, heading - headingVariance - 89f, heading - headingVariance - 88f, ArcDirection.Clockwise); painter.Stroke();
                painter.BeginPath(); painter.Arc(center, radius, heading - headingVariance - 92f, heading - headingVariance - 91f, ArcDirection.Clockwise); painter.Stroke();
                painter.BeginPath(); painter.Arc(center, radius, heading + headingVariance - 89f, heading + headingVariance - 88f, ArcDirection.Clockwise); painter.Stroke();
                painter.BeginPath(); painter.Arc(center, radius, heading + headingVariance - 92f, heading + headingVariance - 91f, ArcDirection.Clockwise); painter.Stroke();

                painter.lineWidth = 10;
                painter.BeginPath(); painter.Arc(center, radius, heading - headingVariance - 90.5f, heading - headingVariance - 89.5f, ArcDirection.Clockwise); painter.Stroke();
                painter.BeginPath(); painter.Arc(center, radius, heading + headingVariance - 90.5f, heading + headingVariance - 89.5f, ArcDirection.Clockwise); painter.Stroke();
            };

            void UpdateMouseAngle(EventBase evt, bool isShiftPressed)
            {
                if (draggingHandles)
                {
                    float angle = Mathf.Abs(Mathf.DeltaAngle(serializedObject.FindProperty("heading").FindPropertyRelative("value").floatValue, mouseAngle));
                    windModule.HeadingVariance = isShiftPressed ? Mathf.Round(Mathf.Clamp(angle, 0, 180f) / 15f) * 15f : Mathf.Clamp(angle, 0, 180f);
                }
                else
                {
                    float angle = Mathf.Repeat(startingWindAngle + Mathf.DeltaAngle(startingAngle, mouseAngle), 360f);
                    windModule.Heading = isShiftPressed ? Mathf.Round(angle / 45f) * 45f : angle;
                }
                EditorUtility.SetDirty(windModule);
            }

            compass.RegisterCallback<MouseMoveEvent>(evt =>
            {
                Vector2 dir = evt.localMousePosition - compass.contentRect.center;
                mouseAngle = Mathf.Repeat(Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg, 360f);

                if (dragging)
                {
                    UpdateMouseAngle(evt, evt.shiftKey);
                    compass.MarkDirtyRepaint();
                }
            });

            compass.RegisterCallback<MouseDownEvent>(evt =>
            {
                Vector2 dir = evt.localMousePosition - compass.contentRect.center;
                startingAngle = Mathf.Repeat(Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg, 360f);

                float currentHeading = serializedObject.FindProperty("heading").FindPropertyRelative("value").floatValue;
                draggingHandles = Mathf.Abs(Mathf.DeltaAngle(startingAngle, currentHeading + windModule.HeadingVariance)) < 6 ||
                                  Mathf.Abs(Mathf.DeltaAngle(startingAngle, currentHeading - windModule.HeadingVariance)) < 6;

                startingWindAngle = currentHeading;
                dragging = true;

                Undo.RecordObject(windModule, "Change Wind Direction");
                MouseCaptureController.CaptureMouse(compass);
            });

            compass.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (dragging)
                {
                    UpdateMouseAngle(evt, evt.shiftKey);
                    dragging = false;
                    MouseCaptureController.ReleaseMouse(compass);
                    compass.MarkDirtyRepaint();
                }
            });
        }

        private void CreateWindSettings(System.Action onPropertyChange)
        {
            Label windSettingsLabel = new Label { text = "Wind Settings" };
            windSettingsLabel.AddToClassList("h1");
            root.Add(windSettingsLabel);

            VisualElement windSettingsContainer = new VisualElement();
            windSettingsContainer.AddToClassList("section-bg");
            root.Add(windSettingsContainer);

            string[] properties = {
                "heading", "headingVariance", "headingChangeRate", "windStrength",
                "windSpeed", "windSpeedVariance", "windSpeedChangeRate", "gustingPower", "gustingFrequency"
            };

            foreach (var propName in properties)
            {
                PropertyField field = new PropertyField();
                field.BindProperty(serializedObject.FindProperty(propName));
                if (propName == "heading" || propName == "headingVariance")
                {
                    field.RegisterValueChangeCallback(evt =>
                    {
                        compass.MarkDirtyRepaint();
                        onPropertyChange?.Invoke();
                    });
                }
                windSettingsContainer.Add(field);
            }
        }

        public override void OpenDocumentationURL()
        {
            Application.OpenURL("https://distant-lands.gitbook.io/cozy-weather-4/using-cozy/modules/atmosphere-module-1");
        }

        private static Vector2 RotatePoint(Vector2 point, Vector2 pivot, float angleDegrees)
        {
            float angleRadians = angleDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angleRadians);
            float sin = Mathf.Sin(angleRadians);

            float dx = point.x - pivot.x;
            float dy = point.y - pivot.y;

            return new Vector2(dx * cos - dy * sin + pivot.x, dx * sin + dy * cos + pivot.y);
        }
    }

    [CustomPropertyDrawer(typeof(CozyWindModule.WindProperty))]
    public class WindPropertyEditor : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement root = new VisualElement();

            PropertyField propertyType = new PropertyField();
            propertyType.BindProperty(property.FindPropertyRelative("propertyType"));
            root.Add(propertyType);

            PropertyField propertyName = new PropertyField();
            propertyName.BindProperty(property.FindPropertyRelative("propertyName"));
            root.Add(propertyName);

            return root;
        }
    }
}