using UnityEngine;
using UnityEditor;

namespace GlobalSnowEffect {

    [CustomEditor(typeof(GlobalSnowFootsteps))]
    public class GlobalSnowFootstepsEditor : Editor {

        static readonly GUIContent feetLabel = new GUIContent("Feet", "Feet transforms (even indices = left feet, odd indices = right feet). If empty, humanoid feet are auto-detected from the Animator.");
        static readonly GUIContent toeOffsetLabel = new GUIContent("Toe Offset (m)", "Offset from the foot bone toward the toes; centers the footprint on the actual contact area.");
        static readonly GUIContent plantHeightLabel = new GUIContent("Plant Height (m)", "Foot height over the character base below which the foot counts as planted.");
        static readonly GUIContent maxPlantSpeedLabel = new GUIContent("Max Plant Speed", "Maximum world speed in m/s of the foot bone to count as planted. Scales automatically with the character speed.");
        static readonly GUIContent replantDistanceLabel = new GUIContent("Replant Distance (m)", "Horizontal distance the foot must move away from its last footprint to leave a new one (covers small shuffling steps).");
        static readonly GUIContent debugLogLabel = new GUIContent("Debug Log", "Log plant events and per-foot height/speed to the console.");

        SerializedProperty feet, plantHeight, maxPlantSpeed, toeOffset, replantDistance, debugLog;

        static bool advancedExpanded;

        void OnEnable() {
            feet = serializedObject.FindProperty("feet");
            plantHeight = serializedObject.FindProperty("plantHeight");
            maxPlantSpeed = serializedObject.FindProperty("maxPlantSpeed");
            toeOffset = serializedObject.FindProperty("toeOffset");
            replantDistance = serializedObject.FindProperty("replantDistance");
            debugLog = serializedObject.FindProperty("debugLog");
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();

            EditorGUILayout.PropertyField(feet, feetLabel, true);
            EditorGUILayout.PropertyField(toeOffset, toeOffsetLabel);
            advancedExpanded = EditorGUILayout.Foldout(advancedExpanded, "Advanced", true);
            if (advancedExpanded) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(plantHeight, plantHeightLabel);
                EditorGUILayout.PropertyField(maxPlantSpeed, maxPlantSpeedLabel);
                EditorGUILayout.PropertyField(replantDistance, replantDistanceLabel);
                EditorGUILayout.PropertyField(debugLog, debugLogLabel);
                EditorGUI.indentLevel--;
            }

            plantHeight.floatValue = Mathf.Clamp(plantHeight.floatValue, 0.01f, 1f);
            maxPlantSpeed.floatValue = Mathf.Clamp(maxPlantSpeed.floatValue, 0.01f, 10f);
            toeOffset.floatValue = Mathf.Clamp(toeOffset.floatValue, -0.5f, 0.5f);
            replantDistance.floatValue = Mathf.Clamp(replantDistance.floatValue, 0.05f, 2f);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
