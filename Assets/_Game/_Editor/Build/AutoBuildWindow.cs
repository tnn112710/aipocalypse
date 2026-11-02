using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aipocalypse.Editor.Build
{
    // Lists every BuildConfig asset with its build name and a Build button.
    public sealed class AutoBuildWindow : EditorWindow
    {
        const string ConfigFolder = "Assets/_Game/Data/Build";

        readonly List<BuildConfig> _configs = new();
        Vector2 _scroll;

        [MenuItem("Autobuild/Build Window", priority = 0)]
        static void Open() => GetWindow<AutoBuildWindow>("Autobuild");

        void OnEnable() => Refresh();
        void OnFocus() => Refresh();
        void OnProjectChange() => Refresh();

        void Refresh()
        {
            _configs.Clear();
            _configs.AddRange(AssetDatabase.FindAssets("t:" + nameof(BuildConfig))
                .Select(guid => AssetDatabase.LoadAssetAtPath<BuildConfig>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(config => config != null)
                .OrderBy(config => config.name));
            Repaint();
        }

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("New Config", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    CreateConfig();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open Builds Folder", EditorStyles.toolbarButton))
                    EditorApplication.ExecuteMenuItem("Autobuild/Open Builds Folder");
            }

            if (_configs.Count == 0)
            {
                EditorGUILayout.HelpBox($"No BuildConfig asset yet: New Config makes one in {ConfigFolder}.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (BuildConfig config in _configs)
            {
                if (config == null)
                    continue;
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(config.name), EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(AutoBuild.GetBuildName(config), EditorStyles.miniLabel);
                    }
                    if (GUILayout.Button("Edit", GUILayout.Width(50), GUILayout.Height(32)))
                        Selection.activeObject = config;
                    if (GUILayout.Button("Build", GUILayout.Width(70), GUILayout.Height(32)))
                        AutoBuild.BuildLater(config);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void CreateConfig()
        {
            if (!AssetDatabase.IsValidFolder(ConfigFolder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(ConfigFolder).Replace('\\', '/'), Path.GetFileName(ConfigFolder));
            var config = CreateInstance<BuildConfig>();
            AssetDatabase.CreateAsset(config, AssetDatabase.GenerateUniqueAssetPath($"{ConfigFolder}/BuildConfig.asset"));
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            Refresh();
        }
    }
}
