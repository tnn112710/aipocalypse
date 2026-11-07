//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(StaticEvent))]
    public class MeridiemEventProfileEditor : Editor
    {

        StaticEvent profile;
        VisualElement root;
        private Texture2D _iconTexture;

        VisualElement timeContainer => root.Q<VisualElement>("time-container");
        VisualElement eventHeader => root.Q<VisualElement>("event-header");
        TextField nameField => root.Q<TextField>("name");
        VisualElement eventType => root.Q<EnumField>();

        void OnEnable()
        {
            profile = (StaticEvent)target;
        }

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();

            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Profiles/UXML/meridiem-event-profile-editor.uxml"
            );

            asset.CloneTree(root);

            switch (profile.EventType)
            {
                case MeridiemEventType.Weather:
                    eventHeader.AddToClassList("weather");
                    break;
                case MeridiemEventType.Ambient:
                    eventHeader.AddToClassList("ambient");
                    break;
                case MeridiemEventType.Holiday:
                    eventHeader.AddToClassList("holiday");
                    break;
                case MeridiemEventType.NPC:
                    eventHeader.AddToClassList("npc");
                    break;
                case MeridiemEventType.Enemy:
                    eventHeader.AddToClassList("enemy");
                    break;
                case MeridiemEventType.Player:
                    eventHeader.AddToClassList("player");
                    break;
                case MeridiemEventType.Status:
                    eventHeader.AddToClassList("status");
                    break;
                case MeridiemEventType.WorldEvent:
                    eventHeader.AddToClassList("worldevent");
                    break;
            }

            void UpdateEventHeader()
            {
                eventHeader.RemoveFromClassList("weather");
                eventHeader.RemoveFromClassList("ambient");
                eventHeader.RemoveFromClassList("holiday");
                eventHeader.RemoveFromClassList("npc");
                eventHeader.RemoveFromClassList("enemy");
                eventHeader.RemoveFromClassList("player");
                eventHeader.RemoveFromClassList("status");
                eventHeader.RemoveFromClassList("worldevent");

                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();

                switch (profile.EventType)
                {
                    case MeridiemEventType.Weather:
                        eventHeader.AddToClassList("weather");
                        break;
                    case MeridiemEventType.Ambient:
                        eventHeader.AddToClassList("ambient");
                        break;
                    case MeridiemEventType.Holiday:
                        eventHeader.AddToClassList("holiday");
                        break;
                    case MeridiemEventType.NPC:
                        eventHeader.AddToClassList("npc");
                        break;
                    case MeridiemEventType.Enemy:
                        eventHeader.AddToClassList("enemy");
                        break;
                    case MeridiemEventType.Player:
                        eventHeader.AddToClassList("player");
                        break;
                    case MeridiemEventType.Status:
                        eventHeader.AddToClassList("status");
                        break;
                    case MeridiemEventType.WorldEvent:
                        eventHeader.AddToClassList("worldevent");
                        break;
                }
            }

            eventType.RegisterCallback<BlurEvent>(evt =>
                UpdateEventHeader()
            );
            eventType.RegisterCallback<ChangeEvent<Enum>>(evt =>
                UpdateEventHeader()
            );

            PropertyField startDate = new PropertyField();
            startDate.BindProperty(serializedObject.FindProperty("startDate"));
            timeContainer.Add(startDate);

            PropertyField endDate = new PropertyField();
            endDate.BindProperty(serializedObject.FindProperty("endDate"));
            timeContainer.Add(endDate);

            PropertyField startTime = new PropertyField();
            startTime.BindProperty(serializedObject.FindProperty("startTime"));
            timeContainer.Add(startTime);

            PropertyField endTime = new PropertyField();
            endTime.BindProperty(serializedObject.FindProperty("endTime"));
            timeContainer.Add(endTime);

            Tooltip overnightWarning = new Tooltip("This event happens overnight", StatusType.Info);
            timeContainer.Add(overnightWarning);

            PropertyField allDay = new PropertyField();
            allDay.BindProperty(serializedObject.FindProperty("allDay"));
            timeContainer.Add(allDay);

            PropertyField dateRange = new PropertyField();
            dateRange.BindProperty(serializedObject.FindProperty("dateRange"));
            timeContainer.Add(dateRange);

            DropdownField repeatStyle = new DropdownField();
            repeatStyle.label = "Repeat Style";
            if (profile.RepeatStyle != null)
            repeatStyle.value = profile.RepeatStyle.DisplayName;

            repeatStyle.choices = MeridiemEventRepeatStyle.RepeatTypeNames;
            timeContainer.Add(repeatStyle);

            VisualElement repeatStyleVariables = new VisualElement();
            UpdateRepeatStyleVariables();
            timeContainer.Add(repeatStyleVariables);

            repeatStyle.RegisterValueChangedCallback(evt =>
            {
                profile.RepeatStyle = (MeridiemEventRepeatStyle)Activator.CreateInstance(MeridiemEventRepeatStyle.MeridiemEventRepeatStyles[repeatStyle.index]);
                UpdateVisibility();
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
                UpdateRepeatStyleVariables();
                EditorUtility.SetDirty(profile);

            });
            dateRange.RegisterValueChangeCallback(evt =>
            {
                UpdateVisibility();
            });
            allDay.RegisterValueChangeCallback(evt =>
            {
                UpdateVisibility();
            });


            startTime.RegisterValueChangeCallback(evt => { UpdateWarningVisibility(); });
            endTime.RegisterValueChangeCallback(evt => { UpdateWarningVisibility(); });
            startDate.RegisterValueChangeCallback(evt => { UpdateWarningVisibility(); });
            endDate.RegisterValueChangeCallback(evt => { UpdateWarningVisibility(); });

            void UpdateRepeatStyleVariables()
            {
                repeatStyleVariables.Clear();

                SerializedProperty repeatStyleProp = serializedObject.FindProperty("repeatStyle");
                SerializedProperty endProp = repeatStyleProp.GetEndProperty();

                SerializedProperty childProp = repeatStyleProp.Copy();
                if (childProp.NextVisible(true)) // move into first child
                {
                    do
                    {
                        if (SerializedProperty.EqualContents(childProp, endProp))
                            break;

                        PropertyField field = new PropertyField();
                        field.BindProperty(childProp);
                        repeatStyleVariables.Add(field);

                    } while (childProp.NextVisible(false)); // stay in same depth
                }
            }

            void UpdateVisibility()
            {
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();

                startDate.style.display = DisplayStyle.Flex;
                endDate.style.display = DisplayStyle.Flex;
                startTime.style.display = DisplayStyle.Flex;
                endTime.style.display = DisplayStyle.Flex;
                dateRange.style.display = DisplayStyle.Flex;

                if (!profile.RepeatStyle.LinkedToDay)
                {
                    startDate.style.display = DisplayStyle.None;
                    endDate.style.display = DisplayStyle.None;
                    dateRange.style.display = DisplayStyle.None;
                }

                if (!profile.DateRange)
                {
                    endDate.style.display = DisplayStyle.None;
                }

                if (profile.AllDay)
                {
                    startTime.style.display = DisplayStyle.None;
                    endTime.style.display = DisplayStyle.None;
                }
            }

            void UpdateWarningVisibility()
            {
                overnightWarning.style.display = DisplayStyle.None;

                if (profile.Overnight)
                    overnightWarning.style.display = DisplayStyle.Flex;
            }

            return root;
        }

    }
}