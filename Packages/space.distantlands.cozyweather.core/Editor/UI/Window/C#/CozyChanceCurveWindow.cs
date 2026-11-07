//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace DistantLands.Cozy.EditorScripts
{
    public class XAxisBridge : ScriptableObject
    {
        public Temperature temperature;
        public Humidity humidity;
        public Pressure pressure;
        public UVIndex uvIndex;
        public AirQuality airQuality;
        public float accumulatedSnow;
        public float accumulatedRain;
        public PrecipitationChance convectiveRainChance;
        public PrecipitationChance stratiformRainChance;
        public SeasonName season;
        public MeridiemDate date;
        public MeridiemTime time;
        public float windSpeed;


        public ChanceConditionType conditionType;
        public string TargetPropertyName => conditionType switch
        {
            ChanceConditionType.Temperature => "temperature",
            ChanceConditionType.Humidity => "humidity",
            ChanceConditionType.Pressure => "pressure",
            ChanceConditionType.UVIndex => "uvIndex",
            ChanceConditionType.AirQuality => "airQuality",
            ChanceConditionType.ConvectiveRainChance => "convectiveRainChance",
            ChanceConditionType.StratiformRainChance => "stratiformRainChance",
            ChanceConditionType.AccumulatedRain => "accumulatedRain",
            ChanceConditionType.AccumulatedSnow => "accumulatedSnow",
            ChanceConditionType.Season => "season",
            ChanceConditionType.YearPercentage => "date",
            ChanceConditionType.Time => "time",
            ChanceConditionType.WindSpeed => "windSpeed",
            _ => "temperature"
        };

        public float PropertyToFloat()
        {
            return conditionType switch
            {
                ChanceConditionType.Temperature => temperature.InRange(0, 100),
                ChanceConditionType.Humidity => humidity / 100f,
                ChanceConditionType.Pressure => pressure.Normalized,
                ChanceConditionType.UVIndex => uvIndex.Normalized,
                ChanceConditionType.AirQuality => airQuality.Normalized,
                ChanceConditionType.ConvectiveRainChance => convectiveRainChance.Normalized,
                ChanceConditionType.StratiformRainChance => stratiformRainChance.Normalized,
                ChanceConditionType.Season => ((float)season / 4f),
                ChanceConditionType.YearPercentage => date.YearPercentage,
                ChanceConditionType.Time => time,
                ChanceConditionType.WindSpeed => windSpeed / 5f,
                _ => 0f
            };
        }

        public void FloatToProperty(float input)
        {
            switch (conditionType)
            {
                case ChanceConditionType.Temperature:
                    temperature = new Temperature(Mathf.Lerp(0, 100, input), TemperatureUnit.Fahrenheit);
                    break;
                case ChanceConditionType.Humidity:
                    humidity = new Humidity(Mathf.Lerp(0, 100, input));
                    break;
                case ChanceConditionType.Pressure:
                    pressure = new Pressure(Mathf.Lerp(28.5f, 31f, input), PressureUnit.inHg);
                    break;
                case ChanceConditionType.UVIndex:
                    uvIndex = new UVIndex(Mathf.Lerp(0, 15, input));
                    break;
                case ChanceConditionType.AirQuality:
                    airQuality = new AirQuality(Mathf.Lerp(0, 500, input));
                    break;
                case ChanceConditionType.ConvectiveRainChance:
                    convectiveRainChance = new PrecipitationChance(Mathf.Lerp(0, 100, input));
                    break;
                case ChanceConditionType.StratiformRainChance:
                    stratiformRainChance = new PrecipitationChance(Mathf.Lerp(0, 100, input));
                    break;
                case ChanceConditionType.Season:
                    season = (SeasonName)Mathf.Floor(input * 3.99f);
                    break;
                case ChanceConditionType.YearPercentage:
                    date = new MeridiemDate((int)(input * CozyPreferences.DaysPerYear));
                    break;
                case ChanceConditionType.Time:
                    time = input;
                    break;
                case ChanceConditionType.WindSpeed:
                    windSpeed = input * 5f;
                    break;
            }
        }

        public static string CurrentPropertyString(float input, ChanceConditionType chanceConditionType)
        {
            return chanceConditionType switch
            {
                ChanceConditionType.Temperature => new Temperature(Mathf.Lerp(0, 100, input), TemperatureUnit.Fahrenheit).ToString(),
                ChanceConditionType.Humidity => new Humidity(Mathf.Lerp(0, 100, input)).ToString(),
                ChanceConditionType.Pressure => new Pressure(Mathf.Lerp(28.5f, 31f, input), PressureUnit.inHg).ToString(),
                ChanceConditionType.UVIndex => new UVIndex(Mathf.Lerp(0, 15, input)).ToString(),
                ChanceConditionType.AirQuality => new AirQuality(Mathf.Lerp(0, 500, input)).ToString(),
                ChanceConditionType.ConvectiveRainChance => new PrecipitationChance(input * 100f).ToString(),
                ChanceConditionType.StratiformRainChance => new PrecipitationChance(input * 100f).ToString(),
                ChanceConditionType.YearPercentage => new MeridiemDate((int)(input * CozyPreferences.DaysPerYear)).ToString(),
                ChanceConditionType.Time => ((MeridiemTime)input).ToString(),
                ChanceConditionType.WindSpeed => $"{input * 5}",
                _ => "",
            };
        }
    }

#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    public class CozyChanceCurveWindow : EditorWindow
    {
        VisualElement root;
        public static SerializedProperty property;
        public static SerializedProperty keysArray;
        Action action;
        int selected = -1;
        bool isDragging = false;
        public static bool radial = false;
        public static Vector2 winSize = new Vector2(200, 320);
        Vector2 dragStart = Vector2.zero;
        float initialTime = 0;
        float initialValue = 0;
        VarType varType;
        float minFloat;
        float maxFloat;
        Vector2 mousePos;
        private static CozyChanceCurveWindow wnd;

        bool hoveringOverNode;
        private XAxisBridge _bridge;
        private SerializedObject _bridgeSerialized;

        VisualElement graphPanel;

        void OnEnable()
        {
            _bridge = ScriptableObject.CreateInstance<XAxisBridge>();
            _bridge.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
            _bridgeSerialized = new SerializedObject(_bridge);
        }

        public static void ShowPropertyEditor(SerializedProperty property, Action action)
        {
            CozyChanceCurveWindow.property = property;
            wnd = CreateInstance<CozyChanceCurveWindow>();
            wnd.titleContent = new GUIContent(property.displayName);
            wnd.action = action;
            keysArray = property.FindPropertyRelative("keys");

            Vector2 mousePos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);

            float width = wnd.position.x;
            float height = wnd.position.y;
            float padding = 80f;

            // Apply offset
            float x = mousePos.x;
            float y = mousePos.y;

            // Optional: Clamp to stay within the screen bounds
            Rect main = EditorGUIUtility.GetMainWindowPosition(); // Full Unity Editor window bounds
            x = Mathf.Clamp(x, main.xMin + padding, main.xMax - (width + padding));
            y = Mathf.Clamp(y, main.yMin + padding, main.yMax - (height + padding));

            wnd.ShowAuxWindow();
            wnd.position = new Rect(x, y, winSize.x, winSize.y);
            wnd.minSize = new Vector2(350, 320);

        }

        void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Delete)
            {
                if (selected != -1)
                {
                    keysArray.DeleteArrayElementAtIndex(selected);
                    selected = -1;
                    keysArray.serializedObject.ApplyModifiedProperties();
                    keysArray.serializedObject.Update();
                    CreateGUI();
                }
            }

            // If there's a click event and this window is not focused, close it
            if (Event.current.type == EventType.MouseDown && focusedWindow != this)
            {
                Close();
            }

        }

        void OnDisable()
        {
            winSize = wnd.position.size;


            action.Invoke();
        }


        public void CreateGUI()
        {
            rootVisualElement.Clear();

            if (property == null || property.serializedObject == null)
            {
                Close();
                return;
            }

            root = rootVisualElement;

            StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Globals.uss"
            );
            root.styleSheets.Add(style);

            VisualElement graphContainer = new VisualElement();
            graphContainer.AddToClassList("chanceGraphContainer");
            root.Add(graphContainer);


            Label mouseYLabel = new Label();
            Label mouseXLabel = new Label();

            Label nodeLabel = new Label();
            nodeLabel.AddToClassList("chanceGraphNodeLabel");
            nodeLabel.style.display = DisplayStyle.None;

            var currentCondition = (ChanceConditionType)property.FindPropertyRelative("curveType").enumValueIndex;



            graphContainer.RegisterCallback<MouseMoveEvent>(evt =>
            {
                mousePos = evt.localMousePosition;
                graphContainer.MarkDirtyRepaint();
            });

            graphContainer.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                mousePos = Vector2.zero;
                graphContainer.MarkDirtyRepaint();
            });

            graphContainer.generateVisualContent += (ctx) =>
            {
                var painter = ctx.painter2D;
                float width = graphContainer.contentRect.width;
                float height = graphContainer.contentRect.height;

                painter.strokeColor = new Color(1, 1, 1, 0.2f);
                painter.lineWidth = 1;

                painter.BeginPath();
                painter.MoveTo(new Vector2(30, 0));
                painter.LineTo(new Vector2(30, height));
                painter.Stroke();
                painter.BeginPath();
                painter.MoveTo(new Vector2(width - 30, 0));
                painter.LineTo(new Vector2(width - 30, height));
                painter.Stroke();
                painter.BeginPath();
                painter.MoveTo(new Vector2(0, height - 24));
                painter.LineTo(new Vector2(width, height - 24));
                painter.Stroke();
                painter.BeginPath();
                painter.MoveTo(new Vector2(0, 24));
                painter.LineTo(new Vector2(width, 24));
                painter.Stroke();

                int count = 10;

                for (int x = 1; x < count; x++)
                {
                    if (x % 2 == 0)
                        painter.strokeColor = new Color(1, 1, 1, 0.05f);
                    else
                        painter.strokeColor = new Color(1, 1, 1, 0.025f);

                    painter.BeginPath();
                    painter.MoveTo(new Vector2(30 + (x * (width - 60) / (count)), 24));
                    painter.LineTo(new Vector2(30 + (x * (width - 60) / (count)), height - 24));
                    painter.Stroke();
                }
                for (int y = 1; y < count; y++)
                {
                    if (y % 2 == 0)
                        painter.strokeColor = new Color(1, 1, 1, 0.05f);
                    else
                        painter.strokeColor = new Color(1, 1, 1, 0.025f);
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(30, 24 + (y * (height - 48) / (count))));
                    painter.LineTo(new Vector2(width - 30, 24 + (y * (height - 48) / (count))));
                    painter.Stroke();
                }

                painter.strokeColor = Branding.blue;
                painter.fillColor = Branding.blue * new Color(1, 1, 1, 0.1f);
                painter.BeginPath();

                for (int i = 0; i < keysArray.arraySize; i++)
                {
                    SerializedProperty keyProperty = keysArray.GetArrayElementAtIndex(i);
                    float t = 1 - keyProperty.FindPropertyRelative("value").floatValue;
                    float v = keyProperty.FindPropertyRelative("time").floatValue;

                    if (i == 0)
                    {
                        painter.MoveTo(new Vector2(35, t * (height - 60) + 30));
                    }

                    painter.LineTo(new Vector2(v * (width - 70) + 33, t * (height - 60) + 30));

                    if (i == keysArray.arraySize - 1)
                        painter.LineTo(new Vector2(width - 35, t * (height - 60) + 30));
                }

                painter.Stroke();

                painter.LineTo(new Vector2(width - 35, (height - 48) + 24));
                painter.LineTo(new Vector2(35, (height - 48) + 24));
                painter.Fill();


                painter.strokeColor = new Color(0.5f, 0.5f, 0.5f, 1);

                if (mousePos.x == 0 || mousePos.y == 0 || hoveringOverNode || isDragging)
                    return;

                painter.SetDashPattern(5, 15);

                painter.BeginPath();
                painter.MoveTo(new Vector2(Mathf.Clamp(mousePos.x, 30, width - 30), Mathf.Clamp(mousePos.y, 24, height - 24)));
                painter.LineTo(new Vector2(0, Mathf.Clamp(mousePos.y, 30, height - 24)));
                painter.Stroke();
                painter.BeginPath();
                painter.MoveTo(new Vector2(Mathf.Clamp(mousePos.x, 30, width - 30), Mathf.Clamp(mousePos.y, 24, height - 24)));
                painter.LineTo(new Vector2(width, Mathf.Clamp(mousePos.y, 30, height - 24)));
                painter.Stroke();
                painter.BeginPath();
                painter.MoveTo(new Vector2(Mathf.Clamp(mousePos.x, 30, width - 30), Mathf.Clamp(mousePos.y, 24, height - 24)));
                painter.LineTo(new Vector2(Mathf.Clamp(mousePos.x, 30, width - 30), 0));
                painter.Stroke();
                painter.BeginPath();
                painter.MoveTo(new Vector2(Mathf.Clamp(mousePos.x, 30, width - 30), Mathf.Clamp(mousePos.y, 24, height - 24)));
                painter.LineTo(new Vector2(Mathf.Clamp(mousePos.x, 30, width - 30), height));
                painter.Stroke();

            };

            graphPanel = new VisualElement();
            graphPanel.AddToClassList("chanceGraph");
            graphContainer.Add(graphPanel);
            graphPanel.Add(nodeLabel);

            Label zero = new Label();
            zero.AddToClassList("chanceGraphLegend");
            zero.style.bottom = 0;
            zero.text = "0%";
            graphPanel.Add(zero);

            Label oneHundred = new Label();
            oneHundred.AddToClassList("chanceGraphLegend");
            oneHundred.style.top = 0;
            oneHundred.text = "100%";
            graphPanel.Add(oneHundred);


            Label zeroTime = new Label();
            zeroTime.AddToClassList("chanceGraphLegend");
            zeroTime.style.bottom = 0;
            zeroTime.style.left = 0;
            zeroTime.style.translate = new Translate(new Length(0), new Length(20, LengthUnit.Pixel));
            zeroTime.text = XAxisBridge.CurrentPropertyString(0, currentCondition);
            graphPanel.Add(zeroTime);

            Label oneHundredTime = new Label();
            oneHundredTime.AddToClassList("chanceGraphLegend");
            oneHundredTime.style.bottom = 0;
            oneHundredTime.style.right = 0;
            oneHundredTime.style.translate = new Translate(new Length(0), new Length(20, LengthUnit.Pixel));
            oneHundredTime.text = XAxisBridge.CurrentPropertyString(1, currentCondition);
            graphPanel.Add(oneHundredTime);

            VisualElement propertiesContainer = new VisualElement();
            propertiesContainer.AddToClassList("section-bg");
            root.Add(propertiesContainer);

            var chanceTypeField = new DropdownField();
            chanceTypeField.BindProperty(property.FindPropertyRelative("curveType"));
            chanceTypeField.index = property.FindPropertyRelative("curveType").enumValueIndex;
            propertiesContainer.Add(chanceTypeField);

            VisualElement keyPropertiesContainer = new VisualElement();
            propertiesContainer.Add(keyPropertiesContainer);

            List<VisualElement> dots = new List<VisualElement>();

            void SelectDot(int index)
            {
                for (int i = 0; i < dots.Count; i++)
                {
                    VisualElement dot = dots[i];

                    if (i == selected)
                        dot.AddToClassList("selected");
                    else
                        dot.RemoveFromClassList("selected");
                }

                if (index > dots.Count)
                {
                    selected = -1;
                    return;
                }

                selected = index;
                CreateXAxisUI(keyPropertiesContainer, keysArray.GetArrayElementAtIndex(selected), currentCondition);
            }

            graphPanel.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    float t = evt.localPosition.x / graphPanel.contentRect.width;
                    float v = 1 - (evt.localPosition.y / graphPanel.contentRect.height);

                    int newIndex = keysArray.arraySize;

                    for (int i = 0; i < keysArray.arraySize; i++)
                    {
                        SerializedProperty keyProperty = keysArray.GetArrayElementAtIndex(i);

                        if (t < keyProperty.FindPropertyRelative("time").floatValue)
                        {
                            newIndex = i;
                            break;
                        }
                    }

                    if (keysArray.arraySize > 0)
                    {
                        Vector2 initialPoint;
                        Vector2 nextPoint;

                        if (keysArray.arraySize == newIndex)
                        {
                            initialPoint = new Vector2(keysArray.GetArrayElementAtIndex(keysArray.arraySize - 1).FindPropertyRelative("time").floatValue, keysArray.GetArrayElementAtIndex(keysArray.arraySize - 1).FindPropertyRelative("value").floatValue);
                            nextPoint = new Vector2(1, keysArray.GetArrayElementAtIndex(keysArray.arraySize - 1).FindPropertyRelative("value").floatValue);
                        }
                        else if (newIndex == 0)
                        {
                            initialPoint = new Vector2(0, keysArray.GetArrayElementAtIndex(0).FindPropertyRelative("value").floatValue);
                            nextPoint = new Vector2(keysArray.GetArrayElementAtIndex(0).FindPropertyRelative("time").floatValue, keysArray.GetArrayElementAtIndex(0).FindPropertyRelative("value").floatValue);
                        }
                        else
                        {
                            initialPoint = new Vector2(keysArray.GetArrayElementAtIndex(newIndex - 1).FindPropertyRelative("time").floatValue, keysArray.GetArrayElementAtIndex(newIndex - 1).FindPropertyRelative("value").floatValue);
                            nextPoint = new Vector2(keysArray.GetArrayElementAtIndex(newIndex).FindPropertyRelative("time").floatValue, keysArray.GetArrayElementAtIndex(newIndex).FindPropertyRelative("value").floatValue);
                        }

                        if (DistanceToLine(initialPoint, nextPoint, new Vector2(t, v)) > 0.08f)
                            return;
                    }

                    keysArray.InsertArrayElementAtIndex(newIndex);
                    SerializedProperty newKey = keysArray.GetArrayElementAtIndex(newIndex);

                    newKey.FindPropertyRelative("time").floatValue = t;
                    newKey.FindPropertyRelative("value").floatValue = v;

                    keysArray.serializedObject.ApplyModifiedProperties();
                    keysArray.serializedObject.Update();

                    SelectDot(newIndex);
                    isDragging = true;

                    CreateGUI();
                }
            });


            for (int i = 0; i < keysArray.arraySize; i++)
            {
                SerializedProperty keyProperty = keysArray.GetArrayElementAtIndex(i);
                VisualElement dot = new VisualElement();
                dot.AddToClassList("chanceGraphKey");
                dots.Add(dot);

                dot.style.left = new Length(keyProperty.FindPropertyRelative("time").floatValue * 100f, LengthUnit.Percent);
                dot.style.bottom = new Length(keyProperty.FindPropertyRelative("value").floatValue * 100f, LengthUnit.Percent);

                dot.RegisterCallback<MouseEnterEvent>(evt =>
                {
                    hoveringOverNode = true;
                    nodeLabel.style.display = DisplayStyle.Flex;
                    nodeLabel.style.left = dot.style.left;
                    nodeLabel.style.bottom = dot.style.bottom;

                    string xValueText = XAxisBridge.CurrentPropertyString(keyProperty.FindPropertyRelative("time").floatValue, currentCondition);

                    nodeLabel.text = $"{xValueText}, {Mathf.Round(keyProperty.FindPropertyRelative("value").floatValue * 100)}%";
                });

                dot.RegisterCallback<MouseLeaveEvent>(evt =>
                {
                    hoveringOverNode = false;
                    if (!isDragging)
                        nodeLabel.style.display = DisplayStyle.None;
                });

                int index = i;

                dot.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 0)
                    {
                        isDragging = true;
                        dot.CaptureMouse();
                        SelectDot(index);
                        dragStart = evt.position;
                        initialValue = keyProperty.FindPropertyRelative("value").floatValue;
                        initialTime = keyProperty.FindPropertyRelative("time").floatValue;
                        evt.StopPropagation();
                    }
                    if (evt.button == 1)
                    {
                        GenericMenu menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Delete"), false, () =>
                        {
                            keysArray.DeleteArrayElementAtIndex(index);
                            keysArray.serializedObject.ApplyModifiedProperties();
                            keysArray.serializedObject.Update();

                            CreateGUI();
                            return;
                        });

                        menu.ShowAsContext();
                    }
                });

                dot.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (!isDragging || selected != index) return;

                    if (!dot.HasMouseCapture())
                    {
                        dot.CaptureMouse();
                    }

                    // 1. Fetch the property dynamically using the CURRENT index.
                    // This prevents the reference from breaking when the array reorders.
                    SerializedProperty currentKeyProperty = keysArray.GetArrayElementAtIndex(index);

                    if (evt.shiftKey)
                    {
                        currentKeyProperty.FindPropertyRelative("time").floatValue = Mathf.Round(Mathf.Clamp01(initialTime + ((evt.position.x - dragStart.x) / graphPanel.contentRect.width)) * 10f) / 10f;
                        currentKeyProperty.FindPropertyRelative("value").floatValue = Mathf.Round(Mathf.Clamp01(initialValue - ((evt.position.y - dragStart.y) / graphPanel.contentRect.height)) * 10f) / 10f;
                    }
                    else
                    {
                        currentKeyProperty.FindPropertyRelative("time").floatValue = Mathf.Clamp01(initialTime + ((evt.position.x - dragStart.x) / graphPanel.contentRect.width));
                        currentKeyProperty.FindPropertyRelative("value").floatValue = Mathf.Clamp01(initialValue - ((evt.position.y - dragStart.y) / graphPanel.contentRect.height));
                    }


                    currentKeyProperty.serializedObject.ApplyModifiedProperties();
                    currentKeyProperty.serializedObject.Update();

                    bool reordered = false;

                    // Move backward
                    if (index > 0)
                    {
                        if (currentKeyProperty.FindPropertyRelative("time").floatValue < keysArray.GetArrayElementAtIndex(index - 1).FindPropertyRelative("time").floatValue)
                        {
                            // Move FIRST, then update indices
                            keysArray.MoveArrayElement(index, index - 1);
                            SelectDot(selected - 1);
                            index--;
                            reordered = true;
                        }
                    }

                    if (index < keysArray.arraySize - 1)
                    {
                        if (currentKeyProperty.FindPropertyRelative("time").floatValue > keysArray.GetArrayElementAtIndex(index + 1).FindPropertyRelative("time").floatValue)
                        {
                            // Move FIRST, then update indices
                            keysArray.MoveArrayElement(index, index + 1);
                            SelectDot(selected + 1);
                            index++;
                            reordered = true;
                        }
                    }

                    if (reordered)
                    {
                        keysArray.serializedObject.ApplyModifiedProperties();
                        keysArray.serializedObject.Update();

                        currentKeyProperty = keysArray.GetArrayElementAtIndex(index);
                    }


                    dot.style.left = new Length(currentKeyProperty.FindPropertyRelative("time").floatValue * 100f, LengthUnit.Percent);
                    dot.style.bottom = new Length(currentKeyProperty.FindPropertyRelative("value").floatValue * 100f, LengthUnit.Percent);

                    nodeLabel.style.left = dot.style.left;
                    nodeLabel.style.bottom = dot.style.bottom;


                    string xValueText = XAxisBridge.CurrentPropertyString(keyProperty.FindPropertyRelative("time").floatValue, currentCondition);

                    nodeLabel.text = $"{xValueText}, {Mathf.Round(currentKeyProperty.FindPropertyRelative("value").floatValue * 100)}%";

                    graphContainer.MarkDirtyRepaint();

                    CreateXAxisUI(keyPropertiesContainer, keysArray.GetArrayElementAtIndex(selected), currentCondition);

                });

                dot.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    isDragging = false;
                    dot.ReleaseMouse();

                    // 3. Rebuild the GUI so the other nodes snap to their proper visual places 
                    // and re-bind their indices properly.
                    CreateGUI();
                });


                graphPanel.Add(dot);
            }

            chanceTypeField.RegisterValueChangedCallback(evt =>
            {
                property.FindPropertyRelative("curveType").enumValueIndex = chanceTypeField.index;
                _bridgeSerialized.FindProperty("conditionType").enumValueIndex = chanceTypeField.index;
                _bridgeSerialized.ApplyModifiedProperties();
                property.serializedObject.ApplyModifiedProperties();
                if (selected != -1)
                    SelectDot(selected);


                zeroTime.text = XAxisBridge.CurrentPropertyString(0, currentCondition);
                oneHundredTime.text = XAxisBridge.CurrentPropertyString(1, currentCondition);
            });

            if (selected != -1)
                SelectDot(selected);

            // ScrollView scrollView = new ScrollView();
            // root.Add(scrollView);

            // VisualElement presetContainer = new VisualElement();
            // presetContainer.AddToClassList("chanceGraphPresetContainer");
            // scrollView.Add(presetContainer);

            // foreach (CurvePreset curve in presetCurves[currentCondition])
            // {
            //     VisualElement curvePreview = new VisualElement();
            //     curvePreview.AddToClassList("chanceGraphPreset");

            //     Label label = new Label();
            //     label.text = curve.name;
            //     curvePreview.Add(label);

            //     curvePreview.generateVisualContent += ctx =>
            //     {
            //         var painter = ctx.painter2D;
            //         float width = curvePreview.contentRect.width;
            //         float height = curvePreview.contentRect.height;

            //         painter.strokeColor = Branding.blue;
            //         painter.BeginPath();

            //         for (int i = 0; i < curve.curve.keys.Length; i++)
            //         {
            //             CurveKey key = curve.curve.keys[i];
            //             float t = 1 - key.value;
            //             float v = key.time;

            //             if (i == 0)
            //             {
            //                 painter.MoveTo(new Vector2(3, t * (height - 6) + 3));
            //             }

            //             painter.LineTo(new Vector2(v * (width - 6) + 3, t * (height - 6) + 3));

            //             if (i == curve.curve.keys.Length - 1)
            //                 painter.LineTo(new Vector2(width - 3, t * (height - 6) + 3));
            //         }

            //         painter.Stroke();

            //     };

            //     presetContainer.Add(curvePreview);
            // }
        
        }

        float DistanceToLine(Vector2 p1, Vector2 p2, Vector2 testPoint)
        {
            return Mathf.Abs((p2.y - p1.y) * testPoint.x - (p2.x - p1.x) * testPoint.y + p2.x * p1.y - p2.y * p1.x) / Mathf.Sqrt((p2.y - p1.y) * (p2.y - p1.y) + (p2.x - p1.x) * (p2.x - p1.x));
        }

        void CreateXAxisUI(VisualElement root, SerializedProperty property, ChanceConditionType targetType)
        {
            root.Clear();

            _bridge.conditionType = targetType;

            _bridge.FloatToProperty(property.FindPropertyRelative("time").floatValue);


            _bridgeSerialized.Update();

            SerializedProperty bridgeProp = _bridgeSerialized.FindProperty(_bridge.TargetPropertyName);

            PropertyField propertyField = new PropertyField(bridgeProp);
            propertyField.style.marginTop = 10;

            propertyField.TrackPropertyValue(bridgeProp, (changedProp) =>
            {
                _bridgeSerialized.ApplyModifiedProperties();
                _bridgeSerialized.Update();

                float newTime = _bridge.PropertyToFloat();

                property.serializedObject.Update();
                property.FindPropertyRelative("time").floatValue = newTime;
                property.serializedObject.ApplyModifiedProperties();

                bool reordered = false;

                if (selected > 0)
                {
                    if (property.FindPropertyRelative("time").floatValue < keysArray.GetArrayElementAtIndex(selected - 1).FindPropertyRelative("time").floatValue)
                    {
                        keysArray.MoveArrayElement(selected, selected - 1);
                        selected--;
                        reordered = true;
                    }
                }

                if (selected < keysArray.arraySize - 1)
                {
                    if (property.FindPropertyRelative("time").floatValue > keysArray.GetArrayElementAtIndex(selected + 1).FindPropertyRelative("time").floatValue)
                    {
                        keysArray.MoveArrayElement(selected, selected + 1);
                        selected++;
                        reordered = true;
                    }
                }

                if (reordered)
                {
                    keysArray.serializedObject.ApplyModifiedProperties();
                    keysArray.serializedObject.Update();
                }


                CreateGUI();
            });

            propertyField.Bind(_bridgeSerialized);

            var valueField = new TextField();
            valueField.label = "Chance";
            valueField.SetValueWithoutNotify($"{property.FindPropertyRelative("value").floatValue * 100f:F0}%");

            valueField.RegisterCallback<BlurEvent>(evt =>
            {
                float newVal;
                if (float.TryParse(valueField.text.Replace("%", ""), out newVal))
                {
                    property.serializedObject.Update();
                    property.FindPropertyRelative("value").floatValue = newVal / 100f;
                    property.serializedObject.ApplyModifiedProperties();
                    CreateGUI();
                }
            });

            root.Add(propertyField);
            root.Add(valueField);
        }

        public struct CurvePreset
        {
            public string name;
            public Cozy.WeightedChanceCurve curve;
            public CurvePreset(string name, Cozy.WeightedChanceCurve curve)
            {
                this.name = name;
                this.curve = curve;
            }
        }
        public static Dictionary<ChanceConditionType, List<CurvePreset>> presetCurves =
            new Dictionary<ChanceConditionType, List<CurvePreset>>()
        {
    {
        ChanceConditionType.Temperature, new List<CurvePreset>()
        {
            new CurvePreset("Only above freezing", new WeightedChanceCurve(new CurveKey(0.32f, 0), new CurveKey(0.34f, 1))),
            new CurvePreset("More likely above freezing", new WeightedChanceCurve(new CurveKey(0.32f, 0.5f), new CurveKey(0.34f, 1))),
            new CurvePreset("More likely below freezing", new WeightedChanceCurve(new CurveKey(0.32f, 1f), new CurveKey(0.34f, 0.5f))),
            new CurvePreset("Only below freezing", new WeightedChanceCurve(new CurveKey(0.32f, 1f), new CurveKey(0.34f, 0f))),
            new CurvePreset("More likely when cold", new WeightedChanceCurve(new CurveKey(0.32f, 1f), new CurveKey(0.75f, 0f))),
            new CurvePreset("More likely when warm", new WeightedChanceCurve(new CurveKey(0.32f, 0f), new CurveKey(0.75f, 1f))),
        }
    },
    {
        ChanceConditionType.Humidity, new List<CurvePreset>()
        {
            new CurvePreset("Only in high humidity", new WeightedChanceCurve(new CurveKey(0.25f, 0), new CurveKey(0.75f, 1))),
            new CurvePreset("Only in mid humidity", new WeightedChanceCurve(new CurveKey(0.15f, 0), new CurveKey(0.35f, 1), new CurveKey(0.65f, 1), new CurveKey(0.85f, 0))),
            new CurvePreset("Only in low humidity", new WeightedChanceCurve(new CurveKey(0.25f, 1), new CurveKey(0.75f, 0))),
        }
    },
    {
        ChanceConditionType.Pressure, new List<CurvePreset>()
        {
            new CurvePreset("More likely in low pressure", new WeightedChanceCurve(new CurveKey(0.25f, 1), new CurveKey(0.5f, 0.5f))),
            new CurvePreset("More likely in high pressure", new WeightedChanceCurve(new CurveKey(0.5f, 0.5f), new CurveKey(0.75f, 1f))),
        }
    },
    {
        ChanceConditionType.UVIndex, new List<CurvePreset>()
        {
            new CurvePreset("More likely in dim sun", new WeightedChanceCurve(new CurveKey(0.25f, 1), new CurveKey(0.5f, 0.5f))),
            new CurvePreset("More likely in bright sun", new WeightedChanceCurve(new CurveKey(0.5f, 0.5f), new CurveKey(0.75f, 1f))),
        }
    },
    {
        ChanceConditionType.AirQuality, new List<CurvePreset>()
        {
            new CurvePreset("More likely in fresh air", new WeightedChanceCurve(new CurveKey(0.25f, 1), new CurveKey(0.5f, 0.5f))),
            new CurvePreset("More likely in polluted air", new WeightedChanceCurve(new CurveKey(0.5f, 0.5f), new CurveKey(0.75f, 1f))),
        }
    }
        };
    }
}