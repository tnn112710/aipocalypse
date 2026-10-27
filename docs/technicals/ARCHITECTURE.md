# Architecture

DI with **VContainer** (no gameplay singletons), events with **MessagePipe**, async with **UniTask**, input through one **InputService**, camera through **Cinemachine**.

The design is still moving, so the architecture is built to absorb change: a small stable **Core**, swappable **Features** on top, everything tunable in data, and events as the joints between them.

This file describes what exists. Each section keeps its rules for the systems still to come; when a system lands, fill in its part of the section. Script-by-script map: [CODE_STRUCTURE.md](CODE_STRUCTURE.md).

## Core vs Features

| Core — build now, rarely changes | Features — *provisional*, built when the design settles |
| --- | --- |
| Movement, look, stamina, fall damage, `Health` | *None yet* |
| Stat reads (`IStatProvider`) | |

- **Features → Core only.** Core never references a feature. Enforced by assemblies (see Assemblies).
- **One feature = one folder + one installer.** Cutting a feature = removing its installer; Core still compiles and runs.
- Core exposes **seams** with plain defaults; features plug better implementations in. Today: the player reads speeds, costs and max meters through `IStatProvider` (default `BaseStats` returns the tuning value); a perks/XP feature will replace it.
- A GDD change should only touch Features. If it forces a Core change, that's a signal to add a seam.

## Layers

```text
VIEWS     MonoBehaviours (Player). Physics, camera, input -> intents. [SerializeField] for prefab parts, [Inject] for services.
RULES     Plain C# that owns the rules (PlayerMotor, Stamina, Health, InputService). Services registered in VContainer
          publish gameplay events and never reference MonoBehaviours directly — use an interface.
STATE     Plain [Serializable] classes, no Unity refs (meters today: Health, Stamina).
DATA      ScriptableObjects: defs, tuning, tables (PlayerTuning and its groups). Read-only at runtime.
```

Dependencies point down only.

## Gameplay events

Core **announces** what happens; features **react**. New rules are new subscribers, not edits to existing code.

- Events are small `readonly struct`s published through MessagePipe (`IPublisher<T>` / `ISubscriber<T>`, injected). Brokers are registered once in the root scope.
- Core events live in `_Core/_Common/Events`: `DamageDealt`, `PlayerDied`, `PlayerDeathSequenceEnded`, `PlayerRespawnFading` and `PlayerRespawning` (async), `RespawnStageChanged`, `PlayerRevived`, QTE lifecycle (`QteStarted`, `QteAttempted`, `QteEnded`), gun jam / repair beats, world time (`HourChanged`, `DayPhaseChanged`, `DayStarted`, `TimeJumped`, `TimeSkipEnded`). Features may define their own.
- **Damage goes through `DamageService.Deal`** (root scope): it applies `IDamageable.TakeDamage` (which returns a `DamageResult`: applied amount, killed) and publishes `DamageDealt` with the hit's `Source`. Physics hits (bullets, swings) use `DamageService.DealHit(hit, info)`, which finds the collider's `IDamageable`, deals the damage and pushes a loose rigidbody by `info.Force`. Weapons, falls and future traps never call `TakeDamage` directly, so hit feedback, kill credit and damage rules hook in at one place.
- **Sound goes through `AudioService`** (root scope), a thin wrapper over BroAudio: game code plays `SoundID`s and never calls BroAudio's static facade.
- Events are for things that *happen* (a hit, a death), never per-frame data (positions, timers).
- Inside one system, plain C# events are fine (`PlayerMotor.Landed`). Across systems, use MessagePipe.

## Composition and data

**Entities are built from parts, not class hierarchies.** The player is `PlayerLife` + `PlayerMotor` + `PlayerLook` + `PlayerCameraRig` + `Stamina` + `PlayerHeadBob` + `HeldItemDriver` and a few more, plain C# parts driven by one view (`Player`). Future entities follow the same pattern.

**Every number lives in data.**

- Settings shared by a whole system (one copy each) are ScriptableObjects under `Assets/_Game/Data/<System>/` (today: `Player/PlayerTuning.asset`, which links one asset per group: movement, vitals, camera, interaction, held item, sounds, vision). Data that belongs to one thing (a gun's `GunDefinition`, an item, an enemy's stats) lives with that thing in `Assets/_Game/Content/<Kind>/<Name>/` (see Folder layout). SO edits made in Play mode persist, so tune by feel while playing.
- Rules that are really tables are data tables, not `if` chains.

## Scopes and app flow

