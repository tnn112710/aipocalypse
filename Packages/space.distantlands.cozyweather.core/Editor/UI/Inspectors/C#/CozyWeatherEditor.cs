//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Collections.Generic;
using UnityEditor.UIElements;
using System;
using UnityEditor.Experimental.GraphView;
using System.Linq;
using System.Reflection;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif


namespace DistantLands.Cozy.EditorScripts
{
#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    [CustomEditor(typeof(CozyWeather))]
    public class CozyWeatherEditor : Editor
    {
        CozyWeather weatherSphere;

        public List<CozyModuleEditor> moduleEditors = new List<CozyModuleEditor>();
        public List<(Type type, CozyModuleAttribute attribute)> typeAttributes = new List<(Type type, CozyModuleAttribute attribute)>();
        private VisualElement root;
        VisualElement ModulePage => root.Query<VisualElement>("module-ui");
        VisualElement ModuleIconContainer => root.Query<VisualElement>("module-icon-container");
        VisualElement PreferencesContainer => root.Query<VisualElement>("preferences-container");
        VisualElement AddModuleButton => root.Query<VisualElement>("add-module");
        VisualElement BackButton => root.Query<VisualElement>("back-button");
        VisualElement ModuleIcon => root.Query<VisualElement>("module-icon");
        VisualElement StylesButton => root.Query<VisualElement>("styles");
        VisualElement DocumentationButton => root.Query<VisualElement>("documentation");
        VisualElement SettingsButton => root.Query<VisualElement>("settings");

        public delegate void UpdateDynamicIcons();
        public static event UpdateDynamicIcons updateDynamicIcons;
        public delegate void RedrawUI();
        public static event RedrawUI redrawUI;

        VisualElement BannerImage => root.Query<VisualElement>("background-image");
        VisualElement BannerIcon => root.Query<VisualElement>("banner-icon");
        Label BannerTitle => root.Query<Label>("banner-title");
        Label BannerSubtitle => root.Query<Label>("banner-subtitle");


        ToolbarSearchField searchbar => root.Q<ToolbarSearchField>();
        VisualElement SearchResults => root.Query<VisualElement>("search-results");

        List<Type> mods;
        List<ModuleWidget> CurrentWidgets;

        public static CozyWeatherEditor Instance;
        [NonSerialized]
        public List<VisualElement> dynamicIcons = new List<VisualElement>();
        public int CurrentModule
        {
            get { return EditorPrefs.GetInt("CZY_SelectedModule", -1); }
            set { EditorPrefs.SetInt("CZY_SelectedModule", value); }
        }

        /// <summary>
        /// This function is called when the object becomes enabled and active.
        /// </summary>
        void OnEnable()
        {
            if (!target)
                return;

            EditorApplication.update += UpdateIcons;
            weatherSphere = (CozyWeather)target;
            ResetModuleEditors();
            Instance = this;

        }

        /// <summary>
        /// This function is called when the behaviour becomes disabled or inactive.
        /// </summary>
        void OnDisable()
        {
            EditorApplication.update -= UpdateIcons;
        }

        public void UpdateIcons()
        {
            updateDynamicIcons?.Invoke();
        }
        public static void RedrawAllUI()
        {
            redrawUI?.Invoke();

            if (Instance)
            {
                Instance.Redraw();
            }
        }

        public void ResetModuleEditors()
        {
            moduleEditors.Clear();
            typeAttributes.Clear();

            for (int i = 0; i < weatherSphere.ModuleArray.Count(); i++)
            {
                CozyModuleEditor moduleEditor = (CozyModuleEditor)CreateEditor(weatherSphere.ModuleArray.ElementAt(i));
                if (moduleEditor == null)
                    continue;
                moduleEditor.weatherEditor = this;
                moduleEditors.Add(moduleEditor);
                updateDynamicIcons += moduleEditor.UpdateDynamicIcon;

                Type moduleType = weatherSphere.ModuleArray.ElementAt(i).GetType();
                (Type type, CozyModuleAttribute attribute) pair = new(moduleType, moduleType.GetCustomAttribute<CozyModuleAttribute>());
                typeAttributes.Add(pair);
            }

            moduleEditors.RemoveAll(x => x == null);
            typeAttributes.RemoveAll(x => x.type == null || x.attribute == null);
        }

