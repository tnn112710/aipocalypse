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

Prototype scenes
  PrototypeLifetimeScope: same installers as World + stand-ins for missing systems
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

## Prototypes → main game

Mechanics are built and tested in small scenes under `Assets/_Game/Scenes/Prototypes/` (not in Build Settings), e.g. `Proto_Movement`, `Proto_Shooting`, `Proto_Enemies`. Rules so a working prototype plugs into World unchanged:

1. **Only the scene is a prototype, never the code.** Mechanic code goes in its real system folder from day one. No `Proto*` copies of classes.
2. **Same prefabs.** Prototype scenes use the real Player/Weapon/Enemy prefabs, not duplicates.
3. **Seams are small interfaces owned by the consumer.** E.g. the weapon needs `IAmmoSource` and `IStatProvider`; the real `InventoryService` / `StatsService` implement them later.
4. **Stand-ins live in `_Core/_Prototype/`** (`InfiniteAmmo`, `BaseStats`, `PrototypeLifetimeScope`). They are the only prototype-only code.
5. **Each system has an installer** (`IInstaller`, e.g. `PlayerInstaller`, `WeaponInstaller`). `GameLifetimeScope` and `PrototypeLifetimeScope` call the same installers; the prototype scope adds stand-ins for what is missing. Plugging a mechanic into World = adding its installer to `GameLifetimeScope`.
6. **Tuning lives in ScriptableObjects** under `Assets/_Game/Data/`, shared by prototype and World.
7. VContainer throws on a missing registration — a prototype scope that fails to start lists exactly which stand-in is missing.

A prototype is done when it works in its scene **and** in World with no code changes beyond scope registration.

## Folder layout

```text
Assets/_Game/
  _Core/
    _Bootstrap/         ProjectLifetimeScope, GameLifetimeScope, MainMenuLifetimeScope, GameStarter, DevStartConfig
    _Common/            Combat (IDamageable, DamageInfo, Health), Utils
    _Prototype/         PrototypeLifetimeScope, stand-ins
    DaySystem/  EnemySystem/  PlayerSystem/  WeaponSystem/
    InventorySystem/  ProgressionSystem/  SaveSystem/  InputSystem/
  Data/                 ScriptableObject assets
  Scenes/               MainMenu, World
    Prototypes/         Proto_* scenes
```

## Assemblies

One runtime asmdef + one test asmdef to start. VContainer's source generator has an open issue with injectors across multiple assemblies (hadashiA/VContainer#822); split only if compile times demand it. Services with rules (Stats, Luck, Perks, Inventory, Night, Death) are plain C# and get EditMode tests.
