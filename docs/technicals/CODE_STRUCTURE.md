# Code structure

Map of the scripts that exist today: where each one lives, what it owns, and how they connect. The *why* (layers, scopes, Core vs Features) is in [ARCHITECTURE.md](ARCHITECTURE.md). **Update this file whenever you add, move or remove a script.**

## Assemblies and namespaces

| Assembly | Folder | References |
| --- | --- | --- |
| `Aipocalypse.Core` | `Assets/_Game/_Core/` | VContainer, MessagePipe (+ .VContainer), UniTask, Unity.InputSystem, Unity.Cinemachine, UnityEngine.UI, Unity.TextMeshPro, AstarPathfindingProject, PackageTools, Drawing, Unity.Animation.Rigging, DOTween.Modules, MoreMountains.Tools, BroAudio, DamageNumbersPro; precompiled plugin DLLs auto-referenced |
| `Aipocalypse.Tests.EditMode` | `Assets/_Game/Tests/EditMode/` | `Aipocalypse.Core`, VContainer, MessagePipe (+ .VContainer), UniTask, test runner, NUnit. Editor only |
| `Aipocalypse.Tests.PlayMode` | `Assets/_Game/Tests/PlayMode/` | `Aipocalypse.Core`, VContainer, test runner, NUnit |

Namespaces follow the folder: `Aipocalypse.Core.<Folder>` (e.g. `Aipocalypse.Core.PlayerSystem`, `Aipocalypse.Core.Combat`). One exception: input uses **`Aipocalypse.Input`**, because a namespace ending in `InputSystem` shadows `UnityEngine.InputSystem.InputSystem`.

## Enemy wander slice

`_Core/EnemySystem/` (`Aipocalypse.Core.EnemySystem`):

- `EnemyInstaller.cs` registers scene navigation, settings, `EnemyRegistry` and the central entry point, and injects the scene-placed enemies when the scope builds.
- `EnemyRegistry.cs` the enemies in play as a dense swap-remove list; brains add themselves while enabled.
- `EnemyManager.cs` walks the registry and time-slices Manual Behavior Designer trees through VContainer's tick loop.
- `EnemyBrain.cs` injected bridge: on its first `Start` (and each re-enable) initializes its mover, starts its tree and registers; stops both and leaves when disabled.
- `WanderMover.cs` samples reachable destinations around the spawn point and drives A* FollowerEntity, including arrival and travel timeout checks.
- `WanderTasks.cs` contains the choose, walk, and pause ActionNodes used by the shared subtree.
- `WanderSettings.cs` holds movement, radius, pause, timeout, destination search (attempts, minimum travel) and think-stride tuning; the asset and shared subtree are in `Data/Enemies/`.

`GameLifetimeScope` installs this slice when navigation and settings are assigned. `AgentScripts/WanderDemo.cs` builds and inspects the two-agent scene setup through Unity CLI. Core also references AstarPathfindingProject and its PackageTools/Drawing base assemblies.

## Script map

