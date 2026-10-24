using MalbersAnimations;
using MalbersAnimations.Controller;
using MalbersAnimations.Scriptables;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace SunfishCreations.Controller
{
    [CreateAssetMenu(menuName = "Malbers Animations/Fishing/Fishing State", fileName = "Fishing", order = 4000)]
    public class Fishing : State
    {
        /// <summary>Comma-separated Holster input names disabled for the whole Fishing State, so the rod cannot be stored mid-fishing.</summary>
        private const string BlockedHolsterInputs = "Holster1,Holster2,Holster3";

        public override string StateName => "Fishing";
        public override string StateIDName => "Fishing"; 
        
        public IntReference fishingMode;

        [Tooltip("Use this to define with type of fishing you are using" +
            "Simple Fishing: No reeling, just straight pull out the fish of the water" +
            "Reeling: The player have to reel out before pulling the fish out.")]
        public FishingType fishingType;

        [Header("Fishing Events")]
        [Space(10)]
        [Tooltip("Called when fishing state is entered")]
        public UnityEvent OnFishingStart = new UnityEvent();

        [Tooltip("Called when a fish is successfully hooked")]
        public UnityEvent<FishData> OnFishHooked = new UnityEvent<FishData>();

        [Tooltip("Called when player fails to hook a fish")]
        public UnityEvent OnFishMissed = new UnityEvent();

        [Tooltip("Called when fishing state is exited")]
        public UnityEvent OnFishingEnd = new UnityEvent();

        [Tooltip("Called when fish Nibbles the bait (not biting yet)")]
        public UnityEvent OnFishNible = new UnityEvent();

        [Tooltip("Called when fish bites (player should react)")]
        public UnityEvent OnFishBiting = new UnityEvent();

        [Tooltip("Called when fish flees")]
        public UnityEvent OnFishGotAway = new UnityEvent();

        [Header("Fishing Settings")]
        [Tooltip("Auto-assigned FishCatcher component")]
        [SerializeField, ReadOnly] public FishCatcher _fishCatcher;

        [Tooltip("Current fishable area where fishing is taking place")]
        [SerializeField, ReadOnly] public FishableArea _currentFishableArea;

        [Tooltip("Auto-assigned FishingRigCapabilities component, used to pick the correct Animator end-clip variant")]
        [SerializeField, ReadOnly] public FishingRigCapabilities _rigCapabilities;

        [Header("Exit State Status")]
        [Tooltip("When the Exit Condition Fish Missed is executed. The State Exit Status will change to this value")]
        public int FishMissed = 1;

        [Tooltip("When the Exit Condition Fish Hooked is executed. The State Exit Status will change to this value")]
        public int FishHooked = 2;

        [Tooltip("When the Exit Condition Fish Presented is executed. The State Exit Status will change to this value")]
        public int FishPresented = 3;

        [Hide(nameof(fishingType), true, (int)FishingType.Simple)]
        [Header("Reeling Settings")]
        [Tooltip("Stance to activate during reeling phase")]
        public StanceID ReelingStance;

        protected MPickUp _pickUp;
        protected bool _pickUpPreviousState;

        protected bool canActivate;
        protected bool _activeFishing;
        public bool _fishProcessed;
        protected IInputAction jumpInput;
        public bool _hooked = false;

        public bool _fishOnHook { get; set; }

        private FishingMode _currentMode;

        private void InitializeFishingMode()
        {
            switch (fishingType)
            {
                case FishingType.Simple:
                    DisableTugOfWar();
                    _currentMode = new SimpleFishingMode();
                    break;
                case FishingType.Reeling:
                    DisableTugOfWar();
                    _currentMode = new ReelingFishingMode();
                    break;
                case FishingType.TugOfWar:
                    _currentMode = new TugOfWarMode();
                    break;
                default:
                    DisableTugOfWar();
                    _currentMode = new SimpleFishingMode();
                    break;
            }
            fishingMode.Value = (int)fishingType;
        }

        /// <summary>
        /// Not using tug of war, so disable the script.
        /// </summary>
        private void DisableTugOfWar()
        {
            FishTugOfWar fishTugOfWar = animal.GetComponentInChildren<FishTugOfWar>();
            if (fishTugOfWar)
            {
                fishTugOfWar.enabled = false;
            }
        }


        public override void TryExitState(float DeltaTime)
        {
            if (!_activeFishing)
            {
                if (m_debug) FDebug("[Allow Exit] No Longer fishing");

                animal.CheckIfGrounded();
                AllowExit();
            }
        }

        public override void StatebyInput()
        {
            canActivate = false;
            if (InputValue)
            {
                Activate();
            }
        }

        public override bool TryActivate()
        {
            return InputValue && CanBeActivated;
        }

        public override void Activate()
        {
            if (m_debug) FDebug($"Entered Fishing State", false, "green");
            CheckFunction();
            if (canActivate)
            {
                base.Activate();
            }
        }

        public override void EnterCoreAnimation()
        {
        }

        public override void EnterTagAnimation()
        {
        }

        protected virtual void CheckFunction()
        {
            canActivate = true;
            OnStateEnter();
        }

        protected virtual void OnStateEnter()
        {
            _activeFishing = true;
            _hooked = false;

            if (_fishCatcher == null)
            {
                _fishCatcher = animal.GetComponentInChildren<FishCatcher>();
                if (_fishCatcher == null)
                {
                    MDebug.LogError("Fishing State needs FishCatcher to be present in the Character");
                }
            }
            if (_pickUp == null)
                _pickUp = animal.GetComponentInChildren<MPickUp>();

            if (_pickUp)
            {
                _pickUpPreviousState = _pickUp.isActiveAndEnabled;
                _pickUp.FocusItem(null as GameObject);
                _pickUp.enabled = false;
            }

            if (_currentFishableArea == null)
            {
                if (m_debug) FDebug("No fishable area set! Cannot start fishing.", false, "red");
                AllowExit();
                return;
            }

            if (_rigCapabilities == null)
                _rigCapabilities = animal.GetComponentInChildren<FishingRigCapabilities>();

            // Push the combined rig-capability/spawn-location config to the Animator so the
            // end-state blend trees select the correct clip:
            // 0 = Two-Handed + Catch Fish, 1 = Two-Handed + Intercept Fish
            // 2 = Single-Hand + Catch Fish, 3 = Single-Hand + Intercept Fish
            bool supportsHandSwap = _rigCapabilities == null || _rigCapabilities.SupportsHandSwap;
            bool spawnOnWater = FishingGlobalSettings.SpawnFishOnWater;
            float fishingConfig = (supportsHandSwap ? 0f : 2f) + (spawnOnWater ? 1f : 0f);
            animal.Anim.SetFloat("FishingConfig", fishingConfig);

            _currentFishableArea.StartFishing();
            _currentFishableArea.OnFishBite.AddListener(HandleFishBite);
            _currentFishableArea.OnFishNibble.AddListener(HandleFishNibble);
            _currentFishableArea.OnBiteWindowEnd.AddListener(HandleBiteWindowEnded);

            InitializeFishingMode();
            _currentMode.OnEnter(this);

            if (animal.InputSource != null)
            {
                jumpInput = animal.InputSource.GetInput("Jump");
                animal.InputSource.DisableInput(BlockedHolsterInputs);
            }

            OnFishingStart?.Invoke();
        }
        protected virtual void HandleBiteWindowEnded()
        {
            if (_fishCatcher.FishBeingPresented || _hooked)
                return;

            _currentMode?.OnBiteWindowEnd(this);
        }

        protected virtual void HandleFishNibble()
        {
            OnFishNible?.Invoke();
        }

        protected virtual void HandleFishBite()
        {
            if (_fishOnHook || _fishCatcher.FishBeingPresented || _fishProcessed)
            {
                return;
            }

            _currentMode?.OnBite(this);
        }

        public virtual void FDebug(string value, bool uiLogger = false, string color = "white")
        {
            if (uiLogger)
            {
                UILogger.Log(value, color);
            }
#if UNITY_EDITOR
            MDebug.Log($"<B>[{animal.name}]</B> → <B>[{this.GetType().Name}]</B> → <color={color}>{value}</color>", this);
#endif
        }

        /// <summary>
        /// Called when the player presses input to store the presented fish.
        /// </summary>
        public virtual void ContinueAfterFishPresentation()
        {
            _fishCatcher?.ReactToFishEndPresentationEvent();
            SetEnterStatus(FishPresented);
        }
        public virtual void FishingRodCommand()
        {
            _currentMode?.OnRodCommand(this);
        }

        public virtual void ReelingInComplete()
        {
            if (!_fishProcessed)
            {
                if (m_debug) FDebug("Congrats you hooked a fish!", false, "green");
                _hooked = true;

                // Broadcast OnFishHooked before FishHooked() sets _isPlayerFishing to false,
                // since FishableArea.RespondToHookedEvent requires it to still be true.
                FishData caughtFish = _currentFishableArea.GetCurrentFish();

                _fishProcessed = true;
                OnFishHooked?.Invoke(caughtFish);
                _currentFishableArea?.FishHooked();
                SetEnterStatus(FishHooked);
                animal.Stance_ResetDefaultValue();
                animal.Stance_Reset();
            }
        }

        public virtual void SetCurrentWaterSpot(FishableArea fishableArea)
        {
            _currentFishableArea = fishableArea;
        }

        private Vector2 _smoothedInput = Vector2.zero;
        private float _inputSmoothTime = 0.1f;

        public override void OnStateMove(float deltaTime)
        {
            base.OnStateMove(deltaTime);

            if (jumpInput != null && jumpInput.GetValue)
            {
                if (_fishCatcher.FishBeingPresented)
                {
                    ContinueAfterFishPresentation();
                }
            }

            if (animal.InputSource != null)
            {
                Vector2 targetInput = new Vector2(
                    animal.InputSource.MoveAxis.x,
                    animal.InputSource.MoveAxis.z
                );

                // Smoothly interpolate toward the target input, useful for keyboard
                _smoothedInput.x = Mathf.Lerp(_smoothedInput.x, targetInput.x, deltaTime / _inputSmoothTime);                _smoothedInput.y = Mathf.Lerp(_smoothedInput.y, targetInput.y, deltaTime / _inputSmoothTime);

                animal.Anim.SetFloat("HorizontalRaw", _smoothedInput.x);
                animal.Anim.SetFloat("VerticalRaw", _smoothedInput.y);
            }
            else //Mobile doesn't need smoothing
            {
                animal.Anim.SetFloat("HorizontalRaw", animal.RawInputAxis.x);
                animal.Anim.SetFloat("VerticalRaw", animal.RawInputAxis.z);
            }
            
        }

        public override void StateExitByInput()
        {
            base.StateExitByInput();
        }

        public override void ResetStateValues()
        {
            _currentMode?.OnExit(this);

            _activeFishing = false;
            _fishOnHook = false;
            _fishProcessed = false;
            _hooked = false;
            InputValue = false;

            animal.InputSource?.EnableInput(BlockedHolsterInputs);

            ClearFishableArea();
            OnFishingEnd?.Invoke();
        }

        public virtual void ClearFishableArea()
        {
            if (_currentFishableArea != null)
            {
                _currentFishableArea.StopFishing();
                _currentFishableArea.OnFishNibble.RemoveListener(HandleFishNibble);
                _currentFishableArea.OnFishBite.RemoveListener(HandleFishBite);
                _currentFishableArea.OnBiteWindowEnd.RemoveListener(HandleBiteWindowEnded);
                _currentFishableArea = null;
            }
        }

        public override void RestoreAnimalOnExit()
        {
            base.RestoreAnimalOnExit();

            if (_pickUp)
                _pickUp.enabled = _pickUpPreviousState;

            animal.LockMovement = false;
        }
    }
}

namespace SunfishCreations
{
    public enum FishingType
    {
        Simple,
        Reeling,
        TugOfWar,
    }
}