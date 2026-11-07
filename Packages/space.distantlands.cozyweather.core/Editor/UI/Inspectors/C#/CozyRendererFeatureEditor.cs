//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEditor;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(CozyRendererFeature))]
    public class CozyRendererFeatureEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(new GUIContent("Preferences", EditorGUIUtility.IconContent("d__Popup@2x").image)))
            {
                SettingsService.OpenProjectSettings("Project/COZY: Stylized Weather 4/Runtime");
            }

            if (GUILayout.Button(new GUIContent("Documentation", EditorGUIUtility.IconContent("d__Help@2x").image)))
            {
                Application.OpenURL("");
            }

            EditorGUILayout.EndHorizontal();

            if (!CozyWeather.Active)
            {
                EditorGUILayout.HelpBox("COZY is not currently setup in this scene! Rendering automatically disabled.", MessageType.Warning);
            }
        }
    }
}