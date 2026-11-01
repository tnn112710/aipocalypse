# Spawning

How anything comes into play at runtime and leaves again: enemies, raid waves, animals, flocks of birds, schools of fish, and later items, rewards, coins, props and trees. One scene service (`SpawnService`) does it all, with pooling and a per-frame budget built in. Code: `Assets/_Game/_Core/SpawnSystem/` (namespace `Aipocalypse.Core.SpawnSystem`), ambient flocks in `_Core/AmbientSystem/`.

```text
WHEN   drivers ──────── RaidDirector (waves) · PopulationDirector (SpawnArea, open world) · console · your system
          │ SpawnRequest
WHAT   SpawnCard ─────── prefab variants by weight, group size, placement, category   (EnemySpawnCard adds the arrival cue)
       SpawnSquad ────── mixed group in slot order (leader first) + formation
       SpawnTable ────── cards by weight, day phase, day range, max alive, "nothing" weight
WHERE  ISpawnArea ────── SpawnZone (arc / ring in the scene) · RingArea (code) · or a plain point
       SpawnPointPicker  samples the area → SpawnPlacer (ground / navmesh / air / water) → SpawnVisibility (out of view)
       Formation ─────── slot offsets (scatter, line, column, wedge, ring, clusters, custom), each slot placed again
HOW    SpawnService ──── queue woken within SpawnBudgetMs a frame → PrefabPool (prewarmed, injected) → ISpawnable.OnSpawned
TRACK  SpawnHandle ───── Count, Pending, Alive, Spent, Cancel(), Despawn(Now | WhenUnseen)
```

## Rules

- **Code spawns only through `SpawnService`.** A plain `Instantiate` gives an object that isn't pooled, counted or (for enemies) injected.
- **Every spawnable prefab has a `Spawnable` on its root.** Components that hold per-life state implement `ISpawnable` and reset it in `OnSpawned`: a pooled object never runs `Awake` or `OnDestroy` again.
- **Disabling the root is despawning.** `Spawnable.OnDisable` hands it back to its pool. Never `Destroy` a pooled object.
- **Dead or used up is `Spawnable.Retire()`.** It stops counting as alive at once while it stays in the world (a body lying, a coin's pickup animation). It's pooled when it disables itself.
- **No spawning in view.** Area requests pick a spot the player can't see (the whole group's footprint) and wait up to `SpawnRules.MaxHiddenWait` before taking the visible spot farthest from the view centre. A spot the player has just looked at stays locked for `SeenLock` seconds.
- **Members still to come count as alive** (`SpawnHandle.Alive`), so an alive cap can't be overshot while a group arrives.

## Recipes

### Make a kind spawnable

1. Prefab root: add `Spawnable`. Enemies get it through `EnemyBrain`'s `RequireComponent`, but an existing prefab only gets it on disk when it's added and saved (`AgentScripts/Spawning/SpawnSystemSetup.cs` does it for every enemy prefab).
2. Parts with per-life state implement `ISpawnable` (`EnemyBrain`: teleports its agent and plays its appear; `Flock`: lays its members out).
3. Create a card: *Create → Aipocalypse → Spawning → Spawn Card* (enemies: *Enemies → Spawn Card*) next to the prefab (`Content/<Kind>/<Name>/<Name>_Card.asset`). Set its variants, group size, category and placement.
4. To spawn it by name from the console, list the card in `Data/Spawning/SpawnSystemSettings.asset` → `Cards` (enemies: also `EnemySystemSettings.Cards` for `enemy.spawn`).

### Spawn from code

Inject `SpawnService` (scene scope) into your service.

