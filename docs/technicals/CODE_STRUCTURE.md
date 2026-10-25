# Code structure

Map of the scripts that exist today: where each one lives, what it owns, and how they connect. The *why* (layers, scopes, Core vs Features) is in [ARCHITECTURE.md](ARCHITECTURE.md). **Update this file whenever you add, move or remove a script.**

## Assemblies and namespaces

| Assembly | Folder | References |
| --- | --- | --- |
| `Aipocalypse.Core` | `Assets/_Game/_Core/` | VContainer, MessagePipe (+ .VContainer), UniTask, Unity.InputSystem, Unity.Cinemachine, UnityEngine.UI, Unity.TextMeshPro, AstarPathfindingProject, PackageTools, Drawing, Unity.Animation.Rigging, DOTween.Modules, MoreMountains.Tools, BroAudio, DamageNumbersPro, Unity.RenderPipelines.Core.Runtime, Unity.RenderPipelines.Universal.Runtime; precompiled plugin DLLs auto-referenced |
| `Aipocalypse.Tests.EditMode` | `Assets/_Game/Tests/EditMode/` | `Aipocalypse.Core`, VContainer, MessagePipe (+ .VContainer), UniTask, BroAudio, UnityEngine.TestRunner, UnityEditor.TestRunner; precompiled (overrideReferences) `nunit.framework.dll`, `DOTween.dll`. Editor only |
| `Aipocalypse.Tests.PlayMode` | `Assets/_Game/Tests/PlayMode/` | `Aipocalypse.Core`, VContainer, test runner, NUnit |

Namespaces follow the folder: `Aipocalypse.Core.<Folder>` (e.g. `Aipocalypse.Core.PlayerSystem`, `Aipocalypse.Core.Combat`). One exception: input uses **`Aipocalypse.Input`**, because a namespace ending in `InputSystem` shadows `UnityEngine.InputSystem.InputSystem`.

## Script map

