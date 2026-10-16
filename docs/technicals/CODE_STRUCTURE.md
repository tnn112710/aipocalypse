# Code structure

Map of the scripts that exist today: where each one lives, what it owns, and how they connect. The *why* (layers, scopes, Core vs Features) is in [ARCHITECTURE.md](ARCHITECTURE.md). **Update this file whenever you add, move or remove a script.**

## Assemblies and namespaces

| Assembly | Folder | References |
| --- | --- | --- |
| `Aipocalypse.Core` | `Assets/_Game/_Core/` | VContainer, MessagePipe (+ .VContainer), UniTask, Unity.InputSystem, Unity.Cinemachine, UnityEngine.UI, Unity.TextMeshPro, AstarPathfindingProject, PackageTools, Drawing; precompiled plugin DLLs auto-referenced |
| `Aipocalypse.Tests.EditMode` | `Assets/_Game/Tests/EditMode/` | `Aipocalypse.Core`, VContainer, test runner, NUnit. Editor only |

Namespaces follow the folder: `Aipocalypse.Core.<Folder>` (e.g. `Aipocalypse.Core.PlayerSystem`, `Aipocalypse.Core.Combat`). One exception: input uses **`Aipocalypse.Input`**, because a namespace ending in `InputSystem` shadows `UnityEngine.InputSystem.InputSystem`.

## Enemy wander slice

`_Core/EnemySystem/` (`Aipocalypse.Core.EnemySystem`):

- `EnemyInstaller.cs` registers scene navigation, settings, assigned agents, and the central entry point.
- `EnemyManager.cs` starts agents and time-slices Manual Behavior Designer trees through VContainer's tick loop.
- `EnemyBrain.cs` bridges each tree to its mover and stops both when disabled.
- `WanderMover.cs` samples reachable destinations around the spawn point and drives A* FollowerEntity, including arrival and travel timeout checks.
- `WanderTasks.cs` contains the choose, walk, and pause ActionNodes used by the shared subtree.
- `WanderSettings.cs` holds movement, radius, pause, timeout, and think-stride tuning; the asset and shared subtree are in `Data/Enemies/`.

`GameLifetimeScope` installs this slice when navigation and settings are assigned. `AgentScripts/WanderDemo.cs` builds and inspects the two-agent scene setup through Unity CLI. Core also references AstarPathfindingProject and its PackageTools/Drawing base assemblies.

## Script map

