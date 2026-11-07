//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomPropertyDrawer(typeof(MeridiemDate))]
    public class MeridiemDateDrawer : PropertyDrawer
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

            field.Add(label);
            field.Add(input);

            Refresh();

            void Refresh()
            {
                input.Clear();
                MeridiemDate date = new MeridiemDate(
                    property.FindPropertyRelative("month").intValue,
                    property.FindPropertyRelative("day").intValue,
                    property.FindPropertyRelative("year").intValue
                );

                bool militaryTime = CozyPreferences.TimeUnit == MeridiemTime.Unit.Military;

                string ParseYear() => property.FindPropertyRelative("year").intValue.ToString("D4");

                DropdownField month = new DropdownField
                {
                    name = "month",
                    style = { flexGrow = 1 },
                    choices = CozyPreferences.Months.Select(x => x.name).ToList(),
                    index = property.FindPropertyRelative("month").intValue - 1
                };

                DropdownField day = new DropdownField
                {
                    name = "day",
                    style = { flexGrow = 1 },
                    choices = Enumerable.Range(1, date.DaysInMonth).Select(i => i.ToString()).ToList(),
                    index = property.FindPropertyRelative("day").intValue - 1
                };

                TextField year = new TextField
                {
                    name = "year",
                    style = { flexGrow = 1 },
                    value = ParseYear()
                };

                switch (CozyPreferences.NumericDateFormat)
                {
                    case CozyPreferences.DateFormat.YMD:
                        input.Add(year);
                        input.Add(month);
                        input.Add(day);
                        break;
                    case CozyPreferences.DateFormat.DMY:
                        input.Add(day);
                        input.Add(month);
                        input.Add(year);
                        break;
                    case CozyPreferences.DateFormat.MDY:
                    default:
                        input.Add(month);
                        input.Add(day);
                        input.Add(year);
                        break;
                }

                void SetValueFromInputFields()
                {
                    int.TryParse(year.value, out int yearVal);
                    MeridiemDate newDate = new MeridiemDate(month.index + 1, day.index + 1, yearVal).ToBalanced();

                    property.FindPropertyRelative("month").intValue = newDate.month;
                    property.FindPropertyRelative("day").intValue = newDate.day;
                    property.FindPropertyRelative("year").intValue = newDate.year;
                    property.serializedObject.ApplyModifiedProperties();
                    property.serializedObject.Update();

                    // Refresh dropdowns
                    month.index = newDate.month - 1;
                    day.index = newDate.day - 1;
                    year.value = ParseYear();

                    day.choices = Enumerable.Range(1, newDate.DaysInMonth).Select(i => i.ToString()).ToList();
                }

                month.RegisterValueChangedCallback(evt => SetValueFromInputFields());
                day.RegisterValueChangedCallback(evt => SetValueFromInputFields());
                year.RegisterCallback<BlurEvent>(evt => SetValueFromInputFields());

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
                        accumulatorX += evt.deltaPosition.x;

                        float threshold = (evt.modifiers & EventModifiers.Shift) != 0 ? 2f : 8f;

                        if (Mathf.Abs(accumulatorX) >= threshold)
                        {
                            int dayOffset = Mathf.RoundToInt(accumulatorX / threshold);

                            if (dayOffset != 0)
                            {
                                MeridiemDate newDate = (date + dayOffset).ToBalanced();
                                date = newDate;

                                property.FindPropertyRelative("month").intValue = newDate.month;
                                property.FindPropertyRelative("day").intValue = newDate.day;
                                property.FindPropertyRelative("year").intValue = newDate.year;

                                month.SetValueWithoutNotify(CozyPreferences.Months[newDate.month - 1].name);
                                day.choices = Enumerable.Range(1, newDate.DaysInMonth).Select(i => i.ToString()).ToList();
                                day.value = newDate.day.ToString();
                                year.SetValueWithoutNotify(newDate.year.ToString("D4"));

                                accumulatorX -= dayOffset * threshold;
                                evt.StopPropagation();

                                property.serializedObject.ApplyModifiedProperties();
                                property.serializedObject.Update();
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

            return field;
        }
    }


    [CustomPropertyDrawer(typeof(DayOfYearAttribute))]
    public class DayOfYearDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement field = new VisualElement();
            field.AddToClassList("unity-base-field");

            Label label = new Label();
            label.text = property.displayName;
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
                MeridiemDate date;
                RefreshMeridiemTime();

                void RefreshMeridiemTime()
                {
                    date = new MeridiemDate(property.FindPropertyRelative("month").intValue,
                                            property.FindPropertyRelative("day").intValue,
                                            0);
                }

                DropdownField month = new DropdownField();
                month.name = "month";
                month.style.flexGrow = 1;
                month.choices = CozyPreferences.Months.Select(x => x.name).ToList();
                month.index = property.FindPropertyRelative("month").intValue - 1;

                DropdownField day = new DropdownField();
                day.name = "day";
                day.style.flexGrow = 1;
                List<string> days = new List<string>();
                for (int i = 0; i < date.DaysInMonth; i++)
                {
                    days.Add((i + 1).ToString());
                }
                day.choices = days;
                day.index = property.FindPropertyRelative("day").intValue - 1;

                switch (CozyPreferences.NumericDateFormat)
                {
                    case CozyPreferences.DateFormat.DMY:
                        input.Add(day);
                        input.Add(month);
                        break;
                    case CozyPreferences.DateFormat.MDY:
                    default:
                        input.Add(month);
                        input.Add(day);
                        break;
                }


                void SetValueFromInputFields()
                {
                    MeridiemDate time = new MeridiemDate(month.index + 1, day.index + 1, 0).ToBalanced();
                    property.FindPropertyRelative("month").intValue = time.month;
                    property.FindPropertyRelative("day").intValue = time.day;
                    property.FindPropertyRelative("year").intValue = 0;
                    property.serializedObject.ApplyModifiedProperties();
                    RefreshMeridiemTime();
                    month.index = time.month - 1;
                    day.index = time.day - 1;
                    CozyTimeModuleEditor.UpdateCalendarUI();

                    List<string> days = new List<string>();
                    for (int i = 0; i < date.DaysInMonth; i++)
                    {
                        days.Add((i + 1).ToString());
                    }
                    day.choices = days;

                }

                month.RegisterValueChangedCallback(evt =>
                {
                    SetValueFromInputFields();
                });
                day.RegisterValueChangedCallback(evt =>
                {
                    SetValueFromInputFields();
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
                        accumulatorX += evt.deltaPosition.x;

                        float threshold = (evt.modifiers & EventModifiers.Shift) != 0 ? 2f : 8f;

                        if (Mathf.Abs(accumulatorX) >= threshold)
                        {
                            int dayOffset = Mathf.RoundToInt(accumulatorX / threshold);

                            if (dayOffset != 0)
                            {
                                MeridiemDate newDate = (date + dayOffset).ToBalanced();
                                newDate.year = 0;
                                date = newDate;

                                property.FindPropertyRelative("month").intValue = newDate.month;
                                property.FindPropertyRelative("day").intValue = newDate.day;
                                property.serializedObject.ApplyModifiedProperties();

                                month.SetValueWithoutNotify(CozyPreferences.Months[newDate.month - 1].name);
                                day.choices = Enumerable.Range(1, newDate.DaysInMonth).Select(i => i.ToString()).ToList();
                                day.SetValueWithoutNotify((newDate.day).ToString());

                                accumulatorX -= dayOffset * threshold;
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