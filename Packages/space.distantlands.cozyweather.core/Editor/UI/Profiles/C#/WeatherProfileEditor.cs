//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEditor.Experimental.GraphView;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(WeatherProfile))]
    public class WeatherProfileEditor : Editor
    {
        WeatherProfile profile;
        VisualElement root;

        public VisualElement ForecastingContainer => root.Q<VisualElement>("forecasting-container");
        public VisualElement LateralShiftContainer => root.Q<VisualElement>("lateral-shift-container");
        public ListView FXContainer => root.Q<ListView>("fx-list");
        public VisualElement Icon => root.Q<VisualElement>("icon");
        public Label Header => root.Q<Label>("header");

        void OnEnable()
        {

            profile = (WeatherProfile)target;

        }

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();

            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Profiles/UXML/weather-profile-editor.uxml"
            );

            asset.CloneTree(root);

            Icon.style.backgroundImage = (Texture2D)serializedObject.FindProperty("icon").objectReferenceValue;
            Header.text = profile.name;


            PropertyField description = new PropertyField();
            description.BindProperty(serializedObject.FindProperty("description"));
            ForecastingContainer.Add(description);
            PropertyField icon = new PropertyField();
            icon.BindProperty(serializedObject.FindProperty("icon"));
            icon.RegisterValueChangeCallback(evt =>
            {
                Icon.style.backgroundImage = (Texture2D)serializedObject.FindProperty("icon").objectReferenceValue;
            });
            ForecastingContainer.Add(icon);
            PropertyField tag = new PropertyField();
            tag.BindProperty(serializedObject.FindProperty("tag"));
            ForecastingContainer.Add(tag);

            PropertyField lateralShifts = new PropertyField();
            lateralShifts.name = "lateralShifts";
            lateralShifts.BindProperty(serializedObject.FindProperty("lateralShifts"));
            LateralShiftContainer.Add(lateralShifts);
            
            PropertyField chance = new PropertyField();
            chance.BindProperty(serializedObject.FindProperty("chance"));
            chance.style.marginTop = 5;
            ForecastingContainer.Add(chance);
            
            PropertyField minTime = new PropertyField();
            minTime.BindProperty(serializedObject.FindProperty("minTime"));
            ForecastingContainer.Add(minTime);
            
            PropertyField maxTime = new PropertyField();
            maxTime.BindProperty(serializedObject.FindProperty("maxTime"));
            ForecastingContainer.Add(maxTime);

            FXContainer.BindProperty(serializedObject.FindProperty("FX"));

            FXContainer.onAdd = (e) =>
            {
                var provider = CreateInstance<FXProfileSearchProvider>();
                provider.Init((val) =>
                {
                    SerializedProperty arrayProp = serializedObject.FindProperty("FX");

                    arrayProp.InsertArrayElementAtIndex(arrayProp.arraySize);
                    arrayProp.GetArrayElementAtIndex(arrayProp.arraySize - 1).objectReferenceValue = val as ScriptableObject;

                    serializedObject.ApplyModifiedProperties();
                    serializedObject.Update();
                });

                SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)), provider);

            };


            return root;
        }
    }
}