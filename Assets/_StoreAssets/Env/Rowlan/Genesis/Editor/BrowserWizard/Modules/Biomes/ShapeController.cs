#if __MICROVERSE_SPLINES__

using JBooth.MicroVerseCore;
using JBooth.MicroVerseCore.Browser;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static Rowlan.Genesis.BrowserWizard.BrowserWizardData;
using UnityEngine.Splines;
using System.Linq;

namespace Rowlan.Genesis.BrowserWizard
{
    public class ShapeController
    {
        private BrowserWizardModule editor;
        private BiomesModule module;

        private SerializedProperty shapeSettings;

        #region voronoi
        private List<List<Vector2>> polygons = new List<List<Vector2>>();
        private List<Vector2> sites = new List<Vector2>();
        #endregion voronoi

        public ShapeController(BrowserWizardModule editor, BiomesModule module)
        {
            this.editor = editor;
            this.module = module;

            shapeSettings = editor.GetSerializedObject().FindProperty("shapeSettings");

            Init();
        }

        public void Init()
        {
            if (MicroVerse.instance != null)
            {
                // derive area default values from terrain bounds
                Terrain[] terrains = MicroVerse.instance.GetComponentsInChildren<Terrain>();
                Bounds worldBounds = TerrainUtil.ComputeTerrainBounds(terrains);

                // generate initial voronoi diagram
                GenerateVoronoi();
            }

        }

        public void OnInspectorGUI( ContentBrowser browser)
        {

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(shapeSettings, true);

                if (check.changed)
                {
                    // apply properties, we use the settings object
                    editor.GetSerializedObject().ApplyModifiedProperties();

                    // regenerate voronoi
                    GenerateVoronoi();
                }
            }
            

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button(new GUIContent("New Voronoi Diagram", "Create voronoi diagram and preview it")))
                {
                    // apply properties, we use the settings object
                    editor.GetSerializedObject().ApplyModifiedProperties();

                    // new seed
                    editor.GetEditorTarget().shapeSettings.layout.seed = Random.Range(0,100000);

                    // regenerate voronoi
                    GenerateVoronoi();
                }

                if (GUILayout.Button(new GUIContent("Create Splines", "Create spline areas using the created voronoi diagram")))
                {
                    CreateOrUpdateSplineAreas();
                }