        public override VisualElement CreateInspectorGUI()
        {
            root ??= new VisualElement();

            Redraw();

            return root;
        }

        public void Redraw()
        {
            if (root == null) return;
            root.Clear();

            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Cozy Hub/UXML/cozy-hub.uxml"
            );

            asset.CloneTree(root);
            UpdateBanner("COZY", "Stylized Weather 4", Resources.Load<Texture2D>("Promo/Distant Lands Watermark"), Resources.Load<Texture2D>("Banners/Cozy Weather"));

            ResetModuleEditors();
            RefreshModuleIcons();
            updateDynamicIcons?.Invoke();

            AddModuleButton.RegisterCallback((ClickEvent evt) => { AddModule(evt); });
            SettingsButton.RegisterCallback((ClickEvent evt) => { SettingsService.OpenProjectSettings("Project/COZY: Stylized Weather 4/Runtime"); });
            StylesButton.RegisterCallback((ClickEvent evt) => { SettingsService.OpenProjectSettings("Project/COZY: Stylized Weather 4/Features"); });
            DocumentationButton.RegisterCallback((ClickEvent evt) => { Application.OpenURL("https://distant-lands.gitbook.io/cozy-weather-4"); });
            BackButton.RegisterCallback<ClickEvent>(evt =>
            {
                OpenHomePage();
            });

            if (CurrentModule == -1 || CurrentModule > moduleEditors.Count - 1)
                OpenHomePage();
            else
                OpenModuleUI(null, CurrentModule);

            Search("");

            searchbar.RegisterCallback((ChangeEvent<string> evt) =>
            {
                Search(evt.newValue);
            });
        }

        public void RefreshModuleIcons()
        {
            ModuleIconContainer.Clear();
            PreferencesContainer.Clear();
            dynamicIcons.Clear();
            int moduleNumber = 0;
            foreach (CozyModuleEditor moduleEditor in moduleEditors)
            {
                VisualElement icon = new VisualElement();
                icon.AddToClassList("module-icon");
                icon.RegisterCallback<ClickEvent, int>(OpenModuleUI, moduleNumber);
                icon.RegisterCallback<ContextClickEvent, CozyModuleEditor>(OpenModuleContextMenu, moduleEditor);

                moduleEditor.DrawDynamicIcon(icon);

                if (moduleNumber == CurrentModule)
                    icon.AddToClassList("active");

                ModuleIconContainer.Add(icon);
                dynamicIcons.Add(icon);
                moduleNumber++;
            }

            VisualElement addModule = new VisualElement();
            addModule.AddToClassList("settings-icon");
            addModule.AddToClassList("addModule");
            Image imageIcon = new Image
            {
                image = Resources.Load<Texture2D>($"COZY Icons/Add Module"),
                name = "icon"
            };

            addModule.Add(imageIcon);
            Label label = new Label();
            label.text = "Add Module";
            addModule.Add(label);
            addModule.RegisterCallback<ClickEvent>(evt => AddModule(evt));

            PreferencesContainer.Add(addModule);

            VisualElement features = new VisualElement();
            features.AddToClassList("settings-icon");
            Image featuresIcon = new Image
            {
                image = Resources.Load<Texture2D>($"COZY Icons/Features"),
                name = "icon"
            };

            features.Add(featuresIcon);
            Label featuresLabel = new Label();
            featuresLabel.text = "Features";
            features.Add(featuresLabel);
            features.RegisterCallback<ClickEvent>(evt => SettingsService.OpenProjectSettings("Project/COZY: Stylized Weather 4/Features"));

            PreferencesContainer.Add(features);

            VisualElement integrations = new VisualElement();
            integrations.AddToClassList("settings-icon");
            Image integrationsIcon = new Image
            {
                image = Resources.Load<Texture2D>($"COZY Icons/Integrations"),
                name = "icon"
            };

            integrations.Add(integrationsIcon);
            Label integrationsLabel = new Label();
            integrationsLabel.text = "Integrations";
            integrations.Add(integrationsLabel);
            integrations.RegisterCallback<ClickEvent>(evt => SettingsService.OpenProjectSettings("Project/COZY: Stylized Weather 4/Integrations"));

            PreferencesContainer.Add(integrations);

            VisualElement setings = new VisualElement();
            setings.AddToClassList("settings-icon");
            Image setingsIcon = new Image
            {
                image = Resources.Load<Texture2D>($"COZY Icons/Settings"),
                name = "icon"
            };

            setings.Add(setingsIcon);
            Label setingsLabel = new Label();
            setingsLabel.text = "Preferences";
            setings.Add(setingsLabel);
            setings.RegisterCallback<ClickEvent>(evt => SettingsService.OpenProjectSettings("Project/COZY: Stylized Weather 4/Runtime"));

            PreferencesContainer.Add(setings);

        }