```text
Assets/_Game/
├─ _Core/                                   asmdef Aipocalypse.Core
│  ├─ _Bootstrap/                           Aipocalypse.Core.Bootstrap
│  │  ├─ ProjectLifetimeScope.cs            root scope: MessagePipe, InputInstaller, AudioInstaller, CombatInstaller, TimeInstaller, QuickTimeInstaller, PlayerInstaller, WeaponInstaller
│  │  ├─ ProjectLifetimeScope.prefab        holds ControlSettings (mouse sensitivity, invert Y…)
│  │  ├─ VContainerSettings.asset           Preloaded Asset → spawns the root prefab before any scene
│  │  └─ GameLifetimeScope.cs               per-scene scope, optional EnemyInstaller, WorldTimeInstaller (Clock), EnvironmentInstaller (Environment + Ambience), Strict flag, execution order -5000
│  ├─ _Common/
│  │  ├─ Entities/EntityScope.cs            Aipocalypse.Core.Entities — base child scope for unique entities, RegisterDefault
│  │  ├─ Combat/                            Aipocalypse.Core.Combat
│  │  │  ├─ IDamageable.cs                  anything that can take damage: TakeDamage → DamageResult (hurt it through DamageService)
│  │  │  ├─ DamageInfo.cs                   DamageInfo struct (amount, type, point, knockback Force, Source) + DamageType enum (Generic, Fall, Gunshot, Melee) + DamageResult (Applied, Killed)
│  │  │  ├─ DamageService.cs                root service: Deal(target, info) → TakeDamage, publishes DamageDealt with the result; DealHit(hit, info) for physics hits (finds the IDamageable, pushes a loose rigidbody)
│  │  │  ├─ CombatInstaller.cs              registers DamageService in the root scope
│  │  │  └─ Health.cs                       plain HP meter: Damage / Heal / Restore
│  │  ├─ Events/CombatEvents.cs             Aipocalypse.Core.Events — DamageDealt (target, info, result), PlayerDied
│  │  ├─ Events/GunRepairQteStarted.cs      owner, item, QTE session, feedback preset: what the HUD needs to show a gun's repair QTE
│  │  ├─ Events/PlayerEvents.cs             ActivePlayerChanged (read PlayerRegistry.Current)
│  │  ├─ Events/WorldTimeEvents.cs          HourChanged, DayPhaseChanged, DayStarted, TimeJumped, TimeSkipEnded (published by WorldClock)
│  │  ├─ Interaction/IInteractable.cs       Aipocalypse.Core.Interaction — TryGetPrompt / Interact(actor) + InteractionPrompt
│  │  ├─ Stats/IStatProvider.cs             Aipocalypse.Core.Stats — StatId enum + stat seam
│  │  └─ Utils/                             Aipocalypse.Core
│  │     ├─ Damping.cs                      ByRate / ByTime: frame-rate independent smoothing share for Lerp (0 = instant)
│  │     ├─ PhysicsQueries.cs               Nearest(hits, count, ignore): nearest NonAlloc hit not under a root (bullets, swings, look and clearance casts)
│  │     └─ TransformExtensions.cs          SetLayerRecursively (held models, hands, muzzle flashes onto the first-person layer)
│  ├─ _Debug/TargetDummy.cs                 Aipocalypse.Core.Debugging — shooting-range target: logs hits, refills its health (the emptying hit counts as a kill), tips over from knockback, flinches from every hit, springs back
│  ├─ AudioSystem/                          Aipocalypse.Core.AudioSystem
│  │  ├─ AudioService.cs                    root service: Play(SoundID, volume) 2D, PlayAt(SoundID, position) 3D, Start / SetVolume / Pause / UnPause / Stop for a kept 2D sound (loops), over BroAudio (unset ids are silent)
│  │  └─ AudioInstaller.cs                  registers AudioService
│  ├─ EnvironmentSystem/                    Aipocalypse.Core.EnvironmentSystem — scene environment, see ENVIRONMENT.md and DAYNIGHT.md
│  │  ├─ ASPTopLayerGlobals.cs              [ExecuteAlways] sets the ANGRY MESH top-layer shader globals (grass caps on the boundary rocks); on Environment/Boundary in Meadow Home
│  │  ├─ BKFoliageGlobals.cs                [ExecuteAlways] sets the BK Pure Nature plant shader globals (fade distance, micro wind); on Environment/Wind in Meadow Home
│  │  ├─ EnvironmentInstaller.cs            scene scope: RegisterComponent(EnvironmentDirector) (+ TimeOfDayAmbience → AmbienceService entry point)
│  │  ├─ TimeOfDay/EnvironmentDirector.cs   [ExecuteAlways] sole writer of the sun / moon light, ambient, fog, reflections, _TOD_ sky globals, Grade From / To volumes; profile → layers → apply → reactors (Play mode); edit-mode preview; collects scene reactors on save / Play
│  │  ├─ TimeOfDay/TimeOfDayProfile.cs      SO: keys (hour, LightingPreset, grade VolumeProfile, blend) → Evaluate(hour); SunPath (sun / moon directions); grade-override warning
│  │  ├─ TimeOfDay/LightingPreset.cs        SO: one look (sun, moon, ambient, reflections, fog, sky, haze, unlit tint) → AtmosphereState
│  │  ├─ TimeOfDay/AtmosphereState.cs       every value the director applies in a frame; Lerp
│  │  ├─ TimeOfDay/IAtmosphereLayer.cs      Modify(ref state, hour) after the profile (weather, zones, special nights)
│  │  ├─ TimeOfDay/EnvironmentReactor.cs    base of scene objects ticked by the director (injected spawns register themselves); TimeOfDayReactor: 0–1 weight from hours / daylight / darkness, eased, Apply(weight)
│  │  ├─ TimeOfDay/TimeOfDayLight.cs        fades lights to their authored intensity
│  │  ├─ TimeOfDay/TimeOfDayEmission.cs     runtime emissive copies of chosen materials (windows)
│  │  ├─ TimeOfDay/TimeOfDayParticles.cs    scales / stops emitters, optional align to the sun (god rays)
│  │  ├─ TimeOfDay/TimeOfDayActive.cs       shows objects only during their hours
│  │  ├─ TimeOfDay/AtmosphereTint.cs        multiplies / replaces colour properties on runtime material copies by an atmosphere channel
│  │  ├─ TimeOfDay/TimeOfDayAmbience.cs     SO: sound beds with a volume curve over the day, calls inside hours (BroAudio SoundIDs)
│  │  ├─ TimeOfDay/AmbienceService.cs       plays the ambience against the clock through AudioService (scene entry point)
│  │  └─ Shaders/SkyTimeOfDay.shader        "Aipocalypse/Skybox/Time Of Day": painted panorama + night gradient, horizon glow, sun disc, stars, moon phases from _TOD_ globals
│  ├─ EquipmentSystem/                      Aipocalypse.Core.EquipmentSystem — rules and how-tos in EQUIPMENT.md
│  │  ├─ Items/ItemCategory.cs              [Flags] Gun, Melee: what a hotbar slot accepts
│  │  ├─ Items/ItemDefinition.cs            abstract SO: name, icon, Category, Prefab (one per item: world + hand), CreateInstance
│  │  ├─ Items/EquippableDefinition.cs      abstract SO: equip / holster time; HeldPrefab = the HeldItem on Prefab's root
│  │  ├─ Items/ItemInstance.cs              one owned item (per-copy state goes in subclasses)
│  │  ├─ Hotbar.cs                          ≤ 9 slots, category check, SlotChanged, FindNext, FindFreeSlot
│  │  ├─ EquipmentSwitcher.cs               holster → swap → equip timing (Raise 0..1, SwitchPhase)
│  │  ├─ Equipment.cs                       selected slot ↔ switcher target; Select / Cycle; pick up (store / swap), DropSelected
│  │  ├─ PickUpAction.cs                    None / Store / Swap
│  │  ├─ IEquipmentOwner.cs                 on an actor's root: exposes its Equipment to world items
│  │  ├─ WorldItem.cs                       item lying in the world (on the item prefab's root): IInteractable, picks up / swaps; Spawn for drops; Awake shows only the World child; StripForHand
│  │  ├─ EquipmentConfig.cs                 SO: SwitchSpeed, slots (accepts, starting item) → CreateHotbar
│  │  ├─ HeldItem.cs                        root of an item's first-person prefab; Bind(item, owner), Show / Hide / OnReady, Tick(input), LateTick, ViewKick, IsAiming, KeepsClear, Zoom (field of view multiplier), Spread (shot cone for the crosshair), StopsSprint(use, aim), TryGetTip (model's furthest point from mesh bounds), Hands (its ItemHands)
│  │  ├─ HeldItemInput.cs                   per-frame use / aim / reload wish + holder state (IsReady, sprinting…); ViewKick struct
│  │  └─ EquipmentRig.cs                    spawns HeldItems (inactive staging → inject → StripForHand → hand), shows the held one, temporary lower/raise pose, TickHeld
│  ├─ HandSystem/                           Aipocalypse.Core.HandSystem — first-person floating hands, see HANDS.md
│  │  ├─ HandPose.cs                        HandSide, Finger, FingerShape / HandShape (Curl, Spread, Tip per finger); HandPose SO
│  │  ├─ HandRig.cs                         root of a hand prefab: grip point, finger chains + Open / Closed / Spread / Fist calibration, bounded grip-to-fist blending, Place, Apply, forearm axis, contact samples, setup buttons
│  │  ├─ HandSet.cs                         SO: the left and right hand prefabs, implied elbows (view space), preview item position
│  │  ├─ HandGrip.cs                        where a hand holds an item (side, pose, aim at elbow / as authored, roll, baked fit or clip-authored shape); Resolve; Preview hand, Bake fit; FingerMask
│  │  ├─ ItemHands.cs                       on an item prefab: one grip per hand, per-item hand scale; Show hands preview, Bake fit for all grips
│  │  ├─ FirstPersonHands.cs                on the player: spawns the HandSet, puts each hand on the item's grip (elbow-aimed) with its finger shape
│  │  ├─ HandFit.cs                         editor only: Bake fit — palm onto the item, finger curl / tip search against its meshes (signed distance)
│  │  └─ HandPreview.cs                     editor only: DontSave ghost hands that follow grips in edit mode; edit-mode elbow
│  ├─ InputSystem/                          Aipocalypse.Input
│  │  ├─ InputService.cs                    the only owner of actions, map switching, cursor lock; hotbar keys + cycling, interact, drop, attack, aim, reload; Debug map (N / M) enabled in dev builds
│  │  ├─ InputInstaller.cs                  registers ControlSettings + InputService (entry point)
│  │  ├─ ControlSettings.cs                 player-facing control options
│  │  └─ GameInputActions.cs                GENERATED from Settings/InputSystem_Actions.inputactions — don't edit
│  ├─ PlayerSystem/                         Aipocalypse.Core.PlayerSystem
│  │  ├─ Player.cs                          view MonoBehaviour: input → intent (look scaled by zoom), ticks the parts, IDamageable, IGunUser (owns the AmmoReserve); footstep / jump / landing sounds, aim / trigger end a sprint (HeldItem.StopsSprint), held item's LookTurn → PlayerLook.Turn, fall damage through DamageService
│  │  ├─ PlayerScope.cs                     EntityScope on the prefab root; default IStatProvider
│  │  ├─ PlayerInstaller.cs                 root-scope services: PlayerRegistry
│  │  ├─ PlayerRegistry.cs                  the active player (Current); players register themselves, publishes ActivePlayerChanged
│  │  ├─ PlayerIntent.cs                    per-frame wish: Move, LookDegrees, Sprint, Jump, SelectSlot, CycleSlot, Interact, Drop, Fire, FirePressed, Aim, Reload
│  │  ├─ PlayerMotor.cs                     CharacterController movement, jump, slopes, landing (+ LandingInfo); Jumped / Landed events; SpeedMultiplier (aiming)
│  │  ├─ PlayerLook.cs                      yaw on body, pitch on camera pivot, + the held item's ViewKick (recoil)
│  │  ├─ PlayerHeadBob.cs                   footstep bob + landing dip on the camera root; Stepped at the bottom of each bob; Phase / Intensity / SprintBlend for the held item's bob
│  │  ├─ HeldItemSway.cs                    held item's procedural motion (+ AimSwaySettings, HeldSwaySettings, HeldSwayInput): AimOffset / AimRotation for the crosshair, shots and model (breathing heavier when tired, walk / strafe bob, move / look lag, jump, air drift, landing, ADS raise), plus model-only position and roll; calmed by AimWeight, scaled by StatId.HeldItemSway
│  │  ├─ PlayerZoom.cs                      narrows the Cinemachine lens by the held item's Zoom, keeps FirstPersonView's FOV unzoomed, LookScale, TanHalfFov
│  │  ├─ PlayerAim.cs                       aim sway → CrosshairPosition and CrosshairSpread on screen (world FOV) + the rig's zoom-corrected turn; static ScreenPosition / ScreenRadius / ModelRotation
│  │  ├─ PlayerBreathSound.cs               loops PlayerSoundTuning.BreathSound while out of breath (volume by Tiredness, fades), pauses it while time is stopped
│  │  ├─ PlayerInteractor.cs                look sphere cast → focused IInteractable + prompt, Interact; drop point
│  │  ├─ HeldItemClearance.cs               eye → normal model tip sphere cast; smooth hand retraction / tilt near obstacles, never moves the player
│  │  ├─ FirstPersonView.cs                 on the world camera: the lens held models draw with (own FOV via SetFieldOfView so zoom doesn't swell the gun, near 0.01 / far 10 m)
│  │  ├─ FirstPersonRenderFeature.cs        URP renderer feature on PC_Renderer: draws the HeldWeapon layer in the world camera after transparents, depth cleared, with FirstPersonView's lens (replaces the overlay camera)
│  │  ├─ Stamina.cs                         stamina meter: spend (with or without regen delay), regen delay, exhaustion, winded wait after running dry
│  │  ├─ Fatigue.cs                         low-stamina tiers (+ FatigueSettings, FatigueTier) → eased recoil / melee power / move speed multipliers, slower still while winded; StatId.FatiguePenalty
│  │  ├─ PlayerBreathing.cs                 out of breath (+ BreathingSettings): IsActive, eased Tiredness and Volume for the breath loop and the aim sway
│  │  ├─ FallDamage.cs                      drop height → damage
│  │  ├─ Tuning/PlayerTuning.cs             ScriptableObject that links the groups below; Player hands each rule only its group
│  │  ├─ Tuning/PlayerMovementTuning.cs     speeds, acceleration, jump, gravity, slopes, heavy landing, sprint / jump stamina costs → PlayerMotor
│  │  ├─ Tuning/PlayerVitalsTuning.cs       max health, max stamina + regen, winded time, fatigue tiers, fall damage heights → Player
│  │  ├─ Tuning/PlayerCameraTuning.cs       pitch limits, head bob, landing dip → PlayerLook, PlayerHeadBob
│  │  ├─ Tuning/PlayerInteractionTuning.cs  look ray reach / radius / mask, drop distance → PlayerInteractor
│  │  ├─ Tuning/PlayerHeldItemTuning.cs     aim origin, aim move speed, obstacle pose, AimSwaySettings, HeldSwaySettings → Player, HeldItemClearance, HeldItemSway
│  │  ├─ Tuning/PlayerSoundTuning.cs        footstep / jump / land / breath SoundIDs and volumes, breathing threshold / volume / fades → Player
│  │  ├─ Defaults/BaseStats.cs              IStatProvider default: returns base values
│  │  └─ Prefabs/Player.prefab              the player entity (see below)
│  ├─ UI/                                   Aipocalypse.Core.UI — rules and how-tos in UI.md
│  │  ├─ Widgets/StatBar.cs                 passive meter view: SetValue / SetWarning / ApplyStyle, optional trail
│  │  ├─ Widgets/StatBarStyle.cs            ScriptableObject: sprites, colors, font for a StatBar
│  │  ├─ Widgets/HotbarSlotView.cs          passive slot view: SetKey / SetIcon / SetSelected / SetAmmo / HideAmmo / ApplyStyle
│  │  ├─ Widgets/HotbarSlotStyle.cs         ScriptableObject: tile, frame, key cap sprites + colors, key font, ammo font + colors
│  │  ├─ Widgets/InteractPromptView.cs      passive "[E] Pick up Rifle" view: SetKey / Show / Hide / ApplyStyle
│  │  ├─ Widgets/InteractPromptStyle.cs     ScriptableObject: key cap sprite, fonts, verb / target colors
│  │  ├─ Widgets/ClockView.cs               passive digital clock: SetTime (monospaced HH:MM, char buffer) / SetPhase / SetDay / Show / ApplyStyle
│  │  ├─ Widgets/ClockStyle.cs              ScriptableObject: fonts + underlay materials, sizes, colors, backing panel, phase labels / colors
│  │  ├─ HudScope.cs                        EntityScope on the HUD root: injects the HUD's views in any scene
│  │  ├─ Hud/PlayerHudView.cs               base presenter: follows PlayerRegistry, Bind / Unbind per active player
│  │  ├─ Hud/PlayerVitalsHud.cs             PlayerHudView → health + stamina bars (LateUpdate)
│  │  ├─ Hud/HotbarHud.cs                   PlayerHudView → one slot view per slot of Player.Equipment + gun ammo counts (events, no polling)
│  │  ├─ Hud/InteractPromptHud.cs           PlayerHudView → prompt from Player.Interactor (LateUpdate)
│  │  ├─ Hud/CrosshairHud.cs                PlayerHudView: moves the crosshair to Player.CrosshairPosition, fades the dot while aiming / sprinting, pushes the spread ticks out to Player.CrosshairSpread
│  │  ├─ Hud/HitFeedbackHud.cs              PlayerHudView → DamageDealt from the player: crosshair hit / kill marker, hit / kill sound, pooled damage numbers
│  │  ├─ Hud/ClockHud.cs                    WorldClock (TryResolve; hidden without one) → ClockView (LateUpdate, view skips repeats)
│  │  ├─ Prefabs/HUD.prefab                 standalone overlay canvas 1920×1080, SafeFrame + regions, HudScope (placed in the scene)
│  │  ├─ Prefabs/Widgets/StatBar.prefab     the bar widget
│  │  ├─ Prefabs/Widgets/HotbarSlot.prefab  the hotbar slot widget
│  │  ├─ Prefabs/Widgets/InteractPrompt.prefab  the interact prompt widget
│  │  └─ Prefabs/Widgets/DamageNumber.prefab    Damage Numbers Pro mesh number (3D, pooled), font Data/UI/DamageNumberFont.asset
│  ├─ WorldTimeSystem/                      Aipocalypse.Core.WorldTimeSystem — the in-game clock, see DAYNIGHT.md
│  │  ├─ WorldTime.cs                       WorldTime (hours since day 1: Day, Hour, WholeHour, Minute, HoursUntil) + DayPhase enum
│  │  ├─ ClockSettings.cs                   SO: seconds per game hour, master speed, start, phase starts + speeds, moon cycle; PhaseAt, RealSecondsBetween, Real time inspector box
│  │  ├─ WorldClock.cs                      scene service (ITickable, scaled time): Advance steps hour by hour and phase by phase (events in order); JumpTo / SetTime / SkipTo; speed requests (slowest wins); MoonPhase
│  │  ├─ TimeWindow.cs                      On / Off / Fade hours, may wrap past midnight → Weight(hour)
│  │  ├─ WorldClockCheats.cs                editor and dev builds (registered when Debug.isDebugBuild): N → JumpToNextHour, M → JumpToNextPhase (InputService debug presses)
│  │  └─ WorldTimeInstaller.cs              scene scope: ClockSettings + WorldClock (entry point) + cheats
│  └─ WeaponSystem/                         Aipocalypse.Core.WeaponSystem — rules and how-tos in WEAPONS.md
│     ├─ GunDefinition.cs                   EquippableDefinition, Category Gun: fire, bullet, magazine, jam, recoil, accuracy, handling, sounds, effects
│     ├─ GunSettings.cs                     the setting groups above + FireMode, ReloadType, AimOrigin (Muzzle, Camera, MuzzleToCrosshair)
│     ├─ GunInstance.cs                     one owned gun: Rounds, IsJammed, Changed (survives drop / swap)
│     ├─ MeleeDefinition.cs                 EquippableDefinition, Category Melee: Swing, Sounds, ImpactEffect
│     ├─ MeleeSettings.cs                   SwingSettings (damage, reach, radius, wind-up, recovery, knockback, layers), MeleeSounds
│     ├─ MeleeAttack.cs                     melee rules: swing timing, hold to repeat, buffered click, alternating sides → SwingStarted, Struck
│     ├─ HeldMelee.cs                       HeldItem for melee: runs MeleeAttack, procedural swing between pose markers, sphere-cast hit → DamageService, knockback, impact, sounds (AudioService)
│     ├─ Ammo/AmmoType.cs                   SO: a kind of round; guns of one type share a pool
│     ├─ Ammo/AmmoReserve.cs                the holder's spare rounds by type; Take / Add / Infinite, Changed
│     ├─ Ammo/AmmoConfig.cs                 SO: starting reserve + Infinite → CreateReserve
│     ├─ IWeaponUser.cs                     holder seam for any weapon: Eye, Root, MeleePower, Exert (stamina per attack; StatId.AttackStaminaCost)
│     ├─ IGunUser.cs                        IWeaponUser + Ammo, AimFrom, AirSpreadScale, FatigueRecoilScale, AimOffset (Player implements it; StatId.AirSpread)
│     ├─ GunTrigger.cs                      fire modes, fire rate, bursts; frame-rate independent
│     ├─ Gun.cs                             a held gun's rules: shoot, magazine, reload (per-round stopped by firing after the current round), jam + clear, pump cycle, aim, sprint recovery (shots wait, clicks kept), empty click; events (+ GunAction, GunInput)
│     ├─ GunRecoil.cs                       procedural recoil: spray pattern → aim kick (view, springs back) + kept share (LookTurn) + small gun kick (model); hip / aim values blended by aim weight
│     ├─ GunJolt.cs                         GunJolt (self-returning kick / wobble of the model) + GunJoltPlayer (runs one; HeldGun applies it on the recoil pivot)
│     ├─ BulletSpread.cs                    random direction inside a cone
│     ├─ GunAccuracy.cs                     spread while airborne (AccuracySettings): less when aiming, × the holder's AirSpreadScale, settles back after landing
│     ├─ GunAim.cs                          muzzle-to-crosshair direction (with the too-close fallback)
│     ├─ HeldGun.cs                         HeldItem for guns: runs Gun, spawns bullets, aim / sprint poses + aim zoom, recoil + jolts on the recoil pivot, drives its (required) GunAnimation, sprint recovery, empty click, forwards GunRepairFlow / jam events to GunRepairFeedback, sounds (AudioService)
│     ├─ GunRepairFeedback.cs               presents one gun's jam / repair beats: sounds, gun jolts, GunJamBeat / GunRepairQteStarted / GunRepairCompleted
│     ├─ GunRepairFlow.cs                   the jam repair's QTE rules: start at QteStartProgress, early finish, miss penalty, interrupt on cancel; QteStarted / Missed
│     ├─ GunAnimation.cs                    every gun's clip view: GunClip enum = Gun.controller states, per-gun Override Controller, manual controller/Animation Rigging graph (hashes + clip lengths cached, no per-frame allocation), action-progress sampling, repair impact events, pooled cosmetic magazine release
│     ├─ Ballistics/BulletSystem.cs         root service (ITickable): bullets as data, ray-tested against each shot's HitLayers, damage through DamageService (source = shooter), impacts, holes (+ BulletShot)
│     ├─ Effects/EffectPool.cs              root service (ITickable): one ObjectPool per effect prefab, timed returns, caps
│     ├─ Effects/PooledEffect.cs            root of a pooled effect prefab: lifetime, max alive, Restart
│     ├─ Effects/BulletEffects.cs           SO: muzzle flash, tracer, impact, bullet hole + hole settings
│     ├─ WeaponInstaller.cs                 registers EffectPool + BulletSystem (entry points) in the root scope
│     ├─ Prefabs/Guns/Gun_Base.prefab       HeldGun skeleton: Poses (Hip, Aim, Sprint), PosePivot > RecoilPivot > Muzzle
│     ├─ Prefabs/Guns/AK47, Pistol, MP5, DoubleBarrel, PumpShotgun.prefab   variants of Gun_Base with the store model and their poses / muzzle;
│     │                                     also the pickup: root WorldItem + trigger box, World child = model lying flat
│     ├─ Prefabs/Effects/                   MuzzleFlash, Tracer, BulletImpact, BulletHole (PooledEffect roots)
│     ├─ Materials/BulletHoleDecal.mat, Textures/BulletHole.png   bullet hole decal
│     └─ Prefabs/Melee/FireAxe.prefab       HeldMelee: Poses (Idle, WindUp, FollowThrough, Sprint), PosePivot > Model; WorldItem + World like the guns
├─ (Assets/_Samples/Audio/Weapons/)        gun sound samples from the old project (rifle, pistol, sniper bolt)
├─ Data/
│  ├─ Audio/                                BroAudio libraries (Guns, Melee, RepairFeedback, Player, Combat, Ambience .asset) + one folder of entities each
│  ├─ WorldTime/ClockSettings.asset         30 s per game hour (12-minute day), start 07:00, phases 05 / 07 / 18 / 20
│  ├─ Environment/MeadowHome/TimeOfDay/     MH_TimeOfDay (profile), Looks/TOD_*.asset, Grades/PP_*.asset, MH_Sky_TimeOfDay.mat, MH_Ambience.asset
│  ├─ Player/PlayerTuning.asset             used in every scene; links Player{Movement,Vitals,Camera,Interaction,HeldItem,Sound}Tuning.asset beside it
│  ├─ Equipment/EquipmentConfig.asset       3 slots "Weapon 1–3", each takes Gun | Melee, empty at spawn
│  ├─ Items/AK47, Pistol, MP5, DoubleBarrel, PumpShotgun, FireAxe.asset   item definitions (one prefab each; weapons hold all their numbers)
│  ├─ Weapons/RifleAmmo, PistolAmmo, ShotgunShells.asset   ammo types
│  ├─ Weapons/AmmoConfig.asset              the player's starting reserve (90 rifle, 48 pistol, 24 shells), Infinite off
│  ├─ Weapons/BulletEffects.asset           effects shared by both guns
│  └─ UI/HealthBarStyle.asset, StaminaBarStyle.asset, HotbarSlotStyle.asset, InteractPromptStyle.asset, ClockStyle.asset, DamageNumberFont.asset
├─ Tests/PlayMode/PrototypeSmokeTests.cs  asmdef Aipocalypse.Tests.PlayMode: loads Prototype.unity, checks injection and enemy registration, picks up a gun, fires and reloads; any logged error fails it
└─ Tests/EditMode/                          asmdef Aipocalypse.Tests.EditMode
   ├─ CombatTests.cs                        DamageService: applies, publishes with source / result, kill, ignores dead / null / zero
   ├─ PlayerRulesTests.cs                   Stamina, Fatigue, PlayerBreathing, Health, FallDamage, directional speed
   ├─ EquipmentTests.cs                     Hotbar, EquipmentSwitcher timing, Equipment select / cycle, pick up / swap / drop
   ├─ GunTests.cs                           fire modes and rate, magazine, reloads, jams, interrupts, shared ammo, recoil, spread
   ├─ GunAnimationTests.cs                  AK47 override/curve bindings, automatic Animator overwrite regression, controller restoration, hand contacts, magazine visibility, interrupt reset, pause/equip/fire, repair, scaled-hand grip alignment
   ├─ WeaponAnimationTests.cs               MP5/pistol/shotgun bindings, scaled hands, automatic Animator pass, contacts and continuity, slap/reset, repeated shell reload and pump cycle, break-action closure
   ├─ HandRigTests.cs                       normal grip compatibility, bounded fist calibration/blending, safe fallback and all guns' punch-contact finger shapes/closure timing
   ├─ ItemPrefabTests.cs                    every item has one prefab for world + hand; world layout; rig strips world parts
   ├─ HeldItemClearanceTests.cs              obstacle retraction and recovery, stable pose, ignores self / triggers, never moves the player
   ├─ HeldMotionTests.cs                    GunAccuracy (speed, airborne, aiming, steadiness, settling); HeldItemSway (look / move lag, aiming, sway stat, landing)
   ├─ WorldClockTests.cs                    clock speed, real minutes per day, phase speeds, events in one long frame, no skipped phase at a boundary, midnight, jumps, N / M cheats, skip (and a jump ending it), freeze, moon phase
   ├─ TimeOfDayTests.cs                     TimeWindow across midnight with fades, profile blending (linear, wrap past midnight, hold), sun path
   └─ MeleeTests.cs                         swing timing, hold rate, buffered click, alternating sides, switching / sprinting
```

