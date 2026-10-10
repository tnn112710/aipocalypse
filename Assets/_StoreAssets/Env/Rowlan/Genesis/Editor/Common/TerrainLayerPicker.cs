using UnityEngine;
using UnityEditor;
using System.Linq;
using JBooth.MicroVerseCore;

namespace Rowlan.Genesis
{
    /// <summary>
    /// Tool window that allows you to pick a terrain layer. 
    /// The layers are presented in order of percentage of splatmap usage.
    /// </summary>
    public class TerrainLayerPicker : EditorWindow
    {
        public static bool helpBoxVisible = false;

        private const float CellWidth = 128f;
        private const float TextureSize = 64f;

        [System.Serializable]
        private class PickerState
        {
            public Terrain terrain;
            public TerrainLayer[] layers;
            public float[] weights;
            public Vector3 hitPointWorld;
            public bool hasSample;
            public int selectedLayerIndex = -1;

            public PickerState Clone()
            {
                return new PickerState
                {
                    terrain = terrain,
                    layers = layers != null ? (TerrainLayer[])layers.Clone() : null,
                    weights = weights != null ? (float[])weights.Clone() : null,
                    hitPointWorld = hitPointWorld,
                    hasSample = hasSample,
                    selectedLayerIndex = selectedLayerIndex
                };
            }
        }

        // current & backup state (for Esc revert)
        private PickerState currentState = new PickerState();
        private PickerState backupState = new PickerState();

        private bool isSampling;

        private Vector2 scrollPos;

        #region Public API

        public TerrainLayer[] GetTerrainLayers()
        {
            return currentState.layers;
        }

        public float[] GetWeights()
        {
            return currentState.weights;
        }

        public static void ShowWindow()
        {
            var window = GetWindow<TerrainLayerPicker>("Terrain Layer Picker");
            window.ShowPopup();
        }

        #endregion Public API

        #region Unity Callbacks

        private void OnEnable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            Event evt = Event.current;
            if (evt == null)
                return;

            int controlId = GUIUtility.GetControlID(FocusType.Passive);

            // when sampling, grab default control so SceneView doesn't select objects on click
            if (isSampling && (evt.type == EventType.Layout || evt.type == EventType.MouseDown || evt.type == EventType.MouseDrag))
            {
                HandleUtility.AddDefaultControl(controlId);
            }

            // handle Esc while sampling
            if (isSampling)
            {
                if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
                {
                    CancelSamplingAndRestore();
                    evt.Use();
                    return;
                }
            }

            // don't do anything else if not in sampling mode
            if (!isSampling)
                return;

            // only react to mouse events if mouse is inside the SceneView window
            Vector2 mouseScreen = GUIUtility.GUIToScreenPoint(evt.mousePosition);
            Rect sceneRect = sceneView.position;
            if (!sceneRect.Contains(mouseScreen))
                return;

