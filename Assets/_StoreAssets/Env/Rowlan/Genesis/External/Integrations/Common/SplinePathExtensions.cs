namespace Rowlan.Genesis.Integrations
{

#if UNITY_EDITOR && (USING_RAM2019 || USING_RAM3 || CREST_OCEAN || CREST_5 || USING_KWS)

    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using JBooth.MicroVerseCore;
    using static JBooth.MicroVerseCore.SplinePath;
    using UnityEngine.Splines;
    using UnityEditor;
    using Unity.Mathematics;

    public static class SplinePathExtensions
    {
        /// <summary>
        /// The delta being used for calculation of spline resolution.
        /// If the threshold is exceeded, then a spline won't be drawn.
        /// This is just to avoid long lasting calculations.
        /// </summary>
        private static float minDelta = 0.00001f;  // 0.001

        public static List<float3> GetLocalPositions( Spline spline, List<float> tNormalizedList)
        {
            List<float3> positions = new List<float3>();

            for (int i = 0; i < tNormalizedList.Count; i++)
            {
                // get normalized index
                float tNormalized = tNormalizedList[i];

                // get position
                float3 position = SplineUtility.EvaluatePosition(spline, tNormalized);

                positions.Add( position);
            }

            return positions;
        }

        public static List<Vector4> GetWorldPositions( Transform transform, List<float3> localPositions, float offsetY)
        {
            Matrix4x4 worldMatrix = transform.localToWorldMatrix;

            List<Vector4> worldPositions = new List<Vector4>();

            foreach (Vector3 position in localPositions)
            {
                Vector3 worldPosition = worldMatrix.MultiplyPoint(new Vector3(position.x, position.y + offsetY, position.z));

                worldPositions.Add(worldPosition);
            }

            return worldPositions;
        }

        /// <summary>
        /// Get the spline points in world space
        /// </summary>
        /// <returns></returns>
        public static List<Vector4> GetWorldPositions(JBooth.MicroVerseCore.SplinePath splinePath, int index = 0, float offsetY = 0f)
        {
            Spline unitySpline = splinePath?.spline?[index];

            Matrix4x4 worldMatrix = splinePath.transform.localToWorldMatrix;

            List<Vector4> worldPositions = new List<Vector4>();

            foreach (BezierKnot knot in unitySpline.Knots)
            {
                Vector4 point = worldMatrix.MultiplyPoint(new Vector4(knot.Position.x, knot.Position.y + offsetY, knot.Position.z));

                worldPositions.Add(point);
            }

            return worldPositions;
        }

        public static List<float> GetNormalizedT(SplineContainer splineContainer, int index = 0, Sampling sampling = Sampling.Interpolate, float interpolationResolution = 1f)
        {
            Spline unitySpline = splineContainer.Splines?[index];

            return GetNormalizedT(unitySpline, sampling, interpolationResolution);
        }

        private static List<float> GetNormalizedT(Spline unitySpline, Sampling sampling, float interpolationResolution)
        {
            // we operate on the normalized position on the spline which is t = [0..1]
            List<float> tNormalizedList = new List<float>();

            // collect all normalized knot index points
            if (sampling == Sampling.Discrete)
            {
                for (int knotIndex = 0; knotIndex < unitySpline.Count; knotIndex++)
                {
                    float tNormalized = unitySpline.ConvertIndexUnit(knotIndex, PathIndexUnit.Knot, PathIndexUnit.Normalized);
                    tNormalizedList.Add(tNormalized);
                }
            }
            // collect interpolated points, good for eg bezier curves
            else if (sampling == Sampling.Interpolate)
            {
                float length = unitySpline.GetLength();

                if (length <= minDelta)
                {
                    Debug.LogError($"Length delta reached: {length} <= {minDelta}");
                    return new List<float>();
                }


                float segmentsPerLength = interpolationResolution * length;
                int segments = Mathf.CeilToInt(segmentsPerLength);
                float segmentStepT = (1f / interpolationResolution) / length;
                int steps = segments + 1;

                if (segmentStepT <= minDelta)
                {
                    Debug.LogError($"Segment Step T delta reached: {segmentStepT} <= {minDelta}");
                    return new List<float>();
                }

                for (float t = 0; t <= 1; t += segmentStepT)
                {
                    tNormalizedList.Add(t);
                }

                // ensure the last knot is added in case it's off by some arbitrary delta
                float lastKnotDelta = minDelta;
                if (tNormalizedList.Count > 0 && tNormalizedList[tNormalizedList.Count - 1] < (1f - lastKnotDelta))
                {
                    tNormalizedList.Add(1f);
                }
            }

            return tNormalizedList;
        }

        public static void CreateUnitySpline(SplineContainer splineContainer, List<Vector3> worldPositions)
        {
            if (splineContainer == null)
                return;

            // undo
            Undo.RecordObject(splineContainer, "Create Unity Spline");

            Spline unitySpline = splineContainer.Spline;

            if (unitySpline == null)
                unitySpline = new Spline();
            else
                unitySpline.Clear();

            foreach (Vector3 position in worldPositions)
            {
                BezierKnot knot = new BezierKnot(position);

                unitySpline.Add(knot, TangentMode.AutoSmooth);
            }

            splineContainer.Spline = unitySpline;

            EditorUtility.SetDirty(splineContainer);

            MicroVerse.instance?.Invalidate();
        }

        /// <summary>
        /// Ensure successive spline width is equal or wider than previous one (eg for Lava flow)
        /// </summary>
        public static void AdjustFlowWidths(JBooth.MicroVerseCore.SplinePath splinePath)
        {
            Spline spline = splinePath.spline?.Spline;

            if (spline == null || spline.Count == 0)
                return;

            // undo
            Undo.RecordObject(splinePath, "Adjust Flow Widths");

            float prevWidth = float.MinValue;

            List<SplineWidthData> splineWidthDatas = splinePath.splineWidths;

            for (int i = 0; i < splineWidthDatas.Count; i++)
            {
                SplineData<float> splineData = splineWidthDatas[i].widthData;

                for (int j = 0; j < splineData.Count; j++)
                {
                    DataPoint<float> dataPoint = splineData[j];

                    float width = dataPoint.Value;

                    // adjust width
                    width = Mathf.Max(prevWidth, width);

                    dataPoint.Value = width;

                    splineData[j] = dataPoint;

                    prevWidth = width;
                }
            }
        }

        /// <summary>
        /// Ensure the y position of the knots goes down in successive order
        /// </summary>
        public static void AdjustFlowHeights(JBooth.MicroVerseCore.SplinePath splinePath)
        {
            Spline spline = splinePath.spline?.Spline;

            if (spline == null || spline.Count == 0)
                return;

            // undo
            Undo.RecordObject(splinePath.spline, "Adjust Flow Heights");

            // get first y position
            float prevY = spline[0].Position.y;

            // iterate through successive spline positions and adjust if necessary
            for (int i = 1; i < spline.Count; i++)
            {
                BezierKnot knot = spline[i];

                if (knot.Position.y > prevY)
                {
                    knot.Position.y = prevY;
                }

                spline[i] = knot;

                prevY = spline[i].Position.y;
            }
        }

        /// <summary>
        /// Ensure the y position of all knots is the same (minimum y position of all knots)
        /// </summary>
        public static void AdjustLakeHeights(SplineContainer splineContainer)
        {
            Spline spline = splineContainer?.Spline;

            if (spline == null || spline.Count == 0)
                return;

            // undo
            Undo.RecordObject(splineContainer, "Adjust Lake Heights");

            // get min y position
            float minY = spline[0].Position.y;

            for (int i = 1; i < spline.Count; i++)
            {
                minY = Mathf.Min(minY, spline[i].Position.y);
            }

            // set min on all knots
            for (int i = 1; i < spline.Count; i++)
            {
                BezierKnot knot = spline[i];

                knot.Position.y = minY;

                spline[i] = knot;

            }
        }
    }

#endif

}