## Who depends on whom

```text
ProjectLifetimeScope (root) ──registers──> InputService, ControlSettings, MessagePipe brokers, AudioService, DamageService,
                                           TimeService, QuickTimeService, PlayerRegistry, EffectPool, BulletSystem (ticked by VContainer)
                                                │
PlayerScope : EntityScope ── parent: GameLifetimeScope if the scene has one, else root
   │ registers default IStatProvider → BaseStats (only if no ancestor provides one)
   └─ injects the Player GameObject (whole hierarchy)
         Player.Construct(InputService, IStatProvider, DamageService, AudioService, IPublisher<PlayerDied>, PlayerRegistry, IObjectResolver)
           creates (plain C#, owned by Player):
             Health, Stamina, PlayerMotor(CharacterController, tuning.Movement, stats, stamina),
             PlayerLook(body, cameraPitch, tuning.Camera), PlayerHeadBob(cameraRoot, tuning.Camera), HeldItemSway(tuning.HeldItem.HeldSway, stats), PlayerZoom(Cinemachine camera, FirstPersonView), PlayerInteractor(cameraPitch, gameObject, tuning.Interaction),
             AmmoReserve(AmmoConfig.CreateReserve()),
             Equipment(EquipmentConfig.CreateHotbar()) → EquipmentRig.Bind(Equipment, owner: this, resolver)
               rig spawns item prefabs (staged inactive, resolver.InjectGameObject, world parts stripped) → HeldGun.Construct(BulletSystem, EffectPool, AudioService, QuickTimeService, publishers), HeldMelee.Construct(EffectPool, DamageService, AudioService)
               HeldItem.Bind(item, owner) → HeldGun builds Gun(GunInstance, owner.Ammo) + GunRecoil + GunAccuracy + GunRepairFlow; HeldMelee builds MeleeAttack
           last: PlayerRegistry.Register(this) → ActivePlayerChanged   (OnDestroy: Unregister)

HudScope : EntityScope (HUD.prefab root, separate from the Player) ── same parent lookup
   └─ injects the HUD GameObject
         PlayerHudView (PlayerRegistry, ISubscriber<ActivePlayerChanged>) → Bind / Unbind the active player
           PlayerVitalsHud, CrosshairHud, HotbarHud, InteractPromptHud (+ InputService for key labels), HitFeedbackHud (+ AudioService, DamageDealt),
           RepairQteHud (gun repair events)
         ClockHud (TryResolve WorldClock)

GameLifetimeScope (scene) ──registers──> WorldClock (+ WorldClockCheats), EnvironmentDirector (RegisterComponent → TryResolve WorldClock), AmbienceService
   EnvironmentDirector ticks its reactors (scene list collected in the editor; injected spawns Register themselves)
```

