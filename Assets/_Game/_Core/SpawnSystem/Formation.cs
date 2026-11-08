using System.Collections.Generic;
using UnityEngine;

namespace MB13.Aipocalypse.Core.SpawnSystem
{
    public enum FormationShape
    {
        // A loose disc (the default for groups).
        Scatter,
        Line,
        Column,
        // A V, leader at the tip.
        Wedge,
        // A circle round the anchor (a surround).
        Ring,
        // Several small scatters side by side.
        Clusters,
        // The authored Slots.
        Custom,
    }

    // How a group stands when it spawns, in its own frame: +Z is the way it faces, slot 0 is the leader. Each slot is then
    // placed by the kind's medium.
    [CreateAssetMenu(fileName = "Formation", menuName = "Aipocalypse/Spawning/Formation")]
    public sealed class Formation : ScriptableObject
    {
        public FormationShape Shape;
        [Min(0.5f), Tooltip("Metres between neighbours")] public float Spacing = 1.5f;
        [Tooltip("Scatter and Ring: metres from the anchor, a random radius in this range per group (widened to keep Spacing)")]
        public Vector2 Radius = new(3f, 5f);
        [Min(1), Tooltip("Clusters: how many")] public int Clusters = 2;
        [Min(0f), Tooltip("Clusters: metres between their centres")] public float ClusterGap = 8f;
        [Min(0f), Tooltip("Metres each slot may move at random, so lines don't look drawn with a ruler")] public float Jitter = 0.3f;
        [Tooltip("Custom: offsets in the group's frame (+Z forward), the leader first; more members than slots repeat them " +
            "further back")]
        public List<Vector3> Slots = new();

        // Offsets of `count` slots for one group (a random radius, turn and jitter).
        public void GetSlots(int count, List<Vector3> into)
        {
            into.Clear();
            float radius = Mathf.Lerp(Mathf.Min(Radius.x, Radius.y), Mathf.Max(Radius.x, Radius.y), Random.value);
            float turn = Random.value * 2f * Mathf.PI;
            for (int i = 0; i < count; i++)
            {
                Vector3 slot = Slot(i, count, radius, turn);
                if (Jitter > 0f && i > 0)
                {
                    Vector2 shift = Random.insideUnitCircle * Jitter;
                    slot += new Vector3(shift.x, 0f, shift.y);
                }
                into.Add(slot);
            }
        }

        public Vector3 Slot(int index, int count, float radius, float turn) => Shape switch
        {
            FormationShape.Line => FormationMath.Line(index, Spacing),
            FormationShape.Column => FormationMath.Column(index, Spacing),
            FormationShape.Wedge => FormationMath.Wedge(index, Spacing),
            FormationShape.Ring => FormationMath.Ring(index, count, radius, Spacing),
            FormationShape.Clusters => FormationMath.Cluster(index, count, Clusters, ClusterGap, Spacing, turn),
            FormationShape.Custom when Slots.Count > 0 =>
                Slots[index % Slots.Count] + Vector3.back * (index / Slots.Count) * Spacing * 2f,
            _ => FormationMath.Scatter(index, count, radius, Spacing, turn),
        };
    }
}
