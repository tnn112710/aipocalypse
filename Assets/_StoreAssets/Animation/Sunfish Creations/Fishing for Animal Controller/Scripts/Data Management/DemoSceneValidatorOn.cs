#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
#else
using UnityEngine;
#endif

namespace SunfishCreations
{
    /// <summary>
    /// Validates demo scene settings when fish should spawn on water.
    /// </summary>
    [ExecuteAlways]
    public class DemoSceneValidatorOn : MonoBehaviour
    {
        private const bool ExpectedValue = true;

        private void OnEnable()
        {
            ValidateAtRuntime();
        }

        private void ValidateAtRuntime()
        {
            bool globalSetting = FishingGlobalSettings.SpawnFishOnWater;

            if (globalSetting != ExpectedValue)
            {
                Debug.LogError(
                    $"[DemoSceneValidatorOn] MISMATCH in scene '{gameObject.scene.name}'!\n" +
                    $"Demo expects SpawnFishOnWater: {ExpectedValue}\n" +
                    $"Global setting is: {globalSetting}\n" +
                    $"Please exit playmode and fix these values.",
                    this);

                if (Application.isPlaying && !Application.isEditor)
                {
                    Debug.Break();
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EditorApplication.QueuePlayerLoopUpdate();
        }
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(DemoSceneValidatorOn))]
    public class DemoSceneValidatorOnEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // Get current global setting value
            bool globalSetting = false;
            bool settingsAvailable = true;

            try
            {
                globalSetting = FishingGlobalSettings.SpawnFishOnWater;
            }
            catch (System.Exception)
            {
                settingsAvailable = false;
            }

            if (!settingsAvailable)
            {
                EditorGUILayout.HelpBox(
                    "⚠ FishingGlobalSettings not found. Please verify your project setup.",
                    MessageType.Warning);
                return;
            }

            const bool expectedValue = true;

            // Validation check
            if (globalSetting == expectedValue)
            {
                EditorGUILayout.HelpBox(
                    "✓ Configuration matches! Demo scene is correctly set up.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"✗ MISMATCH DETECTED!\n\n" +
                    $"Demo scene expects SpawnFishOnWater: {expectedValue}\n" +
                    $"Global PlayerPrefs setting is: {globalSetting}\n\n" +
                    $"This mismatch will cause inconsistent behavior in your demo.",
                    MessageType.Error);

                // Fix button
                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Fix Now", GUILayout.Width(120)))
                {
                    FishingGlobalSettingsWindow.ShowWindow();
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }
    }
#endif
}