using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis
{
    [CustomEditor(typeof(GenesisData))]

    public class GenesisDataEditor : Editor
    {
        private GenesisDataEditor editor;
        public GenesisData editorTarget;

        private int selectedToolbarTabIndex = 0;

        private enum ToolbarTab
        {
            Terrain = 0,
            Stamps = 1,
            Layers = 2,
            Paths = 3,
            Trees = 4,
        }

        private GUIContent[] toolbarButtons;

        private IGenesisModule terrainModule;
        private IGenesisModule stampModule;
        private IGenesisModule layerModule;
        private IGenesisModule pathModule;
        private IGenesisModule treeModule;

        private SerializedProperty replaceExisting;
        private SerializedProperty confirmationPopup;

        public void OnEnable()
        {
            editor = this;
            editorTarget = target as GenesisData;

            toolbarButtons = new GUIContent[]
            {
                new GUIContent( "Terrain", "Terrain Settings"),
                new GUIContent( "Stamps", "Stamp Settings"),
                new GUIContent( "Layers", "Layer Settings"),
                new GUIContent( "Paths", "Path Settings"),
                // new GUIContent( "Trees", "Tree Settings"), // TODO: activate later when more features are implemented
            };

            terrainModule = new TerrainModule( this);
            stampModule = new StampModule(this);
            layerModule = new LayerModule(this);
            pathModule = new PathModule(this);
            // treeModule = new TreeModule(this);

            replaceExisting = serializedObject.FindProperty("replaceExisting");
            confirmationPopup = serializedObject.FindProperty("confirmationPopup");
        }

        public override void OnInspectorGUI()
        {
            // special case: script modification / reload while developing
            // added this to console errors; not required in released asset
            if (serializedObject.targetObject == null)
                return; 

            serializedObject.Update();

            // common
            EditorGUILayout.HelpBox("The existing MicroVerse scene object and terrain data will be deleted and replaced when you create a MicroVerse, Terrain (including Terrain Data), Height Stamps, Texture Stamps, etc.\nThis operation can't be undone! Please confirm to continue.", MessageType.Warning);
            EditorGUILayout.PropertyField(replaceExisting);

            EditorGUILayout.PropertyField(confirmationPopup);
            
            GUI.enabled = editorTarget.replaceExisting;  
            DrawGenesisEditorGUI();
            GUI.enabled = true;

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGenesisEditorGUI()
        {
            EditorGUILayout.Space();

            // toolbar
            selectedToolbarTabIndex = GUILayout.Toolbar(selectedToolbarTabIndex, toolbarButtons);

            switch (selectedToolbarTabIndex)
            {
                case (int)ToolbarTab.Terrain:
                    terrainModule?.OnInspectorGUI();
                    break;

                case (int)ToolbarTab.Stamps:
                    stampModule?.OnInspectorGUI();
                    break;

                case (int)ToolbarTab.Layers:
                    layerModule?.OnInspectorGUI();
                    break;

                case (int)ToolbarTab.Paths:
                    pathModule?.OnInspectorGUI();
                    break;

                case (int)ToolbarTab.Trees:
                    treeModule?.OnInspectorGUI();
                    break;
            }

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            {
                float buttonHeight = 40f;
                bool createTerrainEnabled = JBooth.MicroVerseCore.MicroVerse.instance != null;

                EditorGUI.BeginDisabledGroup(!createTerrainEnabled);
                {
                    switch (selectedToolbarTabIndex)
                    {
                        case (int)ToolbarTab.Terrain:

                            if (GUILayout.Button("Create Terrain", GUILayout.Height(buttonHeight)))
                            {
                                if (CheckConfirmation())
                                {
                                    CreateTerrain();
                                }
                            }
                            break;

                        case (int)ToolbarTab.Stamps:
                            if (GUILayout.Button("Create Height Stamps", GUILayout.Height(buttonHeight)))
                            {
                                if (CheckConfirmation())
                                {
                                    CreateHeightStamps();
                                }
                            }
                            break;

                        case (int)ToolbarTab.Layers:
                            if (GUILayout.Button("Create Texture Stamps", GUILayout.Height(buttonHeight)))
                            {
                                if (CheckConfirmation())
                                {
                                    CreateTextureStamps();
                                }
                            }
                            break;

                        case (int)ToolbarTab.Paths:
                            if (GUILayout.Button("Create Paths", GUILayout.Height(buttonHeight)))
                            {
                                if (CheckConfirmation())
                                {
                                    CreatePaths();
                                }
                            }
                            break;

                        case (int)ToolbarTab.Trees:
                            if (GUILayout.Button("Create TODO", GUILayout.Height(buttonHeight)))
                            {
                                if (CheckConfirmation())
                                {
                                    CreateTerrain();
                                }
                            }
                            break;
                    }
                }
                EditorGUI.EndDisabledGroup();

                if (GUILayout.Button("Create MicroVerse", GUILayout.Height(buttonHeight)))
                {
                    if (CheckConfirmation())
                    {
                        CreateMicroVerse();
                    }
                }
            }
            GUILayout.EndHorizontal();
        }

        private bool CheckConfirmation()
        {
            // no popup requested => proceed
            if (editorTarget.confirmationPopup)
            {
                bool dialogResult = EditorUtility.DisplayDialog("MicroVerse Change", "Are you sure you want to perform the selected operation? The operation is unrecoverable!", "Ok", "Cancel");
                return dialogResult;
            }
            
            return true;
        }

        /// <summary>
        /// Remove the MicroVerse gameobject from the scene, delete terrain data
        /// </summary>
        private void DestroyMicroVerse()
        {
            JBooth.MicroVerseCore.MicroVerse microVerse = JBooth.MicroVerseCore.MicroVerse.instance;

            if (!microVerse)
                return;

            terrainModule?.Destroy();
            stampModule?.Destroy();
            layerModule?.Destroy();
            pathModule?.Destroy();
            treeModule?.Destroy();

            Object.DestroyImmediate(microVerse.gameObject);
        }

        private void CreateMicroVerse()
        {
            if (editorTarget.replaceExisting)
            {
                DestroyMicroVerse();
            }

            // create gameobject
            GameObject microVerseContainer = CreateMicroVerseContainer();

            // add modules
            terrainModule?.Create(microVerseContainer);
            stampModule?.Create(microVerseContainer);
            layerModule?.Create(microVerseContainer);
            pathModule?.Create(microVerseContainer);
            treeModule?.Create(microVerseContainer);

            // register the creation in the undo system
            // Undo.RegisterCreatedObjectUndo(microVerseContainer, "Create " + microVerseContainer.name);

            // select the new object
            Selection.activeObject = microVerseContainer;

            // refresh
            MicroVerse.instance.Invalidate();
        }

        public void CreateTerrain()
        {
            terrainModule.Destroy();
            terrainModule.Create(JBooth.MicroVerseCore.MicroVerse.instance.gameObject);

            // refresh
            MicroVerse.instance.Invalidate();

        }

        public void CreateHeightStamps()
        {
            stampModule?.Destroy();
            stampModule?.Create(MicroVerse.instance.transform.gameObject);

            // refresh
            MicroVerse.instance.Invalidate();
        }

        public void CreateTextureStamps()
        {
            layerModule?.Destroy();
            layerModule?.Create(MicroVerse.instance.transform.gameObject);

            // refresh
            MicroVerse.instance.Invalidate();
        }

        public void CreatePaths()
        {
            pathModule?.Destroy();
            pathModule?.Create(MicroVerse.instance.transform.gameObject);

            // refresh
            MicroVerse.instance.Invalidate();
        }

        private GameObject CreateMicroVerseContainer()
        {
            // create gameobject
            GameObject microVerse = new GameObject("MicroVerse");

            // add the prefabpainter script
            microVerse.AddComponent<MicroVerse>();

            return microVerse;
        }

        public static void SyncToTerrain()
        {
            MicroVerse.instance?.Invalidate();
            MicroVerse.instance?.SaveBackToTerrain();
        }
    }
}