        void UpdateBanner(string title, string subtitle, Texture2D icon, Texture2D background)
        {
            BannerTitle.text = title;
            BannerSubtitle.text = subtitle;
            BannerImage.style.backgroundImage = background;
            BannerIcon.style.backgroundImage = icon;
            UpdateStatusIcon();
        }

        public void UpdateStatusIcon()
        {
            // StatusMessage status = StatusReport.Query<StatusMessage>();
            // status.UpdateStatus(CozySetupWizard.CheckStatus);
        }

        void OpenModuleUI(ClickEvent evt, int moduleNumber)
        {
            foreach (VisualElement ve in dynamicIcons)
                ve.RemoveFromClassList("active");

            BackButton.style.display = DisplayStyle.Flex;
            ModuleIcon.style.display = DisplayStyle.Flex;

            ModulePage.Clear();
            CozyModuleEditor moduleEditor = moduleEditors[moduleNumber];
            var pair = typeAttributes[moduleNumber];

            searchbar.value = "";

            // UpdateBanner(moduleEditor.ModuleTitle, moduleEditor.ModuleSubtitle, moduleEditor.ModuleIcon, moduleEditor.BannerBackground);

            ModuleIcon.style.backgroundImage = new StyleBackground(Resources.Load<Sprite>($"COZY Icons/{pair.attribute.icon}"));

            CurrentModule = moduleNumber;
            dynamicIcons[CurrentModule].AddToClassList("active");

            ModulePage.Add(moduleEditors[moduleNumber].DisplayUI());

        }

        public void OpenHomePage()
        {
            BackButton.style.display = DisplayStyle.None;
            ModuleIcon.style.display = DisplayStyle.None;

            ModulePage.Clear();

            RefreshModuleIcons();
            CurrentModule = -1;

            ModulePage.Add(PackWidgets());
        }

        public class WidgetRow
        {
            public List<ModuleWidget> widgets = new();
            public int usedUnits;

            public bool TryFit(ModuleWidget widget)
            {
                int size = (int)widget.widgetSize;

                if (usedUnits + size > 4)
                    return false;

                widgets.Add(widget);
                usedUnits += size;

                return true;
            }
        }

