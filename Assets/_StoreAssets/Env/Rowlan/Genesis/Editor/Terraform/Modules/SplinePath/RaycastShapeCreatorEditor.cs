#if __MICROVERSE_SPLINES__

using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using static Rowlan.Genesis.Terraform.RaycastShapeCreator;

namespace Rowlan.Genesis.Terraform
{
    /// <summary>
    /// Editor for the creation of a radial raycast shape. Performs radial raycasts and creates an outline. Optionally converts the raycast hit points to a spline.
    /// </summary>
    [CustomEditor(typeof(RaycastShapeCreator))]
    public class RaycastShapeCreatorEditor : Editor
    {
        private static GUIContent descriptionContent = new GUIContent("Perform radial raycast and create a spline along the hit points.\nPerformance will vary depending on the parameters you set.\nSpline container required for spline creation.\n\nNote that Undo is disabled for performance reasons, it would cause a flood of Undo registrations for every spline creation otherwise.");
        private static GUIContent performanceInfoContent = new GUIContent("Automatic spline creation active. This will fire events on every spline change. Performance of automatic spline creation in combination with eg automatic lake mesh creation heavily depends on the performance of the mesh creator you use. Triangulation can be a heavy process, it is recommended to perform the mesh creation manually");
        private static GUIContent experimentalContent = new GUIContent("This feature is experimental and may be extended or removed later.");
        
        private RaycastShapeCreatorEditor editor;
        private RaycastShapeCreator editorTarget;

        public void OnEnable()
        {
            this.editor = this;
            this.editorTarget = target as RaycastShapeCreator;

            if( editorTarget.splineContainer == null)
            {
                editorTarget.splineContainer = editorTarget.GetComponent<SplineContainer>();
            }

            PerformRaycast();
        }

        public override void OnInspectorGUI()
        {
            // initial description
            EditorGUILayout.HelpBox(descriptionContent);
            EditorGUILayout.HelpBox(experimentalContent);

            // warning about auto create spline
            if (editorTarget.autoCreateSpline)
            {
                EditorGUILayout.HelpBox(performanceInfoContent.text, MessageType.Warning);
            }            

            // default inspector
            serializedObject.Update();

            bool changed = DrawDefaultInspector();
            if( changed)
            {
                CreateShape();
            }

            // custom inspector
            if (GUILayout.Button("Create Spline"))
            {
                CreateShape( true);
            }

            serializedObject.ApplyModifiedProperties();
        }


        /// <summary>
        /// Get the main spline from the MicroVerse object
        /// </summary>
        /// <returns></returns>
        private Spline GetMainSpline()
        {
            return editorTarget.splineContainer?.Spline;
        }

        public void OnSceneGUI()
        {
            if (!editorTarget.active)
                return;

            if(editorTarget.transform.hasChanged)
            {
                CreateShape();
            }

            editorTarget.transform.hasChanged = false;
        }

        /// <summary>
        /// Perform the raycast, create spline, reduce points
        /// </summary>
        /// <param name="force"></param>
        private void CreateShape( bool force = false)
        {
            PerformRaycast();

            if (force || editorTarget.autoCreateSpline)
            {
                CreateSpline();

                if (editorTarget.optimizeSpline)
                {
                    ReduceSplinePoints();
                }
            }
        }

