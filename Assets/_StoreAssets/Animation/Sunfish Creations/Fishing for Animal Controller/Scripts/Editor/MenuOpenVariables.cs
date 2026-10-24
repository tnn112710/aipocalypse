#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace SunfishCreations
{
    public class FishingVariablesWindow : EditorWindow
    {
        private SunfishCreationsVariablesController fishingVariables;
        private Vector2 scrollPosition;
        private Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();
        private List<FieldGroup> fieldGroups;

        [MenuItem("Tools/Malbers Animations/Fishing/Fishing Variables")]
        public static void ShowWindow()
        {
            FishingVariablesWindow window = GetWindow<FishingVariablesWindow>("Fishing Variables");
            window.minSize = new Vector2(400, 250);
            window.Show();
        }

        void OnEnable()
        {
            LoadFishingVariables();
            RebuildFieldGroups();
        }

        void LoadFishingVariables()
        {
            string[] guids = AssetDatabase.FindAssets("t:SunfishCreationsVariablesController");
            if (guids.Length == 0)
            {
                Debug.LogWarning("No Fishing Variables ScriptableObject found.");
                fishingVariables = null;
                return;
            }

            string targetPath = guids
                .Select(g => AssetDatabase.GUIDToAssetPath(g))
                .FirstOrDefault(p => p.Contains("Assets/_Code/Variables"))
                ?? AssetDatabase.GUIDToAssetPath(guids[0]);

            fishingVariables = AssetDatabase.LoadAssetAtPath<SunfishCreationsVariablesController>(targetPath);
        }

        void RebuildFieldGroups()
        {
            if (fishingVariables == null) return;

            fieldGroups = new List<FieldGroup>();
            string currentHeader = "General"; // fallback group name

            var fields = typeof(SunfishCreationsVariablesController)
                .GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(f => !f.IsDefined(typeof(HideInInspector), false))
                .OrderBy(f => f.MetadataToken)
                .ToList();

            foreach (var field in fields)
            {
                var headerAttr = field.GetCustomAttribute<HeaderAttribute>();
                if (headerAttr != null)
                {
                    currentHeader = headerAttr.header;
                }

                var group = fieldGroups.FirstOrDefault(g => g.Header == currentHeader);
                if (group == null)
                {
                    group = new FieldGroup { Header = currentHeader, Fields = new List<FieldInfo>() };
                    fieldGroups.Add(group);
                }

                group.Fields.Add(field);
            }
        }

        void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Fishing Variables Controller", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (fishingVariables == null)
            {
                EditorGUILayout.HelpBox("Fishing Variables ScriptableObject not found!", MessageType.Warning);
                if (GUILayout.Button("Refresh / Reload")) LoadFishingVariables();
                return;
            }

            SerializedObject serializedObject = new SerializedObject(fishingVariables);
            serializedObject.Update();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            float originalLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 240f;      

            if (fieldGroups == null) RebuildFieldGroups();

            foreach (var group in fieldGroups)
            {
                if (!foldoutStates.ContainsKey(group.Header))
                    foldoutStates[group.Header] = true;

                foldoutStates[group.Header] = EditorGUILayout.Foldout(foldoutStates[group.Header], group.Header, true, EditorStyles.foldoutHeader);

                if (foldoutStates[group.Header])
                {
                    EditorGUI.indentLevel++;
                    foreach (var field in group.Fields)
                    {
                        SerializedProperty prop = serializedObject.FindProperty(field.Name);
                        if (prop != null)
                        {
                            EditorGUILayout.PropertyField(prop, true);
                        }
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUIUtility.labelWidth = originalLabelWidth;
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select in Project", GUILayout.Height(25)))
            {
                Selection.activeObject = fishingVariables;
                EditorGUIUtility.PingObject(fishingVariables);
            }
            if (GUILayout.Button("Refresh", GUILayout.Height(25)))
            {
                LoadFishingVariables();
                RebuildFieldGroups();
            }
            EditorGUILayout.EndHorizontal();
        }

        void OnDestroy()
        {
        }

        private class FieldGroup
        {
            public string Header;
            public List<FieldInfo> Fields;
        }
    }
}
#endif