        public VisualElement PackWidgets()
        {
            VisualElement container = new VisualElement();
            container.AddToClassList("moduleWidgetContainer");

            if (CurrentWidgets == null)
                CurrentWidgets = new();

            CurrentWidgets.Clear();

            foreach (CozyModuleEditor cozyModuleEditor in moduleEditors)
            {
                if (cozyModuleEditor.ModuleWidgets == null)
                    continue;

                foreach (ModuleWidget widget in cozyModuleEditor.ModuleWidgets)
                {
                    if (widget.Enabled)
                        CurrentWidgets.Add(widget);
                }
            }

            // IMPORTANT
            CurrentWidgets = CurrentWidgets
                // .OrderByDescending(x => (int)x.widgetSize)
                .ToList();

            List<WidgetRow> rows = new()
    {
        new WidgetRow()
    };

            foreach (ModuleWidget widget in CurrentWidgets)
            {
                bool fit = false;

                foreach (WidgetRow row in rows)
                {
                    if (row.TryFit(widget))
                    {
                        fit = true;
                        break;
                    }
                }

                if (fit)
                    continue;

                WidgetRow newRow = new WidgetRow();

                if (!newRow.TryFit(widget))
                {
                    Debug.LogError($"Widget too large: {widget}");
                    continue;
                }

                rows.Add(newRow);
            }

            // Render
            foreach (WidgetRow row in rows)
            {
                VisualElement rowElement = new VisualElement();
                rowElement.AddToClassList("moduleWidgetRow");

                foreach (ModuleWidget widget in row.widgets)
                {
                    container.Add(widget.RenderWidget());
                }

                // container.Add(rowElement);
            }

            return container;
        }

        public void UpdateWidgets()
        {
            foreach (ModuleWidget widget in CurrentWidgets)
            {
                widget.Update();
            }
        }

        void OpenModuleContextMenu(ContextClickEvent evt, CozyModuleEditor moduleEditor)
        {
            moduleEditor.OpenContextMenu();
        }

        public void RepaintUI()
        {
            ResetModuleEditors();
            RefreshModuleIcons();
        }

        internal void ResetModuleList()
        {

            mods = CozyUtilities.ResetModuleList();

            if (mods.Contains(typeof(CozyModule)))
                mods.Remove(typeof(CozyModule));

            if (mods.Contains(typeof(CozyBiomeModule<>)))
                mods.Remove(typeof(CozyBiomeModule<>));

            foreach (ICozyModule a in weatherSphere.ActiveAndEnabledModules)
                if (mods.Contains(a.GetType()))
                    mods.Remove(a.GetType());
        }

