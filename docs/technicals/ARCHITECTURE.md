# Architecture

DI with **VContainer** (no gameplay singletons), events with **MessagePipe**, async with **UniTask**, enemy navigation with **A\* Pathfinding Project**. Rationale in [DECISIONS.md](DECISIONS.md).

The design is still moving, so the architecture is built to absorb change: a small stable **Core**, swappable **Features** on top, everything tunable in data, and events as the joints between them.

## Core vs Features

| Core — build now, rarely changes | Features — *provisional*, built when the design settles |
| --- | --- |
| Movement, look, interact | Day/night rules: phases, fire tiers, skip streak, big fights |
| Shooting, melee, damage, `Health` | Perk cards, XP / skill tree, luck |
| Enemy navigation, attack, pooling | Death penalty, rewind, save |
| Inventory as resource counts | Wave makeup, night types, economy, crafting |
| Clock (time of day only) | Fishing, farming, minigames, companions' special behaviour |
| Stat reads (`IStatProvider`), chance rolls (`IChanceRoll`) | Anything that modifies stats or luck |

- **Features → Core only.** Core never references a feature. Enforced by assemblies (see Assemblies).
- **One feature = one folder + one installer.** Cutting a feature = removing its installer; Core still compiles and runs.
- Core exposes **seams** with plain defaults; features plug better implementations in. E.g. Core rolls jams through `IChanceRoll` (default: plain random); the Luck feature replaces it. Core reads recoil through `IStatProvider` (default: base values); Perks/XP add modifiers.
- A GDD change should only touch Features rows. If it forces a Core change, that's a signal to add a seam.

## Layers

```text
VIEWS     MonoBehaviours: Player, Weapon, Enemy, Fire, HUD, interactables.
          Physics, animation, VFX, input -> intents. [SerializeField] for prefab parts, [Inject] for services.
SERVICES  Plain C# registered in VContainer. Own the rules.
          Publish gameplay events. Never reference MonoBehaviours directly — use an interface.
STATE     Plain [Serializable] classes, no Unity refs. The run's state (inventory, clock, meters…).
DATA      ScriptableObjects: defs, tuning, tables. Read-only at runtime.
```

Dependencies point down only.

## Gameplay events

Core **announces** what happens; features **react**. New rules are new subscribers, not edits to existing code.

- Events are small structs published through MessagePipe (`IPublisher<T>` / `ISubscriber<T>`, injected). Brokers are registered in the root scope.
- Core events live in `_Core/_Common/Events`: `ShotFired`, `DamageDealt`, `EntityDied`, `ItemPicked`, `ItemConsumed`, `TimePhaseChanged`, `PlayerDied`… Features may define their own.
- Examples: loot drops subscribe to `EntityDied`; perks subscribe to `DamageDealt`; the drone's quips subscribe to anything.
- Events are for things that *happen* (a shot, a hit, a death), never per-frame data (positions, timers). Structs only — publishing a struct allocates nothing.
- Inside one system, plain C# events are fine. Across systems, use MessagePipe.

## Composition and data

**Entities are built from parts, not class hierarchies.**

- Enemy = `Health` + `Mover` (A\*) + `Targeting` + `Senses` + `Attack`. An archetype (rusher, tank, ranged…) is a combination + data → a new enemy type is a prefab variant, not a new class.
- Weapon = `WeaponDef` pointing to swappable modules: **fire** (hitscan / projectile / spread / melee swing), **cost** (ammo / stamina / none), **on-hit** (damage, knockback, catch fish…). Fishing gun, axe and jamming pistol are combinations.
- Buffs, perks, food, weather, sanity effects all apply the same small `Modifier` pieces to stats. Start minimal (base + list of modifiers); add expiry, stacking and sources when perks are actually designed.

**Every number lives in data.**

- ScriptableObjects under `Assets/_Game/Data/`: `PlayerTuning`, `WeaponDef`, `EnemyArchetypeDef`, `NightTable`, `WaveTable`… SO edits made in Play mode persist — tune by feel while playing (Odin for editing).
- Rules that are really tables (fire tier → wave size, day → difficulty, luck → jam rate) are data tables, not `if` chains.

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

## Systems

**Core** (`Assets/_Game/_Core/`)

| System | Owns |
| --- | --- |
| PlayerSystem | Controller, look, movement, interact; meters (HP, stamina…) in State, the view shows them |
| EquipmentSystem | Hotbar, held item, switching, equip/unequip; held-item contract (primary/secondary use). Weapons are one kind of held item; Features add their own (rod, farm tools) |
| WeaponSystem | Weapon, `WeaponDef`, fire/cost/on-hit modules, recoil, jams, melee |
| EnemySystem | `EnemyManager` (central tick, time-slicing), `EnemyFactory` + pool, enemy parts (Mover/Targeting/Senses/Attack), `EnemyArchetypeDef`, A\* `FollowerEntity` agents |
| InventorySystem | Resource counts and items (ammo is a resource, not a weapon field) |
| TimeSystem | `TimeService`: time of day, day number, pause, phase events. No rules about what phases mean |
| InputSystem | `InputService` over `InputSystem_Actions`, map switching. Namespace `Aipocalypse.Input` — a namespace ending in `InputSystem` shadows `UnityEngine.InputSystem.InputSystem` |
| _Common | `EntityScope`, `EntityInjector`, `EntityRegistry` + spatial grid, Combat (`IDamageable`, `DamageInfo`, `Health`), Events, Stats (`IStatProvider`, `Modifier`), `IChanceRoll`, Utils |