        /// <summary>
        /// Perform raycast and update collision data
        /// </summary>
        private void PerformRaycast()
        {
            if (!editorTarget.active)
                return;

#if UNITY_2022_3_OR_NEWER

            // reserve memory for the raycast job
            int totalRayCount = editorTarget.rayCount;

            // ensure space for ray data
            if (editorTarget.raycastResult.Length != totalRayCount)
            {
                editorTarget.raycastResult = new RaycastResult[totalRayCount];
            }

            // single raycast and wait for completion
            NativeArray<RaycastHit> raycastResult = new NativeArray<RaycastHit>(totalRayCount * editorTarget.maxHits, Allocator.TempJob);
            NativeArray<RaycastCommand> raycastCommand = new NativeArray<RaycastCommand>(totalRayCount * editorTarget.maxHits, Allocator.TempJob);

            #region prepare raycast

            // layer mask
            QueryParameters queryParameters = QueryParameters.Default;
            queryParameters.layerMask = editorTarget.layerMask;

            // knot world position
            Vector3 origin = editorTarget.transform.position; 

            // initial direction
            Vector3 direction = editorTarget.transform.forward;

            // rotate around origin in ray count steps
            for (int i = 0; i < editorTarget.rayCount; i++)
            {
                // the offset index per knot
                int arrayIndex = i;

                // calculate diretion
                Vector3 rotatedDirection = Quaternion.AngleAxis(360f / editorTarget.rayCount * i, Vector3.up) * direction; // Vector3.forward;

                // raycast origin
                editorTarget.raycastResult[arrayIndex].origin = origin;

                // store direction
                editorTarget.raycastResult[arrayIndex].raycastDirection = rotatedDirection;

                // setup command
                raycastCommand[arrayIndex] = new RaycastCommand(origin, rotatedDirection, queryParameters);

            }

            #endregion prepare raycast

            #region perform raycast

            // schedule raycast batch
            JobHandle jobHandle = RaycastCommand.ScheduleBatch(raycastCommand, raycastResult, 1, editorTarget.maxHits, default(JobHandle));

            // wait for job complection
            jobHandle.Complete();

            #endregion perform raycast

            #region process raycast result

            // set raycast data in the raycast result structure
            for (int i = 0; i < editorTarget.rayCount; i++)
            {
                // the offset index per knot
                int arrayIndex = i;

                if (raycastResult[arrayIndex].collider != null)
                {
                    // logging
                    if (editorTarget.logEnabled)
                    {
                        Debug.Log($"Ray {arrayIndex} hit at: {raycastResult[arrayIndex].collider.name}");
                    }

                    // clamp max distance
                    Vector3 hitPosition = raycastResult[arrayIndex].point;
                    float distance = (hitPosition - editorTarget.raycastResult[arrayIndex].origin).magnitude;
                    if (distance > editorTarget.maxLineLength)
                    {
                        hitPosition = editorTarget.raycastResult[arrayIndex].origin + editorTarget.raycastResult[arrayIndex].raycastDirection * editorTarget.maxLineLength;
                    }

                    editorTarget.raycastResult[arrayIndex].hitPosition = hitPosition;
                    editorTarget.raycastResult[arrayIndex].wasHit = true;

                }
                else
                {
                    editorTarget.raycastResult[arrayIndex].hitPosition = editorTarget.transform.position + editorTarget.raycastResult[arrayIndex].raycastDirection * editorTarget.maxLineLength;
                    editorTarget.raycastResult[arrayIndex].wasHit = false;
                }

                if (editorTarget.additionalLineLength != 0)
                {
                    editorTarget.raycastResult[arrayIndex].hitPosition += editorTarget.raycastResult[arrayIndex].raycastDirection * editorTarget.additionalLineLength;
                }
            }


            #endregion process raycast result

            #region cleanup

            raycastResult.Dispose();
            raycastCommand.Dispose();

            #endregion cleanup
#else
            Debug.LogError("Raycast command query parameters is available with Unity 2022.3.x or higher. Please use a more recent version of Unity.");
#endif
        }


        private void CreateSpline()
        {
            if (editorTarget.splineContainer == null)
                return;

            // undo: not performing it, it would be a flood of undos if auto create spline is active
            //Undo.RecordObject(splineContainer, "Create Unity Spline");

            Spline unitySpline = editorTarget.splineContainer.Spline;

            if (unitySpline == null)
                unitySpline = new Spline();
            else
                unitySpline.Clear();

            Vector3[] worldPositions = editorTarget.raycastResult.Select(x => x.hitPosition).ToArray();

            foreach (Vector3 position in worldPositions)
            {
                Vector3 localPosition = editorTarget.splineContainer.transform.InverseTransformPoint(position);

                BezierKnot knot = new BezierKnot(localPosition);

                unitySpline.Add(knot, TangentMode.AutoSmooth);
            }

            editorTarget.splineContainer.Spline = unitySpline;

            EditorUtility.SetDirty(editorTarget.splineContainer);

            // not applying invalidation (might be too intense on performance)
            // MicroVerse.instance?.Invalidate();
        }

        private void ReduceSplinePoints()
        {
            var spline = GetMainSpline();

            SplineUtils.ReduceSplinePoints(spline, editorTarget.reductionEpsilon, editorTarget.splineTension);
        }
    }

}

#endif