        void AddModule(ClickEvent evt)
        {
            ResetModuleList();
            ModulesSearchProvider provider = CreateInstance<ModulesSearchProvider>();
            provider.weather = weatherSphere;
            provider.cozyWeatherEditor = this;
            SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)), provider);

        }

        void OnSceneGUI()
        {
            if (!CozyPreferences.Gizmos)
                return;


            foreach (CozyModuleEditor editor in moduleEditors)
            {
                editor.DrawGizmos();
            }
        }

        public void RemoveModule()
        {
            if (moduleEditors.Count - 1 <= CurrentModule)
                if (moduleEditors.Count - 1 > 0)
                    CurrentModule = moduleEditors.Count - 2;
                else
                    CurrentModule = -1;

            RepaintUI();

            if (CurrentModule != -1)
                OpenModuleUI(null, CurrentModule);
            else
                OpenHomePage();
        }

        #region Search
        public void Search(string keyword)
        {
            SearchResults.Clear();

            if (keyword == "")
            {
                SearchResults.style.display = DisplayStyle.None;
                return;
            }

            SearchResults.style.display = DisplayStyle.Flex;
            SearchResults.style.paddingLeft = 16;

            int totalFields = 0;

            if (CurrentModule == -1)
            {
                for (int i = 0; i < moduleEditors.Count; i++)
                {
                    CozyModule module = weatherSphere.ModuleArray[i];
                    CozyModuleEditor moduleEditor = moduleEditors[i];

                    List<PropertyField> fields = SearchModule(keyword, module);

                    if (fields.Count > 0)
                    {
                        totalFields += fields.Count;
                        Label label = new Label(typeAttributes[i].attribute.name);
                        label.AddToClassList("h1");
                        SearchResults.Add(label);

                        foreach (PropertyField field in fields)
                        {
                            SearchResults.Add(field);
                        }
                    }
                }
            }
            else
            {
                CozyModule module = weatherSphere.ModuleArray[CurrentModule];
                CozyModuleEditor moduleEditor = moduleEditors[CurrentModule];

                List<PropertyField> fields = SearchModule(keyword, module);

                totalFields += fields.Count;
                foreach (PropertyField field in fields)
                {
                    SearchResults.Add(field);
                }
            }

            if (totalFields == 0)
                SearchResults.Add(new Label("No matching results found"));



        }

        public List<PropertyField> SearchModule(string keyword, CozyModule module)
        {
            SerializedObject moduleSO = new SerializedObject(module);
            List<PropertyField> properties = new List<PropertyField>();
            moduleSO.Update();

            SerializedProperty iterator = moduleSO.GetIterator();
            iterator.NextVisible(true);
            int i = 0;
            while (true)
            {
                if (iterator == null)
                    break;

                i++;
                if (i > 15)
                    break;

                CozySearchable[] index = CozySearchUtils.GetAttributes<CozySearchable>(iterator, false);

                List<string> keywordsOnField = new List<string>();

                if (index.Length > 0 && index[0] != null)
                {
                    keywordsOnField.Add(iterator.displayName);
                    keywordsOnField.AddRange(index[0].keywords);
                }

                if (string.Join('|', keywordsOnField).Contains(keyword, StringComparison.InvariantCultureIgnoreCase))
                {
                    PropertyField field = new PropertyField();
                    field.BindProperty(iterator);
                    properties.Add(field);
                }

                if (index.Length > 0 && index[0].deepSearch)
                    if (iterator.objectReferenceValue != null)
                    {
                        if (iterator.objectReferenceValue.GetType().IsSubclassOf(typeof(CozyProfile)))
                        {
                            properties.AddRange(SearchProfile(keyword, iterator.objectReferenceValue as CozyProfile));
                        }
                    }

                if (iterator.hasChildren)
                {
                    if (!iterator.NextVisible(false))
                        break;
                    continue;
                }

                if (!iterator.NextVisible(true))
                    break;
            }

            moduleSO.ApplyModifiedProperties();

            return properties;

        }

        public List<PropertyField> SearchProfile(string keyword, CozyProfile profile)
        {
            SerializedObject moduleSO = new SerializedObject(profile);
            List<PropertyField> properties = new List<PropertyField>();
            moduleSO.Update();

            SerializedProperty iterator = moduleSO.GetIterator();
            iterator.NextVisible(true);
            int i = 0;
            while (true)
            {
                if (iterator == null)
                    break;

                i++;
                if (i > 50)
                    break;

                CozySearchable[] index = CozySearchUtils.GetAttributes<CozySearchable>(iterator, false);

                List<string> keywordsOnField = new List<string>();

                if (index.Length > 0 && index[0] != null)
                {
                    keywordsOnField.Add(iterator.displayName);
                    keywordsOnField.AddRange(index[0].keywords);
                }

                if (string.Join('|', keywordsOnField).Contains(keyword, StringComparison.InvariantCultureIgnoreCase))
                {
                    PropertyField field = new PropertyField();
                    field.BindProperty(iterator);
                    properties.Add(field);
                }

                if (iterator.hasChildren)
                {
                    if (!iterator.NextVisible(false))
                        break;
                    continue;
                }

                if (!iterator.NextVisible(true))
                    break;
            }

            moduleSO.ApplyModifiedProperties();

            return properties;

        }
        #endregion

        public void SelectModule(ICozyModule module)
        {
            for (int i = 0; i < moduleEditors.Count; i++)
            {
                if (moduleEditors[i].target == (object)module)
                {
                    CurrentModule = i;
                    OpenModuleUI(null, CurrentModule);
                    return;
                }
            }
        }

        public void SelectModule(int id)
        {
            CurrentModule = id;
            OpenModuleUI(null, CurrentModule);
        }
    }
}