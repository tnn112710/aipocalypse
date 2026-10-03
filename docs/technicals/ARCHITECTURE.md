# Architecture

DI with **VContainer** (no gameplay singletons), events with **MessagePipe**, async with **UniTask**, enemy navigation with **A\* Pathfinding Project**. Rationale in [DECISIONS.md](DECISIONS.md).

The design is still moving, so the architecture is built to absorb change: a small stable **Core**, swappable **Features** on top, everything tunable in data, and events as the joints between them.

## Core vs Features

| Core — build now, rarely changes | Features — *provisional*, built when the design settles |
| --- | --- |
| Movement, look, interact | Day/night rules: phases, fire tiers, skip streak, big fights |
| Shooting, melee, damage, `Health` | Perk cards, XP / skill tree, luck / karma |
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
- Examples: Luck subscribes to `EntityDied` (killed the cat → karma down); Perks subscribe to `DamageDealt`; the drone's quips subscribe to anything.
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
| WeaponSystem | Weapon, `WeaponDef`, fire/cost/on-hit modules, recoil, jams, melee |
| EnemySystem | `EnemyFactory` + pool, enemy parts (Mover/Targeting/Senses/Attack), `EnemyArchetypeDef`, A\* agents |
| InventorySystem | Resource counts and items (ammo is a resource, not a weapon field) |
| TimeSystem | `TimeService`: time of day, day number, pause, phase events. No rules about what phases mean |
| InputSystem | `InputService` over `InputSystem_Actions`, map switching. Namespace `Aipocalypse.Input` — a namespace ending in `InputSystem` shadows `UnityEngine.InputSystem.InputSystem` |
| _Common | `EntityScope`, `EntityRegistry`, Combat (`IDamageable`, `DamageInfo`, `Health`), Events, Stats (`IStatProvider`, `Modifier`), `IChanceRoll`, Utils |

**Features** (`Assets/_Game/Features/`, *provisional — follow the GDD*)

| Feature | Owns |
| --- | --- |
| DayNight | `DayCycle` (what phases mean), fire tiers, skip streak, big-fight countdown, `NightPlan`, `WaveDirector` |
| Progression | XP / skill tree, perk cards, luck / karma (`IChanceRoll` implementation) |
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

Player, enemies, companions (dog, cat, drone) and stateful interactables (fire, fishing spot) are **entities**: drop the prefab into any scene — World, a prototype, an empty test scene — and it works. Simple objects (projectiles, pickups, props) are plain components with no scope.

```text
Entity prefab root: PlayerScope : EntityScope
  ├─ own components, wired with [SerializeField]  (camera rig, CinemachineBrain, AudioListener live inside the Player)
  ├─ data: PlayerTuning / WeaponDef / EnemyArchetypeDef (ScriptableObjects)
  └─ needs from outside = small interfaces (IAmmoSource, IStatProvider, IPlayerLocator…)

Parent lookup:  scene's GameLifetimeScope  →  if none: root ProjectLifetimeScope
Each need:      parent chain has it?  yes → use it   no → entity's default (InfiniteAmmo, BaseStats…) + one warning
```

How `EntityScope` does it (VContainer API checked in package source):

- **Parent:** overrides `FindParent()` to return the scene's `GameLifetimeScope` (or `null` → VContainer falls back to the root). Spawned entities get their parent explicitly via `LifetimeScope.EnqueueParent` / the factory.
- **Defaults:** in `Configure`, registers a default for a need only if `Parent.Container.TryGetRegistration(type, …)` fails (it searches the whole parent chain). A child registration would otherwise override the real service.
- **Injection:** `RegisterBuildCallback(r => r.InjectGameObject(gameObject))` injects the entity's own hierarchy. Never `RegisterComponentInHierarchy` for entity parts — it searches the whole scene and grabs another instance's component.
- **Order:** `GameLifetimeScope` gets `[DefaultExecutionOrder(-5000)]`. Without it, an entity scope whose `Awake` runs first would build against a parent with no container yet (NullReferenceException; VContainer only defers for `parentReference`, not `FindParent`).
- **Strict mode:** `GameLifetimeScope.strict` (on in World) turns "default used" into an error, so a missing installer in the real game can't hide behind a default.
- **Pooling:** an enemy's scope is built once when the pool creates it, not on every spawn.
- **Finding each other:** entities register themselves on enable in the `EntityRegistry` (root scope) — e.g. enemies get the player through `IPlayerLocator`. No `FindObjectOfType`, no statics.

## Prototypes → main game

Mechanics are built and tested in small scenes under `Assets/_Game/Scenes/Prototypes/` (not in Build Settings), e.g. `Proto_Movement`, `Proto_Shooting`, `Proto_Enemies`.

1. **Only the scene is a prototype, never the code.** Mechanic code goes in its real folder (Core, Feature or Experimental) from day one. No `Proto*` copies of classes or prefabs.
2. **A prototype scene needs no scope of its own.** Drop in the entity prefabs; they run on root services + their defaults.
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
    PlayerSystem/  WeaponSystem/  EnemySystem/  InventorySystem/  TimeSystem/  InputSystem/
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
