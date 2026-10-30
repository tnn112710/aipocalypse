/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

#if SMARTMOUSE_SPLINES
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace TelePresent.SmartMouse
{
    //Splines are selected by the on-screen Gizmo line's distance to the mouse.
    internal static class SmartMouseSplineHits
    {
        const float PickPixels = 10f;
        const int SamplesPerCurve = 16;
        const float HighlightWidth = 6f;
        const float SortingBias = 0.001f;

        static SplineContainer[] _containers;
        static Vector3[] _points = new Vector3[256];

        public static void Invalidate() => _containers = null;

        public static void CheckSplineHits(Ray ray, List<(float distance, Vector3 point, GameObject obj)> hits)
        {
            Camera camera = Camera.current;
            if (Event.current == null || camera == null || !GizmosVisible()) return;
            if (_containers == null)
                _containers = SmartMouseCompat.FilterToActiveStage(SmartMouseCompat.FindAll<SplineContainer>());

            Vector2 mouse = Event.current.mousePosition;
            var projection = new CameraProjectionCache(camera);
            Vector3 eye = camera.transform.position;
            Vector3 forward = camera.transform.forward;
            Vector3 towardFar = new Vector3(Mathf.Sign(forward.x), Mathf.Sign(forward.y), Mathf.Sign(forward.z));

            foreach (SplineContainer container in _containers)
            {
                if (container == null) continue;
                GameObject go = container.gameObject;
                if (!go.activeInHierarchy || !SmartMouseSettings.IncludesLayer(go.layer) || Contains(hits, go)) continue;

                Matrix4x4 localToWorld = container.transform.localToWorldMatrix;
                IReadOnlyList<Spline> splines = container.Splines;
                for (int i = 0; i < splines.Count; i++)
                {
                    Spline spline = splines[i];
                    if (spline == null || spline.Count < 2) continue;
                    Bounds bounds = SplineUtility.GetBounds(spline, (float4x4)localToWorld);
                    Vector3 farCorner = bounds.center + Vector3.Scale(bounds.extents, towardFar);
                    bounds.Expand(2f * PickPixels / 80f * HandleUtility.GetHandleSize(farCorner));
                    if (!bounds.IntersectRay(ray)) continue;

                    int count = Sample(spline, localToWorld, ref _points);
                    if (!TryNearest(projection, eye, forward, mouse, count, out Vector3 point)) continue;
                    hits.Add((Vector3.Distance(ray.origin, point) * (1f - SortingBias), point, go));
                    break;
                }
            }
        }

        public static void DrawHighlight(GameObject hovered)
        {
            if (hovered == null || !SmartMouseSettings.FocusOnGameObjects || !hovered.TryGetComponent(out SplineContainer container)) return;

            Color color = Handles.color;
            Handles.color = SmartMouseSettings.OverlayColor;
            Matrix4x4 localToWorld = container.transform.localToWorldMatrix;
            IReadOnlyList<Spline> splines = container.Splines;
            for (int i = 0; i < splines.Count; i++)
            {
                int count = Sample(splines[i], localToWorld, ref _points);
                if (count > 1) Handles.DrawAAPolyLine(HighlightWidth, count, _points);
            }
            Handles.color = color;
        }

        // World-space polyline of one spline, SamplesPerCurve points per curve plus the end point.
        internal static int Sample(Spline spline, Matrix4x4 localToWorld, ref Vector3[] buffer)
        {
            if (spline == null || spline.Count < 2) return 0;
            int curves = spline.Closed ? spline.Count : spline.Count - 1;
            int count = curves * SamplesPerCurve + 1;
            if (buffer.Length < count) buffer = new Vector3[Mathf.Max(count, buffer.Length * 2)];

            for (int c = 0; c < curves; c++)
            {
                BezierCurve curve = spline.GetCurve(c);
                for (int s = 0; s < SamplesPerCurve; s++)
                    buffer[c * SamplesPerCurve + s] = localToWorld.MultiplyPoint3x4((Vector3)CurveUtility.EvaluatePosition(curve, s / (float)SamplesPerCurve));
            }
            buffer[count - 1] = localToWorld.MultiplyPoint3x4((Vector3)CurveUtility.EvaluatePosition(spline.GetCurve(curves - 1), 1f));
            return count;
        }

        static bool TryNearest(CameraProjectionCache projection, Vector3 eye, Vector3 forward, Vector2 mouse, int count, out Vector3 point)
        {
            point = Vector3.zero;
            bool found = false;
            float best = PickPixels;
            Vector2 previous = projection.WorldToGUIPoint(_points[0]);
            bool previousInFront = Vector3.Dot(_points[0] - eye, forward) > 0f;
            for (int i = 1; i < count; i++)
            {
                Vector2 current = projection.WorldToGUIPoint(_points[i]);
                bool inFront = Vector3.Dot(_points[i] - eye, forward) > 0f;
                // A point behind the camera projects mirrored, so such a segment is never a hit.
                if (previousInFront && inFront)
                {
                    float distance = HandleUtility.DistancePointLine(mouse, previous, current);
                    if (distance <= best)
                    {
                        found = true;
                        best = distance;
                        Vector2 onLine = HandleUtility.ProjectPointLine(mouse, previous, current);
                        float length = Vector2.Distance(previous, current);
                        float t = length > 0f ? Vector2.Distance(previous, onLine) / length : 0f;
                        point = Vector3.Lerp(_points[i - 1], _points[i], t);
                    }
                }
                previous = current;
                previousInFront = inFront;
            }
            return found;
        }

        static bool GizmosVisible()
        {
            SceneView view = SceneView.currentDrawingSceneView;
            if (view == null) view = SceneView.lastActiveSceneView;
            if (view == null || !view.drawGizmos) return false;
            return !GizmoUtility.TryGetGizmoInfo(typeof(SplineContainer), out GizmoInfo info) || !info.hasGizmo || info.gizmoEnabled;
        }

        static bool Contains(List<(float distance, Vector3 point, GameObject obj)> hits, GameObject go)
        {
            for (int i = 0; i < hits.Count; i++)
                if (hits[i].obj == go) return true;
            return false;
        }
    }
}
#endif