```text
Assets/_Game/
├─ _Core/                                   asmdef Aipocalypse.Core
│  ├─ _Bootstrap/                           Aipocalypse.Core.Bootstrap
│  │  ├─ ProjectLifetimeScope.cs            root scope: MessagePipe, InputInstaller, AudioInstaller, CombatInstaller, TimeInstaller, GraphicsInstaller, QuickTimeInstaller, PlayerInstaller, WeaponInstaller
│  │  ├─ ProjectLifetimeScope.prefab        holds ControlSettings (mouse sensitivity, invert Y, field of view…)
│  │  ├─ VContainerSettings.asset           Preloaded Asset → spawns the root prefab before any scene
│  │  ├─ GameLifetimeScope.cs               per-scene scope: installs every SceneInstaller on its GameObject, reports one with a Problem (warning, error when Strict); a Strict scene also reports each SceneInstaller type it lacks; execution order -5000
│  │  └─ SceneInstaller.cs                  abstract MonoBehaviour + IInstaller next to the GameLifetimeScope: one scene system's settings and registrations; Problem (null = ready)
│  ├─ _Common/
│  │  ├─ Entities/EntityScope.cs            Aipocalypse.Core.Entities — base child scope for unique entities, RegisterDefault
│  │  ├─ Entities/IPlayerTarget.cs          IDamageable + Transform: what enemies and companions need from the player
│  │  ├─ Combat/                            Aipocalypse.Core.Combat
│  │  │  ├─ IDamageable.cs                  anything that can take damage: TakeDamage → DamageResult (hurt it through DamageService)
│  │  │  ├─ DamageInfo.cs                   DamageInfo struct (amount, type, point, knockback Force, Source) + DamageType enum (Generic, Fall, Gunshot, Melee) + DamageResult (Applied, Killed)
│  │  │  ├─ DamageService.cs                root service: Deal(target, info) → TakeDamage, publishes DamageDealt with the result; DealHit(hit, info) for physics hits (finds the IDamageable, pushes a loose rigidbody)
│  │  │  ├─ CombatInstaller.cs              registers DamageService in the root scope
│  │  │  └─ Health.cs                       plain HP meter: Damage / Heal / Restore; SetMax keeps the current share
│  │  ├─ Events/CombatEvents.cs             Aipocalypse.Core.Events — DamageDealt (target, info, result), PlayerDied, PlayerDeathSequenceEnded (the death sequence played out: the respawn starts)
│  │  ├─ Events/PlayerEvents.cs             ActivePlayerChanged (read PlayerRegistry.Current), PlayerRespawnFading (async: fade the view out), PlayerRespawning (async: reset for a respawn), RespawnStage + RespawnStageChanged, PlayerRevived
│  │  ├─ Events/GunJamBeat.cs               owner, item and kind (Jammed, DryFire, Slap, Punch, Miss): presentation beats of a jam and its repair; published by GunRepairFeedback
│  │  ├─ Events/GunRepairCompleted.cs       owner, item and QTE-success flag of a finished repair (interruptions don't publish it)
│  │  ├─ Events/QteEvents.cs                QteStarted (session, presentation, Button), QteAttempted (accepted presses only: Miss / Success), QteEnded (result); no per-frame messages
│  │  ├─ Events/WorldTimeEvents.cs          HourChanged, DayPhaseChanged, DayStarted, TimeJumped, TimeSkipEnded (published by WorldClock)
│  │  ├─ Feedback/FeedbackContext.cs        Aipocalypse.Core.Feedback — injected bridge on hosts of MMF_Players so custom Feel feedbacks reach our services
│  │  ├─ Feedback/MMF_HitStop.cs            Feel feedback "Aipocalypse/Hit Stop" → TimeService.RequestHitStop; never touches Time.timeScale
│  │  ├─ Interaction/IInteractable.cs       Aipocalypse.Core.Interaction — TryGetPrompt / Interact(actor) + InteractionPrompt
│  │  ├─ Stats/IStatProvider.cs             Aipocalypse.Core.Stats — StatId enum (values; …Scale multipliers with base 1) + stat seam with a Changed event; GetScale(stat) extension
│  │  └─ Utils/                             Aipocalypse.Core
│  │     ├─ Damping.cs                      ByRate / ByTime: frame-rate independent smoothing share for Lerp (0 = instant)
│  │     ├─ Spring.cs                       Step(value, velocity, target, stiffness, dampingRatio, dt) for float / Vector2 / Vector3, in 1/120 s substeps (held-item sway, landing dip)
│  │     ├─ SceneCollection.cs              editor only: Collect(owner, list) keeps a serialized list of scene objects current; Hook<TOwner>(collect) runs it on scene save and entering Play (EnemyInstaller, EnvironmentDirector)
│  │     ├─ PhysicsQueries.cs               Nearest(hits, count, ignore): nearest NonAlloc hit not under a root (bullets, swings, look and clearance casts)
│  │     └─ TransformExtensions.cs          SetLayerRecursively (held models, hands, muzzle flashes onto the first-person layer)
│  ├─ _Debug/TargetDummy.cs                 Aipocalypse.Core.Debugging — shooting-range target: logs hits, refills its health (the emptying hit counts as a kill), tips over from knockback, flinches from every hit, springs back
│  ├─ AudioSystem/                          Aipocalypse.Core.AudioSystem
│  │  ├─ AudioService.cs                    root service: Play(SoundID, volume) 2D, PlayAt(SoundID, position) 3D, Start / SetVolume / Pause / UnPause / Stop for a kept 2D sound (loops), over BroAudio (unset ids are silent)
│  │  └─ AudioInstaller.cs                  registers AudioService
│  ├─ EnemySystem/                          Aipocalypse.Core.EnemySystem
│  │  ├─ EnemyInstaller.cs                  SceneInstaller: AstarPath (Navigation prefab), EnemySystemSettings, EnemyRegistry, EnemyManager (entry point); injects the scene's enemies (list collected on scene save / entering Play, "Collect scene enemies" button)
│  │  ├─ EnemySystemSettings.cs             SO: ThinkInterval (1/15 s), MaxThinksPerFrame (200); asset in Data/Enemies/
│  │  ├─ EnemyRegistry.cs                   the enemies in play as a dense swap-remove list; brains add themselves while enabled
│  │  ├─ EnemyManager.cs                    ITickable: thinks count × deltaTime / ThinkInterval Manual trees per frame round-robin (fractions carry, capped at MaxThinksPerFrame, never more than one round owed), scaled time; Advance(deltaTime)
│  │  ├─ EnemyDefinition.cs                 SO per enemy kind: MoveSpeed, StopDistance, Wander (WanderSettings); lives in Content/Enemies/<Name>/
│  │  ├─ EnemyBrain.cs                      injected bridge (requires BehaviorTree + EnemyMover): Definition, Destination; on its first Start (and each re-enable) initializes its mover, starts its tree and registers; stops both and leaves when disabled
│  │  ├─ EnemyMover.cs                      an enemy's legs over A* FollowerEntity: Initialize(navigation, definition), Home, TryFindDestination, MoveTo(destination, timeout), Arrived, TimedOut, Halt
│  │  ├─ WanderTasks.cs                     ChooseWanderDestination / WalkToWanderDestination / PauseWander ActionNodes of the shared subtree (read EnemyBrain.Definition.Wander, move through EnemyMover)
│  │  ├─ WanderSettings.cs                  SO: wander radius, pause, travel timeout, destination attempts, minimum travel; asset in Data/Enemies/, the shared subtree in Content/Enemies/_Shared/
│  │  └─ Prefabs/Navigation.prefab          AstarPath (default: small terrain-following grid, scanned on startup) + RVOSimulator: goes into every scene with agents; WorldMap's instance overrides the graph (recast + cache, ENVIRONMENT.md → Navigation)
│  ├─ EnvironmentSystem/                    Aipocalypse.Core.EnvironmentSystem — scene environment, see ENVIRONMENT.md and DAYNIGHT.md
│  │  ├─ ASPTopLayerGlobals.cs              [ExecuteAlways] sets the ANGRY MESH top-layer shader globals (grass caps on the boundary rocks); on Environment/Boundary in the world map
│  │  ├─ BKFoliageGlobals.cs                [ExecuteAlways] sets the BK Pure Nature plant shader globals (fade distance, micro wind); on Environment/Wind in the world map
│  │  ├─ EnvironmentInstaller.cs            SceneInstaller: RegisterComponent(EnvironmentDirector); optional TimeOfDayAmbience → AmbienceService entry point, only with a ready WorldTimeInstaller on the scope (else a warning)
│  │  ├─ TimeOfDay/EnvironmentDirector.cs   [ExecuteAlways] sole writer of the sun / moon light, ambient, fog, reflections, _TOD_ and Skybox Plus sky globals (Play mode: Skybox Plus moon phase on a runtime copy of the sky material), _StarMapRotation (latitude + sidereal time), Grade From / To volumes; profile → apply → reactors (Play mode); night shadow distance requested from GraphicsService; edit-mode preview; collects scene reactors on save / Play
│  │  ├─ TimeOfDay/TimeOfDayProfile.cs      SO: keys (hour, LightingPreset, grade VolumeProfile, blend) → Evaluate(hour); PhaseLooks (DayPhase → hour, PhaseLookHour); SunPath (sun / moon directions); grade-override warning
│  │  ├─ TimeOfDay/LightingPreset.cs        SO: one look (sun, moon, ambient, reflections, fog, sky, haze, unlit tint) → AtmosphereState
│  │  ├─ TimeOfDay/AtmosphereState.cs       every value the director applies in a frame; Lerp
│  │  ├─ TimeOfDay/EnvironmentReactor.cs    base of scene objects ticked by the director (injected spawns register themselves); TimeOfDayReactor: 0–1 weight from hours / daylight / darkness, eased over real seconds (the fade advances every frame, Apply skips tiny changes), Apply(weight)
│  │  ├─ TimeOfDay/TimeOfDayLight.cs        fades lights to their authored intensity
│  │  ├─ TimeOfDay/TimeOfDayEmission.cs     runtime emissive copies of chosen materials (windows)
│  │  ├─ TimeOfDay/RuntimeMaterials.cs      CopyUnder(root, wanted, makeCopy, copies): one runtime copy per source material on the renderers under an object; Destroy(copies) — used by TimeOfDayEmission and AtmosphereTint
│  │  ├─ TimeOfDay/TimeOfDayParticles.cs    scales / stops emitters, optional align to the sun (god rays)
│  │  ├─ TimeOfDay/TimeOfDayActive.cs       shows objects only during their hours
│  │  ├─ TimeOfDay/TimeOfDayWeight.cs       a weight for other code to read (Weight, -1 before the first tick): the player's darkness for PlayerVision
│  │  ├─ TimeOfDay/AtmosphereTint.cs        multiplies / replaces colour properties on runtime material copies by an atmosphere channel
│  │  ├─ TimeOfDay/TimeOfDayAmbience.cs     SO: sound beds with a volume curve over the day, calls inside hours (BroAudio SoundIDs)
│  │  ├─ TimeOfDay/AmbienceService.cs       plays the ambience against the clock through AudioService (scene entry point); calls wait while TimeService is paused or the clock skips
│  │  ├─ Shaders/SkyStarMap.shader          "Aipocalypse/Skybox/Skybox Plus Star Map": fork of Kronnect Skybox Plus Clouds; STAR_MAP draws a star cubemap (StarMap.png) turned by _StarMapRotation instead of the procedural stars
│  │  └─ Shaders/SkyTimeOfDay.shader        "Aipocalypse/Skybox/Time Of Day": painted panorama + night gradient, horizon glow, sun disc, stars, moon phases from _TOD_ globals
│  ├─ EquipmentSystem/                      Aipocalypse.Core.EquipmentSystem — rules and how-tos in EQUIPMENT.md
│  │  ├─ Items/ItemCategory.cs              [Flags] Gun, Melee: what a hotbar slot accepts; Lantern: goes on the belt
│  │  ├─ Items/ItemDefinition.cs            abstract SO: name, icon, Category, Prefab (one per item: world + hand), MaxStack, Selecting (SelectBehaviour: None, Hold, QuickUse), CreateInstance
│  │  ├─ Items/EquippableDefinition.cs      abstract SO: Selecting = Hold, equip / holster time, HoldFeel; HeldPrefab = the HeldItem on Prefab's root
│  │  ├─ Items/HoldFeel.cs                  per-item feel on top of the player's held-item tuning: AimMoveSpeedScale, SwayScale
│  │  ├─ Items/ItemInstance.cs              one owned item or stack: Count, IsEmpty, CanMerge / Merge, Consume, Changed, virtual Badge (stack count when MaxStack > 1); per-copy state goes in subclasses
│  │  ├─ Items/ItemBadge.cs                 what a hotbar slot shows under the icon: Visible, Count, Warning, Reserve (an AmmoType to look up)
│  │  ├─ Items/LanternDefinition.cs         SO: worn on the belt, not held: radius added to the vision light, colour, edge brightness, flicker, shadows
│  │  ├─ Hotbar.cs                          ≤ 9 slots, category check, SlotChanged, FindNext, FindFreeSlot
│  │  ├─ EquipmentSwitcher.cs               holster → swap → equip timing (Raise 0..1, SwitchPhase)
│  │  ├─ Equipment.cs                       selected slot ↔ switcher target; Select / Cycle (Hold items are held, QuickUse items raise QuickUseRequested); pick up (merge / store / swap; a stored QuickUse item is not selected), DropHeld (mid-switch: the item being put away), Consume(item, amount); Belt (worn item, BeltAccepts); LowerHands / RaiseHands (holster but keep the slot)
│  │  ├─ PickUpAction.cs                    None / Merge / Store / Swap
│  │  ├─ IEquipmentOwner.cs                 on an actor's root: exposes its Equipment to world items
│  │  ├─ WorldItem.cs                       item lying in the world (on the item prefab's root): IInteractable, picks up / swaps; Spawn for drops; Awake shows only the World child; StripForHand
│  │  ├─ EquipmentConfig.cs                 SO: SwitchSpeed, BeltAccepts, slots (accepts, starting item) → CreateHotbar, CreateEquipment
│  │  ├─ HeldItem.cs                        root of an item's first-person prefab; Bind(item, owner), Show / Hide / OnReady, Tick(input), LateTick, ViewKick, LookTurn, IsAiming, AimWeight, KeepsClear, Zoom (field of view multiplier), Spread (shot cone for the crosshair), StopsSprint(use, aim), TryGetTip (model's furthest point from mesh bounds), Hands (its ItemHands)
│  │  ├─ HeldItemInput.cs                   per-frame use / aim / reload wish + holder state (IsReady, sprinting…); ViewKick struct
│  │  ├─ PoseBlender.cs                     a held model's pose pivot easing between pose markers: SnapTo, Set, MoveTowards, ApplyTo; static editor Preview (HeldGun / HeldMelee preview buttons)
│  │  └─ EquipmentRig.cs                    spawns HeldItems (inactive staging → inject → StripForHand → hand), shows the held one, temporary lower/raise pose, TickHeld
│  ├─ GraphicsSystem/                       Aipocalypse.Core.GraphicsSystem
│  │  ├─ GraphicsService.cs                 root service, sole runtime writer of the URP asset's shadow distance: ShadowDistance (player setting; the asset's value until a graphics menu exists), RequestShadowDistance(owner, d) / ReleaseShadowDistance (shortest wins); restores the asset's value on Dispose
│  │  └─ GraphicsInstaller.cs               registers GraphicsService in the root scope
│  ├─ HandSystem/                           Aipocalypse.Core.HandSystem — first-person floating hands, see HANDS.md
│  │  ├─ HandPose.cs                        HandSide, Finger, FingerShape / HandShape (Curl, Spread, Tip per finger); HandPose SO
│  │  ├─ HandRig.cs                         root of a hand prefab: grip point, finger chains + Open / Closed / Spread / Fist calibration, bounded grip-to-fist blending, Place, Apply, forearm axis, contact samples, setup buttons
│  │  ├─ HandSet.cs                         SO: the left and right hand prefabs, implied elbows (view space), preview item position
│  │  ├─ HandGrip.cs                        where a hand holds an item (side, pose, aim at elbow / as authored, roll, baked fit or clip-authored shape); Resolve; Preview hand, Bake fit; FingerMask
│  │  ├─ ItemHands.cs                       on an item prefab: one grip per hand, per-item hand scale; Show hands preview, Bake fit for all grips
│  │  ├─ FirstPersonHands.cs                on the player: spawns the HandSet, puts each hand on the item's grip (elbow-aimed) with its finger shape
│  │  ├─ HandFit.cs                         editor only: Bake fit — palm onto the item, finger curl / tip search against its meshes (signed distance)
│  │  ├─ HandPreview.cs                     editor only: DontSave ghost hands that follow grips in edit mode; edit-mode elbow
│  │  └─ Prefabs/FPHand_Left, FPHand_Right.prefab   the hand prefabs (VR Low Poly Hand), listed in Data/Hands/HandSet.asset
│  ├─ InputSystem/                          Aipocalypse.Input
│  │  ├─ InputService.cs                    the only owner of actions, map switching, cursor lock: RequestUI / ReleaseUI(owner) (gameplay map only while nobody holds a UI request), GameplayEnabled; hotbar keys + cycling, interact, drop, attack, aim, reload; GameButton enum (Reload, Interact, Attack, Jump; sized from the enum, an unmapped button throws) with WasPressed / Consume (claims this frame's press) / GetKeyLabel; UiConfirmPressed (UI map click / submit); Debug map (N / M / K / L) enabled in dev builds
│  │  ├─ InputInstaller.cs                  registers ControlSettings + InputService (entry point)
│  │  ├─ ControlSettings.cs                 player-facing control and view options: mouse sensitivity, gamepad look speed, invert Y, FieldOfView (read live by PlayerZoom)
│  │  └─ GameInputActions.cs                GENERATED from Settings/InputSystem_Actions.inputactions — don't edit
│  ├─ PlayerSystem/                         Aipocalypse.Core.PlayerSystem
│  │  ├─ Player.cs                          view MonoBehaviour: builds its parts, input → intent (look scaled by zoom; aim / trigger end a sprint via HeldItem.StopsSprint) and ticks them in order; IPlayer, IEquipmentOwner, IGunUser (owns the AmmoReserve); IDamageable explicitly (only through DamageService); PlaySequence / StopSequence (dying plays PlayerTuning.DeathSequence); MoveToSpawn, Revive; re-reads max health / stamina on IStatProvider.Changed
│  │  ├─ IPlayer.cs                         IPlayerTarget + Health, Stamina, Equipment, Ammo, HeldItem, Interactor, IsSprinting, CrosshairPosition, CrosshairSpread, SpawnPosition, MoveToSpawn, Revive: the player as PlayerRegistry hands it out
│  │  ├─ PlayerLife.cs                      Health, TakeDamage, fall damage (OnLanded, through DamageService), Died event + PlayerDied, OnDeathSequenceEnded → PlayerDeathSequenceEnded, Revive (Reviving event + PlayerRevived), SpawnPosition / SpawnRotation
│  │  ├─ PlayerFeedback.cs                  footstep per head-bob step (walk / sprint volume), jump and landing sounds, head-bob landing dip and held-sway jump / land kicks (IDisposable)
│  │  ├─ HeldItemDriver.cs                  the item in hand each frame: drop / select / cycle → Equipment.Tick → rig Tick → TickHeld → LookTurn + ViewKick → zoom (unless a pose sequence owns it) → sway + aim → move speed (aim speed × HoldFeel × fatigue) → clearance → LateTick → hands
│  │  ├─ PlayerCameraRig.cs                 sole writer of the CameraRoot / CameraPitch transforms: SetLook(pitch, kick), SetHeadMotion(offset, pitch), Override / ReleaseOverride (pose sequences); applied as each share changes; EyeHeight
│  │  ├─ PlayerScope.cs                     EntityScope on the prefab root; default IStatProvider
│  │  ├─ PlayerInstaller.cs                 root-scope services: PlayerRegistry, PlayerRespawnFlow (entry point) (+ PlayerCheats in dev builds)
│  │  ├─ PlayerRespawnFlow.cs               root IStartable: on PlayerDeathSequenceEnded → Fading (awaits PlayerRespawnFading) → RequestUI → Preparing (MoveToSpawn, awaits PlayerRespawning) → Ready (waits for UiConfirmPressed) → Revive, re-checking cancellation after each await; any PlayerRevived or active-player change ends it (ReleaseUI, stage None); publishes RespawnStageChanged; works without the HUD
│  │  ├─ PlayerCheats.cs                    editor and dev builds: K kills the active player through DamageService, L revives it (InputService debug presses)
│  │  ├─ Sequences/PoseSequence.cs          SO: a scripted first-person moment (death, resting): beats placed on the ground around the start (move + hold: eye height, offset, pitch / yaw / roll, FOV, ease, start / arrive sounds), Continuous (one smooth path, never stops), handheld wander (+ HandheldFadeIn), elliptical drift (+ DriftFadeIn), EndAfter, music, LowerHands
│  │  ├─ Sequences/PoseSequencePlayer.cs    plays one PoseSequence through the camera rig's override (look pitch folded in): beats placed once (ground probe, wall sphere cast), DOVirtual eased moves or a monotone cubic path through the beats (no overshoot into the ground), Perlin handheld from a fresh seed, drift from the last beat on, cues through AudioService, Ended, FovScale
│  │  ├─ PlayerRegistry.cs                  the active player (Current, an IPlayer); players register themselves, publishes ActivePlayerChanged
│  │  ├─ PlayerIntent.cs                    per-frame wish: Move, LookDegrees, Sprint, Jump, SelectSlot, CycleSlot, Interact, Drop, Fire, FirePressed, Aim, Reload
│  │  ├─ PlayerMotor.cs                     CharacterController movement, jump, slopes, landing (+ LandingInfo; sliding down a steep slope counts as falling until stable ground); Jumped / Landed events; SpeedMultiplier (aiming, fatigue); Teleport (no momentum, no fall damage)
│  │  ├─ PlayerLook.cs                      yaw on the body, pitch + the held item's ViewKick (recoil) into the camera rig; Turn (kept recoil), Pitch, ResetPitch (after a sequence)
│  │  ├─ PlayerHeadBob.cs                   footstep bob + landing dip into the camera rig's head motion; Stepped at the bottom of each bob; Phase / Intensity / SprintBlend for the held item's bob
│  │  ├─ HeldItemSway.cs                    held item's procedural motion (+ AimSwaySettings, HeldSwaySettings, HeldSwayInput): AimOffset / AimRotation for the crosshair, shots and model (breathing heavier when tired, walk / strafe bob, move / look lag, jump, air drift, landing, ADS raise), plus model-only position and roll; calmed by AimWeight, scaled by StatId.HeldItemSwayScale × ItemScale (the held item's HoldFeel.SwayScale)
│  │  ├─ PlayerZoom.cs                      sole writer of the lens field of view: ControlSettings.FieldOfView (read live) × the held item's Zoom or a pose sequence's FovScale (MinScale 0.1 … MaxScale 1.5); keeps FirstPersonView on the unzoomed base; LookScale, TanHalfFov
│  │  ├─ PlayerAim.cs                       aim sway → CrosshairPosition and CrosshairSpread on screen (world FOV) + the rig's zoom-corrected turn (EquipmentRig.SetAimRotation); static ScreenPosition / ScreenRadius / ModelRotation
│  │  ├─ PlayerBreathSound.cs               loops PlayerSoundTuning.BreathSound while out of breath (+ BreathSoundSettings: MinVolume, FadeIn, FadeOut; VolumeFor(tiredness, minVolume)), pauses it while TimeService is paused
│  │  ├─ PlayerInteractor.cs                look sphere cast → focused IInteractable + prompt, Interact; drop point
│  │  ├─ HeldItemClearance.cs               eye → normal model tip sphere cast; smooth hand retraction / tilt near obstacles, never moves the player
│  │  ├─ FirstPersonView.cs                 on the world camera: the lens held models draw with (own FOV via SetFieldOfView so zoom doesn't swell the gun, near 0.01 / far 10 m)
│  │  ├─ FirstPersonRenderFeature.cs        URP renderer feature on PC_Renderer: draws the HeldWeapon layer in the world camera after transparents, depth cleared, with FirstPersonView's lens (replaces the overlay camera)
│  │  ├─ Stamina.cs                         stamina meter: spend (pauses regen), regen only while resting after a delay, exhaustion, winded wait after running dry; SetMax keeps the current share
│  │  ├─ Fatigue.cs                         low-stamina tiers (+ FatigueSettings, FatigueTier) → eased recoil / melee power / move speed multipliers, slower still while winded; StatId.FatiguePenaltyScale
│  │  ├─ PlayerBreathing.cs                 out of breath (+ BreathingSettings: Below, EaseTime): IsActive, eased Tiredness for the breath loop and the aim sway
│  │  ├─ FallDamage.cs                      drop height → damage
│  │  ├─ PlayerVision.cs                    light around the player in the dark: radius (StatId.VisionRadius + worn lantern) → intensity that keeps the edge brightness under URP falloff, range, colour, position; eased; off by day
│  │  ├─ Tuning/PlayerTuning.cs             ScriptableObject that links the groups below (+ DeathSequence, a PoseSequence); Player hands each rule only its group
│  │  ├─ Tuning/PlayerMovementTuning.cs     speeds, acceleration, jump, gravity, slopes, heavy landing, walk / sprint / jump stamina costs, idle speed for regen → PlayerMotor
│  │  ├─ Tuning/PlayerVitalsTuning.cs       max health, max stamina + regen, winded time, out of breath (BreathingSettings), fatigue tiers, fall damage heights → Player, PlayerLife
│  │  ├─ Tuning/PlayerCameraTuning.cs       eye height, pitch limits, head bob, landing dip → PlayerCameraRig, PlayerLook, PlayerHeadBob
│  │  ├─ Tuning/PlayerInteractionTuning.cs  look ray reach / radius / mask, drop distance → PlayerInteractor
│  │  ├─ Tuning/PlayerHeldItemTuning.cs     aim origin, aim move speed, obstacle pose, AimSwaySettings, HeldSwaySettings → HeldItemDriver, HeldItemClearance, HeldItemSway
│  │  ├─ Tuning/PlayerSoundTuning.cs        footstep (walk / sprint volume) / jump / land / breath SoundIDs and volumes, breath sound volume and fades (BreathSoundSettings) → PlayerFeedback, PlayerBreathSound
│  │  ├─ Tuning/PlayerVisionTuning.cs       vision radius, colour, edge brightness, light / lantern positions, range scale, max intensity, blend → PlayerVision
│  │  ├─ Defaults/BaseStats.cs              IStatProvider default: returns base values; Changed never fires
│  │  └─ Prefabs/Player.prefab              the player entity (see below)
│  ├─ QuickTimeSystem/                      Aipocalypse.Core.QuickTimeSystem
│  │  ├─ QteChallenge.cs                    abstract challenge SO (TimeScale, Presentations, Button: the GameButton that attempts it), IQteSession contract, QteResult / QteAttemptResult
│  │  ├─ TimingQteChallenge.cs              timing challenge + TimingQteSession: repeating fill, green zone, duration (or owner-controlled), attempts per cycle, miss hold and bounded zone relocation; optional random delegate for tests
│  │  ├─ QuickTimeService.cs                root ITickable: one owner-bound session at a time, unscaled tick while gameplay input is on and TimeService isn't paused; reads WasPressed(Button) and Consume(Button) (ticks before Player.Update, so a press completing a repair doesn't also reload); a press is judged before the frame moves the session, which advances at most Time.maximumDeltaTime per frame; timed or owner-controlled lifetime, cancel, optional slow motion (standalone QTEs only), completion callback; QteStarted / QteAttempted / QteEnded
│  │  └─ QuickTimeInstaller.cs              registers QuickTimeService as a root entry point
│  ├─ TimeSystem/                           Aipocalypse.Core.TimeSystem
│  │  ├─ TimeService.cs                     root ITickable, sole writer of Time.timeScale and the fixed step: owner-keyed Request / Release (slowest wins), RequestPause / ReleasePause (IsPaused = any owner holds one; beats every request), RequestHitStop (real-time, overlaps keep the longest / lowest, ignored while paused); restores Unity's values on Dispose
│  │  └─ TimeInstaller.cs                   registers TimeService as a root entry point
│  ├─ UI/                                   Aipocalypse.Core.UI — rules and how-tos in UI.md
│  │  ├─ Widgets/StatBar.cs                 passive meter view: SetValue / SetWarning / ApplyStyle, optional trail
│  │  ├─ Widgets/StatBarStyle.cs            ScriptableObject: sprites, colors, font for a StatBar
│  │  ├─ Widgets/StyleUtils.cs              ImageTypeFor(sprite) (Sliced when the sprite has 9-slice borders); editor: RefreshViews re-applies a style asset to the open views using it (the *Style OnValidate)
│  │  ├─ Widgets/HotbarSlotView.cs          passive slot view: SetKey / SetIcon / SetSelected / SetBadge(count, reserve, showReserve, warning) / HideBadge / ApplyStyle
│  │  ├─ Widgets/HotbarSlotStyle.cs         ScriptableObject: tile, frame, key cap sprites + colors, key font, ammo badge font + colors
│  │  ├─ Widgets/InteractPromptView.cs      passive "[E] Pick up Rifle" view: SetKey / Show / Hide / ApplyStyle
│  │  ├─ Widgets/InteractPromptStyle.cs     ScriptableObject: key cap sprite, fonts, verb / target colors
│  │  ├─ Widgets/CrosshairStyle.cs          ScriptableObject: MinSpread, SpreadFade, SpreadSpeed, FadeSpeed
│  │  ├─ Widgets/HitFeedbackStyle.cs        ScriptableObject: hit / kill marker colors, durations, pops, PopShare, KillNumberColor
│  │  ├─ Widgets/ClockView.cs               passive digital clock: SetTime (monospaced HH:MM, char buffer) / SetPhase / SetDay / Show / ApplyStyle
│  │  ├─ Widgets/ClockStyle.cs              ScriptableObject: fonts + underlay materials, sizes, colors, backing panel, Phases (DayPhase → PhaseLabel: text, colour; Phase(phase))
│  │  ├─ Widgets/DeathScreenView.cs         passive: eyelids + darkness (CloseEyes from the style's steps), menu (ShowMenu / HideMenu fades), SetStatus (pulse), HideAll, Illustration slot
│  │  ├─ Widgets/DeathScreenStyle.cs        ScriptableObject: eyelid texture + EyelidSolidAt, EyeStep timeline (lids, darkness, ease), menu colors / glow / fonts / texts, fades, MinLoadingTime, continue pulse
│  │  ├─ Widgets/QteView.cs                 replaceable QTE widget contract: PresentationId, CanDisplay / Display(session), Flash, DisplaySuccess
│  │  ├─ Widgets/TimingQteView.cs           draws a timing session as a circle or a bar, with no weapon or input knowledge
│  │  ├─ Widgets/QteStyle.cs                ScriptableObject: QTE sprites, colors, key cap; Success (color, duration, hold, pop / burst scales and shares) and Miss (color, duration, shake, squash) result looks; EndFadeDuration
│  │  ├─ HudScope.cs                        EntityScope on the HUD root: injects the HUD's views in any scene
│  │  ├─ Hud/PlayerHudView.cs               base presenter: follows PlayerRegistry, Bind / Unbind(IPlayer) per active player
│  │  ├─ Hud/PlayerVitalsHud.cs             PlayerHudView → health + stamina bars (LateUpdate)
│  │  ├─ Hud/HotbarHud.cs                   PlayerHudView → one slot view per slot of Player.Equipment; each item's Changed → its Badge, AmmoReserve.Changed → reserve counts (events, no polling)
│  │  ├─ Hud/InteractPromptHud.cs           PlayerHudView → prompt from Player.Interactor (LateUpdate)
│  │  ├─ Hud/CrosshairHud.cs                PlayerHudView: moves the crosshair to Player.CrosshairPosition, fades the dot while aiming / sprinting, pushes the spread ticks out to Player.CrosshairSpread (CrosshairStyle)
│  │  ├─ Hud/HitFeedbackHud.cs              PlayerHudView → DamageDealt from the player: crosshair hit / kill marker (HitFeedbackStyle), hit / kill sound, pooled damage numbers
│  │  ├─ Hud/QteHud.cs                      shows any running QTE from QteStarted / QteAttempted (Miss → shake) / QteEnded (Success → burst, else fade over EndFadeDuration); key label from GetKeyLabel(Button); hidden (session kept) while paused or when gameplay input is off (QteStyle)
│  │  ├─ Hud/ClockHud.cs                    WorldClock (TryResolve; hidden without one) → ClockView (LateUpdate, view skips repeats)
│  │  ├─ Hud/HudVisibilityHud.cs            PlayerHudView on the HUD root: fades the HUD's CanvasGroup out on PlayerDied, back in on PlayerRevived (the death screen ignores it)
│  │  ├─ Hud/DeathScreenHud.cs              PlayerHudView presenting PlayerRespawnFlow: PlayerRespawnFading → eyes close, menu "Loading..."; PlayerRespawning → at least MinLoadingTime; RespawnStageChanged Ready → "Click to continue", None → fade out
│  │  ├─ Prefabs/HUD.prefab                 standalone overlay canvas 1920×1080, SafeFrame + regions, HudScope (placed in the scene)
│  │  ├─ Prefabs/Widgets/StatBar.prefab     the bar widget
│  │  ├─ Prefabs/Widgets/HotbarSlot.prefab  the hotbar slot widget
│  │  ├─ Prefabs/Widgets/InteractPrompt.prefab  the interact prompt widget
│  │  └─ Prefabs/Widgets/DamageNumber.prefab    Damage Numbers Pro mesh number (3D, pooled), font Data/UI/DamageNumberFont.asset
│  ├─ WorldTimeSystem/                      Aipocalypse.Core.WorldTimeSystem — the in-game clock, see DAYNIGHT.md
│  │  ├─ WorldTime.cs                       WorldTime (hours since day 1: Day, Hour, WholeHour, Minute) + DayPhase enum
│  │  ├─ ClockSettings.cs                   SO: seconds per game hour, master speed, start, Phases (DayPhase → PhaseTiming: start + speed; Timing, HasEveryPhase), moon cycle; PhaseAt, RealSecondsBetween, Real time inspector box
│  │  ├─ WorldClock.cs                      scene service (ITickable, scaled time): Advance steps hour by hour and phase by phase (events in order); JumpTo / SetTime / SkipTo; speed requests (slowest wins); MoonPhase
│  │  ├─ TimeWindow.cs                      On / Off / Fade hours, may wrap past midnight → Weight(hour)
│  │  ├─ WorldClockCheats.cs                editor and dev builds (registered when Debug.isDebugBuild): N → JumpToNextHour, M → JumpToNextPhase (InputService debug presses)
│  │  └─ WorldTimeInstaller.cs              SceneInstaller (_settings: ClockSettings): ClockSettings + WorldClock (entry point) + cheats
│  └─ WeaponSystem/                         Aipocalypse.Core.WeaponSystem — rules and how-tos in WEAPONS.md
│     ├─ GunDefinition.cs                   EquippableDefinition, Category Gun: fire, bullet, magazine, jam, recoil, accuracy, handling, sounds, effects
│     ├─ GunSettings.cs                     the setting groups above (BulletSettings.DamageType, default Gunshot) + FireMode, ReloadType, AimOrigin (Muzzle, Camera, MuzzleToCrosshair)
│     ├─ GunInstance.cs                     one owned gun: Rounds, IsJammed, Changed; Badge = rounds, warning when empty or jammed, Reserve = its AmmoType (survives drop / swap)
│     ├─ MeleeDefinition.cs                 EquippableDefinition, Category Melee: Swing, Sounds, ImpactEffect
│     ├─ MeleeSettings.cs                   SwingSettings (damage, reach, radius, wind-up, recovery, knockback, layers, stamina; swing motion: StrikeTime, AlternateSides, PoseSpeed), MeleeSounds
│     ├─ MeleeAttack.cs                     melee rules: swing timing, hold to repeat, buffered click, alternating sides → SwingStarted, Struck
│     ├─ HeldMelee.cs                       HeldItem for melee: runs MeleeAttack, procedural swing between pose markers (PoseBlender, the definition's swing motion), sphere-cast hit → DamageService, knockback, impact, sounds (AudioService)
│     ├─ Ammo/AmmoType.cs                   SO: a kind of round; guns of one type share a pool
│     ├─ Ammo/AmmoReserve.cs                the holder's spare rounds by type; Take / Add / Infinite, Changed
│     ├─ Ammo/AmmoConfig.cs                 SO: starting reserve + Infinite → CreateReserve
│     ├─ IWeaponUser.cs                     holder seam for any weapon: Eye, Root, MeleePower, Exert (stamina per attack; StatId.AttackStaminaCostScale)
│     ├─ IGunUser.cs                        IWeaponUser + Ammo, AimFrom, AirSpreadScale, FatigueRecoilScale, AimOffset (Player implements it; StatId.AirSpreadScale)
│     ├─ GunTrigger.cs                      fire modes, fire rate, bursts; frame-rate independent
│     ├─ Gun.cs                             a held gun's rules: shoot, magazine, reload (per-round stopped by firing after the current round), jam + clear (jam pause, repair timing, AddRepairTime), pump cycle, aim, sprint recovery (shots wait, clicks kept), empty click; events (+ GunAction, GunInput)
│     ├─ GunRecoil.cs                       procedural recoil: spray pattern → aim kick (view, springs back) + kept share (LookTurn) + small gun kick (model); hip / aim values blended by aim weight
│     ├─ GunJolt.cs                         GunJolt (self-returning kick / wobble of the model) + GunJoltPlayer (runs one; HeldGun applies it on the recoil pivot)
│     ├─ BulletSpread.cs                    random direction inside a cone
│     ├─ GunAccuracy.cs                     spread while airborne (AccuracySettings): less when aiming, × the holder's AirSpreadScale, settles back after landing
│     ├─ GunAim.cs                          Shot(user, muzzle, range, layers, bullets, out origin, out direction): the origin / direction rules for anything that aims through an eye and leaves a muzzle (AimOrigin, cover check); MuzzleToPoint with the too-close fallback
│     ├─ HeldGun.cs                         HeldItem for guns: runs Gun, spawns bullets (GunAim.Shot), aim / sprint poses (PoseBlender) + aim zoom, recoil + jolts on the recoil pivot, drives its optional GunAnimation, sprint recovery, empty click, its GunJam, sounds (AudioService)
│     ├─ GunJam.cs                          GunJam: one gun's jam part (GunRepairFlow + GunRepairFeedback + QteAttempted subscription; Tick, ActionStarted, ActionEnded → cleared?, Cleared, Hide, Dispose); GunJamFactory: root service holding AudioService, QuickTimeService and the jam publishers / subscriber
│     ├─ GunRepairProfile.cs                SO per gun: automatic / manual start, jam pause, quick / stubborn duration and chance, QTE onset, miss penalty and cap, challenge, feedback preset
│     ├─ GunRepairFlow.cs                   the jam repair's QTE rules: start at QteStartProgress, early finish, miss penalty, interrupt on cancel; Missed
│     ├─ GunRepairFeedback.cs               presents one gun's jam / repair beats: sounds, gun jolts, GunJamBeat / GunRepairCompleted
│     ├─ RepairFeedbackSettings.cs          SO preset: gun jolts (jam, dry fire, slap, punch, miss, success) and result sounds (+ MechanismDelay)
│     ├─ GunJamFeedback.cs                  on Player/GunJamFeedback (with FeedbackContext): its owner's GunJamBeat / GunRepairCompleted → one MMF_Player per beat (camera impulse; hit stop for punch and success)
│     ├─ GunAnimation.cs                    a gun's clip view: GunClip enum = Gun.controller states, per-gun Override Controller, manual controller/Animation Rigging graph (hashes + clip lengths cached, no per-frame allocation), action-progress sampling, repair impact events, pooled cosmetic magazine release
│     ├─ Ballistics/BulletSystem.cs         root service (ITickable, Advance(dt) for tests): bullets as data, each following only the tracer it was handed (spawn id), ray-tested against each shot's HitLayers, damage of the shot's DamageType through DamageService (source = shooter), impacts, holes parented to the collider hit (+ BulletShot)
│     ├─ Effects/EffectPool.cs              root service (ITickable): one ObjectPool per effect prefab, timed returns, caps
│     ├─ Effects/PooledEffect.cs            root of a pooled effect prefab: lifetime, max alive, Restart, ScaleDecal (scale-invariant decal size)
│     ├─ Effects/BulletEffects.cs           SO: muzzle flash, tracer, impact, bullet hole + hole settings
│     ├─ WeaponInstaller.cs                 registers EffectPool + BulletSystem (entry points) and GunJamFactory in the root scope
│     └─ Prefabs/Gun_Base.prefab            HeldGun skeleton: Poses (Hip, Aim, Sprint), PosePivot > RecoilPivot > Muzzle; every gun is a variant of it, and the base alone is a working block-out
├─ Content/                                 one folder per thing (weapon, item, enemy…) with everything it needs; ARCHITECTURE.md → Folder layout
│  ├─ Weapons/AK47, Pistol, MP5, DoubleBarrel, PumpShotgun/   <Gun>.prefab: Gun_Base variant with the store model, poses, AnimationRoot (muzzle at AnimationRoot/Motion/Weapon/Muzzle,
│  │                                        DoubleBarrel: …/Weapon/Hinge/Muzzle; the base Muzzle is removed); also the pickup (root WorldItem + trigger box, World child = model
│  │                                        lying flat). <Gun>.asset (GunDefinition: all its numbers), <Gun>_Repair / _RepairQte.asset, Animations/ (<Gun>.overrideController + clips), <Gun>_Magazine.prefab
│  ├─ Weapons/FireAxe/                      FireAxe.prefab (HeldMelee: Poses (Idle, WindUp, FollowThrough, Sprint), PosePivot > Model; WorldItem + World), FireAxe.asset
│  ├─ Weapons/_Shared/                      Animations/ (Gun.controller + base clips); Effects/ (MuzzleFlash, Tracer, BulletImpact, BulletHole PooledEffect roots,
│  │                                        BulletHoleDecal.mat, BulletHole.png); Ammo/ (RifleAmmo, PistolAmmo, ShotgunShells); Materials/ShotgunShell.mat
│  ├─ Items/Lantern/                        Lantern.asset + Lantern.prefab (belt lantern pickup: WorldItem + trigger box, World > Model, Polygon Mega Survival kerosene lamp)
│  ├─ Enemies/WanderCapsule/                WanderCapsule.prefab (capsule enemy: FollowerEntity, EnemyMover, BehaviorTree, EnemyBrain; two in WorldMap), WanderCapsule.asset (its EnemyDefinition)
│  └─ Enemies/_Shared/                      Wander.asset (shared Behavior Designer subtree), Materials/WanderCapsule1-2.mat
├─ Environment/                             shared environment kit for every map: Water/ (water materials), TerrainLayers/, Vegetation/ (detail prefabs in `DetailS/`, committed with a capital S; the generator writes `Details/`, see ENVIRONMENT.md), Rocks/, Mountains/ (ENVIRONMENT.md)
├─ Maps/WorldMap/                           what only this map uses: Terrain/ (TerrainData, Heightmaps/), Water/ (meshes), Backdrop/, Navigation/ (NavmeshWater mesh, WorldMapNavmesh.bytes cache)
├─ Scenes/Prototypes/                       WorldMap.unity (the prototype world map; its data in Maps/WorldMap/); Proto_* scenes are planned (one per mechanic), none exist yet
├─ (Assets/_Placeholder/Audio/)            Weapons/: gun sound samples from the old project (rifle, pistol, sniper bolt); Voice/: CC0 death groans;
│                                          Music/: PLACEHOLDER death music cut from a YouTube Short, not licensed (sources in SOURCES.txt)
├─ Data/                                    settings shared by a whole system (one copy each); per-thing data lives in Content/
│  ├─ Audio/                                BroAudio libraries (Guns, Melee, RepairFeedback, Player, Combat, Ambience, Music .asset) + one folder of entities each
│  ├─ Camera/ImpulseReaction.asset          rotation-only noise played by the player camera's impulse listener
│  ├─ WorldTime/ClockSettings.asset         30 s per game hour (12-minute day), start day 1 07:00, phases Dawn 06 / Morning 08 / Afternoon 12 / Dusk 16 / Evening 18 / Midnight 00, speed 1
│  ├─ Environment/TimeOfDay/                TimeOfDay (profile), Looks/TOD_*.asset, Grades/PP_*.asset, TimeOfDay_Sky.mat, TimeOfDay_Ambience.asset, Base_PostProcessing / Base_Skybox (setup sources)
│  ├─ Player/PlayerTuning.asset             used in every scene; links Player{Movement,Vitals,Camera,Interaction,HeldItem,Sound,Vision}Tuning.asset beside it and Sequences/PlayerDeath.asset
│  ├─ Equipment/EquipmentConfig.asset       3 slots "Weapon 1–3", each takes Gun | Melee, empty at spawn; belt takes Lantern
│  ├─ Hands/HandSet.asset, Poses/           the hand prefabs + shared hand poses
│  ├─ Enemies/EnemySystemSettings.asset     think interval, thinks per frame
│  ├─ Enemies/WanderSettings.asset          wander tuning
│  ├─ Weapons/AmmoConfig.asset              the player's starting reserve (90 rifle, 48 pistol, 24 shells), Infinite off
│  ├─ Weapons/BulletEffects.asset           effects shared by every gun
│  ├─ Weapons/RepairFeedback.asset          the repair feedback preset every gun's repair profile uses
│  └─ UI/HealthBarStyle.asset, StaminaBarStyle.asset, HotbarSlotStyle.asset, InteractPromptStyle.asset, CrosshairStyle.asset, HitFeedbackStyle.asset, ClockStyle.asset,
│        RepairQteStyle.asset (QteStyle) + RepairQteFont.asset, DamageNumberFont.asset, DeathScreenStyle.asset;
│        DeathScreen/ (Eyelid.png, Glow.png, VarelaRound-Regular.ttf + OFL licence, DeathScreenFont.asset, DeathScreenFont_Title.mat)
├─ Tests/PlayMode/ControllerHitRelay.cs    [ExecuteAlways] forwards OnControllerColliderHit to a PlayerMotor in PlayerMotorTests (here because an editor-only assembly's component can't be added)
├─ Tests/PlayMode/WorldMapSmokeTests.cs   asmdef Aipocalypse.Tests.PlayMode: loads WorldMap.unity, checks injection and enemy registration, picks up a gun, fires and reloads; any logged error fails it
└─ Tests/EditMode/                          asmdef Aipocalypse.Tests.EditMode
   ├─ CombatTests.cs                        DamageService: applies, publishes with source / result, kill, ignores dead / null / zero
   ├─ PlayerRulesTests.cs                   Stamina, Fatigue, PlayerBreathing, Health, FallDamage, directional speed, PlayerVision (off by day, range / intensity from the radius, lantern)
   ├─ EquipmentTests.cs                     Hotbar, EquipmentSwitcher timing, Equipment select / cycle, pick up / swap / drop (also mid-switch), lantern onto the belt / swap, lower / raise hands, quick-use items and stacks (TestConsumableDefinition)
   ├─ PoseSequenceTests.cs                  PoseSequencePlayer: start pose, beat moves, ground and wall placement, Ended, Stop
   ├─ GunTests.cs                           fire modes and rate, magazine, reloads, jams, interrupts, shared ammo, recoil, spread
   ├─ RepairQteTests.cs                     timing passes, zone tuning, miss hold and relocation, timeouts, automatic / manual repair, jam pause, miss penalty, early completion, interruption, hit stop, gun jolt, time request arbitration
   ├─ GunAnimationTests.cs                  AK47 override/curve bindings, automatic Animator overwrite regression, controller restoration, hand contacts, magazine visibility, interrupt reset, pause/equip/fire, repair, scaled-hand grip alignment
   ├─ WeaponAnimationTests.cs               MP5/pistol/shotgun bindings, scaled hands, automatic Animator pass, contacts and continuity, slap/reset, repeated shell reload and pump cycle, break-action closure, repair variants
   ├─ HandRigTests.cs                       normal grip compatibility, bounded fist calibration/blending, safe fallback and all guns' punch-contact finger shapes/closure timing
   ├─ ItemPrefabTests.cs                    every item has one prefab for world + hand; world layout; rig strips world parts
   ├─ HeldItemClearanceTests.cs              obstacle retraction and recovery, stable pose, ignores self / triggers, never moves the player
   ├─ HeldMotionTests.cs                    GunAccuracy (speed, airborne, aiming, steadiness, settling); HeldItemSway (look / move lag, aiming, sway stat, landing)
   ├─ WorldClockTests.cs                    clock speed, real minutes per day, phase speeds, events in one long frame, no skipped phase at a boundary, midnight, jumps, N / M cheats, skip (and a jump ending it), freeze, moon phase
   ├─ PhaseDataTests.cs                     every ClockSettings / ClockStyle / TimeOfDayProfile asset has every DayPhase
   ├─ TimeOfDayTests.cs                     TimeWindow across midnight with fades, profile blending (linear, wrap past midnight, hold), sun path, reactor fade at high frame rates
   ├─ MeleeTests.cs                         swing timing, hold rate, buffered click, alternating sides, switching / sprinting
   ├─ ServiceRequestTests.cs                TimeService (pause beats slow motion, hit stops, unknown owners, Dispose restores), GraphicsService (shortest shadow distance wins, restored)
   ├─ QuickTimeServiceTests.cs              press judged before a long frame moves the pointer, no timeout in one hitch, slow motion released / skipped for owner-controlled, death cancels, owner checks
   ├─ PlayerRespawnFlowTests.cs             the stages after the fade; a revive during the fade ends the flow even when the fade handler ignores cancellation
   ├─ PlayerMotorTests.cs                   real CharacterController: a fall lands once with its drop height; a steep-slope slide counts as a fall to the flat ground (ControllerHitRelay)
   ├─ BulletSystemTests.cs                  bullets stop at range / at a wall and skip the shooter; capped EffectPool recycles the oldest; a recycled tracer follows only its new bullet; a hole sticks to the moving target it hit
   └─ TestConsumableDefinition.cs           quick-use stackable ItemDefinition for EquipmentTests
```

