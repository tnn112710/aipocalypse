//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomPropertyDrawer(typeof(AtmosphereOverridesListAttribute))]
    public class AtmosphereOverridesListDrawer : PropertyDrawer
    {
        private List<bool> layerFoldoutStates = new List<bool>();
        private List<bool> layerGenerationStates = new List<bool>();

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement container = new VisualElement();

            if (!property.isArray)
            {
                container.Add(new Label("[AtmosphereOverridesList] must be used on an Array or List."));
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

            listView.makeItem = BuildListItemTemplate;

            listView.bindItem = (VisualElement element, int i) =>
            {
                if (i < 0 || i >= property.arraySize) return;

                SerializedProperty elementProperty = property.GetArrayElementAtIndex(i);
                AtmosphereOverride atmosphereOverride = elementProperty.managedReferenceValue as AtmosphereOverride;

                if (atmosphereOverride == null) return;

                bool featureEnabled = atmosphereOverride.FeatureEnabledGlobally;
                bool open = layerFoldoutStates[i];

                VisualElement header = element.Q<VisualElement>("header");
                Toggle activeToggle = element.Q<Toggle>("activeToggle");
                VisualElement warning = element.Q<VisualElement>("warning");
                VisualElement contentContainer = element.Q<VisualElement>("container");
                VisualElement context = element.Q<VisualElement>("context");
                VisualElement help = element.Q<VisualElement>("help");
                Label label = element.Q<Label>("label");

                header.ClearClassList();
                header.AddToClassList("overrideFoldout");
                header.AddToClassList(atmosphereOverride.Category.ToLower());

                label.text = atmosphereOverride.Title;

                warning.style.display = featureEnabled ? DisplayStyle.None : DisplayStyle.Flex;
                warning.RegisterCallback<ClickEvent>(evt =>
                    SettingsService.OpenProjectSettings("Project/COZY: Stylized Weather 4/Features"));

                contentContainer.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;

                help.RegisterCallback<ClickEvent>(evt =>
                    Application.OpenURL($"https://distant-lands.gitbook.io/cozy-weather-4/using-cozy/features/{atmosphereOverride.Category.ToLower()}#{atmosphereOverride.Title}"));

                SerializedProperty activeProp = elementProperty.FindPropertyRelative("active");
                if (activeProp != null)
                {
                    activeToggle.SetValueWithoutNotify(activeProp.boolValue);
                    activeToggle.RegisterValueChangedCallback(evt =>
                    {
                        activeProp.boolValue = evt.newValue;
                        property.serializedObject.ApplyModifiedProperties();
                        contentContainer.SetEnabled(evt.newValue && featureEnabled);
                    });
                }

                void GenerateBody()
                {
                    contentContainer.Clear();
                    PropertyField propertyField = new PropertyField(elementProperty);
                    propertyField.BindProperty(elementProperty);
                    propertyField.style.marginBottom = 10;
                    propertyField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
                    {
                        if (CozyWeather.Instance && CozyWeather.Instance.Atmosphere != null)
                            ((CozyAtmosphereModule)CozyWeather.Instance.Atmosphere).UpdateModel();
                    });
                    contentContainer.Add(propertyField);
                    layerGenerationStates[i] = true;
                }

                header.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.target != context && evt.target != help && evt.target != activeToggle && evt.target != warning)
                    {
                        open = !open;
                        layerFoldoutStates[i] = open;
                        if (open && (!layerGenerationStates[i] || contentContainer.childCount == 0)) GenerateBody();
                        contentContainer.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                    }
                });

                if (open && (!layerGenerationStates[i] || contentContainer.childCount == 0)) GenerateBody();

                void MoveElement(int newPos)
                {
                    property.MoveArrayElement(i, newPos);
                    property.serializedObject.ApplyModifiedProperties();

                    MoveState(layerFoldoutStates, i, newPos);
                    MoveState(layerGenerationStates, i, newPos);

                    listView.RefreshItems();
                }

                context.RegisterCallback<ClickEvent>(evt =>
                {
                    GenericMenu ctxMenu = new GenericMenu();
                    ctxMenu.AddItem(new GUIContent("Move To Top"), false, () => MoveElement(0));
                    ctxMenu.AddItem(new GUIContent("Move Up"), false, () => MoveElement(Mathf.Max(i - 1, 0)));
                    ctxMenu.AddItem(new GUIContent("Move Down"), false, () => MoveElement(Mathf.Min(i + 1, property.arraySize - 1)));
                    ctxMenu.AddItem(new GUIContent("Move To Bottom"), false, () => MoveElement(property.arraySize - 1));
                    ctxMenu.AddSeparator("");
                    ctxMenu.AddItem(new GUIContent("Remove Layer"), false, () =>
                    {
                        property.DeleteArrayElementAtIndex(i);
                        property.serializedObject.ApplyModifiedProperties();
                        layerFoldoutStates.RemoveAt(i);
                        layerGenerationStates.RemoveAt(i);
                        listView.RefreshItems();
                    });
                    ctxMenu.ShowAsContext();
                });
            };

            listView.itemIndexChanged += (int oldIndex, int newIndex) =>
            {
                MoveState(layerFoldoutStates, oldIndex, newIndex);
                MoveState(layerGenerationStates, oldIndex, newIndex);

                property.serializedObject.ApplyModifiedProperties();
                listView.RefreshItems();
            };

            container.Add(listView);

            return container;
        }

        private VisualElement BuildListItemTemplate()
        {
            VisualElement foldout = new VisualElement { name = "foldout" };

            VisualElement header = new VisualElement { name = "header" };
            foldout.Add(header);

            VisualElement container = new VisualElement { name = "container" };
            foldout.Add(container);

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

            return foldout;
        }

        private void SyncStateLists(int targetSize)
        {
            while (layerFoldoutStates.Count < targetSize) layerFoldoutStates.Add(false);
            while (layerGenerationStates.Count < targetSize) layerGenerationStates.Add(false);
            while (layerFoldoutStates.Count > targetSize) layerFoldoutStates.RemoveAt(layerFoldoutStates.Count - 1);
            while (layerGenerationStates.Count > targetSize) layerGenerationStates.RemoveAt(layerGenerationStates.Count - 1);
        }

        private void MoveState<T>(List<T> list, int from, int to)
        {
            if (from < 0 || from >= list.Count || to < 0 || to >= list.Count) return;

            T item = list[from];
            list.RemoveAt(from);
            list.Insert(to, item);
        }
    }
}