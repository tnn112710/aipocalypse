---
name: tools-unity-astar
description: A* Pathfinding Project 5.4 (Aron Granberg) for aipocalypse — FollowerEntity agents (ECS-backed, RVO), scene/graph setup, driving hundreds of enemies from EnemyManager, pooling, dynamic obstacles (NavmeshCut, graph updates), navmesh queries, off-mesh links, performance and debugging. Use for any enemy/companion movement or pathfinding work. NOT Unity NavMesh (NavMeshAgent) and NOT the A* 4.x movement API (AIPath+Seeker as default, canSearch, NNConstraint-first examples).
---

# A* Pathfinding Project (aipocalypse)

Installed: **A\* Pathfinding Project 5.4.7** (`Packages/com.arongranberg.astar`, full C# source) on Unity 6000.6, Entities 6.6 (so `MODULE_ENTITIES` and `MODULE_ENTITIES_6_6_0_OR_NEWER` are on). Namespace `Pathfinding` (ECS parts: `Pathfinding.ECS`, `Pathfinding.ECS.RVO`).

Before writing code, read [references/api.md](references/api.md). It lists the API checked against the 5.4.7 source. Most online examples are 4.x (`AIPath` + `Seeker`, `canSearch`, `NNConstraint`) and either won't compile against `FollowerEntity` or are the slow path. When unsure, read the source: `Core/AI/FollowerEntity.cs` (its class doc is the best manual), `Core/AI/IAstarAI.cs`, `Core/AstarPath.cs`, `CHANGELOG.md` (5.4 breaking changes). Online docs: https://arongranberg.com/astar/docs/ (offline copy: `Documentation/documentation.html`).

## 1. Which movement script

| Agent | Use |
| --- | --- |
| Crowd enemies (hundreds), companions, bosses, drone on the ground | **`FollowerEntity`** (ARCHITECTURE.md). ECS/Burst movement inside, our code stays MonoBehaviour |
| Flying agents (drone, flyers) | No graph movement. Steer in code over the spatial grid; use `AstarPath` linecasts only if they must avoid walls |
| `AIPath`, `RichAI`, `AILerp`, `Seeker`, `RVOController` | Don't add. Legacy 4.x-style scripts. `FollowerEntity` must **not** have a `Seeker` or `RVOController` (it has its own `pathfindingSettings` and `rvoSettings`) |
| Unity `NavMeshAgent` / AI Navigation package | Not used for agents (see PLUGINS.md) |

Don't bake `FollowerEntity` into ECS subscenes. Subscene baking removes the MonoBehaviour and needs `FollowerEntityProxy` plus our own ECS code, which breaks "no ECS in our code". Consider it only if `Proto_Stress` proves GameObject overhead is the bottleneck, and get the user's sign-off.

## 2. Scene setup

Every scene with walking agents needs these, otherwise agents throw or don't move:
- **`AstarPath`** (the A* inspector object, holds the graphs). Exactly one per scene.
- **`RVOSimulator`**, only if any agent has local avoidance on. Without it, RVO silently does nothing (`RVOSystem` reads `RVOSimulator.active`).

Package both as one `Navigation` prefab under `Assets/_Game/Prefabs/` and drop it into World and every `Proto_*` scene that has agents. Entity prefabs never carry these (scene-level, one instance).

Graphs:
- **World**: `RecastGraph` (3D terrain + props). Tile it (tile size ~ 64–128 voxels) so local updates and navmesh cuts are cheap. Set the layer mask so props that shouldn't block (grass, small debris, triggers) are excluded. A recast scan includes terrain trees (filtered by layer/tag since 5.4.5). Check that GPUI-instanced trees still have colliders where agents must route around them.
- **Cache the World graph** (A* Inspector → Save & Load → Generate cache, enable "Cache startup") so scenes don't rescan on load. Rescan or regenerate the cache whenever level geometry changes.
- **Prototype scenes**: a small recast or grid graph with **Scan on Awake** is fine.
- Multiple agent sizes (swarm vs tank) means one graph per size, each with its own agent radius. The agent picks its graph with `pathfindingSettings.graphMask`.

## 3. Agent setup (FollowerEntity inspector)

- Shape: `radius` / `height` match the collider capsule.
- Movement: `maxSpeed`, `rotationSpeed`, `stopDistance`, `slowdownTime`, `desiredWallDistance`. Copy these from `EnemyArchetypeDef` at spawn, never hardcoded in the prefab. Settings that are structs (`movementSettings`, `pathfindingSettings`) are copied, so read, modify, then assign back.
- `autoRepath.mode = Dynamic` (default, repaths more often when close to the destination). Use `Never` only for agents whose paths we start manually.
- `enableLocalAvoidance`: on for crowd enemies (ARCHITECTURE.md). `rvoSettings.layer` / `collidesWith` separate swarms from the player or companions. `rvoSettings.priority` lets tanks push swarmers.
- `enableGravity` + `groundMask`: keep gravity on for walkers. The ground mask must hit terrain and walkable props only.
- Debug: set `debugFlags` and `rvoSettings.debug` to none on prefabs. They cost CPU in the editor with many agents.
- Movement is not root motion. Drive the Animator from `velocity` (magnitude for blend speed), with in-place animations.

## 4. Driving agents from code

Write movement code against **`IAstarAI`** (`FollowerEntity` implements it) unless you need something only `FollowerEntity` has (`SetDestination` with facing, `reachedCrowdedEndOfPath`, `currentNode`, `nearestNavmeshBorder`).

```csharp
using Pathfinding;
using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // Enemy part. EnemyManager calls Think() time-sliced; no Update() of its own.
    public sealed class Mover : MonoBehaviour
    {
        [SerializeField] FollowerEntity _agent;
        Vector3 _lastDestination;

        // Not "Reset": Unity treats Reset as an editor message and errors on parameters.
        public void Spawn(EnemyArchetypeDef def, Vector3 position)
        {
            _agent.maxSpeed = def.MoveSpeed;
            _agent.stopDistance = def.AttackRange * 0.8f;
            _agent.Teleport(position);          // long jump: always Teleport, never set position
            _agent.isStopped = false;
            _lastDestination = new Vector3(float.PositiveInfinity, 0f, 0f);
        }

        // Called at the enemy's think rate, not every frame.
        public void SetGoal(Vector3 goal, float repathThresholdSqr)
        {
            if ((goal - _lastDestination).sqrMagnitude < repathThresholdSqr) return;   // skip tiny moves
            _lastDestination = goal;
            _agent.destination = goal;
        }

        public bool Arrived => _agent.reachedCrowdedEndOfPath;   // true also when blocked by a crowd at the goal
        public void Halt() => _agent.isStopped = true;
    }
}
```

Rules:
- **Property access is not free.** Each `FollowerEntity` get/set syncs with the movement jobs. With hundreds of agents, touch each agent's properties only when its think tick runs, and only when the value changes. Read position from `transform.position`, which is synced when `updatePosition` is on, not `agent.position`.
- **Don't set `destination` every frame.** Setting it repairs the path immediately. A moving target every 4–10 frames looks the same and costs far less. Scale the interval with distance (ARCHITECTURE.md → time-slicing / distance LOD).
- **Shared target (everyone chases the player):** `AIDestinationSetter` with `target = player transform` is synced inside ECS (`SyncDestinationTransformSystem`) and is cheaper than setting `destination` from C#. It adds a component per enemy, but it's an A* component the ECS side reads directly. Set `target` at spawn from `IPlayerLocator`; null it on despawn.
- Arrival checks: `reachedDestination` (within `stopDistance` of the destination; may never become true if unreachable), `reachedEndOfPath` (end of the computed path), `reachedCrowdedEndOfPath` (also true when RVO neighbours block the goal, which is what melee crowds want). They are valid right after setting `destination`, with no need to wait a frame.
- `isStopped = true` brakes smoothly and keeps the path. To freeze instantly for stuns or ragdolls, set `simulateMovement = false` and disable `updatePosition` if physics or animation takes over. Restore both after.
- `Teleport(pos)` for spawns and long jumps (clears the path by default). `position = x` only for tiny corrections.
- Agents need the graph scanned before `Start` (the component teleports onto the navmesh in `Start`). Spawning after the scene's `AstarPath` has loaded is always fine.

## 5. Pooling

`FollowerEntity` **creates its ECS entity in `OnEnable` and destroys it in `OnDisable`**. So:
- Pool by `SetActive(false/true)` as for any pooled enemy, then call `Teleport(spawnPos)` **after** re-enabling, and re-apply per-spawn settings (speed, stop distance, RVO layer). Settings set while disabled are kept in serialized backing fields, but apply them after enable to be safe.
- Entity creation per spawn is cheap but not zero. If `Proto_Stress` shows spawn spikes from it, keep pooled enemies active but parked (`isStopped`, `simulateMovement = false`, `enableLocalAvoidance = false`, moved out of the world) instead of deactivated. Measure first.
- Clear `AIDestinationSetter.target` and any `movementOverrides` callbacks on despawn.

## 6. Navigation service (no `AstarPath.active` in gameplay code)

`AstarPath.active` and `RVOSimulator.active` are plugin singletons. Only one Core service touches them, registered by the EnemySystem installer:

```csharp
public interface INavigation
{
    bool TryGetNearestWalkable(Vector3 point, float maxDistance, out Vector3 onNavmesh);
    bool IsReachable(Vector3 from, Vector3 to);          // same connected area; no path search
    bool HasClearPath(Vector3 from, Vector3 to);         // navmesh linecast (no physics)
    void InvalidateArea(Bounds bounds);                  // after level geometry changes
}
```

Implement it with `AstarPath.active.GetNearest(point, NearestNodeConstraint.Walkable)` (check `info.node != null` and the distance), `PathUtilities.IsPathPossible(nodeA, nodeB)`, `AstarPath.active.Linecast(a, b)` (returns **true if blocked**), and `AstarPath.active.UpdateGraphs(bounds)`. Use it for spawn-point validation (snap spawn positions onto the navmesh), "can the enemy reach the player" checks and flee targets. All of these are synchronous and allocation-free in the simple overloads. Never call them per frame for every enemy.

## 7. Dynamic obstacles (barricades, doors, built walls, fire)

| Need | Use |
| --- | --- |
| Object blocks walking on a recast/navmesh graph (barricade, crate pile, door closed) | **`NavmeshCut`** on the object. Updates only affected tiles, no rescan. Enable/disable the component to toggle |
| Area becomes costlier or forbidden for some agents (fire, traps) | Tag regions (`GraphUpdateScene` component or `GraphUpdateObject` with `setTag`), then per-agent `pathfindingSettings.traversableTags` / `tagCostMultipliers` |
| Level geometry changes permanently | `INavigation.InvalidateArea(bounds)` → `AstarPath.active.UpdateGraphs(bounds)` |

Agents with stale paths repath automatically after graph updates (`hasPath` is false until they do). Batch updates. `UpdateGraphs` is queued and runs threaded. Don't flush (`FlushGraphUpdates`) in gameplay.

## 8. Time, pause and the TimeService

FollowerEntity simulates in the ECS world's time, which follows `Time.timeScale`. Pausing through `TimeService` therefore freezes agents with no extra code. `AIMovementSystemGroup.CustomTimeScale` scales agent movement separately. Don't touch it outside `TimeService`, and only if a design needs enemies slowed independently (e.g. a "bullet time for enemies" perk).

## 9. Off-mesh links (jumps, ladders, windows)

`NodeLink2` components connect two points; `FollowerEntity` traverses them automatically with a straight move. For custom traversal (jump arc, vault animation), implement `IOffMeshLinkHandler` and set `agent.onTraverseOffMeshLink`, or set a handler per link. Use `agent.isTraversingOffMeshLink` / `nextOffMeshLink` to trigger animations. Local avoidance is disabled during traversal (`localAvoidanceTemporarilyDisabled`).

## 10. Performance checklist

- No `Update()` per enemy. `EnemyManager` ticks and sets destinations at the think rate (ARCHITECTURE.md).
- Minimise `FollowerEntity` property access per frame (§4). Avoid `movementOverrides` callbacks on crowd agents: any callback creates a sync point for **all** agents.
- RVO: `maxNeighbours` ~10, smaller `agentTimeHorizon` for dense swarms. Disable avoidance on far or idle enemies.
- Turn off all debug rendering on agent prefabs. In builds, enable `ALINE_EXCLUDED_IN_BUILD` (A* Inspector → Optimization) unless graph gizmos are needed in player builds.
- Profile in `Proto_Stress` with Burst on and the Scene view's A* gizmos off. Look at the `AIMovementSystemGroup`, `RVOSystem` and pathfinding threads, not only the main thread.

## 11. Debugging

- Scene view: A* Inspector → Settings → Debug (path debug, graph coloring, `Graph Update Debug Mode` shows update bounds). Per agent: `debugFlags` (path, corners, stop distance) and `rvoSettings.debug`.
- Agent "Debug Info" foldout shows `reachedDestination`, `hasPath`, `pathPending`, `remainingDistance` live.
- CLI: `unity command console` for A* errors. `run_script` to scan graphs or count nodes in edit mode.
- Common failures: agent doesn't move (no `AstarPath` in scene / graph not scanned / destination never set / `isStopped` left true); RVO no effect (no `RVOSimulator`); agent slides under props (props not in the recast layer mask); agent stuck at spawn (spawn point off the navmesh, so snap with `TryGetNearestWalkable`); agents freeze when the game is paused for UI but must keep moving (TimeService pause semantics, not an A* bug).
