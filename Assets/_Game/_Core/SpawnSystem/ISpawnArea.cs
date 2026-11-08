using UnityEngine;

namespace MB13.Aipocalypse.Core.SpawnSystem
{
    // A place a request samples spawn points in (SpawnZone in a scene, RingArea from code).
    public interface ISpawnArea
    {
        // Where things spawned here head from: ground walkers need a path to it, and groups face it.
        Vector3 Origin { get; }

        // A point of the area at its origin's height, rolls in [0, 1].
        Vector3 Sample(float a, float b);
    }

    // A ring round a point (min radius 0 = a disc): an area from code, round the player or a camp.
    public readonly struct RingArea : ISpawnArea
    {
        readonly Vector3 _center;
        readonly float _minRadius;
        readonly float _maxRadius;

        public RingArea(Vector3 center, float minRadius, float maxRadius)
        {
            _center = center;
            _minRadius = Mathf.Max(0f, minRadius);
            _maxRadius = Mathf.Max(_minRadius, maxRadius);
        }

        public Vector3 Origin => _center;

        // Even over the ring's area.
        public Vector3 Sample(float a, float b)
        {
            float angle = a * 2f * Mathf.PI;
            float radius = Mathf.Sqrt(Mathf.Lerp(_minRadius * _minRadius, _maxRadius * _maxRadius, b));
            return _center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        }
    }
}
