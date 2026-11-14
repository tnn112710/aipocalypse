using MB13.Aipocalypse.Core.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    [CreateAssetMenu(fileName = "PlayerHeldItemTuning", menuName = "Aipocalypse/Player/Held Item Tuning")]
    public sealed class PlayerHeldItemTuning : ScriptableObject
    {
        [Header("Guns"), SerializeField, Tooltip("Where bullets start: the muzzle along the barrel (follows the model and its kick), the screen centre, or the muzzle towards what the crosshair is on")]
        AimOrigin _aimFrom = AimOrigin.Muzzle;
        [SerializeField, Range(0.1f, 1f), Tooltip("Move speed multiplier while aiming down sights")]
        float _aimMoveSpeedMultiplier = 0.6f;
        [SerializeField, Min(0f), Tooltip("Above this ground speed (m/s) the held item counts as moving (guns use their moving recoil)")]
        float _movingSpeed = 0.5f;
        [SerializeField, Min(0f), Tooltip("Hip wobble multiplier while walking")] float _walkSpreadScale = 1.5f;
        [SerializeField, Min(0f), Tooltip("Hip wobble multiplier while crouching (stacks with walking)")] float _crouchSpreadScale = 0.7f;
        [SerializeField, Min(0f), Tooltip("Hip wobble multiplier while sprinting (replaces the others); it settles after the sprint")]
        float _sprintSpreadScale = 2.5f;

        [Header("Obstacle pose"), SerializeField, Tooltip("Retract and tilt the held model near walls or entities")]
        bool _keepHeldItemClear = true;
        [SerializeField, Min(0f), Tooltip("Extra room kept in front of the model's tip (m)")]
        float _clearanceMargin = 0.05f;
        [SerializeField, Min(0f), Tooltip("Thickness of the check from the eye to the tip (m)")]
        float _clearanceRadius = 0.05f;
        [SerializeField, Min(0.01f)] float _clearanceRetractDistance = 0.45f;
        [SerializeField] Vector3 _clearanceEuler = new(15f, -10f, 5f);
        [SerializeField, Min(0f)] float _clearanceBlendSpeed = 14f;
        [SerializeField, Tooltip("What the model is kept out of")]
        LayerMask _clearanceLayers = Physics.DefaultRaycastLayers;

        [Header("Aim sway (crosshair, shots and model)"), SerializeField, InlineProperty, HideLabel]
        AimSwaySettings _aimSway = new();

        [Header("Sway & bob (model only)"), SerializeField, InlineProperty, HideLabel]
        HeldSwaySettings _heldSway = new();

        public AimOrigin AimFrom => _aimFrom;
        public float AimMoveSpeedMultiplier => _aimMoveSpeedMultiplier;
        public float MovingSpeed => _movingSpeed;

        // The hip wobble's movement scale (HeldItemInput.MoveSpreadScale).
        public float MoveSpreadScale(bool sprinting, bool moving, bool crouching) =>
            sprinting ? _sprintSpreadScale : (moving ? _walkSpreadScale : 1f) * (crouching ? _crouchSpreadScale : 1f);
        public bool KeepHeldItemClear => _keepHeldItemClear;
        public float ClearanceMargin => _clearanceMargin;
        public float ClearanceRadius => _clearanceRadius;
        public float ClearanceRetractDistance => _clearanceRetractDistance;
        public Vector3 ClearanceEuler => _clearanceEuler;
        public float ClearanceBlendSpeed => _clearanceBlendSpeed;
        public LayerMask ClearanceLayers => _clearanceLayers;
        public AimSwaySettings AimSway => _aimSway;
        public HeldSwaySettings HeldSway => _heldSway;
    }
}