## Who depends on whom

```text
ProjectLifetimeScope (root) ──registers──> InputService, ControlSettings, MessagePipe brokers, AudioService, DamageService, TimeService,
                                           GraphicsService, QuickTimeService, PlayerRegistry, PlayerRespawnFlow, EffectPool, BulletSystem,
                                           GunJamFactory (+ PlayerCheats in dev builds); entry points are ticked by VContainer
                                                │
GameLifetimeScope (scene) ── installs each SceneInstaller component on it:
   WorldTimeInstaller   → ClockSettings, WorldClock (+ WorldClockCheats)
   EnvironmentInstaller → EnvironmentDirector (RegisterComponent → TryResolve WorldClock, GraphicsService), AmbienceService (needs WorldTimeInstaller)
   EnemyInstaller       → AstarPath, EnemySystemSettings, EnemyRegistry, EnemyManager; injects the listed scene enemies (EnemyBrain)
   EnvironmentDirector ticks its reactors (scene list collected in the editor; injected spawns Register themselves)
                                                │
PlayerScope : EntityScope ── parent: GameLifetimeScope if the scene has one, else root
   │ registers default IStatProvider → BaseStats (only if no ancestor provides one)
   └─ injects the Player GameObject (whole hierarchy)
         Player.Construct(InputService, TimeService, ControlSettings, IStatProvider, DamageService, AudioService, IPublisher<PlayerDied>,
                          IPublisher<PlayerDeathSequenceEnded>, IPublisher<PlayerRevived>, PlayerRegistry, IObjectResolver)
           VisionLight's TimeOfDayWeight (EnvironmentReactor) registers itself with the scene's EnvironmentDirector, if any
           creates (plain C#, owned by Player):
             PlayerLife(health, fall damage, death / revive events), Stamina, Fatigue, PlayerBreathing(tuning.Vitals.Breathing), PlayerBreathSound(AudioService, tuning.Sounds),
             PlayerMotor(CharacterController, tuning.Movement, stats, stamina), PlayerCameraRig(cameraRoot, cameraPitch, tuning.Camera.EyeHeight),
             PlayerLook(body, rig, tuning.Camera), PlayerHeadBob(rig, tuning.Camera), HeldItemSway(HeldSway, AimSway, stats), PlayerZoom(Cinemachine camera, FirstPersonView, ControlSettings),
             PlayerAim(EquipmentRig, zoom), PlayerInteractor(cameraPitch, gameObject, tuning.Interaction), HeldItemClearance, PlayerVision(VisionLight, tuning.Vision),
             PoseSequencePlayer(body, rig, AudioService), PlayerFeedback(motor, head bob, sway, AudioService, tuning.Sounds), AmmoReserve(AmmoConfig.CreateReserve()),
             Equipment(EquipmentConfig.CreateEquipment()) → EquipmentRig.Bind(Equipment, owner: this, resolver); HeldItemDriver(equipment, rig, parts…)
               rig spawns item prefabs (staged inactive, resolver.InjectGameObject, world parts stripped) → HeldGun.Construct(BulletSystem, EffectPool, AudioService, GunJamFactory),
               HeldMelee.Construct(EffectPool, DamageService, AudioService)
               HeldItem.Bind(item, owner) → HeldGun builds Gun(GunInstance, owner.Ammo) + GunRecoil + GunAccuracy + GunJam (GunRepairFlow + GunRepairFeedback); HeldMelee builds MeleeAttack
           last: PlayerRegistry.Register(this) → ActivePlayerChanged   (OnDestroy: Unregister)

HudScope : EntityScope (HUD.prefab root, separate from the Player) ── same parent lookup
   └─ injects the HUD GameObject
         PlayerHudView (PlayerRegistry, ISubscriber<ActivePlayerChanged>) → Bind / Unbind the active IPlayer
           PlayerVitalsHud, CrosshairHud, HotbarHud, InteractPromptHud (+ InputService for key labels), HitFeedbackHud (+ AudioService, DamageDealt),
           HudVisibilityHud (PlayerDied / PlayerRevived), DeathScreenHud (PlayerRespawnFading, PlayerRespawning, RespawnStageChanged)
         QteHud (QuickTimeService, TimeService, InputService, QteStarted / QteAttempted / QteEnded), ClockHud (TryResolve WorldClock)
```

