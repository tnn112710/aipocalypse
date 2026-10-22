using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    [CreateAssetMenu(fileName = "PlayerInteractionTuning", menuName = "Aipocalypse/Player/Interaction Tuning")]
    public sealed class PlayerInteractionTuning : ScriptableObject
    {
        [field: SerializeField, Min(0f), Tooltip("How far from the eye things can be used (m)")]
        public float InteractRange { get; private set; } = 2.5f;
        [field: SerializeField, Min(0f), Tooltip("Thickness of the look ray, so small items are easy to aim at (m)")]
        public float InteractRadius { get; private set; } = 0.12f;
        [field: SerializeField, Tooltip("Layers the look ray hits: interactables and anything that should block them")]
        public LayerMask InteractMask { get; private set; } = Physics.DefaultRaycastLayers;
        [field: Header("Dropping"), SerializeField, Min(0f), Tooltip("How far in front of the player dropped items land (m)")]
        public float DropDistance { get; private set; } = 1.2f;
        [field: SerializeField, Min(0f), Tooltip("Room kept between a dropped item and a wall in front (m)")]
        public float DropWallGap { get; private set; } = 0.25f;
        [field: SerializeField, Min(0.1f), Tooltip("How far down the ground is searched for under the drop point (m)")]
        public float DropGroundProbe { get; private set; } = 10f;
        [field: SerializeField, Range(-180f, 180f), Tooltip("Dropped item's yaw relative to the player's facing (degrees)")]
        public float DropYaw { get; private set; } = 90f;
    }
}
