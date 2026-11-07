//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(ForecastProfile))]
#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    public class ForecastProfileEditor : Editor
    {

        ForecastProfile forecastProfile;
        VisualElement root;

        public VisualElement ChancesByVariableChart => root.Q<VisualElement>("chances-by-variable-chart");
        public VisualElement ChancesByVariableKey => root.Q<VisualElement>("chances-by-variable-key");
        public EnumField ChancesByVariableLimit => root.Q<EnumField>("chances-by-variable-limit");
        public EnumField StartWith => root.Q<EnumField>("start-with");
        public ObjectField InitialWeather => root.Q<ObjectField>("initial-weather");
        public ListView InitialForecast => root.Q<ListView>("initial-forecast");

        static Gradient WeatherKeyColors = new Gradient()
        {
            colorKeys = new GradientColorKey[8] {
                new GradientColorKey(Branding.deepBlue,0f/7f),
                new GradientColorKey(Branding.red,1f/7f),
                new GradientColorKey(Branding.yellow,2f/7f),
                new GradientColorKey(Branding.green,3f/7f),
                new GradientColorKey(Branding.purple,4f/7f),
                new GradientColorKey(Branding.blue,5f/7f),
                new GradientColorKey(Branding.orange,6f/7f),
                new GradientColorKey(Branding.deepBlue,1f)
            }
        };
        void OnEnable()
        {
            forecastProfile = (ForecastProfile)target;

        }

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();

            StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Globals.uss"
            );
            root.styleSheets.Add(style);

            Tooltip tooltip = new Tooltip("Add profiles, manage transitions, and control forecast rules");
            root.Add(tooltip);

            Button button = new Button();
            button.text = "Edit Forecast";
            button.clicked += () =>
            {
                CozyForecastWindow.OpenWindow(forecastProfile);
            };
            root.Add(button);

            return root;
        }
    }
}