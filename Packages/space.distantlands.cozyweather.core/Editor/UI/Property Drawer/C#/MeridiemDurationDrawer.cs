//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;
using System;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomPropertyDrawer(typeof(MeridiemTimeDelta))]
    public class MeridiemDurationDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement field = new VisualElement();
            field.AddToClassList("unity-base-field");

            Label label = new Label();
            label.text = preferredLabel;
            if (string.IsNullOrEmpty(preferredLabel)) label.style.display = DisplayStyle.None;
            label.AddToClassList("unity-base-field__label");
            VisualElement input = new VisualElement();
            input.style.flexDirection = FlexDirection.Row;
            input.style.flexWrap = Wrap.NoWrap;
            input.AddToClassList("unity-base-field__input");

            Refresh();

            void Refresh()
            {
                input.Clear();
                MeridiemTimeDelta time;
                RefreshMeridiemTime();

                void RefreshMeridiemTime()
                {
                    time = new MeridiemTimeDelta(property.FindPropertyRelative("hours").intValue,
                                            property.FindPropertyRelative("minutes").intValue,
                                            property.FindPropertyRelative("seconds").intValue,
                                            property.FindPropertyRelative("milliseconds").intValue);
                }

                float sliderMin = 0f;
                float sliderMax = 0f;

                if (fieldInfo != null)
                {
                    object[] attributes = fieldInfo.GetCustomAttributes(typeof(MeridiemRangeAttribute), true);
                    if (attributes != null && attributes.Length > 0)
                    {
                        MeridiemRangeAttribute range = attributes[0] as MeridiemRangeAttribute;
                        if (range != null)
                        {
                            sliderMin = range.min;
                            sliderMax = range.max;
                        }
                    }
                }

                Slider slider = new Slider(sliderMin / 24f, sliderMax / 24f, SliderDirection.Horizontal);
                slider.showInputField = false;
                slider.SetValueWithoutNotify(time);
                slider.style.flexGrow = 1;
                slider.style.marginRight = 4;
                if (sliderMin != sliderMax)
                    input.Add(slider);

                string ParseHours()
                {
                    string time = "";

                    time = property.FindPropertyRelative("hours").intValue.ToString();

                    return time;
                }
                string ParseMinutes()
                {
                    return MeridiemTime.WrapInt(property.FindPropertyRelative("minutes").intValue, 60, true).ToString("D2");
                }
                string ParseSeconds()
                {
                    return MeridiemTime.WrapInt(property.FindPropertyRelative("seconds").intValue, 60, true).ToString("D2");
                }
                string ParseMilliseconds()
                {
                    return MeridiemTime.WrapInt(property.FindPropertyRelative("milliseconds").intValue, 1000, true).ToString("D3");
                }

                TextField hours = new TextField();
                hours.maxLength = 2;
                hours.style.width = 25;
                hours.value = ParseHours();
                input.Add(hours);

                TextField minutes = new TextField();
                minutes.maxLength = 2;
                minutes.style.width = 25;
                minutes.value = ParseMinutes();
                input.Add(minutes);

                TextField seconds = new TextField();
                seconds.maxLength = 2;
                seconds.style.width = 25;
                seconds.value = ParseSeconds();

                TextField milliseconds = new TextField();
                milliseconds.maxLength = 3;
                milliseconds.style.width = 35;
                milliseconds.value = ParseMilliseconds();

                if (CozyPreferences.ShowSeconds)
                    input.Add(seconds);

                if (CozyPreferences.ShowMilliseconds)
                    input.Add(milliseconds);
                void SetValueFromInputFields()
                {
                    if (string.IsNullOrEmpty(hours.value)) hours.value = "0";
                    if (string.IsNullOrEmpty(minutes.value)) minutes.value = "0";
                    if (string.IsNullOrEmpty(seconds.value)) seconds.value = "0";
                    if (string.IsNullOrEmpty(milliseconds.value)) milliseconds.value = "0";

                    int parsedHours = int.Parse(hours.value);

                    float min = sliderMin != sliderMax ? sliderMin : 0;
                    float max = sliderMin != sliderMax ? sliderMax : int.MaxValue;

                    MeridiemTimeDelta time = new MeridiemTimeDelta(
                        (int)Mathf.Clamp(parsedHours, min, max),
                        Math.Clamp(int.Parse(minutes.value), 0, 59),
                        Math.Clamp(int.Parse(seconds.value), 0, 59),
                        Math.Clamp(int.Parse(milliseconds.value), 0, 999)
                    );

                    property.FindPropertyRelative("hours").intValue = time.hours;
                    property.FindPropertyRelative("minutes").intValue = time.minutes;
                    property.FindPropertyRelative("seconds").intValue = time.seconds;
                    property.FindPropertyRelative("milliseconds").intValue = time.milliseconds;

                    property.serializedObject.ApplyModifiedProperties();
                    RefreshMeridiemTime();

                    hours.SetValueWithoutNotify(ParseHours());
                    minutes.SetValueWithoutNotify(ParseMinutes());
                    seconds.SetValueWithoutNotify(ParseSeconds());
                    milliseconds.SetValueWithoutNotify(ParseMilliseconds());
                    slider.SetValueWithoutNotify(time);
                }

                hours.RegisterCallback<BlurEvent>(evt =>
                {
                    SetValueFromInputFields();
                });
                minutes.RegisterCallback<BlurEvent>(evt =>
                {
                    SetValueFromInputFields();
                });
                seconds.RegisterCallback<BlurEvent>(evt =>
                {
                    SetValueFromInputFields();
                });
                milliseconds.RegisterCallback<BlurEvent>(evt =>
                {
                    SetValueFromInputFields();
                });
                slider.RegisterValueChangedCallback(evt =>
                {
                    time = evt.newValue;
                    slider.SetValueWithoutNotify(time);
                    property.FindPropertyRelative("hours").intValue = time.hours;
                    property.FindPropertyRelative("minutes").intValue = time.minutes;
                    property.FindPropertyRelative("seconds").intValue = time.seconds;
                    property.FindPropertyRelative("milliseconds").intValue = time.milliseconds;
                    property.serializedObject.ApplyModifiedProperties();
                    property.serializedObject.Update();
                    hours.SetValueWithoutNotify(ParseHours());
                    minutes.SetValueWithoutNotify(ParseMinutes());
                    seconds.SetValueWithoutNotify(ParseSeconds());
                    milliseconds.SetValueWithoutNotify(ParseMilliseconds());
                });
            }


            field.Add(label);
            field.Add(input);

            return field;
        }

    }

}