```text
Assets/_Game/
├─ _Core/                                   asmdef Aipocalypse.Core
│  ├─ _Bootstrap/                           Aipocalypse.Core.Bootstrap
│  │  ├─ ProjectLifetimeScope.cs            root scope: MessagePipe, InputInstaller, PlayerInstaller, WeaponInstaller
│  │  ├─ ProjectLifetimeScope.prefab        holds ControlSettings (mouse sensitivity, invert Y…)
│  │  ├─ VContainerSettings.asset           Preloaded Asset → spawns the root prefab before any scene
│  │  └─ GameLifetimeScope.cs               per-scene scope, optional EnemyInstaller, Strict flag, execution order -5000
│  ├─ _Common/
│  │  ├─ Entities/EntityScope.cs            Aipocalypse.Core.Entities — base child scope for unique entities, RegisterDefault
│  │  ├─ Combat/                            Aipocalypse.Core.Combat
│  │  │  ├─ IDamageable.cs                  anything that can take damage
│  │  │  ├─ DamageInfo.cs                   DamageInfo struct (amount, type, point, knockback Force) + DamageType enum (Generic, Fall, Gunshot, Melee)
│  │  │  └─ Health.cs                       plain HP meter: Damage / Heal / Restore
│  │  ├─ Events/CombatEvents.cs             Aipocalypse.Core.Events — DamageDealt, PlayerDied
│  │  ├─ Events/PlayerEvents.cs             ActivePlayerChanged (read PlayerRegistry.Current)
│  │  ├─ Interaction/IInteractable.cs       Aipocalypse.Core.Interaction — TryGetPrompt / Interact(actor) + InteractionPrompt
│  │  └─ Stats/IStatProvider.cs             Aipocalypse.Core.Stats — StatId enum + stat seam
│  ├─ _Debug/TargetDummy.cs                 Aipocalypse.Core.Debugging — shooting-range target: logs hits, refills its health, tips over from knockback and springs back
│  ├─ EquipmentSystem/                      Aipocalypse.Core.EquipmentSystem — rules and how-tos in EQUIPMENT.md
│  │  ├─ Items/ItemCategory.cs              [Flags] Gun, Melee: what a hotbar slot accepts
│  │  ├─ Items/ItemDefinition.cs            abstract SO: name, icon, Category, WorldPrefab, CreateInstance
│  │  ├─ Items/EquippableDefinition.cs      abstract SO: held prefab, equip / holster time
│  │  ├─ Items/ItemInstance.cs              one owned item (per-copy state goes in subclasses)
│  │  ├─ Hotbar.cs                          ≤ 9 slots, category check, SlotChanged, FindNext, FindFreeSlot
│  │  ├─ EquipmentSwitcher.cs               holster → swap → equip timing (Raise 0..1, SwitchPhase)
│  │  ├─ Equipment.cs                       selected slot ↔ switcher target; Select / Cycle; pick up (store / swap), DropSelected
│  │  ├─ PickUpAction.cs                    None / Store / Swap
│  │  ├─ IEquipmentOwner.cs                 on an actor's root: exposes its Equipment to world items
│  │  ├─ WorldItem.cs                       item lying in the world: IInteractable, picks up / swaps; Spawn for drops
│  │  ├─ EquipmentConfig.cs                 SO: SwitchSpeed, slots (accepts, starting item) → CreateHotbar
│  │  ├─ HeldItem.cs                        root of an item's first-person prefab; Bind(item, owner), Show / Hide / OnReady, Tick(input), LateTick, ViewKick, IsAiming, KeepsClear, TryGetTip (model's furthest point from mesh bounds), Hands (its ItemHands)
│  │  ├─ HeldItemInput.cs                   per-frame use / aim / reload wish + holder state (IsReady, sprinting…); ViewKick struct
│  │  └─ EquipmentRig.cs                    spawns HeldItems through the resolver (injected), shows the held one, temporary lower/raise pose, TickHeld
│  ├─ HandSystem/                           Aipocalypse.Core.HandSystem — first-person floating hands, see HANDS.md
│  │  ├─ HandPose.cs                        HandSide, Finger, FingerShape / HandShape (Curl, Spread, Tip per finger); HandPose SO
│  │  ├─ HandRig.cs                         root of a hand prefab: grip point, finger chains + Open / Closed / Spread calibration, Place, Apply, forearm axis, contact samples, setup buttons
│  │  ├─ HandSet.cs                         SO: the left and right hand prefabs, implied elbows (view space), preview item position
│  │  ├─ HandGrip.cs                        where a hand holds an item (side, pose, aim at elbow / as authored, roll, baked fit or clip-authored shape); Resolve; Preview hand, Bake fit; FingerMask
│  │  ├─ ItemHands.cs                       on an item prefab: default grips, per-item hand scale + named procedural hand actions (hand keys, part tracks, shown props); Play / SetProgress / Stop; preview scrubber
│  │  ├─ FirstPersonHands.cs                on the player: spawns the HandSet, puts each hand on the item's current grip (elbow-aimed), arcs between grips with fingers opening
│  │  ├─ HandFit.cs                         editor only: Bake fit — palm onto the item, finger curl / tip search against its meshes (signed distance)
│  │  └─ HandPreview.cs                     editor only: DontSave ghost hands that follow grips in edit mode; edit-mode elbow
│  ├─ InputSystem/                          Aipocalypse.Input
│  │  ├─ InputService.cs                    the only owner of actions, map switching, cursor lock; hotbar keys + cycling, interact, drop, attack, aim, reload
│  │  ├─ InputInstaller.cs                  registers ControlSettings + InputService (entry point)
│  │  ├─ ControlSettings.cs                 player-facing control options
│  │  └─ GameInputActions.cs                GENERATED from Settings/InputSystem_Actions.inputactions — don't edit
│  ├─ PlayerSystem/                         Aipocalypse.Core.PlayerSystem
│  │  ├─ Player.cs                          view MonoBehaviour: input → intent, ticks the parts, IDamageable, IGunUser (owns the AmmoReserve)
│  │  ├─ PlayerScope.cs                     EntityScope on the prefab root; default IStatProvider
│  │  ├─ PlayerInstaller.cs                 root-scope services: PlayerRegistry
│  │  ├─ PlayerRegistry.cs                  the active player (Current); players register themselves, publishes ActivePlayerChanged
│  │  ├─ PlayerIntent.cs                    per-frame wish: Move, LookDegrees, Sprint, Jump, SelectSlot, CycleSlot, Interact, Drop, Fire, FirePressed, Aim, Reload
│  │  ├─ PlayerMotor.cs                     CharacterController movement, jump, slopes, landing (+ LandingInfo); SpeedMultiplier (aiming)
│  │  ├─ PlayerLook.cs                      yaw on body, pitch on camera pivot, + the held item's ViewKick (recoil)
│  │  ├─ PlayerHeadBob.cs                   footstep bob + landing dip on the camera root
│  │  ├─ PlayerInteractor.cs                look sphere cast → focused IInteractable + prompt, Interact; drop point
│  │  ├─ HeldItemClearance.cs               eye → normal model tip sphere cast; smooth hand retraction / tilt near obstacles, never moves the player
│  │  ├─ WeaponOverlayCamera.cs             render callback synchronizes weapon camera FOV / aspect / viewport after Cinemachine
│  │  ├─ Stamina.cs                         stamina meter: spend, regen delay, exhaustion
│  │  ├─ FallDamage.cs                      drop height → damage
│  │  ├─ PlayerTuning.cs                    ScriptableObject type for every player number
│  │  ├─ Defaults/BaseStats.cs              IStatProvider default: returns base values
│  │  └─ Prefabs/Player.prefab              the player entity (see below)
│  ├─ UI/                                   Aipocalypse.Core.UI — rules and how-tos in UI.md
│  │  ├─ Widgets/StatBar.cs                 passive meter view: SetValue / SetWarning / ApplyStyle, optional trail
│  │  ├─ Widgets/StatBarStyle.cs            ScriptableObject: sprites, colors, font for a StatBar
│  │  ├─ Widgets/HotbarSlotView.cs          passive slot view: SetKey / SetIcon / SetSelected / SetAmmo / HideAmmo / ApplyStyle
│  │  ├─ Widgets/HotbarSlotStyle.cs         ScriptableObject: tile, frame, key cap sprites + colors, key font, ammo font + colors
│  │  ├─ Widgets/InteractPromptView.cs      passive "[E] Pick up Rifle" view: SetKey / Show / Hide / ApplyStyle
│  │  ├─ Widgets/InteractPromptStyle.cs     ScriptableObject: key cap sprite, fonts, verb / target colors
│  │  ├─ HudScope.cs                        EntityScope on the HUD root: injects the HUD's views in any scene
│  │  ├─ Hud/PlayerHudView.cs               base presenter: follows PlayerRegistry, Bind / Unbind per active player
│  │  ├─ Hud/PlayerVitalsHud.cs             PlayerHudView → health + stamina bars (LateUpdate)
│  │  ├─ Hud/HotbarHud.cs                   PlayerHudView → one slot view per slot of Player.Equipment + gun ammo counts (events, no polling)
│  │  ├─ Hud/InteractPromptHud.cs           PlayerHudView → prompt from Player.Interactor (LateUpdate)
│  │  ├─ Prefabs/HUD.prefab                 standalone overlay canvas 1920×1080, SafeFrame + regions, HudScope (placed in the scene)
│  │  ├─ Prefabs/Widgets/StatBar.prefab     the bar widget
│  │  ├─ Prefabs/Widgets/HotbarSlot.prefab  the hotbar slot widget
│  │  └─ Prefabs/Widgets/InteractPrompt.prefab  the interact prompt widget
│  └─ WeaponSystem/                         Aipocalypse.Core.WeaponSystem — rules and how-tos in WEAPONS.md
│     ├─ GunDefinition.cs                   EquippableDefinition, Category Gun: fire, bullet, magazine, jam, recoil, handling, sounds, effects
│     ├─ GunSettings.cs                     the setting groups above + FireMode, ReloadType, AimOrigin (Muzzle, Camera, MuzzleToCrosshair)
│     ├─ GunInstance.cs                     one owned gun: Rounds, IsJammed, Changed (survives drop / swap)
│     ├─ MeleeDefinition.cs                 EquippableDefinition, Category Melee: Swing, Sounds, ImpactEffect
│     ├─ MeleeSettings.cs                   SwingSettings (damage, reach, radius, wind-up, recovery, knockback, layers), MeleeSounds
│     ├─ MeleeAttack.cs                     melee rules: swing timing, hold to repeat, buffered click, alternating sides → SwingStarted, Struck
│     ├─ HeldMelee.cs                       HeldItem for melee: runs MeleeAttack, procedural swing between pose markers, sphere-cast hit → damage, knockback, impact, sounds
│     ├─ Ammo/AmmoType.cs                   SO: a kind of round; guns of one type share a pool
│     ├─ Ammo/AmmoReserve.cs                the holder's spare rounds by type; Take / Add / Infinite, Changed
│     ├─ Ammo/AmmoConfig.cs                 SO: starting reserve + Infinite → CreateReserve
│     ├─ IWeaponUser.cs                     holder seam for any weapon: Eye, Root
│     ├─ IGunUser.cs                        IWeaponUser + Ammo, AimFrom (Player implements it)
│     ├─ GunTrigger.cs                      fire modes, fire rate, bursts; frame-rate independent
│     ├─ Gun.cs                             a held gun's rules: shoot, magazine, reload (per-round stopped by firing after the current round), jam + clear, pump cycle, aim; events (+ GunAction, GunInput)
│     ├─ GunRecoil.cs                       procedural recoil: spray pattern → aim kick (view) + small gun kick (model), spring return
│     ├─ BulletSpread.cs                    random direction inside a cone
│     ├─ GunAim.cs                          muzzle-to-crosshair direction (with the too-close fallback)
│     ├─ HeldGun.cs                         HeldItem for guns: runs Gun, spawns bullets, poses, recoil, optional GunAnimation (procedural fallback), pump / break parts, sounds (own AudioSource)
│     ├─ GunAnimation.cs                    optional clip view: shared Animator state slots + per-gun Override Controller, manual controller/Animation Rigging graph, action-progress sampling, pooled cosmetic magazine release; all five guns (including pump Cycling and per-shell reload loops)
│     ├─ Ballistics/BulletSystem.cs         root service (ITickable): bullets as data, ray-tested, damage, impacts, holes (+ BulletShot)
│     ├─ Effects/EffectPool.cs              root service (ITickable): one ObjectPool per effect prefab, timed returns, caps
│     ├─ Effects/PooledEffect.cs            root of a pooled effect prefab: lifetime, max alive, Restart
│     ├─ Effects/BulletEffects.cs           SO: muzzle flash, tracer, impact, bullet hole + hole settings
│     ├─ WeaponInstaller.cs                 registers EffectPool + BulletSystem (entry points) in the root scope
│     ├─ Prefabs/Guns/Gun_Base.prefab       HeldGun + 2D AudioSource skeleton: Poses (Hip, Aim, Sprint), PosePivot > RecoilPivot > Muzzle
│     ├─ Prefabs/Guns/AK47, Pistol, MP5, DoubleBarrel, PumpShotgun.prefab   variants of Gun_Base with the store model and their poses / muzzle
│     ├─ Prefabs/Guns/*_Pickup.prefab       world prefabs: WorldItem + trigger box + model lying flat
│     ├─ Prefabs/Effects/                   MuzzleFlash, Tracer, BulletImpact, BulletHole (PooledEffect roots)
│     ├─ Materials/BulletHoleDecal.mat, Textures/BulletHole.png   bullet hole decal
│     ├─ Prefabs/Melee/FireAxe.prefab       HeldMelee + 2D AudioSource: Poses (Idle, WindUp, FollowThrough, Sprint), PosePivot > Model
│     └─ Prefabs/Melee/FireAxe_Pickup.prefab  world prefab
├─ (Assets/_Samples/Audio/Weapons/)        gun sound samples from the old project (rifle, pistol, sniper bolt)
├─ Data/
│  ├─ Player/PlayerTuning.asset             the tuning values used in every scene
│  ├─ Equipment/EquipmentConfig.asset       3 slots "Weapon 1–3", each takes Gun | Melee, empty at spawn
│  ├─ Items/AK47, Pistol, MP5, DoubleBarrel, PumpShotgun, FireAxe.asset   item definitions (held + world prefab; weapons hold all their numbers)
│  ├─ Weapons/RifleAmmo, PistolAmmo, ShotgunShells.asset   ammo types
│  ├─ Weapons/AmmoConfig.asset              the player's starting reserve (90 rifle, 48 pistol, 24 shells), Infinite off
│  ├─ Weapons/BulletEffects.asset           effects shared by both guns
│  └─ UI/HealthBarStyle.asset, StaminaBarStyle.asset, HotbarSlotStyle.asset, InteractPromptStyle.asset
└─ Tests/EditMode/                          asmdef Aipocalypse.Tests.EditMode
   ├─ PlayerRulesTests.cs                   Stamina, Health, FallDamage, directional speed
   ├─ EquipmentTests.cs                     Hotbar, EquipmentSwitcher timing, Equipment select / cycle, pick up / swap / drop
   ├─ GunTests.cs                           fire modes and rate, magazine, reloads, jams, interrupts, shared ammo, recoil, spread
   ├─ GunAnimationTests.cs                  AK47 override/curve bindings, automatic Animator overwrite regression, controller restoration, hand contacts, magazine visibility, interrupt reset, pause/equip/fire, repair, scaled-hand grip alignment
   ├─ WeaponAnimationTests.cs               MP5/pistol/shotgun bindings, scaled hands, automatic Animator pass, contacts and continuity, slap/reset, repeated shell reload and pump cycle, break-action closure
   ├─ HeldItemClearanceTests.cs              obstacle retraction and recovery, stable pose, ignores self / triggers, never moves the player
   └─ MeleeTests.cs                         swing timing, hold rate, buffered click, alternating sides, switching / sprinting
```

