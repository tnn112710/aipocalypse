using System.Collections.Generic;
using UnityEngine;

namespace Aipocalypse.Core.SpawnSystem
{
    // A spawn area in a scene: an arc MinRadius–MaxRadius metres round this point, HalfAngle degrees either side of its forward
    // (180 = a full ring), or round hand-placed spots (cave mouths, gaps in a wall, a tree line) when any is set. Raid waves
    // name it by Id (RaidInstaller lists the scene's zones); a SpawnArea on the same object keeps it populated.
    public sealed class SpawnZone : MonoBehaviour, ISpawnArea
    {
        const int GizmoSegments = 32;

        [SerializeField, Tooltip("Raid waves name the zone they come from by this id")] string _id = "Front";
        [SerializeField, Min(0f), Tooltip("Metres from this point")] float _minRadius = 60f;
        [SerializeField, Min(0f), Tooltip("Metres from this point")] float _maxRadius = 80f;
        [SerializeField, Range(0f, 180f), Tooltip("Degrees either side of the forward (outward) direction")] float _halfAngle = 120f;
        [SerializeField, Tooltip("Hand-placed spots things come from (a cave mouth, a gap in a wall): when any is set, points are " +
            "picked round them instead of over the arc")]
        List<Transform> _points = new();
        [SerializeField, Min(0f), Tooltip("Metres round a hand-placed spot a point may land")] float _pointRadius = 4f;
        [SerializeField, Tooltip("Metres along the forward (outward) from this point to the Origin ground walkers need a path to. " +
            "Keep it past a gate that can be closed, or a closed gate leaves no spawn point with a path")]
        float _originOffset;

        public string Id => _id;
        // Where things spawned here head from: ground walkers need a path here.
        public Vector3 Origin => transform.position + transform.forward * _originOffset;

        // Whether points are picked round hand-placed spots.
        public bool UsesPoints
        {
            get
            {
                foreach (Transform point in _points)
                    if (point != null)
                        return true;
                return false;
            }
        }

        // A point of the arc at this point's height, rolls in [0, 1]; even over the arc's area. With hand-placed spots: one of
        // them by `angleRoll` (what's left of it turns round the spot), `radiusRoll` out from it, even over its disc.
        public Vector3 Sample(float angleRoll, float radiusRoll)
        {
            if (UsesPoints)
                return SamplePoint(angleRoll, radiusRoll);
            float angle = Mathf.Lerp(-_halfAngle, _halfAngle, angleRoll);
            float radius = Mathf.Sqrt(Mathf.Lerp(_minRadius * _minRadius, _maxRadius * _maxRadius, radiusRoll));
            return Point(angle, radius);
        }

        Vector3 SamplePoint(float pickRoll, float radiusRoll)
        {
            int count = _points.Count;
            float scaled = Mathf.Clamp01(pickRoll) * count;
            int index = Mathf.Min(count - 1, (int)scaled);
            float angle = (scaled - index) * 2f * Mathf.PI;
            float radius = Mathf.Sqrt(Mathf.Clamp01(radiusRoll)) * _pointRadius;
            for (int i = 0; i < count; i++)
            {
                Transform point = _points[(index + i) % count];
                if (point != null)
                    return point.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            }
            return transform.position;
        }

        Vector3 Facing
        {
            get
            {
                Vector3 forward = transform.forward;
                forward.y = 0f;
                return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            }
        }

        Vector3 Point(float angle, float radius) => transform.position + Quaternion.AngleAxis(angle, Vector3.up) * Facing * radius;

        void OnValidate() => _maxRadius = Mathf.Max(_maxRadius, _minRadius);

        // Always drawn (Gizmos on in the Scene or Game view): a translucent band over the arc, brighter when selected.
        void OnDrawGizmos() => DrawArc(false);
        void OnDrawGizmosSelected() => DrawArc(true);

        void DrawArc(bool selected)
        {
            var edge = new Color(1f, 0.35f, 0.2f, selected ? 1f : 0.6f);
            if (UsesPoints)
            {
                DrawPoints(edge);
                return;
            }
#if UNITY_EDITOR
            var fill = new Color(1f, 0.35f, 0.2f, selected ? 0.25f : 0.12f);
            var quad = new Vector3[4];
#endif
            Gizmos.color = edge;
            Vector3 inner = Point(-_halfAngle, _minRadius);
            Vector3 outer = Point(-_halfAngle, _maxRadius);
            Gizmos.DrawLine(inner, outer);
            for (int i = 1; i <= GizmoSegments; i++)
            {
                float angle = Mathf.Lerp(-_halfAngle, _halfAngle, i / (float)GizmoSegments);
                Vector3 nextInner = Point(angle, _minRadius);
                Vector3 nextOuter = Point(angle, _maxRadius);
#if UNITY_EDITOR
                quad[0] = inner;
                quad[1] = outer;
                quad[2] = nextOuter;
                quad[3] = nextInner;
                UnityEditor.Handles.color = fill;
                UnityEditor.Handles.DrawAAConvexPolygon(quad);
#endif
                Gizmos.DrawLine(inner, nextInner);
                Gizmos.DrawLine(outer, nextOuter);
                inner = nextInner;
                outer = nextOuter;
            }
            Gizmos.DrawLine(inner, outer);
            Gizmos.DrawLine(transform.position, Point(0f, _minRadius));
            Gizmos.DrawLine(transform.position, Point(-_halfAngle, _minRadius));
            Gizmos.DrawLine(transform.position, Point(_halfAngle, _minRadius));
#if UNITY_EDITOR
            UnityEditor.Handles.Label(Point(0f, (_minRadius + _maxRadius) * 0.5f), $"Spawn zone '{_id}' ({_halfAngle * 2f:0}°)");
#endif
        }

        void DrawPoints(Color edge)
        {
            Gizmos.color = edge;
            int count = 0;
            foreach (Transform point in _points)
            {
                if (point == null)
                    continue;
                count++;
                Gizmos.DrawLine(transform.position, point.position);
#if UNITY_EDITOR
                UnityEditor.Handles.color = edge;
                UnityEditor.Handles.DrawWireDisc(point.position, Vector3.up, Mathf.Max(0.5f, _pointRadius));
#endif
            }
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, $"Spawn zone '{_id}' ({count} points)");
#endif
        }
    }
}
