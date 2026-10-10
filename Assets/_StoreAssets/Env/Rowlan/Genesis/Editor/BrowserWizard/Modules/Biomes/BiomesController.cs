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
    public class BiomesController
    {
        private BrowserWizardModule editor;
        private BiomesModule module;

        private SerializedProperty biomesSettings;

        public BiomesController(BrowserWizardModule editor, BiomesModule module)
        {
            this.editor = editor;
            this.module = module;

            biomesSettings = editor.GetSerializedObject().FindProperty("biomesSettings");

            Init();
        }

        private void Init()
        {
        }

        public void OnInspectorGUI( ContentBrowser browser)
        {
            bool enabled = browser.GetSelectedTab() == Tab.Biomes && editor.GetEditorTarget().splineContainer != null;
            if (!enabled)
            {
                EditorGUILayout.HelpBox($"Select {Tab.Biomes} in the Content Browser and provide a spline container with splines", MessageType.Info);
                return;
            }

            EditorGUILayout.PropertyField(biomesSettings, true);

            GUILayout.BeginHorizontal();
            {
                // biomes can only be created if there are splines
                bool prevEnabled = GUI.enabled;
                GUI.enabled = browser.GetSelectedTab() == Tab.Biomes && editor.GetEditorTarget()?.splineContainer != null;
                {
                    if (GUILayout.Button(new GUIContent("Create Biomes", "Create biomes using the created spline")))
                    {
                        module.Clear(editor.GetEditorTarget()?.biomesContainer);
                        List<SplineContainer> splineContainers = editor.GetEditorTarget()?.splineContainer?.GetComponentsInChildren<SplineContainer>().ToList();
                        CreateBiomes(browser, splineContainers);
                    }
                }
                GUI.enabled = prevEnabled;
            }
            GUILayout.EndHorizontal();
        }

        private void CreateBiomes(ContentBrowser browser, List<SplineContainer> splineContainers)
        {
            int browserBiomeCount = browser.GetPresets() == null ? 0 : browser.GetPresets().Count;

            if (browserBiomeCount == 0)
                return;

            GameObject parentGO = module.GetOrCreateSubContainer("Biomes", ref editor.GetEditorTarget().biomesContainer);

            if (parentGO == null)
                return;

            module.Clear(parentGO);

            List<PresetItem> presets = browser.GetPresets();

            for (int i = 0; i < splineContainers.Count; i++)
            {
                SplineContainer splineContainer = splineContainers[i];
                int randomBiomeIndex = UnityEngine.Random.Range(0, browserBiomeCount);

                PresetItem presetItem = presets[randomBiomeIndex];

                GameObject presetGO = Spawn(browser, presetItem, parentGO.transform);

                bool isBaseBiomeIndex = i == 0;

                if ( isBaseBiomeIndex)
                {
                    FalloffOverride fo = presetGO.GetComponent<FalloffOverride>();
                    if (fo)
                    {
                        fo.filter.filterType = FalloffFilter.FilterType.Global;
                    }
                }
                else
                {
                    // create spline area, prevent adding it multiple times in case one exists already (eg because of a previous biome assignment)
                    SplineArea sa = splineContainer.transform.GetComponent<SplineArea>();
                    if (sa == null)
                    {
                        sa = Undo.AddComponent<SplineArea>(splineContainer.transform.gameObject);
                    }

                    FalloffOverride fo = presetGO.GetComponent<FalloffOverride>();
                    fo.filter.filterType = FalloffFilter.FilterType.SplineArea;
                    fo.filter.splineArea = sa;
                    fo.filter.splineAreaFalloff = RandomUtils.GetRandom(editor.GetEditorTarget().biomesSettings.falloff);
                    fo.filter.splineAreaFalloffBoost = RandomUtils.GetRandom(editor.GetEditorTarget().biomesSettings.widthBoost);
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

            // consider seed & noise
            if (editor.GetEditorTarget().biomesSettings.autoSeedAndNoise)
            {
                Stamp[] stamps = prefabInstance.GetComponentsInChildren<Stamp>();

                RandomUtils.SetRandomSeed(stamps);
                RandomUtils.SetRandomNoiseOffset(stamps, editor.GetEditorTarget().biomesSettings.noiseRandomization);
            }

            return prefabInstance;
        }

    }
}
#endif
