//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;
using UnityEditor.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomPropertyDrawer(typeof(AtmospherePropertyAttribute))]
    public class AtmospherePropertyDrawer : PropertyDrawer
    {
        VisualElement root;
        private void OnEnable()
        {
            Undo.undoRedoPerformed += HandleUndo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndo;
        }

        void HandleUndo()
        {
            if (root != null)
                root.MarkDirtyRepaint();
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var attr = (AtmospherePropertyAttribute)attribute;

            root = new VisualElement();
            root.AddToClassList("atmosphere-property");

            VisualElement labelToggle = new VisualElement();
            labelToggle.AddToClassList("label-toggle");


            VisualElement propertyContainer = new VisualElement();
            propertyContainer.AddToClassList("field");
            propertyContainer.AddToClassList("multi-field");
            propertyContainer.style.paddingRight = 4;


            Toggle overrideValue = new Toggle();
            Label label = new Label();
            label.AddToClassList("unity-base-field__label--with-dragger");
            label.text = SplitCamelCase(attr.fieldTitle);
            VisualElement graph = new VisualElement();
            graph.AddToClassList("field");
            graph.AddToClassList("rounded-field");
            graph.RegisterCallback<ContextClickEvent>(evt =>
            {
                GenericMenu menu = new GenericMenu();

                string copyBuffer = EditorGUIUtility.systemCopyBuffer;
                bool isGradient = attr.varType == VarType.Color && copyBuffer.StartsWith("UnityEditor.GradientWrapperJSON");
                bool isCurve = attr.varType == VarType.Float && copyBuffer.StartsWith("UnityEditor.AnimationCurveWrapperJSON");
                bool isColor = attr.varType == VarType.Color && (copyBuffer.StartsWith("#") || copyBuffer.StartsWith("Color("));
                bool isFloat = attr.varType == VarType.Float;
                bool isAtmosphereProperty = copyBuffer.StartsWith($"DistantLands.Cozy.AtmosphereProperty<{attr.varType}>:");

                bool canPaste = isGradient || isAtmosphereProperty || isColor || isFloat || isCurve;

                menu.AddItem(new GUIContent("Copy"), false, () =>
                {
                    var value = property.boxedValue;
                    string serialized = $"DistantLands.Cozy.AtmosphereProperty<{attr.varType}>:{JsonUtility.ToJson(value)}";

                    EditorGUIUtility.systemCopyBuffer = serialized;
                });
                menu.AddItem(new GUIContent("Copy C# Default"), false, () =>
                {
                    var value = property.boxedValue;
                    StringBuilder builder = new StringBuilder();
                    builder.Append("new DefaultValue(");

                    switch (attr.varType)
                    {
                        case VarType.Color:
                            var colorValue = (AtmosphereProperty<ColorValue>)value;
                            builder.Append(colorValue.interpolateValue.ToString().ToLower());
                            Color constantColor = colorValue.constantValue;
                            builder.Append($", \"new Color({constantColor.r}f, {constantColor.g}f, {constantColor.b}f)\"");

                            foreach (var key in colorValue.interpolatedValue)
                            {
                                Color inter = key.value;
                                builder.Append($", ({key.time.AsPercentage()}f, \"new Color({inter.r}f, {inter.g}f, {inter.b}f)\")");
                            }
                            break;
                        case VarType.Float:
                            var floatValue = (AtmosphereProperty<FloatValue>)value;
                            builder.Append(floatValue.interpolateValue.ToString().ToLower());
                            float constantFloat = floatValue.constantValue;
                            builder.Append($", \"{constantFloat}f\"");

                            foreach (var key in floatValue.interpolatedValue)
                            {
                                float inter = key.value;
                                builder.Append($", ({key.time.AsPercentage()}f, \"{inter}f\")");
                            }
                            break;
                    }

                    builder.Append(")");

                    EditorGUIUtility.systemCopyBuffer = builder.ToString();
                });
                if (canPaste)
                {
                    menu.AddItem(new GUIContent("Paste"), false, () =>
                    {
                        if (isAtmosphereProperty)
                        {
                            string serialized = EditorGUIUtility.systemCopyBuffer;
                            string jsonObject = serialized[(serialized.Split(':')[0].Length + 1)..];

                            switch (attr.varType)
                            {
                                case VarType.Color:
                                    try
                                    {
                                        AtmosphereProperty<ColorValue> colorVal = JsonUtility.FromJson<AtmosphereProperty<ColorValue>>(jsonObject);

                                        property.FindPropertyRelative("constantValue.value").colorValue = colorVal.constantValue;
                                        property.FindPropertyRelative("interpolateValue").boolValue = colorVal.interpolateValue;
                                        property.FindPropertyRelative("overrideValue").boolValue = true;

                                        SerializedProperty interpolatedArray = property.FindPropertyRelative("interpolatedValue");
                                        interpolatedArray.ClearArray();

                                        foreach (AtmosphereProperty<ColorValue>.AtmosphereGradientKey key in colorVal.interpolatedValue)
                                        {
                                            int index = interpolatedArray.arraySize;
                                            interpolatedArray.InsertArrayElementAtIndex(index);
                                            interpolatedArray.GetArrayElementAtIndex(index).FindPropertyRelative("value.value").colorValue = key.value.value;
                                            PropertyConversion.SetMeridiemTime(interpolatedArray.GetArrayElementAtIndex(index).FindPropertyRelative("time"), key.time);
                                        }

                                    }
                                    catch
                                    {
                                        Debug.LogError("Paste failed. Please try copying the data again!");
                                    }

                                    break;
                                case VarType.Float:
                                    try
                                    {
                                        AtmosphereProperty<FloatValue> floatVal = JsonUtility.FromJson<AtmosphereProperty<FloatValue>>(jsonObject);

                                        property.FindPropertyRelative("constantValue.value").floatValue = floatVal.constantValue;
                                        property.FindPropertyRelative("interpolateValue").boolValue = floatVal.interpolateValue;
                                        property.FindPropertyRelative("overrideValue").boolValue = true;

                                        SerializedProperty interpolatedArray = property.FindPropertyRelative("interpolatedValue");
                                        interpolatedArray.ClearArray();

                                        foreach (AtmosphereProperty<FloatValue>.AtmosphereGradientKey key in floatVal.interpolatedValue)
                                        {
                                            int index = interpolatedArray.arraySize;
                                            interpolatedArray.InsertArrayElementAtIndex(index);
                                            interpolatedArray.GetArrayElementAtIndex(index).FindPropertyRelative("value.value").floatValue = key.value.value;
                                            PropertyConversion.SetMeridiemTime(interpolatedArray.GetArrayElementAtIndex(index).FindPropertyRelative("time"), key.time);
                                        }

                                    }
                                    catch
                                    {
                                        Debug.LogError("Paste failed. Please try copying the data again!");
                                    }

                                    break;
                            }


                            property.serializedObject.ApplyModifiedProperties();
                            property.serializedObject.Update();

                            graph.MarkDirtyRepaint();
                            Refresh();
                            return;
                        }
                        if (isGradient)
                        {
                            Gradient gradient = GradientJSONConverter.Parse(copyBuffer);
                            SerializedProperty interpolatedArray = property.FindPropertyRelative("interpolatedValue");
                            interpolatedArray.ClearArray();

                            List<GradientColorKey> colorKeys = gradient.colorKeys.ToList();
                            colorKeys.Sort((a, b) => b.time.CompareTo(a.time));

                            foreach (GradientColorKey colorKey in colorKeys)
                            {
                                int index = interpolatedArray.arraySize;
                                interpolatedArray.InsertArrayElementAtIndex(index);
                                interpolatedArray.GetArrayElementAtIndex(index).FindPropertyRelative("value.value").colorValue = colorKey.color;
                                PropertyConversion.SetMeridiemTime(interpolatedArray.GetArrayElementAtIndex(index).FindPropertyRelative("time"), MeridiemTime.ToClamped(colorKey.time));
                            }

                            property.FindPropertyRelative("interpolateValue").boolValue = true;

                            property.serializedObject.ApplyModifiedProperties();
                            property.serializedObject.Update();

                            graph.MarkDirtyRepaint();
                            Refresh();
                            return;
                        }
                        if (isColor)
                        {
                            if (copyBuffer.StartsWith("#") && ColorUtility.TryParseHtmlString(copyBuffer, out Color newColor))
                            {
                                property.FindPropertyRelative("interpolateValue").boolValue = false;
                                property.FindPropertyRelative("constantValue.value").colorValue = newColor;

                                property.serializedObject.ApplyModifiedProperties();
                                property.serializedObject.Update();

                                graph.MarkDirtyRepaint();
                            }
                            else
                            {
                                Match match = Regex.Match(copyBuffer, @"Color\(([\d\.\-eE]+),([\d\.\-eE]+),([\d\.\-eE]+),([\d\.\-eE]+)\)");

                                if (match.Success)
                                {
                                    float r = float.Parse(match.Groups[1].Value);
                                    float g = float.Parse(match.Groups[2].Value);
                                    float b = float.Parse(match.Groups[3].Value);
                                    float a = float.Parse(match.Groups[4].Value);
                                    property.FindPropertyRelative("interpolateValue").boolValue = false;
                                    property.FindPropertyRelative("constantValue.value").colorValue = new Color(r, g, b, a);

                                    property.serializedObject.ApplyModifiedProperties();
                                    property.serializedObject.Update();

                                    graph.MarkDirtyRepaint();
                                }
                            }

                            Refresh();
                            return;
                        }
                        if (isFloat)
                        {
                            if (float.TryParse(copyBuffer, out float result))
                            {

                                property.FindPropertyRelative("interpolateValue").boolValue = false;
                                property.FindPropertyRelative("constantValue.value").floatValue = result;

                                property.serializedObject.ApplyModifiedProperties();
                                property.serializedObject.Update();

                                graph.MarkDirtyRepaint();
                            }

                            Refresh();
                            return;
                        }
                    });
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Paste"));
                }

                menu.ShowAsContext();
            });




            if (attr.varType == VarType.Float && attr.minFloat < attr.maxFloat)
            {
                Slider propertyField = new Slider();
                propertyField.showInputField = true;
                propertyField.lowValue = attr.minFloat;
                propertyField.highValue = attr.maxFloat;
                propertyField.BindProperty(property.FindPropertyRelative("constantValue.value"));
                propertyField.label = "";
                propertyContainer.Add(propertyField);
            }
            else
            {
                PropertyField propertyField = new PropertyField();
                propertyField.BindProperty(property.FindPropertyRelative("constantValue.value"));
                propertyField.label = "";
                propertyContainer.Add(propertyField);
            }

            if (attr.varType == VarType.Float)
            {
                float startX = 0;
                float startValue = 0;
                bool dragging = false;

                label.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button == 0) // left click
                    {
                        dragging = true;
                        startX = evt.mousePosition.x;
                        startValue = (float)property.FindPropertyRelative("constantValue.value").floatValue;
                        label.CaptureMouse();
                        evt.StopPropagation();
                    }
                });

                label.RegisterCallback<MouseMoveEvent>(evt =>
                {
                    if (dragging && label.HasMouseCapture())
                    {
                        float delta = evt.mousePosition.x - startX;
                        float sensitivity = 0.02f;

                        float newVal = startValue + delta * sensitivity;

                        if (attr.minFloat < attr.maxFloat)
                            newVal = Mathf.Clamp(newVal, attr.minFloat, attr.maxFloat);

                        property.FindPropertyRelative("constantValue.value").floatValue = newVal;
                        property.serializedObject.ApplyModifiedProperties();

                        evt.StopPropagation();
                    }
                });

                label.RegisterCallback<MouseUpEvent>(evt =>
                {
                    if (dragging && evt.button == 0)
                    {
                        dragging = false;
                        label.ReleaseMouse();
                        evt.StopPropagation();
                    }
                });

            }

            Button moreOptions = new Button();
            moreOptions.AddToClassList("inline-btn");
            moreOptions.iconImage = Resources.Load("COZY Icons/Curves") as Texture2D;
            moreOptions.style.width = 25;
            moreOptions.style.paddingBottom = 1;
            moreOptions.style.paddingLeft = 1;
            moreOptions.style.paddingRight = 1;
            moreOptions.style.paddingTop = 1;
            moreOptions.style.display = attr.varType == VarType.Enum ? DisplayStyle.None : DisplayStyle.Flex;
            moreOptions.RegisterCallback<ClickEvent>(evt =>
            {
                property.FindPropertyRelative("interpolateValue").boolValue = !property.FindPropertyRelative("interpolateValue").boolValue;
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
                Refresh();
            });

            graph.RegisterCallback<ClickEvent>(evt =>
            {
                CozyAtmospherePropertyEditor.ShowPropertyEditor(property, attr.varType, Refresh);
            });

            overrideValue.BindProperty(property.FindPropertyRelative("overrideValue"));
            overrideValue.RegisterValueChangedCallback(evt =>
            {
                label.SetEnabled(evt.newValue);
                graph.SetEnabled(evt.newValue);
                propertyContainer.SetEnabled(evt.newValue);
                moreOptions.SetEnabled(evt.newValue);
            });

            labelToggle.Add(overrideValue);
            labelToggle.Add(label);

            // PropertyField constantValue = new PropertyField();
            // constantValue.BindProperty(property.FindPropertyRelative("constantValue"));
            // field.Add(constantValue);

            Undo.undoRedoPerformed += HandleUndo;

            graph.generateVisualContent += context =>
            {
                float width = graph.contentRect.width;
                float height = graph.contentRect.height;

                var painter = context.painter2D;

                // Helper for extracting gradient keys
                TKey[] ExtractGradientKeys<TKey>(SerializedProperty interpolatedValue, System.Func<SerializedProperty, TKey> keySelector)
                {
                    var keys = new List<TKey>(interpolatedValue.arraySize);
                    for (int i = 0; i < interpolatedValue.arraySize; i++)
                    {
                        keys.Add(keySelector(interpolatedValue.GetArrayElementAtIndex(i)));
                    }
                    return keys.ToArray();
                }

                // Color gradient drawing
                void DrawColorGradient(AtmosphereProperty<ColorValue> atmosphereProperty, AtmosphereProperty<ColorValue>.AtmosphereGradientKey[] colorKeys)
                {
                    painter.lineWidth = 20;
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
                    if (colorKeys.Length > 0)
                    {
                        DrawGradientSection(0, atmosphereProperty.Evaluate(0), colorKeys[0].time, colorKeys[0].value);
                        DrawGradientSection(colorKeys[colorKeys.Length - 1].time, colorKeys[colorKeys.Length - 1].value, 1.05f, atmosphereProperty.Evaluate(1));
                        for (int i = 0; i < colorKeys.Length - 1; i++)
                        {
                            DrawGradientSection(colorKeys[i].time, colorKeys[i].value, colorKeys[i + 1].time, colorKeys[i + 1].value);
                        }
                    }
                }

                // Float gradient drawing
                void DrawFloatGradient(AtmosphereProperty<FloatValue> atmosphereProperty, AtmosphereProperty<FloatValue>.AtmosphereGradientKey[] keys)
                {
                    painter.lineWidth = 20;
                    painter.lineCap = LineCap.Round;
                    painter.strokeColor = Color.gray3;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(5, height * 0.5f));
                    painter.LineTo(new Vector2(width - 5, height * 0.5f));
                    painter.Stroke();


                    float min = attr.minFloat;
                    float max = attr.maxFloat;

                    if (min == max && keys.Length > 0)
                    {
                        min = keys.Min(x => x.value.value);
                        max = keys.Max(x => x.value.value);
                    }

                    painter.strokeColor = Branding.blue;
                    painter.lineWidth = 1;
                    painter.BeginPath();

                    float Map(float s, float a1, float a2)
                    {
                        if (a1 == a2)
                            return 0.5f;
                        return 0.05f + (s - a1) * (0.82f) / (a2 - a1);
                    }
                    void DrawGradientSection(float start, float startValue, float end, float endValue)
                    {

                        painter.MoveTo(new Vector2(width * start, height * (1 - Map(startValue, min, max))));
                        painter.LineTo(new Vector2(width * end, height * (1 - Map(endValue, min, max))));
                        painter.Stroke();
                    }
                    if (keys.Length > 0)
                    {
                        DrawGradientSection(0, atmosphereProperty.Evaluate(0), keys[0].time, keys[0].value);
                        DrawGradientSection(keys[keys.Length - 1].time, keys[keys.Length - 1].value, 1, atmosphereProperty.Evaluate(1));
                        for (int i = 0; i < keys.Length - 1; i++)
                        {
                            DrawGradientSection(keys[i].time, keys[i].value, keys[i + 1].time, keys[i + 1].value);
                        }
                    }
                }

                // Switch for varType
                switch (attr.varType)
                {
                    case VarType.Color:
                        {
                            var interpolatedValue = property.FindPropertyRelative("interpolatedValue");
                            var atmosphereProperty = new AtmosphereProperty<ColorValue>();
                            atmosphereProperty.constantValue = property.FindPropertyRelative("constantValue").FindPropertyRelative("value").colorValue;
                            atmosphereProperty.interpolateValue = property.FindPropertyRelative("interpolateValue").boolValue;
                            atmosphereProperty.overrideValue = property.FindPropertyRelative("overrideValue").boolValue;
                            var colorKeys = ExtractGradientKeys(interpolatedValue, sp =>
                            {
                                var key = new AtmosphereProperty<ColorValue>.AtmosphereGradientKey();
                                key.value = sp.FindPropertyRelative("value.value").colorValue;
                                key.time = PropertyConversion.GetMeridiemTime(sp.FindPropertyRelative("time"));
                                return key;
                            });
                            atmosphereProperty.interpolatedValue = colorKeys;
                            DrawColorGradient(atmosphereProperty, colorKeys);
                        }
                        break;
                    case VarType.Float:
                        {
                            var interpolatedValue = property.FindPropertyRelative("interpolatedValue");
                            var atmosphereProperty = new AtmosphereProperty<FloatValue>();
                            atmosphereProperty.constantValue = property.FindPropertyRelative("constantValue").FindPropertyRelative("value").floatValue;
                            atmosphereProperty.interpolateValue = property.FindPropertyRelative("interpolateValue").boolValue;
                            atmosphereProperty.overrideValue = property.FindPropertyRelative("overrideValue").boolValue;
                            var keys = ExtractGradientKeys(interpolatedValue, sp =>
                            {
                                var key = new AtmosphereProperty<FloatValue>.AtmosphereGradientKey();
                                key.value = sp.FindPropertyRelative("value.value").floatValue;
                                key.time = PropertyConversion.GetMeridiemTime(sp.FindPropertyRelative("time"));
                                return key;
                            });
                            atmosphereProperty.interpolatedValue = keys;
                            DrawFloatGradient(atmosphereProperty, keys);
                        }
                        break;
                }
            };

            root.Add(labelToggle);
            root.Add(graph);
            root.Add(propertyContainer);

            string propertyType = property.FindPropertyRelative("constantValue").type;
            if (propertyType == typeof(FloatValue).Name || propertyType == typeof(ColorValue).Name || propertyType == typeof(BoolValue).Name)
            {
                root.Add(moreOptions);
            }
            else
            {
                property.FindPropertyRelative("interpolateValue").boolValue = false;
            }

            Refresh();

            void Refresh()
            {
                if (graph == null)
                    return;

                graph.MarkDirtyRepaint();

                if (property != null && property.serializedObject != null && property.serializedObject.targetObject != null)
                {
                    graph.style.display = property.FindPropertyRelative("interpolateValue").boolValue ? DisplayStyle.Flex : DisplayStyle.None;
                    propertyContainer.style.display = property.FindPropertyRelative("interpolateValue").boolValue ? DisplayStyle.None : DisplayStyle.Flex;
                }
            }


            return root;
        }

        public string SplitCamelCase(string str)
        {
            return Regex.Replace(
                Regex.Replace(
                    str,
                    @"(\P{Ll})(\P{Ll}\p{Ll})",
                    "$1 $2"
                ),
                @"(\p{Ll})(\P{Ll})",
                "$1 $2"
            );
        }

    }


    [CustomPropertyDrawer(typeof(BoolValue))]
    [CustomPropertyDrawer(typeof(CubemapValue))]
    [CustomPropertyDrawer(typeof(Texture3DValue))]
    [CustomPropertyDrawer(typeof(RenderTextureValue))]
    [CustomPropertyDrawer(typeof(Texture2DValue))]
    [CustomPropertyDrawer(typeof(Vector3Value))]
    [CustomPropertyDrawer(typeof(Vector4Value))]
    [CustomPropertyDrawer(typeof(FloatValue))]
    public class StaticAtmospherePropertyDrawer : PropertyDrawer
    {

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            PropertyField propertyField = new PropertyField();
            propertyField.BindProperty(property.FindPropertyRelative("value"));
            propertyField.label = SplitCamelCase(preferredLabel);
            return propertyField;
        }
        public string SplitCamelCase(string str)
        {
            return Regex.Replace(
                Regex.Replace(
                    str,
                    @"(\P{Ll})(\P{Ll}\p{Ll})",
                    "$1 $2"
                ),
                @"(\p{Ll})(\P{Ll})",
                "$1 $2"
            );
        }

    }


}