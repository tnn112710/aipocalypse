using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal interface IUIToolkitUxmlAuthoringStore
    {
        bool IsAvailable { get; }
        string Guidance { get; }
        string Text { get; }
        string ElementName { get; }
        SerializedProperty TextProperty { get; }
        IReadOnlyList<string> GetElementNames();
        bool TrySetText(string text, string undoName);
    }

    internal sealed class TextStudioUIToolkitUxmlAuthoringStore : IUIToolkitUxmlAuthoringStore
    {
        const string BindingPathMarker = "bindings.Array.data[";

        readonly SerializedObject _serializedObject;
        readonly string _ownerPrefix;
        readonly string _ownerTextPath;
        readonly string _guidance;

        public TextStudioUIToolkitUxmlAuthoringStore(SerializedProperty bindingProperty)
        {
            _serializedObject = bindingProperty?.serializedObject;
            if (bindingProperty == null ||
                !TryBuildOwnerPrefix(bindingProperty.propertyPath, out _ownerPrefix))
            {
                _guidance =
                    "Edit Label Text in the main UI Builder Inspector. This Unity version's owner serialization layout was not recognized.";
                return;
            }

            _ownerTextPath = _ownerPrefix + "text";

            SerializedProperty text = TextProperty;
            if (text == null || text.propertyType != SerializedPropertyType.String)
            {
                _ownerTextPath = string.Empty;
                _guidance =
                    "Edit Label Text in the main UI Builder Inspector. The owning Label text could not be resolved safely.";
            }
            else
            {
                _guidance = string.Empty;
            }
        }

        public bool IsAvailable => TextProperty != null;
        public string Guidance => _guidance;
        public string Text => TextProperty?.stringValue ?? string.Empty;
        public string ElementName
        {
            get
            {
                if (_serializedObject == null || string.IsNullOrEmpty(_ownerPrefix))
                    return string.Empty;
                SerializedProperty property = _serializedObject.FindProperty(_ownerPrefix + "name");
                return property?.stringValue ?? string.Empty;
            }
        }

        public SerializedProperty TextProperty
        {
            get
            {
                if (_serializedObject == null || string.IsNullOrEmpty(_ownerTextPath))
                    return null;
                return _serializedObject.FindProperty(_ownerTextPath);
            }
        }

        public IReadOnlyList<string> GetElementNames()
        {
            return CollectNamedElementNames(_serializedObject);
        }

        public bool TrySetText(string text, string undoName)
        {
            SerializedProperty property = TextProperty;
            if (property == null)
                return false;

            UnityEngine.Object[] targets = _serializedObject.targetObjects;
            Undo.RecordObjects(
                targets,
                string.IsNullOrWhiteSpace(undoName) ? "Edit Text Studio Markup" : undoName);
            _serializedObject.UpdateIfRequiredOrScript();
            property = TextProperty;
            if (property == null)
                return false;

            property.stringValue = text ?? string.Empty;
            MarkAttributeOverridden(_serializedObject, _ownerTextPath);
            bool applied = _serializedObject.ApplyModifiedProperties();
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                    EditorUtility.SetDirty(targets[i]);
            }
            return applied || string.Equals(Text, text ?? string.Empty, StringComparison.Ordinal);
        }

        internal static bool TryBuildOwnerTextPath(string bindingPropertyPath, out string ownerTextPath)
        {
            if (!TryBuildOwnerPrefix(bindingPropertyPath, out string ownerPrefix))
            {
                ownerTextPath = string.Empty;
                return false;
            }

            ownerTextPath = ownerPrefix + "text";
            return true;
        }

        static bool TryBuildOwnerPrefix(string bindingPropertyPath, out string ownerPrefix)
        {
            ownerPrefix = string.Empty;
            if (string.IsNullOrEmpty(bindingPropertyPath))
                return false;

            int marker = bindingPropertyPath.LastIndexOf(BindingPathMarker, StringComparison.Ordinal);
            if (marker < 0)
                return false;

            ownerPrefix = bindingPropertyPath.Substring(0, marker);
            return true;
        }

        internal static IReadOnlyList<string> CollectNamedElementNames(
            SerializedObject serializedObject)
        {
            List<string> names = new List<string>();
            if (serializedObject == null)
                return names;

            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;
                if (iterator.propertyType != SerializedPropertyType.String ||
                    !iterator.propertyPath.Contains("m_Properties.Array.data[") ||
                    !iterator.propertyPath.EndsWith(".name", StringComparison.Ordinal) ||
                    !string.Equals(iterator.stringValue, "name", StringComparison.Ordinal))
                {
                    continue;
                }

                string valuePath = iterator.propertyPath.Substring(
                    0,
                    iterator.propertyPath.Length - ".name".Length) + ".value";
                SerializedProperty value = serializedObject.FindProperty(valuePath);
                if (value != null &&
                    value.propertyType == SerializedPropertyType.String &&
                    !string.IsNullOrWhiteSpace(value.stringValue))
                {
                    names.Add(value.stringValue);
                }
            }

            return names;
        }

        internal static int CountNamedElementMatches(
            IReadOnlyList<string> elementNames,
            string candidate)
        {
            if (elementNames == null || string.IsNullOrWhiteSpace(candidate))
                return 0;

            int matches = 0;
            for (int i = 0; i < elementNames.Count; i++)
            {
                if (string.Equals(elementNames[i], candidate, StringComparison.Ordinal))
                    matches++;
            }
            return matches;
        }

        internal static void MarkAttributeOverridden(
            SerializedObject serializedObject,
            string propertyPath)
        {
            if (serializedObject == null || string.IsNullOrEmpty(propertyPath))
                return;

            SerializedProperty flags = serializedObject.FindProperty(
                propertyPath + "_UxmlAttributeFlags");
            if (flags != null)
            {
                const string overriddenFlagName = "OverriddenInUxml";
                int overriddenFlagIndex = Array.IndexOf(flags.enumNames, overriddenFlagName);
                if (overriddenFlagIndex >= 0)
                    flags.enumValueIndex = overriddenFlagIndex;
                else
                    flags.intValue = 1;
            }
        }
    }
}