- Only `Player` gets injected. The parts are plain classes it creates and owns. They get Unity components through their constructors, not through DI.
- `PlayerSystem` → `EquipmentSystem` ← `WeaponSystem`: the player owns an `Equipment`; weapon kinds derive from equipment's definitions. `EquipmentSystem` knows neither the player nor weapons: `WorldItem` finds the picker's hotbar through `IEquipmentOwner`, and a `HeldGun` finds its holder's ammo and aim through `IGunUser`, a `HeldMelee` its eye and root through `IWeaponUser` (Player implements all of them). The player also uses a few `WeaponSystem` types directly (`AmmoReserve`, `AimOrigin` in its tuning).
- Bullets, swings and falls hurt anything with an `IDamageable` (`_Common/Combat`) through `DamageService.Deal`, which applies `TakeDamage` and publishes `DamageDealt` (source, result). Targets only apply their own rules.
- Interaction: `_Common/Interaction.IInteractable` is the only contract. `PlayerInteractor` finds one with a look cast; the interactable decides what happens (`WorldItem` → `Equipment.TryPickUp`).
- Inside the player system, plain C# events are used (`PlayerMotor.Landed`). Anything crossing systems goes through MessagePipe (`DamageDealt`, `PlayerDied`).

## Frame order (Player.Update)

