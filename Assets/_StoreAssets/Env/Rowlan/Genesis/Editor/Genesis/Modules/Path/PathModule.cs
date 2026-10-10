using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using JBooth.MicroVerseCore;
using System.Linq;
using static Rowlan.Genesis.GenesisData;

#if __MICROVERSE_SPLINES__
using UnityEngine.Splines;
#endif

namespace Rowlan.Genesis
{
#if __MICROVERSE_SPLINES__
    public class PathModule : IGenesisModule, ITemplateProcessor
    {
        private GenesisDataEditor editor;

        private SerializedProperty data;

        private SplineAlignment splineAlignment;

        private TemplateProvider<PathTemplateCollection> templateProvider;

        private Vector2 scrollPosition = Vector2.zero;

        public PathModule(GenesisDataEditor editor)
        {
            this.editor = editor;

            data = editor.serializedObject.FindProperty("pathSettings");

            this.splineAlignment = new SplineAlignment(editor);

            templateProvider = new TemplateProvider<PathTemplateCollection>(this);

        }

        public void OnInspectorGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            {
                // tools
                GUILayout.BeginVertical("box");
                {
                    EditorGUILayout.LabelField("Tools", GUIStyles.BoxTitleStyle);

                    if (GUILayout.Button(new GUIContent("Align Splines", "Align all spline knots to the surface of the terrain"), GUILayout.Width(120)))
                    {
                        splineAlignment.AlignAllSplines();
                    }
                }
                GUILayout.EndVertical();

                // templates
                GUILayout.BeginVertical("box");
                {
                    EditorGUILayout.LabelField("Presets", GUIStyles.BoxTitleStyle);

                    templateProvider.DrawDropTargets();
                }
                GUILayout.EndVertical();

                // toolbar (clear etc)
                GUILayout.BeginVertical("box");
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(50)))
                        {
                            Undo.RegisterCompleteObjectUndo(editor.editorTarget, "Clear");

                            editor.editorTarget.pathSettings = new PathSettings[0];


                        }
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();

                // settings
                EditorGUILayout.PropertyField(data, true);
            }
            EditorGUILayout.EndScrollView();

        }

        public void Create(GameObject parent)
        {

            GameObject splinePathGO = new GameObject("Paths");

            // parent
            splinePathGO.transform.parent = parent.transform;

            // set group
            splinePathGO.AddComponent<PathGroup>();

            PathSettings[] pathSettingsList = editor.editorTarget.pathSettings;

            // add splat filters
            for (int i = 0; i < pathSettingsList.Length; i++)
            {
                PathSettings settings = pathSettingsList[i];

                if (!settings.active)
                    continue;

                TerrainLayer terrainLayer = settings.terrainLayer;

                // stamp gameobject
                string name = terrainLayer.name + " (Path)";
                GameObject stampGO = new GameObject(name);
                stampGO.transform.parent = splinePathGO.transform;

                // add spline container
                SplineContainer splineContainer = stampGO.AddComponent<SplineContainer>();

                // create spline
                CreateSpline(stampGO, settings);

                // add stamp
                JBooth.MicroVerseCore.SplinePath stamp = stampGO.AddComponent<JBooth.MicroVerseCore.SplinePath>();

                stamp.spline = splineContainer;
                stamp.layer = settings.terrainLayer;

                EditorUtility.SetDirty(splineContainer);

            }

            splineAlignment.AlignAllSplines();
        }

        public void Destroy()
        {
            MicroVerse microVerse = MicroVerse.instance;

            if (!microVerse)
                return;

            PathGroup group = microVerse.GetPathGroup();

            if (!group)
                return;

            UnityEngine.Object.DestroyImmediate(group.gameObject);
        }

        private void CreateSpline(GameObject stampGO, PathSettings settings)
        {
            // spline
            SplineContainer splineContainer = stampGO.GetComponent<SplineContainer>();

            // get the default spline that's implicitly created
            Spline spline = splineContainer.Spline;

            // settings
            spline.Closed = settings.closed;

            // calculate terrain bounds
            Bounds totalWorldBounds = MicroVerse.instance.GetTerrainWorldBounds();

            /*
            Vector3 startPosition = new Vector3(totalWorldBounds.min.x, 0f, totalWorldBounds.min.z);
            Vector3 endPosition = new Vector3(totalWorldBounds.max.x, 0f, totalWorldBounds.max.z);

            // create the spline shape
            SplineCreator.CreateSpline(spline, startPosition, endPosition);
            */

            // we align all at once
            bool autoAlign = false;

            SplineShape splineShape = new SplineShape(
                splineContainer,
                spline,
                totalWorldBounds, 
                settings.deformationAngle,
                settings.subdivisionIterations,
                settings.subdivisionRange,
                settings.scale, 
                autoAlign
                );

            splineShape.Apply(settings.shape);

            // subdivision & deformation
            /*
            Vector2 deformationAngle = settings.deformationAngle;
            int subDivIterations = settings.subDivIterations;

            for (int i = 0; i < subDivIterations; i++)
            {
                SplineCreator.SubdivideSpline(spline, deformationAngle);
            }
            */

        }

        public void Process(ITemplateSettings template, Object[] objects)
        {
            foreach (Object droppedObject in objects)
            {
                // consistency check
                bool accept = droppedObject is TerrainLayer;

                if (!accept)
                {
                    Debug.LogError($"Invalid object {droppedObject} for template {template}");
                    continue;
                }

                PathSettingsTemplate settingsTemplate = template as PathSettingsTemplate;

                PathSettings[] currentObjects = editor.editorTarget.pathSettings;

                // new settings
                PathSettings newSettings = new PathSettings();

                // apply template
                newSettings.Apply(settingsTemplate);

                // apply dropped object data
                newSettings.terrainLayer = droppedObject as TerrainLayer;

                // create new settings
                PathSettings[] newObjects = new PathSettings[] { newSettings };

                // merge current and new settings
                editor.editorTarget.pathSettings = currentObjects.Concat(newObjects).ToArray();


            }
        }

    }
#else
    public class PathModule : IGenesisModule, ITemplateProcessor
    {
        public PathModule(GenesisDataEditor editor)
        {
        }

        public void Create(GameObject parent)
        {
        }

        public void Destroy()
        {
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("MicroVerse module not available", MessageType.Warning);
        }

        public void Process(ITemplateSettings template, Object[] objects)
        {
        }
    }
#endif
}