using System.Reflection;
using UnityEditor;
using UnityEngine;
using SunfishCreations.Common;

namespace SunfishCreations.Common.Editor
{
    [CustomPropertyDrawer(typeof(HideIfAttribute))]
    public class HideIfDrawer : PropertyDrawer
    {
        private const BindingFlags FieldFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return ShouldHide(property) ? 0f : EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (ShouldHide(property))
                return;

            EditorGUI.PropertyField(position, property, label, true);
        }

        private bool ShouldHide(SerializedProperty property)
        {
            var attr = (HideIfAttribute)attribute;
            object target = GetTargetObject(property);

            if (target == null)
                return false;

            FieldInfo field = target.GetType().GetField(attr.ConditionFieldName, FieldFlags);

            if (field == null)
            {
                Debug.LogWarning($"[HideIf] Could not find field '{attr.ConditionFieldName}' on {target.GetType().Name}.");
                return false;
            }

            bool conditionValue;

            if (field.FieldType == typeof(bool))
            {
                conditionValue = (bool)field.GetValue(target);
            }
            else
            {
                // Support BoolReference and any type exposing a bool Value property
                PropertyInfo valueProp = field.FieldType.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
                if (valueProp == null || valueProp.PropertyType != typeof(bool))
                {
                    Debug.LogWarning($"[HideIf] Field '{attr.ConditionFieldName}' on {target.GetType().Name} is not a bool or a type with a bool Value property.");
                    return false;
                }

                conditionValue = (bool)valueProp.GetValue(field.GetValue(target));
            }

            return conditionValue == attr.HideWhenEquals;
        }

        /// <summary>
        /// Walks the serialized property path to retrieve the owner object.
        /// </summary>
        private static object GetTargetObject(SerializedProperty property)
        {
            object obj = property.serializedObject.targetObject;
            string[] pathParts = property.propertyPath.Replace(".Array.data[", "[").Split('.');

            // We want the owner of this property, so skip the last segment
            for (int i = 0; i < pathParts.Length - 1; i++)            {
                string part = pathParts[i];

                if (part.Contains("["))
                {
                    string memberName = part[..part.IndexOf('[')];
                    int index = int.Parse(part[(part.IndexOf('[') + 1)..part.IndexOf(']')]);
                    obj = GetFieldValue(obj, memberName);

                    if (obj is System.Collections.IList list)
                        obj = list[index];
                }
                else
                {
                    obj = GetFieldValue(obj, part);
                }

                if (obj == null)
                    return null;
            }

            return obj;
        }

        private static object GetFieldValue(object source, string name)
        {
            if (source == null)
                return null;

            System.Type type = source.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(name, FieldFlags);
                if (field != null)
                    return field.GetValue(source);

                type = type.BaseType;
            }

            return null;
        }
    }
}
