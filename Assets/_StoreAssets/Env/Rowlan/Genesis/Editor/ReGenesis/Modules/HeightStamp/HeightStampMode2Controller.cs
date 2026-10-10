using JBooth.MicroVerseCore;
using JBooth.MicroVerseCore.Browser;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static JBooth.MicroVerseCore.Browser.Package;

namespace Rowlan.Genesis.ReGenesis
{
    public class HeightStampMode2Controller
    {
        private ReGenesisModule editor;

        private SerializedProperty heightMapSource;
        private SerializedProperty path;
        private SerializedProperty scaleRangeWidth;
        private SerializedProperty scaleRangeHeight;

        private ContentBrowserManager contentBrowserManager;

        public HeightStampMode2Controller(ReGenesisModule editor)
        {
            this.editor = editor;

            SerializedProperty settings = editor.GetSerializedObject().FindProperty("heightStampSettings");

            heightMapSource = settings.FindPropertyRelative("heightMapSource");
            path = settings.FindPropertyRelative("path");
            scaleRangeWidth = settings.FindPropertyRelative("scaleRangeWidth");
            scaleRangeHeight = settings.FindPropertyRelative("scaleRangeHeight");

            // content browser
            contentBrowserManager = new ContentBrowserManager();
            contentBrowserManager.LoadHeightContentCollections( true);

        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical("box");
            {

                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("Heightmap", GUIStyles.BoxTitleStyle);

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Reset", EditorStyles.miniButton))
                    {
                        editor.GetEditorTarget().Reset();
                    }

                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(heightMapSource);

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Heightmap Source", GUIStyles.BoxTitleStyle);

                // convert source to enum
                ReGenesisData.HeightStampSettings.HeightMapSource source = (ReGenesisData.HeightStampSettings.HeightMapSource)heightMapSource.intValue;

                if (source == ReGenesisData.HeightStampSettings.HeightMapSource.Path)
                {
                    #region Path

                    if (ReGenesisEditorWindow.helpBoxVisible)
                    {
                        EditorGUILayout.HelpBox("Specify path which contains heightmaps. The path will be scanned for textures which can be applied.", MessageType.None);
                    }

                    EditorGUILayout.BeginHorizontal();
                    {
                        float prev = EditorGUIUtility.labelWidth;
                        EditorGUIUtility.labelWidth = 60f;
                        {
                            EditorGUILayout.PropertyField(path);
                        }
                        EditorGUIUtility.labelWidth = prev;

                        if (GUILayout.Button("Select", GUILayout.Width(60)))
                        {
                            string selectedPath = GUIUtils.SelectAssetPath("Select Heightmap Path");
                            if (selectedPath != null)
                            {
                                path.stringValue = selectedPath;
                            }
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    #endregion Path

                }
                else if (source == ReGenesisData.HeightStampSettings.HeightMapSource.ContentBrowser)
                {
                    #region ContentBrowser

                    EditorGUILayout.BeginHorizontal();
                    {
                        EditorGUILayout.PrefixLabel("Packages");
                        
                        GUILayout.FlexibleSpace();

                        if( GUILayout.Button( "All", EditorStyles.miniButton))
                        {
                            contentBrowserManager.SetAllPackageSelections(true);
                        }

                        if (GUILayout.Button("None", EditorStyles.miniButton))
                        {
                            contentBrowserManager.SetAllPackageSelections(false);
                        }

                        if (GUILayout.Button("Reload", EditorStyles.miniButton))
                        {
                            contentBrowserManager.LoadHeightContentCollections(true);
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUI.indentLevel++;

                    foreach (var package in contentBrowserManager.GetFilteredCollectionPackages())
                    {
                        package.selected = EditorGUILayout.Toggle(package.packName, package.selected);
                    }

                    EditorGUI.indentLevel--;

                    #endregion ContentBrowser
                }
                else
                {
                    Debug.LogError("Unsupported heightmap source: " + source);
                }

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Settings", GUIStyles.BoxTitleStyle);

                EditorGUILayout.PropertyField(scaleRangeWidth);
                EditorGUILayout.PropertyField(scaleRangeHeight);

                EditorGUILayout.Space();

                if (ReGenesisEditorWindow.helpBoxVisible)
                {
                    EditorGUILayout.HelpBox("Assign a random heightmap to the height stamps of one or multiple selected gameobjects.", MessageType.None);
                }

                EditorGUILayout.BeginHorizontal();
                {
                    bool enabled = source == ReGenesisData.HeightStampSettings.HeightMapSource.Path
                        || (source == ReGenesisData.HeightStampSettings.HeightMapSource.ContentBrowser && contentBrowserManager.IsAnyPackageSelected());
                    bool prevEnabled = GUI.enabled;
                    GUI.enabled = enabled;
                    {
                        if (GUILayout.Button("Create New Height Stamp", GUILayout.Height(30)))
                        {
                            CreateNewHeightStamp();
                        }

                        if (GUILayout.Button("Assign Random Heightmap", GUILayout.Height(30)))
                        {
                            HeightStamp[] heightStamps = editor.GetSelectedHeightStamps();

                            Undo.RegisterCompleteObjectUndo(heightStamps, "Assign Random Heightmap");

                            RandomizeHeightmaps(heightStamps);
                        }
                    }
                    GUI.enabled = prevEnabled;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private void RandomizeHeightmaps(HeightStamp[] heightStamps)
        {
            foreach( HeightStamp heightStamp in heightStamps)
            {
                RandomizeHeightmap(heightStamp);
            }
        }

        private void RandomizeHeightmap( HeightStamp heightStamp)
        {
            Texture2D texture = GetRandomHeightMap();

            if (texture == null)
                return;

            heightStamp.stamp = texture;

            MicroVerse.instance.Invalidate();

        }

        private Texture2D GetRandomHeightMap()
        {
            Texture2D texture = null;

            ReGenesisData.HeightStampSettings.HeightMapSource source = (ReGenesisData.HeightStampSettings.HeightMapSource)heightMapSource.intValue;

            if (source == ReGenesisData.HeightStampSettings.HeightMapSource.Path)
            {
                texture = GetRandomHeightMapFromPath();
            }
            else if (source == ReGenesisData.HeightStampSettings.HeightMapSource.ContentBrowser)
            {
                texture = contentBrowserManager.GetRandomHeightMapFromContentCollection();
            }

            return texture;
        }

        private Texture2D GetRandomHeightMapFromPath()
        {
            string pathText = path.stringValue;

            if (string.IsNullOrEmpty(pathText))
                return null;

            string[] guids = AssetDatabase.FindAssets("t:texture2D", new[] { pathText });
            if (guids.Length == 0)
            {
                Debug.Log("No matching assets found at " + pathText);
                return null;
            }

            int index = Random.Range(0, guids.Length);
            string guid = guids[index];

            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

            return texture;
        }

        private void CreateNewHeightStamp()
        {
            Texture2D texture = GetRandomHeightMap();

            if (texture == null)
            {
                Debug.LogError("No matching texture found");
                return;
            }

            GameObject parent = MicroVerse.instance.gameObject;

            // stamp gameobject
            string name = "Height Stamp";
            GameObject stampGO = new GameObject(name);
            stampGO.transform.parent = parent.transform;

            GameObjectUtility.EnsureUniqueNameForSibling(stampGO);

            // stamp settings
            HeightStamp stamp = stampGO.AddComponent<HeightStamp>();

            stamp.stamp = texture;
            stamp.mode = HeightStamp.CombineMode.Add;

            stamp.falloff = new FalloffFilter();
            stamp.falloff.filterType = FalloffFilter.FilterType.Range;
            stamp.falloff.falloffRange = new Vector2(0.2f, 1f);

            // position (will be randomized later)
            Vector3 position = Vector3.zero;

            // rotation (will be randomized later)
            Quaternion rotation = Quaternion.identity;

            // scale
            Bounds worldBounds = MicroVerse.instance.GetTerrainWorldBounds();
            float totalWidth = worldBounds.size.x;
            float totalHeight = worldBounds.size.y;
            float totalLength = worldBounds.size.z;

            float xScaleRandom = Random.Range(scaleRangeWidth.vector2Value.x, scaleRangeWidth.vector2Value.y);
            float yScaleRandom = Random.Range(scaleRangeWidth.vector2Value.x, scaleRangeWidth.vector2Value.y);
            float zScaleRandom = xScaleRandom; // for now uniform, could be distorted

            float scaleX = totalWidth * xScaleRandom;
            float scaleY = totalHeight * yScaleRandom;
            float scaleZ = totalLength * zScaleRandom;

            Vector3 scale = new Vector3(scaleX, scaleY, scaleZ);

            // set gameobject transform
            stampGO.transform.position = position;
            stampGO.transform.rotation = rotation;
            stampGO.transform.localScale = scale;
            
            // randomize position and rotation
            Transform[] transforms = new Transform[] { stampGO.transform };

            // randomize position, don't constrain within world bounds
            HeightStampUtils.SetRandomPosition(transforms, HeightStampUtils.ConstrainWorldBounds.Disabled);
            HeightStampUtils.SetRandomRotation(transforms);

            // set new selection
            Selection.activeObject = stampGO;

            Undo.RegisterCreatedObjectUndo(stampGO, "Create New Height Stamp");

        }

    }
}