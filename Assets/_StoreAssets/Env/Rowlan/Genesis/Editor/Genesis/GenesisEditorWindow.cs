using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis
{
    public class GenesisEditorWindow : EditorWindow
    {
        private static GenesisData lastUsed;

        [MenuItem(ProjectSetup.MENU, false /*, ProjectSetup.MENU_ORDER */)]
        static void CreateWindow()
        {
            GenesisEditorWindow wnd = EditorWindow.GetWindow<GenesisEditorWindow>();
            wnd.titleContent.text = ProjectSetup.WINDOW_TITLE;

            wnd.position = new Rect(50, 50, 600, 820);
            //wnd.Close();
        }

        private GenesisEditorWindow editorWindow;

        private GenesisDataEditor genesisDataEditor;
        private GenesisData genesisData;

        public static bool helpBoxVisible = false;

        private void OnEnable()
        {
            editorWindow = this;

            //ScriptableObjectManager<GenesisData> settingsManager = new ScriptableObjectManager<GenesisData>(ProjectSetup.SETTINGS_FOLDER, ProjectSetup.SETTINGS_FILENAME);
            //genesisData = settingsManager.GetAsset();

            //genesisDataEditor = Editor.CreateEditor(genesisData) as GenesisDataEditor;
            //genesisDataEditor.OnEnable();

            // script change case: reload the current object
            Reload();
        }

        private void Reload()
        {
            if( genesisData == null)
            {
                genesisData = lastUsed;
            }

            genesisDataEditor = Editor.CreateEditor(genesisData) as GenesisDataEditor;

            if (genesisDataEditor != null)
            {
                genesisDataEditor.OnEnable();
            }

        }

        private void OnDisable()
        {
            // SaveGenesisData();

            DestroyImmediate(genesisDataEditor);

            lastUsed = genesisData;
        }

        private void SaveGenesisData()
        {
            if (genesisData == null)
                return;

            EditorUtility.SetDirty(genesisData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void OnGUI()
        {
            // header
            DrawDialogHeader();

            /// 
            /// General settings
            /// 

            GUILayout.BeginVertical("box");
            {

                EditorGUILayout.LabelField("General Settings", GUIStyles.BoxTitleStyle);

                EditorGUILayout.BeginHorizontal();

                // container
                EditorGUILayout.PrefixLabel("");

                EditorGUI.BeginChangeCheck();
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        genesisData = EditorGUILayout.ObjectField(genesisData, typeof(GenesisData), true) as GenesisData;

                        if (GUILayout.Button("New", EditorStyles.miniButton, GUILayout.Width(50)))
                        {
                            ScriptableObjectManager<GenesisData> settingsManager = new ScriptableObjectManager<GenesisData>(ProjectSetup.GENESIS_DATA_SETTINGS_DIRECTORY, ProjectSetup.SETTINGS_DATA_FILENAME, true);
                            genesisData = settingsManager.GetAsset();

                            Reload();
                        }

                    }
                    EditorGUILayout.EndHorizontal();
                }
                bool editorTargetChanged = EditorGUI.EndChangeCheck();

                if (editorTargetChanged)
                {
                    if( genesisDataEditor) 
                    {
                        SaveGenesisData();

                        DestroyImmediate(genesisDataEditor);
                    }

                    genesisDataEditor = Editor.CreateEditor(genesisData) as GenesisDataEditor;
                }

                EditorGUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            // genesis settings editor
            if (genesisData)
            {
                genesisDataEditor.OnInspectorGUI();
            }
        }

        public static void DrawDialogHeader()
        {
            // common header
            GUIUtils.DrawHeader("Genesis for MicroVerse", ref helpBoxVisible);

            // help
            if (helpBoxVisible)
            {
                GUILayout.BeginVertical(GUIStyles.HelpBoxStyle);
                {
                    EditorGUILayout.HelpBox("Genesis is a wizard for the creation in the MicroVerse ecosystem", MessageType.Info);
                }
                GUILayout.EndVertical();
            }

            EditorGUILayout.Space();

            GUILayout.BeginHorizontal(GUIStyles.AppTitleBoxStyle);
            {
                if (GUILayout.Button("ReGenesis", EditorStyles.miniButton))
                {
                    ReGenesis.ReGenesisEditorWindow.CreateWindow();
                }

                if (GUILayout.Button("Terraform", EditorStyles.miniButton))
                {
                    Terraform.TerraformEditorEditorWindow.CreateWindow();
                }

#if __MICROVERSE_ROADS__
                if (GUILayout.Button("Roads", EditorStyles.miniButton))
                {
                    Roads.RoadsEditorWindow.CreateWindow();
                }
#endif

                if (GUILayout.Button("Visual Ambience", EditorStyles.miniButton))
                {
                    Ambience.AmbienceEditorWindow.CreateWindow();
                }

                if (GUILayout.Button("Gameplay", EditorStyles.miniButton))
                {
                    Gameplay.GameplayEditorWindow.CreateWindow();

                }

                if (GUILayout.Button("Converter", EditorStyles.miniButton))
                {
                    Converter.ConverterEditorWindow.CreateWindow();
                }

                if (GUILayout.Button("Browser Tools", EditorStyles.miniButton))
                {
                    BrowserTools.BrowserToolsEditorWindow.CreateWindow();
                }

                GUILayout.FlexibleSpace();

                if( GUILayout.Button("Sync to Terrain", EditorStyles.miniButton))
                {
                    GenesisDataEditor.SyncToTerrain();
                }
            }
            GUILayout.EndHorizontal();
        }
    }
}