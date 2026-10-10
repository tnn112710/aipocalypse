using JBooth.MicroVerseCore;
using JBooth.MicroVerseCore.Browser;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using static JBooth.MicroVerseCore.Browser.ContentBrowser;

namespace Rowlan.Genesis.BrowserTools
{
    public class BatchController
    {
        private static GUIContent aboutContent = new GUIContent("Perform operations on presets of the Content Browser.\n\nSupported for Texture Stamps. Stamps will be placed with Global Falloff on the terrain and removed after the pause time.\n\nVarious modes:\n* Slideshow\n* Thumbnail creation for the content collection\n* Screenshot creation using PhotoSession\n* Change Seed and Noise in assets");
        private static GUIContent experimentalContent = new GUIContent("Note: This feature is experimental and may be removed in a later version");
        private static GUIContent batchModeContent = new GUIContent("Batch Mode", "Slide show, thumbnail creation or PhotoSession screenshot creation mode");
        private static GUIContent pauseTimeContent = new GUIContent("Pause Time", "The time in seconds to wait between batch operations");
        private static GUIContent removeNoiseContent = new GUIContent("Remove Noise", "Thumbnail creation: Remove stamp noise when thumbnails are created.");
        private static GUIContent confirmOverwriteContent = new GUIContent("Confirm Overwrite", "Thumbnail creation: Show overwrite confirmation popup for each item in case the thumbnail file exists.");
        private static GUIContent startBatchContent = new GUIContent("Start Batch", "Start the batch operations");
        private static GUIContent stopBatchContent = new GUIContent("Stop Batch", "Set a flag to stop batch processing");
#if USING_PHOTOSESSION
        private static GUIContent photoSessionContent = new GUIContent("Photo Session", "A Photo Session instance from hierarchy. It's used to save screenshots of the scene view");
#endif
        public enum BatchMode
        {
            Slideshow,
            ThumbnailCreation,
            [InspectorName("PhotoSession Screenshot")]
            PhotoSessionScreenshot,
            [InspectorName("Random Seed and Noise")]
            RandomSeedAndNoise,
            [InspectorName("Falloff Range, Scale 300,120,300")]
            DefaultFalloffAndScale
        }

        private BrowserToolsModule editor;

        private Dictionary<BatchMode, Tab[]> featureMatrix = new Dictionary<BatchMode, Tab[]>();

        private class BatchSettings
        {
            public BatchMode mode = BatchMode.Slideshow;
            public NoiseRandomization noiseRandomization = NoiseRandomization.SharedOffset;
            public bool removeNoise = false;
            public bool confirmOverwrite = true;
            public float pauseTime = 1f;
#if USING_PHOTOSESSION
            public PhotoSession.PhotoSession photoSession;
#endif
        }

        private BatchSettings settings = new BatchSettings();

        private bool running = false;

        private string statusText = "";

        public BatchController(BrowserToolsModule editor)
        {
            this.editor = editor;

            // initialize feature matrix
            Tab[] tabs;
            
            tabs = new Tab[] { Tab.Texture, Tab.Vegetation, Tab.Biomes, Tab.Global};
            featureMatrix.Add(BatchMode.Slideshow, tabs);
            featureMatrix.Add(BatchMode.ThumbnailCreation, tabs);
            featureMatrix.Add(BatchMode.PhotoSessionScreenshot, tabs);

            tabs = new Tab[] { Tab.Texture, Tab.Vegetation, Tab.Biomes };
            featureMatrix.Add(BatchMode.RandomSeedAndNoise, tabs);
            featureMatrix.Add(BatchMode.DefaultFalloffAndScale, tabs);

        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.HelpBox(aboutContent);
                EditorGUILayout.HelpBox(experimentalContent);
            }
            EditorGUILayout.EndVertical();

            ContentBrowser browser = ContentBrowserManager.GetContentBrowser();