            // continuous sampling while hovering
            if (evt.type == EventType.MouseMove || evt.type == EventType.MouseDrag)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);

                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
                {
                    Terrain terrain = hit.collider.GetComponent<Terrain>();
                    if (terrain?.terrainData != null)
                    {
                        SampleTerrainLayers(terrain, hit.point);
                        Repaint();
                    }
                    else
                    {
                        currentState.hasSample = false;
                        currentState.selectedLayerIndex = -1;
                    }
                }
                else
                {
                    currentState.hasSample = false;
                    currentState.selectedLayerIndex = -1;
                }
            }

            // click to pick the layer (dominant at cursor) but don't select the terrain itself
            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                if (currentState.hasSample && currentState.weights != null && currentState.weights.Length > 0)
                {
                    // the list is already sorted, so index 0 is the dominant layer
                    currentState.selectedLayerIndex = 0;
                }

                // consume the click so scene doesn't select terrain or other objects
                evt.Use();

                // commit sampling (keep the picked info) and stop sampling
                isSampling = false;

                SceneView.RepaintAll();

                // repaint invoking editor windows so thumbnails etc. refresh
                EditorUtils.RepaintAllEditorWindows();

                Repaint();
            }
        }

        private void OnGUI()
        {
            // header
            DrawDialogHeader();

            // ESC in the popup window cancels sampling & restores previous state
            Event evt = Event.current;
            if (evt != null && evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape && isSampling)
            {
                CancelSamplingAndRestore();
                evt.Use();
            }

            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Press \"Start Picking\", then hover over a Terrain in the Scene view.\n" +
                "• Hover = live preview of layer weights\n" +
                "• Left click in SceneView = pick layers\n" +
                "• Esc = cancel sampling and restore previous state",
                MessageType.Info);

            // start/stop buttons
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUI.BeginDisabledGroup(isSampling);
                {
                    if (GUILayout.Button("Start Picking", GUILayout.Height(30)))
                    {
                        BeginSampling();
                    }
                }
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(!isSampling);
                {
                    if (GUILayout.Button("Stop Picking", GUILayout.Height(30)))
                    {
                        // just stop sampling, keep whatever is currently sampled
                        isSampling = false;
                        SceneView.RepaintAll();
                        EditorUtils.RepaintAllEditorWindows();
                        Repaint();
                    }
                }
                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndHorizontal();

            // sampling information
            
            EditorGUILayout.LabelField("Sampled Terrain:", currentState.terrain == null ? "" : currentState.terrain.name);
            EditorGUILayout.LabelField("World Position:", currentState.hasSample ? currentState.hitPointWorld.ToString("F2") : "");
            
            // selected layer info
            /*
            if (currentState.selectedLayerIndex >= 0 && currentState.layers != null && currentState.selectedLayerIndex < currentState.layers.Length)
            {
                EditorGUILayout.Space();

                TerrainLayer selectedLayer = currentState.layers[currentState.selectedLayerIndex];
                float selectedWeight = currentState.weights[currentState.selectedLayerIndex] * 100f;
                string selectedName = selectedLayer != null && !string.IsNullOrEmpty(selectedLayer.name)
                    ? selectedLayer.name
                    : $"Layer {currentState.selectedLayerIndex}";
                EditorGUILayout.HelpBox($"Picked Layer: {selectedName} ({selectedWeight:F1}%)", MessageType.None);
            }
            */
            

            // draw grid of sorted layers
            int layerCount = currentState.layers == null || currentState.weights == null
                ? 0
                : Mathf.Min(currentState.layers.Length, currentState.weights.Length);

            if (layerCount == 0)
            {
                EditorGUILayout.HelpBox("The sampled terrain has no terrain layers assigned.", MessageType.Warning);
                return;
            }

            float windowWidth = position.width;
            int columns = Mathf.Max(1, Mathf.FloorToInt(windowWidth / CellWidth));

            // scroll view
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            int index = 0;
            while (index < layerCount)
            {
                EditorGUILayout.BeginHorizontal();

                for (int column = 0; column < columns && index < layerCount; column++, index++)
                {
                    DrawLayerCell(index);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        #endregion Unity Callbacks

        #region Sampling Logic

        /// <summary>
        /// Start sampling and snapshot the current state for possible Esc revert.
        /// </summary>
        private void BeginSampling()
        {
            // update the terrain so that picking accesses the real terraindata
            SyncMicroVerse();

            // backup current state
            backupState = currentState != null ? currentState.Clone() : new PickerState();

            // start fresh sampling
            isSampling = true;
            currentState.hasSample = false;
            currentState.selectedLayerIndex = -1;

            SceneView.RepaintAll();
            Repaint();
        }

        /// <summary>
        /// Cancel sampling and restore the state from before Start Picking was pressed.
        /// </summary>
        private void CancelSamplingAndRestore()
        {
            // restore previous state
            currentState = backupState != null ? backupState.Clone() : new PickerState();

            isSampling = false;

            SceneView.RepaintAll();
            EditorUtils.RepaintAllEditorWindows();
            Repaint();
        }

        /// <summary>
        /// Sample the terrain layers of the specified terrain at the given position.
        /// </summary>
        private void SampleTerrainLayers(Terrain terrain, Vector3 worldPos)
        {
            currentState.terrain = terrain;
            currentState.hitPointWorld = worldPos;

            TerrainData terrainData = terrain.terrainData;
            TerrainLayer[] terrainLayers = terrainData.terrainLayers;

            if (terrainLayers == null || terrainLayers.Length == 0)
            {
                currentState.layers = null;
                currentState.weights = null;
                currentState.hasSample = false;
                currentState.selectedLayerIndex = -1;
                return;
            }

            // convert world position to splatmap indices
            Vector3 localPos = worldPos - terrain.transform.position;

            float normalizedX = Mathf.Clamp01(localPos.x / terrainData.size.x);
            float normalizedZ = Mathf.Clamp01(localPos.z / terrainData.size.z);

            int mapX = Mathf.Clamp(Mathf.RoundToInt(normalizedX * (terrainData.alphamapWidth - 1)), 0, terrainData.alphamapWidth - 1);
            int mapZ = Mathf.Clamp(Mathf.RoundToInt(normalizedZ * (terrainData.alphamapHeight - 1)), 0, terrainData.alphamapHeight - 1);

            float[,,] alpha = terrainData.GetAlphamaps(mapX, mapZ, 1, 1);

            int layerCount = alpha.GetLength(2);
            float[] weights = new float[layerCount];

            for (int i = 0; i < layerCount; i++)
            {
                weights[i] = alpha[0, 0, i];
            }

            // sort by percentage (descending)
            int[] layerIndexes = Enumerable.Range(0, layerCount)
                                           .OrderByDescending(i => weights[i])
                                           .ToArray();

            currentState.layers = new TerrainLayer[layerCount];
            currentState.weights = new float[layerCount];

            for (int i = 0; i < layerCount; i++)
            {
                int layerIndex = layerIndexes[i];
                currentState.layers[i] = (layerIndex < terrainLayers.Length) ? terrainLayers[layerIndex] : null;
                currentState.weights[i] = weights[layerIndex];
            }

            currentState.hasSample = true;
            currentState.selectedLayerIndex = -1; // reset selection when sampling a new point
        }

        #endregion Sampling Logic

        #region MicroVerse

        /// <summary>
        /// It's important to have MicroVerse synced to the terrain, otherwise the terrain layer detection might not be accurate.
        /// </summary>
        private void SyncMicroVerse()
        {
            MicroVerse.instance.Modify(true, true, false);
            MicroVerse.instance.SaveBackToTerrain();
            MicroVerse.instance.SyncTerrainList();
        }

        #endregion MicroVerse

        #region UI Helpers

        private void DrawLayerCell(int index)
        {
            TerrainLayer layer = currentState.layers[index];
            float weight = currentState.weights[index];
            float percentage = weight * 100f;

            bool isSelected = (index == currentState.selectedLayerIndex);

            Color prevColor = GUI.backgroundColor;
            if (isSelected)
            {
                GUI.backgroundColor = new Color(0.5f, 0.8f, 1f, 1f);
            }

            EditorGUILayout.BeginVertical("box", GUILayout.Width(CellWidth));

            // Texture preview
            Texture2D previewTex = layer != null ? layer.diffuseTexture : null;
            Rect area = GUILayoutUtility.GetRect(TextureSize, TextureSize);

            if (previewTex != null)
            {
                // top-left aligned
                Rect drawRect = new Rect(
                    area.x,
                    area.y,
                    TextureSize,
                    TextureSize
                );

                // DrawPreviewTexture so RGB/sRGB don't darken the preview
                EditorGUI.DrawPreviewTexture(drawRect, previewTex, null, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.Box(area, "No Texture");
            }

            // layer name
            string layerName = layer != null && !string.IsNullOrEmpty(layer.name) ? layer.name : $"Layer {index}";
            EditorGUILayout.LabelField(layerName, EditorStyles.miniBoldLabel, GUILayout.Width(CellWidth));

            // percentage text
            EditorGUILayout.LabelField($"{percentage:F1}%", EditorStyles.miniLabel, GUILayout.Width(CellWidth));

            // simple progress bar
            Rect rect = GUILayoutUtility.GetRect(CellWidth, 12f);
            EditorGUI.ProgressBar(rect, weight, "");

            EditorGUILayout.EndVertical();

            GUI.backgroundColor = prevColor;
        }

        private void DrawDialogHeader()
        {
            // common header
            GUIUtils.DrawHeader("Terrain Layer Picker", ref helpBoxVisible);

            // help
            if (helpBoxVisible)
            {
                EditorGUILayout.HelpBox(
                    "Picker for terrain layers on a terrain."
                    + "\n"
                    + "Used in different tools to retrieve the terrain layers, eg for applying a texture filter.",
                    MessageType.Info);
            }
        }

        #endregion UI Helpers
    }
}
