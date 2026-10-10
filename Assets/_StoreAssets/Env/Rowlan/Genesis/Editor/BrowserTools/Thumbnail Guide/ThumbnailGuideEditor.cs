using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.BrowserTools
{
    [CustomEditor(typeof(ThumbnailGuide))]
    public class ThumbnailGuideEditor : Editor
    {
        public ThumbnailGuideEditor editor;
        public ThumbnailGuide editorTarget;

        public void OnEnable()
        {
            editor = this;
            editorTarget = (ThumbnailGuide) target;

        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox("Select this GameObject to have a guide for thumbnail creation visible.\n\nPlease note that a single label height is considered arbitrarily on top, in the Content Browser it depends on the chosen thumbnail dimensions to keep the label at readable size.", MessageType.Info);

            // gizmos not visible warning
            bool gizmosVisible = SceneView.lastActiveSceneView.drawGizmos;
            if (!gizmosVisible)
            {
                EditorGUILayout.HelpBox("Gizmos must be visible in SceneView", MessageType.Warning);
            }

            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();

        }

        public void OnSceneGUI()
        {
            Handles.BeginGUI();

            using (new Handles.DrawingScope(Color.black))
            {
                SceneView sceneView = SceneView.lastActiveSceneView;

                // dimensions
                float width = sceneView.position.width;
                float height = sceneView.position.width / sceneView.camera.aspect;
                float orthosize = height;
                float verticalBoxWidth = (width - orthosize) / 2;

                float boxLeftStartX = 0f;
                float boxLeftWidth = verticalBoxWidth;

                float boxRightStartX = width - verticalBoxWidth;
                float boxRightWidth = verticalBoxWidth;

                float boxCenterStartX = boxLeftStartX + boxLeftWidth;
                float boxCenterWidth = orthosize;

                // background boxes
                {
                    GUI.color = editorTarget.backgroundColor;

                    // left box
                    GUI.Box(new Rect(boxLeftStartX, 0, boxLeftWidth, height), "");

                    // right box
                    GUI.Box(new Rect(boxRightStartX, 0, boxRightWidth, height), "");

                    // center box (label)
                    float centerBlockHeight = height / editorTarget.totalLines;
                    for (int i = 0; i < editorTarget.visibleLines; i++)
                    {
                        float lineHeightStartY = centerBlockHeight * i;

                        GUI.color = createDimColor(editorTarget.backgroundColor, i, editorTarget.visibleLines);

                        GUI.Box(new Rect(boxCenterStartX, lineHeightStartY, boxCenterWidth, centerBlockHeight), "");
                    }
                }

                // lines
                {
                    Handles.color = editorTarget.lineColor;

                    // left line
                    float leftLineX = boxLeftStartX + boxLeftWidth - 1;
                    Vector2 p1Left = new Vector2(leftLineX, 0f);
                    Vector2 p2Left = new Vector2(leftLineX, height);
                    Handles.DrawLine(p1Left, p2Left, 2f);

                    // right line
                    float rightLineX = boxRightStartX + 1;
                    Vector2 p1Right = new Vector2(rightLineX, 0f);
                    Vector2 p2Right = new Vector2(rightLineX, height);
                    Handles.DrawLine(p1Right, p2Right, 2f);

                    // center lines
                    for (int i = 0; i < editorTarget.visibleLines; i++)
                    {
                        float y = height / editorTarget.totalLines * (i + 1); // heuristic with div by 6; assume 6 lines have place in a thumbnail; actual size depends on thumbnail scale. could as well be just 4 lines

                        Vector2 p1Center = new Vector2(leftLineX, y);
                        Vector2 p2Center = new Vector2(rightLineX, y);

                        Handles.color = createDimColor(editorTarget.lineColor, i, editorTarget.totalLines);

                        Handles.DrawLine(p1Center, p2Center, 2f);
                    }
                }

            }

            Handles.EndGUI();
        }

        /// <summary>
        /// Create a gradient color depending on the dim steps. Usually these are data of a for-loop
        /// </summary>
        /// <param name="baseColor"></param>
        /// <param name="dimStep">The current dim step</param>
        /// <param name="dimStepTotal">The total number of dim steps</param>
        /// <returns></returns>
        private Color createDimColor( Color baseColor, int dimStep, int dimStepTotal)
        {
            if (!editorTarget.dim)
                return baseColor;

            // create dim color
            float alpha = 1 - (dimStep / (float) dimStepTotal);
            alpha = alpha * editorTarget.dimFactor;

            Color color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

            return color;

        }
    }
}