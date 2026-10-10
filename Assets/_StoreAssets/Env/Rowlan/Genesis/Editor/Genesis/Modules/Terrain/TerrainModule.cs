using JBooth.MicroVerseCore;
using Rowlan.Genesis.Terraform;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis
{
    public class TerrainModule : IGenesisModule
    {
        // create GUIContent; required for the intPopup with serializedProperty option
        public static GUIContent[] GUIHeightmapResolutionNames = new GUIContent[]
        {
            EditorGUIUtility.TrTextContent("33"),
            EditorGUIUtility.TrTextContent("65"),
            EditorGUIUtility.TrTextContent("129"),
            EditorGUIUtility.TrTextContent("257"),
            EditorGUIUtility.TrTextContent("513"),
            EditorGUIUtility.TrTextContent("1025"),
            EditorGUIUtility.TrTextContent("2049"),
            EditorGUIUtility.TrTextContent("4097")
        }; 

        public static int[] GUIHeightmapResolutions = new int[] { 33, 65, 129, 257, 513, 1025, 2049, 4097 };

        public static GUIContent[] GUITextureResolutionNames = new GUIContent[]
        {
            EditorGUIUtility.TrTextContent("16"),
            EditorGUIUtility.TrTextContent("32"),
            EditorGUIUtility.TrTextContent("64"),
            EditorGUIUtility.TrTextContent("128"),
            EditorGUIUtility.TrTextContent("256"),
            EditorGUIUtility.TrTextContent("512"),
            EditorGUIUtility.TrTextContent("1024"),
            EditorGUIUtility.TrTextContent("2048"),
            EditorGUIUtility.TrTextContent("4096")
        };

        public static int[] GUITextureResolutions = new int[] { 16, 32, 64, 128, 256, 512, 1024, 2048, 4096 };

        private GenesisDataEditor editor;

        private SerializedProperty totalWidth;
        private SerializedProperty totalLength;
        private SerializedProperty totalHeight;

        private SerializedProperty tilesX;
        private SerializedProperty tilesZ;

        private SerializedProperty startPosition;

        private SerializedProperty groupingID;
        private SerializedProperty autoConnect;

        private SerializedProperty pixelError;
        private SerializedProperty baseMapDistance;

        private SerializedProperty drawInstanced;

        private SerializedProperty heightmapResolution;
        private SerializedProperty controlTextureResolution;
        private SerializedProperty baseTextureResolution;

        private SerializedProperty detailResolutionPerPatch;
        private SerializedProperty detailResolution;

        private SerializedProperty clearExisting;
        private SerializedProperty assetDirectory;
        private SerializedProperty enableGuid;


        private bool generalFoldout = true;
        private bool groupingFoldout = true;
        private bool detailResolutionsFoldout = true;
        private bool textureResolutionsFoldout = true;
        private bool filesFoldout = true;

        private Vector2 scrollPosition = Vector2.zero;

        public TerrainModule(GenesisDataEditor editor)
        {
            this.editor = editor;

            SerializedProperty data = editor.serializedObject.FindProperty("terrainSettings");
             
            totalWidth = data.FindPropertyRelative("totalWidth");
            totalLength = data.FindPropertyRelative("totalLength");
            totalHeight = data.FindPropertyRelative("totalHeight");

            tilesX = data.FindPropertyRelative("tilesX");
            tilesZ = data.FindPropertyRelative("tilesZ");

            startPosition = data.FindPropertyRelative("startPosition");

            groupingID = data.FindPropertyRelative("groupingID");
            autoConnect = data.FindPropertyRelative("autoConnect");

            pixelError = data.FindPropertyRelative("pixelError");
            baseMapDistance = data.FindPropertyRelative("baseMapDistance");

            drawInstanced = data.FindPropertyRelative("drawInstanced");

            heightmapResolution = data.FindPropertyRelative("heightmapResolution");
            controlTextureResolution = data.FindPropertyRelative("controlTextureResolution");
            baseTextureResolution = data.FindPropertyRelative("baseTextureResolution");

            detailResolutionPerPatch = data.FindPropertyRelative("detailResolutionPerPatch");
            detailResolution = data.FindPropertyRelative("detailResolution");

            clearExisting = data.FindPropertyRelative("clearExisting");
            assetDirectory = data.FindPropertyRelative("assetDirectory");
            enableGuid = data.FindPropertyRelative("enableGuid");

        }

        public void OnInspectorGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            {
                filesFoldout = EditorGUILayout.Foldout(filesFoldout, "Files");

                if (filesFoldout)
                {
                    EditorGUI.indentLevel++;
                    {
                        GUIUtils.AssetPathSelector(assetDirectory, "Select Asset Directory");

                        EditorGUILayout.PropertyField(clearExisting);
                        EditorGUILayout.PropertyField(enableGuid);
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space();

                generalFoldout = EditorGUILayout.Foldout(generalFoldout, "General");

                if (generalFoldout)
                {
                    EditorGUI.indentLevel++;
                    {

                        EditorGUILayout.PropertyField(totalWidth);
                        EditorGUILayout.PropertyField(totalLength);
                        EditorGUILayout.PropertyField(totalHeight);

                        EditorGUILayout.Space();

                        EditorGUILayout.PropertyField(tilesX);
                        EditorGUILayout.PropertyField(tilesZ);

                        EditorGUILayout.Space();

                        bool prevWideMode = EditorGUIUtility.wideMode;
                        EditorGUIUtility.wideMode = true;
                        {
                            EditorGUILayout.PropertyField(startPosition);
                        }
                        EditorGUIUtility.wideMode = prevWideMode;


                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space();

                groupingFoldout = EditorGUILayout.Foldout(generalFoldout, "Grouping");

                if (groupingFoldout)
                {
                    EditorGUI.indentLevel++;
                    {
                        EditorGUILayout.PropertyField(groupingID);

                        // multi-terrain tiles: disable autoconnect
                        EditorGUI.BeginDisabledGroup(true);
                        {
                            EditorGUILayout.PropertyField(autoConnect);
                        }
                        EditorGUI.EndDisabledGroup();
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space();

                detailResolutionsFoldout = EditorGUILayout.Foldout(textureResolutionsFoldout, "Detail Resolutions");

                if (detailResolutionsFoldout)
                {
                    EditorGUI.indentLevel++;
                    {
                        EditorGUILayout.PropertyField(detailResolutionPerPatch);
                        EditorGUILayout.PropertyField(detailResolution);
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space();

                textureResolutionsFoldout = EditorGUILayout.Foldout(textureResolutionsFoldout, "Texture Resolutions");

                if (textureResolutionsFoldout)
                {
                    EditorGUI.indentLevel++;
                    {
                        // MicroVerse requires drawInstanced => that's only displayed
                        EditorGUI.BeginDisabledGroup(true);
                        {
                            EditorGUILayout.PropertyField(drawInstanced);
                        }
                        EditorGUI.EndDisabledGroup();

                        EditorGUILayout.PropertyField(pixelError);
                        EditorGUILayout.PropertyField(baseMapDistance);

                        EditorGUILayout.IntPopup(heightmapResolution, GUIHeightmapResolutionNames, GUIHeightmapResolutions);

                        EditorGUILayout.IntPopup(controlTextureResolution, GUITextureResolutionNames, GUITextureResolutions);
                        EditorGUILayout.IntPopup(baseTextureResolution, GUITextureResolutionNames, GUITextureResolutions);

                    }
                    EditorGUI.indentLevel--;
                }

            }
            EditorGUILayout.EndScrollView();
        }

        public void Create(GameObject parent)
        {
            GameObject terrains = new GameObject("Terrains");

            // parent
            terrains.transform.parent = parent.transform;

            // set group
            terrains.AddComponent<TerrainGroup>();

            // create terrain
            TerrainCreator.Create( terrains,editor.editorTarget);
        }

        public void Destroy()
        {

            if (!MicroVerse.instance)
                return;

            TerrainGroup group = MicroVerse.instance.GetTerrainGroup();

            if (!group)
                return;

            TerrainCreator.ClearExistingTerrains();

            Object.DestroyImmediate(group.gameObject);

        }

    }
}