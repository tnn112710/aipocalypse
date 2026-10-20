using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ArmoryCore
{
    public enum ArmoryCoreWeaponState
    {
        Ready,
        Reloading,
        Cycling,
        ClearingMalfunction
    }

    public enum ArmoryCoreMalfunctionType
    {
        None,
        FailureToFire,
        FailureToFeed,
        Stovepipe,
        DoubleFeed
    }

    public enum ArmoryCoreShotOutcome
    {
        Fired,
        Safe,
        Cooldown,
        Empty,
        Reloading,
        ActionRequired,
        Malfunction,
        InvalidConfiguration
    }

    [Serializable]
    public sealed class ArmoryCoreWeaponEvent : UnityEvent { }

    [Serializable]
    public sealed class ArmoryCoreWeaponAmmoEvent : UnityEvent<int, int> { }

    [Serializable]
    public sealed class ArmoryCoreWeaponAimEvent : UnityEvent<bool> { }

    [DisallowMultipleComponent]
    public sealed class ArmoryCoreWeaponController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private ArmoryCoreWeaponDefinition definition;
        [SerializeField] private Transform muzzle;
        [SerializeField, HideInInspector] private Vector3 baseMuzzleLocalPosition;
        [SerializeField, HideInInspector] private Quaternion baseMuzzleLocalRotation = Quaternion.identity;
        [SerializeField, HideInInspector] private bool baseMuzzlePoseConfigured;
        [SerializeField] private Transform aimReference;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private bool initializeOnAwake = true;

        [Header("Player input (optional)")]
        [Tooltip("Enable this only on the locally controlled weapon.")]
        [SerializeField] private bool processPlayerInput;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private bool autoFindMainCamera = true;
        [SerializeField] private bool controlCameraFieldOfView = true;
        [SerializeField, Range(20f, 120f)] private float hipFieldOfView = 60f;
        [SerializeField, Range(5f, 100f)] private float aimFieldOfView = 42f;
        [SerializeField, Min(0.01f)] private float fieldOfViewSpeed = 14f;
        [SerializeField] private bool compensateSightZero = true;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;

        [Header("Presentation (optional)")]
        [SerializeField] private Animator animator;
        [SerializeField] private string fireTrigger = "Fire";
        [SerializeField] private string reloadTrigger = "Reload";
        [SerializeField] private string emptyTrigger = "Empty";
        [SerializeField] private string cycleTrigger = "Cycle";
        [SerializeField] private string malfunctionTrigger = "Malfunction";
        [SerializeField] private string aimingParameter = "Aiming";
        [SerializeField] private string fireModeParameter = "FireMode";
        [SerializeField] private ParticleSystem muzzleFlash;
        [SerializeField, Min(0)] private int muzzleFlashParticles = 4;
        [SerializeField] private Light muzzleLight;
        [SerializeField, Min(0f)] private float muzzleLightDuration = 0.045f;
        [SerializeField] private Transform casingEjectionPoint;
        [SerializeField] private Rigidbody casingPrefab;
        [SerializeField, Min(0f)] private float casingEjectionForce = 2.4f;
        [SerializeField, Min(0f)] private float casingEjectionTorque = 1.2f;
        [SerializeField, Min(0.01f)] private float casingLifetime = 8f;
        [SerializeField] private Transform recoilTarget;
        [SerializeField, Min(0f)] private float visualKickMetersPerDegree = 0.003f;

        [Header("Runtime inventory")]
        [SerializeField, Min(0)] private int reserveRounds = 120;

        [Header("Inspector events")]
        public ArmoryCoreWeaponEvent onShot = new();
        public ArmoryCoreWeaponEvent onEmpty = new();
        public ArmoryCoreWeaponEvent onReloadStarted = new();
        public ArmoryCoreWeaponEvent onReloadCompleted = new();
        public ArmoryCoreWeaponEvent onMalfunction = new();
        public ArmoryCoreWeaponEvent onMalfunctionCleared = new();
        public ArmoryCoreWeaponAmmoEvent onAmmoChanged = new();
        public ArmoryCoreWeaponAimEvent onAimChanged = new();

        private readonly List<ArmoryCoreAttachmentDefinition> installedAttachments = new();
        private readonly List<ArmoryCoreAttachmentModule> installedModules = new();
        private readonly List<ArmoryCoreAttachmentModule> previousInstalledModules = new();
        private ArmoryCoreWeaponRuntimeStats stats;
        private ArmoryCoreWeaponState state;
        private ArmoryCoreMalfunctionType malfunction;
        private ArmoryCoreFireMode fireMode;
        private int roundsInMagazine;
        private bool chamberLoaded;
        private bool actionRequired;
        private bool triggerHeld;
        private bool automaticDryFireLatched;
        private bool isAiming;
        private float movementAmount;
        private int burstShotsRemaining;
        private float nextShotTime;
        private float stateFinishTime;
        private float heat;
        private float durability = 1f;
        private float fouling;
        private Vector2 recoilOffset;
        private float lastShotTime = float.NegativeInfinity;
        private float muzzleLightOffTime = float.NegativeInfinity;
        private Vector3 recoilTargetBaseLocalPosition;
        private Quaternion recoilTargetBaseLocalRotation = Quaternion.identity;
        private bool recoilTargetPoseCaptured;
        private float originalCameraFieldOfView;
        private bool cameraFieldOfViewCaptured;
        private System.Random random;

        public event Action<ArmoryCoreShotOutcome> ShotAttempted;
        public event Action<Vector2> RecoilApplied;
        public event Action ConfigurationChanged;

        public ArmoryCoreWeaponDefinition Definition => definition;
        public ArmoryCoreWeaponRuntimeStats Stats => stats;
        public IReadOnlyList<ArmoryCoreAttachmentDefinition> InstalledAttachments => installedAttachments;
        public IReadOnlyList<ArmoryCoreAttachmentModule> InstalledModules => installedModules;
        public ArmoryCoreWeaponState State => state;
        public ArmoryCoreMalfunctionType Malfunction => malfunction;
        public ArmoryCoreFireMode FireMode => fireMode;
        public int RoundsInMagazine => roundsInMagazine;
        public bool ChamberLoaded => chamberLoaded;
        public int ReserveRounds => reserveRounds;
        public int TotalRoundsInWeapon => roundsInMagazine + (chamberLoaded ? 1 : 0);
        public float Heat => heat;
        public float Durability => durability;
        public float Fouling => fouling;
        public Vector2 RecoilOffset => recoilOffset;
        public Transform Muzzle => muzzle;
        public AudioSource AudioSource => audioSource;
        public bool IsAiming => isAiming;
        public bool IsPlayerInputEnabled => processPlayerInput;
        public Camera AimCamera => aimCamera;
        public ArmoryCoreSightModule ActiveSight => FindInstalledModule<ArmoryCoreSightModule>();
        public bool IsOperational => definition != null && definition.ammunition != null && malfunction == ArmoryCoreMalfunctionType.None;

        private void Awake()
        {
            random = new System.Random();
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (processPlayerInput || aimCamera != null)
                ResolveAimCamera();
            ConfigureAudioSource();
            CaptureRecoilTargetPose();
            if (initializeOnAwake)
                InitializeState();
            else
                RebuildInstalledAttachments();
        }

        private void Update()
        {
            UpdatePlayerInput();
            UpdatePresentation();
            if (definition == null)
                return;

            heat = Mathf.Max(0f, heat - definition.heatDissipationPerSecond * Time.deltaTime);
            RecoverRecoil();
            UpdateTimedState();
            UpdateTrigger();
            UpdateCookOff();
        }

        private void OnDisable()
        {
            triggerHeld = false;
            if (muzzleLight != null)
                muzzleLight.enabled = false;
            if (recoilTarget != null && recoilTargetPoseCaptured)
            {
                recoilTarget.localPosition = recoilTargetBaseLocalPosition;
                recoilTarget.localRotation = recoilTargetBaseLocalRotation;
            }
            RestoreCameraFieldOfView();
        }

        private void OnValidate()
        {
            reserveRounds = Mathf.Max(0, reserveRounds);
            hipFieldOfView = Mathf.Clamp(hipFieldOfView, 20f, 120f);
            aimFieldOfView = Mathf.Clamp(aimFieldOfView, 5f, 100f);
            fieldOfViewSpeed = Mathf.Max(0.01f, fieldOfViewSpeed);
            muzzleFlashParticles = Mathf.Max(0, muzzleFlashParticles);
            muzzleLightDuration = Mathf.Max(0f, muzzleLightDuration);
            casingLifetime = Mathf.Max(0.01f, casingLifetime);
            visualKickMetersPerDegree = Mathf.Max(0f, visualKickMetersPerDegree);
            if (!Application.isPlaying)
            {
                if (audioSource == null)
                    audioSource = GetComponent<AudioSource>();
            }
        }

        public void Configure(ArmoryCoreWeaponDefinition weaponDefinition, Transform muzzleTransform)
        {
            Configure(
                weaponDefinition,
                muzzleTransform,
                GetComponent<AudioSource>());
        }

        public void Configure(
            ArmoryCoreWeaponDefinition weaponDefinition,
            Transform muzzleTransform,
            AudioSource weaponAudioSource)
        {
            definition = weaponDefinition;
            muzzle = muzzleTransform;
            if (!baseMuzzlePoseConfigured && muzzle != null)
            {
                baseMuzzleLocalPosition = muzzle.localPosition;
                baseMuzzleLocalRotation = muzzle.localRotation;
                baseMuzzlePoseConfigured = true;
            }
            audioSource = weaponAudioSource;
            ConfigureAudioSource();
            RebuildInstalledAttachments();
        }

        public void ConfigurePresentation(ParticleSystem flash, Light flashLight, Animator weaponAnimator = null)
        {
            muzzleFlash = flash;
            muzzleLight = flashLight;
            if (weaponAnimator != null)
                animator = weaponAnimator;
        }

        public void InitializeState()
        {
            state = ArmoryCoreWeaponState.Ready;
            malfunction = ArmoryCoreMalfunctionType.None;
            actionRequired = false;
            heat = 0f;
            durability = 1f;
            fouling = 0f;
            triggerHeld = false;
            automaticDryFireLatched = false;
            burstShotsRemaining = 0;
            roundsInMagazine = definition != null && definition.spawnWithFullMagazine
                ? definition.magazineCapacity
                : 0;
            chamberLoaded = definition != null
                && definition.chamberCapacity > 0
                && definition.spawnWithChamberLoaded
                && definition.action != ArmoryCoreWeaponAction.OpenBolt;
            fireMode = FirstUsableFireMode();
            RebuildInstalledAttachments();
            NotifyAmmoChanged();
            UpdateAnimatorState();
        }

        public void SetAiming(bool value)
        {
            if (isAiming == value)
                return;
            isAiming = value;
            SetAnimatorBool(aimingParameter, value);
            for (var i = 0; i < installedModules.Count; i++)
                installedModules[i]?.OnAimChanged(value);
            onAimChanged.Invoke(value);
        }

        public void SetAimReference(Transform value) => aimReference = value;

        public void SetAimCamera(Camera value)
        {
            RestoreCameraFieldOfView();
            aimCamera = value;
            if (aimCamera != null)
            {
                aimReference = aimCamera.transform;
                CaptureCameraFieldOfView();
            }
        }

        public void SetPlayerInputEnabled(bool value)
        {
            processPlayerInput = value;
            if (value)
                ResolveAimCamera();
            else
                SetTriggerHeld(false);
        }

        public void SetMovementAmount(float normalizedAmount)
        {
            movementAmount = Mathf.Clamp01(normalizedAmount);
        }

        public void SetReserveRounds(int rounds)
        {
            reserveRounds = Mathf.Max(0, rounds);
            NotifyAmmoChanged();
        }

        public bool SetFireMode(ArmoryCoreFireMode mode)
        {
            if (definition == null || !definition.Supports(mode))
                return false;
            fireMode = mode;
            burstShotsRemaining = 0;
            automaticDryFireLatched = false;
            UpdateAnimatorState();
            return true;
        }

        public ArmoryCoreFireMode CycleFireMode()
        {
            if (definition == null || definition.fireModes == null || definition.fireModes.Length == 0)
                return fireMode;
            var index = Array.IndexOf(definition.fireModes, fireMode);
            fireMode = definition.fireModes[(index + 1 + definition.fireModes.Length) % definition.fireModes.Length];
            burstShotsRemaining = 0;
            automaticDryFireLatched = false;
            UpdateAnimatorState();
            return fireMode;
        }

        public void SetTriggerHeld(bool held)
        {
            if (held == triggerHeld)
                return;
            triggerHeld = held;
            if (!held)
            {
                automaticDryFireLatched = false;
                return;
            }

            if (fireMode == ArmoryCoreFireMode.Burst)
                burstShotsRemaining = Mathf.Max(1, definition == null ? 1 : definition.burstSize);
            if (fireMode == ArmoryCoreFireMode.SemiAutomatic || fireMode == ArmoryCoreFireMode.Automatic)
            {
                var result = TryFire(CurrentAimDirection());
                if (fireMode == ArmoryCoreFireMode.Automatic && result == ArmoryCoreShotOutcome.Empty)
                    automaticDryFireLatched = true;
            }
        }

        public ArmoryCoreShotOutcome TryFire()
        {
            return TryFire(CurrentAimDirection());
        }

        public ArmoryCoreShotOutcome TryFire(Vector3 worldDirection)
        {
            var outcome = ValidateShot();
            if (outcome != ArmoryCoreShotOutcome.Fired)
            {
                if (outcome == ArmoryCoreShotOutcome.Empty)
                {
                    PlayOneShot(definition == null ? null : definition.emptySound,
                        definition == null ? 1f : definition.emptyVolume,
                        randomizePitch: false);
                    SetAnimatorTrigger(emptyTrigger);
                    onEmpty.Invoke();
                }
                ShotAttempted?.Invoke(outcome);
                return outcome;
            }

            var interval = 60f / stats.RoundsPerMinute;
            nextShotTime = Mathf.Max(Time.time, nextShotTime) + interval;

            var stoppage = RollMalfunction();
            if (stoppage == ArmoryCoreMalfunctionType.FailureToFire)
            {
                malfunction = stoppage;
                SetAnimatorTrigger(malfunctionTrigger);
                onMalfunction.Invoke();
                ShotAttempted?.Invoke(ArmoryCoreShotOutcome.Malfunction);
                return ArmoryCoreShotOutcome.Malfunction;
            }

            ConsumeRoundForShot();
            EmitProjectiles(worldDirection);
            ApplyShotState();
            PlayShotSound();
            PlayShotPresentation();
            NotifyModulesAboutShot();
            NotifyAmmoChanged();

            if (stoppage != ArmoryCoreMalfunctionType.None)
            {
                if (definition.action != ArmoryCoreWeaponAction.OpenBolt)
                    chamberLoaded = false;
                malfunction = stoppage;
                SetAnimatorTrigger(malfunctionTrigger);
                onMalfunction.Invoke();
            }

            onShot.Invoke();
            ShotAttempted?.Invoke(ArmoryCoreShotOutcome.Fired);
            return ArmoryCoreShotOutcome.Fired;
        }

        public bool StartReload()
        {
            if (definition == null || state != ArmoryCoreWeaponState.Ready || malfunction != ArmoryCoreMalfunctionType.None)
                return false;
            if (reserveRounds <= 0 || roundsInMagazine >= definition.magazineCapacity)
                return false;

            triggerHeld = false;
            automaticDryFireLatched = false;
            burstShotsRemaining = 0;
            state = ArmoryCoreWeaponState.Reloading;
            var empty = !chamberLoaded && roundsInMagazine == 0;
            stateFinishTime = Time.time + ReloadStepDuration(empty);
            PlayOneShot(definition.reloadSound, definition.reloadVolume, randomizePitch: false);
            SetAnimatorTrigger(reloadTrigger);
            onReloadStarted.Invoke();
            return true;
        }

        public bool CancelReload()
        {
            if (state != ArmoryCoreWeaponState.Reloading)
                return false;
            state = ArmoryCoreWeaponState.Ready;
            return true;
        }

        public bool CycleAction()
        {
            if (definition == null || !actionRequired || state != ArmoryCoreWeaponState.Ready)
                return false;
            state = ArmoryCoreWeaponState.Cycling;
            stateFinishTime = Time.time + definition.actionCycleTime;
            SetAnimatorTrigger(cycleTrigger);
            return true;
        }

        public bool StartClearingMalfunction()
        {
            if (malfunction == ArmoryCoreMalfunctionType.None || state != ArmoryCoreWeaponState.Ready)
                return false;
            triggerHeld = false;
            state = ArmoryCoreWeaponState.ClearingMalfunction;
            stateFinishTime = Time.time + MalfunctionClearDuration(malfunction);
            SetAnimatorTrigger(malfunctionTrigger);
            return true;
        }

        public void ServiceWeapon(float durabilityRestored = 1f, float foulingRemoved = 1f)
        {
            durability = Mathf.Clamp01(durability + Mathf.Max(0f, durabilityRestored));
            fouling = Mathf.Clamp01(fouling - Mathf.Max(0f, foulingRemoved));
            malfunction = ArmoryCoreMalfunctionType.None;
            if (state == ArmoryCoreWeaponState.ClearingMalfunction)
                state = ArmoryCoreWeaponState.Ready;
        }

        public bool SelectAttachment(ArmoryCoreAttachmentSlot slot, string attachmentId)
        {
            return SelectAttachment(SlotName(slot), attachmentId);
        }

        public bool SelectAttachment(string slot, string attachmentId)
        {
            var slotRoot = GetAttachmentSlotRoot(slot);
            if (slotRoot == null)
                return false;

            var requestedId = attachmentId?.Trim();
            Transform selectedOption = null;
            if (!string.IsNullOrWhiteSpace(requestedId))
            {
                for (var index = 0; index < slotRoot.childCount; index++)
                {
                    var option = slotRoot.GetChild(index);
                    var module = option.GetComponent<ArmoryCoreAttachmentModule>();
                    var attachment = module == null ? null : module.Definition;
                    var matches = string.Equals(attachment?.id, requestedId, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(attachment?.displayName, requestedId, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(option.name, requestedId, StringComparison.OrdinalIgnoreCase);
                    if (!matches)
                        continue;
                    if (attachment != null && !CanInstall(attachment))
                        return false;
                    selectedOption = option;
                    break;
                }
                if (selectedOption == null)
                    return false;
            }

            for (var index = 0; index < slotRoot.childCount; index++)
                slotRoot.GetChild(index).gameObject.SetActive(slotRoot.GetChild(index) == selectedOption);
            RebuildInstalledAttachments();
            return selectedOption != null || string.IsNullOrWhiteSpace(requestedId);
        }

        public bool ClearAttachmentSlot(ArmoryCoreAttachmentSlot slot)
        {
            return SelectAttachment(slot, null);
        }

        public bool ClearAttachmentSlot(string slot)
        {
            return SelectAttachment(slot, null);
        }

        public string GetActiveAttachment(ArmoryCoreAttachmentSlot slot)
        {
            return GetActiveAttachment(SlotName(slot));
        }

        public string GetActiveAttachment(string slot)
        {
            var slotRoot = GetAttachmentSlotRoot(slot);
            if (slotRoot == null)
                return string.Empty;
            for (var index = 0; index < slotRoot.childCount; index++)
            {
                var option = slotRoot.GetChild(index);
                if (!option.gameObject.activeSelf)
                    continue;
                var module = option.GetComponent<ArmoryCoreAttachmentModule>();
                return string.IsNullOrWhiteSpace(module?.Definition?.id)
                    ? option.name
                    : module.Definition.id;
            }
            return string.Empty;
        }

        public Transform GetAttachmentSlotRoot(ArmoryCoreAttachmentSlot slot)
        {
            return GetAttachmentSlotRoot(SlotName(slot));
        }

        public Transform GetAttachmentSlotRoot(string slot)
        {
            return transform.Find(GetSlotObjectName(slot));
        }

        public bool CanInstall(ArmoryCoreAttachmentDefinition attachment)
        {
            if (definition == null || attachment == null)
                return false;

            var compatibleIds = definition.compatibleAttachmentIds;
            if (compatibleIds != null && compatibleIds.Length > 0)
            {
                var idFound = false;
                for (var i = 0; i < compatibleIds.Length; i++)
                {
                    if (string.Equals(compatibleIds[i], attachment.id, StringComparison.OrdinalIgnoreCase))
                    {
                        idFound = true;
                        break;
                    }
                }
                if (!idFound)
                    return false;
            }

            var tags = attachment.requiredWeaponTags;
            if (tags == null)
                return true;
            for (var i = 0; i < tags.Length; i++)
            {
                if (!HasTag(definition.weaponTags, tags[i]))
                    return false;
            }
            return true;
        }

        public void RebuildInstalledAttachments()
        {
            previousInstalledModules.Clear();
            previousInstalledModules.AddRange(installedModules);
            installedAttachments.Clear();
            installedModules.Clear();
            var modules = GetComponentsInChildren<ArmoryCoreAttachmentModule>(false);
            for (var i = 0; i < modules.Length; i++)
            {
                var attachment = modules[i].Definition;
                if (attachment != null && CanInstall(attachment))
                {
                    installedModules.Add(modules[i]);
                    installedAttachments.Add(attachment);
                }
            }

            for (var i = 0; i < previousInstalledModules.Count; i++)
            {
                var previous = previousInstalledModules[i];
                if (previous != null && !installedModules.Contains(previous))
                    previous.OnRemoved(this);
            }
            for (var i = 0; i < installedModules.Count; i++)
            {
                var current = installedModules[i];
                if (current != null && !previousInstalledModules.Contains(current))
                    current.OnInstalled(this);
            }

            UpdateMuzzlePose();
            stats = ArmoryCoreWeaponRuntimeStats.Build(definition, installedModules);
            ConfigurationChanged?.Invoke();
        }

        private void UpdateMuzzlePose()
        {
            if (muzzle == null)
                return;

            Transform activeMuzzlePoint = null;
            for (var i = 0; i < installedModules.Count; i++)
            {
                var module = installedModules[i];
                if (module?.Definition == null
                    || module.Definition.slot != ArmoryCoreAttachmentSlot.Barrel
                    || module.MuzzlePoint == null)
                    continue;
                activeMuzzlePoint = module.MuzzlePoint;
                break;
            }

            if (activeMuzzlePoint != null)
            {
                muzzle.SetPositionAndRotation(activeMuzzlePoint.position, activeMuzzlePoint.rotation);
                return;
            }

            if (!baseMuzzlePoseConfigured)
                return;
            muzzle.localPosition = baseMuzzleLocalPosition;
            muzzle.localRotation = baseMuzzleLocalRotation;
        }

        private void UpdateTrigger()
        {
            if (state != ArmoryCoreWeaponState.Ready || malfunction != ArmoryCoreMalfunctionType.None || Time.time < nextShotTime)
                return;
            if (fireMode == ArmoryCoreFireMode.Automatic && triggerHeld && !automaticDryFireLatched)
            {
                var result = TryFire(CurrentAimDirection());
                if (result == ArmoryCoreShotOutcome.Empty)
                    automaticDryFireLatched = true;
            }
            else if (fireMode == ArmoryCoreFireMode.Burst && burstShotsRemaining > 0)
            {
                var result = TryFire(CurrentAimDirection());
                if (result == ArmoryCoreShotOutcome.Fired)
                    burstShotsRemaining--;
                else if (result != ArmoryCoreShotOutcome.Cooldown)
                    burstShotsRemaining = 0;
            }
        }

        private void UpdateTimedState()
        {
            if (state == ArmoryCoreWeaponState.Ready || Time.time < stateFinishTime)
                return;

            if (state == ArmoryCoreWeaponState.Reloading)
            {
                if (definition.reloadType == ArmoryCoreReloadType.InternalMagazine
                    || definition.reloadType == ArmoryCoreReloadType.SingleRound)
                {
                    LoadRounds(1);
                    if (roundsInMagazine < definition.magazineCapacity && reserveRounds > 0)
                    {
                        stateFinishTime = Time.time + definition.perRoundReloadTime * ReloadMultiplier();
                        return;
                    }
                }
                else
                {
                    LoadRounds(definition.magazineCapacity - roundsInMagazine);
                }

                ChamberAfterReloadIfNeeded();
                state = ArmoryCoreWeaponState.Ready;
                NotifyAmmoChanged();
                onReloadCompleted.Invoke();
                return;
            }

            if (state == ArmoryCoreWeaponState.Cycling)
            {
                actionRequired = false;
                TryChamberFromMagazine();
                state = ArmoryCoreWeaponState.Ready;
                NotifyAmmoChanged();
                return;
            }

            if (state == ArmoryCoreWeaponState.ClearingMalfunction)
            {
                if (malfunction == ArmoryCoreMalfunctionType.DoubleFeed && roundsInMagazine > 0)
                    roundsInMagazine--;
                if (malfunction == ArmoryCoreMalfunctionType.FailureToFire)
                {
                    if (definition.action == ArmoryCoreWeaponAction.OpenBolt && roundsInMagazine > 0)
                        roundsInMagazine--;
                    else
                        chamberLoaded = false;
                }
                malfunction = ArmoryCoreMalfunctionType.None;
                TryChamberFromMagazine();
                state = ArmoryCoreWeaponState.Ready;
                NotifyAmmoChanged();
                onMalfunctionCleared.Invoke();
            }
        }

        private ArmoryCoreShotOutcome ValidateShot()
        {
            if (definition == null || definition.ammunition == null || muzzle == null)
                return ArmoryCoreShotOutcome.InvalidConfiguration;
            if (fireMode == ArmoryCoreFireMode.Safe)
                return ArmoryCoreShotOutcome.Safe;
            if (state == ArmoryCoreWeaponState.Reloading || state == ArmoryCoreWeaponState.ClearingMalfunction)
                return ArmoryCoreShotOutcome.Reloading;
            if (state == ArmoryCoreWeaponState.Cycling || actionRequired)
                return ArmoryCoreShotOutcome.ActionRequired;
            if (malfunction != ArmoryCoreMalfunctionType.None)
                return ArmoryCoreShotOutcome.Malfunction;
            if (Time.time < nextShotTime)
                return ArmoryCoreShotOutcome.Cooldown;
            if (!HasRoundReady())
                return ArmoryCoreShotOutcome.Empty;
            return ArmoryCoreShotOutcome.Fired;
        }

        private void EmitProjectiles(Vector3 worldDirection)
        {
            var baseDirection = worldDirection.sqrMagnitude > 0.0001f ? worldDirection.normalized : muzzle.forward;
            baseDirection = CompensateAimForSightZero(baseDirection);
            var heatRatio = Mathf.Clamp01(heat / Mathf.Max(0.01f, definition.heatForAccuracyPenalty));
            var stanceMultiplier = isAiming ? 1f : definition.hipFireSpreadMultiplier;
            var movementMultiplier = Mathf.Lerp(1f, definition.movingSpreadMultiplier, movementAmount);
            var moa = stats.AccuracyMinutesOfAngle
                * stanceMultiplier
                * movementMultiplier
                * Mathf.Lerp(1f, definition.heatSpreadMultiplier, heatRatio);
            var coneHalfAngleDegrees = moa / 60f * 0.5f;

            for (var i = 0; i < definition.projectilesPerShot; i++)
            {
                var direction = ApplyConeSpread(baseDirection, coneHalfAngleDegrees);
                ArmoryCoreBallisticProjectile.Launch(new ArmoryCoreProjectileLaunchData
                {
                    position = muzzle.position,
                    direction = direction,
                    muzzleVelocityMetersPerSecond = stats.MuzzleVelocityMetersPerSecond,
                    ammunition = definition.ammunition,
                    baseDamage = definition.damagePerProjectile,
                    hitMask = hitMask,
                    owner = transform.root.gameObject
                });
            }
        }

        private Vector3 ApplyConeSpread(Vector3 direction, float halfAngleDegrees)
        {
            if (halfAngleDegrees <= 0f)
                return direction;
            var radial = Mathf.Sqrt((float)random.NextDouble());
            var angle = (float)random.NextDouble() * Mathf.PI * 2f;
            var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radial;
            var basis = Quaternion.LookRotation(direction, Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f
                ? Vector3.forward
                : Vector3.up);
            return (basis * Quaternion.Euler(
                offset.y * halfAngleDegrees,
                offset.x * halfAngleDegrees,
                0f) * Vector3.forward).normalized;
        }

        private ArmoryCoreMalfunctionType RollMalfunction()
        {
            var heatPenalty = 1f + Mathf.Pow(heat / Mathf.Max(1f, definition.heatForAccuracyPenalty), 2f) * 2f;
            var conditionPenalty = 1f + fouling * 8f + (1f - durability) * 12f;
            var probability = heatPenalty * conditionPenalty / Mathf.Max(1f, stats.MeanRoundsBetweenFailures);
            if (random.NextDouble() >= Mathf.Clamp(probability, 0f, 0.35f))
                return ArmoryCoreMalfunctionType.None;
            var roll = random.NextDouble();
            if (roll < 0.36)
                return ArmoryCoreMalfunctionType.FailureToFire;
            if (roll < 0.66)
                return ArmoryCoreMalfunctionType.FailureToFeed;
            if (roll < 0.88)
                return ArmoryCoreMalfunctionType.Stovepipe;
            return ArmoryCoreMalfunctionType.DoubleFeed;
        }

        private void ConsumeRoundForShot()
        {
            if (definition.action == ArmoryCoreWeaponAction.OpenBolt)
                roundsInMagazine = Mathf.Max(0, roundsInMagazine - 1);
            else
                chamberLoaded = false;
        }

        private void ApplyShotState()
        {
            heat += definition.heatPerShot;
            durability = Mathf.Clamp01(durability - definition.durabilityLossPerShot);
            fouling = Mathf.Clamp01(fouling + definition.foulingPerShot);
            lastShotTime = Time.time;

            var vertical = stats.VerticalRecoilDegrees
                * Mathf.Lerp(1f - definition.recoilRandomness, 1f + definition.recoilRandomness, (float)random.NextDouble());
            var horizontal = stats.HorizontalRecoilDegrees
                * Mathf.Lerp(-1f, 1f, (float)random.NextDouble());
            var impulse = new Vector2(horizontal, vertical);
            recoilOffset += impulse;
            RecoilApplied?.Invoke(impulse);

            if (definition.action == ArmoryCoreWeaponAction.Pump || HasAnimatedManualActionPart())
            {
                actionRequired = true;
                CycleAction();
                return;
            }
            if (definition.action == ArmoryCoreWeaponAction.Manual
                || definition.action == ArmoryCoreWeaponAction.BreakAction)
            {
                actionRequired = true;
                return;
            }
            if (definition.action != ArmoryCoreWeaponAction.OpenBolt)
                TryChamberFromMagazine();
        }

        private bool HasAnimatedManualActionPart()
        {
            if (definition == null
                || (definition.action != ArmoryCoreWeaponAction.Manual
                    && definition.action != ArmoryCoreWeaponAction.BreakAction))
                return false;
            var transforms = GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < transforms.Length; index++)
            {
                if (string.Equals(
                    transforms[index].name,
                    "pumpModel",
                    StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private void RecoverRecoil()
        {
            if (definition == null || Time.time - lastShotTime < definition.recoilRecoveryDelay)
                return;
            recoilOffset = Vector2.MoveTowards(
                recoilOffset,
                Vector2.zero,
                definition.recoilRecoveryDegreesPerSecond * Time.deltaTime);
        }

        private void UpdateCookOff()
        {
            if (definition == null
                || heat <= definition.heatForCookOff
                || !HasRoundReady()
                || state != ArmoryCoreWeaponState.Ready
                || Time.time < nextShotTime)
                return;
            var excess = heat / Mathf.Max(0.01f, definition.heatForCookOff) - 1f;
            if (random.NextDouble() < Mathf.Clamp01(excess * 0.03f * Time.deltaTime))
            {
                var selectedMode = fireMode;
                if (fireMode == ArmoryCoreFireMode.Safe)
                    fireMode = ArmoryCoreFireMode.SemiAutomatic;
                TryFire(muzzle == null ? transform.forward : muzzle.forward);
                fireMode = selectedMode;
            }
        }

        private void ChamberAfterReloadIfNeeded()
        {
            if (definition.chamberCapacity == 0 || definition.action == ArmoryCoreWeaponAction.OpenBolt || chamberLoaded)
                return;
            TryChamberFromMagazine();
        }

        private bool TryChamberFromMagazine()
        {
            if (definition == null || definition.chamberCapacity == 0 || roundsInMagazine <= 0 || chamberLoaded)
                return false;
            roundsInMagazine--;
            chamberLoaded = true;
            return true;
        }

        private bool HasRoundReady()
        {
            return definition != null && (definition.action == ArmoryCoreWeaponAction.OpenBolt
                ? roundsInMagazine > 0
                : chamberLoaded);
        }

        private void LoadRounds(int requested)
        {
            var amount = Mathf.Min(
                Mathf.Max(0, requested),
                Mathf.Min(reserveRounds, definition.magazineCapacity - roundsInMagazine));
            roundsInMagazine += amount;
            reserveRounds -= amount;
        }

        private float ReloadStepDuration(bool empty)
        {
            if (definition.reloadType == ArmoryCoreReloadType.InternalMagazine
                || definition.reloadType == ArmoryCoreReloadType.SingleRound)
                return definition.perRoundReloadTime * ReloadMultiplier();
            return empty ? stats.EmptyReloadSeconds : stats.TacticalReloadSeconds;
        }

        private float ReloadMultiplier()
        {
            return definition.tacticalReloadTime <= 0f
                ? 1f
                : stats.TacticalReloadSeconds / definition.tacticalReloadTime;
        }

        private static float MalfunctionClearDuration(ArmoryCoreMalfunctionType type)
        {
            return type switch
            {
                ArmoryCoreMalfunctionType.FailureToFire => 1.1f,
                ArmoryCoreMalfunctionType.FailureToFeed => 1.4f,
                ArmoryCoreMalfunctionType.Stovepipe => 1.25f,
                ArmoryCoreMalfunctionType.DoubleFeed => 3.6f,
                _ => 0f
            };
        }

        private void ConfigureAudioSource()
        {
            if (audioSource == null)
                return;
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.dopplerLevel = 0.25f;
            if (definition == null)
                return;
            audioSource.minDistance = definition.audioMinDistanceMeters;
            audioSource.maxDistance = definition.audioMaxDistanceMeters;
        }

        private void UpdatePlayerInput()
        {
            if (!processPlayerInput)
                return;
            ResolveAimCamera();

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            var fireHeld = mouse != null && mouse.leftButton.isPressed;
            var aiming = mouse != null && mouse.rightButton.isPressed;
            var reloadPressed = keyboard != null && keyboard.rKey.wasPressedThisFrame;
            var fireModePressed = keyboard != null && keyboard.bKey.wasPressedThisFrame;
            var cyclePressed = keyboard != null && keyboard.cKey.wasPressedThisFrame;
            var clearPressed = keyboard != null && keyboard.tKey.wasPressedThisFrame;
            var lightPressed = keyboard != null && keyboard.fKey.wasPressedThisFrame;
            var utilityPressed = keyboard != null && keyboard.zKey.wasPressedThisFrame;
            var zoomDelta = mouse == null ? 0f : mouse.scroll.ReadValue().y;
#else
            var fireHeld = Input.GetMouseButton(0);
            var aiming = Input.GetMouseButton(1);
            var reloadPressed = Input.GetKeyDown(KeyCode.R);
            var fireModePressed = Input.GetKeyDown(KeyCode.B);
            var cyclePressed = Input.GetKeyDown(KeyCode.C);
            var clearPressed = Input.GetKeyDown(KeyCode.T);
            var lightPressed = Input.GetKeyDown(KeyCode.F);
            var utilityPressed = Input.GetKeyDown(KeyCode.Z);
            var zoomDelta = Input.mouseScrollDelta.y;
#endif

            SetAiming(aiming);
            SetTriggerHeld(fireHeld);
            if (reloadPressed)
                StartReload();
            if (fireModePressed)
                CycleFireMode();
            if (cyclePressed)
                CycleAction();
            if (clearPressed)
                StartClearingMalfunction();
            if (lightPressed)
                ToggleFlashlight();
            if (utilityPressed && !ToggleBipod())
                ToggleStock();
            if (Mathf.Abs(zoomDelta) > 0.01f)
                CycleSightMagnification(zoomDelta > 0f ? 1 : -1);
        }

        private void UpdatePresentation()
        {
            if (muzzleLight != null && muzzleLight.enabled && Time.time >= muzzleLightOffTime)
                muzzleLight.enabled = false;

            if (recoilTarget != null)
            {
                CaptureRecoilTargetPose();
                var recoilDirection = muzzle == null
                    ? Vector3.back
                    : recoilTarget.parent == null
                        ? -muzzle.forward
                        : recoilTarget.parent.InverseTransformDirection(-muzzle.forward);
                recoilTarget.localPosition = recoilTargetBaseLocalPosition
                    + recoilDirection.normalized * recoilOffset.y * visualKickMetersPerDegree;
                recoilTarget.localRotation = recoilTargetBaseLocalRotation
                    * Quaternion.Euler(-recoilOffset.y, recoilOffset.x, 0f);
            }

            if (aimCamera == null || !controlCameraFieldOfView)
                return;
            CaptureCameraFieldOfView();
            var target = isAiming
                ? FieldOfViewForMagnification(aimFieldOfView, Mathf.Max(1f, stats.Magnification))
                : hipFieldOfView;
            aimCamera.fieldOfView = Mathf.Lerp(
                aimCamera.fieldOfView,
                target,
                1f - Mathf.Exp(-fieldOfViewSpeed * Time.unscaledDeltaTime));
        }

        private void PlayShotPresentation()
        {
            SetAnimatorTrigger(fireTrigger);

            if (muzzleFlash != null && muzzleFlashParticles > 0 && stats.MuzzleFlashMultiplier > 0.001f)
            {
                var particles = Mathf.Max(1, Mathf.RoundToInt(muzzleFlashParticles * stats.MuzzleFlashMultiplier));
                muzzleFlash.Emit(particles);
            }

            if (muzzleLight != null && stats.MuzzleFlashMultiplier > 0.001f)
            {
                muzzleLight.enabled = true;
                muzzleLightOffTime = Time.time + muzzleLightDuration * Mathf.Clamp(stats.MuzzleFlashMultiplier, 0.25f, 1f);
            }

            if (casingPrefab == null || casingEjectionPoint == null)
                return;
            var casing = Instantiate(casingPrefab, casingEjectionPoint.position, casingEjectionPoint.rotation);
            var direction = (casingEjectionPoint.right
                + casingEjectionPoint.up * UnityEngine.Random.Range(0.2f, 0.65f)
                + UnityEngine.Random.insideUnitSphere * 0.12f).normalized;
            casing.AddForce(direction * casingEjectionForce, ForceMode.Impulse);
            casing.AddTorque(UnityEngine.Random.insideUnitSphere * casingEjectionTorque, ForceMode.Impulse);
            Destroy(casing.gameObject, casingLifetime);
        }

        private void NotifyModulesAboutShot()
        {
            for (var i = 0; i < installedModules.Count; i++)
                installedModules[i]?.OnWeaponShot();
        }

        private Vector3 CompensateAimForSightZero(Vector3 direction)
        {
            if (!compensateSightZero || muzzle == null || definition?.ammunition == null)
                return direction;
            var sight = ActiveSight;
            if (sight == null)
                return direction;

            var distance = Mathf.Max(1f, sight.CurrentZeroDistanceMeters);
            var velocity = Mathf.Max(1f, stats.MuzzleVelocityMetersPerSecond);
            var flightTime = distance / velocity;
            var origin = aimReference == null ? muzzle.position : aimReference.position;
            var gravityCompensation = -Physics.gravity
                * definition.ammunition.gravityMultiplier
                * (0.5f * flightTime * flightTime);
            var target = origin + direction.normalized * distance + gravityCompensation;
            var corrected = target - muzzle.position;
            return corrected.sqrMagnitude > 0.0001f ? corrected.normalized : direction;
        }

        private void ResolveAimCamera()
        {
            if (aimCamera == null && autoFindMainCamera)
                aimCamera = Camera.main;
            if (aimCamera == null)
                return;
            aimReference = aimCamera.transform;
            CaptureCameraFieldOfView();
        }

        private void CaptureCameraFieldOfView()
        {
            if (aimCamera == null || cameraFieldOfViewCaptured)
                return;
            originalCameraFieldOfView = aimCamera.fieldOfView;
            cameraFieldOfViewCaptured = true;
            if (Mathf.Approximately(hipFieldOfView, 60f))
                hipFieldOfView = originalCameraFieldOfView;
        }

        private void RestoreCameraFieldOfView()
        {
            if (aimCamera != null && cameraFieldOfViewCaptured)
                aimCamera.fieldOfView = originalCameraFieldOfView;
            cameraFieldOfViewCaptured = false;
        }

        private void CaptureRecoilTargetPose()
        {
            if (recoilTarget == null || recoilTargetPoseCaptured)
                return;
            recoilTargetBaseLocalPosition = recoilTarget.localPosition;
            recoilTargetBaseLocalRotation = recoilTarget.localRotation;
            recoilTargetPoseCaptured = true;
        }

        private static float FieldOfViewForMagnification(float baseFieldOfView, float magnification)
        {
            var radians = baseFieldOfView * Mathf.Deg2Rad;
            return 2f * Mathf.Atan(Mathf.Tan(radians * 0.5f) / Mathf.Max(1f, magnification)) * Mathf.Rad2Deg;
        }

        private void NotifyAmmoChanged()
        {
            onAmmoChanged.Invoke(TotalRoundsInWeapon, reserveRounds);
        }

        private void UpdateAnimatorState()
        {
            SetAnimatorBool(aimingParameter, isAiming);
            SetAnimatorInteger(fireModeParameter, (int)fireMode);
        }

        private void SetAnimatorTrigger(string parameter)
        {
            if (animator != null && HasAnimatorParameter(parameter, AnimatorControllerParameterType.Trigger))
                animator.SetTrigger(parameter);
        }

        private void SetAnimatorBool(string parameter, bool value)
        {
            if (animator != null && HasAnimatorParameter(parameter, AnimatorControllerParameterType.Bool))
                animator.SetBool(parameter, value);
        }

        private void SetAnimatorInteger(string parameter, int value)
        {
            if (animator != null && HasAnimatorParameter(parameter, AnimatorControllerParameterType.Int))
                animator.SetInteger(parameter, value);
        }

        private bool HasAnimatorParameter(string parameter, AnimatorControllerParameterType type)
        {
            if (animator == null || string.IsNullOrWhiteSpace(parameter))
                return false;
            var parameters = animator.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == type && parameters[i].name == parameter)
                    return true;
            }
            return false;
        }

        private void PlayShotSound()
        {
            if (definition == null)
                return;
            AudioClip moduleOverride = null;
            for (var index = 0; index < installedModules.Count; index++)
            {
                if (installedModules[index] == null || installedModules[index].ShotSoundOverride == null)
                    continue;
                moduleOverride = installedModules[index].ShotSoundOverride;
                break;
            }

            var clip = moduleOverride != null
                ? moduleOverride
                : stats.Suppressed && definition.suppressedShotSound != null
                ? definition.suppressedShotSound
                : definition.shotSound;
            var pressure = Mathf.Clamp(stats.SoundPressureMultiplier, 0.05f, 1f);
            PlayOneShot(clip, definition.shotVolume * Mathf.Sqrt(pressure), randomizePitch: true);
        }

        private void PlayOneShot(AudioClip clip, float volume, bool randomizePitch)
        {
            if (clip == null)
                return;
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                ConfigureAudioSource();
            }
            if (audioSource == null)
                return;

            audioSource.pitch = randomizePitch && definition != null
                ? 1f + UnityEngine.Random.Range(-definition.shotPitchVariation, definition.shotPitchVariation)
                : 1f;
            audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        public float CycleSightMagnification(int direction = 1)
        {
            var sight = ActiveSight;
            return sight == null ? 1f : sight.CycleMagnification(direction);
        }

        public bool ToggleFlashlight()
        {
            var flashlight = FindInstalledModule<ArmoryCoreFlashlightModule>();
            if (flashlight == null)
                return false;
            flashlight.ToggleLight();
            return true;
        }

        public bool ToggleBipod()
        {
            var grip = FindInstalledModule<ArmoryCoreGripModule>();
            return grip != null && grip.SupportsDeployment && grip.ToggleDeployed();
        }

        public bool ToggleStock()
        {
            var stock = FindInstalledModule<ArmoryCoreStockModule>();
            return stock != null && stock.ToggleFolded();
        }

        public T GetInstalledModule<T>() where T : ArmoryCoreAttachmentModule
        {
            return FindInstalledModule<T>();
        }

        private T FindInstalledModule<T>() where T : ArmoryCoreAttachmentModule
        {
            for (var i = 0; i < installedModules.Count; i++)
            {
                if (installedModules[i] is T match)
                    return match;
            }
            return null;
        }

        private Vector3 CurrentAimDirection()
        {
            if (aimReference != null)
                return aimReference.forward;
            var sight = ActiveSight;
            if (sight != null && sight.AimPoint != null)
                return sight.AimPoint.forward;
            if (muzzle != null)
                return muzzle.forward;
            return transform.forward;
        }

        private ArmoryCoreFireMode FirstUsableFireMode()
        {
            if (definition == null || definition.fireModes == null || definition.fireModes.Length == 0)
                return ArmoryCoreFireMode.Safe;
            for (var i = 0; i < definition.fireModes.Length; i++)
            {
                if (definition.fireModes[i] != ArmoryCoreFireMode.Safe)
                    return definition.fireModes[i];
            }
            return definition.fireModes[0];
        }

        private static bool HasTag(string[] tags, string required)
        {
            if (tags == null || string.IsNullOrWhiteSpace(required))
                return false;
            for (var i = 0; i < tags.Length; i++)
            {
                if (string.Equals(tags[i], required, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string SlotName(ArmoryCoreAttachmentSlot slot)
        {
            return slot switch
            {
                ArmoryCoreAttachmentSlot.Sights => "sights",
                ArmoryCoreAttachmentSlot.Barrel => "barrel",
                ArmoryCoreAttachmentSlot.Grip => "grip",
                ArmoryCoreAttachmentSlot.Stock => "stock",
                ArmoryCoreAttachmentSlot.Magazine => "magazine",
                _ => "generic"
            };
        }

        private static string GetSlotObjectName(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot))
                return "_All";
            return slot.Trim().ToLowerInvariant() switch
            {
                "sights" => "Sights_All",
                "barrel" => "Barrel_All",
                "grip" => "Grip_All",
                "stock" => "Stock_All",
                "magazine" => "Magazine_All",
                "generic" => "Generic_All",
                _ => slot.Trim() + "_All"
            };
        }
    }
}