**Features** (`Assets/_Game/Features/`, *provisional — follow the GDD*)

| Feature | Owns |
| --- | --- |
| DayNight | `DayCycle` (what phases mean), fire tiers, skip streak, big-fight countdown, `NightPlan`, `WaveDirector` |
| Progression | XP / skill tree, perk cards, luck (`IChanceRoll` implementation) |
| Persistence | Save, death penalty, rewind |
| Activities | Fishing, chopping, farming, minigames |
| Economy | Shops, crafting, selling |

Feel / Damage Numbers Pro hook into `Health` and event subscribers, never into weapons directly.

## Holding off on expensive commitments

- **Save:** not until the core loop is fun — save formats lock in data shapes. Until then, `DevStartConfig` presets replace saving. When added: versioned JSON, no old-save compatibility promise during development.
- **Stats/modifiers:** minimal version first; extend only when a designed feature needs it.
- **Interfaces:** only at real seams (Core ↔ Feature, entity ↔ outside world).

## Experiments

Trial mechanics live in `Assets/_Game/_Experimental/<Name>/`, each with its own installer and a toggle in `DevStartConfig`. Kept → move to `Features/`. Cut → delete the folder. Mirrors the `IDEAS.md` → `CORE.md` flow.

## Debug tools

Part of Foundation, not an afterthought — experimenting is only fast if setting up a situation is fast. A debug panel (Odin / in-game overlay):

- Set time / day, skip to dusk or a big fight, time-scale slider
- Spawn an enemy or a wave at the crosshair
- Give ammo / items, set meters, god mode
- Apply a modifier or perk, toggle features and experiments

Each feature adds its own commands through a small `IDebugCommands` registration, so the panel grows with the game.

## Entities: self-contained prefabs

Any entity prefab works when dropped into any scene — World, a prototype, an empty test scene. There are two weights, because a `LifetimeScope` per enemy does not scale to hundreds:

| Kind | Examples | How it gets services |
| --- | --- | --- |
| **Unique entity** — one or a few | Player, dog, cat, drone, fire | Own `EntityScope` (a child container) |
| **Crowd entity** — many, pooled | Enemies, fish, critters, loot pickups | `EntityInjector`: injected once from the nearest scope, no container of its own |
| Plain object | Projectiles, VFX, props | No injection; data via `[SerializeField]` / the spawner |

```text
Unique entity root: PlayerScope : EntityScope
  ├─ own components, wired with [SerializeField]  (camera rig, CinemachineBrain, AudioListener live inside the Player)
  ├─ data: PlayerTuning / WeaponDef (ScriptableObjects)
  └─ needs from outside = small interfaces (IAmmoSource, IStatProvider…)

Parent lookup:  scene's GameLifetimeScope  →  if none: root ProjectLifetimeScope
Each need:      parent chain has it?  yes → use it   no → entity's default (InfiniteAmmo, BaseStats…) + one warning
```

How `EntityScope` does it (VContainer API checked in package source):

- **Parent:** overrides `FindParent()` to return the scene's `GameLifetimeScope` (or `null` → VContainer falls back to the root).
- **Defaults:** in `Configure`, registers a default for a need only if `Parent.Container.TryGetRegistration(type, …)` fails (it searches the whole parent chain). A child registration would otherwise override the real service.
- **Injection:** `RegisterBuildCallback(r => r.InjectGameObject(gameObject))` injects the entity's own hierarchy. Never `RegisterComponentInHierarchy` for entity parts — it searches the whole scene and grabs another instance's component.
- **Order:** `GameLifetimeScope` gets `[DefaultExecutionOrder(-5000)]`. Without it, an entity scope whose `Awake` runs first would build against a parent with no container yet (NullReferenceException; VContainer only defers for `parentReference`, not `FindParent`).
- **Strict mode:** `GameLifetimeScope.strict` (on in World) turns "default used" into an error, so a missing installer in the real game can't hide behind a default.

Crowd entities:

- **Spawned** (normal case): the pool injects each instance **once at creation** (`resolver.InjectGameObject`), then reuses it. Spawning = take from pool + `Reset(def, position)`: no injection, no `Instantiate`, no allocation.
- **Dropped into a scene by hand** (prototypes): `EntityInjector` finds the nearest scope on `Awake`, injects once, and hands the enemy to the scene's `EnemyManager` (or a default one if none exists). Same prefab, same behaviour.
- Enemies read their numbers from their `EnemyArchetypeDef` (+ a per-night difficulty multiplier), not from the stats service.

**Finding each other:** entities register on enable in the `EntityRegistry` (root scope): dense lists for iteration plus a **spatial grid** for "who is near this point" (senses, explosions, targeting). Enemies get the player through `IPlayerLocator`. No `FindObjectOfType`, no statics.

