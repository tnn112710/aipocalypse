using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ArmoryCoreEditor
{
    /// <summary>
    /// Mesh viewport of the Weapon Setup window. It draws the model's own meshes and materials without cloning its
    /// components, orbits around it and projects model points to the screen for the markers drawn over it.
    /// </summary>
    internal sealed class ArmoryCoreModelViewport : IDisposable
    {
        private readonly PreviewRenderUtility preview = new PreviewRenderUtility();
        private readonly Dictionary<SkinnedMeshRenderer, Mesh> baked = new Dictionary<SkinnedMeshRenderer, Mesh>();
        private GameObject source;
        private Bounds bounds;
        private Vector2 orbit = new Vector2(12f, 135f);
        private Vector3 pan;
        private float zoom = 1f;

        public Camera Camera => preview.camera;
        public Bounds Bounds => bounds;
        private Matrix4x4 WorldToModel => source ? source.transform.worldToLocalMatrix : Matrix4x4.identity;
        private Matrix4x4 ModelToWorld => source ? source.transform.localToWorldMatrix : Matrix4x4.identity;

        public ArmoryCoreModelViewport()
        {
            Camera.fieldOfView = 35f;
            Camera.nearClipPlane = 0.001f;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = new Color(0.085f, 0.10f, 0.13f);
            preview.lights[0].intensity = 1.2f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            preview.lights[1].intensity = 0.65f;
            preview.lights[1].transform.rotation = Quaternion.Euler(320f, 210f, 0f);
            preview.ambientColor = new Color(0.35f, 0.35f, 0.38f);
        }

        public void Frame()
        {
            pan = Vector3.zero;
            zoom = 1f;
        }

        /// <summary>Points the camera along a model-space direction; <paramref name="right"/> orients a straight up or down view.</summary>
        public void LookAlong(Vector3 forward, Vector3 right)
        {
            forward.Normalize();
            var pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg, -89.9f, 89.9f);
            var yaw = forward.x * forward.x + forward.z * forward.z > 1e-4f
                ? Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg
                : Mathf.Atan2(-right.z, right.x) * Mathf.Rad2Deg;
            orbit = new Vector2(pitch, yaw);
            Frame();
        }

        public void Draw(Rect rect, GameObject model, Action repaint)
        {
            if (source != model)
            {
                source = model;
                Frame();
                ClearMeshes();
            }
            if (!source)
            {
                GUI.Box(rect, "Drag a weapon prefab or model into «Model» on the left, or select one in the Hierarchy.", EditorStyles.centeredGreyMiniLabel);
                return;
            }
            if (Event.current.type == EventType.Repaint)
            {
                preview.BeginPreview(rect, GUIStyle.none);
                QueueMeshes();
                ConfigureCamera(rect);
                // updatefov: false, otherwise tall rects widen the field of view and the mesh stops matching Project and PickRay.
                preview.Render(true, false);
                GUI.DrawTexture(rect, preview.EndPreview(), ScaleMode.StretchToFill, false);
            }
            var frameLabel = new GUIContent("Frame model", "Show the whole model");
            var frameWidth = Mathf.Ceil(GUI.skin.button.CalcSize(frameLabel).x) + 12f;
            if (GUI.Button(new Rect(rect.xMax - frameWidth - 8f, rect.y + 8f, frameWidth, 22f), frameLabel))
            {
                Frame();
                repaint();
            }
            GUI.Label(new Rect(rect.x + 12f, rect.yMax - 25f, rect.width - 24f, 20f), "RMB / Alt+LMB — orbit   ·   wheel — zoom   ·   MMB — pan", EditorStyles.whiteMiniLabel);
            Navigate(rect, repaint);
        }

        private void ConfigureCamera(Rect rect)
        {
            var radius = Mathf.Max(0.05f, bounds.extents.magnitude);
            var rotation = Quaternion.Euler(orbit.x, orbit.y, 0f);
            var distance = radius / Mathf.Sin(Camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * 1.15f * zoom;
            var aspect = Mathf.Max(0.1f, rect.width / rect.height);
            if (aspect < 1f)
                distance /= aspect;
            Camera.aspect = aspect;
            Camera.transform.SetPositionAndRotation(bounds.center + pan + rotation * Vector3.back * distance, rotation);
            Camera.farClipPlane = distance + radius * 10f + 10f;
        }

        private void QueueMeshes()
        {
            var found = false;
            bounds = new Bounds(Vector3.zero, Vector3.one * 0.5f);
            foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !ArmoryCoreModelGeometry.ActiveUnder(renderer.transform, source.transform))
                    continue;
                Mesh mesh = null;
                if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh)
                {
                    if (!baked.TryGetValue(skin, out mesh))
                    {
                        mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                        baked.Add(skin, mesh);
                    }
                    skin.BakeMesh(mesh);
                }
                else if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter filter))
                {
                    mesh = filter.sharedMesh;
                }
                if (!mesh)
                    continue;
                var matrix = WorldToModel * renderer.transform.localToWorldMatrix;
                var local = mesh.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f)));
                    if (!found)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        found = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
                var materials = renderer.sharedMaterials;
                for (var sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var material = materials.Length > 0 ? materials[Mathf.Min(sub, materials.Length - 1)] : null;
                    if (material)
                        preview.DrawMesh(mesh, matrix, material, sub);
                }
            }
        }

        private void Navigate(Rect rect, Action repaint)
        {
            var e = Event.current;
            if (!rect.Contains(e.mousePosition))
                return;
            if (e.type == EventType.ScrollWheel)
            {
                zoom = Mathf.Clamp(zoom * Mathf.Exp(e.delta.y * 0.06f), 0.15f, 8f);
                e.Use();
                repaint();
            }
            else if (e.type == EventType.MouseDrag && (e.button == 1 || e.button == 0 && e.alt))
            {
                orbit += new Vector2(-e.delta.y, e.delta.x) * 0.5f;
                orbit.x = Mathf.Clamp(orbit.x, -88f, 88f);
                e.Use();
                repaint();
            }
            else if (e.type == EventType.MouseDrag && e.button == 2)
            {
                var size = Mathf.Max(0.1f, bounds.size.magnitude) * zoom / Mathf.Max(100f, rect.height);
                pan += (Camera.transform.up * e.delta.y - Camera.transform.right * e.delta.x) * size;
                e.Use();
                repaint();
            }
        }

        public Vector2 Project(Rect rect, Vector3 world)
        {
            var point = Camera.WorldToViewportPoint(WorldToModel.MultiplyPoint3x4(world));
            return new Vector2(rect.x + rect.width * point.x, rect.yMax - rect.height * point.y);
        }

        /// <summary>World ray under the mouse, built directly from the field of view so a tiny near plane loses no precision.</summary>
        public Ray PickRay(Rect rect, Vector2 mouse)
        {
            var x = 2f * (mouse.x - rect.x) / rect.width - 1f;
            var y = 1f - 2f * (mouse.y - rect.y) / rect.height;
            var tangent = Mathf.Tan(Camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            var local = new Ray(Camera.transform.position,
                Camera.transform.TransformDirection(new Vector3(x * tangent * Camera.aspect, y * tangent, 1f)).normalized);
            return new Ray(ModelToWorld.MultiplyPoint3x4(local.origin), ModelToWorld.MultiplyVector(local.direction).normalized);
        }

        /// <summary>Moves a world point in the view plane through <paramref name="anchor"/>.</summary>
        public bool DragPoint(Rect rect, Vector3 anchor, Vector2 mouse, out Vector3 world)
        {
            var ray = PickRay(rect, mouse);
            var plane = new Plane(WorldToModel.transpose.MultiplyVector(Camera.transform.forward).normalized, anchor);
            if (plane.Raycast(ray, out var distance))
            {
                world = ray.GetPoint(distance);
                return true;
            }
            world = anchor;
            return false;
        }

        public void Line(Rect rect, Vector3 a, Vector3 b, Color color, float width = 3f)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            var previous = Handles.color;
            Handles.color = color;
            GUI.BeginClip(rect);
            Handles.BeginGUI();
            Handles.DrawAAPolyLine(width, Project(rect, a) - rect.position, Project(rect, b) - rect.position);
            Handles.EndGUI();
            GUI.EndClip();
            Handles.color = previous;
        }

        public void Point(Rect rect, Vector3 world, Color color, string label = null)
        {
            var point = Project(rect, world);
            if (!rect.Contains(point))
                return;
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(point.x - 5f, point.y - 5f, 10f, 10f), EditorGUIUtility.whiteTexture);
            GUI.color = previous;
            if (!string.IsNullOrEmpty(label))
                GUI.Label(new Rect(point.x + 9f, point.y - 11f, 180f, 20f), label, EditorStyles.whiteMiniLabel);
        }

        public void Arrow(Rect rect, Vector3 from, Vector3 to, Color color, string label)
        {
            Line(rect, from, to, color);
            var a = Project(rect, from);
            var b = Project(rect, to);
            var direction = (a - b).normalized;
            if (Event.current.type == EventType.Repaint && rect.Contains(b) && Vector2.Distance(a, b) > 2f)
            {
                var side = new Vector2(-direction.y, direction.x);
                var previous = Handles.color;
                Handles.color = color;
                GUI.BeginClip(rect);
                Handles.BeginGUI();
                Handles.DrawAAPolyLine(3f, b + direction * 11f + side * 5f - rect.position, b - rect.position, b + direction * 11f - side * 5f - rect.position);
                Handles.EndGUI();
                GUI.EndClip();
                Handles.color = previous;
            }
            Point(rect, to, color, label);
        }

        private void ClearMeshes()
        {
            foreach (var mesh in baked.Values)
            {
                if (mesh)
                    UnityEngine.Object.DestroyImmediate(mesh);
            }
            baked.Clear();
        }

        public void Dispose()
        {
            ClearMeshes();
            preview.Cleanup();
        }
    }

    /// <summary>The visible triangles of a model in its root space: what the markers slide on and what the first guess is read from.</summary>
    internal sealed class ArmoryCoreModelGeometry
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();
        public Bounds Bounds { get; private set; }
        public bool IsEmpty => triangles.Count == 0;

        public static ArmoryCoreModelGeometry Build(GameObject root)
        {
            var geometry = new ArmoryCoreModelGeometry();
            if (!root)
                return geometry;
            var toRoot = root.transform.worldToLocalMatrix;
            var found = false;
            var bounds = default(Bounds);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !ActiveUnder(renderer.transform, root.transform))
                    continue;
                Mesh source = null;
                Mesh data = null;
                if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh)
                {
                    source = skin.sharedMesh;
                    data = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                    skin.BakeMesh(data);
                }
                else if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh)
                {
                    source = data = filter.sharedMesh;
                }
                if (!source)
                    continue;
                try
                {
                    var matrix = toRoot * renderer.transform.localToWorldMatrix;
                    var offset = geometry.vertices.Count;
                    foreach (var vertex in data.vertices)
                    {
                        var point = matrix.MultiplyPoint3x4(vertex);
                        geometry.vertices.Add(point);
                        if (!found)
                        {
                            bounds = new Bounds(point, Vector3.zero);
                            found = true;
                        }
                        else
                        {
                            bounds.Encapsulate(point);
                        }
                    }
                    for (var sub = 0; sub < data.subMeshCount; sub++)
                    {
                        if (data.GetTopology(sub) != MeshTopology.Triangles)
                            continue;
                        foreach (var index in data.GetTriangles(sub))
                            geometry.triangles.Add(offset + index);
                    }
                }
                finally
                {
                    if (data != source)
                        UnityEngine.Object.DestroyImmediate(data);
                }
            }
            geometry.Bounds = bounds;
            return geometry;
        }

        public static bool ActiveUnder(Transform node, Transform root)
        {
            // Prefab assets have no active hierarchy; their own active flags still apply.
            for (var current = node; current; current = current.parent)
            {
                if (!current.gameObject.activeSelf)
                    return false;
                if (current == root)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Point halfway through the first solid the ray enters: the middle of the part under the cursor along the view, so
        /// a marker sits inside the barrel instead of on its skin.
        /// </summary>
        public bool RaycastThrough(Ray ray, out Vector3 point)
        {
            point = default;
            var hits = new List<float>();
            for (var t = 0; t + 2 < triangles.Count; t += 3)
            {
                if (Intersect(ray, vertices[triangles[t]], vertices[triangles[t + 1]], vertices[triangles[t + 2]], out var distance))
                    hits.Add(distance);
            }
            if (hits.Count == 0)
                return false;
            hits.Sort();
            var entry = hits[0];
            var exit = entry;
            foreach (var hit in hits)
            {
                if (hit <= entry + 0.0005f)
                    continue;
                exit = hit;
                break;
            }
            point = ray.GetPoint((entry + exit) * 0.5f);
            return true;
        }

        /// <summary>
        /// First guess of the barrel: it runs along the longest side of the model, and the muzzle is the end whose last few
        /// centimetres are the thinnest — a stock or a grip is always thicker than the barrel.
        /// </summary>
        public void GuessMuzzle(out Vector3 muzzle, out Vector3 forward)
        {
            var size = Bounds.size;
            var axis = size.z >= size.x && size.z >= size.y ? 2 : size.x >= size.y ? 0 : 1;
            var up = axis == 1 ? 2 : 1;
            var length = Mathf.Max(0.001f, size[axis]);
            var slab = length * 0.06f;
            float Thickness(bool front, out Vector3 center)
            {
                var low = float.MaxValue;
                var high = float.MinValue;
                var sum = Vector3.zero;
                var count = 0;
                var end = front ? Bounds.max[axis] : Bounds.min[axis];
                foreach (var vertex in vertices)
                {
                    if (Mathf.Abs(vertex[axis] - end) > slab)
                        continue;
                    low = Mathf.Min(low, vertex[up]);
                    high = Mathf.Max(high, vertex[up]);
                    sum += vertex;
                    count++;
                }
                center = count > 0 ? sum / count : Bounds.center;
                center[axis] = end;
                return count > 0 ? high - low : float.MaxValue;
            }
            var frontThickness = Thickness(true, out var frontCenter);
            var backThickness = Thickness(false, out var backCenter);
            var towardFront = frontThickness <= backThickness;
            muzzle = towardFront ? frontCenter : backCenter;
            forward = Vector3.zero;
            forward[axis] = towardFront ? 1f : -1f;
        }

        private static bool Intersect(Ray ray, Vector3 a, Vector3 b, Vector3 c, out float distance)
        {
            distance = 0f;
            var e1 = b - a;
            var e2 = c - a;
            var p = Vector3.Cross(ray.direction, e2);
            var det = Vector3.Dot(e1, p);
            if (Mathf.Abs(det) < 1e-12f)
                return false;
            var inverse = 1f / det;
            var s = ray.origin - a;
            var u = Vector3.Dot(s, p) * inverse;
            if (u < 0f || u > 1f)
                return false;
            var q = Vector3.Cross(s, e1);
            var v = Vector3.Dot(ray.direction, q) * inverse;
            if (v < 0f || u + v > 1f)
                return false;
            distance = Vector3.Dot(e2, q) * inverse;
            return distance > 0f;
        }
    }
}