- Only `Player` gets injected. The parts are plain classes it creates and owns. They get Unity components through their constructors, not through DI.
- Outside systems see the player through `PlayerRegistry.Current`, an `IPlayer` (HUD, respawn flow, cheats); enemies and companions only need `IPlayerTarget` (`_Common/Entities`: transform + `IDamageable`).
- `PlayerSystem` → `EquipmentSystem` ← `WeaponSystem`: the player owns an `Equipment`; weapon kinds derive from equipment's definitions. `EquipmentSystem` knows neither the player nor weapons: `WorldItem` finds the picker's hotbar through `IEquipmentOwner`, and a `HeldGun` finds its holder's ammo and aim through `IGunUser`, a `HeldMelee` its eye and root through `IWeaponUser` (Player implements all of them). The player also uses a few `WeaponSystem` types directly (`AmmoReserve`, `AimOrigin` in its tuning).
- `PlayerSystem` → `EnvironmentSystem`: the player's `VisionLight` carries a `TimeOfDayWeight` (Darkness) that the scene's director ticks; `Player` only reads its `Weight`. No director (prototype scenes) = never dark, light off.
- Bullets, swings and falls hurt anything with an `IDamageable` (`_Common/Combat`) through `DamageService.Deal`, which applies `TakeDamage` and publishes `DamageDealt` (source, result). Targets only apply their own rules.
- Interaction: `_Common/Interaction.IInteractable` is the only contract. `PlayerInteractor` finds one with a look cast; the interactable decides what happens (`WorldItem` → `Equipment.TryPickUp`).
- QTEs: `QuickTimeSystem` knows no weapons. A gun's `GunRepairFlow` starts a session; the HUD's `QteHud` shows any session from the QTE events, and `_Common` events carry no weapon types.
- Inside the player system, plain C# events are used (`PlayerMotor.Landed`, `PlayerLife.Died`). Anything crossing systems goes through MessagePipe (`DamageDealt`, `PlayerDied`).