                if ( GUILayout.Button(new GUIContent( "Align Splines", "Align splines to terrain")))
                {
                    MicroVerse.instance.AlignAndCenterSplines(editor.GetEditorTarget().splineContainer?.gameObject);

                    MicroVerse.instance.Invalidate();
                }
                /*
                if (GUILayout.Button(new GUIContent("New Voronoi & Splines", "Create voronoi diagram and spline areas using the created voronoi diagram")))
                {
                    GenerateVoronoi();

                    if (CreateSplineObjects(out List<SplineContainer> splineContainers))
                    {
                        // nothing to do
                    }
                }
                */
            }
            GUILayout.EndHorizontal();

            // preview
            GUILayout.Space(10);

            GUILayout.Label("Preview", EditorStyles.boldLabel);

            // draw voronoi diagram preview in remaining area
            DrawPreview();

        }

        /// <summary>
        /// Create new splines and potentially update existing biomes with those splines.
        /// If a biomes container exists, then the spline areas are re-assigned to the new splines.
        /// </summary>
        private void CreateOrUpdateSplineAreas()
        {
            GameObject splineGroup = editor.GetEditorTarget()?.splineContainer;
            GameObject biomesGroup = editor.GetEditorTarget()?.biomesContainer;

            bool isCreateNew = splineGroup == null || biomesGroup == null;

            // create entirely new
            if (isCreateNew)
            {
                if (CreateSplineObjects(out List<SplineContainer> splineContainers))
                {
                    // nothing to do
                }
            }
            // update existing
            else
            {
                // register existing spline area mapping
                // map of <filter,spline gameobject filter index> ... index is just the sequential number used for grouping and reassignment
                Dictionary<FalloffFilter, int> filterIndexMap = new Dictionary<FalloffFilter, int>();

                // previous assignments, ie the spline assignments before new splines were created
                List<SplineContainer> currSplineContainers = splineGroup.GetComponentsInChildren<SplineContainer>().ToList();

                FalloffOverride[] falloffOverrides = biomesGroup.GetComponentsInChildren<FalloffOverride>();
                foreach (FalloffOverride falloffOverride in falloffOverrides)
                {
                    // only register splines
                    bool hasSpline = falloffOverride.filter.filterType == FalloffFilter.FilterType.SplineArea && falloffOverride.filter.splineArea != null;
                    if (!hasSpline)
                        continue;

                    // get index of current spline container
                    int index = currSplineContainers.IndexOf(falloffOverride.filter.splineArea.spline);

                    if (index == -1)
                        continue;

                    filterIndexMap.Add(falloffOverride.filter, index);
                }

                // create new splines
                if (CreateSplineObjects(out List<SplineContainer> splineContainers))
                {
                    foreach( var item in filterIndexMap)
                    {
                        FalloffFilter filter = item.Key;
                        int currIndex = item.Value;

                        // map the current index to the one of the new spline
                        int newIndex = currIndex % splineContainers.Count;

                        // assing new spline area and container
                        // need to assign the full spline area, assigning the container only isn't sufficient
                        filter.splineArea = splineContainers[newIndex].transform?.GetComponent<SplineArea>();

                        EditorUtility.SetDirty(filter.splineArea);
                    }
                }
            }
        }

        void GenerateVoronoi()
        {
            ShapeSettings settings = editor.GetEditorTarget().shapeSettings;

            int seed = settings.layout.seed;
            int numberOfSites = settings.layout.sitesToCreate;

            Bounds terrainBounds = HeightStampUtils.GetTerrainWorldBounds();
            Rect bounds2D = new Rect(0, 0, terrainBounds.size.x, terrainBounds.size.z);

            polygons = VoronoiGenerator.GenerateVoronoi(
                bounds2D, 
                numberOfSites, 
                seed
                );

            if (settings.deformer.enabled)
            {
                polygons = VoronoiDeformer.Deform(
                    bounds2D,
                    polygons,
                    settings.deformer.maxSubdivisionLength,
                    settings.deformer.edgeOverlap,
                    settings.deformer.frequency,
                    settings.deformer.amplitude
                    );
            }
        }

        /// <summary>
        /// Draw voronoi diagram preview in remaining area
        /// </summary>
        void DrawPreview()
        {
            // get total bounds for preview scale
            Bounds terrainBounds = HeightStampUtils.GetTerrainWorldBounds();

            float totalWidth = terrainBounds.size.x;
            float totalHeight = terrainBounds.size.z;

            // preview dimensions and scale
            PolygonPreview.CalculatePreviewArea(totalWidth, totalHeight, out Rect previewRect, out Vector2 scale);

            // preview background
            PolygonPreview.DrawBackground(previewRect);

            // draw each Voronoi cell.
            PolygonPreview.DrawPolygons(previewRect, scale, polygons);

            // draw sites as dots
            PolygonPreview.DrawDots(previewRect, scale, sites);
        }

        private bool CreateSplineObjects( out List<SplineContainer> splineContainers)
        {
            splineContainers = null;

            if (polygons == null || polygons.Count == 0)
            {
                Debug.LogWarning("No polygons generated. Please generate polygons first.");
                return false;
            }

            // Create a parent GameObject for organization.
            GameObject parentGO = GetOrCreateSubContainer("Splines", ref editor.GetEditorTarget().splineContainer);

            if (parentGO == null)
                return false;

            module.Clear(parentGO);

            BiomesTangentMode biomesTangentMode = editor.GetEditorTarget().shapeSettings.spline.tangentMode;
            float splineTension = editor.GetEditorTarget().shapeSettings.spline.tension;

            // map tangent mode
            TangentMode tangentMode = TangentMode.Linear;
            switch (biomesTangentMode)
            {
                case BiomesTangentMode.Linear:
                    tangentMode = TangentMode.Linear;
                    break;
                case BiomesTangentMode.AutoSmooth:
                    tangentMode = TangentMode.AutoSmooth;
                    break;
            }

            Bounds terrainBounds = HeightStampUtils.GetTerrainWorldBounds();

            splineContainers = new List<SplineContainer>();

            for (int i = 0; i < polygons.Count; i++)
            {
                List<Vector2> poly = polygons[i];
                if (poly == null || poly.Count < 3)
                    continue;

                GameObject splineGO = new GameObject("Spline " + i);

                splineGO.transform.parent = parentGO.transform;
                float splineY = splineGO.transform.position.y; // Use this as the spline's height.

                SplineContainer container = splineGO.AddComponent<SplineContainer>();
                Spline spline = new Spline();

                // create voronoi polygons using spline knots
                foreach (var v in poly)
                {
                    Vector3 pos = new Vector3(v.x + terrainBounds.center.x - terrainBounds.extents.x, splineY, v.y + terrainBounds.center.z - terrainBounds.extents.z);
                    BezierKnot knot = new BezierKnot(pos);
                    spline.Add(knot);
                }

                // auto smooth, spline tension
                {
                    SplineRange splineRange = new SplineRange(0, spline.Count);

                    // Sets the tangent mode for all knots in the spline to "Auto Smooth."
                    spline.SetTangentMode(splineRange, tangentMode);

                    // Sets the tension parameter for all knots. Note that the "Tension" parameter is only applicable to
                    // "Auto Smooth" mode knots.
                    spline.SetAutoSmoothTension(splineRange, splineTension);
                }

                spline.Closed = true;
                container.Spline = spline;

                // create spline area on spline; required if eg splines are recreated and the assigned area is supposed to be reused
                SplineArea splineArea = splineGO.transform.gameObject.AddComponent<SplineArea>();
                splineArea.spline = container;

                // register container
                splineContainers.Add(container);
            }

            MicroVerse.instance.AlignAndCenter(splineContainers.ToArray());

            return true;
        }

        private GameObject GetOrCreateSubContainer( string name, ref GameObject slot)
        {
            if (slot != null)
                return slot;

            Transform parentTransform = module.GetRootContainer()?.transform;

            if (parentTransform == null)
            {
                slot = null;
                return null;
            }

            GameObject parentGO = new GameObject(name);

            Undo.RegisterCreatedObjectUndo(parentGO, $"Create {name}");
            Undo.SetTransformParent(parentGO.transform, parentTransform, $"Create {name}");

            slot = parentGO;

            return parentGO;
        }

    }
}
#endif
