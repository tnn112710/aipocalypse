#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using MalbersAnimations.Controller;
using MalbersAnimations.Weapons;
using MalbersAnimations;
using SunfishCreations.Animations;
using Unity.Collections;
using SunfishCreations.Controller;

namespace SunfishCreations
{
    [HelpURL("https://sunfish-studios.gitbook.io/fishing-for-animal-controller/main-components/fishing-rod")]
    public class FishingRodWM : MMelee, IHandSwapper
    {
        [Header("Fishing Caster")]
        [Tooltip("Reference to the FishingCaster component (auto-assigned on equip)")]
        [SerializeField, ReadOnly] private FishingCaster _caster;

        [Header("Cooldown")]
        [Tooltip("Time in seconds before the rod can be cast again")]
        [SerializeField] private float _cooldown = 2f;

        [Tooltip("Offset applied when rod is held in the middle (e.g., during two-handed grip)")]
        public TransformOffset MiddleRodOffset = new(1);

        // --- Protected Fields ---
        protected MAnimal _animal;
        protected Fishing _fishingState;
        protected GameObject hitGameObject;
        protected FishingRodBendController _bendController;
        protected IMWeaponOwner _weaponOwner;
        protected float _lastCastTime;
        protected int stateIDValue = 16;

        protected FishingRigCapabilities _capabilities;
        private bool _capabilitiesResolved;

        private Animator _cachedAnimator;
        private bool _animatorResolved;

        // Animator parameter/state name hashes for end-state swap suppression.
        private static readonly int FishingConfigParam = Animator.StringToHash("FishingConfig");
        private static readonly int FishingEndCatchFishHash = Animator.StringToHash("Fishing_End_Catch_Fish");
        private static readonly int FishingEndSwapRodToIdleHash = Animator.StringToHash("Fishing_End_Swap_Rod_to_Idle");

        // --- Properties ---
        [Tooltip("Determines if the fishing rod can be unequipped (prevents unequipping while fishing)")]
        public override bool CanUnequip => _animal?.ActiveStateID != stateIDValue;

        public virtual FishingCaster Caster
        {
            get => _caster;
            set => _caster = value;
        }

        public override bool Equip(IMWeaponOwner _char)
        {
            bool equipResult = base.Equip(_char);

            if (equipResult)
            {
                _animal = CurrentOwner.Owner.GetComponent<MAnimal>();
                _fishingState = _animal?.State_Get<Fishing>();
                _caster = _animal?.GetComponentInChildren<FishingCaster>();
                _weaponOwner = _char;
            }
            return equipResult;
        }

        public override void MainAttack_Start(IMWeaponOwner RC)
        {
            if (Time.time - _lastCastTime < _cooldown)
            {
                if (debug) Debugging($"Fishing Rod on cooldown", this, "yellow");
                return;
            }

            if (_animal != null && _animal.ActiveStateID == stateIDValue)
            {
                if (debug) Debugging("Will not cast rod, already in Fishing State", this);
                _fishingState?.FishingRodCommand();
                return;
            }

            base.MainAttack_Start(RC);
            if (debug) Debugging("Fishing Rod Cast Started", this, "cyan");

            if (WaterCheck())
            {
                FishableArea fishableArea = hitGameObject.GetComponent<FishableArea>();
                if(debug) FDebug($"Cast Hit Water", false, "blue");

                if (fishableArea == null)
                {
                    Debug.LogError($"{hitGameObject} does not implement FishableArea. This may be an issue.");
                }

                if (debug) Debugging("Cast Hit Water", this, "cyan");
                _fishingState?.SetCurrentWaterSpot(fishableArea);
                _animal?.State_Activate(stateIDValue);

                if (_bendController)
                {
                    _bendController.InformWaterHit(true);
                }
            }
            else
            {
                if (_bendController)
                {
                    _bendController.InformWaterHit(false);
                }

                FDebug($"Cast did not hit Water", true, "red");
            }

            _lastCastTime = Time.time; // Start cooldown
        }

        public virtual void ApplyMiddleRodOffset()
        {
            MiddleRodOffset.SetOffset(transform);
        }

        public virtual void FDebug(string value, bool uiLogger = false, string color = "white")
        {
#if UNITY_EDITOR
            if (uiLogger)
            {
                UILogger.Log(value, color);
                return;
            }
            MDebug.Log($"<B>[{name}]</B> → <B>[{this.GetType().Name}]</B> → <color={color}>{value}</color>", this);
#endif
        }

        // --- Protected Virtual Lifecycle & Logic Methods ---
        protected virtual void Awake()
        {
            if (animator)
                defaultAnimatorSpeed = animator.speed;
            Initialize();
        }

