#if __MICROVERSE_SPLINES__

using JBooth.MicroVerseCore;
using JBooth.MicroVerseCore.Browser;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static JBooth.MicroVerseCore.Browser.ContentBrowser;
using UnityEngine.Splines;
using System.Linq;

namespace Rowlan.Genesis.BrowserWizard
{
    public class HeightController
    {
        private BrowserWizardModule editor;
        private BiomesModule module;

        private SerializedProperty heightSettings;

        public HeightController(BrowserWizardModule editor, BiomesModule module)
        {
            this.editor = editor;
            this.module = module;

            heightSettings = editor.GetSerializedObject().FindProperty("heightSettings");

            Init();
        }

        private void Init()
        {
        }

        public void OnInspectorGUI(ContentBrowser browser)
        {
            bool enabled = browser.GetSelectedTab() == Tab.Height && editor.GetEditorTarget().splineContainer != null;
            if (!enabled)
            {
                EditorGUILayout.HelpBox($"Select {Tab.Height} in the Content Browser and provide a spline container with splines", MessageType.Info);
                return;
            }

            EditorGUILayout.PropertyField(heightSettings, true);

            bool prevEnabled = GUI.enabled;
            GUI.enabled = browser.GetSelectedTab() == Tab.Height && editor.GetEditorTarget()?.splineContainer != null;
            {
                if (GUILayout.Button(new GUIContent("Create Height", "Create random height stamps")))
                {
                    module.Clear(editor.GetEditorTarget()?.heightContainer);
                    List<SplineContainer> splineContainers = editor.GetEditorTarget()?.splineContainer?.GetComponentsInChildren<SplineContainer>()?.ToList();
                    CreateHeight(browser, splineContainers);
                }
            }
            GUI.enabled = prevEnabled;
        }

        private void CreateHeight(ContentBrowser browser, List<SplineContainer> splineContainers)
        {
            int browserBiomeCount = browser.GetPresets() == null ? 0 : browser.GetPresets().Count;

            if (browserBiomeCount == 0)
                return;

            GameObject parentGO = module.GetOrCreateSubContainer("Height", ref editor.GetEditorTarget().heightContainer);

            if (parentGO == null)
                return;

            module.Clear(parentGO);

            List<PresetItem> presets = browser.GetPresets();

            Bounds terrainWorldBounds = HeightStampUtils.GetTerrainWorldBounds();

            Vector2 heightRange = editor.GetEditorTarget().heightSettings.heightRange;

            FalloffFilter.FilterType filterType = editor.GetEditorTarget().heightSettings.filterType;
            Vector2 filterFalloffRange = editor.GetEditorTarget().heightSettings.filterFalloffRange;


            for (int i = 0; i < splineContainers.Count; i++)
            {
                SplineContainer splineContainer = splineContainers[i];
                int randomBiomeIndex = UnityEngine.Random.Range(0, browserBiomeCount);

                PresetItem presetItem = presets[randomBiomeIndex];

                // create heightstamp
                GameObject presetGO = Spawn(browser, presetItem, parentGO.transform);

                // rotation in y
                HeightStampUtils.SetRandomRotation(presetGO.transform);

                // get filter
                HeightStamp heightStamp = presetGO.GetComponent<HeightStamp>();
                FalloffFilter filter = presetGO.GetComponent<HeightStamp>().falloff;

                bool isBaseBiomeIndex = i == 0;

                if (isBaseBiomeIndex)
                {
                    presetGO.name += " (Base)";
                }

                if (isBaseBiomeIndex)
                {
                    // position
                    presetGO.transform.position = terrainWorldBounds.center;

                    // scale
                    presetGO.transform.localScale = new Vector3(terrainWorldBounds.size.x, RandomUtils.GetRandom(heightRange), terrainWorldBounds.size.z);

                    // filter
                    filter.filterType = FalloffFilter.FilterType.Range;
                    filter.falloffRange = filterFalloffRange;


                }
                else
                {
                    // combine mode for height stamps
                    heightStamp.mode = editor.GetEditorTarget().heightSettings.combineMode;

                    // transform
                    presetGO.transform.position = splineContainer.transform.position;

                    // scale
                    // spline bounds (could cause stretching when large islands are used)
                    if(editor.GetEditorTarget().heightSettings.splineBoundsScale)
                    {
                        Bounds bounds = SplineUtility.GetBounds(splineContainer.Spline);

                        float sizeX = bounds.size.x;
                        float sizeZ = bounds.size.z;

                        if( editor.GetEditorTarget().heightSettings.keepRatio)
                        {
                            sizeX = Mathf.Max( sizeX, sizeZ);
                            sizeZ = sizeX;
                        }

                        presetGO.transform.localScale = new Vector3(sizeX, RandomUtils.GetRandom(heightRange), sizeZ);
                    }
                    // default scale
                    else
                    {
                        presetGO.transform.localScale = new Vector3(presetGO.transform.localScale.x, RandomUtils.GetRandom(heightRange), presetGO.transform.localScale.z);
                    }

                    // filter
                    filter.filterType = FalloffFilter.FilterType.Range;
                    filter.falloffRange = filterFalloffRange;

                }
            }

            MicroVerse.instance?.Invalidate(null);
        }

        /// <summary>
        /// Create a prefab instance using a preset
        /// </summary>
        /// <param name="preset"></param>
        /// <param name="container"></param>
        /// <returns></returns>
        public GameObject Spawn(ContentBrowser browser, PresetItem preset, Transform container)
        {
            if (preset == null)
                return null;

            // instantiate MicroVerse object from preset
            GameObject prefabInstance = contentTabs[(int)browser.GetSelectedTab()].Spawn(browser, preset, false);

            // reparent
            prefabInstance.transform.SetParent(container);

            return prefabInstance;
        }
    }
}
#endif
