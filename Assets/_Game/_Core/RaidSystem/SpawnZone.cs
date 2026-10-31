using UnityEngine;

namespace Aipocalypse.Core.RaidSystem
{
    // Where raid groups come from: an arc MinRadius–MaxRadius metres round this point (the gate), HalfAngle degrees either side
    // of its forward (outward). Raid waves name it by Id; RaidInstaller lists the scene's zones.
    public sealed class SpawnZone : MonoBehaviour
    {
        const int GizmoSegments = 32;

        [SerializeField, Tooltip("Raid waves name the zone they come from by this id")] string _id = "Front";
        [SerializeField, Min(0f), Tooltip("Metres from this point")] float _minRadius = 60f;
        [SerializeField, Min(0f), Tooltip("Metres from this point")] float _maxRadius = 80f;
        [SerializeField, Range(0f, 180f), Tooltip("Degrees either side of the forward (outward) direction")] float _halfAngle = 120f;

        public string Id => _id;
        // Where raiders head from the zone: spawn points must have a path here.
        public Vector3 Origin => transform.position;

        // A point of the arc at this point's height, rolls in [0, 1]; even over the arc's area.
        public Vector3 Sample(float angleRoll, float radiusRoll)
        {
            float angle = Mathf.Lerp(-_halfAngle, _halfAngle, angleRoll);
            float radius = Mathf.Sqrt(Mathf.Lerp(_minRadius * _minRadius, _maxRadius * _maxRadius, radiusRoll));
            return Point(angle, radius);
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
    }
}