        public override void Initialize()
        {
            base.Initialize();
            CanCauseDamage = false;
            _bendController = GetComponent<FishingRodBendController>();
            OnUnequiped.AddListener(HandleUnequipped);
        }

        protected virtual void HandleUnequipped(Transform arg0)
        {
            if (_fishingState?.IsActiveState == true)
            {
                _fishingState.AllowExit();
            }
        }

        protected virtual bool WaterCheck()
        {
            if (_caster == null)
            {
                if (debug) Debugging("No FishingCaster assigned!", this, "red");
                return false;
            }

            if (_caster.TryCastToWater(out Vector3 waterHitPoint, out GameObject waterObject))
            {
                hitGameObject = waterObject;
                _caster.InformWaterHit(waterHitPoint);
                return true;
            }

            return false;
        }

        void IHandSwapper.OnSwapToSecondaryHand()
        {
            OnSwapToSecondaryHand();
        }

        void IHandSwapper.OnSwapToOriginalHand(bool middleRod)
        {
            OnSwapToOriginalHand(middleRod);
        }

        /// <summary>
        /// Resolves and caches the character's FishingRigCapabilities by walking up to the
        /// Animator and then searching downward. Resolves once; null = capability enabled
        /// for backward compatibility.
        /// </summary>
        protected virtual FishingRigCapabilities ResolveCapabilities()
        {
            if (!_capabilitiesResolved)
            {
                Animator animator = ResolveAnimator();
                _capabilities = animator != null ? animator.GetComponentInChildren<FishingRigCapabilities>() : null;
                _capabilitiesResolved = true;
            }
            return _capabilities;
        }

        private Animator ResolveAnimator()
        {
            if (!_animatorResolved)
            {
                _cachedAnimator = GetComponentInParent<Animator>();
                _animatorResolved = true;
            }
            return _cachedAnimator;
        }

        /// <summary>
        /// When FishingConfig >= 1, end-state clips have hand positioning baked in and
        /// don't need the HandSwap_Behaviour to reparent the rod. Returns true when the
        /// current swap should be suppressed.
        /// </summary>
        private bool ShouldSuppressSwapForBakedClips()
        {
            Animator animator = ResolveAnimator();
            if (animator == null)
                return false;

            // FishingConfig 0 = v1 catch clips that need code-driven rod reparenting.
            // FishingConfig >= 1 = v2/single-hand clips with hand positioning baked in.
            if (animator.GetFloat(FishingConfigParam) < 1f)
                return false;

            // Only suppress in the end states; the Reeling state still needs the swap.
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            return stateInfo.shortNameHash == FishingEndCatchFishHash
                || stateInfo.shortNameHash == FishingEndSwapRodToIdleHash;
        }

        // Expose interface methods as virtual for inheritance
        protected virtual void OnSwapToSecondaryHand()        {
            FishingRigCapabilities capabilities = ResolveCapabilities();
            if (capabilities != null && !capabilities.SupportsHandSwap)
                return;

            if (ShouldSuppressSwapForBakedClips())
                return;

            if (IsRightHanded)
            {
                transform.SetParent(_weaponOwner?.LeftHand);
            }
            else
            {
                transform.SetParent(_weaponOwner?.RightHand);
            }
        }

        protected virtual void OnSwapToOriginalHand(bool middleRod)
        {
            FishingRigCapabilities capabilities = ResolveCapabilities();
            if (capabilities != null && !capabilities.SupportsHandSwap)
                return;

            if (ShouldSuppressSwapForBakedClips())
                return;

            if (IsRightHanded)
            {
                transform.SetParent(_weaponOwner?.RightHand);
            }
            else
            {
                transform.SetParent(_weaponOwner?.LeftHand);
            }

            if (middleRod)
            {
                MiddleRodOffset.SetOffset(transform);
            }
            else
            {
                RightHandOffset.SetOffset(transform);
            }
        }
    }

    #region Inspector
#if UNITY_EDITOR
    [CanEditMultipleObjects, CustomEditor(typeof(FishingRodWM))]
    public class FishingRodWMEditor : MMeleeEditor
    {
        protected SerializedProperty MiddleRodOffset, _cooldown;

        protected override void SetOnEnable()
        {
            base.SetOnEnable();
            _cooldown = serializedObject.FindProperty("_cooldown");
            MiddleRodOffset = serializedObject.FindProperty("MiddleRodOffset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw MMelee base inspector
            base.OnInspectorGUI();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Fishing Rod Info", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Fishing Rod requires the Weapon Owner to have a Fishing Caster.", MessageType.Info);

            EditorGUILayout.PropertyField(_cooldown);
            EditorGUILayout.PropertyField(MiddleRodOffset, true);

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
    #endregion
}