```text
Root (VContainerSettings → ProjectLifetimeScope prefab, auto-created before any scene)
  SettingsService · InputService · SceneLoader · SessionRequest · EntityRegistry · MessagePipe brokers · LoadingScreen
  (SaveService later — see Features)

MainMenu scene (first in Build Settings, no Boot scene)
  MainMenuLifetimeScope: New Game | Continue | Settings | Exit

World scene (one persistent map, no mission/night scopes)
  GameLifetimeScope: installers for Core + enabled Features; GameStarter (entry point) reads SessionRequest → builds State → starts the clock
  Quit to menu → load MainMenu (scene unload disposes all game services)

Any scene (World, Proto_*, test arenas)
  Entity prefabs carry their own EntityScope — see Entities
```

- **Play from any scene:** the root scope always exists. With no `SessionRequest` (Play pressed in World), `GameStarter` uses a `DevStartConfig` asset (editor only): start day/time, inventory, stats, **which features and experiments are on**. In builds an empty request returns to the menu.
- Scenes load with `SceneManager.LoadSceneAsync` + UniTask; no Addressables for now.
- **Built so far:** the root (`VContainerSettings` + `ProjectLifetimeScope` prefab) with MessagePipe, `InputService`, `AudioService`, `DamageService`, `TimeService`, `GraphicsService`, `QuickTimeService`, `PlayerRegistry`, `PlayerRespawnFlow`, `BulletSystem`, `EffectPool` and `GunJamFactory` (registered by `WeaponInstaller`), `ScreenLog` and, in dev builds, the dev console (`ConsoleInstaller`); `GameLifetimeScope` with the strict flag, installing the `SceneInstaller` components on its GameObject: `WorldTimeInstaller` (world clock), `EnvironmentInstaller` (`EnvironmentDirector` + ambience) and `EnemyInstaller`. A scene gets a system by adding its installer component. The clock starts at `ClockSettings.StartHour` until `GameStarter` exists. Menu, World, `GameStarter` and the other root services are not built yet.

## Systems

**Core** (`Assets/_Game/_Core/`)

