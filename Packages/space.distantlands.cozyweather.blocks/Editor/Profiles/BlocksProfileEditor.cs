using System;
using System.Collections.Generic;
using System.Linq;
using DistantLands.Cozy.Blocks;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(BlocksProfile))]
    [CanEditMultipleObjects]
    public class BlocksProfileEditor : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();
            StyleSheet globalStyles = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Globals.uss");
            if (globalStyles != null)
                root.styleSheets.Add(globalStyles);

            root.Add(new HelpBox(
                "A Blocks Profile contains only the atmosphere values you want to override. All other values remain controlled by the active Atmosphere profile.",
                HelpBoxMessageType.Info));

            PropertyField overrides = new PropertyField();
            overrides.BindProperty(serializedObject.FindProperty("overrides"));
            root.Add(overrides);

            Button addOverrideButton = new Button { text = "Add Override" };
            addOverrideButton.style.marginTop = 7;
            addOverrideButton.SetEnabled(!serializedObject.isEditingMultipleObjects);
            addOverrideButton.RegisterCallback<ClickEvent>(evt =>
            {
                BlocksOverrideSearchProvider provider = CreateInstance<BlocksOverrideSearchProvider>();
                provider.profile = (BlocksProfile)target;
                provider.arrayProperty = serializedObject.FindProperty("overrides");
                provider.action = () =>
                {
                    serializedObject.Update();
                    overrides.Bind(serializedObject);
                };

                SearchWindow.Open(
                    new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)),
                    provider);
            });
            root.Add(addOverrideButton);

            return root;
        }
    }

    public class BlocksOverrideSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        public BlocksProfile profile;
        [NonSerialized]
        public SerializedProperty arrayProperty;
        public Action action;

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> entries = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Add Override"), 0)
            };

            HashSet<Type> existingTypes = new HashSet<Type>();
            if (profile?.overrides != null)
            {
                foreach (BlockOverride blockOverride in profile.overrides)
                {
                    if (blockOverride != null)
                        existingTypes.Add(blockOverride.GetType());
                }
            }

            var availableOverrides = typeof(BlockOverride).Assembly.GetTypes()
                .Where(type =>
                    !type.IsAbstract &&
                    !type.ContainsGenericParameters &&
                    type != typeof(BlockOverride) &&
                    typeof(BlockOverride).IsAssignableFrom(type) &&
                    !existingTypes.Contains(type))
                .Select(type => (Type: type, Instance: (BlockOverride)Activator.CreateInstance(type)))
                .OrderBy(item => item.Instance.Category)
                .ThenBy(item => item.Instance.Title)
                .ToArray();

            string currentCategory = null;
            foreach (var item in availableOverrides)
            {
                string category = string.IsNullOrEmpty(item.Instance.Category)
                    ? "Other"
                    : item.Instance.Category;
                if (category != currentCategory)
                {
                    entries.Add(new SearchTreeGroupEntry(new GUIContent(category), 1));
                    currentCategory = category;
                }

                entries.Add(new SearchTreeEntry(new GUIContent(item.Instance.Title))
                {
                    userData = item.Instance,
                    level = 2
                });
            }

            return entries;
        }

        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            if (searchTreeEntry.userData is not BlockOverride blockOverride ||
                arrayProperty == null)
                return false;

            SerializedObject serializedObject = arrayProperty.serializedObject;
            serializedObject.Update();
            SerializedProperty list = serializedObject.FindProperty(arrayProperty.propertyPath);
            int index = list.arraySize;
            list.InsertArrayElementAtIndex(index);
            list.GetArrayElementAtIndex(index).managedReferenceValue = blockOverride;
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
            action?.Invoke();
            return true;
        }
    }

    [CustomPropertyDrawer(typeof(BlockOverride), true)]
    public class BlockOverrideDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement root = new VisualElement();
            SerializedProperty iterator = property.Copy();
            SerializedProperty endProperty = iterator.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) &&
                   !SerializedProperty.EqualContents(iterator, endProperty))
            {
                if (iterator.depth > property.depth + 1)
                {
                    enterChildren = false;
                    continue;
                }

                if (iterator.name == "m_Script" || iterator.name == "active")
                {
                    enterChildren = false;
                    continue;
                }

                PropertyField field = new PropertyField(iterator.Copy());
                field.BindProperty(iterator.Copy());
                root.Add(field);
                enterChildren = false;
            }

            return root;
        }
    }

    [CustomPropertyDrawer(typeof(BlockPropertyAttribute))]
    public class BlockPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            BlockPropertyAttribute blockProperty = (BlockPropertyAttribute)attribute;
            SerializedProperty disabledProperty = property.FindPropertyRelative("disabled");
            SerializedProperty valueProperty = property.FindPropertyRelative("value");
            if (disabledProperty == null || valueProperty == null)
            {
                return new HelpBox(
                    $"Block property '{property.displayName}' must use a Blocks value with disabled and value fields.",
                    HelpBoxMessageType.Error);
            }

            string label = ObjectNames.NicifyVariableName(blockProperty.fieldTitle);
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            Toggle toggle = new Toggle
            {
                label = string.Empty,
                tooltip = $"Enable the {label} override"
            };
            toggle.style.width = 20;
            toggle.SetValueWithoutNotify(!disabledProperty.boolValue);
            row.TrackPropertyValue(disabledProperty, serializedProperty =>
                toggle.SetValueWithoutNotify(!serializedProperty.boolValue));
            row.Add(toggle);

            VisualElement field;
            if (valueProperty.propertyType == SerializedPropertyType.Float &&
                blockProperty.minFloat < blockProperty.maxFloat)
            {
                Slider slider = new Slider(label, blockProperty.minFloat, blockProperty.maxFloat)
                {
                    showInputField = true,
                    tooltip = property.tooltip
                };
                slider.BindProperty(valueProperty);
                field = slider;
            }
            else
            {
                PropertyField propertyField = new PropertyField(valueProperty, label)
                {
                    tooltip = property.tooltip
                };
                propertyField.BindProperty(valueProperty);
                field = propertyField;
            }

            field.style.flexGrow = 1;
            field.SetEnabled(!disabledProperty.boolValue);
            toggle.RegisterValueChangedCallback(evt =>
            {
                disabledProperty.boolValue = !evt.newValue;
                disabledProperty.serializedObject.ApplyModifiedProperties();
                field.SetEnabled(evt.newValue);
            });
            row.Add(field);
            return row;
        }
    }
}
