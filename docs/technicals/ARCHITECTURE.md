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
- Core events live in `_Core/_Common/Events`: `DamageDealt`, `PlayerDied`, gun jam / repair beats, world time (`HourChanged`, `DayPhaseChanged`, `DayStarted`, `TimeJumped`, `TimeSkipEnded`). Features may define their own.
- **Damage goes through `DamageService.Deal`** (root scope): it applies `IDamageable.TakeDamage` (which returns a `DamageResult`: applied amount, killed) and publishes `DamageDealt` with the hit's `Source`. Weapons, falls and future traps never call `TakeDamage` directly, so hit feedback, kill credit and damage rules hook in at one place.
- **Sound goes through `AudioService`** (root scope), a thin wrapper over BroAudio: game code plays `SoundID`s and never calls BroAudio's static facade.
- Events are for things that *happen* (a hit, a death), never per-frame data (positions, timers).
- Inside one system, plain C# events are fine (`PlayerMotor.Landed`). Across systems, use MessagePipe.

## Composition and data

**Entities are built from parts, not class hierarchies.** The player is `PlayerMotor` + `PlayerLook` + `Stamina` + `Health` + `PlayerHeadBob`, plain C# parts driven by one view (`Player`). Future entities follow the same pattern.

**Every number lives in data.**

- ScriptableObjects under `Assets/_Game/Data/<System>/` (today: `Player/PlayerTuning.asset`, which links one asset per group: movement, vitals, camera, interaction, held item, sounds). SO edits made in Play mode persist, so tune by feel while playing.
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
- **Built so far:** the root (`VContainerSettings` + `ProjectLifetimeScope` prefab) with MessagePipe, `InputService`, `PlayerRegistry`, `BulletSystem` and `EffectPool`; `GameLifetimeScope` with the strict flag and optional installers for enemies, the world clock (`ClockSettings` assigned) and the environment (`EnvironmentDirector` + ambience). The clock starts at `ClockSettings.StartHour` until `GameStarter` exists. Menu, World, `GameStarter` and the other root services are not built yet.

## Systems

**Core** (`Assets/_Game/_Core/`)

| System | Owns |
| --- | --- |
| _Bootstrap | Root `ProjectLifetimeScope` (+ prefab, `ControlSettings`), `VContainerSettings` asset, `GameLifetimeScope` |
| PlayerSystem | Controller, look, movement, stamina, fall damage, head bob, owns the player's `Equipment`. See Entities → Player |
| EquipmentSystem | Items (`ItemDefinition` → `EquippableDefinition`, `ItemInstance`), `Hotbar` (≤ 9 category-checked slots), `EquipmentSwitcher` (timed holster → equip), `Equipment` (selection, pick up / swap / drop), first-person `EquipmentRig` / `HeldItem`, `WorldItem` pickups. Rules and how-tos: [EQUIPMENT.md](EQUIPMENT.md) |
| WeaponSystem | Weapon item kinds (`GunDefinition`, `MeleeDefinition`), guns (`Gun` rules, `HeldGun` view, `GunRecoil`, ammo by type in `AmmoReserve`), melee (`MeleeAttack` rules, `HeldMelee` view), and two root services ticked by VContainer: `BulletSystem` (bullets as data) and `EffectPool` (pooled muzzle flashes, tracers, impacts, holes). Rules and how-tos: [WEAPONS.md](WEAPONS.md) |
| InputSystem | `InputService` (root scope) over `InputSystem_Actions` through its generated wrapper `GameInputActions`: owns enabling, map switching (`EnableGameplay` / `EnableUI`) and cursor lock; consumers only read. Mouse look is per-frame delta, stick look is scaled by deltaTime. Namespace `Aipocalypse.Input` — a namespace ending in `InputSystem` shadows `UnityEngine.InputSystem.InputSystem` |
| _Common | `EntityScope`, Combat (`IDamageable`, `DamageInfo` / `DamageResult`, `DamageService`, `Health`), Events, Stats (`IStatProvider`), Interaction (`IInteractable`) |
| AudioSystem | `AudioService` (root): plays BroAudio `SoundID`s 2D or at a point. Libraries in `Data/Audio/` |
| WorldTimeSystem | The in-game clock (`WorldClock`, scene scope): day, hour, Dawn / Day / Dusk / Night phases, master and per-phase speed, jumps, skips, slow / freeze requests, moon phase; publishes hour, phase, day, jump and skip events; N / M cheats in dev builds. Separate from `TimeService`, which owns Unity's `Time.timeScale`. [DAYNIGHT.md](DAYNIGHT.md) |
| EnvironmentSystem | Scene-level environment with no gameplay rules: the time-of-day look (`EnvironmentDirector`, sole writer of the sun / moon light, ambient, fog, reflections, sky globals and time-of-day grade volumes; data in `TimeOfDayProfile` + `LightingPreset`; `IAtmosphereLayer` for weather later), reactors (lamps, windows, particles, material tints), `AmbienceService`; shader globals helpers `BKFoliageGlobals`, `ASPTopLayerGlobals`. Day/night: [DAYNIGHT.md](DAYNIGHT.md); environment content and its generator: [ENVIRONMENT.md](ENVIRONMENT.md) |
| UI | Standalone HUD canvas with its own `HudScope` (placed in the scene; player views follow `PlayerRegistry`), passive widgets with style assets (`StatBar` + `StatBarStyle`, `HotbarSlotView` + `HotbarSlotStyle`, `ClockView` + `ClockStyle`), presenters that feed them (`PlayerVitalsHud`, `HotbarHud`, `ClockHud`). Rules and how-tos: [UI.md](UI.md) |