```csharp
// One, now, exactly here (already where it can stand):
Spawnable coin = _spawns.Spawn(coinCard, point, Quaternion.identity);

// A group of 6 somewhere in a zone, out of the player's view, arriving over the next frames:
SpawnHandle raiders = _spawns.Spawn(SpawnRequest.Group(goblinCard, frontZone, 6).WithRules(_raidRules));

// In a formation, round a point, facing the player:
_spawns.Spawn(SpawnRequest.At(goblinCard, point, 7).With(wedge).Facing(player.position - point));

// A mixed squad (leader first), or a pick from a table by the clock:
_spawns.Spawn(SpawnRequest.Of(shamanSquad, campZone));
_spawns.Spawn(SpawnRequest.From(nightTable, SpawnConditions.Of(clock), new RingArea(camp, 20f, 60f)));

// Following and ending it:
int alive = raiders.Alive;            // in play + still to come
raiders.Despawn(DespawnMode.WhenUnseen); // dawn: they leave once out of sight
_spawns.Despawn(instance);            // one, now
_spawns.Prewarm(goblinCard, 20);      // before it's needed (scene start, a raid getting ready)
```

`SpawnRequest.Count` 0 means the card's group size. A request that can't find a spot keeps trying every `RetryInterval` (and warns once if nothing in its area fits the kind); one at a point where the kind can't stand ends cancelled with a warning.

### Flyers and swimmers

Set the card's `Placement`:

