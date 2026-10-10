using JBooth.MicroVerseCore;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.EditorCoroutines.Editor;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

#if __MICROVERSE_SPLINES__
using UnityEngine.Splines;
#endif

namespace Rowlan.Genesis
{
#if __MICROVERSE_SPLINES__
    public class SplineShape
    {
        private SplineContainer splineContainer;
        private Bounds totalWorldBounds;
        private Spline spline;
        private Vector2 deformationAngle = Vector2.zero;
        private int subdivisionIterations = 4;
        private Vector2 subdivisionRange = new Vector2(0.5f, 0.5f);
        private float scale = 0.6f;
        private bool autoAlign = false;

        private SplineAlignment splineAlignment;

        public SplineShape( SplineContainer splineContainer, Spline spline, Bounds totalWorldBounds, Vector2 deformationAngle, int subdivisionIterations, Vector2 subdivisionRange, float scale, bool autoAlign)
        {
            this.splineContainer = splineContainer;
            this.totalWorldBounds = totalWorldBounds;
            this.spline = spline;
            this.deformationAngle = deformationAngle;
            this.subdivisionIterations = subdivisionIterations;
            this.subdivisionRange = subdivisionRange;
            this.scale = scale;
            this.autoAlign = autoAlign;

            this.splineAlignment = new SplineAlignment(null);
        }

        public void Apply(PathSettings.Shape shape)
        {
            switch (shape)
            {
                case PathSettings.Shape.Line:
                    spline.Closed = false;
                    CreateLine();
                    break;
                case PathSettings.Shape.Rectangle:
                    spline.Closed = true;
                    CreateRectangle();
                    break;
                case PathSettings.Shape.RoundedRectangle:
                    spline.Closed = true;
                    CreateRoundedRectangle();
                    break;
                case PathSettings.Shape.Circle:
                    spline.Closed = true;
                    CreateCircle();
                    break;

                case PathSettings.Shape.Random:
                    spline.Closed = true;
                    CreateRandomShape();
                    break;
            }
        }

        public void Apply( Bounds bounds)
        {
            Vector3[] positions = CreateBoundsPositions(bounds);
            BezierKnot[] knots = positions.Select(x => new BezierKnot(x)).ToArray();

            ApplyPositions(knots, TangentMode.AutoSmooth);
        }

        private void CreateLine()
        {
            // calculate terrain bounds
            Vector3[] positions = CreateLinePositions();
            BezierKnot[] knots = positions.Select(x => new BezierKnot(x)).ToArray();

            ApplyPositions(knots, TangentMode.AutoSmooth);
        }

        private void CreateRectangle()
        {
            Vector3[] positions = CreateRectanglePositions();
            BezierKnot[] knots = positions.Select(x => new BezierKnot(x)).ToArray();

            ApplyPositions(knots, TangentMode.Linear);

        }

        private void CreateRoundedRectangle()
        {
            Vector3[] positions = CreateRectanglePositions();
            BezierKnot[] knots = positions.Select(x => new BezierKnot(x)).ToArray();

            ApplyPositions(knots, TangentMode.AutoSmooth);
        }

        private void CreateCircle()
        {
            Vector3[] positions = CreateRectanglePositions();
            BezierKnot[] knots = positions.Select(x => new BezierKnot(x)).ToArray();

            // modify tangents
            // (4/3)*tan(pi/8) = 4*(sqrt(2)-1)/3 = 0.552284749831
            // https://stackoverflow.com/questions/1734745/how-to-create-circle-with-b%C3%A9zier-curves
            float tangentSize = 0.552284749831f * Mathf.Sqrt(totalWorldBounds.size.x * totalWorldBounds.size.x + totalWorldBounds.size.z * totalWorldBounds.size.z) / 2f * scale;

            Unity.Mathematics.float3 tangent = new Unity.Mathematics.float3(0f, 0f, tangentSize);

            knots[0] = new BezierKnot(knots[0].Position, tangent, tangent, Quaternion.Euler(0f, 45, 0f));
            knots[1] = new BezierKnot(knots[1].Position, tangent, tangent, Quaternion.Euler(0f, 135, 0f));
            knots[2] = new BezierKnot(knots[2].Position, tangent, tangent, Quaternion.Euler(0f, 225, 0f));
            knots[3] = new BezierKnot(knots[3].Position, tangent, tangent, Quaternion.Euler(0f, 315, 0f));

            ApplyPositions(knots, TangentMode.Mirrored);

        }

