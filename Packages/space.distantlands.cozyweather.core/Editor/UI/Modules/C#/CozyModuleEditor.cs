//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Text;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(CozyModule))]
    public class CozyModuleEditor : Editor
    {

        public CozyWeatherEditor weatherEditor;
        [NonSerialized]
        public VisualElement icon;


        public StringBuilder sb = new StringBuilder();


        public virtual void GetDebugInformation()
        {
            if (serializedObject == null)
                return;

            serializedObject.Update();

            SerializedProperty iterator = serializedObject.GetIterator();
            iterator.NextVisible(true);

            while (true)
            {
                if (iterator == null)
                    break;

                EditorGUILayout.PropertyField(iterator, true);
                if (iterator.hasChildren)
                {
                    if (!iterator.NextVisible(false))
                        break;
                    continue;
                }

                if (!iterator.NextVisible(true))
                    break;

            }

            serializedObject.ApplyModifiedProperties();

        }

        public virtual void GetReportsInformation()
        {



        }

        public virtual void DrawDynamicIcon(VisualElement parent)
        {
            VisualElement iconContainer = new VisualElement();

            Type targetType = target.GetType();
            var moduleAttribute = targetType.GetCustomAttribute<CozyModuleAttribute>();


            Image imageIcon = new Image
            {
                image = CozyModuleAttribute.LoadIcon(moduleAttribute.icon).texture,
                name = "icon"
            };

            iconContainer.Add(imageIcon);
            Label label = new Label();
            label.text = moduleAttribute.name;
            iconContainer.Add(label);
            parent.tooltip = moduleAttribute.tooltip;
            parent.RegisterCallback<ClickEvent>(OpenModuleUI);
            parent.RegisterCallback<ContextClickEvent>((ContextClickEvent) => { OpenContextMenu(); });
            parent.Add(iconContainer);

        }

        public virtual void UpdateDynamicIcon()
        {

        }

        public virtual void RemoveModule()
        {
            CozyWeather.Instance.RemoveModule(target.GetType());
            weatherEditor.RemoveModule();
        }

        public void ResetModule()
        {
            CozyWeather.Instance.ResetModule(target.GetType());
        }

        public void EditScript()
        {
            MonoScript script = MonoScript.FromMonoBehaviour((MonoBehaviour)target);
            AssetDatabase.OpenAsset(script, 1);
        }

        public void OpenContextMenu()
        {

            GenericMenu menu = new GenericMenu();
            AddContextMenuItems(menu);
            menu.AddItem(new GUIContent("Documentation"), false, OpenDocumentationURL);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Move Up"), false, () =>
            {
                ComponentUtility.MoveComponentUp(CozyWeather.Instance.ModuleHolder.GetComponent(target.GetType()));
                CozyWeather.Instance.ResetModules();
                weatherEditor.ResetModuleEditors();
                weatherEditor.RefreshModuleIcons();
            });
            menu.AddItem(new GUIContent("Move Down"), false, () =>
            {
                ComponentUtility.MoveComponentDown(CozyWeather.Instance.ModuleHolder.GetComponent(target.GetType()));
                CozyWeather.Instance.ResetModules();
                weatherEditor.ResetModuleEditors();
                weatherEditor.RefreshModuleIcons();
            });
            menu.AddSeparator("");
            if (ModuleWidgets != null && ModuleWidgets.Length > 0)
            {
                for (int i = 0; i < ModuleWidgets.Length; i++)
                {
                    var widget = ModuleWidgets[i];
                    menu.AddItem(new GUIContent(widget.name), widget.Enabled, () =>
                    {
                        widget.Enabled = !widget.Enabled;
                        weatherEditor.OpenHomePage();
                    });
                }
                menu.AddItem(new GUIContent("Show All Widgets"), false, ShowWidgets);
                menu.AddSeparator("");
            }
            menu.AddItem(new GUIContent("Reset"), false, ResetModule);
            menu.AddItem(new GUIContent("Remove Module"), false, RemoveModule);
            menu.AddItem(new GUIContent("Edit Script"), false, EditScript);

            menu.ShowAsContext();

        }

        public virtual void AddContextMenuItems(GenericMenu menu)
        {

        }

        public virtual void OpenDocumentationURL()
        {
            Application.OpenURL("https://distant-lands.gitbook.io/");
        }

        public override void OnInspectorGUI()
        {

        }

        public virtual ModuleWidget[] ModuleWidgets => null;

        public virtual VisualElement DisplayUI()
        {
            VisualElement root = new VisualElement();


            return root;

        }

        public void OpenModuleUI(ClickEvent evt)
        {

        }

        public virtual void DrawGizmos()
        {

        }

        public void ShowWidgets()
        {
            foreach (ModuleWidget widget in ModuleWidgets)
            {
                widget.Enabled = true;
                weatherEditor.OpenHomePage();
            }
        }
    }

    /// <summary>
    /// Base class for a module widget 
    /// </summary>
    public class ModuleWidget
    {
        public string name;
        public bool Enabled
        {
            get
            {
                return EditorPrefs.GetBool($"CZY_{name}WidgetEnabled", defaultEnabledState);
            }
            set
            {
                EditorPrefs.SetBool($"CZY_{name}WidgetEnabled", value);
            }
        }
        private bool defaultEnabledState = true;
        public enum WidgetSize { Small = 1, Medium = 2, Large = 4 }
        public WidgetSize widgetSize;
        public VisualElement container;
        public ModuleWidget(WidgetSize widgetSize, string name, bool enabled, Func<VisualElement> factory, Action<ModuleWidget> updateContent)
        {
            this.widgetSize = widgetSize;
            this.name = name;
            widgetContent = factory;
            this.updateContent = updateContent;
            defaultEnabledState = enabled;
        }
        public ModuleWidget(WidgetSize widgetSize, bool enabled, Func<VisualElement> factory, Action<ModuleWidget> updateContent)
        {
            this.widgetSize = widgetSize;
            this.name = "";
            widgetContent = factory;
            this.updateContent = updateContent;
            defaultEnabledState = enabled;
        }
        public VisualElement RenderWidget()
        {
            container = new VisualElement();
            container.AddToClassList("moduleWidget");
            container.AddToClassList(widgetSize.ToString().ToLower());
            container.RegisterCallback<ContextClickEvent>(evt =>
            {
                GenericMenu menu = new();
                menu.AddItem(new("Enabled"), Enabled, () =>
                {
                    Enabled = !Enabled;
                    if (CozyWeatherEditor.Instance)
                        CozyWeatherEditor.Instance.OpenHomePage();
                });
                menu.ShowAsContext();
            });


            container.Add(widgetContent.Invoke());

            return container;
        }

        public void Update()
        {
            updateContent.Invoke(this);
        }
        Func<VisualElement> widgetContent;
        Action<ModuleWidget> updateContent;
    }
}