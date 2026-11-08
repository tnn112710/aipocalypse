using UnityEngine;

namespace MB13.Aipocalypse.Core.SpawnSystem
{
    // Slot offsets of a group in its own frame (+Z = the way it faces, slot 0 = the leader), `spacing` metres between
    // neighbours. Pure maths for Formation and its tests.
    public static class FormationMath
    {
        const float GoldenAngle = 2.39996f;

        // A disc of `radius` (a sunflower spiral turned by `turn` radians), widened so neighbours stay about `spacing` apart.
        public static Vector3 Scatter(int index, int count, float radius, float spacing, float turn)
        {
            if (count <= 1)
                return Vector3.zero;
            radius = Mathf.Max(radius, spacing * Mathf.Sqrt(count / Mathf.PI));
            float r = radius * Mathf.Sqrt((index + 0.5f) / count);
            float angle = turn + index * GoldenAngle;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * r;
        }

        // Abreast: the leader in the middle, then right and left alternately.
        public static Vector3 Line(int index, float spacing) => new(Side(index) * ((index + 1) / 2) * spacing, 0f, 0f);

        // In file behind the leader.
        public static Vector3 Column(int index, float spacing) => new(0f, 0f, -index * spacing);

        // A V with the leader at the tip, pairs stepping back and out.
        public static Vector3 Wedge(int index, float spacing)
        {
            int row = (index + 1) / 2;
            return new Vector3(Side(index) * row * spacing, 0f, -row * spacing);
        }

        // A circle round the anchor, slot 0 in front, widened so neighbours stay `spacing` apart.
        public static Vector3 Ring(int index, int count, float radius, float spacing)
        {
            if (count <= 1)
                return Vector3.zero;
            radius = Mathf.Max(radius, count * spacing / (2f * Mathf.PI));
            float angle = 0.5f * Mathf.PI + index * 2f * Mathf.PI / count;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        }

        // `clusters` small scatters abreast, `gap` metres between their centres; slots go round the clusters in turn.
        public static Vector3 Cluster(int index, int count, int clusters, float gap, float spacing, float turn)
        {
            clusters = Mathf.Clamp(clusters, 1, Mathf.Max(1, count));
            int cluster = index % clusters;
            int inCluster = index / clusters;
            int size = count / clusters + (cluster < count % clusters ? 1 : 0);
            float across = (cluster - (clusters - 1) * 0.5f) * gap;
            return new Vector3(across, 0f, 0f) + Scatter(inCluster, size, 0f, spacing, turn + cluster);
        }

        // 0 for the leader, then +1, -1, +1… (right, left).
        static int Side(int index) => index == 0 ? 0 : index % 2 == 1 ? 1 : -1;
    }
}
