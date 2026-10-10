using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using TextStudio.BuiltIns;
using TMPro;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    internal partial class TextStudioTextEditor
    {
        static VisualElement CreateSection(string title)
        {
            VisualElement section = new VisualElement();
            section.AddToClassList("ts-section");
            Label label = new Label(title);
            label.AddToClassList("ts-section-title");
            section.Add(label);
            return section;
        }

        static Foldout CreateFoldoutSection(string title, string name, bool expanded)
        {
            Foldout foldout = new Foldout
            {
                text = title,
                name = name,
                value = expanded
            };
            foldout.AddToClassList("ts-section");
            foldout.AddToClassList("ts-section-foldout");
            return foldout;
        }

        static PropertyField CreateBoundPropertyField(SerializedProperty property, string label)
        {
            PropertyField field = new PropertyField(property, label);
            field.AddToClassList("ts-property-field");
            if (property != null)
                field.name = BuildPropertyFieldName(property);
            return field;
        }

        static PropertyField CreateVisualSettingsPropertyField(
            SerializedProperty property,
            string label,
            bool editable,
            bool bindImmediately = true)
        {
            PropertyField field = CreateBoundPropertyField(property, label);
            if (bindImmediately && property != null && property.serializedObject != null)
                field.Bind(property.serializedObject);
            field.SetEnabled(editable);
            if (!editable)
                field.AddToClassList("ts-profile-read-only-field");
            return field;
        }

        static void AddDefaultEffectsExpressionField(
            VisualElement container,
            SerializedProperty property,
            string label,
            string tooltip,
            bool editable,
            bool bindImmediately)
        {
            PropertyField field = CreateVisualSettingsPropertyField(property, label, editable, bindImmediately);
            field.tooltip = tooltip;
            field.AddToClassList("ts-default-effects-expression-field");
            container.Add(field);
        }

        static string BuildPropertyFieldName(SerializedProperty property)
        {
            return "ts-field-" + property.propertyPath.Replace(".", "-");
        }

        static HelpBox CreateHelpBox(string message, HelpBoxMessageType type)
        {
            return TextStudioInspectorElements.CreateHelpBox(message, type);
        }

        static VisualElement CreateAlignedHint(string message, string name = null)
        {
            Label label = CreateAlignedHintLabel(message, name);
            return CreateAlignedHintRow(label);
        }

        static Label CreateAlignedHintLabel(string message, string name = null)
        {
            Label label = new Label(message);
            if (!string.IsNullOrEmpty(name))
                label.name = name;
            label.AddToClassList("ts-muted-text");
            label.AddToClassList("ts-form-hint-text");
            return label;
        }

        static VisualElement CreateAlignedHintRow(Label label)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("ts-form-hint-row");
            row.Add(label);
            return row;
        }

        static Button CreateIconButton(string text, string tooltip, Action action)
        {
            Button button = new Button(action)
            {
                text = text,
                tooltip = tooltip
            };
            button.AddToClassList("ts-preview-button");
            return button;
        }

        static bool ContainsOrdinalIgnoreCase(List<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
