//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;
using UnityEditor.UIElements;
using System;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomPropertyDrawer(typeof(MeridiemTime))]
    public class MeridiemTimeDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement field = new VisualElement();
            field.AddToClassList("unity-base-field");

            Label label = new Label();
            label.text = preferredLabel;
            if (string.IsNullOrEmpty(preferredLabel)) label.style.display = DisplayStyle.None;
            label.AddToClassList("unity-base-field__label");
            label.AddToClassList("unity-base-field__label--with-dragger");
            VisualElement input = new VisualElement();
            input.style.flexDirection = FlexDirection.Row;
            input.style.flexWrap = Wrap.NoWrap;
            input.AddToClassList("unity-base-field__input");

            Refresh();

            void Refresh()
            {
                input.Clear();
                MeridiemTime time;
                RefreshMeridiemTime();

                void RefreshMeridiemTime()
                {
                    time = new MeridiemTime(property.FindPropertyRelative("hours").intValue,
                                            property.FindPropertyRelative("minutes").intValue,
                                            property.FindPropertyRelative("seconds").intValue,
                                            property.FindPropertyRelative("milliseconds").intValue,
                                            (Meridiem)property.FindPropertyRelative("meridiem").enumValueIndex);

                }

                bool militaryTime = CozyPreferences.TimeUnit == MeridiemTime.Unit.Military;

                Slider slider = new Slider(0, 1, SliderDirection.Horizontal);
                slider.showInputField = false;
                slider.SetValueWithoutNotify(time);
                slider.style.flexGrow = 1;
                slider.style.marginRight = 4;
                input.Add(slider);

                string ParseHours()
                {
                    string time = "";

                    if (militaryTime)
                    {
                        time = MeridiemTime.WrapInt(property.FindPropertyRelative("hours").intValue + (property.FindPropertyRelative("hours").intValue % 12 == 0 ? 12 : 0) + property.FindPropertyRelative("meridiem").enumValueIndex * 12, 24, true).ToString("D2");
                    }
                    else
                    {
                        time = MeridiemTime.WrapInt(property.FindPropertyRelative("hours").intValue, 12, false).ToString();
                    }

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
                milliseconds.style.width = 25;
                milliseconds.value = ParseMilliseconds();

                if (CozyPreferences.ShowSeconds)
                    input.Add(seconds);

                if (CozyPreferences.ShowMilliseconds)
                    input.Add(milliseconds);


                EnumField meridiem = new EnumField();
                meridiem.BindProperty(property.FindPropertyRelative("meridiem"));
                if (CozyPreferences.TimeUnit == MeridiemTime.Unit.Standard)
                    input.Add(meridiem);


                void SetValueFromInputFields()
                {
                    if (hours.value == "")
                        hours.value = "0";
                    if (minutes.value == "")
                        minutes.value = "0";
                    if (seconds.value == "")
                        seconds.value = "0";
                    if (milliseconds.value == "")
                        milliseconds.value = "0";

                    MeridiemTime time = MeridiemTime.From24Hour(Math.Clamp(int.Parse(hours.value), 0, 23), Math.Clamp(int.Parse(minutes.value), 0, 59), Math.Clamp(int.Parse(seconds.value), 0, 59), Math.Clamp(int.Parse(milliseconds.value), 0, 999));

                    if (!militaryTime)
                        time = new MeridiemTime(Math.Clamp(int.Parse(hours.value), 0, 23), Math.Clamp(int.Parse(minutes.value), 0, 59), Math.Clamp(int.Parse(seconds.value), 0, 59), Math.Clamp(int.Parse(milliseconds.value), 0, 999), (Meridiem)meridiem.value);

                    property.FindPropertyRelative("hours").intValue = time.hours;
                    property.FindPropertyRelative("minutes").intValue = time.minutes;
                    property.FindPropertyRelative("seconds").intValue = time.seconds;
                    property.FindPropertyRelative("milliseconds").intValue = time.milliseconds;
                    property.FindPropertyRelative("meridiem").enumValueIndex = (int)time.meridiem;
                    property.serializedObject.ApplyModifiedProperties();
                    RefreshMeridiemTime();
                    hours.value = ParseHours();
                    minutes.value = ParseMinutes();
                    seconds.value = ParseSeconds();
                    milliseconds.value = ParseMilliseconds();
                    meridiem.value = time.meridiem;
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
                meridiem.RegisterValueChangedCallback(evt =>
                {
                    SetValueFromInputFields();
                });

                slider.RegisterValueChangedCallback(evt =>
                {
                    time = MeridiemTime.ToClamped(evt.newValue);
                    slider.SetValueWithoutNotify(time);
                    property.FindPropertyRelative("hours").intValue = time.hours;
                    property.FindPropertyRelative("minutes").intValue = time.minutes;
                    property.FindPropertyRelative("seconds").intValue = time.seconds;
                    property.FindPropertyRelative("milliseconds").intValue = time.milliseconds;
                    property.FindPropertyRelative("meridiem").enumValueIndex = (int)time.meridiem;
                    property.serializedObject.ApplyModifiedProperties();
                    property.serializedObject.Update();
                    hours.SetValueWithoutNotify(ParseHours());
                    minutes.SetValueWithoutNotify(ParseMinutes());
                    seconds.SetValueWithoutNotify(ParseSeconds());
                    milliseconds.SetValueWithoutNotify(ParseMilliseconds());
                    meridiem.SetValueWithoutNotify(time.meridiem);
                });



                // --- Label Drag Handling ---
                float accumulatorX = 0f;
                bool dragging = false;

                label.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 0)
                    {
                        dragging = true;
                        accumulatorX = 0f;
                        label.CapturePointer(0);
                        evt.StopPropagation();
                        EditorGUIUtility.SetWantsMouseJumping(1);
                    }
                });

                label.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (dragging && label.HasMouseCapture())
                    {
                        accumulatorX += evt.deltaPosition.x / ((evt.modifiers & EventModifiers.Shift) != 0 ? 50f : 400f);

                        if (Mathf.Abs(accumulatorX) >= 0)
                        {
                            float timeOffset = accumulatorX;

                            if (timeOffset != 0)
                            {
                                time = MeridiemTime.ToClamped((float)time + timeOffset);

                                slider.SetValueWithoutNotify(time);
                                property.FindPropertyRelative("hours").intValue = time.hours;
                                property.FindPropertyRelative("minutes").intValue = time.minutes;
                                property.FindPropertyRelative("seconds").intValue = time.seconds;
                                property.FindPropertyRelative("milliseconds").intValue = time.milliseconds;
                                property.FindPropertyRelative("meridiem").enumValueIndex = (int)time.meridiem;
                                property.serializedObject.ApplyModifiedProperties();
                                property.serializedObject.Update();
                                hours.SetValueWithoutNotify(ParseHours());
                                minutes.SetValueWithoutNotify(ParseMinutes());
                                seconds.SetValueWithoutNotify(ParseSeconds());
                                milliseconds.SetValueWithoutNotify(ParseMilliseconds());
                                meridiem.SetValueWithoutNotify(time.meridiem);

                                accumulatorX -= timeOffset;

                                evt.StopPropagation();
                            }
                        }
                    }
                });

                label.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (dragging && evt.button == 0)
                    {
                        dragging = false;
                        label.ReleaseMouse();
                        evt.StopPropagation();
                        EditorGUIUtility.SetWantsMouseJumping(0);
                    }
                });
            }


            field.Add(label);
            field.Add(input);

            return field;
        }

    }
}