## Who depends on whom

```text
ProjectLifetimeScope (root) ──registers──> InputService, ControlSettings, MessagePipe brokers, PlayerRegistry,
                                           EffectPool, BulletSystem (ticked by VContainer)
                                                │
PlayerScope : EntityScope ── parent: GameLifetimeScope if the scene has one, else root
   │ registers default IStatProvider → BaseStats (only if no ancestor provides one)
   └─ injects the Player GameObject (whole hierarchy)
         Player.Construct(InputService, IStatProvider, IPublisher<DamageDealt>, IPublisher<PlayerDied>, PlayerRegistry, IObjectResolver)
           creates (plain C#, owned by Player):
             Health, Stamina, PlayerMotor(CharacterController, tuning, stats, stamina),
             PlayerLook(body, cameraPitch), PlayerHeadBob(cameraRoot), PlayerInteractor(cameraPitch, gameObject),
             AmmoReserve(AmmoConfig.CreateReserve()),
             Equipment(EquipmentConfig.CreateHotbar()) → EquipmentRig.Bind(Equipment, owner: this, resolver)
               rig spawns held prefabs with resolver.Instantiate → HeldGun.Construct(BulletSystem, EffectPool), HeldMelee.Construct(EffectPool)
               HeldItem.Bind(item, owner) → HeldGun builds Gun(GunInstance, owner.Ammo) + GunRecoil; HeldMelee builds MeleeAttack
           last: PlayerRegistry.Register(this) → ActivePlayerChanged   (OnDestroy: Unregister)

HudScope : EntityScope (HUD.prefab root, separate from the Player) ── same parent lookup
   └─ injects the HUD GameObject
         PlayerHudView (PlayerRegistry, ISubscriber<ActivePlayerChanged>) → Bind / Unbind the active player
           PlayerVitalsHud, HotbarHud, InteractPromptHud (+ InputService for key labels)
```