| System | Owns |
| --- | --- |
| _Bootstrap | Root `ProjectLifetimeScope` (+ prefab, `ControlSettings`), `VContainerSettings` asset, `GameLifetimeScope` and `SceneInstaller` (base of the scene systems' installer components) |
| PlayerSystem | Controller, look, movement, stamina, life and fall damage, head bob, the camera rig, the item in hand (`HeldItemDriver`), pose sequences; owns the player's `Equipment`; `IPlayer` for outside systems; root `PlayerRegistry` and `PlayerRespawnFlow`. See Entities → Player |
| EquipmentSystem | Items (`ItemDefinition` → `EquippableDefinition`, `ItemInstance`), `Hotbar` (≤ 9 category-checked slots), `EquipmentSwitcher` (timed holster → equip), `Equipment` (selection, pick up / swap / drop), first-person `EquipmentRig` / `HeldItem`, `WorldItem` pickups. Rules and how-tos: [EQUIPMENT.md](EQUIPMENT.md) |
| WeaponSystem | Weapon item kinds (`GunDefinition`, `MeleeDefinition`), guns (`Gun` rules, `HeldGun` view, `GunRecoil`, `GunAim` shot rules, jams and their repair in `GunJam`, ammo by type in `AmmoReserve`), melee (`MeleeAttack` rules, `HeldMelee` view), and root services: `BulletSystem` (bullets as data) and `EffectPool` (pooled muzzle flashes, tracers, impacts, holes), both ticked by VContainer, and `GunJamFactory`. Rules and how-tos: [WEAPONS.md](WEAPONS.md) |
| HandSystem | First-person floating hands: `HandPose` (finger shapes), `HandRig` (a hand prefab's grip point, finger chains and calibration), `HandSet` (the left / right hand prefabs and implied elbows), `HandGrip` and `ItemHands` (where an item is held, per hand), `FirstPersonHands` (on the player: puts each hand on the held item's grip); editor-only `HandFit` (bake fit) and `HandPreview` (edit-mode ghost hands). Rules and how-tos: [HANDS.md](HANDS.md) |
| EnemySystem | Scene system (`EnemyInstaller`): `EnemyRegistry`, `EnemyManager` (time-sliced Behavior Designer ticks), `EnemyBrain` + `EnemyMover` per enemy, `EnemyDefinition` per enemy kind. See AI |
| InputSystem | `InputService` (root scope) over `InputSystem_Actions` through its generated wrapper `GameInputActions`: owns enabling, map switching and cursor lock; consumers only read. Menus take the UI map with owner-keyed `RequestUI` / `ReleaseUI`; gameplay input is on only while nobody holds a request. Text input (`RequestTextInput`, the dev console) beats both: only the `Console` map and typed characters are live. A reader that ticks first can `Consume` a `GameButton` press so later readers don't see it (the QTE's confirm). Mouse look is per-frame delta, stick look is scaled by deltaTime. Namespace `Aipocalypse.Input` — a namespace ending in `InputSystem` shadows `UnityEngine.InputSystem.InputSystem` |
| _Common | Entities (`EntityScope`, `IPlayerTarget`), Combat (`IDamageable`, `DamageInfo` / `DamageResult`, `DamageService`, `Health`), Events, Feedback (`FeedbackContext`, `MMF_HitStop`: Feel feedbacks that reach our services), Stats (`IStatProvider`), Interaction (`IInteractable`), Utils (`Damping` frame-rate independent smoothing, `PhysicsQueries.Nearest` for NonAlloc hit buffers, `TransformExtensions`) |
| _Debug | Dev console (`ConsoleInstaller`, root: `ConsoleService`, `CommandRegistry` + `ConsoleCommands` base, `LogBuffer`, `ScreenLog`, the `DebugConsole` overlay; editor and dev builds) and `TargetDummy`: shooting-range target that logs hits, refills its health, tips over from knockback and springs back. [DEBUG.md](DEBUG.md), console: [CONSOLE.md](CONSOLE.md) |
| AudioSystem | `AudioService` (root): plays BroAudio `SoundID`s 2D or at a point. Libraries in `Data/Audio/` |
| TimeSystem | `TimeService` (root): sole writer of `Time.timeScale` and the fixed step. Owner-keyed slow-motion requests (slowest wins), owner-keyed pauses (`RequestPause` / `ReleasePause`; `IsPaused` while any owner holds one, beating every request) and real-time hit stops; restores Unity's values on dispose. Every pause or slow-motion caller goes through it |
| QuickTimeSystem | `QuickTimeService` (root): one owner-bound QTE session at a time (`QteChallenge` data → `IQteSession`), ticked in real time while gameplay input is on and the game isn't paused; reads and consumes the challenge's `Button`; announces `QteStarted` / `QteAttempted` / `QteEnded`. Knows no weapons: a gun's jam repair is one user ([WEAPONS.md → Gun jam clearing and QTE](WEAPONS.md#gun-jam-clearing-and-qte)) and `QteHud` shows any session |
| GraphicsSystem | `GraphicsService` (root): sole runtime writer of the URP asset's shadow distance: the player's `ShadowDistance` plus owner-keyed shorter requests (the environment's night distance); the asset's value comes back on dispose. Also the player's graphics toggles that are global shader keywords: `StarTwinkle` (`STAR_TWINKLE`, on by default) |
| WorldTimeSystem | The in-game clock (`WorldClock`, scene scope): day, hour, Dawn / Morning / Afternoon / Dusk / Evening / Midnight phases, master and per-phase speed, jumps, skips, slow / freeze requests, moon phase; publishes hour, phase, day, jump and skip events; N / M cheats in dev builds. Separate from `TimeService`, which owns Unity's `Time.timeScale`. [DAYNIGHT.md](DAYNIGHT.md) |
| EnvironmentSystem | Scene-level environment with no gameplay rules: the time-of-day look (`EnvironmentDirector`, sole writer of the sun / moon light, ambient, fog, reflections, sky globals and time-of-day grade volumes; data in `TimeOfDayProfile` + `LightingPreset`; weather later), reactors (lamps, windows, particles, material tints), `AmbienceService`; shader globals helpers `BKFoliageGlobals`, `ASPTopLayerGlobals`. Day/night: [DAYNIGHT.md](DAYNIGHT.md); environment content and its generator: [ENVIRONMENT.md](ENVIRONMENT.md) |
| UI | Standalone HUD canvas with its own `HudScope` (placed in the scene; player views follow `PlayerRegistry`), passive widgets with style assets (`StatBar` + `StatBarStyle`, `HotbarSlotView` + `HotbarSlotStyle`, `ClockView` + `ClockStyle`, `TimingQteView` + `QteStyle`, `DeathScreenView` + `DeathScreenStyle`; crosshair and hit-feedback styles), presenters that feed them (`PlayerVitalsHud`, `HotbarHud`, `CrosshairHud`, `InteractPromptHud`, `HitFeedbackHud`, `QteHud`, `ClockHud`, `DeathScreenHud`, `HudVisibilityHud`). Rules and how-tos: [UI.md](UI.md) |

**Features** (`Assets/_Game/Features/`): *none yet.* Each system or feature gets a row here when it lands.

## Holding off on expensive commitments

- **Save:** not until the core loop is fun — save formats lock in data shapes. When added: versioned JSON, no old-save compatibility promise during development.
- **Stats/modifiers:** minimal version first (today: `IStatProvider` with a pass-through default); extend only when a designed feature needs it.
- **Interfaces:** only at real seams (Core ↔ Feature, entity ↔ outside world).

## Experiments

*None yet.* Trial mechanics live in `Assets/_Game/_Experimental/<Name>/`, each with its own installer. Kept → move to `Features/`. Cut → delete the folder. Mirrors the `IDEAS.md` → `CORE.md` flow.

## Debug tools

Editor and development builds only, details in [DEBUG.md](DEBUG.md) and, for the console, [CONSOLE.md](CONSOLE.md):

- **Dev console** (backquote key, `ConsoleService`, root): Unreal-style drop-down for commands and cheats; Unity logs show in it (and on screen) only after `log.screen.*`. Each system owns its commands in a `<System>Commands : ConsoleCommands` class with `[Command]` members, registered as an entry point by its installer when `Debug.isDebugBuild`; it adds itself to the root `CommandRegistry` while its scope lives, so scene systems' commands exist only in their scenes. Commands go through the system's services and owner-keyed requests, never around them.
- **`ScreenLog`** (root, always registered, no-op in release builds): on-screen messages, keyed lines, mirrored Unity errors.
- **Hotkeys:** the `Debug` input map (N / M: next hour / phase, `WorldClockCheats`; K / L: kill / revive the player, `PlayerCheats`), off while the console has text input. Odin buttons on views (e.g. `Player.Revive`).

## Entities: self-contained prefabs

Any entity prefab works when dropped into any scene — World, a prototype, an empty test scene.

| Kind | Examples | How it gets services |
| --- | --- | --- |
| **Unique entity** — one or a few | Player | Own `EntityScope` (a child container) |
| **Crowd entity** — many, pooled | `WanderCapsule` enemies (pooling not built yet) | Injected once from the scene scope, no container of its own (a scope per instance doesn't scale): scene-placed ones by `EnemyInstaller`'s build callback; code-spawned ones will go through the scene resolver (`resolver.Instantiate`) once spawning is built |
| Plain object | *Not built yet* | No injection; data via `[SerializeField]` / the spawner |

```text
Unique entity root: PlayerScope : EntityScope
  ├─ own components, wired with [SerializeField]  (camera rig, CinemachineBrain, AudioListener live inside the Player)
  ├─ data: PlayerTuning (ScriptableObject → one per group)
  └─ needs from outside = small interfaces (IStatProvider…)

Parent lookup:  scene's GameLifetimeScope  →  if none: root ProjectLifetimeScope
Each need:      parent chain has it?  yes → use it   no → entity's default (BaseStats…) + one warning
```

How `EntityScope` does it (VContainer API checked in package source):

- **Parent:** overrides `FindParent()` to return the scene's `GameLifetimeScope` (or `null` → VContainer falls back to the root).
- **Defaults:** `RegisterDefault` registers a default only if `Parent.Container.TryGetRegistration(type, …)` fails (it searches the whole parent chain). A child registration would otherwise override the real service. Defaults live in the entity's system (`PlayerSystem/Defaults/`).
- **Injection:** `RegisterBuildCallback(r => r.InjectGameObject(gameObject))` injects the entity's own hierarchy. Never `RegisterComponentInHierarchy` for entity parts — it searches the whole scene and grabs another instance's component.
- **Order:** `GameLifetimeScope` gets `[DefaultExecutionOrder(-5000)]`. Without it, an entity scope whose `Awake` runs first would build against a parent with no container yet (NullReferenceException; VContainer only defers for `parentReference`, not `FindParent`). `EntityScope` itself is `-4000`, so it injects before its own components' `Awake`.
- **Strict mode:** `GameLifetimeScope.Strict` (to be on in World) turns "default used" into an error, so a missing installer in the real game can't hide behind a default. A strict scope also reports each `SceneInstaller` type it lacks, and an installer that can't install (its `Problem`) as an error instead of a warning.

**Finding each other:** `PlayerRegistry` (root scope) holds the active player as an `IPlayer`: players register themselves and it publishes `ActivePlayerChanged`. The HUD, the respawn flow and the cheats use it today; enemies and companions will too, through the narrower `IPlayerTarget` (transform + `IDamageable`). Other entities: *not built yet* (planned: an `EntityRegistry` in the root scope, which can absorb `PlayerRegistry`). No `FindObjectOfType`, no statics.

### Player

`_Core/PlayerSystem/Prefabs/Player.prefab` works dropped into any scene; scenes need no camera of their own. Hierarchy: [CODE_STRUCTURE.md → Player prefab](CODE_STRUCTURE.md#player-prefab).

- `Player` and the debug `TargetDummy` are the only gameplay MonoBehaviours with an `Update` (HUD views read the player in `LateUpdate`). Each frame it builds a `PlayerIntent` from `InputService`, then ticks plain C# parts in a fixed order: `PlayerLook` → `PlayerMotor` → `Stamina` → `Fatigue` + `PlayerBreathing` → `PlayerHeadBob` → `PlayerInteractor` → `HeldItemDriver` (equipment, the held item, zoom, sway, hands) → pose sequence → `PlayerVision`. `PlayerCameraRig` is the only writer of the camera transforms: look, head motion and pose sequences hand it their share. Tests or cutscenes can drive the motor with an intent directly.
- `PlayerMotor`: walk/sprint (sprint forward only; backpedal and strafe slower), jump with coyote time and buffering, momentum kept in the air, sliding on slopes past the controller's slope limit, landing tracking (drop height, impact speed). Speeds and costs are read through `IStatProvider`.
- `Stamina`: walking drains slowly, sprint drains faster, jump costs a chunk, shots and swings cost per weapon; every spend pauses regen, and regen runs only while the player stands idle (`PlayerMotor.IsIdle`: no move input, stable ground, nearly still), after a delay; at 0 the player is exhausted (no sprint or jump) until it refills to a fraction. `PlayerLife` owns `Health` and fall damage (dealt through `DamageService`), linear between a safe and a lethal drop height; the player is hurt only through `DamageService` (`IDamageable` is implemented explicitly). Dying publishes `PlayerDied`. Max health and stamina are read again when `IStatProvider.Changed` fires. `Fatigue`: under 50 / 25 / 10% stamina, eased multipliers raise gun recoil and lower melee damage, knockback and move speed; firing is never blocked ([WEAPONS.md → Fatigue](WEAPONS.md#fatigue)).
- Feel: `PlayerFeedback` plays footsteps at the bottom of each head-bob step (`PlayerHeadBob.Stepped`), plus jump and landing sounds; `PlayerBreathSound` breathes heavily when out of breath (`PlayerSoundTuning`). `PlayerZoom` owns the lens: the player's `ControlSettings.FieldOfView`, narrowed by the held item's `Zoom` when aiming down sights, with look speed scaled to match; the held model keeps its own field of view.
- Equipment: keys 1–9 select hotbar slots, the mouse wheel cycles; switching holsters the held item, then raises the new one over per-item times scaled by `EquipmentConfig.SwitchSpeed`. Details: [EQUIPMENT.md](EQUIPMENT.md).
- First-person presentation: `FirstPersonRenderFeature` draws the HeldWeapon layer inside the world camera with a cleared depth and its own lens (`FirstPersonView`), so held models stay visible and unzoomed. `HeldItemClearance` blends the hand back and tilts it near obstacles without moving the player; melee swings opt out.
- Pose sequences: a `PoseSequence` asset scripts a first-person moment as beats (eye height, offset, pitch / yaw / roll, field of view, ease, hold, sounds), then a settled pose with music. A `Continuous` sequence flows through its beats on one smooth path and never stops (holds only add time); a hand-held wander and a slow elliptical drift (from the last beat on) keep the frame moving, like phone footage. `Player.PlaySequence(sequence)` hands it the camera and body (no movement, no item use, the held item holstered via `Equipment.LowerHands` if the sequence asks) until `StopSequence`. Beats are placed around the feet on the ground (short of walls). Dying plays `PlayerTuning.DeathSequence` (`Data/Player/Sequences/PlayerDeath.asset`, continuous: groan on the hit, kneel and sweep the view across the grass, topple onto the side with a thud, roll onto the back and look up at the landscape with music); `EndAfter` seconds after it settles, `PlayerDeathSequenceEnded` is published (the hook for a fade / respawn). Then the root `PlayerRespawnFlow` runs the respawn in stages (`RespawnStageChanged`): Fading awaits every `PlayerRespawnFading` subscriber (the HUD's death screen closes the eyes and shows "You're dead" with "Loading..."); it takes the UI input; Preparing moves the body back to `Player.SpawnPosition` (where it entered the scene) and awaits every `PlayerRespawning` subscriber (`IAsyncPublisher`: systems that reset for a respawn finish there); Ready waits for a click, then revives ([UI.md → Death screen](UI.md#death-screen)). It works without a HUD, and any revive (the L cheat) ends it. `Revive` stops the sequence (and its music) and publishes `PlayerRevived`. Benches, chairs and resting moments are planned as more sequences on the same player (a seat needs beats placed in an anchor's space, which was removed while unused: see git history).
- All numbers live in the `Data/Player/` tuning assets, linked from `PlayerTuning.asset` (equipment: `Data/Equipment/`; each item's numbers: its `Content/<Kind>/<Item>/<Item>.asset`).

## AI

The first AI slice was a capsule wander demo (`Content/Enemies/WanderCapsule/WanderCapsule.prefab`, kept as the bare enemy contract the Goblin builder starts from); `WorldMap.unity` now has ten animated goblins outside the home gate instead. Every AI agent decides with a Behavior Designer Pro tree; tasks delegate movement to `EnemyMover` (the enemy's legs over A* `FollowerEntity`). Each enemy kind has an `EnemyDefinition` in `Content/Enemies/<Name>/` (move speed, stop distance, its behaviour tuning); the capsules use `WanderCapsule/WanderCapsule.asset`. The shared `Content/Enemies/_Shared/Wander.asset` subtree repeats a sequence: choose a reachable point, walk there, pause; its tuning is the definition's `WanderSettings` (`Data/Enemies/WanderSettings.asset`).

A scene gets enemies by adding the `EnemyInstaller` component to its `GameLifetimeScope` (navigation and `EnemySystemSettings` assigned). Enemies are found through `EnemyRegistry` (scene scope), a dense list: each `EnemyBrain` is injected once (scene-placed ones by the installer's build callback; code spawning, not built yet, will instantiate through the scene's resolver), starts its tree on its first `Start` and joins the registry while enabled, so spawned and pooled enemies tick like placed ones. `EnemyManager` ticks the Manual trees round-robin on a time budget: each enemy thinks about every `ThinkInterval` (1/15 s) at any frame rate, at most `MaxThinksPerFrame` trees a frame, on scaled time (paused = no thinking). `_Core/EnemySystem/Prefabs/Navigation.prefab` holds AstarPath and RVOSimulator; its default graph is a small terrain-following grid scanned on startup (for small test scenes; `Proto_*` scenes are planned). WorldMap's instance holds one tiled recast graph over the whole map, loaded at startup from a cache the map generator bakes, with lakes, ponds and rivers cut out and streams wadeable ([ENVIRONMENT.md → Navigation](ENVIRONMENT.md#navigation)). While active, an enemy also bends grass through the scene's `GrassBendField` (optional; [ENVIRONMENT.md → Stylized Grass](ENVIRONMENT.md)). Spawn rules, pooling and enemy damage are not implemented yet. Skill: `aipocalypse-enemy-ai`.

**Melee.** The goblins run the shared `Content/Enemies/_Shared/Melee.asset` subtree: a selector whose first branch fights and whose fallback is the wander sequence. The fight branch is a sequence with a `Both` conditional abort: `PlayerInReach` (re-checked every think) notices the player within `DetectRadius` (5 m) and keeps them until past `LoseRadius` (8 m, so it doesn't flicker at the edge), which cuts into the wander or ends the fight; `ChaseTarget` runs at them (a `MoveSpeed` × `ChaseSpeedMultiplier` modifier, 1.4 → 3.5 m/s, removed when the chase ends) until within `AttackRange` (1.5 m); `MeleeAttack` stands, turns in place to face them (`EnemyMover.Face`: a destination on itself with a facing direction, which A\* turns toward) and plays a random swing (`EnemyAnimator.Attack`), then waits out `AttackCooldown` in a combat idle and swings again, or chases again if they stepped back. While a target is set, the model is in its fighting stance (`Combat` animator bool, set by `EnemyBrain.Target`); it relaxes back to the calm idle when the target is dropped. The player comes from the root `PlayerRegistry` (`EnemyBrain.Player`, optional, so enemies in a scene without a player just wander); the engaged one is `EnemyBrain.Target`. Swings deal no damage yet. Tuning: the kind's `MeleeSettings` (`EnemyDefinition.Melee`; empty = the kind ignores the player). The subtree is generated by `AgentScripts/Enemies/EnemyTrees.cs` (`EnemyTrees.Melee`), so edits made in the Behavior Designer window are lost on a rebuild: change the script, or stop rebuilding it.

**Animated enemies.** Ten Sidekick goblins (`Content/Enemies/Goblin/`, six looks) wander outside the home gate with a model and an Animator. The entity prefab nests a `Model` prefab (the vendor look, rebuilt or swapped freely); its `EnemyAnimator` writes a fixed parameter contract (`Speed` in m/s, `LookX`/`LookY`, `Lean`, `Fidget`/`FidgetIndex`) from the mover's velocity and turn rate, through the plain C# `LocomotionAnimation` rules (glances while standing, look and lean into turns, idle fidgets). `EnemyAnimationManager`, a scene `ITickable`, advances them all each frame on scaled time; no enemy has an `Update`. There is no root motion: the `FollowerEntity` moves the body and the walk clip's authored speed matches the definition's `MoveSpeed` stat. Goblins hold a sword (`Goblin_Sword.prefab`, nested on the right hand bone, no collider) closed in a right-hand grip layer, and swing it with full-body attack states. The tuning is `Data/Enemies/EnemyAnimationSettings.asset` (`EnemyDefinition.Animation`). Controllers are generated from a text spec, so any vendor's clips fit the same contract. Models cull their Animator completely off screen (safe without root motion) and draw Mesh LODs (Sidekick: up to LOD2, picked by screen size, no bias, so close goblins keep full detail); 80 goblins on screen cost ~2.3 ms main thread and ~1.6 ms GPU in the editor (numbers in the skill). Skill: `aipocalypse-characters` (vendor side: `tools-unity-synty-characters`).

**Enemy stats, damage and death.** Each `EnemyDefinition` holds base stats in an enum-keyed table (`EnemyStat`: `MaxHealth` 100 for every kind today, `MoveSpeed`); every activation builds a fresh `EnemyStats` (base + modifiers by source, `Changed` re-applies speed and max health), the seam for slows, buffs and elite tiers. `EnemyBody` (on every enemy root) is the `IDamageable` that bullets and swings reach through `DamageService.DealHit`, so hit markers, damage numbers and kill confirms work unchanged. Hits on a collider with a `Hitbox` are scaled there (`DamageService.DealHit` → `DamageInfo.OnHitbox`): every goblin's head bone carries a ×2 critical one, shown as a red number; the root capsule stops at the hips and a sphere on the upper chest covers the hunched torso, so the head isn't inside another collider. A hit pushes the body by `DamageInfo.Force` (`Knockback`: speed per N·s, exponential drag, a cap) through `EnemyMover.Push` (`FollowerEntity.Move`, which keeps it on the navmesh) and plays a directional hit reaction (light, or heavy past a push speed) on the model's upper body. At 0 HP the colliders go off, `EnemyBrain.Die` stops the tree and the mover, a death clip plays (even off screen), and after `CorpseTime` the visual sinks and the enemy disables itself; enabling it again revives it at full health, the hook for pooling and respawns. `EnemyBodyManager` (a scene `ITickable`) advances only bodies that are sliding or dead. Tuning: `Data/Enemies/EnemyReactionSettings.asset` (`EnemyDefinition.Reaction`). Enemies publish no enemy events: kills are `DamageDealt` with `Result.Killed`.

## Performance

Standards that apply to all gameplay code now:

- **Zero allocations per frame:** no LINQ, no `new` in hot paths, no boxing, no string building in `Update`. Event payloads are structs.
- **DI is setup-time only:** resolve and inject at creation, never per frame.
- **One update per entity, explicit order:** a view ticks its plain C# parts (as `Player` does) instead of each part having its own `Update`. Many-instance things get one central tick (VContainer `ITickable`), not an `Update()` each.
- **Pool everything that spawns repeatedly** (`UnityEngine.Pool`) and prewarm pools before they're needed.
- Measure before optimising (Profiler, `ProfilerMarker`s); don't assume.

Per-system budgets (enemy counts, stress scenes) get added here with those systems.

## Prototypes → main game

Mechanics are built and tested in small scenes under `Assets/_Game/Scenes/Prototypes/` (not in Build Settings). Today the mechanics are tested together in `Assets/_Game/Scenes/Prototypes/WorldMap.unity`.

1. **Only the scene is a prototype, never the code.** Mechanic code goes in its real folder (Core, Feature or Experimental) from day one. No `Proto*` copies of classes or prefabs.
2. **A prototype scene needs no scope of its own.** Drop in the entity prefabs (and `HUD.prefab` if you want the HUD); they run on root services + their defaults.
3. **Real systems via installers.** When a prototype needs a real system, add a `GameLifetimeScope` with just those `SceneInstaller` components. World uses the same class with all of them, strict.
4. **Defaults belong to the entity's system** (e.g. `PlayerSystem/Defaults/BaseStats`) — they are part of the entity's contract.
5. **Tuning lives in ScriptableObjects** under `Assets/_Game/Data/`, shared by every scene.
6. **Smoke test** (PlayMode): `Tests/PlayMode/WorldMapSmokeTests.cs` loads `WorldMap.unity`, checks injection and enemy registration, picks up a gun, fires and reloads; any logged error fails it.

A prototype is done when it works in its scene **and** in World with no code changes.

## Folder layout

What exists, plus where new things go. Script by script: [CODE_STRUCTURE.md](CODE_STRUCTURE.md).

**Code and content are kept apart.** Code lives in one folder per system (`_Core`, `Features`, `_Experimental`), together with the base prefabs that are part of a system's contract (`Player.prefab`, `Gun_Base.prefab`, `HUD.prefab`, `Navigation.prefab`). Content lives in one folder per thing: adding a weapon, enemy or animal adds one folder, and cutting it deletes that folder.

```text
Assets/_Game/
  _Core/                  CODE. asmdef Aipocalypse.Core
    _Bootstrap/           ProjectLifetimeScope (+ prefab), VContainerSettings asset (preloaded), GameLifetimeScope, SceneInstaller
    _Common/              Entities, Combat, Events, Feedback, Stats, Interaction, Utils
    PlayerSystem/  InputSystem/  EquipmentSystem/  WeaponSystem/  HandSystem/  EnemySystem/  TimeSystem/  QuickTimeSystem/  GraphicsSystem/  UI/ …
    <NewSystem>/          one folder per Core system, with its installer (+ Prefabs/ for its base prefabs)
  Features/               (when the first feature lands) asmdef Aipocalypse.Features → references Core
  _Experimental/          (when the first experiment lands) asmdef Aipocalypse.Experimental → references Core + Features
  Content/                CONTENT, one folder per thing with everything it needs (prefab, definition, animations, materials…)
    Weapons/<Name>/       e.g. AK47/: AK47.prefab, AK47.asset, AK47_Repair(Qte).asset, Animations/
    Weapons/_Shared/      what several weapons use: base animations, effects, ammo types, materials
    Items/<Name>/         non-weapon items (Lantern/)
    Enemies/  Animals/  Companions/   <Name>/ per kind (Enemies/WanderCapsule/), _Shared/ for what several use (Enemies/_Shared/Wander.asset)
  Environment/            shared environment kit every map can use: Water/, TerrainLayers/, Vegetation/, Rocks/, Mountains/
  Maps/<Map>/             one folder per map: what only that map uses (Terrain/, Heightmaps/, water meshes, Navigation/…), and the
                          scene (<Map>.unity) once the map is official
    WorldMap/             the prototype world map's data; its scene is Scenes/Prototypes/WorldMap.unity (ENVIRONMENT.md)
  Data/                   settings shared by a whole system, one folder per system (one copy each: PlayerTuning, EquipmentConfig,
                          AmmoConfig, ClockSettings, EnemySystemSettings, UI styles, Audio libraries, HandSet + poses, Environment/TimeOfDay)
  Scenes/Prototypes/      WorldMap.unity (data in Maps/WorldMap/), Proto_* scenes (scene-only assets in a folder named after the scene)
  Settings/               URP assets, Input System actions
  Tests/EditMode/  Tests/PlayMode/
```

- **Content or Data?** It belongs to one weapon, item, enemy or animal → `Content/<Kind>/<Name>/`. One copy shared by the whole system → `Data/<System>/`. Used by several things of one kind → `Content/<Kind>/_Shared/`.
- **Map or Environment?** Only one map uses it (its terrain, heightmaps, water meshes) → `Maps/<Map>/`. Another map could use it (a water material, a terrain layer, a vegetation copy) → `Environment/`.
- **Names** keep the thing's name (`AK47_Repair`, not `Repair`): the object picker shows only file names. Map assets get no map prefix (the folder says which map); first-party copies of vendor assets keep the source pack's prefix (`BK_`, `HL_`, `KN_`, `AM_`, `GL_`).
- **Making a prototype map official:** rename its `Maps/<Map>/` folder and scene, update its generator's path constants, and add the scene to Build Settings.

## Assemblies

- `Aipocalypse.Core`, `Aipocalypse.Tests.EditMode` and `Aipocalypse.Tests.PlayMode` exist. `Aipocalypse.Features` / `Aipocalypse.Experimental` get created with their first code; the asmdef references enforce **Features → Core** at compile time.
- **Don't enable VContainer's source generator** for now: it has an open issue with injectors across multiple assemblies (hadashiA/VContainer#822), and reflection-based injection is fine on PC. Revisit only if profiling shows injection cost.
- Rules are plain C# and get EditMode tests (player rules, combat, equipment, guns and their clips, melee, hands, the repair QTE and time requests, pose sequences, the world clock, time of day and phase data; suites in [CODE_STRUCTURE.md](CODE_STRUCTURE.md)).