| Medium | Height | Placed |
| --- | --- | --- |
| `Ground` | — | On `GroundMask` under the sample; `Navmesh` Require (walkers: snapped, with a path to the area's origin), Prefer (snapped when close) or Ignore (props, items). `MaxSlope`, `AlignToGround`, `Clearance` |
| `Air` | metres above the surface (min, max) | Over the ground or a water body, whichever is higher; `Clearance` keeps it off trees and rocks |
| `Water` | metres below the surface (min, max) | Only over a listed water body deep enough for the band plus 0.3 m margins and `Clearance` from the surface and the bed |

Water bodies are the flat renderers listed on the scene's `SpawnInstaller` → `Water Bodies` (WorldMap: GreatLake, NorthPond, WestPond, WaterfallPond, Swamp, the waterfall source pool). The level is the top of a renderer's bounds. Sloped river segments don't fit that, so they aren't listed; fish in rivers need a river query later. Flying or swimming *agents* also need a mover that isn't A* (`EnemyMover` is ground-only); the spawn side is ready.

### Props, items, rewards, coins, trees

Same path, different card settings:

- **Category** `Item` (drops, rewards, coins) or `Prop` (trees, rocks, crates), for population caps.
- **Placement** `Ground` + `Navmesh = Ignore`, `AlignToGround` for things lying on slopes, `MaxSlope` to keep trees off cliffs, `Clearance` to keep them apart from obstacles.
- **A burst** (coins from a chest, loot from a kill): `SpawnRequest.At(coinCard, point, 8)` with a small scatter `Formation`. Members that can't be placed are dropped unless `FallBackToAnchor` is set; turn it off on items so they don't stack.
- **Picked up, chopped, opened:** call `Retire()`, then disable it when its animation ends. A `SpawnArea` counts it as spent and replaces it after `RespawnDelay`, so a chopped tree regrows and a picked coin respawns.
- **Dense static scenery** (forests, grass) stays with the map generator, MicroVerse and GPU Instancer. Use spawning for the few interactive ones.

### Open-world population (SpawnArea)

A place that keeps itself populated while the player is near: a goblin camp, a deer meadow, a nest, a fishing spot, a grove of choppable trees.

1. A GameObject with a `SpawnZone` (the arc or ring things appear in; `HalfAngle` 180 = a full ring) and a `SpawnArea` (on the same object).
2. Area fields: `Card` or `Table`, `Capacity` (most alive here), `GroupSize` (most per top-up), `RespawnDelay` (seconds before a killed or used-up one comes back; 0 = never), `ActivateRadius` / `DeactivateRadius` (wake inside, sleep past, with a gap so it doesn't flicker), `Formation`, `Rules` (min player distance, hidden wait), `Prewarm`.
3. Save the scene: `SpawnInstaller` collects the areas on save and on entering Play.

`PopulationDirector` looks at every area each `PopulationInterval` (0.5 s):

- It wakes the near ones and tops them up out of view, within `SpawnSystemSettings` `CategoryCaps` and `GlobalCap`.
- When the player goes past an area's deactivate radius, its members leave once unseen.
- Killed ones stay gone until their delay ends, also across visits.
- The respawn timers live in the director (seconds of scaled play time). A save system can store them later. Nothing persists across scene loads yet.

*Not placed in WorldMap yet*: the open world has no content using it. It's tested by `PopulationTests` and ready for the first camp, herd or grove.

### Ambient flocks and schools (AmbientSystem)

Birds and fish that only decorate are one `Spawnable` per flock (`Flock` + a `FlockDefinition`). Their members are plain renderers moved by `FlockSystem`'s single central tick (boids in `FlockMath`): no behaviour tree, A* agent, Animator, collider or `Update` per member.

- Members keep together, roam up to `RoamRadius` from where they spawned, stay inside the card's height (air) or depth (water) band, and scatter from the player within `FleeRadius`.
- The scene needs an `AmbientInstaller` next to the `SpawnInstaller` (WorldMap has both).
- Content lives in `Content/Ambient/`. `FishSchool` uses the polyperfect chub mesh as a static mesh. `BirdFlock` uses blockout members until a bird pack is chosen: swap the definition's `Member`.
- Spawn them like anything else: `spawn.near fishschool 3 150`, or through a `SpawnArea` with the flock's card.

### Raids

`RaidSpawner` asks for each wave group with `SpawnRequest.Group(card, zone, count).WithRules(RaidSettings.Spawning)`, plays the card's arrival cue at the group's anchor once it's placed, counts its handles' `Alive` against `AliveCap`, and withdraws with `Despawn(Now)` (death, stop) or `Despawn(WhenUnseen)` (dawn). Details: [RAIDS.md](RAIDS.md).

## Reference

**Scene setup:** `SpawnInstaller` on the `GameLifetimeScope`. It holds `SpawnSystemSettings` (`Data/Spawning/SpawnSystemSettings.asset`), the water bodies and the collected areas. `EnemyInstaller`, `RaidInstaller` and `AmbientInstaller` require it. The navmesh comes from `EnemyInstaller` through `INavmeshQuery` (`EnemyNavigation`). Without it, `Require` points can't be placed and `Prefer` points stay on the ground.

**Lifecycle:** `Pooled` → (prewarm: `Warming` one frame) → `Active` (counted) → `Leaving` (withdrawn, not counted, despawns once unseen) or `Retired` (dead or used up, not counted, lies until it disables) → `Pooled`. `Spawnable.Life` counts lives, so a handle never mistakes a reused instance for its own.

**`SpawnSystemSettings`:**

| Field | Default | Meaning |
| --- | --- | --- |
| `SpawnBudgetMs` | 2 | Milliseconds a frame spent waking queued members, at least one a frame. A pooled goblin wakes in about 1 ms (editor) |
| `PrewarmPerFrame` | 2 | Instances created and woken once per frame while prewarming |
| `StagingPoint` | (0, −1000, 0) | Where prewarmed instances wake for a frame before they're parked |
| `GroupRadius`, `GroupSpacing` | 3–5 m, 1.2 m | The scatter used when a request names no formation |
| `RetryInterval` | 0.25 s | Between two tries of a request still looking for its spot |
| `Cards` | goblin, fish school, bird flock | Spawnable by name from the console |
| `DefaultRules` | | `SpawnRules` for requests without their own (console, `At`) |
| `PopulationInterval`, `GlobalCap`, `CategoryCaps` | 0.5 s, 300, per category | The population director's pace and caps |

**`SpawnRules`:**

- `Candidates`: points sampled per pick.
- `SnapDistance`: metres a point may move to reach the navmesh.
- `MinPlayerDistance`, `VisibleDistance`, `ViewMargin`: what counts as seen.
- `MaxHiddenWait`: seconds before a visible spot is allowed.
- `TargetHeight`: where the line-of-sight ray aims (a chest).
- `SeenLock`, `SeenLockRadius`: just-seen spots stay off-limits.
- `GroundMask`, `SightBlockers`, `ObstacleMask`: layers.

Raids keep theirs in `RaidSettings.Spawning`.

**Console** (dev builds; [CONSOLE.md](CONSOLE.md)):

- `spawn.stats`: alive per category, queue, placing, leaving, awake areas.
- `spawn.card <card> [count] [formation]`: in front of the player; formations are scatter, line, column, wedge, ring and clusters.
- `spawn.near <card> [count] [radius]`: anywhere it fits round the player, seen or not.
- `spawn.test <ground|air|water> [count] [radius]`: draws where points land for 30 s.
- `spawn.clear`: despawns what the console spawned.

**Performance** (measured 2026-11-02, editor, `AgentScripts/Perf/SpawnWakeProbe.cs`):

- **First wake of a goblin:** a never-woken goblin costs about 1.4 ms (Awake, OnEnable) plus about 2 ms of `Start` the next frame. Prewarming therefore wakes each instance once, off-stage, while loading. A prewarmed goblin then wakes in about 1.1 ms in total, with no `Start` left.
- **Where a warm wake goes** (about 1 ms):
  - Re-enabling the model's GameObject (the Animator re-links to its model): about 0.5 ms.
  - The A* agent's entity: about 0.3 ms.
  - The rest: under 0.2 ms each.
- **Toggling components instead of the GameObject** ("dormant" pooling) would save about 0.5 ms more per spawn. Not done: it would change the enemy lifecycle contract, and the queue's budget already covers it.
- **Spawned instances live at the scene root**, not under a shared parent. A shared parent serialises animation write-back: 30 goblins took 0.50 ms there, against 0.36 ms at the root.
- **Far enemies think less often:** past `EnemySystemSettings.FarThinkDistance` (60 m) from the player, an enemy thinks only every `FarThinkInterval` (0.25 s) instead of every 1/15 s. It keeps walking in between.
- **Picking a variant with free copies:** a card's variant is picked by weight, but when that one has no free pooled copy the service takes another variant of the card that has one. Random picks can outrun a prewarm made by weight, and instantiating mid-fight costs about ten wakes.
- **300 goblins** (`SpawnPlayCheck.Crowd`, editor, untargeted so they wander; measured after the variant fallback above):
  - Prewarm: 7.5 s at 2 a frame, `PrefabPool.Prewarm` about 2.7 ms a frame.
  - Arrival in 10 groups: 5.1 s, `SpawnService.Spawn` median 2.5 ms, p95 2.8 ms, worst 10 ms.
  - Steady PlayerLoop median: 13.7 ms with far enemies thinking slower, 15.1 ms without.
  - `EnemyManager.Think`: 0.18 ms against 0.26 ms. The trees aren't the crowd's main cost.
- **Flocks:** one central tick. Each member checks only its own flock's members (O(n²) per flock, n ≤ 12).
- **Allocations:** none per frame in the service, pool, director or flock ticks. Requests and handles are allocated per group, not per frame.

**Tests:**

- `SpawnServiceTests`: handles, counts, retire, cancel, leave, squads, tables, prewarm.
- `FormationTests`, `SpawnTableTests`, `SpawnPlacementTests` (air and water bands, water bodies, rings), `PopulationTests` (rules, caps, flocks, far think rate), `SpawnPointScorerTests`, `EnemySpawningTests` (cards).
- Edit mode runs no `OnDisable`, so returning to the pool is checked in play:
  - `AgentScripts/Spawning/SpawnPlayCheck.cs`: `Raid`, `Console`, `Flocks`.
  - `AgentScripts/Raids/RaidSoak.cs`.

## Not built yet

- Water levels for sloped rivers.
- Flying and swimming movers for agents.
- Saving the population ledger.
- `InstantiateAsync` for growing pools: prewarm happens while loading, and creation cost hasn't shown up as a problem.
- Dormant (component-level) pooling.
- Distance LOD for flocks.
- Spawn points tagged with a capacity (hint points), and line-of-fire scoring for ranged enemies.