**Features** (`Assets/_Game/Features/`): *none yet.* Each system or feature gets a row here when it lands.

## Holding off on expensive commitments

- **Save:** not until the core loop is fun — save formats lock in data shapes. When added: versioned JSON, no old-save compatibility promise during development.
- **Stats/modifiers:** minimal version first (today: `IStatProvider` with a pass-through default); extend only when a designed feature needs it.
- **Interfaces:** only at real seams (Core ↔ Feature, entity ↔ outside world).

## Experiments

*None yet.* Trial mechanics live in `Assets/_Game/_Experimental/<Name>/`, each with its own installer. Kept → move to `Features/`. Cut → delete the folder. Mirrors the `IDEAS.md` → `CORE.md` flow.

## Debug tools

*Not built yet.* Planned as an in-game/Odin debug panel; each system adds its own commands through a small `IDebugCommands` registration. Until then: Odin buttons on views (e.g. `Player.Revive`).

## Entities: self-contained prefabs

Any entity prefab works when dropped into any scene — World, a prototype, an empty test scene.

| Kind | Examples | How it gets services |
| --- | --- | --- |
| **Unique entity** — one or a few | Player | Own `EntityScope` (a child container) |
| **Crowd entity** — many, pooled | *Not built yet* | Injected once from the nearest scope, no container of its own (a scope per instance doesn't scale) |
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
- **Strict mode:** `GameLifetimeScope.Strict` (to be on in World) turns "default used" into an error, so a missing installer in the real game can't hide behind a default.

**Finding each other:** `PlayerRegistry` (root scope) holds the active player: players register themselves and it publishes `ActivePlayerChanged`. The HUD uses it today; enemies and companions will too. Other entities: *not built yet* (planned: an `EntityRegistry` in the root scope, which can absorb `PlayerRegistry`). No `FindObjectOfType`, no statics.

### Player

`_Core/PlayerSystem/Prefabs/Player.prefab` works dropped into any scene; scenes need no camera of their own. Hierarchy: [CODE_STRUCTURE.md → Player prefab](CODE_STRUCTURE.md#player-prefab).

- `Player` is the only gameplay MonoBehaviour with an `Update` (HUD views read it in `LateUpdate`). Each frame it builds a `PlayerIntent` from `InputService`, then ticks plain C# parts in a fixed order: `PlayerLook` → `PlayerMotor` → `Stamina` → `Fatigue` + `PlayerBreathing` → `PlayerHeadBob` → `Equipment` + `EquipmentRig`. Tests or cutscenes can drive the motor with an intent directly.
- `PlayerMotor`: walk/sprint (sprint forward only; backpedal and strafe slower), jump with coyote time and buffering, momentum kept in the air, sliding on slopes past the controller's slope limit, landing tracking (drop height, impact speed). Speeds and costs are read through `IStatProvider`.
- `Stamina`: sprint drains, jump costs a chunk, shots and swings cost per weapon (without pausing regen); regen after a delay; at 0 the player is exhausted (no sprint or jump) until it refills to a fraction. `Health` takes fall damage (dealt through `DamageService`), linear between a safe and a lethal drop height. Dying publishes `PlayerDied`. `Fatigue`: under 50 / 25 / 10% stamina, eased multipliers raise gun recoil and lower melee damage, knockback and move speed; firing is never blocked ([WEAPONS.md → Fatigue](WEAPONS.md#fatigue)).
- Feel: footsteps play at the bottom of each head-bob step (`PlayerHeadBob.Stepped`), plus jump and landing sounds and heavy breathing when out of breath (`PlayerSoundTuning`). Aiming down sights zooms the Cinemachine lens by the held item's `Zoom` (`PlayerZoom`) and scales look speed to match; the held model keeps its own field of view.
- Equipment: keys 1–9 select hotbar slots, the mouse wheel cycles; switching holsters the held item, then raises the new one over per-item times scaled by `EquipmentConfig.SwitchSpeed`. Details: [EQUIPMENT.md](EQUIPMENT.md).
- First-person presentation: `FirstPersonRenderFeature` draws the HeldWeapon layer inside the world camera with a cleared depth and its own lens (`FirstPersonView`), so held models stay visible and unzoomed. `HeldItemClearance` blends the hand back and tilts it near obstacles without moving the player; melee swings opt out.
- All numbers live in the `Data/Player/` tuning assets, linked from `PlayerTuning.asset` (equipment: `Data/Equipment/`, `Data/Items/`).

## AI

The first AI slice is a capsule wander demo in `Prototype.unity`. Every AI agent decides with a Behavior Designer Pro tree; tasks delegate navigation to `WanderMover`. The shared `Data/Enemies/Wander.asset` subtree repeats a sequence: choose a reachable point, walk there, pause. Tuning lives in `WanderSettings.asset`.

`GameLifetimeScope` optionally installs `EnemyInstaller` when its navigation and wander settings are assigned. Enemies are found through `EnemyRegistry` (scene scope), a dense list: each `EnemyBrain` is injected once (scene-placed ones by the scope's build callback, spawned ones by their spawner), starts its tree on its first `Start` and joins the registry while enabled, so spawned and pooled enemies tick like placed ones. `EnemyManager` walks the registry and ticks the Manual trees with a staggered stride. `Navigation.prefab` holds AstarPath and RVOSimulator; its small terrain-following grid scans on startup. Pooling, spawning and combat are not implemented yet. Skill: `tools-unity-behavior-designer`.

## Performance

Standards that apply to all gameplay code now:

- **Zero allocations per frame:** no LINQ, no `new` in hot paths, no boxing, no string building in `Update`. Event payloads are structs.
- **DI is setup-time only:** resolve and inject at creation, never per frame.
- **One update per entity, explicit order:** a view ticks its plain C# parts (as `Player` does) instead of each part having its own `Update`. Many-instance things get one central tick (VContainer `ITickable`), not an `Update()` each.
- **Pool everything that spawns repeatedly** (`UnityEngine.Pool`) and prewarm pools before they're needed.
- Measure before optimising (Profiler, `ProfilerMarker`s); don't assume.

Per-system budgets (enemy counts, stress scenes) get added here with those systems.

## Prototypes → main game

Mechanics are built and tested in small scenes under `Assets/_Game/Scenes/Prototypes/` (not in Build Settings). Today the player is tested in `Assets/_Game/Scenes/Prototype.unity`.

1. **Only the scene is a prototype, never the code.** Mechanic code goes in its real folder (Core, Feature or Experimental) from day one. No `Proto*` copies of classes or prefabs.
2. **A prototype scene needs no scope of its own.** Drop in the entity prefabs (and `HUD.prefab` if you want the HUD); they run on root services + their defaults.
3. **Real systems via installers.** When a prototype needs a real system, add a `GameLifetimeScope` with just those installers. World uses the same class with all installers.
4. **Defaults belong to the entity's system** (e.g. `PlayerSystem/Defaults/BaseStats`) — they are part of the entity's contract.
5. **Tuning lives in ScriptableObjects** under `Assets/_Game/Data/`, shared by every scene.
6. **Smoke test** (PlayMode, not built yet): load every scene, wait a frame, assert entities are injected and no errors were logged.

A prototype is done when it works in its scene **and** in World with no code changes.

## Folder layout

What exists, plus where new things go. Script by script: [CODE_STRUCTURE.md](CODE_STRUCTURE.md).

```text
Assets/_Game/
  _Core/                  asmdef Aipocalypse.Core
    _Bootstrap/           ProjectLifetimeScope (+ prefab), VContainerSettings asset (preloaded), GameLifetimeScope
    _Common/              Entities, Combat, Events, Stats, Interaction
    PlayerSystem/  InputSystem/  EquipmentSystem/  WeaponSystem/  UI/
    <NewSystem>/          one folder per Core system, with its installer
  Features/               (when the first feature lands) asmdef Aipocalypse.Features → references Core
  _Experimental/          (when the first experiment lands) asmdef Aipocalypse.Experimental → references Core + Features
  Data/                   ScriptableObject assets, one folder per system
    Environment/          TerrainData + MicroVerse stamp assets per area, own heightmaps (skill tools-unity-microverse)
  Scenes/                 Prototype.unity
    Prototypes/           Proto_* scenes
  Tests/EditMode/         asmdef Aipocalypse.Tests.EditMode
```

## Assemblies

- `Aipocalypse.Core` and `Aipocalypse.Tests.EditMode` exist. `Aipocalypse.Features` / `Aipocalypse.Experimental` get created with their first code; the asmdef references enforce **Features → Core** at compile time.
- **Don't enable VContainer's source generator** for now: it has an open issue with injectors across multiple assemblies (hadashiA/VContainer#822), and reflection-based injection is fine on PC. Revisit only if profiling shows injection cost.
- Rules are plain C# and get EditMode tests (today: `Stamina`, `Fatigue`, `PlayerBreathing`, `Health`, `FallDamage`, directional speed).


## Time and QTE services

Two kinds of time: `TimeService` (root) owns Unity's `Time.timeScale`; `WorldClock` (scene scope, [DAYNIGHT.md](DAYNIGHT.md)) is the in-game day and runs on scaled time, so pausing stops it, while its own speed requests change only the clock.

ProjectLifetimeScope now also installs TimeInstaller and QuickTimeInstaller. TimeService is the sole owner of Time.timeScale and fixed-step adjustment: owner-keyed requests combine by taking the slowest, explicit pause wins, and releasing a request preserves other requests. Disposal restores the initial Unity settings. Future pause/slow-motion callers must use this service.

QuickTimeService owns one replaceable IQteSession at a time and ticks it in real time while gameplay input is enabled and TimeService is not paused. QteChallenge data creates timed or owner-controlled sessions; MessagePipe QteStarted/QteEnded announce lifecycle changes. The weapon view requests an event and receives its result through a method callback; Gun owns gameplay completion. The passive HUD widgets render the session through QteView. All five guns consume the same system through separate per-gun repair profiles, timing challenges and animation overrides. No durability, heat or additional malfunction types were added.

Gun repair QTEs are optional accelerators: the automatic 4-5 second repair continues while an owner-controlled session is visible, with no slow-motion request. The onset fraction is per-profile data. The owner closes the event on normal completion; a successful input completes gameplay immediately. Standalone timed QTEs retain their own duration and optional time-scale settings.
