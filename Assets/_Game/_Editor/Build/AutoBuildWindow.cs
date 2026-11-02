using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aipocalypse.Editor.Build
{
    // Every BuildConfig asset: tick some and Build Selected, or Build one; each row shows its build name and last build,
    // with buttons to open its folder or run it.
    public sealed class AutoBuildWindow : EditorWindow
    {
        const string ConfigFolder = "Assets/_Game/Data/Build";
        const string SelectionKey = "Aipocalypse.AutoBuild.Selected";

        readonly List<BuildConfig> _configs = new();
        readonly HashSet<string> _selected = new();
        Vector2 _scroll;
        GUIStyle _status;

        [MenuItem("Autobuild/Build Window", priority = 0)]
        static void Open() => GetWindow<AutoBuildWindow>("Autobuild");

        void OnEnable()
        {
            _selected.UnionWith(SessionState.GetString(SelectionKey, string.Empty).Split(';').Where(g => g.Length > 0));
            Refresh();
        }

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
            _status ??= new GUIStyle(EditorStyles.miniLabel) { richText = true };
            BuildConfig[] selected = _configs.Where(c => _selected.Contains(Guid(c))).ToArray();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("New Config", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    CreateConfig();
                using (new EditorGUI.DisabledScope(selected.Length == 0))
                {
                    if (GUILayout.Button($"Build Selected ({selected.Length})", EditorStyles.toolbarButton, GUILayout.Width(130)))
                        AutoBuild.BuildLater(selected);
                }
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
                if (config != null)
                    DrawRow(config);
            EditorGUILayout.EndScrollView();
        }

        void DrawRow(BuildConfig config)
        {
            AutoBuildHistory.Entry last = AutoBuildHistory.Get(config);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                string guid = Guid(config);
                bool ticked = EditorGUILayout.Toggle(_selected.Contains(guid), GUILayout.Width(16), GUILayout.Height(44));
                if (ticked != _selected.Contains(guid))
                    Select(guid, ticked);
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(config.name), EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(AutoBuild.GetBuildName(config), EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(Status(last), _status);
                }
                if (GUILayout.Button("Edit", GUILayout.Width(44), GUILayout.Height(44)))
                    Selection.activeObject = config;
                using (new EditorGUI.DisabledScope(last == null || !Directory.Exists(last.Folder)))
                {
                    if (GUILayout.Button("Open", GUILayout.Width(48), GUILayout.Height(44)))
                        EditorUtility.OpenWithDefaultApp(last.Folder);
                }
                using (new EditorGUI.DisabledScope(last == null || !File.Exists(last.Executable)))
                {
                    if (GUILayout.Button("Run", GUILayout.Width(44), GUILayout.Height(44)))
                        Run(last);
                }
                if (GUILayout.Button("Build", GUILayout.Width(60), GUILayout.Height(44)))
                    AutoBuild.BuildLater(config);
            }
        }

        static string Status(AutoBuildHistory.Entry last)
        {
            if (last == null)
                return "<color=#888888>Not built on this machine yet</color>";
            if (!last.Succeeded)
                return $"<color=#E06C6C>{last.Result} ({last.Errors} errors) · {last.Finished}</color>";
            string size = last.Size >= 1L << 30 ? $"{last.Size / (double)(1L << 30):0.0} GB" : $"{last.Size >> 20} MB";
            string time = $"{(int)(last.Seconds / 60)}:{(int)(last.Seconds % 60):00}";
            string zip = last.Zip != null ? " · zipped" : string.Empty;
            return $"<color=#7EC27E>Built {last.Finished} · {time} · {size}{zip}</color>";
        }

        static void Run(AutoBuildHistory.Entry last)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(last.Executable)
            {
                WorkingDirectory = Path.GetDirectoryName(last.Executable),
                UseShellExecute = true,
            });
        }

        void Select(string guid, bool on)
        {
            if (on)
                _selected.Add(guid);
            else
                _selected.Remove(guid);
            SessionState.SetString(SelectionKey, string.Join(";", _selected));
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

        static string Guid(BuildConfig config) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(config));
    }
}
