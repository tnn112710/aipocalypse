using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    [CreateAssetMenu(fileName = "PlayerInteractionTuning", menuName = "Aipocalypse/Player/Interaction Tuning")]
    public sealed class PlayerInteractionTuning : ScriptableObject
    {
        [SerializeField, Min(0f), Tooltip("How far from the eye things can be used (m)")]
        float _interactRange = 2.5f;
        [SerializeField, Min(0f), Tooltip("Thickness of the look ray, so small items are easy to aim at (m)")]
        float _interactRadius = 0.12f;
        [SerializeField, Tooltip("Layers the look ray hits: interactables and anything that should block them")]
        LayerMask _interactMask = Physics.DefaultRaycastLayers;
        [Header("Dropping"), SerializeField, Min(0f), Tooltip("How far in front of the player dropped items land (m)")]
        float _dropDistance = 1.2f;
        [SerializeField, Min(0f), Tooltip("Room kept between a dropped item and a wall in front (m)")]
        float _dropWallGap = 0.25f;
        [SerializeField, Min(0.1f), Tooltip("How far down the ground is searched for under the drop point (m)")]
        float _dropGroundProbe = 10f;
        [SerializeField, Range(-180f, 180f), Tooltip("Dropped item's yaw relative to the player's facing (degrees)")]
        float _dropYaw = 90f;

        public float InteractRange => _interactRange;
        public float InteractRadius => _interactRadius;
        public LayerMask InteractMask => _interactMask;
        public float DropDistance => _dropDistance;
        public float DropWallGap => _dropWallGap;
        public float DropGroundProbe => _dropGroundProbe;
        public float DropYaw => _dropYaw;
    }
}
