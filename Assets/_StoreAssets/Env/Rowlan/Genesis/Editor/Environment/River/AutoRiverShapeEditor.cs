#if __MICROVERSE_SPLINES__
using JBooth.MicroVerseCore;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Rowlan.Genesis.Environment
{
    /// <summary>
    /// Create river shape for a spline by heightmap sampling
    /// </summary>
    [CustomEditor(typeof(AutoRiverShape))]
    public class AutoRiverShapeEditor : Editor
    {

        private AutoRiverShapeEditor editor;
        private AutoRiverShape editorTarget;

        private bool splineChangedEventEnabled = true;

        private TerrainHeightSampler terrainHeightSampler;


        public void OnEnable()
        {
            editor = this;
            editorTarget = (AutoRiverShape)target;

            if (editorTarget.splinePath == null)
                editorTarget.splinePath = editorTarget.GetComponent<JBooth.MicroVerseCore.SplinePath>();

            // install listeners
            Spline.Changed += OnSplineChanged;

            Terrain[] terrains = MicroVerse.instance.GetComponentsInChildren<Terrain>();
            terrainHeightSampler = new TerrainHeightSampler(terrains);
        }

        private void OnDisable()
        {
            // uninstall listeners
            Spline.Changed -= OnSplineChanged;
        }


        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (var check = new EditorGUI.ChangeCheckScope())
            {

                EditorGUILayout.HelpBox("Create a spline river shape by sampling the terrain height. Please disable Spline Path during sampling or otherwise this will affect the height sampling ", MessageType.None);

                base.DrawDefaultInspector();

                if (check.changed)
                {
                    // at least 2 knots
                    editorTarget.riverLength = Mathf.Max(editorTarget.riverLength, 2);

                    // sampling distance >= 1
                    editorTarget.samplingDistance = Mathf.Max(editorTarget.samplingDistance, 1);

                    CreateRiverShape(0, false);
                }
            }

            // info box on high knot count
            int knotSamplingThreshold = 100;
            if (editorTarget.continuous && (editorTarget.riverLength * editorTarget.samplingDistance) > knotSamplingThreshold)
            {
                EditorGUILayout.HelpBox("Your spline knot sampling count for continuous sampling is high. This may affect performance during terrain height sampling.", MessageType.None);
            }

            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Create River Shape"))
                    {
                        CreateRiverShape(0, true);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }


        void OnSplineChanged(Spline spline, int knotIndex, SplineModification modificationType)
        {
            if (!splineChangedEventEnabled)
                return;

            CreateRiverShape(knotIndex, false);
        }

        private SplineContainer GetSplineContainer()
        {
            return editorTarget.splinePath?.spline;
        }

        private Spline GetSpline()
        {
            return editorTarget.splinePath?.spline?.Spline;
        }

        private void AddPositionAt(Vector3 worldPos, List<Vector3> positions)
        {
            if (terrainHeightSampler.GetTerrainHeight(worldPos, out float height))
            {
                Vector3 position = new Vector3(worldPos.x, height, worldPos.z);

                positions.Add(position);
            }
        }

        private Vector3 GetLowestNeighbour(SampleData[] data, Vector3 worldPos)
        {
            foreach (SampleData grid in data)
            {
                Vector3 sampleWorldPos = new Vector3(0, 0, 0);
                sampleWorldPos.x = worldPos.x + grid.x;
                sampleWorldPos.z = worldPos.z + grid.z;

                if(terrainHeightSampler.GetTerrainHeight(sampleWorldPos, out float height))
                {
                    // y is in terrain local pos => convert to world pos
                    grid.h = height;
                }
                else
                {
                    grid.h = 0f;
                }

            }

            SampleData min = GetMin(data);

            float y = min.h; // terrain.transform.localToWorldMatrix.MultiplyPoint(sampleWorldPos).y;

            Vector3 neighbour = new Vector3(worldPos.x + min.x, y, worldPos.z + min.z);

            return neighbour;
        }


        private void CreateRiverShape(int knotIndex, bool force)
        {
            splineChangedEventEnabled = false;

            try
            {
                PerformSampling(knotIndex, force);
            }
            finally
            {
                splineChangedEventEnabled = true;
            }
        }

        /// <summary>
        /// Sample the river
        /// </summary>
        /// <param name="knotIndex"></param>
        /// <param name="force">Force creation of the spline, eg when the button for manual shaping is pressed</param>
        private void PerformSampling(int knotIndex, bool force)
        {
            if (!editorTarget.continuous && !force)
                return;

            Spline spline = GetSpline();

            if (spline == null)
                return;

            if (spline.Count == 0 || spline.Count < knotIndex)
                return;

            int riverLength = editorTarget.riverLength;
            SampleData[] data = CreateSampleData();

            Matrix4x4 splineWorldMatrix = editorTarget.splinePath.transform.localToWorldMatrix;

            List<Vector3> positions = new List<Vector3>();

            // add first as start position
            Vector3 position = splineWorldMatrix.MultiplyPoint(spline[knotIndex].Position);

            AddPositionAt( position, positions);

            // add the rest; we already have a knot => start with +1
            for (int i = knotIndex + 1; i < riverLength; i++)
            {
                position = GetLowestNeighbour( data, position);

                positions.Add(position);
            }

            // remove all starting at the knot index
            // special case knot index 0: spline RemoveAt has a bug, it accesses the next knot => if 0, then we simply clear the spline
            if (knotIndex == 0)
            {
                spline.Clear();
            }
            else
            {
                while (spline.Count > knotIndex)
                {
                    int index = spline.Count - 1;
                    spline.RemoveAt(index);
                }
            }

            Matrix4x4 localMatrix = editorTarget.splinePath.transform.worldToLocalMatrix;
            foreach (Vector3 knotPosition in positions)
            {
                Vector3 localPosition = localMatrix.MultiplyPoint(knotPosition);
                BezierKnot knot = new BezierKnot(localPosition);
                //spline.Add(knot, TangentMode.Linear);
                spline.Add(knot, TangentMode.AutoSmooth);
            }

            Invalidate( force);

        }

        private void Invalidate( bool forceInvalidate)
        {
            EditorUtility.SetDirty(GetSplineContainer()?.transform);

            if (editorTarget.autoRefresh || forceInvalidate)
            {
                MicroVerse.instance.Invalidate();
            }

            SceneView.lastActiveSceneView.Repaint();
        }

        private SampleData GetMin(SampleData[] data)
        {
            SampleData min = data[0];

            for (int i = 1; i < data.Length; i++)
            {
                if (data[i].h < min.h)
                {
                    min = data[i];
                }
            }

            return min;
        }

        private SampleData[] CreateSampleData()
        {

            int d = editorTarget.samplingDistance;

            SampleData[] data = new SampleData[]
            {
                            new SampleData(-d,-d, 0f),
                            new SampleData(0,-d, 0f),
                            new SampleData(d,-d, 0f),
                            new SampleData(d,0, 0f),
                            new SampleData(d,d, 0f),
                            new SampleData(0,d, 0f),
                            new SampleData(-d,d, 0f),
                            new SampleData(-d,0, 0f),
            };

            return data;
        }

        private class SampleData
        {
            public int x;
            public int z;
            public float h;

            public SampleData(int x, int z, float h)
            {
                this.x = x;
                this.z = z;
                this.h = h;
            }
        }


    }
}
#endif