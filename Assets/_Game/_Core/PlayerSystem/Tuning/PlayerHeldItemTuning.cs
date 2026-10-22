using Aipocalypse.Core.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    [CreateAssetMenu(fileName = "PlayerHeldItemTuning", menuName = "Aipocalypse/Player/Held Item Tuning")]
    public sealed class PlayerHeldItemTuning : ScriptableObject
    {
        [field: Header("Guns"), SerializeField, Tooltip("Where bullets start: the muzzle along the barrel (follows the model and its kick), the screen centre, or the muzzle towards what the crosshair is on")]
        public AimOrigin AimFrom { get; private set; } = AimOrigin.Muzzle;
        [field: SerializeField, Range(0.1f, 1f), Tooltip("Move speed multiplier while aiming down sights")]
        public float AimMoveSpeedMultiplier { get; private set; } = 0.6f;
        [field: SerializeField, Min(0f), Tooltip("Above this ground speed (m/s) the held item counts as moving (guns use their moving recoil)")]
        public float MovingSpeed { get; private set; } = 0.5f;

        [field: Header("Obstacle pose"), SerializeField, Tooltip("Retract and tilt the held model near walls or entities")]
        public bool KeepHeldItemClear { get; private set; } = true;
        [field: SerializeField, Min(0f), Tooltip("Extra room kept in front of the model's tip (m)")]
        public float ClearanceMargin { get; private set; } = 0.05f;
        [field: SerializeField, Min(0f), Tooltip("Thickness of the check from the eye to the tip (m)")]
        public float ClearanceRadius { get; private set; } = 0.05f;
        [field: SerializeField, Min(0.01f)] public float ClearanceRetractDistance { get; private set; } = 0.45f;
        [field: SerializeField] public Vector3 ClearanceEuler { get; private set; } = new(15f, -10f, 5f);
        [field: SerializeField, Min(0f)] public float ClearanceBlendSpeed { get; private set; } = 14f;
        [field: SerializeField, Tooltip("What the model is kept out of")]
        public LayerMask ClearanceLayers { get; private set; } = Physics.DefaultRaycastLayers;

        [field: Header("Aim sway (crosshair, shots and model)"), SerializeField, InlineProperty, HideLabel]
        public AimSwaySettings AimSway { get; private set; } = new();

        [field: Header("Sway & bob (model only)"), SerializeField, InlineProperty, HideLabel]
        public HeldSwaySettings HeldSway { get; private set; } = new();
    }
}
