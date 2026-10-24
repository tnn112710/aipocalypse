#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace SunfishCreations
{
    public class FishingGlobalSettingsWindow : EditorWindow
    {
        [MenuItem("Tools/Malbers Animations/Fishing/Global Settings")]
        public static void ShowWindow()
        {
            var window = GetWindow<FishingGlobalSettingsWindow>("Fishing Global Settings");
            window.minSize = new Vector2(320, 120);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Fishing Runtime Toggles", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Spawn Fish On Water Toggle
            bool currentValue = FishingGlobalSettings.SpawnFishOnWater;
            string toggleLabel = $"Spawn Fish On Water: {(currentValue ? "On" : "Off")}";

            bool newValue = EditorGUILayout.Toggle(toggleLabel, currentValue);
            if (newValue != currentValue)
            {
                FishingGlobalSettings.SpawnFishOnWater = newValue;
            }

            // Reset button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset to Default", GUILayout.Width(150)))
            {
                FishingGlobalSettings.ResetSpawnFishOnWater();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Info box
            EditorGUILayout.HelpBox(                "This setting persists across editor sessions and builds via PlayerPrefs.",
                MessageType.Info);
        }
    }
}
#endif