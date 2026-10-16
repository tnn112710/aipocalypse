#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Aipocalypse.Core.HandSystem
{
    // Editor-only: fits a grip to its item. Slides the palm onto the item's surface along the back-of-hand axis, then
    // curls each finger until it touches the item's meshes. The result is stored on the grip, so nothing runs in game.
    // Uses the project's HandSet hand for that side, posed exactly as the player sees it (implied elbow).
    internal static class HandFit
    {
        const float CurlStep = 0.05f;
        const float TipStep = 0.1f;
        const float MinTip = -0.6f;
        const float MaxTip = 0.5f;
        const float MaxExtraCurl = 0.3f;
        const float ContactSlack = 0.002f;
        const float PalmRange = 0.05f;
        const float PalmContact = 0.001f;

        sealed class Mesh
        {
            public readonly List<Vector3> A = new(), B = new(), C = new(), N = new();
            public Bounds Bounds;
        }

        public static void Bake(HandGrip grip)
        {
            HandRig rig = HandPreview.Spawn(grip.Side, grip.gameObject.scene);
            if (rig == null)
                return;
            try
            {
                List<Mesh> meshes = Collect(grip);
                if (meshes.Count == 0)
                {
                    Debug.LogWarning($"{grip.name}: no meshes to fit to.", grip);
                    return;
                }
                Vector3 elbow = HandPreview.Elbow(grip.transform, grip.Side);
                HandShape pose = grip.PoseShape;
                var samples = new List<Vector3>();

                float palm = grip.FitsPalm ? FitPalm(grip, rig, elbow, pose, meshes, samples) : 0f;
                grip.Resolve(rig, elbow, palm, out Vector3 position, out Quaternion rotation);
                rig.Place(position, rotation);
                rig.Apply(pose);

                var fingers = new FingerShape[HandShape.FingerCount];
                for (int f = 0; f < HandShape.FingerCount; f++)
                    fingers[f] = ((int)grip.KeepFingers & (1 << f)) != 0 ? pose[f] : FitFinger(rig, f, pose[f], meshes, samples);
                grip.SetFit(new HandShape { Thumb = fingers[0], Index = fingers[1], Middle = fingers[2], Ring = fingers[3], Little = fingers[4] }, palm);
            }
            finally
            {
                Object.DestroyImmediate(rig.gameObject);
            }
        }

        // Moves the hand along its back-of-hand axis until the palm just touches the item (positive = away from it).
        static float FitPalm(HandGrip grip, HandRig rig, Vector3 elbow, HandShape pose, List<Mesh> meshes, List<Vector3> samples)
        {
            float Gap(float offset)
            {
                grip.Resolve(rig, elbow, offset, out Vector3 position, out Quaternion rotation);
                rig.Place(position, rotation);
                rig.Apply(pose);
                rig.GetPalmSamples(rotation * Vector3.down, samples);
                float gap = float.MaxValue;
                foreach (Vector3 p in samples)
                    gap = Mathf.Min(gap, SignedDistance(p, meshes));
                return gap;
            }

            float low = -PalmRange, high = PalmRange;
            if (Gap(low) > PalmContact)
                return 0f; // Nothing under the palm in reach: keep the authored place.
            if (Gap(high) < PalmContact)
                return high;
            for (int i = 0; i < 20; i++)
            {
                float mid = (low + high) * 0.5f;
                if (Gap(mid) < PalmContact)
                    low = mid;
                else
                    high = mid;
            }
            return high;
        }

        // The finger shape that hugs the item most closely without going into it, searched over the base curl and the
        // tip bend and kept near the pose, so fingers never close on empty air. The pose itself if nothing fits.
        static FingerShape FitFinger(HandRig rig, int finger, FingerShape pose, List<Mesh> meshes, List<Vector3> samples)
        {
            float radius = rig.FingerRadius;
            float maxCurl = Mathf.Min(1f, pose.Curl + MaxExtraCurl);
            FingerShape best = pose;
            float bestScore = float.MaxValue;
            for (float curl = 0f; curl <= maxCurl + 1e-4f; curl += CurlStep)
            {
                for (float tip = MinTip; tip <= MaxTip + 1e-4f; tip += TipStep)
                {
                    var shape = new FingerShape(curl, pose.Spread, tip);
                    rig.ApplyFinger(finger, shape);
                    rig.GetFingerSamples(finger, samples);
                    float gaps = 0f;
                    bool inside = false;
                    foreach (Vector3 p in samples)
                    {
                        float gap = SignedDistance(p, meshes) - radius;
                        if (gap < -ContactSlack)
                        {
                            inside = true;
                            break;
                        }
                        gaps += Mathf.Max(0f, gap);
                    }
                    if (inside)
                        continue;
                    // Closest to the surface overall, then closest to the pose.
                    float score = gaps / samples.Count + 0.004f * Mathf.Abs(curl - pose.Curl) + 0.002f * Mathf.Abs(tip - pose.Tip);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = shape;
                    }
                }
            }
            rig.ApplyFinger(finger, best);
            return best;
        }

        // Shown meshes of the item, plus hidden ones the grip itself holds (a shell prop).
        static List<Mesh> Collect(HandGrip grip)
        {
            ItemHands item = grip.GetComponentInParent<ItemHands>(true);
            Transform root = item != null ? item.transform : grip.transform.root;
            Transform held = grip.transform.parent;
            var meshes = new List<Mesh>();
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                bool shown = filter.gameObject.activeInHierarchy && filter.TryGetComponent(out MeshRenderer renderer) && renderer.enabled;
                if (!shown && (held == null || !filter.transform.IsChildOf(held)))
                    continue;
                meshes.Add(Build(filter));
            }
            return meshes;
        }

        static Mesh Build(MeshFilter filter)
        {
            UnityEngine.Mesh source = filter.sharedMesh;
            Vector3[] vertices = source.vertices;
            int[] triangles = source.triangles;
            Matrix4x4 m = filter.transform.localToWorldMatrix;
            var mesh = new Mesh();
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = m.MultiplyPoint3x4(vertices[i]);
            mesh.Bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (Vector3 v in vertices)
                mesh.Bounds.Encapsulate(v);
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-14f)
                    continue;
                mesh.A.Add(a);
                mesh.B.Add(b);
                mesh.C.Add(c);
                mesh.N.Add(n.normalized);
            }
            return mesh;
        }

        // Signed distance to the union of the meshes (negative inside any of them).
        static float SignedDistance(Vector3 p, List<Mesh> meshes)
        {
            float best = float.MaxValue;
            foreach (Mesh mesh in meshes)
            {
                float outside = Mathf.Sqrt(mesh.Bounds.SqrDistance(p));
                if (outside > 0f && outside >= best)
                    continue;
                best = Mathf.Min(best, SignedDistance(p, mesh));
            }
            return best;
        }

        // Distance to one mesh, negative inside. Near an edge or corner several faces are equally close; the one the
        // point is most squarely in front of or behind decides the sign.
        static float SignedDistance(Vector3 p, Mesh mesh)
        {
            const float tie = 1e-5f;
            float best = float.MaxValue;
            float sign = 1f;
            float clarity = -1f;
            for (int i = 0; i < mesh.A.Count; i++)
            {
                Vector3 offset = p - ClosestOnTriangle(p, mesh.A[i], mesh.B[i], mesh.C[i]);
                float distance = offset.magnitude;
                if (distance > best + tie)
                    continue;
                float facing = distance > 1e-6f ? Vector3.Dot(offset / distance, mesh.N[i]) : 0f;
                if (distance < best - tie)
                {
                    best = distance;
                    clarity = Mathf.Abs(facing);
                    sign = facing >= 0f ? 1f : -1f;
                }
                else if (Mathf.Abs(facing) > clarity)
                {
                    best = Mathf.Min(best, distance);
                    clarity = Mathf.Abs(facing);
                    sign = facing >= 0f ? 1f : -1f;
                }
            }
            return best * sign;
        }

        // Ericson, Real-Time Collision Detection 5.1.5.
        static Vector3 ClosestOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;
            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));
            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f) return b + (c - b) * ((d4 - d3) / (d4 - d3 + (d5 - d6)));
            float denom = 1f / (va + vb + vc);
            return a + ab * (vb * denom) + ac * (vc * denom);
        }
    }
}
#endif
