using JBooth.MicroVerseCore;
using JBooth.MicroVerseCore.Browser;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Migration
{
    public class RemapController
    {

        /// <summary>
        /// Mapping of asset path to asset data.
        /// Keep as local variable for the inspector.
        /// </summary> 
        public AnalyzerData analyzerData;

        public AnalyzerData originalAssets;

        private MigrationModule editor;

        private Vector2 scrollPosition = Vector2.zero;

        // A custom GUIStyle for the text area with a monospaced font.
        private GUIStyle textAreaStyle;


        private int buttonWidth = 160;
        private string defaultRegistryPath = "Assets/Original Remap Asset.asset";

        public RemapController(MigrationModule editor)
        {
            this.editor = editor;
        }

        public void OnInspectorGUI()
        {

            EditorGUILayout.HelpBox("The remap operations are not undoable and will change your project files! Backup your project first!", MessageType.Warning);

            EditorGUILayout.HelpBox("Remap guids in presets eg from Built-in to URP", MessageType.None);

            // check if browser is open
            ContentBrowser browser = ContentBrowserManager.GetContentBrowser();

            // browser not found, show message
            if (browser == null)
            {
                EditorGUILayout.HelpBox("Please open the Content Browser", MessageType.Error);
            }
            // browser found
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                {
                    EditorGUILayout.TextField("Package", $"{browser.GetSelectedPackage()?.packName}");
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.HelpBox("An asset is required to store the original data. Otherwise upon installation of eg the URP assets the data would get lost. You can delete the asset after the remap operation.", MessageType.None);

                EditorGUILayout.BeginHorizontal();
                {
                    originalAssets = (AnalyzerData)EditorGUILayout.ObjectField("Original Asset Registry", originalAssets, typeof(AnalyzerData), false);

                    if (GUILayout.Button("New", EditorStyles.miniButton, GUILayout.Width(60)))
                    {
                        AnalyzerData asset = ScriptableObject.CreateInstance<AnalyzerData>();

                        // Define a unique asset path
                        string path = AssetDatabase.GenerateUniqueAssetPath(defaultRegistryPath);

                        // Create and save the asset to the specified path
                        AssetDatabase.CreateAsset(asset, path);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();

                        originalAssets = asset;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            GUI.enabled = originalAssets != null;
            {
                EditorGUILayout.HelpBox("Step 1: Scan original, save scan result in original asset registry", MessageType.None);

                if (GUILayout.Button("Scan & Update Registry", GUILayout.Width(buttonWidth)))
                {
                    PerformAnalysis(browser);
                }

                EditorGUILayout.HelpBox("\nStep 2: Install Update (eg URP version)\n", MessageType.None);

                EditorGUILayout.HelpBox("Step 3: Re-assign original and load for comparison", MessageType.None);

                if (GUILayout.Button("Load Original", GUILayout.Width(buttonWidth)))
                {
                    analyzerData = originalAssets;
                    EditorUtility.SetDirty(analyzerData);
                }

                if(originalAssets != null && analyzerData?.packageId != browser.GetSelectedPackage().id)
                {
                    EditorGUILayout.HelpBox($"Package mismatch. Original asset registry must match the selected package: {analyzerData?.packageId}", MessageType.Error);
                }

                EditorGUILayout.HelpBox("Step 4: Update preset files. This step is not undoable!", MessageType.None);

                if (GUILayout.Button("Update Preset Files", GUILayout.Width(buttonWidth)))
                {
                    UpdatePresetFiles(browser);
                }
            }
            GUI.enabled = true;

            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.HelpBox("GUID and Asset Path", MessageType.None);

                string tableText = "";

                if (analyzerData != null && analyzerData.assetDataList != null)
                {
                    foreach (AssetData data in analyzerData.assetDataList)
                    {
                        tableText += data.guid + "\t" + data.assetPath + "\n";
                    }
                }

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
                {
                    //GUI.enabled = false;
                    tableText = EditorGUILayout.TextArea(tableText, GUILayout.ExpandHeight(true));
                    //GUI.enabled = true;
                }
                EditorGUILayout.EndScrollView();

            }
            EditorGUILayout.EndVertical();
        }

        private List<PresetItem> GetFilteredPresets(ContentBrowser browser)
        {
            // get all content types of the currently selected package
            List<BrowserContent> filteredContent = browser.GetAllContent()
                .Where(x => x is ContentCollection)
                .Where(x => x.id == browser.GetSelectedPackage().id)
                .ToList();

            List<ContentCollection> filteredCollections = filteredContent
                .Cast<ContentCollection>()
                .ToList();

            List<PresetItem> filteredPresets = new List<PresetItem>();

            foreach (ContentCollection collection in filteredCollections)
            {
                ContentData[] contentData = collection.contents;
                for (int i = 0; i < contentData.Length; i++)
                {
                    filteredPresets.Add(new PresetItem(collection, contentData[i], i));
                }
            }

            return filteredPresets;
        }

        private void PerformAnalysis(ContentBrowser browser)
        {
            List<PresetItem> filteredPresets = GetFilteredPresets(browser);

            RemapAnalyzer analyzer = new RemapAnalyzer(filteredPresets);

            analyzerData = ScriptableObject.CreateInstance<AnalyzerData>();
            analyzerData.assetDataList = analyzer.GetAssetData();
            analyzerData.packageId = browser.GetSelectedPackage().id;

            EditorUtility.SetDirty(analyzerData);

            // save into asset
            originalAssets.assetDataList = analyzerData.assetDataList;
            originalAssets.packageId = browser.GetSelectedPackage().id;

            EditorUtility.SetDirty(originalAssets);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }

        private void UpdatePresetFiles(ContentBrowser browser)
        {
            bool isContinue = EditorUtility.DisplayDialog("Update Preset Files", "This will change the preset files and is not undoable!\n\nCreate a backup first!\n\nContinue?", "Yes", "No");

            if (!isContinue)
                return;

            List<PresetItem> filteredPresets = GetFilteredPresets(browser);

            // map old guid => new guid
            Dictionary<string, string> guidMap = new Dictionary<string, string>();
            foreach (AssetData assetData in originalAssets.assetDataList)
            {
                GUID originalGuid = new GUID(assetData.guid);
                GUID newGuid = AssetDatabase.GUIDFromAssetPath(assetData.assetPath);

                if (!assetData.guid.Equals(newGuid))
                {
                    Debug.Log($"Mismatch: {assetData.assetPath}");

                    guidMap.TryAdd(originalGuid.ToString(), newGuid.ToString());
                }

            }

            foreach ( PresetItem presetItem in filteredPresets)
            {
                GameObject prefab = presetItem.content.prefab;

                if (prefab == null)
                    continue;

                if( AssetDatabase.TryGetGUIDAndLocalFileIdentifier( prefab, out string guid, out long localId))
                {
                    string presetAssetPath = AssetDatabase.GUIDToAssetPath(guid);

                    ReplaceMetaGuid(presetAssetPath, guidMap);
                }
            }
        }

        private void ReplaceMetaGuid(string assetPath, Dictionary<string, string> guidMap)
        {
            if( !assetPath.EndsWith(".prefab"))
            {

                Debug.LogError($"Not a prefab: {assetPath}");
                return;
            }

            // Determine the meta file path
            string filePath = assetPath;
            if (!File.Exists(filePath))
            {
                Debug.LogError("Meta file not found at: " + filePath);
                return;
            }

            // Read the contents of the meta file.
            string metaContents = File.ReadAllText(filePath);

            foreach (KeyValuePair<string, string> kvp in guidMap)
            {
                string oldGuid = kvp.Key;
                string newGuid = kvp.Value;

                string pattern = @"(?<=guid:\s)" + Regex.Escape(oldGuid);
                Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);

                // Replace the old GUID with the new GUID
                metaContents = regex.Replace(metaContents, newGuid);

                // Debug.Log($"Key: {kvp.Key}, Value: {kvp.Value}");
            }

            // Write the modified contents back to the meta file.
            File.WriteAllText(filePath, metaContents);

            Debug.Log("Replaced GUIDs in file: " + filePath);

            // Optionally force a reimport of the asset so Unity picks up the changes immediately.
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }
    }
}