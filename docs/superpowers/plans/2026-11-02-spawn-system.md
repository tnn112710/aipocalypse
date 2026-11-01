# Spawn System Implementation Plan

> **For agentic workers:** executed natively in the session that wrote it (superpowers:executing-plans). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** One generic, budgeted spawn + pool module in Core that serves today's raid waves and later formations, flyers, swimmers, weighted tables, open-world population and non-agent content (props, items, rewards, coins, trees).

**Architecture:** Drivers (RaidDirector, PopulationDirector, console) send a `SpawnRequest` to the scene `SpawnService`. It picks the card (`SpawnTable`), finds an anchor in an area (`SpawnPointPicker` + `SpawnPlacer` per medium + `SpawnVisibility`), lays out slots (`Formation` / `SpawnSquad`), queues members and wakes them from `PrefabPool` under a per-frame millisecond budget. Every spawnable prefab has a `Spawnable` root; components that care implement `ISpawnable`. A `SpawnHandle` tracks a request's members and despawns them now, when unseen, or after a delay.

**Tech Stack:** Unity 6000.6.7f1, VContainer, A* 5.4 (behind `INavmeshQuery`), UnityEngine.Pool, EditMode tests.

**Spec:** the proposal agreed in the planning conversation (2026-11-02), summarised in "Design decisions" below; the lasting reference is `docs/technicals/SPAWNING.md` (written in Task 9).

**Status (2026-11-02):** done. Deviations: flocks live in their own `AmbientSystem` (with `AmbientInstaller`); pooling stays on `SetActive` with a warm prewarm (Task 1 numbers), so dormant pooling and `InstantiateAsync` were not built; the far think rate is on by default (60 m, 0.25 s); a variant with free pooled copies is preferred when the picked one has none. The lasting reference is `docs/technicals/SPAWNING.md`.

## Global Constraints

- Folder `Assets/_Game/_Core/SpawnSystem/`, namespace `Aipocalypse.Core.SpawnSystem`, assembly `Aipocalypse.Core` (no new asmdef).
- Scene system: `SpawnInstaller : SceneInstaller` on the `GameLifetimeScope`; the strict-scope rule means WorldMap's scope must carry it.
- No statics holding state, no `Find*`, no per-instance `Update`; central ticks only; zero per-frame allocations in ticks.
- A* only behind `INavmeshQuery` (implemented by `EnemyNavigation`); Stylized Water 3 not referenced (flat water bodies by renderer bounds).
- Project code style (AGENTS.md): `_camelCase`, `[SerializeField] T _field;`, no `private`, sealed leaf classes, compact comments, Dictionary serialization.
- Renamed or retyped serialized fields keep their values (migration script, then reserialize and check `git diff`).
- Raid behaviour unchanged: the raid soak (`AgentScripts/Raids/RaidSoak.cs`) and the WorldMap smoke test still pass.

## Design decisions

| Topic | Decision |
| --- | --- |
| Spawnable root | `Spawnable` component on every spawnable prefab root: prefab, pool, state (`Pooled`/`Active`/`Retiring`), info; returns to its pool on disable |
| Hooks | `ISpawnable.OnSpawned(in SpawnInfo)` after activation, `OnDespawned()` before parking |
| Cards | `SpawnCard` (concrete, generic: variants by weight, group size, placement, category); `EnemySpawnCard : SpawnCard` adds the arrival cue. Variants retyped `EnemyBrain → Spawnable` with a migration |
| Categories | `SpawnCategory { Enemy, Animal, Bird, Fish, Item, Prop }` (items cover rewards and coins; props cover trees) |
| Placement | `SpawnPlacement` on the card: `Medium { Ground, Air, Water }`, `Navmesh { Require, Prefer, Ignore }`, height band, clearance, max slope |
| Areas | `ISpawnArea` (`Origin`, `Sample(a, b)`); `SpawnZone` (arc / annulus, moved from RaidSystem) implements it; `RingArea` struct for code |
| Visibility | `SpawnVisibility`: view cone + line of sight, footprint test for groups, recently-seen lock |
| Budget | Members wake under `SpawnSystemSettings.SpawnBudgetMs` per frame (at least one), pending members count as alive |
| Pooling | `PrefabPool` per prefab, injected through the scene resolver, parked under an inactive root, prewarm under `PrewarmBudgetMs`; strategy decided by Task 1 numbers |
| Open world | `SpawnArea` component + `PopulationDirector` (rings with hysteresis, category caps, respawn ledger); built and unit-tested, not placed in WorldMap yet |
| Ambient birds / fish | `Flock` spawnable + central `FlockSystem` boids, no tree / A* / Animator per member |

## Review Focus

1. A pooled enemy despawned while its group is still queued must not be counted twice or re-listed in the handle (handle lists each member once).
2. A request whose area has no valid point keeps waiting without spawning in view, and gives up visibly (warning) instead of spamming.
3. Retiring (dead) members stop counting as alive at once, so raid waves advance as before.
4. Scene unload / exit play: pools and handles drop destroyed instances without errors (null checks on Unity objects).
5. Water placement over a body's AABB that covers dry land must reject points whose bed is above the water level.

