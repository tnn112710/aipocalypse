#if __MICROVERSE_SPLINES__
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis.SplineDeformer
{
    public class StackedDeformerProcessor
    {
        public class PointStack
        {
            public List<Vector3> originalPoints = new List<Vector3>();
            public List<Vector3> subdivisionPoints = new List<Vector3>();
            public List<Vector3> noisePoints = new List<Vector3>();
            public List<Vector3> chaikinPoints = new List<Vector3>();
            public List<Vector3> finalPoints = new List<Vector3>();

            public void Init(List<Vector3> points)
            {
                originalPoints = new List<Vector3>(points);
                subdivisionPoints = new List<Vector3>(points);
                noisePoints = new List<Vector3>(points);
                chaikinPoints = new List<Vector3>(points);
                finalPoints = new List<Vector3>(points);
            }
        }

        public PointStack pointStack = new PointStack();

        public void ProcessStack(List<Vector3> points, StackedDeformerSettings settings)
        {
            pointStack.Init(points);

            // pipeline: subdivision -> tangent noise -> chaikin -> max knots
            pointStack.subdivisionPoints = PolygonUtils.SubdividePoints(pointStack.originalPoints, settings.subdivision.maxSegmentLength, settings.subdivision.maxSubdivisions);
            pointStack.noisePoints = PolygonUtils.ApplyTangentNoise(pointStack.subdivisionPoints, settings.tangentNoise.noiseAmplitude, settings.tangentNoise.noiseSeed);
            pointStack.chaikinPoints = PolygonUtils.ApplyChaikinSmoothing(pointStack.noisePoints, settings.chaikinSmoothing.chaikinIterations);
            pointStack.finalPoints = PolygonUtils.LimitPoints(pointStack.chaikinPoints, settings.reduction.maxKnots);

            // EditorWindow.GetWindow<SplineDeformerEditorWindow>()?.Repaint();
            // Repaint();
        }
    }
}
#endif