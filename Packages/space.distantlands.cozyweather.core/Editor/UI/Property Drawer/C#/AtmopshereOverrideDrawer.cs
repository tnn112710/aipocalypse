//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomPropertyDrawer(typeof(AtmosphereOverride))]
    public class AtmosphereOverrideDrawer : PropertyDrawer
    {
        VisualElement root;
        List<SerializedProperty> properties = new List<SerializedProperty>();

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var attr = (AtmospherePropertyAttribute)attribute;

            root = new VisualElement();
            properties = new List<SerializedProperty>();

            VisualElement activeToggleContainer = new VisualElement();
            activeToggleContainer.AddToClassList("active-toggle-container");
            Label toggleAll = new Label("ALL");
            Label toggleNone = new Label("NONE");

            activeToggleContainer.Add(toggleAll);
            activeToggleContainer.Add(toggleNone);
            root.Add(activeToggleContainer);


            SerializedProperty iterator = property.Copy();
            SerializedProperty endProperty = iterator.GetEndProperty();

            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
            {
                // Only draw direct children (skip nested children)
                if (iterator.depth > property.depth + 1)
                {
                    enterChildren = false;
                    continue;
                }

                // Skip Unity internal fields like script reference, if any
                if (iterator.name == "m_Script" || iterator.propertyPath == "m_Script" || iterator.name == "active")
                    continue;

                var propertyReference = iterator.Copy();
                properties.Add(propertyReference);

                enterChildren = false;
            }


            root.Add(DisplayProperties(property));

            toggleAll.RegisterCallback<ClickEvent>(evt =>
            {
                foreach (SerializedProperty prop in properties) prop.FindPropertyRelative("overrideValue").boolValue = true;
                property.serializedObject.ApplyModifiedProperties();
            });
            toggleNone.RegisterCallback<ClickEvent>(evt =>
            {
                foreach (SerializedProperty prop in properties) prop.FindPropertyRelative("overrideValue").boolValue = false;
                property.serializedObject.ApplyModifiedProperties();
            });

            return root;
        }
        public virtual VisualElement DisplayProperties(SerializedProperty property)
        {
            VisualElement container = new VisualElement();

            foreach (SerializedProperty prop in properties)
            {
                var field = new PropertyField(prop);
                container.Add(field);
            }

            return container;

        }

    }
}