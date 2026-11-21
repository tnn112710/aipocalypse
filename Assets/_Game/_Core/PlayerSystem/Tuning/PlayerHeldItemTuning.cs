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
        float _aimMoveSpeedMultiplier = 0.5f;
        [SerializeField, Min(0f), Tooltip("Above this ground speed (m/s) the held item counts as moving (guns use their moving recoil)")]
        float _movingSpeed = 0.5f;
        [SerializeField, Min(0f), Tooltip("Hip wobble multiplier while walking")] float _walkSpreadScale = 1.5f;
        [SerializeField, Min(0f), Tooltip("Hip wobble multiplier while crouching (stacks with walking)")] float _crouchSpreadScale = 0.7f;
        [SerializeField, Min(0f), Tooltip("Hip wobble multiplier while sprinting (replaces the others); it settles after the sprint")]
        float _sprintSpreadScale = 2.5f;
        [SerializeField, Min(0f), Tooltip("Hip wobble degrees added per 100°/s of turning (a flick shakes the aim, then it settles)")]
        float _turnSpread = 0.5f;
        [SerializeField, Min(0f), Tooltip("Most degrees turning can add")] float _maxTurnSpread = 2f;
        [SerializeField, Min(0f), Tooltip("Seconds turning's wobble takes to follow the mouse (smooths per-frame mouse jitter)")]
        float _turnSmoothing = 0.1f;

        [Header("Aiming while firing"), SerializeField, Min(0), Tooltip("The level before skills (StatId.AimWhileFiringLevel adds to it). " +
            "0 = RMB does nothing while the trigger is held; 1+ = the matching entry below")]
        int _aimWhileFiringLevel = 1;
        [SerializeField, Tooltip("Level 1, 2, …: raising the sights with the trigger held. Levels past the end use the last entry")]
        AimWhileFiring[] _aimWhileFiringLevels =
        {
            new() { AimSpeedScale = 0.4f, RecoilScale = 1.6f, WobbleScale = 2f },
            new() { AimSpeedScale = 0.6f, RecoilScale = 1.35f, WobbleScale = 1.5f },
            new() { AimSpeedScale = 0.8f, RecoilScale = 1.15f, WobbleScale = 1.2f },
        };

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
        public int AimWhileFiringLevel => _aimWhileFiringLevel;

        // The entry for `level` (0 or below, or no entries = can't aim in while firing).
        public AimWhileFiring AimWhileFiringAt(int level)
        {
            if (level <= 0 || _aimWhileFiringLevels == null || _aimWhileFiringLevels.Length == 0)
                return default;
            return _aimWhileFiringLevels[Mathf.Min(level, _aimWhileFiringLevels.Length) - 1];
        }

        // Hip wobble degrees from turning at `lookDegreesPerSecond` (HeldItemInput.TurnSpread).
        public float TurnSpread(float lookDegreesPerSecond) => Mathf.Min(Mathf.Abs(lookDegreesPerSecond) * 0.01f * _turnSpread, _maxTurnSpread);

        // TurnSpread eased from `current` over TurnSmoothing, so per-frame mouse jitter doesn't shake the envelope.
        public float SmoothTurnSpread(float current, float lookDegreesPerSecond, float deltaTime) =>
            Mathf.Lerp(current, TurnSpread(lookDegreesPerSecond), Damping.ByTime(_turnSmoothing, deltaTime));

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