        private void CreateRandomShape()
        {
            Vector3[] positions = CreateRandomShapePositions();
            BezierKnot[] knots = positions.Select(x => new BezierKnot(x)).ToArray();

            ApplyPositions(knots, TangentMode.AutoSmooth);
        }

        private Vector3[] CreateLinePositions()
        {
            Vector3 startPosition = new Vector3(
                totalWorldBounds.center.x - totalWorldBounds.extents.x * scale,
                0f,
                totalWorldBounds.center.z + totalWorldBounds.extents.z * scale
                );

            Vector3 endPosition = new Vector3(
                totalWorldBounds.center.x + totalWorldBounds.extents.x * scale,
                0f,
                totalWorldBounds.center.z - totalWorldBounds.extents.z * scale
            );

            return new Vector3[] { startPosition, endPosition };

        }
        private Vector3[] CreateRectanglePositions()
        {
            Vector3 topLeft = new Vector3(
                totalWorldBounds.center.x - totalWorldBounds.extents.x * scale,
                0f,
                totalWorldBounds.center.z + totalWorldBounds.extents.z * scale
                );

            Vector3 topRight = new Vector3(
                totalWorldBounds.center.x + totalWorldBounds.extents.x * scale,
                0f,
                totalWorldBounds.center.z + totalWorldBounds.extents.z * scale
            );

            Vector3 bottomRight = new Vector3(
                totalWorldBounds.center.x + totalWorldBounds.extents.x * scale,
                0f,
                totalWorldBounds.center.z - totalWorldBounds.extents.z * scale
            );

            Vector3 bottomLeft = new Vector3(
                totalWorldBounds.center.x - totalWorldBounds.extents.x * scale,
                0f,
                totalWorldBounds.center.z - totalWorldBounds.extents.z * scale
            );

            return new Vector3[] { topLeft, topRight, bottomRight, bottomLeft };
        }

        private Vector3[] CreateRandomShapePositions()
        {
            int pointCount = 4;

            List<Vector3> positions = new List<Vector3>();

            for( int i=0; i < pointCount; i++)
            {
                Vector2 point = PolygonUtils.GetRandomPointXZ(totalWorldBounds, scale);

                Vector3 position = new Vector3( point.x, 0f, point.y);

                positions.Add(position);
            }

            return positions.ToArray();
        }

        private Vector3[] CreateBoundsPositions( Bounds bounds)
        {
            Vector3 topLeft = new Vector3(
                bounds.center.x - bounds.extents.x * scale,
                bounds.center.y - bounds.extents.y * scale,
                bounds.center.z + bounds.extents.z * scale
                );

            Vector3 topRight = new Vector3(
                bounds.center.x + bounds.extents.x * scale,
                bounds.center.y - bounds.extents.y * scale,
                bounds.center.z + bounds.extents.z * scale
            );

            Vector3 bottomRight = new Vector3(
                bounds.center.x + bounds.extents.x * scale,
                bounds.center.y - bounds.extents.y * scale,
                bounds.center.z - bounds.extents.z * scale
            );

            Vector3 bottomLeft = new Vector3(
                bounds.center.x - bounds.extents.x * scale,
                bounds.center.y - bounds.extents.y * scale,
                bounds.center.z - bounds.extents.z * scale
            );

            return new Vector3[] { topLeft, topRight, bottomRight, bottomLeft };
        }

        private void ApplyPositions(BezierKnot[] worldKnots, TangentMode tangentMode)
        {
            // create spline
            spline.Clear();

            foreach (BezierKnot knot in worldKnots)
            {
                float3 localKnotPosition = splineContainer.transform.InverseTransformPoint(knot.Position);

                BezierKnot newKnot = new BezierKnot(localKnotPosition, knot.TangentIn, knot.TangentOut, knot.Rotation);

                spline.Add(newKnot, tangentMode);
            }

            // subdivide
            for (int i = 0; i < subdivisionIterations; i++)
            {
                SplineCreator.SubdivideSpline(spline, subdivisionRange, deformationAngle);
            }

            // align
            if (autoAlign)
            {
                splineAlignment.AlignAllSplines();
            }
        }
    }
#endif
}