## Frame order

VContainer's update loop runs before MonoBehaviour `Update`s, so every `ITickable` ticks before `Player.Update` in the same frame (root scope first, then the scene scope).

```text
VContainer ticks     TimeService counts down a hit stop (real time)
                     QuickTimeService ticks the QTE (unscaled) and Consumes its Button press, so the player doesn't also see it
                     PlayerCheats (K / L); EffectPool returns expired effects
                     BulletSystem moves and ray-tests the bullets fired in earlier frames → DamageService → DamageDealt → HitFeedbackHud (marker, sound, number); impacts, holes
                     scene scope: WorldClock advances (HourChanged / DayPhaseChanged / DayStarted), WorldClockCheats; AmbienceService bed volumes and calls;
                     EnemyManager thinks its share of the enemy trees

Player.Update
intent = dead, in a pose sequence or gameplay input disabled ? default : read InputService (look × PlayerZoom.LookScale; aim / trigger end a sprint)
PlayerLook.Tick      yaw + pitch → PlayerCameraRig.SetLook   (movement uses the new facing this frame; skipped in a pose sequence)
PlayerMotor.Tick     velocity, jump, CharacterController.Move, landing → Jumped / Landed
                       └─ PlayerFeedback: jump / landing sound, head-bob dip, sway kicks; PlayerLife.OnLanded: fall damage → DamageService → Health → DamageDealt / PlayerDied
Stamina.Tick         regen while the motor is idle (spending happened inside the motor, and in the held item last frame)
Fatigue.Tick         tier for the stamina (+ winded) → eased recoil / melee power / move speed (used by this frame's held item and next frame's motor)
PlayerBreathing.Tick out of breath → eased Tiredness → PlayerBreathSound starts / fades / stops the breath loop (AudioService.Start)
PlayerHeadBob.Tick   bob + dip → PlayerCameraRig.SetHeadMotion; Stepped → PlayerFeedback footstep (skipped in a pose sequence)
PlayerInteractor     look cast → focus + prompt; intent.Interact → IInteractable.Interact (pickup / swap)
HeldItemDriver.Tick
  Equipment          intent.Drop → DropHeld → WorldItem.Spawn at the drop point; else SelectSlot / CycleSlot → Select / Cycle; EquipmentSwitcher.Tick (holster → swap → equip)
                       └─ HeldChanged → EquipmentRig shows/hides models; SelectionChanged → HotbarHud highlight
  EquipmentRig.Tick  restore hand pose from Raise before measuring obstacles
  EquipmentRig.TickHeld  the held item: HeldGun → Gun.Tick (reload / jam clear / pump, aim, trigger → Fired counts shots), GunJam.Tick; pose + recoil pivots,
                       GunAnimation (if any) samples the controller and constraints (parts, hand targets, fingers).
                       HeldMelee → MeleeAttack.Tick (Struck marks a hit to check); swing pose
  PlayerLook.Turn    held item's LookTurn (kept recoil) into yaw / pitch; PlayerLook.SetKick: its ViewKick → camera rig
  PlayerZoom.Tick    held.Zoom (skipped in a pose sequence)
  HeldItemSway.Tick  aim offset (breathing by Tiredness, bob in step with the head, spring lag / kicks) + model position / roll, calmed by held.AimWeight, × HoldFeel.SwayScale
                       → EquipmentRig.ApplyHandMotion (model extras); PlayerAim.Tick → CrosshairPosition / CrosshairSpread (world FOV), EquipmentRig.SetAimRotation (zoom-corrected)
  PlayerMotor.SpeedMultiplier  aiming ? AimMoveSpeedMultiplier × HoldFeel.AimMoveSpeedScale : 1, × Fatigue.MoveSpeed
  HeldItemClearance  eye → normal model tip cast; blend an obstacle offset / tilt onto the hand (melee swings opt out)
  HeldItem.LateTick  model and camera are final: HeldGun spawns this frame's shots (muzzle flash, one GunAccuracy spread for the shot, GunAim.Shot, BulletSystem.Fire per pellet)
                       from the muzzle as drawn, then GunRecoil.Kick (shows from next frame).
                       HeldMelee sphere-casts from the camera → damage, knockback, impact effect, hit sound
  FirstPersonHands.Tick  each hand onto the held item's grip, turned towards its implied elbow; hidden with no grip
PoseSequencePlayer   in a pose sequence only: PlayerCameraRig.Override (wins over look, head bob and kick), cues, music, settled look (raw look input) + drift
                       → PlayerZoom.Tick(FovScale)
PlayerVision.Tick    darkness (TimeOfDayWeight, eased by the director last frame) + StatId.VisionRadius + Equipment.Belt lantern → the VisionLight

LateUpdate           CinemachineBrain moves PlayerCamera to CameraPitch
                     PlayerVitalsHud reads Health / Stamina → StatBar.SetValue (skips unchanged values)
                     CrosshairHud reads Player.CrosshairPosition / AimWeight / IsSprinting → moves and fades the dot
                     InteractPromptHud reads Player.Interactor → InteractPromptView
                     QteHud reads the running session → its QteView (idle frames skip)
                     EnvironmentDirector reads WorldClock.Now → light, RenderSettings, sky globals, grades → reactors
                     ClockHud reads WorldClock.Now / Phase → ClockView
StatBar.Update       only while a trail is catching up, otherwise disabled
```

