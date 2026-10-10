using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis
{
    /// <summary>
    /// Utility methods to draw a preview image for polygons and dots
    /// </summary>
    public class PolygonPreview
    {
        /// <summary>
        /// Calculate dimensions for the preview are and the scale
        /// </summary>
        /// <param name="totalWidth"></param>
        /// <param name="totalHeight"></param>
        /// <param name="previewRect"></param>
        /// <param name="scale"></param>
        public static void CalculatePreviewArea(float totalWidth, float totalHeight, out Rect previewRect, out Vector2 scale)
        {
            // get the remaining area
            Rect availableRect = GUILayoutUtility.GetRect(0, 10000, 0, 10000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            // calculate the size of the square: use the smaller dimension for 1:1 ratio
            float squareSize = Mathf.Min(availableRect.width, availableRect.height);

            // arbitrary margin
            squareSize -= 10f;

            // calculate a rect for the square so that it is centered in the available area
            previewRect = new Rect(
                availableRect.x + (availableRect.width - squareSize) / 2,
                availableRect.y + (availableRect.height - squareSize) / 2,
                squareSize,
                squareSize
            );

            // scale conversion from defined area to the preview rectangle.
            float scaleX = previewRect.width / totalWidth;
            float scaleY = previewRect.height / totalHeight;

            scale = new Vector2(scaleX, scaleY);
        }

        /// <summary>
        /// Draw background area
        /// </summary>
        /// <param name="totalWidth"></param>
        /// <param name="totalHeight"></param>
        /// <param name="previewRect"></param>
        /// <param name="scale"></param>
        public static void DrawBackground( Rect previewRect)
        {
            EditorGUI.DrawRect(previewRect, GUIStyles.PreviewAreaBackgroundColor);
            Handles.color = GUIStyles.PreviewAreaForegroundColor;
            Handles.DrawSolidRectangleWithOutline(previewRect, Color.clear, GUIStyles.PreviewAreaForegroundColor);
        }

        /// <summary>
        /// Draw a list of polygons
        /// </summary>
        /// <param name="previewRect"></param>
        /// <param name="scale"></param>
        /// <param name="polygons"></param>
        public static void DrawPolygons(Rect previewRect, Vector2 scale, List<List<Vector2>> polygons)
        {
            foreach (var poly in polygons)
            {
                DrawPolygon(previewRect, scale, poly);
            }
        }

        /// <summary>
        /// Draw a single polygon into the preview area
        /// </summary>
        /// <param name="previewRect"></param>
        /// <param name="scale"></param>
        /// <param name="polygon"></param>
        public static void DrawPolygon(Rect previewRect, Vector2 scale, List<Vector2> polygon)
        {
            if (polygon.Count < 2)
                return;

            Vector3[] previewPoints = new Vector3[polygon.Count + 1];
            for (int i = 0; i < polygon.Count; i++)
            {
                float x = previewRect.x + polygon[i].x * scale.x;
                float y = previewRect.y + previewRect.height - polygon[i].y * scale.y;
                previewPoints[i] = new Vector3(x, y, 0);
            }

            previewPoints[polygon.Count] = previewPoints[0];

            Handles.color = GUIStyles.DiagramLineColor;
            Handles.DrawAAPolyLine(2f, previewPoints);
        }

        /// <summary>
        /// Draw dots with the specified radius, eg voronoi diagram sites
        /// </summary>
        /// <param name="previewRect"></param>
        /// <param name="scale"></param>
        /// <param name="points"></param>
        /// <param name="radius"></param>
        public static void DrawDots(Rect previewRect, Vector2 scale, List<Vector2> points, float radius = 3f)
        {
            Handles.color = GUIStyles.DiagramDotColor;
            foreach (var s in points)
            {
                float x = previewRect.x + s.x * scale.x;
                float y = previewRect.y + previewRect.height - s.y * scale.y;
                Handles.DrawSolidDisc(new Vector3(x, y, 0), Vector3.forward, radius);
            }
        }

        /// <summary>
        /// Draw a preview image for the provided points
        /// </summary>
        /// <param name="pointsList"></param>
        /// <param name="color"></param>
        public static void DrawPreviewXZ(List<List<Vector3>> polygons, Color color)
        {
            CalculateBounds(polygons, out Bounds bounds);

            // X/Z plane: calculate width/height
            float width = bounds.size.x;
            float height = bounds.size.z;

            // preview dimensions and scale
            CalculatePreviewArea(width, height, out Rect previewRect, out Vector2 scale);

            // background
            DrawBackground(previewRect);

            // draw lines and dots
            foreach (List<Vector3> polygon in polygons)
            {
                // X/Z plane: convert XYZ to XZ and offset points by minX/minZ
                List<Vector2> offsetPoints2d = polygon.ConvertAll(p => new Vector2(p.x - bounds.min.x, p.z - bounds.min.z)).ToList();

                // draw lines and dots
                DrawDiagram(offsetPoints2d, previewRect, scale, color, 1f); // radius 1f, we have multiple diagrams
            }
        }

        /// <summary>
        /// Draw a preview image for the provided points
        /// </summary>
        /// <param name="points3d"></param>
        /// <param name="color"></param>
        public static void DrawPreviewXZ(List<Vector3> polygon, Color color)
        {
            CalculateBounds(polygon, out Bounds bounds);

            // X/Z plane: calculate width/height
            float width = bounds.size.x;
            float height = bounds.size.z;

            // preview dimensions and scale
            CalculatePreviewArea(width, height, out Rect previewRect, out Vector2 scale);

            // background
            DrawBackground(previewRect);

            // X/Z plane: convert XYZ to XZ and offset points by minX/minZ
            List<Vector2> offsetPolygonXZ = polygon.ConvertAll(p => new Vector2(p.x - bounds.min.x, p.z - bounds.min.z)).ToList();

            // draw lines and dots
            DrawDiagram( offsetPolygonXZ, previewRect, scale, color);
        }

        /// <summary>
        /// Draw lines and dots wit the given parameters
        /// </summary>
        /// <param name="offsetPoints2D"></param>
        /// <param name="previewRect"></param>
        /// <param name="scale"></param>
        /// <param name="color"></param>
        private static void DrawDiagram(List<Vector2> offsetPolygonXZ, Rect previewRect, Vector2 scale, Color color, float dotRadius = 3f)
        {
            // lines
            var oldColor = GUIStyles.DiagramLineColor;
            GUIStyles.DiagramLineColor = color;
            DrawPolygon(previewRect, scale, offsetPolygonXZ);
            GUIStyles.DiagramLineColor = oldColor;

            // dots
            var oldDot = GUIStyles.DiagramDotColor;
            GUIStyles.DiagramDotColor = color;
            DrawDots(previewRect, scale, offsetPolygonXZ, dotRadius);
            GUIStyles.DiagramDotColor = oldDot;

        }

        /// <summary>
        /// Calculate axis aligned bounding box
        /// On XZ plane you can then use minX/minY: lower-left corner, maxX/maxY: upper-right corner
        /// </summary>
        /// <param name="points3D"></param>
        /// <param name="bounds"></param>
        public static bool CalculateBounds(List<List<Vector3>> polygons, out Bounds bounds)
        {
            // create a single list of all Vector3 in the list of lists
            List<Vector3> flattenedList = polygons.SelectMany(p => p.ToList()).ToList();

            bool success = CalculateBounds(flattenedList, out bounds);

            return success;
        }

        /// <summary>
        /// Calculate axis aligned bounding box
        /// On XZ plane you can then use minX/minY: lower-left corner, maxX/maxY: upper-right corner
        /// </summary>
        /// <param name="points3D"></param>
        /// <param name="bounds"></param>
        public static bool CalculateBounds(List<Vector3> polygon, out Bounds bounds)
        {
            if(polygon.Count == 0)
            {
                bounds = new Bounds();
                return false;
            }

            bounds = new Bounds(polygon[0], Vector3.zero);

            foreach( var point3D in polygon)
            {
                bounds.Encapsulate(point3D);
            }

            return true;
        }
    }
}