# Architecture

DI with **VContainer** (no gameplay singletons), async with **UniTask**, enemy navigation with **A\* Pathfinding Project**. Rationale in [DECISIONS.md](DECISIONS.md).

## Layers

```text
VIEWS     MonoBehaviours: Player, Weapon, Enemy, Fire, HUD, interactables.
          Physics, animation, VFX, input -> intents. [SerializeField] for prefab parts, [Inject] for services.
SERVICES  Plain C# registered in VContainer. Own the rules (clock, night, inventory, stats, perks, save).
          Expose C# events / read-only state. Never reference MonoBehaviours directly — use an interface.
STATE     Plain [Serializable] classes, no Unity refs. Everything that is saved and rewound.
DATA      ScriptableObject defs (WeaponDef, EnemyArchetypeDef, PerkCardDef…). Read-only at runtime.
```

Dependencies point down only.

## Scopes and app flow

```text
Root (VContainerSettings → ProjectLifetimeScope prefab, auto-created before any scene)
  SaveService · SettingsService · InputService · SceneLoader · SessionRequest · LoadingScreen

MainMenu scene (first in Build Settings, no Boot scene)
  MainMenuLifetimeScope: New Game → SessionRequest = New(slot) | Continue → Load(slot) | Settings | Exit

World scene (one persistent map, no mission/night scopes)
  GameLifetimeScope: GameStarter (entry point) reads SessionRequest → builds/loads State → DayCycle.Begin()
  Quit to menu: save → load MainMenu (scene unload disposes all game services)

Any scene (World, Proto_*, test arenas)
  Entity prefabs (Player, enemies, companions, fire…) carry their own EntityScope — see Entities
```

- **Play from any scene:** the root scope always exists. If `SessionRequest` is empty (Play pressed in World), `GameStarter` uses a `DevStartConfig` asset (editor only): start day/time, fire tier, inventory, perks, stat overrides. In builds an empty request returns to the menu.
- Scenes load with `SceneManager.LoadSceneAsync` + UniTask; no Addressables for now.

## Systems

| System | Owns |
| --- | --- |
| DaySystem | `DayCycle` state machine (Morning → Day → Dusk → FireChoice → Night/Skip → PerkPick), `TimeService` (day, time, pause; `ITickable`), `NightService` (fire tier, skip streak, big-fight countdown, `NightPlan`), `WaveDirector` |
| EnemySystem | `EnemyFactory` + pool; enemy = `EnemyArchetypeDef` (rusher/tank/ranged/flyer/swarm/sneaker) + `EnemySkinDef`; A\* agents |
| PlayerSystem | Controller, look, movement; meters (HP, hunger, stamina, sanity) live in State, the view shows them |
| WeaponSystem | Weapon, `WeaponDef`, firing, recoil, jams, melee fallback |
| ProgressionSystem | `StatsService` (base + `Modifier { stat, op, value, source, expiresOnDay }`), `LuckService.Roll(chance, context)`, `PerkService`, XP/skill tree |
| InventorySystem | Carried items, stash, coins/wood/food/materials/ammo |
| SaveSystem | Serialize State, one snapshot per day (big-fight rewind = load older snapshot), `DeathService` (loot loss, lost-loot cache) |
| InputSystem | `InputService` over `InputSystem_Actions` (map switching for UI/minigames). Namespace `Aipocalypse.Input` — a namespace ending in `InputSystem` shadows `UnityEngine.InputSystem.InputSystem` |
| _Common/Combat | `IDamageable`, `DamageInfo`, `Health`; Feel / Damage Numbers Pro hook into Health events |

Rules worth keeping:

- Every tunable number that can change during a run is a **Stat** with modifiers (XP = permanent, perk cards = expire after N days, upgrades). Weapons, stamina, chopping read stats, never raw fields.
- Every chance roll goes through `LuckService`.
- Ammo is an inventory resource, not a weapon field.
- Services talk through C# events. Add MessagePipe only if events get unmanageable.

## Entities: self-contained prefabs

Player, enemies, companions (dog, cat, drone) and stateful interactables (fire, fishing spot) are **entities**: drop the prefab into any scene — World, a prototype, an empty test scene — and it works. Simple objects (projectiles, pickups, props) are plain components with no scope.

```text
Entity prefab root: PlayerScope : EntityScope
  ├─ own components, wired with [SerializeField]  (camera rig, CinemachineBrain, AudioListener live inside the Player)
  ├─ data: PlayerDef / WeaponDef / EnemyArchetypeDef (ScriptableObjects)
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
- **Finding each other:** entities register themselves on enable in an `EntityRegistry` (root scope) — e.g. enemies get the player through `IPlayerLocator`. No `FindObjectOfType`, no statics.

## Prototypes → main game

Mechanics are built and tested in small scenes under `Assets/_Game/Scenes/Prototypes/` (not in Build Settings), e.g. `Proto_Movement`, `Proto_Shooting`, `Proto_Enemies`.

1. **Only the scene is a prototype, never the code.** Mechanic code goes in its real system folder from day one. No `Proto*` copies of classes or prefabs.
2. **A prototype scene needs no scope of its own.** Drop in the entity prefabs; they run on root services + their defaults.
3. **Real systems via installers.** When a prototype needs a real system (e.g. `Proto_Ammo` needs Inventory and Stats), add a `GameLifetimeScope` to the scene with just those installers. World uses the same class with all installers. Plugging into World = the installer is already there; nothing to port.
4. **Defaults belong to the entity's system** (e.g. `WeaponSystem/Defaults/InfiniteAmmo`), not to a prototype folder — they are part of the entity's contract.
5. **Tuning lives in ScriptableObjects** under `Assets/_Game/Data/`, shared by every scene.
6. **Smoke test** (PlayMode): load World and every `Proto_*` scene, wait a frame, assert entities are injected and no errors were logged.

A prototype is done when it works in its scene **and** in World with no code changes.

## Folder layout

```text
Assets/_Game/
  _Core/
    _Bootstrap/         ProjectLifetimeScope, GameLifetimeScope, MainMenuLifetimeScope, GameStarter, DevStartConfig
    _Common/            EntityScope, EntityRegistry, Combat (IDamageable, DamageInfo, Health), Utils
    DaySystem/  EnemySystem/  PlayerSystem/  WeaponSystem/
    InventorySystem/  ProgressionSystem/  SaveSystem/  InputSystem/
  Data/                 ScriptableObject assets
  Scenes/               MainMenu, World
    Prototypes/         Proto_* scenes
```

## Assemblies

One runtime asmdef + one test asmdef to start. VContainer's source generator has an open issue with injectors across multiple assemblies (hadashiA/VContainer#822); split only if compile times demand it. Services with rules (Stats, Luck, Perks, Inventory, Night, Death) are plain C# and get EditMode tests.
