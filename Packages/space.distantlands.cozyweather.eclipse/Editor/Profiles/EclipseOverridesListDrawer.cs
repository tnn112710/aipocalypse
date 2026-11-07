using System.Collections.Generic;
using DistantLands.Cozy.Eclipse;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomPropertyDrawer(typeof(EclipseOverridesListAttribute))]
    public class EclipseOverridesListDrawer : PropertyDrawer
    {
        private readonly List<bool> foldoutStates = new List<bool>();
        private readonly List<bool> bodyStates = new List<bool>();

        private sealed class ItemState
        {
            public int index = -1;
            public int boundIndex = -1;
            public string category;
            public string title;
            public bool featureEnabled;
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement root = new VisualElement();
            if (!property.isArray)
            {
                root.Add(new HelpBox("[EclipseOverridesList] must be used on an Array or List.", HelpBoxMessageType.Error));
                return root;
            }

            SyncState(property.arraySize);
            ListView listView = new ListView
            {
                bindingPath = property.propertyPath,
                showBoundCollectionSize = false,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                selectionType = SelectionType.None
            };
            listView.AddToClassList("overridesList");
            listView.makeItem = () => CreateItem(property, listView);
            listView.bindItem = (element, index) => BindItem(property, element, index);
            listView.itemIndexChanged += (oldIndex, newIndex) =>
            {
                MoveState(foldoutStates, oldIndex, newIndex);
                MoveState(bodyStates, oldIndex, newIndex);
                property.serializedObject.ApplyModifiedProperties();
                listView.RefreshItems();
            };
            root.Add(listView);
            return root;
        }

        private void BindItem(SerializedProperty property, VisualElement element, int index)
        {
            if (index < 0 || index >= property.arraySize)
                return;

            SyncState(property.arraySize);
            SerializedProperty elementProperty = property.GetArrayElementAtIndex(index);
            EclipseOverride eclipseOverride = elementProperty.managedReferenceValue as EclipseOverride;
            VisualElement header = element.Q<VisualElement>("header");
            Toggle activeToggle = element.Q<Toggle>("activeToggle");
            VisualElement content = element.Q<VisualElement>("container");
            VisualElement warning = element.Q<VisualElement>("warning");
            VisualElement context = element.Q<VisualElement>("context");
            Label label = element.Q<Label>("label");
            ItemState state = (ItemState)element.userData;

            if (state.boundIndex >= 0 && state.boundIndex != index)
            {
                content.Clear();
                bodyStates[index] = false;
            }

            state.index = index;
            state.boundIndex = index;
            if (eclipseOverride == null)
            {
                state.category = string.Empty;
                state.title = string.Empty;
                state.featureEnabled = false;
                header.ClearClassList();
                header.AddToClassList("overrideFoldout");
                label.text = "Missing Eclipse Override";
                content.Clear();
                content.style.display = DisplayStyle.None;
                activeToggle.style.display = DisplayStyle.None;
                warning.style.display = DisplayStyle.None;
                content.SetEnabled(false);
                return;
            }

            state.category = eclipseOverride.Category;
            state.title = eclipseOverride.Title;
            state.featureEnabled = eclipseOverride.FeatureEnabledGlobally;
            header.ClearClassList();
            header.AddToClassList("overrideFoldout");
            if (!string.IsNullOrEmpty(eclipseOverride.Category))
                header.AddToClassList(eclipseOverride.Category.ToLowerInvariant());

            label.text = eclipseOverride.Title;
            bool open = foldoutStates[index];
            content.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            warning.style.display = state.featureEnabled ? DisplayStyle.None : DisplayStyle.Flex;
            activeToggle.style.display = DisplayStyle.Flex;

            SerializedProperty activeProperty = elementProperty.FindPropertyRelative("active");
            activeToggle.SetValueWithoutNotify(activeProperty == null || activeProperty.boolValue);
            content.SetEnabled((activeProperty == null || activeProperty.boolValue) && state.featureEnabled);
            context.tooltip = "Move or remove this override";

            if (open && (!bodyStates[index] || content.childCount == 0))
                GenerateBody(property, element, index);
        }

        private VisualElement CreateItem(SerializedProperty property, ListView listView)
        {
            VisualElement item = new VisualElement();
            item.userData = new ItemState();

            VisualElement header = new VisualElement { name = "header" };
            item.Add(header);
            VisualElement content = new VisualElement { name = "container" };
            item.Add(content);

            Toggle activeToggle = new Toggle { name = "activeToggle" };
            header.Add(activeToggle);
            header.Add(new VisualElement { name = "icon" });
            header.Add(new Label { name = "label" });
            header.Add(new VisualElement { name = "split" });
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
                if (evt.target == activeToggle || evt.target == warning || evt.target == help || evt.target == context)
                    return;

                ItemState state = (ItemState)item.userData;
                if (state.index < 0 || state.index >= foldoutStates.Count)
                    return;

                foldoutStates[state.index] = !foldoutStates[state.index];
                bool open = foldoutStates[state.index];
                if (open && (!bodyStates[state.index] || content.childCount == 0))
                    GenerateBody(property, item, state.index);
                content.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            });

            activeToggle.RegisterValueChangedCallback(evt =>
            {
                ItemState state = (ItemState)item.userData;
                SerializedProperty overrideProperty = GetElementProperty(property, state.index);
                SerializedProperty activeProperty = overrideProperty?.FindPropertyRelative("active");
                if (activeProperty == null)
                    return;

                activeProperty.boolValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
                content.SetEnabled(evt.newValue && state.featureEnabled);
            });

            warning.RegisterCallback<ClickEvent>(evt =>
                SettingsService.OpenProjectSettings("Project/COZY: Stylized Weather 4/Features"));
            help.RegisterCallback<ClickEvent>(evt =>
            {
                ItemState state = (ItemState)item.userData;
                if (!string.IsNullOrEmpty(state.category) && !string.IsNullOrEmpty(state.title))
                    Application.OpenURL(
                        $"https://distant-lands.gitbook.io/cozy-weather-4/using-cozy/features/{state.category.ToLowerInvariant()}#{state.title}");
            });
            context.RegisterCallback<ClickEvent>(evt =>
            {
                ItemState state = (ItemState)item.userData;
                if (state.index < 0 || state.index >= property.arraySize)
                    return;

                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Move To Top"), false, () => MoveElement(property, listView, state.index, 0));
                menu.AddItem(new GUIContent("Move Up"), false, () => MoveElement(property, listView, state.index, Mathf.Max(state.index - 1, 0)));
                menu.AddItem(new GUIContent("Move Down"), false, () => MoveElement(property, listView, state.index, Mathf.Min(state.index + 1, property.arraySize - 1)));
                menu.AddItem(new GUIContent("Move To Bottom"), false, () => MoveElement(property, listView, state.index, property.arraySize - 1));
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Remove Override"), false, () => RemoveElement(property, listView, state.index));
                menu.ShowAsContext();
            });

            return item;
        }

        private void GenerateBody(SerializedProperty property, VisualElement element, int index)
        {
            SerializedProperty elementProperty = GetElementProperty(property, index);
            if (elementProperty == null)
                return;

            VisualElement content = element.Q<VisualElement>("container");
            content.Clear();
            PropertyField propertyField = new PropertyField(elementProperty);
            propertyField.BindProperty(elementProperty);
            propertyField.style.marginBottom = 10;
            content.Add(propertyField);
            bodyStates[index] = true;
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
            MoveState(bodyStates, from, to);
            listView.RefreshItems();
        }

        private void RemoveElement(SerializedProperty property, ListView listView, int index)
        {
            if (index < 0 || index >= property.arraySize)
                return;

            property.DeleteArrayElementAtIndex(index);
            property.serializedObject.ApplyModifiedProperties();
            foldoutStates.RemoveAt(index);
            bodyStates.RemoveAt(index);
            listView.RefreshItems();
        }

        private void SyncState(int targetSize)
        {
            while (foldoutStates.Count < targetSize)
                foldoutStates.Add(false);
            while (bodyStates.Count < targetSize)
                bodyStates.Add(false);
            while (foldoutStates.Count > targetSize)
                foldoutStates.RemoveAt(foldoutStates.Count - 1);
            while (bodyStates.Count > targetSize)
                bodyStates.RemoveAt(bodyStates.Count - 1);
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