## Player prefab

```text
Player                       CharacterController (h 1.8, r 0.35, step 0.35, slope 45°) · Player (AmmoConfig) · PlayerScope
├─ CameraRoot                at eye height (PlayerCameraTuning.EyeHeight, 1.65); PlayerCameraRig adds the head motion
│  └─ CameraPitch            pitch + view kick from PlayerCameraRig; Cinemachine tracking target
│     └─ HeldItems           EquipmentRig
│        ├─ Hand             (0.17, -0.21, 0.62); held prefabs spawn here, posed by the rig (guns add their own poses on top)
│        └─ Hands            FirstPersonHands (HandSet); HeldWeapon layer; spawns FPHand_Left / FPHand_Right
├─ VisionLight               y 3; point Light (off, no shadows), PlayerVision drives it · TimeOfDayWeight (Darkness, response 1.5 s)
├─ PlayerCamera              Camera (tag MainCamera, all layers, no camera stack) · URP camera data · CinemachineBrain · AudioListener · FirstPersonView
├─ PlayerCinemachineCamera   CinemachineCamera (FOV from ControlSettings.FieldOfView via PlayerZoom) · HardLockToTarget · RotateWithFollowTarget · CinemachineImpulseListener
└─ GunJamFeedback            FeedbackContext · GunJamFeedback; one MMF_Player child per jam / repair beat
```

The HUD is not in this prefab: `HUD.prefab` sits next to it in the scene ([UI.md → Where the HUD lives](UI.md#where-the-hud-lives)).

## Adding a script

- **New system:** folder under `_Core/` (or `Features/` / `_Experimental/`), namespace `Aipocalypse.Core.<System>`, one installer. A root service installs from `ProjectLifetimeScope` (`<System>Installer : IInstaller`); a scene system is a `SceneInstaller` component added to the scene's `GameLifetimeScope` GameObject. Add it to the map above.
- **Rules go in plain C#** (testable, EditMode tests in `Tests/EditMode/`). MonoBehaviours are views: they wire components and forward to the rules.
- **Numbers go in a ScriptableObject** type next to the system, with the asset in `Data/<System>/`.
- **New `*LifetimeScope.cs`:** `DisableScriptModifier` is on, but check that VContainer didn't replace the file with its template.
- **Generated code** (`GameInputActions.cs`): change the `.inputactions` asset, never the file.
