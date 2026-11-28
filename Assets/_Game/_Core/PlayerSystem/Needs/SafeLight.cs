using System.Collections.Generic;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem.Needs
{
    // A light that keeps the player sane (the home campfire; lit fire pits later): inside its radius sanity recovers, outside
    // it the darkness drain grows with the distance to the nearest one. The radius is the sanity rule, not the lamp's range.
    // Collected into NeedsInstaller on scene save / Play.
    public sealed class SafeLight : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("Metres (flat) inside which the player counts as in the light")] float _radius = 28f;

        public float Radius => _radius;

        // Flat metres from the nearest light's edge: 0 inside any, infinite with none.
        public static float DistanceOutside(Vector3 position, IReadOnlyList<SafeLight> lights)
        {
            float best = float.PositiveInfinity;
            if (lights == null)
                return best;
            for (int i = 0; i < lights.Count; i++)
            {
                SafeLight light = lights[i];
                if (light == null || !light.enabled || !light.gameObject.activeInHierarchy)
                    continue;
                Vector3 offset = position - light.transform.position;
                float outside = new Vector2(offset.x, offset.z).magnitude - light._radius;
                if (outside <= 0f)
                    return 0f;
                if (outside < best)
                    best = outside;
            }
            return best;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.75f, 0.3f, 0.8f);
            Vector3 centre = transform.position;
            const int segments = 48;
            Vector3 previous = centre + new Vector3(_radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector3 next = centre + new Vector3(Mathf.Cos(angle) * _radius, 0f, Mathf.Sin(angle) * _radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