- Only `Player` gets injected. The parts are plain classes it creates and owns. They get Unity components through their constructors, not through DI.
- `PlayerSystem` → `EquipmentSystem` ← `WeaponSystem`: the player owns an `Equipment`; weapon kinds derive from equipment's definitions. `EquipmentSystem` knows neither the player nor weapons: `WorldItem` finds the picker's hotbar through `IEquipmentOwner`, and a `HeldGun` finds its holder's ammo and aim through `IGunUser`, a `HeldMelee` its eye and root through `IWeaponUser` (Player implements all of them). The player also uses a few `WeaponSystem` types directly (`AmmoReserve`, `AimOrigin` in its tuning).
- Bullets hit anything with an `IDamageable` (`_Common/Combat`) and call `TakeDamage` with `DamageType.Gunshot`; the target publishes `DamageDealt` itself.
- Interaction: `_Common/Interaction.IInteractable` is the only contract. `PlayerInteractor` finds one with a look cast; the interactable decides what happens (`WorldItem` → `Equipment.TryPickUp`).
- Inside the player system, plain C# events are used (`PlayerMotor.Landed` / `Jumped`). Anything crossing systems goes through MessagePipe (`DamageDealt`, `PlayerDied`).

## Frame order (Player.Update)

```text
intent = dead or gameplay input disabled ? default : read InputService
PlayerLook.Tick      yaw + pitch                    (movement uses the new facing this frame)
PlayerMotor.Tick     velocity, jump, CharacterController.Move, landing → Landed event
                       └─ Player.OnLanded: head-bob dip + fall damage → Health → DamageDealt / PlayerDied
Stamina.Tick         regen (spending happened inside the motor)
PlayerHeadBob.Tick   camera root offset
PlayerInteractor     look cast → focus + prompt; intent.Interact → IInteractable.Interact (pickup / swap)
Equipment            intent.Drop → DropSelected → WorldItem.Spawn at the drop point; else SelectSlot / CycleSlot → Select / Cycle; EquipmentSwitcher.Tick (holster → swap → equip)
                       └─ HeldChanged → EquipmentRig shows/hides models; SelectionChanged → HotbarHud highlight
EquipmentRig.Tick    restore hand pose from Raise before measuring obstacles
EquipmentRig.TickHeld  the held item: HeldGun → Gun.Tick (reload / jam clear / pump, aim, trigger → Fired counts shots); pose + recoil pivots, pump / break parts,
                       ItemHands.SetProgress (procedural hand action parts: mag, bolt, shells), or GunAnimation samples the controller and constraints for all five guns.
                       HeldMelee → MeleeAttack.Tick (Struck marks a hit to check); swing pose
PlayerLook.SetKick   held item's ViewKick on the camera pivot; PlayerMotor.SpeedMultiplier from IsAiming
HeldItemClearance    eye → normal model tip cast; blend an obstacle offset / tilt onto the hand (melee swings opt out)
HeldItem.LateTick    model and camera are final: HeldGun spawns this frame's shots (muzzle flash, BulletSystem.Fire per pellet)
                       from the muzzle as drawn, then GunRecoil.Kick (shows from next frame).
                       HeldMelee sphere-casts from the camera → damage, knockback, impact effect, hit sound
FirstPersonHands.Tick  each hand onto the held item's current grip, turned towards its implied elbow (arc + finger opening on a grip change); hidden with no grip
VContainer ticks     BulletSystem moves and ray-tests bullets → damage, impacts, holes; EffectPool returns expired effects
LateUpdate           CinemachineBrain moves PlayerCamera to CameraPitch
                     PlayerVitalsHud reads Health / Stamina → StatBar.SetValue (skips unchanged values)
                     InteractPromptHud reads Player.Interactor → InteractPromptView
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
├─ PlayerCamera              Camera (tag MainCamera, excludes HeldWeapon) · URP camera data · CinemachineBrain · AudioListener · WeaponOverlayCamera
│  └─ WeaponCamera           URP Overlay (Clear Depth), HeldWeapon only, same FOV, near 0.01 m
└─ PlayerCinemachineCamera   CinemachineCamera (FOV 70) · HardLockToTarget · RotateWithFollowTarget
```

The HUD is not in this prefab: `HUD.prefab` sits next to it in the scene ([UI.md → Where the HUD lives](UI.md#where-the-hud-lives)).

## Adding a script

- **New system:** folder under `_Core/` (or `Features/` / `_Experimental/`), namespace `Aipocalypse.Core.<System>`, one `<System>Installer : IInstaller` registered from the right scope. Add it to the map above.
- **Rules go in plain C#** (testable, EditMode tests in `Tests/EditMode/`). MonoBehaviours are views: they wire components and forward to the rules.
- **Numbers go in a ScriptableObject** type next to the system, with the asset in `Data/<System>/`.
- **New `*LifetimeScope.cs`:** `DisableScriptModifier` is on, but check that VContainer didn't replace the file with its template.
- **Generated code** (`GameInputActions.cs`): change the `.inputactions` asset, never the file.