## Performance

Target: **hundreds of active enemies at 60 fps** on the target PC, without ECS in our own code. Verify with a stress scene, don't assume.

**Update loop**

- Crowd entities have **no `Update()` of their own.** `EnemyManager` ticks all active enemies in one loop over a dense list (VContainer `ITickable`). Hundreds of separate `Update()` calls cost far more than one loop.
- **Time-slicing:** decisions (retarget, line-of-sight, choose attack) run at a reduced rate — e.g. each enemy thinks every 4–10 frames, staggered so the load is even. Movement and animation stay per-frame.
- **Distance LOD:** far enemies think less often, skip line-of-sight raycasts, and use cheaper animation.

**Navigation (A\* 5.4)**

- Use **`FollowerEntity`** agents: A\* runs their movement on ECS/Burst internally (`com.unity.entities` is installed), which scales to hundreds while our code stays MonoBehaviours.
- Repath on a timer scaled by distance, not every frame; local avoidance (RVO) on; swarms can share a destination.

**Physics and queries**

- Senses and targeting check the spatial grid first; raycast only for the few that pass. Use `NonAlloc` / batched queries (`RaycastCommand`) when counts grow.
- Simple colliders (capsules), dedicated layers and a tight collision matrix.

**Memory / GC**

- **Zero allocations per frame** in gameplay code: no LINQ, no `new` in hot paths, no boxing, no string building in `Update`. Event payloads are structs.
- **Pool everything that spawns repeatedly:** enemies, projectiles, VFX, decals, damage numbers, loot pickups, audio sources (`UnityEngine.Pool`). Prewarm pools during loading or at dusk, not when the wave arrives.
- Stats are **cached**: a stat recomputes when its modifiers change (dirty flag), not on every read.
- DI is setup-time only: resolve and inject at creation, never per frame.

**Rendering and animation**

- Animator culling (`CullUpdateTransforms` / `CullCompletely`), LOD groups, few bones on crowd meshes. If skinned crowds become the bottleneck: GPU/vertex-animated crowds for swarms.
- GPU Instancer Pro for foliage and props; SRP Batcher-friendly materials.

**Escape hatch to Jobs/Burst (still no ECS):** `EnemyManager` keeps enemy hot data (position, target, timers) in arrays, with components as views. If profiling shows the tick loop is the bottleneck, that loop moves into a Burst job without changing the rest of the architecture.

**Measure:** `Proto_Stress` spawns N enemies (100 / 300 / 500) attacking the player, with `ProfilerMarker`s on `EnemyManager`, senses and spawning. Run it whenever enemy code changes.

## Prototypes → main game

Mechanics are built and tested in small scenes under `Assets/_Game/Scenes/Prototypes/` (not in Build Settings), e.g. `Proto_Movement`, `Proto_Shooting`, `Proto_Enemies`.

1. **Only the scene is a prototype, never the code.** Mechanic code goes in its real folder (Core, Feature or Experimental) from day one. No `Proto*` copies of classes or prefabs.
2. **A prototype scene needs no scope of its own.** Drop in the entity prefabs; they run on root services + their defaults (crowd entities via `EntityInjector`).
3. **Real systems via installers.** When a prototype needs a real system, add a `GameLifetimeScope` with just those installers. World uses the same class with all installers.
4. **Defaults belong to the entity's system** (e.g. `WeaponSystem/Defaults/InfiniteAmmo`) — they are part of the entity's contract.
5. **Tuning lives in ScriptableObjects** under `Assets/_Game/Data/`, shared by every scene.
6. **Smoke test** (PlayMode): load World and every `Proto_*` scene, wait a frame, assert entities are injected and no errors were logged.

A prototype is done when it works in its scene **and** in World with no code changes.

## Folder layout

```text
Assets/_Game/
  _Core/                  asmdef Aipocalypse.Core
    _Bootstrap/           ProjectLifetimeScope, GameLifetimeScope, MainMenuLifetimeScope, GameStarter, DevStartConfig
    _Common/              EntityScope, EntityRegistry, Combat, Events, Stats, IChanceRoll, Utils
    _Debug/               debug panel, IDebugCommands
    PlayerSystem/  EquipmentSystem/  WeaponSystem/  EnemySystem/  InventorySystem/  TimeSystem/  InputSystem/
  Features/               asmdef Aipocalypse.Features → references Core
    DayNight/  Progression/  Persistence/  Activities/  Economy/
  _Experimental/          asmdef Aipocalypse.Experimental → references Core + Features
  Data/                   ScriptableObject assets
  Scenes/                 MainMenu, World
    Prototypes/           Proto_* scenes
```

## Assemblies

- `Aipocalypse.Core`, `Aipocalypse.Features`, `Aipocalypse.Experimental` + one EditMode/PlayMode test asmdef. The asmdef references enforce **Features → Core** at compile time.
- **Don't enable VContainer's source generator** for now: it has an open issue with injectors across multiple assemblies (hadashiA/VContainer#822), and reflection-based injection is fine on PC. Revisit only if profiling shows injection cost.
- Rule-heavy services (stats, inventory, day/night rules, progression) are plain C# and get EditMode tests.