---

### Task 1: Measure the wake cost (phase 0)

**Files:** Create `AgentScripts/Perf/SpawnWakeProbe.cs`; Modify `docs/technicals/ARCHITECTURE.md` (Performance).

- [x] In play mode (WorldMap, `runInBackground`), prewarm goblins and time with `Stopwatch`: first wake from parking (Awake + OnEnable + Start), re-wake of a used one, and component toggles on an active goblin (Animator, FollowerEntity, BehaviorTree stop/start, renderers, colliders), plus first wake with the shared "Enemies" parent vs scene root.
- [x] Decide: `SetActive` pooling kept, plus whatever the numbers point to (warm prewarm that pays Awake/Start at load, scene-root parenting, dormant components). Record numbers and the decision.

### Task 2: Core types and pool (phase 1)

**Files:** Create `SpawnSystem/Spawnable.cs`, `ISpawnable.cs`, `SpawnInfo.cs`, `SpawnCategory.cs`, `PrefabPool.cs`, `SpawnSystemSettings.cs`, `SpawnCard.cs`, `SpawnPlacement.cs`; Modify `EnemySystem/EnemySpawnCard.cs`, `EnemyBrain.cs`; Delete `EnemySystem/EnemyPool.cs`. Test: `Tests/EditMode/SpawnCardTests.cs`, `PrefabPoolTests.cs`.

**Interfaces (produces):**
- `Spawnable`: `Spawnable Prefab`, `SpawnState State`, `bool IsAlive`, `SpawnInfo Info`, `SpawnCard Card`, `void Retire()`.
- `PrefabPool`: `Spawnable Get(Spawnable prefab, Vector3 position, Quaternion rotation)`, `void Prewarm(Spawnable prefab, int count)`, `int Made(Spawnable)`, `int Free(Spawnable)`.
- `SpawnCard`: `IReadOnlyDictionary<Spawnable, float> Variants`, `Vector2Int GroupSize`, `SpawnPlacement Placement`, `SpawnCategory Category`, `Spawnable PickVariant(float roll)`, `int PickGroupSize(float roll)`.

- [x] Tests: `Card_PicksVariantsByWeight`, `Card_GroupSize_StaysInItsRange` (moved from EnemySpawningTests), `Pool_ReusesReturnedInstances`, `Pool_PrewarmCreatesParkedInstances`, `Pool_IgnoresDoubleReturn`.
- [x] Migrate `EnemySpawnCard._variants` (EnemyBrain → Spawnable): add `Spawnable` to Goblin.prefab and WanderCapsule.prefab, copy weights via a migration script, reserialize, check `git diff`.
- [x] `EnemyBrain : ISpawnable` (teleport + appear in `OnSpawned`), `Die()` retires its `Spawnable`, reaction effects prewarmed in `Construct`.

### Task 3: SpawnService, handles, placement and visibility (phase 1)

**Files:** Create `SpawnService.cs`, `SpawnRequest.cs`, `SpawnHandle.cs`, `SpawnPlacer.cs`, `SpawnVisibility.cs`, `INavmeshQuery.cs`, `ISpawnArea.cs`, `SpawnInstaller.cs`, `SpawnCommands.cs`; Move (with `.meta`) `RaidSystem/SpawnZone.cs`, `SpawnPointPicker.cs`, `SpawnPointScorer.cs` and `SpawnRules` (from `RaidSettings.cs`) to `SpawnSystem/`; Modify `RaidSpawner.cs`, `RaidInstaller.cs`, `EnemyInstaller.cs`, `EnemyCommands.cs`, `EnemySystemSettings.cs`, `EnemyNavigation.cs`, `AgentScripts/Perf/PerfSuite.cs`, `AgentScripts/Raids/RaidNightSetup.cs`; Delete `EnemySpawner.cs`, `SpawnBatch.cs`. Test: `SpawnPointScorerTests.cs` (namespace), `SpawnHandleTests.cs`.

**Interfaces (produces):**
- `SpawnService`: `Spawnable Spawn(SpawnCard card, Vector3 position, Quaternion rotation)`, `SpawnHandle Spawn(in SpawnRequest request)`, `void Despawn(Spawnable instance, DespawnMode mode = DespawnMode.Now)`, `void Prewarm(SpawnCard card, int count)`, `void CancelQueued()`, `int Queued`, `int CountAlive(SpawnCategory)`, `int CountAlive(SpawnCard)`.
- `SpawnRequest` (struct): `Group(card, area, count)`, `At(card, position, facing)`, `.With(Formation)`, `.WithRules(SpawnRules)`, `.Facing(Vector3)`.
- `SpawnHandle`: `int Count`, `int Pending`, `int Alive`, `bool IsPlaced`, `Vector3 Anchor`, `IReadOnlyList<Spawnable> Members`, `void Cancel()`, `void Despawn(DespawnMode)`.
- `DespawnMode { Now, WhenUnseen }`.

