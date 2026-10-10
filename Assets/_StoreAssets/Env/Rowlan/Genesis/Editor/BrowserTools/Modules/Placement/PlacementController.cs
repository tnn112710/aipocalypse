using JBooth.MicroVerseCore;
using JBooth.MicroVerseCore.Browser;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static JBooth.MicroVerseCore.Browser.ContentBrowser;

namespace Rowlan.Genesis.BrowserTools
{
    public class PlacementController
    {
        private static GUIContent clearContent = new GUIContent("Clear", "Delete all stamps of the same type before performing the placement");
        private static GUIContent terrainHeightContent = new GUIContent("Terrain Height", "The terrain height to be used for height stamps");

        private BrowserToolsModule editor;

        private struct TerrainSettings
        {
            public float totalWidth;
            public float totalHeight;
            public float totalLength;
        }

        private bool clear = true;
        private int terrainHeight = 600;

        public PlacementController(BrowserToolsModule editor)
        {
            this.editor = editor;
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.HelpBox("Automatically place presets of the Content Browser in the Scene", MessageType.None);
                EditorGUILayout.HelpBox("Note: This feature is experimental and may be removed in a later version", MessageType.None);
            }
            EditorGUILayout.EndVertical();

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
                    EditorGUILayout.TextField("Presets", $"{browser.GetPresets()?.Count}");
                }
                EditorGUI.EndDisabledGroup();
                Tab tab = browser.GetSelectedTab();
                switch (tab)
                {
                    case Tab.Height:

                        clear = EditorGUILayout.Toggle(clearContent, clear);
                        if (clear)
                        {
                            EditorGUILayout.HelpBox("Clearing will remove alle existing height stamps!", MessageType.None);
                        }

                        terrainHeight = EditorGUILayout.IntSlider(terrainHeightContent, terrainHeight, 100, 1000);

                        if (GUILayout.Button("Place all Height Content on Grid"))
                        {
                            PerformHeightPlacement(browser);
                        }
                        break;

                    default:
                        //Debug.LogError($"Unsupported stamp {tab}");
                        break;
                }
            }
        }


        private void PerformHeightPlacement(ContentBrowser browser)
        {
            if (clear)
            {
                HeightStamp[] stamps = MicroVerse.instance.GetComponentsInChildren<HeightStamp>();
                foreach (HeightStamp stamp in stamps)
                {
                    if (stamp == null || !stamp.gameObject.activeInHierarchy)
                        continue;

                    Undo.DestroyObjectImmediate(stamp.gameObject);
                }
            }

            List<PresetItem> presets = browser.GetPresets();

            GameObject parent = MicroVerse.instance.gameObject;

            Terrain[] terrains = MicroVerse.instance.GetComponentsInChildren<Terrain>();
            Bounds worldBounds = TerrainUtil.ComputeTerrainBounds(terrains);

            TerrainSettings terrainSettings = new TerrainSettings()
            {
                totalWidth = worldBounds.size.x,
                totalHeight = terrainHeight, // worldBounds.size.y,
                totalLength = worldBounds.size.z,
            };

            // [MinMax(0f, 1f, true)]
            Vector2 scaleRangeHeight = new Vector2(1.0f, 1.0f); // scale to absolute terrain height for now

            PerformGridPlacement(parent, presets, terrainSettings, scaleRangeHeight);
        }

        private void PerformGridPlacement(GameObject parent, List<PresetItem> presets, TerrainSettings terrainSettings, Vector2 scaleRangeHeight)
        {
            // calculate optimal size
            float sizeX = LayoutUtils.FitSquares(terrainSettings.totalWidth, terrainSettings.totalLength, presets.Count);
            float sizeZ = sizeX;

            int columns = (int)Mathf.Round(terrainSettings.totalWidth / sizeX);

            float heightRatio = sizeX / terrainSettings.totalWidth;

            // stamps
            for (int i = 0; i < presets.Count; i++)
            {
                int gridX = i % columns;
                int gridY = i / columns;

                PresetItem settings = presets[i];

                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(new GUID(settings.content.stamp)));

                // stamp gameobject
                string name = texture.name + " (Height Stamp)";
                GameObject stampGO = new GameObject(name);
                stampGO.transform.parent = parent.transform;

                // stamp settings
                HeightStamp stamp = stampGO.AddComponent<HeightStamp>();

                stamp.stamp = texture;
                stamp.mode = HeightStamp.CombineMode.Add;

                stamp.falloff = new FalloffFilter();
                stamp.falloff.filterType = FalloffFilter.FilterType.Box;

                // position
                float positionX = gridX * sizeX + sizeX / 2f;
                float positionY = 0f;
                float positionZ = gridY * sizeZ + sizeZ / 2f;

                Vector3 position = new Vector3(positionX, positionY, positionZ);

                // rotation
                Quaternion rotation = Quaternion.identity;

                // scale
                float yScaleRandom = Random.Range(scaleRangeHeight.y, scaleRangeHeight.y);

                float scaleX = sizeX;
                float scaleY = terrainSettings.totalHeight * yScaleRandom * heightRatio;
                float scaleZ = sizeZ;

                Vector3 scale = new Vector3(scaleX, scaleY, scaleZ);

                // set gameobject transform
                stampGO.transform.position = position;
                stampGO.transform.rotation = rotation;
                stampGO.transform.localScale = scale;

            }
        }
    }
}