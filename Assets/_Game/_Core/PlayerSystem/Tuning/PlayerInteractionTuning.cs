using MB13.Aipocalypse.Core.Ghosts;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
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
        [Header("Dropping"), SerializeField, Min(0f), Tooltip("Room kept between a thrown item's start and a wall in front (m)")]
        float _dropWallGap = 0.25f;
        [SerializeField, Range(-180f, 180f), Tooltip("Dropped item's yaw relative to the player's facing (degrees)")]
        float _dropYaw = 90f;
        [SerializeField, Min(0f), Tooltip("Speed a dropped item is thrown forward at (m/s)")]
        float _throwSpeed = 3.5f;
        [SerializeField, Min(0f), Tooltip("Upward speed added to a throw (m/s)")]
        float _throwLift = 1.5f;
        [Header("Pickup magnet"), SerializeField, Min(0f), Tooltip("Auto-collected items (Tokens) within this many metres of the player fly to them; 0 = off")]
        float _magnetRadius = 1f;
        [SerializeField, Min(0.1f), Tooltip("How fast they fly in (m/s)")]
        float _magnetSpeed = 6f;
        [SerializeField, Tooltip("Layers world items' pickup boxes are on")]
        LayerMask _pickupMask = 1;
        [Header("Looting"), SerializeField, Min(0f), Tooltip("Seconds of searching per stack the first time a crate or body is opened")]
        float _searchSecondsPerItem = 1f;
        [Header("Repair"), SerializeField, Min(0.1f), Tooltip("Seconds per hammer knock (one repair each)")]
        float _repairKnockInterval = 0.7f;
        [SerializeField, Min(0f), Tooltip("Metres past the interact range before walking off stops the repair")]
        float _repairReachSlack = 0.75f;
        [SerializeField, Range(5f, 90f), Tooltip("Degrees off the repair point the view may turn before it counts as looking away")]
        float _repairLookAngle = 45f;
        [SerializeField, Min(0f), Tooltip("Seconds of looking away before the repair stops")]
        float _repairLookAwayGrace = 0.4f;
        [Header("Ghost"), SerializeField, Tooltip("How something looked at or being rebuilt shows as it will be (a broken fence standing)")]
        GhostStyle _ghost;
        [Header("Whistle"), SerializeField, Min(0f), Tooltip("How far the whistle reaches what the player aims at (m)")]
        float _whistleRange = 50f;
        [SerializeField, Min(0f), Tooltip("Thickness of the whistle's aim ray (m)")]
        float _whistleRadius = 0.35f;
        [SerializeField, Tooltip("Layers the whistle's aim ray hits: whistle targets and anything that blocks the call")]
        LayerMask _whistleMask = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0f), Tooltip("Seconds before the next whistle")]
        float _whistleCooldown = 0.6f;
        [SerializeField, Tooltip("Layers whose whistle targets hear it through walls when the aim is near them (the robots)")]
        LayerMask _whistleHeardMask;
        [SerializeField, Range(0f, 30f), Tooltip("How near the aim a target heard through walls must be (degrees)")]
        float _whistleHeardAngle = 8f;

        public float InteractRange => _interactRange;
        public float InteractRadius => _interactRadius;
        public LayerMask InteractMask => _interactMask;
        public float DropWallGap => _dropWallGap;
        public float DropYaw => _dropYaw;
        public float ThrowSpeed => _throwSpeed;
        public float ThrowLift => _throwLift;
        public float MagnetRadius => _magnetRadius;
        public float MagnetSpeed => _magnetSpeed;
        public LayerMask PickupMask => _pickupMask;
        public float SearchSecondsPerItem => _searchSecondsPerItem;
        public float RepairKnockInterval => _repairKnockInterval;
        public float RepairReachSlack => _repairReachSlack;
        public float RepairLookAngle => _repairLookAngle;
        public float RepairLookAwayGrace => _repairLookAwayGrace;
        public GhostStyle Ghost => _ghost;
        public float WhistleRange => _whistleRange;
        public float WhistleRadius => _whistleRadius;
        public LayerMask WhistleMask => _whistleMask;
        public float WhistleCooldown => _whistleCooldown;
        public LayerMask WhistleHeardMask => _whistleHeardMask;
        public float WhistleHeardAngle => _whistleHeardAngle;
    }
}
