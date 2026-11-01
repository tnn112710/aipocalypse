using UnityEngine;

namespace Aipocalypse.Core.SpawnSystem
{
    // Keeps a place populated while the player is near (a camp, a nest, a herd's meadow, a fishing spot, a grove): up to
    // Capacity alive in its SpawnZone, topped up out of the player's view, and each one killed or used up comes back after
    // RespawnDelay. When the player goes away its members leave once unseen and come back on the next visit; the killed ones
    // stay gone until their delay is over. PopulationDirector runs every area SpawnInstaller collected.
    [RequireComponent(typeof(SpawnZone))]
    public sealed class SpawnArea : MonoBehaviour
    {
        [SerializeField, Tooltip("What lives here (or the table below)")] SpawnCard _card;
        [SerializeField, Tooltip("Picked from instead of the card when set (by day phase and day, per group)")] SpawnTable _table;
        [SerializeField, Min(1), Tooltip("Most alive here at once")] int _capacity = 4;
        [SerializeField, Min(0), Tooltip("Most brought in per top-up; 0 = the card's group size")] int _groupSize;
        [SerializeField, Min(0f), Tooltip("Seconds before one killed or used up is replaced; 0 = never again")] float _respawnDelay = 180f;
        [SerializeField, Min(0f), Tooltip("Metres from the zone's origin at which the area wakes up")] float _activateRadius = 150f;
        [SerializeField, Min(0f), Tooltip("Metres past which it sleeps again; more than the activate radius so it doesn't flicker at " +
            "the edge")]
        float _deactivateRadius = 200f;
        [SerializeField, Tooltip("How each group stands; none = a scatter")] Formation _formation;
        [SerializeField, Tooltip("How spots are picked (out of view, distance from the player)")] SpawnRules _rules = new();
        [SerializeField, Tooltip("Pool its members when the scene starts, so the first visit doesn't instantiate them")] bool _prewarm = true;

        SpawnZone _zone;

        public SpawnZone Zone => _zone != null ? _zone : _zone = GetComponent<SpawnZone>();
        public SpawnCard Card => _card;
        public SpawnTable Table => _table;
        public int Capacity => _capacity;
        public int GroupSize => _groupSize;
        public float RespawnDelay => _respawnDelay;
        public float ActivateRadius => _activateRadius;
        public float DeactivateRadius => Mathf.Max(_activateRadius, _deactivateRadius);
        public Formation Formation => _formation;
        public SpawnRules Rules => _rules;
        public bool Prewarm => _prewarm;

        void OnValidate() => _deactivateRadius = Mathf.Max(_deactivateRadius, _activateRadius);

        // Flat rings where it wakes and sleeps and what lives here; selected, one group laid out in its formation where the
        // zone's middle sample falls, facing its origin.
#if UNITY_EDITOR
        void OnDrawGizmos() => DrawArea(false);
        void OnDrawGizmosSelected() => DrawArea(true);

        void DrawArea(bool selected)
        {
            var color = new Color(0.3f, 0.8f, 1f, selected ? 0.9f : 0.35f);
            Vector3 center = Zone.Origin;
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.DrawWireDisc(center, Vector3.up, _activateRadius);
            UnityEditor.Handles.color = color * new Color(1f, 1f, 1f, 0.4f);
            UnityEditor.Handles.DrawWireDisc(center, Vector3.up, DeactivateRadius);
            string what = _table != null ? _table.name : _card != null ? _card.name : "nothing (set a card or a table)";
            UnityEditor.Handles.Label(center + Vector3.up * 4f, $"Spawn area: {what} ×{_capacity}" +
                (_respawnDelay > 0f ? $", back after {_respawnDelay:0} s" : ", no respawn"));
            if (selected && _formation != null)
                DrawFormation();
        }

        void DrawFormation()
        {
            SpawnCard card = _card != null ? _card : _table != null && _table.Entries.Count > 0 ? _table.Entries[0].Card : null;
            int count = _groupSize > 0 ? _groupSize : card != null ? Mathf.Max(1, card.GroupSize.y) : _capacity;
            count = Mathf.Min(count, _capacity);
            Vector3 anchor = Zone.Sample(0.5f, 0.5f);
            Vector3 facing = Zone.Origin - anchor;
            facing.y = 0f;
            Quaternion turn = facing.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(facing) : Quaternion.identity;
            float radius = (_formation.Radius.x + _formation.Radius.y) * 0.5f;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 1f);
            for (int i = 0; i < count; i++)
            {
                Vector3 slot = anchor + turn * _formation.Slot(i, count, radius, 0f);
                Gizmos.DrawWireSphere(slot + Vector3.up * 0.5f, i == 0 ? 0.6f : 0.4f);
            }
            Gizmos.DrawLine(anchor + Vector3.up * 0.5f, anchor + Vector3.up * 0.5f + turn * Vector3.forward * 2f);
        }
#endif
    }
}