- [x] Tests: `Handle_CountsPendingAsAlive`, `Handle_ListsAPooledMemberOnce`, `Handle_CancelDropsPending`.
- [x] WorldMap: add `SpawnInstaller` (settings `Data/Spawning/SpawnSystemSettings.asset`) to the `GameLifetimeScope`; save.
- [x] Verify: recompile clean, EditMode tests pass, raid soak plays a night with the same spawns.

### Task 4: Formations, squads and tables (phase 2)

**Files:** Create `Formation.cs`, `FormationMath.cs`, `SpawnSquad.cs`, `SpawnTable.cs`, `SpawnConditions.cs`; Modify `SpawnService.cs`, `SpawnPointPicker.cs`, `SpawnVisibility.cs`, `EnemySystem/CrowdSpread.cs` (GroupOffset moves to `FormationMath.Scatter`). Test: `FormationTests.cs`, `SpawnTableTests.cs`, `CrowdSpreadTests.cs`.

- `FormationShape { Scatter, Line, Column, Wedge, Ring, Clusters, Custom }`; `Formation.GetSlots(int count, float turn, List<Vector3> into)` in local space, slot 0 = leader, +Z = facing.
- `SpawnTable.Pick(in SpawnConditions conditions, Func<SpawnCard, int> alive, float roll)` → `SpawnCard` or null (nothing weight); entry: card, weight, day phases, day range, max alive.
- Group placement is two-phase: anchor (footprint hidden), then each slot placed by medium; a failed slot falls back to the anchor for ground walkers and is dropped otherwise.
- [x] Tests: `Line_SpacesSlotsEvenly`, `Wedge_LeaderAtTip`, `Ring_SlotsOnCircle`, `Scatter_KeepsMinSpacing`, `Table_PicksByWeight`, `Table_SkipsEntriesOutsideTheirPhaseOrDay`, `Table_RespectsMaxAlive`, `Table_NothingWeightReturnsNull`.

### Task 5: Air and water placement (phase 3)

**Files:** Create `IWaterLevel.cs`, `WaterBodies.cs`; Modify `SpawnPlacer.cs`, `SpawnInstaller.cs`, `SpawnCommands.cs`. Test: `SpawnPlacementTests.cs`.

- `SpawnPlacer.PickWaterDepth(float level, float bed, Vector2 depth, float margin, float roll, out float y)` and `PickAltitude(float surface, Vector2 height, float roll)` are pure statics.
- [x] Tests: `Water_RejectsDryAndShallowPoints`, `Water_StaysBetweenBedAndSurface`, `Air_StaysInItsBand`.
- [x] `spawn.test air|water [n]` draws placed points (gizmo lines) to check in the scene.

### Task 6: Open-world population (phase 4)

**Files:** Create `SpawnArea.cs`, `PopulationDirector.cs`, `PopulationRules.cs`; Modify `SpawnInstaller.cs`, `SpawnSystemSettings.cs` (category caps, global cap, tick interval). Test: `PopulationRulesTests.cs`.

- Area: zone, card or table, capacity, respawn delay, activation / deactivation radius (hysteresis), rules.
- Ledger per area: alive, killed waiting to respawn (timestamps); despawned-when-far members come back on re-activation.
- [x] Tests: `Area_ActivatesInsideAndDeactivatesPastHysteresis`, `Area_TopsUpToCapacity`, `Area_WaitsForRespawnAfterKills`, `Caps_LimitByCategoryAndGlobal`.

### Task 7: Scale (phase 5)

**Files:** Modify `PrefabPool.cs` (per Task 1 decision; async prewarm only if measured better), `EnemySystem/EnemyManager.cs`, `EnemySystemSettings.cs` (far think rate); Create `Flock.cs`, `FlockDefinition.cs`, `FlockSystem.cs`, `FlockMath.cs`, placeholder bird / fish flock content. Test: `FlockMathTests.cs`, `EnemyManagerTests` (far think rate).

- [x] Tests: `Flock_StaysInsideItsBand`, `Flock_SeparatesNeighbours`, `Manager_FarEnemiesThinkLessOften`.
- [x] `spawn.flock birds|fish` spawns a flock near the player through the service.

### Task 8: Verify

- [x] Recompile clean, console clean, all EditMode tests pass, PlayMode smoke test (or play probe), raid soak night, `enemy.spawn goblin 300` frame numbers recorded.

### Task 9: Document

- [x] `docs/technicals/SPAWNING.md` (how to use: recipes for enemies, groups, flyers, fish, props / items / coins / trees, population areas, budgets), ARCHITECTURE.md, CODE_STRUCTURE.md, RAIDS.md, CONSOLE.md, AGENTS.md doc list, `aipocalypse-enemy-ai` and `aipocalypse-architecture` skills.
