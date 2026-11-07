using System.Collections.Generic;
using DistantLands.Cozy.Blocks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomPropertyDrawer(typeof(BlockOverridesListAttribute))]
    public class BlockOverridesListDrawer : PropertyDrawer
    {
        private readonly List<bool> foldoutStates = new();
        private readonly List<bool> generationStates = new();

        private sealed class ListItemState
        {
            public int index = -1;
            public int boundIndex = -1;
            public bool featureEnabled;
            public string category;
            public string title;
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement container = new VisualElement();

            if (!property.isArray)
            {
                container.Add(new Label("[BlockOverridesList] must be used on an Array or List."));
                return container;
            }

            SyncStateLists(property.arraySize);

            ListView listView = new ListView
            {
                bindingPath = property.propertyPath,
                showBoundCollectionSize = false,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                selectionType = SelectionType.None
            };
            listView.AddToClassList("overridesList");
            listView.makeItem = () => BuildListItemTemplate(property, listView);

            listView.bindItem = (VisualElement element, int index) =>
            {
                if (index < 0 || index >= property.arraySize)
                    return;

                SyncStateLists(property.arraySize);

                SerializedProperty elementProperty = property.GetArrayElementAtIndex(index);
                BlockOverride blockOverride = elementProperty.managedReferenceValue as BlockOverride;
                VisualElement header = element.Q<VisualElement>("header");
                Toggle activeToggle = element.Q<Toggle>("activeToggle");
                VisualElement warning = element.Q<VisualElement>("warning");
                VisualElement contentContainer = element.Q<VisualElement>("container");
                VisualElement context = element.Q<VisualElement>("context");
                VisualElement help = element.Q<VisualElement>("help");
                Label label = element.Q<Label>("label");
                ListItemState state = (ListItemState)element.userData;
                if (state.boundIndex >= 0 && state.boundIndex != index)
                {
                    contentContainer.Clear();
                    generationStates[index] = false;
                }

                if (blockOverride == null)
                {
                    state.index = index;
                    state.featureEnabled = false;
                    state.category = string.Empty;
                    state.title = string.Empty;
                    header.ClearClassList();
                    header.AddToClassList("overrideFoldout");
                    label.text = "Missing Block Override";
                    contentContainer.Clear();
                    contentContainer.style.display = DisplayStyle.None;
                    activeToggle.style.display = DisplayStyle.None;
                    warning.style.display = DisplayStyle.None;
                    help.style.display = DisplayStyle.None;
                    activeToggle.SetValueWithoutNotify(false);
                    contentContainer.SetEnabled(false);
                    state.boundIndex = index;
                    return;
                }

                bool featureEnabled = blockOverride.FeatureEnabledGlobally;
                state.index = index;
                state.featureEnabled = featureEnabled;
                state.category = blockOverride.Category;
                state.title = blockOverride.Title;
                bool open = foldoutStates[index];

                header.ClearClassList();
                header.AddToClassList("overrideFoldout");
                header.AddToClassList(blockOverride.Category.ToLowerInvariant());

                label.text = blockOverride.Title;
                warning.style.display = featureEnabled ? DisplayStyle.None : DisplayStyle.Flex;
                contentContainer.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;

                SerializedProperty activeProperty = elementProperty.FindPropertyRelative("active");
                if (activeProperty != null)
                {
                    activeToggle.style.display = DisplayStyle.Flex;
                    activeToggle.SetValueWithoutNotify(activeProperty.boolValue);
                    contentContainer.SetEnabled(activeProperty.boolValue && featureEnabled);
                }

                help.tooltip = $"Open COZY {blockOverride.Category} feature documentation";
                context.tooltip = "Move or remove this override";

                if (open && (!generationStates[index] || contentContainer.childCount == 0))
                    GenerateBody(property, element, index);
                state.boundIndex = index;
            };

            listView.itemIndexChanged += (int oldIndex, int newIndex) =>
            {
                MoveState(foldoutStates, oldIndex, newIndex);
                MoveState(generationStates, oldIndex, newIndex);
                property.serializedObject.ApplyModifiedProperties();
                listView.RefreshItems();
            };

            container.Add(listView);
            return container;
        }

        private void GenerateBody(SerializedProperty property, VisualElement element, int index)
        {
            SerializedProperty elementProperty = GetElementProperty(property, index);
            if (elementProperty == null)
                return;

            VisualElement contentContainer = element.Q<VisualElement>("container");
            contentContainer.Clear();
            PropertyField propertyField = new PropertyField(elementProperty);
            propertyField.BindProperty(elementProperty);
            propertyField.style.marginBottom = 10;
            contentContainer.Add(propertyField);
            generationStates[index] = true;
        }

        private static SerializedProperty GetElementProperty(SerializedProperty property, int index)
        {
            SerializedProperty list = property.serializedObject.FindProperty(property.propertyPath);
            if (list == null || index < 0 || index >= list.arraySize)
                return null;

            return list.GetArrayElementAtIndex(index);
        }

        private void MoveElement(SerializedProperty property, ListView listView, int from, int to)
        {
            if (from < 0 || from >= property.arraySize || to < 0 || to >= property.arraySize)
                return;

            property.MoveArrayElement(from, to);
            property.serializedObject.ApplyModifiedProperties();
            MoveState(foldoutStates, from, to);
            MoveState(generationStates, from, to);
            listView.RefreshItems();
        }

        private void RemoveElement(SerializedProperty property, ListView listView, int index)
        {
            if (index < 0 || index >= property.arraySize)
                return;

            property.DeleteArrayElementAtIndex(index);
            property.serializedObject.ApplyModifiedProperties();
            foldoutStates.RemoveAt(index);
            generationStates.RemoveAt(index);
            listView.RefreshItems();
        }

        private VisualElement BuildListItemTemplate(SerializedProperty property, ListView listView)
        {
            VisualElement foldout = new VisualElement { name = "foldout" };
            foldout.userData = new ListItemState();

            VisualElement header = new VisualElement { name = "header" };
            foldout.Add(header);

            VisualElement contentContainer = new VisualElement { name = "container" };
            foldout.Add(contentContainer);

            Toggle activeToggle = new Toggle { name = "activeToggle" };
            header.Add(activeToggle);

            VisualElement icon = new VisualElement { name = "icon" };
            header.Add(icon);

            Label label = new Label { name = "label" };
            header.Add(label);

            VisualElement split = new VisualElement { name = "split" };
            header.Add(split);

            VisualElement warning = new VisualElement { name = "warning" };
            warning.tooltip = "This shader feature is currently disabled. Please enable it globally.";
            warning.AddToClassList("toolbarBtn");
            header.Add(warning);

            VisualElement help = new VisualElement { name = "help" };
            help.AddToClassList("toolbarBtn");
            header.Add(help);

            VisualElement context = new VisualElement { name = "context" };
            context.AddToClassList("toolbarBtn");
            header.Add(context);

            header.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == context || evt.target == help ||
                    evt.target == activeToggle || evt.target == warning)
                    return;

                ListItemState state = (ListItemState)foldout.userData;
                if (state.index < 0 || state.index >= foldoutStates.Count)
                    return;

                bool open = !foldoutStates[state.index];
                foldoutStates[state.index] = open;
                if (open && (!generationStates[state.index] || contentContainer.childCount == 0))
                    GenerateBody(property, foldout, state.index);
                contentContainer.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            });

            activeToggle.RegisterValueChangedCallback(evt =>
            {
                ListItemState state = (ListItemState)foldout.userData;
                SerializedProperty elementProperty = GetElementProperty(property, state.index);
                SerializedProperty activeProperty = elementProperty?.FindPropertyRelative("active");
                if (activeProperty == null)
                    return;

                activeProperty.boolValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
                contentContainer.SetEnabled(evt.newValue && state.featureEnabled);
            });

            warning.RegisterCallback<ClickEvent>(evt =>
                SettingsService.OpenProjectSettings("Project/COZY: Stylized Weather 4/Features"));

            help.RegisterCallback<ClickEvent>(evt =>
            {
                ListItemState state = (ListItemState)foldout.userData;
                if (!string.IsNullOrEmpty(state.category) && !string.IsNullOrEmpty(state.title))
                    Application.OpenURL(
                        $"https://distant-lands.gitbook.io/cozy-weather-4/using-cozy/features/{state.category.ToLowerInvariant()}#{state.title}");
            });

            context.RegisterCallback<ClickEvent>(evt =>
            {
                ListItemState state = (ListItemState)foldout.userData;
                int index = state.index;
                if (index < 0 || index >= property.arraySize)
                    return;

                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Move To Top"), false, () => MoveElement(property, listView, index, 0));
                menu.AddItem(new GUIContent("Move Up"), false, () => MoveElement(property, listView, index, Mathf.Max(index - 1, 0)));
                menu.AddItem(new GUIContent("Move Down"), false, () => MoveElement(property, listView, index, Mathf.Min(index + 1, property.arraySize - 1)));
                menu.AddItem(new GUIContent("Move To Bottom"), false, () => MoveElement(property, listView, index, property.arraySize - 1));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Remove Layer"), false, () => RemoveElement(property, listView, index));
                menu.ShowAsContext();
            });

            return foldout;
        }

        private void SyncStateLists(int targetSize)
        {
            while (foldoutStates.Count < targetSize)
                foldoutStates.Add(false);
            while (generationStates.Count < targetSize)
                generationStates.Add(false);
            while (foldoutStates.Count > targetSize)
                foldoutStates.RemoveAt(foldoutStates.Count - 1);
            while (generationStates.Count > targetSize)
                generationStates.RemoveAt(generationStates.Count - 1);
        }

        private static void MoveState<T>(List<T> list, int from, int to)
        {
            if (from < 0 || from >= list.Count || to < 0 || to >= list.Count)
                return;

            T item = list[from];
            list.RemoveAt(from);
            list.Insert(to, item);
        }
    }
}