```text
intent = dead or gameplay input disabled ? default : read InputService
PlayerLook.Tick      yaw + pitch                    (movement uses the new facing this frame)
PlayerMotor.Tick     velocity, jump, CharacterController.Move, landing → Landed event
                       └─ Player.OnJumped / OnLanded: jump / landing sound, head-bob dip, fall damage → DamageService → Health → DamageDealt / PlayerDied
Stamina.Tick         regen (spending happened inside the motor, and in the held item last frame)
Fatigue.Tick         tier for the stamina (+ winded) → eased recoil / melee power / move speed (used by this frame's held item and next frame's motor)
PlayerBreathing.Tick out of breath → eased Tiredness → Player starts / fades / stops the breath loop (AudioService.Start)
PlayerHeadBob.Tick   camera root offset; Stepped → footstep sound
PlayerInteractor     look cast → focus + prompt; intent.Interact → IInteractable.Interact (pickup / swap)
Equipment            intent.Drop → DropSelected → WorldItem.Spawn at the drop point; else SelectSlot / CycleSlot → Select / Cycle; EquipmentSwitcher.Tick (holster → swap → equip)
                       └─ HeldChanged → EquipmentRig shows/hides models; SelectionChanged → HotbarHud highlight
EquipmentRig.Tick    restore hand pose from Raise before measuring obstacles
EquipmentRig.TickHeld  the held item: HeldGun → Gun.Tick (reload / jam clear / pump, aim, trigger → Fired counts shots); pose + recoil pivots,
                       GunAnimation samples the controller and constraints (parts, hand targets, fingers).
                       HeldMelee → MeleeAttack.Tick (Struck marks a hit to check); swing pose
PlayerLook.Turn      held item's LookTurn (kept recoil) into yaw / pitch; PlayerLook.SetKick: its ViewKick on the camera pivot; PlayerZoom.Tick(held.Zoom)
HeldItemSway.Tick    aim offset (breathing by Tiredness, bob in step with the head, spring lag / kicks) + model position / roll, calmed by held.AimWeight
                       → EquipmentRig.ApplyHandMotion (model extras); PlayerAim.Tick → CrosshairPosition / CrosshairSpread (world FOV), EquipmentRig.SetAimRotation (zoom-corrected)
PlayerMotor.SpeedMultiplier  from IsAiming × Fatigue.MoveSpeed
HeldItemClearance    eye → normal model tip cast; blend an obstacle offset / tilt onto the hand (melee swings opt out)
HeldItem.LateTick    model and camera are final: HeldGun spawns this frame's shots (muzzle flash, one GunAccuracy spread for the shot, BulletSystem.Fire per pellet)
                       from the muzzle as drawn, then GunRecoil.Kick (shows from next frame).
                       HeldMelee sphere-casts from the camera → damage, knockback, impact effect, hit sound
FirstPersonHands.Tick  each hand onto the held item's grip, turned towards its implied elbow; hidden with no grip
VContainer ticks     BulletSystem moves and ray-tests bullets → DamageService → DamageDealt → HitFeedbackHud (marker, sound, number); impacts, holes;
                     EffectPool returns expired effects; EnemyManager ticks registered enemy trees (scene scope);
                     WorldClock advances (HourChanged / DayPhaseChanged / DayStarted); cheats; AmbienceService bed volumes and calls
LateUpdate           CinemachineBrain moves PlayerCamera to CameraPitch
                     PlayerVitalsHud reads Health / Stamina → StatBar.SetValue (skips unchanged values)
                     CrosshairHud reads Player.CrosshairPosition / AimWeight / IsSprinting → moves and fades the dot
                     InteractPromptHud reads Player.Interactor → InteractPromptView
                     EnvironmentDirector reads WorldClock.Now → light, RenderSettings, sky globals, grades → reactors
                     ClockHud reads WorldClock.Now / Phase → ClockView
StatBar.Update       only while a trail is catching up, otherwise disabled
```

