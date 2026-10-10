#if __MICROVERSE_SPLINES__

using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;

using UnityEngine.Splines;

namespace Rowlan.Genesis.SplineDeformer
{
    public class StackedDeformerController
    {
        private SplineDeformerModule editor;

        private StackedDeformerSettings settings;

        private StackedDeformerBatchProcessor batchProcessor = new StackedDeformerBatchProcessor();

        // internally used by the inspector to detect if the spline container changed
        private SplineContainer prevSplineContainer = null;

        public StackedDeformerController(SplineDeformerModule editor)
        {
            this.editor = editor;

            settings = ScriptableObject.CreateInstance<StackedDeformerSettings>();

        }

        public void OnInspectorGUI()
        {
            bool hasSpline = GetSplineContainer() != null;

            bool splineContainerChanged = prevSplineContainer != GetSplineContainer();
            if (splineContainerChanged)
            {
                prevSplineContainer = GetSplineContainer();
                ProcessStack();
            }

            if (hasSpline)
            {
                DrawStackSettings();

                EditorGUILayout.Space();

                // Apply final button
                if (GUILayout.Button("Apply Final to Spline", GUILayout.Height(30)))
                {
                    ApplyFinalToSpline();
                }

                EditorGUILayout.Space();

                DrawPreviewToggles();

                EditorGUILayout.Space();

                DrawPreview();

                // preview settings
                EditorGUILayout.BeginHorizontal();
                {
                    settings.previewAutoUpdate = EditorGUILayout.Toggle("Preview Auto Update", settings.previewAutoUpdate);

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Manual Update"))
                    {
                        ProcessStack();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawStackSettings()
        {
            EditorGUI.BeginChangeCheck();
            {

                // Subdivision controls
                EditorGUILayout.LabelField("Subdivision", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                settings.subdivision.maxSegmentLength = EditorGUILayout.Slider("Max Segment Length", settings.subdivision.maxSegmentLength, 10f, 100f);
                settings.subdivision.maxSubdivisions = EditorGUILayout.IntSlider("Max Passes", settings.subdivision.maxSubdivisions, 0, 5);
                EditorGUI.indentLevel--;

                EditorGUILayout.Space();

                // Tangent Noise modifier
                EditorGUILayout.LabelField("Tangent Noise", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                settings.tangentNoise.noiseAmplitude = EditorGUILayout.FloatField("Amplitude", settings.tangentNoise.noiseAmplitude);
                settings.tangentNoise.noiseSeed = EditorGUILayout.IntField("Seed", settings.tangentNoise.noiseSeed);
                EditorGUI.indentLevel--;

                EditorGUILayout.Space();

                // Chaikin smoothing
                EditorGUILayout.LabelField("Chaikin Smoothing", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                settings.chaikinSmoothing.chaikinIterations = EditorGUILayout.IntSlider("Iterations", settings.chaikinSmoothing.chaikinIterations, 0, 4);
                EditorGUI.indentLevel--;

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Max Knots", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                settings.reduction.maxKnots = EditorGUILayout.IntSlider("Max Knots", settings.reduction.maxKnots, 4, 100);
                EditorGUI.indentLevel--;
            }
            if (EditorGUI.EndChangeCheck())
            {
                if( settings.previewAutoUpdate)
                {
                    ProcessStack();
                }
                
            }
        }

        private void DrawPreviewToggles()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                settings.source.previewVisible = GUILayout.Toggle(settings.source.previewVisible, "Original", EditorStyles.toolbarButton);
                settings.subdivision.previewVisible = GUILayout.Toggle(settings.subdivision.previewVisible, "Subdivision", EditorStyles.toolbarButton);
                settings.tangentNoise.previewVisible = GUILayout.Toggle(settings.tangentNoise.previewVisible, "Noise", EditorStyles.toolbarButton);
                settings.chaikinSmoothing.previewVisible = GUILayout.Toggle(settings.chaikinSmoothing.previewVisible, "Chaikin", EditorStyles.toolbarButton);
                settings.target.previewVisible = GUILayout.Toggle(settings.target.previewVisible, "Final", EditorStyles.toolbarButton);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPreview()
        {
            // draw previews horizontally
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            {
                if (settings.source.previewVisible) PolygonPreview.DrawPreviewXZ(batchProcessor.worldPoints.original, Color.white);
                if (settings.subdivision.previewVisible) PolygonPreview.DrawPreviewXZ(batchProcessor.worldPoints.subdivision, Color.green);
                if (settings.tangentNoise.previewVisible) PolygonPreview.DrawPreviewXZ(batchProcessor.worldPoints.noise, Color.cyan);
                if (settings.chaikinSmoothing.previewVisible) PolygonPreview.DrawPreviewXZ(batchProcessor.worldPoints.chaikin, Color.yellow);
                if (settings.target.previewVisible) PolygonPreview.DrawPreviewXZ(batchProcessor.worldPoints.final, Color.green);
            }
            EditorGUILayout.EndHorizontal();
        }


        public SplineContainer[] GetSplineContainers()
        {
            if (editor.GetEditorTarget().autoAssignment)
            {
                return editor.GetSelectedSplineContainers();
            }
            else if (editor.GetEditorTarget().splineContainer != null)
            {
                return new SplineContainer[] { editor.GetEditorTarget().splineContainer };
            }

            return new SplineContainer[0];
        }


        public SplineContainer GetSplineContainer()
        {
            return editor.GetEditorTarget().splineContainer;
        }

        /// <summary>
        /// Take the spline as source and apply the stack deformation to it
        /// </summary>
        private void ProcessStack()
        {
            batchProcessor.ProcessStack(GetSplineContainers(), settings, editor.GetEditorTarget().splineIndex);
        }

        /// <summary>
        /// Calculate final polygon and apply it to the spline using local points
        /// </summary>
        private void ApplyFinalToSpline()
        {
            foreach (SplineContainer container in batchProcessor.GetSplineContainers())
            {
                StackedDeformerProcessor processor = batchProcessor.GetProcessor(container);

                SplineUtils.ApplyToSpline(processor.pointStack.finalPoints, container);
            }

            // align knots to terrain, center main handle
            MicroVerse.instance.AlignAndCenter(batchProcessor.GetSplineContainers());

            // original spline changed => update original settings and stack
            ProcessStack();

            // update scene
            UpdateMicroVerse();
        }

        /// <summary>
        /// Update the stamps that are associated with the spline containers
        /// </summary>
        private void UpdateMicroVerse()
        {
            // refresh biomes
            // TODO: this is just a quick hack to update the biomes. need to find proper mechanism
            foreach (SplineContainer container in batchProcessor.GetSplineContainers())
            {
                // skip inactive ones, we don't want to reactivate them
                if (!container.transform.gameObject.activeInHierarchy)
                    continue;

                container.transform.gameObject.SetActive(false);
                container.transform.gameObject.SetActive(true);
            }

            MicroVerse.instance.Invalidate();

            SceneView.lastActiveSceneView.Repaint();

        }


    }
}
#endif