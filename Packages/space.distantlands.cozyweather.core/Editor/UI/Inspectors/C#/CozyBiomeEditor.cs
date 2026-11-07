//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using System;
using UnityEditor.Experimental.GraphView;
using System.Reflection;
#if SPLINES
using UnityEngine.Splines;
#endif
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace DistantLands.Cozy.EditorScripts
{

    [CustomEditor(typeof(CozyBiome))]
#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    public class CozyBiomeEditor : Editor
    {
        CozyBiome biome;
        public List<CozyBiomeModuleEditor> moduleEditors = new List<CozyBiomeModuleEditor>();
        public List<(Type type, CozyModuleAttribute attribute)> typeAttributes = new List<(Type type, CozyModuleAttribute attribute)>();
        [NonSerialized]
        public VisualElement root;
        VisualElement BiomeSettingsContainer => root.Query<VisualElement>("biome-settings");
        VisualElement ModuleIconContainer => root.Query<VisualElement>("module-icon-container");
        VisualElement ModulePage => root.Query<VisualElement>("module-ui");
        List<Type> mods;
        public static CozyWeatherEditor instance;
        [NonSerialized]
        public List<VisualElement> dynamicIcons = new List<VisualElement>();
        public int CurrentModule
        {
            get { return EditorPrefs.GetInt("CZY_SelectedBiomeModule", -1); }
            set { EditorPrefs.SetInt("CZY_SelectedBiomeModule", value); }
        }

        /// <summary>
        /// This function is called when the object becomes enabled and active.
        /// </summary>
        void OnEnable()
        {
            if (!target)
                return;

            biome = (CozyBiome)target;
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
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Inspectors/UXML/cozy-biome.uxml"
            );

            asset.CloneTree(root);

            ResetModuleEditors();
            RefreshModuleIcons();
            RefreshBiomeUI();


            if (CurrentModule == -1 || CurrentModule > moduleEditors.Count - 1)
                ShowBlankBiome();
            else
                OpenModuleUI(null, CurrentModule);
        }

        void OpenModuleContextMenu(ContextClickEvent evt, CozyBiomeModuleEditor moduleEditor)
        {
            moduleEditor.OpenContextMenu();
        }

        public void ShowBlankBiome()
        {
            ModulePage.Clear();
            ModulePage.Add(new Tooltip("Add a module to view it here."));
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
                ShowBlankBiome();
        }

        public void RefreshBiomeUI()
        {
            PropertyField biomeType = new PropertyField();
            biomeType.BindProperty(serializedObject.FindProperty("biomeType"));
            BiomeSettingsContainer.Add(biomeType);

            PropertyField priority = new PropertyField();
            priority.BindProperty(serializedObject.FindProperty("priority"));
            BiomeSettingsContainer.Add(priority);

            PropertyField targetWeight = new PropertyField();
            targetWeight.BindProperty(serializedObject.FindProperty("targetWeight"));
            targetWeight.label = "Weight";
            targetWeight.style.marginBottom = 5;
            BiomeSettingsContainer.Add(targetWeight);

#if SPLINES
            PropertyField splineRadius = new PropertyField();
            splineRadius.BindProperty(serializedObject.FindProperty("splineRadius"));
            if (biome.Splines.Count > 0)
                BiomeSettingsContainer.Add(splineRadius);
#endif

            PropertyField smoothingRadius = new PropertyField();
            biomeType.RegisterValueChangeCallback(evt =>
            {
                smoothingRadius.style.display = serializedObject.FindProperty("biomeType").enumValueIndex == (int)BiomeType.Local ? DisplayStyle.Flex : DisplayStyle.None;
            });
            smoothingRadius.BindProperty(serializedObject.FindProperty("smoothingRadius"));
            BiomeSettingsContainer.Add(smoothingRadius);

            Tooltip tooltip = new Tooltip("No colliders found! Please add a collider or spline to this biome", StatusType.Warning);
            tooltip.style.display = serializedObject.FindProperty("biomeType").enumValueIndex == (int)BiomeType.Local && biome.ColliderCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            BiomeSettingsContainer.Add(tooltip);
            tooltip.schedule.Execute(() =>
            {
                if (tooltip == null || tooltip.style == null || serializedObject == null || serializedObject.FindProperty("biomeType") == null) return;
                tooltip.style.display = serializedObject.FindProperty("biomeType").enumValueIndex == (int)BiomeType.Local && biome.ColliderCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }).Every(16);
        }

        public void RefreshModuleIcons()
        {
            ModuleIconContainer.Clear();
            dynamicIcons.Clear();
            int moduleNumber = 0;
            foreach (CozyBiomeModuleEditor moduleEditor in moduleEditors)
            {
                VisualElement icon = new VisualElement();
                icon.AddToClassList("module-icon");
                icon.RegisterCallback<ClickEvent, int>(OpenModuleUI, moduleNumber);
                icon.RegisterCallback<ContextClickEvent, CozyBiomeModuleEditor>(OpenModuleContextMenu, moduleEditor);

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

            ModuleIconContainer.Add(addModule);

        }

        public void ResetModuleEditors()
        {
            moduleEditors.Clear();
            typeAttributes.Clear();

            for (int i = 0; i < biome.ModuleArray.Count(); i++)
            {
                CozyBiomeModuleEditor moduleEditor = (CozyBiomeModuleEditor)CreateEditor(biome.ModuleArray.ElementAt(i));
                if (moduleEditor == null)
                    continue;

                moduleEditors.Add(moduleEditor);
                moduleEditor.biomeEditor = this;

                Type moduleType = biome.ModuleArray.ElementAt(i).GetType();
                (Type type, CozyModuleAttribute attribute) pair = new(moduleType, moduleType.GetCustomAttribute<CozyModuleAttribute>());
                typeAttributes.Add(pair);
            }

            moduleEditors.RemoveAll(x => x == null);
            typeAttributes.RemoveAll(x => x.type == null || x.attribute == null);
        }

        void AddModule(ClickEvent evt)
        {
            ResetModuleList();
            BiomeModulesSearchProvider provider = CreateInstance<BiomeModulesSearchProvider>();
            provider.biome = biome;
            provider.editor = this;
            SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)), provider);

        }

        internal void ResetModuleList()
        {

            mods = CozyUtilities.ResetBiomeModulesList();

            if (mods.Contains(typeof(CozyBiomeModule<>)))
                mods.Remove(typeof(CozyBiomeModule<>));

            foreach (ICozyModule a in biome.ActiveAndEnabledModules)
                if (mods.Contains(a.GetType()))
                    mods.Remove(a.GetType());
        }

        void OpenModuleUI(ClickEvent evt, int moduleNumber)
        {
            foreach (VisualElement ve in dynamicIcons)
                ve.RemoveFromClassList("active");

            ModulePage.Clear();
            CozyBiomeModuleEditor moduleEditor = moduleEditors[moduleNumber];
            var pair = typeAttributes[moduleNumber];

            CurrentModule = moduleNumber;
            dynamicIcons[CurrentModule].AddToClassList("active");

            ModulePage.Add(moduleEditors[moduleNumber].DisplayBiomeUI());

        }

        public void RepaintUI()
        {
            ResetModuleEditors();
            RefreshModuleIcons();
        }

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

    }
}