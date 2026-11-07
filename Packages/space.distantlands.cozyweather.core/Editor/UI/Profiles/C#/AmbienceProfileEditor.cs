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
    [CustomEditor(typeof(AmbienceProfile))]
    public class AmbienceProfileEditor : Editor
    {


        AmbienceProfile profile;
        VisualElement root;

        public VisualElement ForecastingContainer => root.Q<VisualElement>("forecasting-container");
        public ListView FXContainer => root.Q<ListView>("fx-list");

        void OnEnable()
        {

            profile = (AmbienceProfile)target;

        }

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();

            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Profiles/UXML/ambience-profile-editor.uxml"
            );

            asset.CloneTree(root);

            // PropertyField chance = new PropertyField();
            // WRCField chance = 
            // chance.BindProperty(serializedObject.FindProperty("chance"));
            PropertyField chance = new PropertyField();
            chance.BindProperty(serializedObject.FindProperty("chance"));
            ForecastingContainer.Add(chance);

            PropertyField minTime = new PropertyField();
            minTime.BindProperty(serializedObject.FindProperty("minTime"));
            ForecastingContainer.Add(minTime);
            PropertyField maxTime = new PropertyField();
            maxTime.BindProperty(serializedObject.FindProperty("maxTime"));
            ForecastingContainer.Add(maxTime);
            PropertyField dontPlayDuring = new PropertyField();
            dontPlayDuring.BindProperty(serializedObject.FindProperty("dontPlayDuring"));
            ForecastingContainer.Add(dontPlayDuring);

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