## Player prefab

```text
Player                       CharacterController (h 1.8, r 0.35, step 0.35, slope 45°) · Player (AmmoConfig) · PlayerScope
├─ CameraRoot                y 1.65 (eye height); PlayerHeadBob offsets it
│  └─ CameraPitch            PlayerLook pitches it; Cinemachine tracking target
│     └─ HeldItems           EquipmentRig
│        ├─ Hand             (0.17, -0.21, 0.62); held prefabs spawn here, posed by the rig (guns add their own poses on top)
│        └─ Hands            FirstPersonHands (HandSet); HeldWeapon layer; spawns FPHand_Left / FPHand_Right
├─ PlayerCamera              Camera (tag MainCamera, all layers, no camera stack) · URP camera data · CinemachineBrain · AudioListener · FirstPersonView
└─ PlayerCinemachineCamera   CinemachineCamera (FOV 70) · HardLockToTarget · RotateWithFollowTarget
```

The HUD is not in this prefab: `HUD.prefab` sits next to it in the scene ([UI.md → Where the HUD lives](UI.md#where-the-hud-lives)).

## Adding a script

- **New system:** folder under `_Core/` (or `Features/` / `_Experimental/`), namespace `Aipocalypse.Core.<System>`, one `<System>Installer : IInstaller` registered from the right scope. Add it to the map above.
- **Rules go in plain C#** (testable, EditMode tests in `Tests/EditMode/`). MonoBehaviours are views: they wire components and forward to the rules.
- **Numbers go in a ScriptableObject** type next to the system, with the asset in `Data/<System>/`.
- **New `*LifetimeScope.cs`:** `DisableScriptModifier` is on, but check that VContainer didn't replace the file with its template.
- **Generated code** (`GameInputActions.cs`): change the `.inputactions` asset, never the file.


## Repair QTE and time ownership

- `_Core/TimeSystem/TimeService.cs`: sole writer of Time.timeScale; owner-keyed minimum-scale requests, explicit pause priority, fixed-step scaling, disposal restoration, and RequestHitStop (real-time hit stops timed by its unscaled Tick; overlaps keep the longest/lowest; ignored while paused). TimeInstaller.cs registers it as a root entry point.
- `_Core/_Common/Feedback/FeedbackContext.cs`: injected bridge on hosts of MMF_Players so custom Feel feedbacks reach our services (TimeService). `MMF_HitStop.cs`: Feel feedback "Aipocalypse/Hit Stop" → TimeService.RequestHitStop; never touches Time.timeScale.
- `_Core/WeaponSystem/GunJamFeedback.cs`: on `Player/GunJamFeedback` (with FeedbackContext); subscribes to its owner's GunJamBeat and GunRepairCompleted and plays one MMF_Player per beat (camera impulse; hit stop for punch and success).
- `_Core/_Common/Events/GunJamBeat.cs`: owner, item and kind (Jammed, DryFire, Slap, Punch, Miss) for presenting a jam and its repair; published by HeldGun.
- `_Core/QuickTimeSystem/QteChallenge.cs`: abstract challenge data, IQteSession contract and QteResult. TimingQteChallenge.cs supplies repeating fill logic and configurable duration, zone and per-cycle attempts.
- `_Core/QuickTimeSystem/QuickTimeService.cs`: one active owner-bound session, unscaled central tick, confirmation through InputService.ReloadPressed, timed or owner-controlled lifetime, cancellation, optional time request, completion callback and lifecycle events. QuickTimeInstaller.cs registers the root entry point.
- `_Core/_Common/Events/QteEvents.cs`: QteStarted, QteEnded and accepted QteAttempted MessagePipe payloads; no per-frame messages. IQteSession.Press distinguishes Ignored/Miss/Success, so locked presses emit no attempt event.
- `_Core/WeaponSystem/GunRepairProfile.cs`: per-gun automatic/manual initiation, jam pause, quick/stubborn duration and probability, configurable QTE onset fraction, miss penalty and cap, challenge and cosmetic feedback references. Gun.cs selects timing, waits out the jam pause (DryFired on trigger pulls), applies miss penalties (AddRepairTime) and owns completion. GunRepairFlow.cs requests/cancels the QTE and turns its results into Gun calls; HeldGun.cs chooses repair animation states and presents the flow's events.
- `_Core/WeaponSystem/RepairFeedbackSettings.cs`: shared or per-gun ScriptableObject preset for result colors, UI punch/ring/hold/shake/squash/fade, GunJolt presets (jam, dry fire, slap, punch, miss, success; `GunJolt.cs` holds the struct and the GunJoltPlayer that runs one) and success/mechanism/accent/miss/natural sounds (SoundIDs). HeldGun plays them through AudioService and the active jolt on its existing recoil pivot (scaled time), resets on hide, and publishes GunJamBeat and GunRepairCompleted.
- `_Core/_Common/Events/GunRepairCompleted.cs`: owner, item and QTE-success flag for completed repairs; interruptions do not publish it.
- `_Core/QuickTimeSystem/TimingQteChallenge.cs`: timing settings and session with independent lifetime/pointer clocks, configurable miss hold and bounded green-zone relocation after the hold. Pointer pauses never extend standalone expiry or gun repair; presses during a hold are ignored. Optional random delegate supports deterministic relocation checks.
- `_Core/UI/Widgets/QteStyle.cs`: Synty sprites, colors (track, fill, zone, no-attempt fill, marker, track flash strength) and the key cap (sprite, font, size, label color, gap below the QTE). QteView.cs is the replaceable view contract; TimingQteView.cs presents circle or bar without weapon/input knowledge.
- `_Core/UI/Hud/RepairQteHud.cs`: PlayerHudView presenter for the compact QTE/key cap plus scaled DOTween result feedback. Subscribes to GunRepairQteStarted, GunJamBeat (Miss) and GunRepairCompleted for the active player only, never reads the gun, retains its last session for success/natural fade, and resets on cancellation, unbind, disable, input lock or weapon change; idle frames do nothing. Pre-placed SuccessBurst and CanvasGroup avoid runtime object creation. QteView.Flash lets compatible widgets implement the flash without coupling the session to UI.
- InputService.cs owns confirmation input consumption and the Reload binding label. ProjectLifetimeScope installs TimeInstaller and QuickTimeInstaller after InputInstaller.
- `Tests/EditMode/RepairQteTests.cs`: timing passes, zone tuning, timeout, automatic/manual repair variants, continuous progress, owner-controlled QTE lifetime, early completion, interruption and time request arbitration. GunAnimationTests also validates repair focus/recovery contacts and all AK47 override bindings.
- `AgentScripts/RepairQteSetup.cs`: live-Editor asset/clip/HUD builder. RepairQteChecks.cs: Play-mode input, timing, time restoration, cancellation and screenshot staging checks. The builder refuses to duplicate an existing HUD widget; AutomaticRepair updates its layout/tuning in place.


GunAnimation also samples the clip-authored `_repairImpactCount` track and raises RepairSlapped once per contact; the stepped `_repairPunch` track makes ImpactIsPunch true for punches. HeldGun subscribes locally and plays the GunSounds.RepairSlap or RepairPunch entity (random clip and volume live on the entity). Early success uses cached transform/shape snapshots to blend to RepairEnd without playing the remaining hits; HandGrip.SetAnimatedShape applies the blended finger pose. Arrays are cached once when the graph is created. GunAnimationTests covers skipped-frame markers, duplicate suppression, pause, restart and early success with no extra impacts.

All five guns now supply independent Data/Weapons/<Gun>_Repair and <Gun>_RepairQte assets, impact audio settings on their GunDefinition, and four repair overrides. RepairQteSetup.OtherGuns creates the per-gun data; Ak47Animations.UpdateRepairing(weapon) fits the shared slap authoring to each gun's contact hierarchy. RepairQteChecks accepts any configured held gun and verifies full repair duration, 3/4 impacts and audio starts, persistent QTE and early completion. WeaponAnimationTests validates all new override bindings plus both repair variants and early returns for all four later guns.
