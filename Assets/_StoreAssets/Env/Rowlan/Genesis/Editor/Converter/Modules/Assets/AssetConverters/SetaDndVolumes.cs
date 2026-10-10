using UnityEditor;

namespace Rowlan.Genesis.Converter
{
#if USING_BIOMES_PRESETS_MV_3

using Rowlan.Biomes_Presets_3;
using UnityEngine;
using JBooth.MicroVerseCore;
using System.Collections.Generic;
using System.IO;
using System.Linq;

    public class SetaDndVolumes: IAssetConverter
    {
        private ConverterModule editor;

        private PlayerSettings setup;

        private string inputPath = "";
        private string outputPath = "";
        private ContentCollection contentCollection;
        private bool unpackPrefab = false;
        private bool useOriginalThumbnail = true;
        private bool applyPresetInstance = true;
        private bool searchSubFolders = false;
        public SetaDndVolumes(ConverterModule editor)
        {
            this.editor = editor;
        }


        public void OnInspectorGUI()
        {
            // header
            EditorGUILayout.LabelField("DnD Volumes", EditorStyles.miniBoldLabel);

            EditorGUILayout.HelpBox("Convert prefabs to presets:\n* reparent prefabs from input folder\n* disable directional light gameobject\n* add PresetInstance script\n* add LightValidation script\n* register directional light for delayed activation\n* save prefabs to output folder, replace existing ones\n* clear content collection and insert new presets", MessageType.None);

            inputPath = GUIUtils.AssetPathSelector(inputPath, "DnD Volumes Prefab Path");
            outputPath = GUIUtils.AssetPathSelector(outputPath, "Path for converted Prefabs");
            contentCollection = (ContentCollection) EditorGUILayout.ObjectField("Content Collection", contentCollection, typeof(ContentCollection), false);
            unpackPrefab = EditorGUILayout.Toggle("Unpack Prefab", unpackPrefab);
            useOriginalThumbnail = EditorGUILayout.Toggle("Use original thumbnail", useOriginalThumbnail);
            searchSubFolders = EditorGUILayout.Toggle("Search in Sub-Folders", searchSubFolders);
            applyPresetInstance = EditorGUILayout.Toggle(new GUIContent("Apply Preset Instance", "Add preset instance specific behaviour: Add scripts, disable directional light of the original prefab"), applyPresetInstance);
            
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Convert Prefabs"))
                {
                    ConvertPrefabs();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void ConvertPrefabs()
        {
            if (!PerformConsistencyChecks())
                return;

            bool isContinue = EditorUtility.DisplayDialog("Overwrite Files", $"This will overwrite existing prefabs and content collections.\nThis isn't undoable!\nBackup your project files.\nContinue?", "Yes", "No");

            if (!isContinue)
                return;

            string[] prefabGUIDs = AssetDatabase.FindAssets("t:Prefab", new[] { inputPath });

            List<ContentData> contentDataList = new List<ContentData>();

            foreach (string guid in prefabGUIDs)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                string subPath = assetPath.Replace(inputPath, "");
                int folderCount = subPath.Count(c => c == Path.DirectorySeparatorChar) + subPath.Count(c => c == Path.AltDirectorySeparatorChar);
                bool isInSubFolder = folderCount > 1; // subPath asset starts with a path separator, eg /Bonus Content/MyPrefab.prefab

                if (isInSubFolder && !searchSubFolders)
                {
                    // Debug.Log($"Skipping prefab in subpath: {assetPath.Replace(inputPath,"")}");
                    continue;
                }

                ContentData contentData = new ContentData();

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                if (prefab == null)
                    continue;

                // reparent the prefab
                if( ReparentPrefab(prefab, out string newPrefabPath))
                {
                    GameObject newPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(newPrefabPath);

                    contentData.prefab = newPrefab;

                    if( useOriginalThumbnail)
                    {
                        string thumbnailPath = assetPath.Replace(".prefab", ".jpg");

                        Texture2D thumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(thumbnailPath);
                        contentData.previewImage = thumbnail;
                    }

                    contentDataList.Add(contentData);
                }
            }

            contentCollection.contents = contentDataList.ToArray();

            // select collection in project
            Selection.activeObject = contentCollection;

            EditorUtility.SetDirty(contentCollection);
            AssetDatabase.SaveAssetIfDirty(contentCollection);
        }

        private bool ReparentPrefab( GameObject prefab, out string newPrefabPath)
        {
            newPrefabPath = @$"{outputPath}/{prefab.name}.prefab";

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

            if (instance == null)
            {
                Debug.LogError($"Failed to instantiate the prefab {prefab.name}.");

                return false;
            }

            // unpack the prefab
            if (unpackPrefab)
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            GameObject newPrefabInstance = new GameObject(prefab.name);

            instance.transform.SetParent(newPrefabInstance.transform, false);

            ProcessNewPrefab(newPrefabInstance);

            GameObject newPrefabAsset = PrefabUtility.SaveAsPrefabAsset(newPrefabInstance, newPrefabPath);
            
            AssetDatabase.SaveAssetIfDirty(newPrefabAsset);

            Debug.Log("Prefab created: " + newPrefabPath);

            // destroy prefab, so it won't be used in the scene
            UnityEngine.Object.DestroyImmediate(newPrefabInstance, true);

            return true;
        }

        private void ProcessNewPrefab(GameObject prefab)
        {
            if (!applyPresetInstance)
                return;

            #region PresetInstance

            PresetInstance presetInstance = prefab.AddComponent<PresetInstance>();

            presetInstance.category = PresetInstance.Category.SkyAndLight;

            Light[] lights = prefab.GetComponentsInChildren<Light>();

            foreach (Light light in lights)
            {
                Debug.Log($"Disabling {light.gameObject.name}, registering for lazy activation");

                light.gameObject.SetActive(false);
                presetInstance.delayedActivation.Add(light.gameObject);
            }

            #endregion PresetInstance

            #region DirectionalLightOverview

            LightValidation lightValidation = prefab.AddComponent<LightValidation>();

            #endregion
        }

        private bool PerformConsistencyChecks()
        {
            if (inputPath == outputPath)
            {
                EditorUtility.DisplayDialog("Input Error", "Input and Output path mustn't be the same", "Ok");
                return false;
            }

            if (contentCollection == null)
            {
                EditorUtility.DisplayDialog("Input Error", "Content Collection isn't set", "Ok");
                return false;
            }

            return true;
        }

    }

#else
    public class SetaDndVolumes : IAssetConverter
    {
        public SetaDndVolumes(ConverterModule editor)
        {
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("This feature is supported by Biomes & Presets 3 for MicroVerse. The asset isn't installed.", MessageType.Warning);
        }
    }
#endif
}