            if( browser == null)
            {
                EditorGUILayout.HelpBox("Please open the Content Browser", MessageType.Error);
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                {
                    EditorGUILayout.TextField("Package", $"{browser.GetSelectedPackage()?.packName}");
                    EditorGUILayout.TextField("Presets", $"{browser.GetPresets()?.Count}");
                    EditorGUILayout.TextField("Status", statusText);

                }
                EditorGUI.EndDisabledGroup();

                Tab tab = browser.GetSelectedTab();

                bool batchEnabled = false;

                if (featureMatrix.TryGetValue(settings.mode, out Tab[] tabs))
                {
                    batchEnabled = tabs.Contains(tab);
                }

                EditorGUILayout.BeginVertical();
                {
                    settings.mode = (BatchMode)EditorGUILayout.EnumPopup(batchModeContent, settings.mode);

                    if (!batchEnabled)
                    {
                        EditorGUILayout.HelpBox($"Feature supported for these tabs of the content browser: {string.Join(",", tabs)}", MessageType.Warning);
                    }

                    settings.pauseTime = EditorGUILayout.FloatField(pauseTimeContent, settings.pauseTime);

                    switch (settings.mode)
                    {
                        case BatchMode.Slideshow:
                            break;
                        case BatchMode.ThumbnailCreation:
                            settings.removeNoise= EditorGUILayout.Toggle(removeNoiseContent, settings.removeNoise);
                            settings.confirmOverwrite = EditorGUILayout.Toggle(confirmOverwriteContent, settings.confirmOverwrite);
                            break;
                        case BatchMode.PhotoSessionScreenshot:
#if USING_PHOTOSESSION

                            // new GUIContent("Photo Session", "A Photo Session instance")
                            settings.photoSession = (PhotoSession.PhotoSession)EditorGUILayout.ObjectField(photoSessionContent, settings.photoSession, typeof(PhotoSession.PhotoSession), true);

                            if (settings.photoSession == null)
                            {
                                EditorGUILayout.HelpBox("Please assign a PhotoSession instance from the Hierarchy.", MessageType.Error);
                            }
#else
                                     EditorGUILayout.HelpBox("This feature requires PhotoSession. Please install PhotoSession.", MessageType.Error);
#endif
                            break;
                        case BatchMode.RandomSeedAndNoise:
                            settings.noiseRandomization = (NoiseRandomization)EditorGUILayout.EnumPopup("Noise Randomization", settings.noiseRandomization);
                            break;
                        case BatchMode.DefaultFalloffAndScale:
                            break;

                    }

                    EditorGUILayout.BeginHorizontal();
                    {
                        bool startBatchEnabled = batchEnabled && !running;
                        bool stopBatchEnabled = batchEnabled && running;

                        EditorGUI.BeginDisabledGroup(!startBatchEnabled);
                        {
                            if (GUILayout.Button(startBatchContent))
                            {
                                StartBatch( browser);
                            }
                        }
                        EditorGUI.EndDisabledGroup();

                        EditorGUI.BeginDisabledGroup(!stopBatchEnabled);
                        {

                            if (GUILayout.Button(stopBatchContent))
                            {
                                running = false;
                            }

                        }
                        EditorGUI.EndDisabledGroup();

                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
        }

        public void StartBatch(ContentBrowser browser)
        {
            ContentCollection contentCollection = GetContenCollection();

            if (contentCollection == null || contentCollection.contents.Length == 0)
                return;

            // pre-process batch, eg confirmation popup
            switch (settings.mode)
            {
                case BatchMode.Slideshow:
                case BatchMode.ThumbnailCreation:
                case BatchMode.PhotoSessionScreenshot:
                    // effectively start the batch
                    EditorCoroutineUtility.StartCoroutine(PerformBatchTasks(browser, contentCollection), this);
                    break;

                case BatchMode.RandomSeedAndNoise:
                    ChangeSeedAndNoise(browser, contentCollection);
                    break;
                case BatchMode.DefaultFalloffAndScale:
                    ChangeFalloffAndScale(browser, contentCollection);
                    break;
            }
        }

        private ContentCollection GetContenCollection()
        {
            ContentBrowser browser = EditorWindow.GetWindow<ContentBrowser>();
            BrowserContent contentAsset = browser.GetSelectedBrowserContentAsset();

            if (contentAsset == null)
            {
                Debug.LogError("Content asset not found");
                return null;
            }

            ContentCollection collection = contentAsset as ContentCollection;

            if (collection == null)
            {
                Debug.LogError("No content collection specified");
                return null;
            }

            return collection;
        }

        private void UpdateBrowserSelection(ContentBrowser browser, PresetItem selectedPresetItem)
        {
            browser.GetContentSelectionGrid().selectedPresetItem = selectedPresetItem;
            browser.Repaint();
        }

        public IEnumerator PerformBatchTasks( ContentBrowser browser, ContentCollection collection)
        {

            Debug.Log("Task started");

            ContentSelectionGrid selectionGrid = browser.GetContentSelectionGrid();
            List<PresetItem> presets = browser.GetPresets();

            PresetItem initialSelectedPresetItem = selectionGrid.selectedPresetItem;

            ContentData[] contents = collection.contents;

            running = true;

            for (int i = 0; i < contents.Length; i++)
            {
                if (!running)
                {
                    Debug.Log("Robot aborted");
                    break;
                }

                ContentData data = contents[i];

                // instantiate
                GameObject prefab = data.prefab;

                if (prefab == null)
                {
                    continue;
                }

                // selection
                PresetItem selectedPresetItem = presets.Find(x => x.content == data);
                UpdateBrowserSelection(browser, selectedPresetItem);

                GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

                // set falloff to global
                // might need to be extended depending on the use case
                FalloffOverride falloffOverride = instance.GetComponent<FalloffOverride>();
                if (falloffOverride)
                {
                    falloffOverride.filter.filterType = FalloffFilter.FilterType.Global;
                }

                TextureStamp textureStamp = instance.GetComponent<TextureStamp>();
                if (textureStamp)
                {
                    textureStamp.filterSet.falloffFilter.filterType = FalloffFilter.FilterType.Global;
                }

#if __MICROVERSE_VEGETATION__
                TreeStamp treeStamp = instance.GetComponent<TreeStamp>();
                if (treeStamp)
                {
                    treeStamp.filterSet.falloffFilter.filterType = FalloffFilter.FilterType.Global;
                }
#endif

                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = data.prefab.name;
                instance.transform.SetParent(MicroVerse.instance?.transform);

                // optionally remove the noise, eg for thumbnail creation
                if( settings.removeNoise)
                {
                    Stamp[] stamps = instance.transform.GetComponentsInChildren<Stamp>();
                    foreach(Stamp stamp in stamps)
                    {
                        stamp.GetFilterSet().weightNoise.noiseType = Noise.NoiseType.None;
                        stamp.GetFilterSet().weight2Noise.noiseType = Noise.NoiseType.None;
                        stamp.GetFilterSet().weight3Noise.noiseType = Noise.NoiseType.None;
                    }
                }

                // manually invoke the drop action execution; see also code in SceneDragHandler.cs
                #region drop action

                IContentBrowserDropAction[] dropActions = instance.GetComponentsInChildren<IContentBrowserDropAction>();
                for (int j = 0; j < dropActions.Length; j++)
                {
                    IContentBrowserDropAction dropAction = dropActions[j];

                    if (dropAction == null)
                        continue;

                    // perform action
                    // no need to handle the destroyAfterExecute value, the object will be destroy either way later
                    dropAction.Execute(out bool destroyAfterExecute);
                }

                #endregion drop action

                // update MV
                MicroVerse.instance?.Invalidate(null);

                Debug.Log($"Processing preset {instance.name}");

                statusText = $"{i+1} / {contents.Length}: {instance.name}";
                
                Repaint();

                yield return new EditorWaitForSeconds(settings.pauseTime);

                // Debug.Log($"Waited {settings.pauseTime} seconds");

                int collectionIndex = i;
                PerformAction( settings, collection, collectionIndex);

#if __MICROVERSE_OBJECTS__
                // in case of an object stamp first destroy all spawned instances so they don't remain
                ObjectStamp[] objectStamps = instance.GetComponentsInChildren<ObjectStamp>();
                foreach(ObjectStamp objectStamp in objectStamps)
                {
                    foreach(GameObject osInstance in objectStamp.spawnedInstances)
                    {
                        GameObject.DestroyImmediate(osInstance);
                    }
                }
#endif

                // destroy the instance itself
                GameObject.DestroyImmediate(instance);
            }

            running = false;

            Debug.Log("Task finished");

            statusText = "";

            Repaint();

            // select initially selected preset item
            UpdateBrowserSelection(browser, initialSelectedPresetItem);

            yield break;
        }

        private void PerformAction(BatchSettings settings, ContentCollection collection, int collectionIndex)
        {
            switch (settings.mode)
            {
                case BatchMode.Slideshow:
                    break;
                case BatchMode.ThumbnailCreation:
                    ThumbnailCreator.SaveIcon(collection, collectionIndex, settings.confirmOverwrite);
                    break;
                case BatchMode.PhotoSessionScreenshot:
#if USING_PHOTOSESSION
                    settings.photoSession?.CaptureSceneView();
#endif
                    break;

                case BatchMode.RandomSeedAndNoise:
                    // handled in its own method;
                    break;
                case BatchMode.DefaultFalloffAndScale:
                    // handled in its own method;
                    break;
            }
        }

        private void Repaint()
        {
            EditorWindow.GetWindow<BrowserToolsEditorWindow>()?.Repaint();
        }


        private void ChangeSeedAndNoise(ContentBrowser browser, ContentCollection collection)
        {
            ContentData[] contents = collection.contents;

            bool isContinue = EditorUtility.DisplayDialog("Change Seed and Noise", $"This will change {contents.Length} asset files and cannot be undone! Continue?", "Yes", "No");

            if (!isContinue)
                return;

            for (int i = 0; i < contents.Length; i++)
            {

                int collectionIndex = i;
                ContentData item = collection.contents[collectionIndex];

                if (item.prefab == null)
                    return;

                GameObject prefab = item.prefab;
                GameObject prefabInstance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

                #region update prefab data

                Stamp[] stamps = prefabInstance.GetComponentsInChildren<Stamp>();

                RandomUtils.SetRandomSeed(stamps);
                RandomUtils.SetRandomNoiseOffset(stamps, settings.noiseRandomization);

                #endregion update prefab data

                PrefabUtility.ApplyPrefabInstance(prefabInstance, InteractionMode.AutomatedAction);
                //PrefabUtility.RecordPrefabInstancePropertyModifications(prefabInstance);

                Object.DestroyImmediate(prefabInstance);

                Debug.Log($"Prefab updated: {prefab}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }

        private void ChangeFalloffAndScale(ContentBrowser browser, ContentCollection collection)
        {
            ContentData[] contents = collection.contents;

            bool isContinue = EditorUtility.DisplayDialog("Change Falloff and Transform", $"This will change {contents.Length} asset files and cannot be undone! Continue?", "Yes", "No");

            if (!isContinue)
                return;

            for (int i = 0; i < contents.Length; i++)
            {

                int collectionIndex = i;
                ContentData item = collection.contents[collectionIndex];

                if (item.prefab == null)
                    return;

                GameObject prefab = item.prefab;
                GameObject prefabInstance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

                #region update prefab data

                prefab.transform.localPosition = Vector3.zero;
                prefab.transform.localRotation = Quaternion.identity;
                prefab.transform.localScale = editor.GetEditorTarget().defaultScale;

                FalloffOverride falloffOverride = prefab.GetComponent<FalloffOverride>();

                if(falloffOverride)
                {
                    falloffOverride.filter.filterType = FalloffFilter.FilterType.Range;
                }

                #endregion update prefab data

                PrefabUtility.ApplyPrefabInstance(prefabInstance, InteractionMode.AutomatedAction);
                //PrefabUtility.RecordPrefabInstancePropertyModifications(prefabInstance);

                // first set the instance dirty so the prefab changes persist
                EditorUtility.SetDirty(prefabInstance);

                // destroy the instance so it won't show up in the hierarchy anymore
                Object.DestroyImmediate(prefabInstance);

                Debug.Log($"Prefab updated: {prefab}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }
    }
}