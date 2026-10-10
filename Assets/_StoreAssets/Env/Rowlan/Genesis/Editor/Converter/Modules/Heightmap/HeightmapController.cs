using JBooth.MicroVerseCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Converter
{
    public class HeightmapController
    {
        private ConverterModule editor;

        private PlayerSettings setup;

        private SerializedProperty inputPath;
        private SerializedProperty outputPath;
        private SerializedProperty collectionName;

        public HeightmapController(ConverterModule editor)
        {
            this.editor = editor;


            SerializedProperty settings = editor.GetSerializedObject().FindProperty("heightmapSettings");

            inputPath = settings.FindPropertyRelative("inputPath");
            outputPath = settings.FindPropertyRelative("outputPath");
            collectionName = settings.FindPropertyRelative("collectionName");
        }


        public void OnInspectorGUI()
        {

            EditorGUILayout.HelpBox("Convert all Texture2D at an input location to a heightstamp collection", MessageType.None);

            GUIUtils.AssetPathSelector(inputPath, "Heightmap Path");
            GUIUtils.AssetPathSelector(outputPath, "Path for converted Heightstamps");
            EditorGUILayout.PropertyField(collectionName);

            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Create Height Stamp Collection"))
                {
                    ConvertToHeightStamps();
                }
                if (GUILayout.Button("Create Unity Terrain Brushes"))
                {
                    ConvertToTerrainBrushes();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private Texture2D[] GetHeightmaps()
        {
            string inputPathText = inputPath.stringValue;

            string[] folders = new string[] { inputPathText };

            // get all textures of the input path
            Texture2D[] heightmaps = AssetDatabase.FindAssets($"t: {typeof(Texture2D).Name}", folders)
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .Select(AssetDatabase.LoadAssetAtPath<Texture2D>)
                        .ToArray();

            Debug.Log("Heightmaps found: " + heightmaps.Length);

            // sort by name
            heightmaps = heightmaps.OrderBy(go => go.name).ToArray();

            return heightmaps;
        }

        private void ConvertToHeightStamps()
        {
            if (!PerformConsistencyChecks())
                return;

            string outputPathText = outputPath.stringValue;
            string collectionNameText = collectionName.stringValue;

            string outputFileName = collectionNameText;

            // scriptableobject must end with ".asset"
            string extension = ".asset";
            if( !outputFileName.EndsWith(extension))
            {
                outputFileName += extension;
            }

            // get all heightmaps as textures
            Texture2D[] heightmaps = GetHeightmaps();

            // create collection object
            ScriptableObjectManager<ContentCollection> settingsManager = new ScriptableObjectManager<ContentCollection>(outputPathText, outputFileName, true);
            ContentCollection collection = settingsManager.GetAsset();

            List<ContentData> contentDataList = new List<ContentData>();

            foreach ( Texture2D heightmap in heightmaps)
            {
                ContentData contentData = new ContentData();

                GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(heightmap);
                contentData.stamp = globalId.assetGUID.ToString();

                contentDataList.Add(contentData);
                
            }

            collection.contents = contentDataList.ToArray();

            // show new object in project
            Selection.activeObject = collection;

            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssetIfDirty(collection);
        }

        private void ConvertToTerrainBrushes()
        {
            if (!PerformConsistencyChecks())
                return;

            string outputPathText = outputPath.stringValue;

            // get all heightmaps as textures
            Texture2D[] heightmaps = GetHeightmaps();

            Type brushType = Assembly.Load("UnityEditor").GetType("UnityEditor.Brush");

            foreach (Texture2D heightmap in heightmaps)
            {
                MethodInfo methodInfo = brushType.GetMethod("CreateInstance", BindingFlags.NonPublic | BindingFlags.Static);
                UnityEngine.Object brushAsset = (UnityEngine.Object)methodInfo.Invoke(null, new object[] { heightmap, AnimationCurve.Linear(0, 0, 1, 1), 1.0f, false });

                string outputPath = Path.Combine(outputPathText, heightmap.name + ".asset");

                AssetDatabase.CreateAsset(brushAsset, outputPath);
            }

            AssetDatabase.SaveAssets();
        }

        private bool PerformConsistencyChecks()
        {
            string heightmapInputPath = inputPath.stringValue;
            string heightmapOutputPath = outputPath.stringValue;

            if (heightmapInputPath == heightmapOutputPath)
            {
                EditorUtility.DisplayDialog("Input Error", "Input and Output path mustn't be the same", "Ok");
                return false;
            }

            return true;
        }

    }
}