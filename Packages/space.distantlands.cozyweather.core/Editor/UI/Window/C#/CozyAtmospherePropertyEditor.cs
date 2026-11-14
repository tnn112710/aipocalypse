//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace DistantLands.Cozy.EditorScripts
{
#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    public class CozyAtmospherePropertyEditor : EditorWindow
    {
        VisualElement root;
        public static SerializedProperty property;
        static List<CozyAtmospherePropertyEditor> gradientEditors = new List<CozyAtmospherePropertyEditor>();
        VisualElement timeline => root.Q<VisualElement>("timeline");
        VisualElement timelineDots => root.Q<VisualElement>("dots");
        VisualElement timelineClickArea => root.Q<VisualElement>("timeline-click-area");
        VisualElement gradient => root.Q<VisualElement>("gradient");
        VisualElement clock => root.Q<VisualElement>("clock");
        VisualElement clockContainer => root.Q<VisualElement>("clock-container");
        VisualElement clockDots => root.Q<VisualElement>("clock-dots");
        VisualElement timelineProperties => root.Q<VisualElement>("timeline-properties");
        VisualElement constantContainer => root.Q<VisualElement>("constant-container");
        VisualElement interpolatedContainer => root.Q<VisualElement>("interpolated-container");
        List<(VisualElement element, SerializedProperty property)> keys = new List<(VisualElement, SerializedProperty property)>();
        Action action;
        int selected = 0;
        float trackedTime = 0.5f;
        bool isDragging = false;
        public static bool radial = false;
        public static Vector2 winSize = new Vector2(300, 500);
        float dragStartX = 0;
        float initialTime = 0;
        VarType varType;
        float minFloat;
        float maxFloat;
        private static CozyAtmospherePropertyEditor wnd;

        public static void ShowPropertyEditor(SerializedProperty property, VarType varType, Action action)
        {
            CozyAtmospherePropertyEditor.property = property;
            wnd = CreateInstance<CozyAtmospherePropertyEditor>();
            wnd.titleContent = new GUIContent(property.displayName);
            wnd.action = action;
            wnd.varType = varType;

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
            wnd.minSize = new Vector2(350, 300);

        }

        void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Delete)
            {
                SerializedProperty interpolatedValue = property.FindPropertyRelative("interpolatedValue");

                interpolatedValue.DeleteArrayElementAtIndex(selected);
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
                selected = Mathf.Clamp(selected - 1, 0, interpolatedValue.arraySize - 1);
                CreateGUI();
                action.Invoke();
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

            SerializedProperty interpolatedValue = property.FindPropertyRelative("interpolatedValue");

            for (int iter = 0; iter < interpolatedValue.arraySize; iter++)
            {
                // First, sort interpolatedValue by "time"
                List<(float time, int originalIndex)> times = new List<(float, int)>();
                for (int i = 0; i < interpolatedValue.arraySize; i++)
                {
                    var time = PropertyConversion.GetMeridiemTime(interpolatedValue.GetArrayElementAtIndex(i).FindPropertyRelative("time"));
                    times.Add((time, i));
                }

                int originalSelectedIndex = Mathf.Clamp(selected, 0, interpolatedValue.arraySize - 1);

                // Sort by time
                times.Sort((a, b) => a.time.CompareTo(b.time));

                // Create a mapping from originalIndex -> sortedIndex
                Dictionary<int, int> newPositions = new Dictionary<int, int>();
                for (int sortedIndex = 0; sortedIndex < times.Count; sortedIndex++)
                {
                    newPositions[times[sortedIndex].originalIndex] = sortedIndex;
                }

                // Step 3: Determine new position of the originally selected index
                for (int i = 0; i < times.Count; i++)
                {
                    if (times[i].originalIndex == originalSelectedIndex)
                    {
                        selected = i;
                        break;
                    }
                }

                // Reorder elements in serialized property
                for (int i = 0; i < interpolatedValue.arraySize; i++)
                {
                    while (newPositions.ContainsKey(i) && newPositions[i] != i)
                    {
                        int target = newPositions[i];
                        interpolatedValue.MoveArrayElement(i, target);

                        // Update mapping to avoid repeating
                        int temp = newPositions[i];
                        newPositions[i] = newPositions[temp];
                        newPositions[temp] = temp;
                    }
                }
            }

            interpolatedValue.serializedObject.ApplyModifiedProperties();
            interpolatedValue.serializedObject.Update();


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

            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Window/UXML/AtmospherePropertyWindow.uxml"
            );
            asset.CloneTree(root);

            SerializedProperty interpolatedValue = property.FindPropertyRelative("interpolatedValue");
            SerializedProperty constantValue = property.FindPropertyRelative("constantValue.value");
            SerializedProperty interpolateValue = property.FindPropertyRelative("interpolateValue");

            PropertyField constantField = new PropertyField();
            constantField.BindProperty(constantValue);
            constantField.RegisterValueChangeCallback(evt =>
            {
                action.Invoke();
            });

            constantContainer.Add(constantField);

            root.Q<Toggle>("interpolate-value").BindProperty(interpolateValue);
            root.Q<Toggle>("interpolate-value").RegisterValueChangedCallback(evt =>
            {
                action.Invoke();
                constantContainer.style.display = interpolateValue.boolValue ? DisplayStyle.None : DisplayStyle.Flex;
                interpolatedContainer.style.display = interpolateValue.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            });

            constantContainer.style.display = interpolateValue.boolValue ? DisplayStyle.None : DisplayStyle.Flex;
            interpolatedContainer.style.display = interpolateValue.boolValue ? DisplayStyle.Flex : DisplayStyle.None;

            root.Q<ToolbarButton>("add").RegisterCallback<ClickEvent>(evt =>
            {
                interpolatedValue.InsertArrayElementAtIndex(0);
                PropertyConversion.SetMeridiemTime(interpolatedValue.GetArrayElementAtIndex(0).FindPropertyRelative("time"), 0);
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
                selected = 0;
                RegenerateFields();
                action.Invoke();

            });
            root.Q<ToolbarButton>("help").RegisterCallback<ClickEvent>(evt =>
            {
                Application.OpenURL("https://distant-lands.gitbook.io/cozy-weather-4");
            });

            timelineClickArea.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;

                if (evt.target != timelineClickArea && evt.target != timelineDots)
                    return;

                float time = evt.position.x / (float)timelineDots.resolvedStyle.width;
                int targetIndex = -1;

                keys.Sort((a, b) => a.element.style.left.value.value.CompareTo(b.element.style.left.value.value));

                float GetKeyTime(int index)
                {
                    return (float)keys[index].element.style.left.value.value / 100f;
                }

                for (int i = 0; i < interpolatedValue.arraySize; i++)
                {
                    float keyTime = GetKeyTime(i);

                    if (time > keyTime - 0.025f && time < keyTime + 0.025f)
                    {
                        targetIndex = i;
                        return;
                    }
                }

                //Create atmosphere property to grab the value from. Only works for color and float values!
                switch (varType)
                {
                    case VarType.Color:
                        AtmosphereProperty<ColorValue> atmosphereColorProperty = new AtmosphereProperty<ColorValue>();
                        atmosphereColorProperty.constantValue = property.FindPropertyRelative("constantValue").FindPropertyRelative("value").colorValue;
                        atmosphereColorProperty.interpolateValue = true;
                        atmosphereColorProperty.overrideValue = true;

                        List<AtmosphereProperty<ColorValue>.AtmosphereGradientKey> colorKeys = new List<AtmosphereProperty<ColorValue>.AtmosphereGradientKey>();
                        for (int i = 0; i < interpolatedValue.arraySize; i++)
                        {
                            AtmosphereProperty<ColorValue>.AtmosphereGradientKey colorKey = new AtmosphereProperty<ColorValue>.AtmosphereGradientKey();
                            colorKey.value = keys[i].property.FindPropertyRelative("value.value").colorValue;
                            colorKey.time = GetKeyTime(i);

                            if (colorKey.time > time && targetIndex == -1)
                                targetIndex = i;

                            colorKeys.Add(colorKey);
                        }

                        if (targetIndex == -1)
                            targetIndex = interpolatedValue.arraySize;

                        atmosphereColorProperty.interpolatedValue = colorKeys.ToArray();

                        //Insert the new value at the correct index
                        interpolatedValue.InsertArrayElementAtIndex(targetIndex);
                        PropertyConversion.SetMeridiemTime(interpolatedValue.GetArrayElementAtIndex(targetIndex).FindPropertyRelative("time"), time);
                        interpolatedValue.GetArrayElementAtIndex(targetIndex).FindPropertyRelative("value.value").colorValue = atmosphereColorProperty.Evaluate(time);
                        break;
                    case VarType.Float:
                        AtmosphereProperty<FloatValue> atmosphereFloatProperty = new AtmosphereProperty<FloatValue>();
                        atmosphereFloatProperty.constantValue = property.FindPropertyRelative("constantValue").FindPropertyRelative("value").floatValue;
                        atmosphereFloatProperty.interpolateValue = true;
                        atmosphereFloatProperty.overrideValue = true;

                        List<AtmosphereProperty<FloatValue>.AtmosphereGradientKey> floatKeys = new List<AtmosphereProperty<FloatValue>.AtmosphereGradientKey>();
                        for (int i = 0; i < interpolatedValue.arraySize; i++)
                        {
                            AtmosphereProperty<FloatValue>.AtmosphereGradientKey floatKey = new AtmosphereProperty<FloatValue>.AtmosphereGradientKey();
                            floatKey.value = keys[i].property.FindPropertyRelative("value.value").floatValue;
                            floatKey.time = GetKeyTime(i);

                            if (floatKey.time > time && targetIndex == -1)
                                targetIndex = i;

                            floatKeys.Add(floatKey);
                        }

                        if (targetIndex == -1)
                            targetIndex = interpolatedValue.arraySize;

                        atmosphereFloatProperty.interpolatedValue = floatKeys.ToArray();

                        //Insert the new value at the correct index
                        interpolatedValue.InsertArrayElementAtIndex(targetIndex);
                        PropertyConversion.SetMeridiemTime(interpolatedValue.GetArrayElementAtIndex(targetIndex).FindPropertyRelative("time"), time);
                        interpolatedValue.GetArrayElementAtIndex(targetIndex).FindPropertyRelative("value.value").floatValue = atmosphereFloatProperty.Evaluate(time);
                        break;
                    case VarType.Vector3:
                        AtmosphereProperty<Vector3Value> atmosphereVectorProperty = new AtmosphereProperty<Vector3Value>();
                        atmosphereVectorProperty.constantValue = property.FindPropertyRelative("constantValue").FindPropertyRelative("value").vector3Value;
                        atmosphereVectorProperty.interpolateValue = true;
                        atmosphereVectorProperty.overrideValue = true;

                        List<AtmosphereProperty<Vector3Value>.AtmosphereGradientKey> vectorKeys = new List<AtmosphereProperty<Vector3Value>.AtmosphereGradientKey>();
                        for (int i = 0; i < interpolatedValue.arraySize; i++)
                        {
                            AtmosphereProperty<Vector3Value>.AtmosphereGradientKey vectorKey = new AtmosphereProperty<Vector3Value>.AtmosphereGradientKey();
                            vectorKey.value = keys[i].property.FindPropertyRelative("value.value").vector3Value;
                            vectorKey.time = GetKeyTime(i);

                            if (vectorKey.time > time && targetIndex == -1)
                                targetIndex = i;

                            vectorKeys.Add(vectorKey);
                        }

                        if (targetIndex == -1)
                            targetIndex = interpolatedValue.arraySize;

                        atmosphereVectorProperty.interpolatedValue = vectorKeys.ToArray();

                        //Insert the new value at the correct index
                        interpolatedValue.InsertArrayElementAtIndex(targetIndex);
                        PropertyConversion.SetMeridiemTime(interpolatedValue.GetArrayElementAtIndex(targetIndex).FindPropertyRelative("time"), time);
                        interpolatedValue.GetArrayElementAtIndex(targetIndex).FindPropertyRelative("value.value").vector3Value = atmosphereVectorProperty.Evaluate(time);
                        break;
                }

                trackedTime = time;
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
                selected = targetIndex;
                RegenerateFields();
                action.Invoke();

            });


            void RegenerateFields()
            {
                timelineProperties.Clear();

                if (interpolatedValue.arraySize == 0)
                    return;

                selected = Mathf.Clamp(selected, 0, interpolatedValue.arraySize - 1);

                PropertyField valueField = new PropertyField();
                valueField.BindProperty(interpolatedValue.GetArrayElementAtIndex(selected).FindPropertyRelative("value.value"));
                valueField.RegisterValueChangeCallback(evt =>
                {
                    action.Invoke();
                    RegenerateDots();
                });
                timelineProperties.Add(valueField);

                PropertyField timeField = new PropertyField();
                timeField.BindProperty(interpolatedValue.GetArrayElementAtIndex(selected).FindPropertyRelative("time"));
                timeField.RegisterValueChangeCallback(evt =>
                {
                    action.Invoke();
                    RegenerateDots();
                });
                timelineProperties.Add(timeField);
            }

            void RegenerateDots()
            {

                if (property == null || property.serializedObject == null)
                {
                    Close();
                    return;
                }

                gradient.MarkDirtyRepaint();
                clock.MarkDirtyRepaint();
                timelineDots.Clear();
                clockDots.Clear();
                keys = new List<(VisualElement, SerializedProperty property)>();

                // Clamp selected index
                selected = Mathf.Clamp(selected, 0, interpolatedValue.arraySize - 1);

                for (int i = 0; i < interpolatedValue.arraySize; i++)
                {

                    //TIMELINE

                    VisualElement timelineDot = new VisualElement();
                    timelineDot.AddToClassList("knob");
                    timelineDot.style.left = new Length(PropertyConversion.GetMeridiemTime(interpolatedValue.GetArrayElementAtIndex(i).FindPropertyRelative("time")) * 100, LengthUnit.Percent);

                    if (varType == VarType.Color)
                    {
                        Color color = interpolatedValue.GetArrayElementAtIndex(i).FindPropertyRelative("value.value").colorValue;
                        timelineDot.style.backgroundColor = new Color(color.r, color.g, color.b, 1);
                    }
                    else
                        timelineDot.style.backgroundColor = Color.gray6;

                    if (selected == i)
                    {
                        timelineDot.AddToClassList("selected");
                    }
                    int j = i;

                    timelineDot.RegisterCallback<PointerDownEvent>(evt =>
                    {
                        if (evt.button != 0) return;
                        isDragging = true;
                        dragStartX = evt.position.x;
                        initialTime = PropertyConversion.GetMeridiemTime(interpolatedValue.GetArrayElementAtIndex(j).FindPropertyRelative("time"));
                        selected = j;
                        trackedTime = initialTime;
                        timelineDot.AddToClassList("selected");
                        timelineDot.CapturePointer(evt.pointerId);
                        evt.StopPropagation();
                    });

                    timelineDot.RegisterCallback<PointerMoveEvent>(evt =>
                    {
                        if (!isDragging || selected != j) return;

                        selected = j;
                        float deltaX = evt.position.x - dragStartX;
                        float timelineWidth = timeline.resolvedStyle.width;

                        if (timelineWidth > 0)
                        {
                            float normalizedDelta = deltaX / timelineWidth;
                            trackedTime = Mathf.Clamp01(initialTime + normalizedDelta);
                            // PropertyConversion.SetMeridiemTime(interpolatedValue.GetArrayElementAtIndex(j).FindPropertyRelative("time"), trackedTime);
                            timelineDot.style.left = new Length(trackedTime * 100, LengthUnit.Percent);
                            gradient.MarkDirtyRepaint();

                            property.serializedObject.ApplyModifiedProperties();
                        }

                        // action.Invoke();
                        evt.StopPropagation();
                    });

                    timelineDot.RegisterCallback<PointerUpEvent>(evt =>
                    {
                        if (evt.button != 0) return;
                        isDragging = false;
                        PropertyConversion.SetMeridiemTime(interpolatedValue.GetArrayElementAtIndex(j).FindPropertyRelative("time"), trackedTime);
                        property.serializedObject.ApplyModifiedProperties();
                        timelineDot.ReleasePointer(evt.pointerId);
                        evt.StopPropagation();
                        RegenerateFields();
                    });

                    timelineDot.RegisterCallback<ClickEvent>(evt =>
                    {
                        selected = j;
                        RegenerateFields();
                    });
                    timelineDot.RegisterCallback<ContextClickEvent>(evt =>
                    {
                        GenericMenu menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Duplicate"), false, () =>
                        {
                            interpolatedValue.InsertArrayElementAtIndex(j);
                            PropertyConversion.SetMeridiemTime(interpolatedValue.GetArrayElementAtIndex(j + 1).FindPropertyRelative("time"), PropertyConversion.GetMeridiemTime(interpolatedValue.GetArrayElementAtIndex(j + 1).FindPropertyRelative("time")) + 0.02f);
                            property.serializedObject.ApplyModifiedProperties();
                            property.serializedObject.Update();
                            selected = j + 1;
                            RegenerateFields();
                            action.Invoke();
                        });
                        menu.AddItem(new GUIContent("Delete"), false, () =>
                        {
                            interpolatedValue.DeleteArrayElementAtIndex(j);
                            property.serializedObject.ApplyModifiedProperties();
                            property.serializedObject.Update();
                            selected = Mathf.Clamp(j - 1, 0, interpolatedValue.arraySize - 1);
                            RegenerateFields();
                            action.Invoke();
                        });
                        menu.ShowAsContext();
                    });

                    keys.Add((timelineDot, interpolatedValue.GetArrayElementAtIndex(j)));
                    timelineDots.Add(timelineDot);

                    //CLOCK

                    VisualElement clockDot = new VisualElement();
                    clockDot.AddToClassList("knob");

                    void CalculatePosition()
                    {
                        float time = PropertyConversion.GetMeridiemTime(interpolatedValue.GetArrayElementAtIndex(i).FindPropertyRelative("time"));

                        clockDot.style.left = new Length((Mathf.Sin(Mathf.PI * 2 * (time + 0.5f)) / 2 + 0.5f) * 100, LengthUnit.Percent);
                        clockDot.style.bottom = new Length((Mathf.Cos(Mathf.PI * 2 * (time + 0.5f)) / 2 + 0.5f) * 100, LengthUnit.Percent);
                    }

                    CalculatePosition();

                    if (varType == VarType.Color)
                        clockDot.style.backgroundColor = interpolatedValue.GetArrayElementAtIndex(i).FindPropertyRelative("value.value").colorValue;

                    if (selected == i)
                    {
                        clockDot.AddToClassList("selected");
                    }

                    clockDot.RegisterCallback<ClickEvent>(evt =>
                    {
                        selected = j;
                        RegenerateFields();
                    });
                    clockDot.RegisterCallback<ContextClickEvent>(evt =>
                    {
                        GenericMenu menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Duplicate"), false, () =>
                        {
                            interpolatedValue.InsertArrayElementAtIndex(j);
                            PropertyConversion.SetMeridiemTime(interpolatedValue.GetArrayElementAtIndex(j + 1).FindPropertyRelative("time"), PropertyConversion.GetMeridiemTime(interpolatedValue.GetArrayElementAtIndex(j + 1).FindPropertyRelative("time")) + 0.02f);
                            property.serializedObject.ApplyModifiedProperties();
                            property.serializedObject.Update();
                            selected = j + 1;
                            RegenerateFields();
                            action.Invoke();
                        });
                        menu.AddItem(new GUIContent("Delete"), false, () =>
                        {
                            interpolatedValue.DeleteArrayElementAtIndex(j);
                            property.serializedObject.ApplyModifiedProperties();
                            property.serializedObject.Update();
                            selected = Mathf.Clamp(j - 1, 0, interpolatedValue.arraySize - 1);
                            RegenerateFields();
                            action.Invoke();
                        });
                        menu.ShowAsContext();
                    });
                    clockDots.Add(clockDot);
                }

            }

            void RegenerateGraph()
            {

                gradient.generateVisualContent += context =>
                {


                    float width = gradient.contentRect.width;
                    float height = gradient.contentRect.height;

                    var painter = context.painter2D;


                    float GetKeyTime(int index)
                    {
                        return (float)keys[index].element.style.left.value.value / 100f;
                    }

                    keys.Sort((a, b) => a.element.style.left.value.value.CompareTo(b.element.style.left.value.value));

                    if (varType == VarType.Color)
                    {
                        painter.lineWidth = 30;

                        AtmosphereProperty<ColorValue> colorProperty = new AtmosphereProperty<ColorValue>();
                        colorProperty.constantValue = property.FindPropertyRelative("constantValue").FindPropertyRelative("value").colorValue;
                        colorProperty.interpolateValue = true;
                        colorProperty.overrideValue = true;

                        List<AtmosphereProperty<ColorValue>.AtmosphereGradientKey> colorKeys = new List<AtmosphereProperty<ColorValue>.AtmosphereGradientKey>();
                        for (int i = 0; i < interpolatedValue.arraySize; i++)
                        {
                            AtmosphereProperty<ColorValue>.AtmosphereGradientKey colorKey = new AtmosphereProperty<ColorValue>.AtmosphereGradientKey();
                            colorKey.value = keys[i].property.FindPropertyRelative("value.value").colorValue;
                            colorKey.time = GetKeyTime(i);
                            colorKeys.Add(colorKey);
                        }

                        colorProperty.interpolatedValue = colorKeys.ToArray();

                        void DrawGradientSection(float start, Color startingColor, float end, Color endColor)
                        {
                            Gradient gradient = new Gradient()
                            {
                                alphaKeys = new GradientAlphaKey[2] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) },
                                colorKeys = new GradientColorKey[2] { new GradientColorKey(startingColor, 0), new GradientColorKey(endColor, 1) }
                            };

                            painter.strokeGradient = gradient;
                            painter.BeginPath();
                            painter.MoveTo(new Vector2(width * start, height / 2));
                            painter.LineTo(new Vector2(width * end, height / 2));
                            painter.Stroke();
                        }

                        if (colorKeys.Count > 0)
                        {
                            DrawGradientSection(0, colorProperty.Evaluate(0), GetKeyTime(0), keys[0].property.FindPropertyRelative("value.value").colorValue);
                            DrawGradientSection(GetKeyTime(colorKeys.Count - 1), keys[^1].property.FindPropertyRelative("value.value").colorValue, 1, colorProperty.Evaluate(1));

                            for (int i = 0; i < colorKeys.Count - 1; i++)
                            {
                                DrawGradientSection(GetKeyTime(i), keys[i].property.FindPropertyRelative("value.value").colorValue, GetKeyTime(i + 1), keys[i + 1].property.FindPropertyRelative("value.value").colorValue);
                            }
                        }
                    }
                    if (varType == VarType.Float)
                    {
                        painter.lineWidth = 20;
                        painter.lineCap = LineCap.Round;
                        painter.strokeColor = Color.gray3;
                        painter.BeginPath();
                        painter.MoveTo(new Vector2(5, height * 0.5f));
                        painter.LineTo(new Vector2(width - 5, height * 0.5f));
                        painter.Stroke();

                        painter.strokeColor = Branding.blue;
                        painter.lineWidth = 1;
                        painter.BeginPath();


                        keys.Sort((a, b) => a.element.style.left.value.value.CompareTo(b.element.style.left.value.value));

                        minFloat = Mathf.Min(keys.Select(x => x.property.FindPropertyRelative("value.value").floatValue).ToArray());
                        maxFloat = Mathf.Max(keys.Select(x => x.property.FindPropertyRelative("value.value").floatValue).ToArray());

                        AtmosphereProperty<FloatValue> atmosphereProperty = new AtmosphereProperty<FloatValue>();
                        atmosphereProperty.constantValue = property.FindPropertyRelative("constantValue").FindPropertyRelative("value").floatValue;
                        atmosphereProperty.interpolateValue = true;
                        atmosphereProperty.overrideValue = true;

                        List<AtmosphereProperty<FloatValue>.AtmosphereGradientKey> colorKeys = new List<AtmosphereProperty<FloatValue>.AtmosphereGradientKey>();
                        for (int i = 0; i < interpolatedValue.arraySize; i++)
                        {
                            AtmosphereProperty<FloatValue>.AtmosphereGradientKey floatKey = new AtmosphereProperty<FloatValue>.AtmosphereGradientKey();
                            floatKey.value = keys[i].property.FindPropertyRelative("value.value").floatValue;
                            floatKey.time = GetKeyTime(i);
                            colorKeys.Add(floatKey);
                        }

                        atmosphereProperty.interpolatedValue = colorKeys.ToArray();

                        float Map(float s, float a1, float a2)
                        {
                            if (a1 == a2)
                                return 0.5f;
                            return 0.05f + (s - a1) * (0.9f) / (a2 - a1);
                        }

                        void DrawGradientSection(float start, float startValue, float end, float endValue)
                        {
                            painter.MoveTo(new Vector2(width * start, height * (1 - Map(startValue, minFloat, maxFloat))));
                            painter.LineTo(new Vector2(width * end, height * (1 - Map(endValue, minFloat, maxFloat))));
                            painter.Stroke();
                        }

                        if (colorKeys.Count > 0)
                        {
                            DrawGradientSection(0, atmosphereProperty.Evaluate(0), GetKeyTime(0), keys[0].property.FindPropertyRelative("value.value").floatValue);
                            DrawGradientSection(GetKeyTime(colorKeys.Count - 1), keys[^1].property.FindPropertyRelative("value.value").floatValue, 1, atmosphereProperty.Evaluate(1));

                            for (int i = 0; i < colorKeys.Count - 1; i++)
                            {
                                DrawGradientSection(GetKeyTime(i), keys[i].property.FindPropertyRelative("value.value").floatValue, GetKeyTime(i + 1), keys[i + 1].property.FindPropertyRelative("value.value").floatValue);
                            }
                        }
                    }
                    if (varType == VarType.Vector3)
                    {
                        painter.lineWidth = 20;
                        painter.lineCap = LineCap.Round;
                        painter.strokeColor = Color.gray3;
                        painter.BeginPath();
                        painter.MoveTo(new Vector2(5, height * 0.5f));
                        painter.LineTo(new Vector2(width - 5, height * 0.5f));
                        painter.Stroke();

                        painter.lineWidth = 1;


                        keys.Sort((a, b) => a.element.style.left.value.value.CompareTo(b.element.style.left.value.value));

                        float min = Mathf.Min(Mathf.Min(keys.Select(x => x.property.FindPropertyRelative("value.value").vector3Value.x).ToArray()), Mathf.Min(keys.Select(x => x.property.FindPropertyRelative("value.value").vector3Value.y).ToArray()), Mathf.Min(keys.Select(x => x.property.FindPropertyRelative("value.value").vector3Value.z).ToArray()));
                        float max = Mathf.Max(Mathf.Max(keys.Select(x => x.property.FindPropertyRelative("value.value").vector3Value.x).ToArray()), Mathf.Max(keys.Select(x => x.property.FindPropertyRelative("value.value").vector3Value.y).ToArray()), Mathf.Max(keys.Select(x => x.property.FindPropertyRelative("value.value").vector3Value.z).ToArray()));

                        AtmosphereProperty<Vector3Value> atmosphereProperty = new AtmosphereProperty<Vector3Value>();
                        atmosphereProperty.constantValue = property.FindPropertyRelative("constantValue").FindPropertyRelative("value").vector3Value;
                        atmosphereProperty.interpolateValue = true;
                        atmosphereProperty.overrideValue = true;

                        List<AtmosphereProperty<Vector3Value>.AtmosphereGradientKey> valueKeys = new List<AtmosphereProperty<Vector3Value>.AtmosphereGradientKey>();
                        for (int i = 0; i < interpolatedValue.arraySize; i++)
                        {
                            AtmosphereProperty<Vector3Value>.AtmosphereGradientKey key = new AtmosphereProperty<Vector3Value>.AtmosphereGradientKey();
                            key.value = keys[i].property.FindPropertyRelative("value.value").vector3Value;
                            key.time = GetKeyTime(i);
                            valueKeys.Add(key);
                        }

                        atmosphereProperty.interpolatedValue = valueKeys.ToArray();

                        float Map(float s, float a1, float a2)
                        {
                            if (a1 == a2)
                                return 0.5f;
                            return 0.05f + (s - a1) * (0.9f) / (a2 - a1);
                        }

                        void DrawGradientSection(float start, Vector3 startValue, float end, Vector3 endValue)
                        {
                            painter.strokeColor = Branding.red;
                            painter.BeginPath();
                            painter.MoveTo(new Vector2(width * start, height * (1 - Map(startValue.x, min, max))));
                            painter.LineTo(new Vector2(width * end, height * (1 - Map(endValue.x, min, max))));
                            painter.Stroke();

                            painter.strokeColor = Branding.green;
                            painter.BeginPath();
                            painter.MoveTo(new Vector2(width * start, height * (1 - Map(startValue.y, min, max))));
                            painter.LineTo(new Vector2(width * end, height * (1 - Map(endValue.y, min, max))));
                            painter.Stroke();

                            painter.strokeColor = Branding.blue;
                            painter.BeginPath();
                            painter.MoveTo(new Vector2(width * start, height * (1 - Map(startValue.z, min, max))));
                            painter.LineTo(new Vector2(width * end, height * (1 - Map(endValue.z, min, max))));
                            painter.Stroke();
                        }

                        if (valueKeys.Count > 0)
                        {
                            DrawGradientSection(0, atmosphereProperty.Evaluate(0), GetKeyTime(0), keys[0].property.FindPropertyRelative("value.value").vector3Value);
                            DrawGradientSection(GetKeyTime(valueKeys.Count - 1), keys[^1].property.FindPropertyRelative("value.value").vector3Value, 1, atmosphereProperty.Evaluate(1));

                            for (int i = 0; i < valueKeys.Count - 1; i++)
                            {
                                DrawGradientSection(GetKeyTime(i), keys[i].property.FindPropertyRelative("value.value").vector3Value, GetKeyTime(i + 1), keys[i + 1].property.FindPropertyRelative("value.value").vector3Value);
                            }
                        }

                    }

                };

                clock.generateVisualContent += context =>
                {
                    float width = clock.contentRect.width;
                    float height = clock.contentRect.height;

                    var painter = context.painter2D;

                    if (varType == VarType.Color)
                    {
                        painter.lineWidth = 20;

                        SerializedProperty interpolatedValue = property.FindPropertyRelative("interpolatedValue");
                        AtmosphereProperty<ColorValue> atmosphereProperty = new AtmosphereProperty<ColorValue>();
                        atmosphereProperty.constantValue = property.FindPropertyRelative("constantValue").FindPropertyRelative("value").colorValue;
                        atmosphereProperty.interpolateValue = true;
                        atmosphereProperty.overrideValue = true;

                        List<AtmosphereProperty<ColorValue>.AtmosphereGradientKey> colorKeys = new List<AtmosphereProperty<ColorValue>.AtmosphereGradientKey>();
                        for (int i = 0; i < interpolatedValue.arraySize; i++)
                        {
                            AtmosphereProperty<ColorValue>.AtmosphereGradientKey colorKey = new AtmosphereProperty<ColorValue>.AtmosphereGradientKey();
                            colorKey.value = interpolatedValue.GetArrayElementAtIndex(i).FindPropertyRelative("value.value").colorValue;
                            colorKey.time = PropertyConversion.GetMeridiemTime(interpolatedValue.GetArrayElementAtIndex(i).FindPropertyRelative("time"));
                            colorKeys.Add(colorKey);
                        }

                        atmosphereProperty.interpolatedValue = colorKeys.ToArray();

                        void DrawGradientSection(float start, Color startingColor, float end, Color endColor)
                        {
                            Gradient gradient = new Gradient()
                            {
                                alphaKeys = new GradientAlphaKey[2] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) },
                                colorKeys = new GradientColorKey[2] { new GradientColorKey(startingColor, 0), new GradientColorKey(endColor, 1) }
                            };

                            painter.strokeGradient = gradient;
                            painter.BeginPath();
                            painter.Arc(new Vector2(width * 0.5f, height * 0.5f), width * 0.5f, new Angle(start * 360 + 90, AngleUnit.Degree), new Angle(end * 360 + 90, AngleUnit.Degree));
                            painter.Stroke();
                        }


                        if (colorKeys.Count > 0)
                        {
                            DrawGradientSection(0, atmosphereProperty.Evaluate(0), colorKeys[0].time, colorKeys[0].value);
                            DrawGradientSection(colorKeys[colorKeys.Count - 1].time, colorKeys[colorKeys.Count - 1].value, 1, atmosphereProperty.Evaluate(1));

                            for (int i = 0; i < colorKeys.Count - 1; i++)
                            {
                                DrawGradientSection(colorKeys[i].time, colorKeys[i].value, colorKeys[i + 1].time, colorKeys[i + 1].value);
                            }
                        }
                    }
                    if (varType == VarType.Float)
                    {
                        SerializedProperty interpolatedValue = property.FindPropertyRelative("interpolatedValue");
                        AtmosphereProperty<FloatValue> atmosphereProperty = new AtmosphereProperty<FloatValue>();
                        atmosphereProperty.constantValue = property.FindPropertyRelative("constantValue").FindPropertyRelative("value").floatValue;
                        atmosphereProperty.interpolateValue = true;
                        atmosphereProperty.overrideValue = true;

                        List<AtmosphereProperty<FloatValue>.AtmosphereGradientKey> keys = new List<AtmosphereProperty<FloatValue>.AtmosphereGradientKey>();
                        for (int i = 0; i < interpolatedValue.arraySize; i++)
                        {
                            AtmosphereProperty<FloatValue>.AtmosphereGradientKey colorKey = new AtmosphereProperty<FloatValue>.AtmosphereGradientKey();
                            colorKey.value = interpolatedValue.GetArrayElementAtIndex(i).FindPropertyRelative("value.value").floatValue;
                            colorKey.time = PropertyConversion.GetMeridiemTime(interpolatedValue.GetArrayElementAtIndex(i).FindPropertyRelative("time"));
                            keys.Add(colorKey);
                        }

                        atmosphereProperty.interpolatedValue = keys.ToArray();

                        painter.lineWidth = 20;
                        painter.lineCap = LineCap.Round;
                        painter.strokeColor = Color.gray3;
                        painter.BeginPath();
                        painter.MoveTo(new Vector2(5, height * 0.5f));
                        painter.LineTo(new Vector2(width - 5, height * 0.5f));
                        painter.Stroke();

                        painter.strokeColor = Branding.blue;
                        painter.lineWidth = 1;

                        painter.BeginPath();

                        float Map(float s, float a1, float a2)
                        {
                            return 0.05f + (s - a1) * (0.9f) / (a2 - a1);
                        }

                        void DrawGradientSection(float start, float startValue, float end, float endValue)
                        {
                            painter.MoveTo(new Vector2(width * start, height * (1 - Map(startValue, minFloat, maxFloat))));
                            painter.LineTo(new Vector2(width * end, height * (1 - Map(endValue, minFloat, maxFloat))));
                            painter.Stroke();
                        }

                        if (keys.Count > 0)
                        {
                            DrawGradientSection(0, atmosphereProperty.Evaluate(0), keys[0].time, keys[0].value);
                            DrawGradientSection(keys[keys.Count - 1].time, keys[keys.Count - 1].value, 1, atmosphereProperty.Evaluate(1));

                            for (int i = 0; i < keys.Count - 1; i++)
                            {
                                DrawGradientSection(keys[i].time, keys[i].value, keys[i + 1].time, keys[i + 1].value);
                            }
                        }

                    }
                };
            }

            RegenerateDots();
            RegenerateFields();
            RegenerateGraph();

        }

    }
}