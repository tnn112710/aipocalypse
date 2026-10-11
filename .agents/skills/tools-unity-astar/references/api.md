# A* Pathfinding Project 5.4.7: checked API

Read from the package source (`Packages/com.arongranberg.astar`). After an upgrade, re-check with `grep -nE '^\s*public ' Core/AI/FollowerEntity.cs` and read `CHANGELOG.md`. Members not listed here have not been checked, so read the source before using them.

## IAstarAI (`Core/AI/IAstarAI.cs`), implemented by FollowerEntity

| Member | Notes |
| --- | --- |
| `Vector3 destination { get; set; }` | `+∞` when unset. Setting repairs the path at once |
| `bool isStopped { get; set; }` | Smooth stop, keeps the path |
| `float maxSpeed { get; set; }` | |
| `float radius`, `float height { get; set; }` | |
| `Vector3 position { get; }` | Via the interface. FollowerEntity also has a setter |
| `Quaternion rotation { get; set; }` | |
| `Vector3 velocity`, `desiredVelocity { get; }` | Use `velocity` for animation |
| `float remainingDistance { get; }` | Along the path |
| `bool reachedDestination`, `reachedEndOfPath { get; }` | Valid immediately after a destination change |
| `Vector3 endOfPath`, `steeringTarget { get; }` | |
| `bool hasPath`, `pathPending { get; }` | |
| `bool simulateMovement`, `updatePosition`, `updateRotation { get; set; }` | `canMove` is obsolete, use `simulateMovement` |
| `bool canSearch { get; set; }` | **Obsolete on FollowerEntity**, use `autoRepath.mode` |
| `System.Action onSearchPath { get; set; }` | |
| `void SearchPath()` | Force a recalculation now |
| `void SetPath(Path path, bool updateDestinationFromPath = true)` | For custom path types (Flee, Random) |
| `void Teleport(Vector3 newPosition, bool clearPath = true)` | |
| `void Move(Vector3 deltaPosition)` | |
| `void GetRemainingPath(List<Vector3> buffer, out bool stale)` | Pass a reused list |
| `MovementUpdate` / `FinalizeMovement` | `FinalizeMovement` **throws** on FollowerEntity |

## FollowerEntity extras (`Core/AI/FollowerEntity.cs`)

- `void SetDestination(float3 destination, float3 facingDirection = default)`: arrive facing a direction.
- `bool reachedCrowdedEndOfPath`: also true when RVO neighbours block the goal.
- `float stopDistance`, `rotationSpeed`, `maxRotationSpeed`, `positionSmoothing`, `rotationSmoothing`.
- `MovementSettings movementSettings` (struct, so copy, modify, assign). Holds `follower` (PIDMovement: `speed`, `rotationSpeed`, `slowdownTime`, `desiredWallDistance`, `allowRotatingOnSpot`, `leadInRadiusWhenApproachingDestination`…), `stopDistance`, `groundMask`, `isStopped`, `debugFlags`.
- `ref PathRequestSettings pathfindingSettings`: `graphMask`, `traversableTags`, `tagCostMultipliers`, `traversalProvider`.
- `ECS.AutoRepathPolicy autoRepath`: `mode` (`Never`, `EveryNSeconds`, `Dynamic`) and `period`.
- `bool enableLocalAvoidance`, `RVOAgent rvoSettings` (`agentTimeHorizon`, `obstacleTimeHorizon`, `maxNeighbours`, `layer`, `collidesWith`, `priority`, `locked`, `debug`).
- `bool enableGravity`, `LayerMask groundMask`, `OrientationMode orientation`, `MovementPlaneSource movementPlaneSource`.
- `GraphNode currentNode`, `GraphHitInfo nearestNavmeshBorder`.
- Off-mesh links: `bool isTraversingOffMeshLink`, `OffMeshLinks.OffMeshLinkTracer offMeshLink`, `nextOffMeshLink`, `IOffMeshLinkHandler onTraverseOffMeshLink { get; set; }`.
- `ManagedMovementOverrides movementOverrides`: `AddBeforeControlCallback`, `AddAfterControlCallback`, `AddBeforeMovementCallback`. Each callback forces a sync point for all agents, and the docs call this API unstable.
- `Entity entity`, `World world`, `bool entityExists`. The entity exists only while the component is enabled.
- `void SyncWithEntity()`.

Lifecycle: `OnEnable` creates the entity; `Start` calls `Teleport(position, false)` to snap onto the navmesh; `OnDisable` destroys the entity.

Removed in 5.4.7: `ManagedState.autoRepath/rvoSettings/onTraverseOffMeshLink/pathfindingSettings/enableLocalAvoidance/enableGravity`. Use the FollowerEntity properties above instead.

## AIDestinationSetter (`Behaviors/AIDestinationSetter.cs`)

`public Transform target; public bool useRotation;`. With FollowerEntity and Entities ≥ 6.6, it adds an `AIDestinationSetterRef` component and the ECS `SyncDestinationTransformSystem` follows `target`. No per-component Update.

## AstarPath (`Core/AstarPath.cs`): only inside the INavigation service

- `static AstarPath active`
- `NNInfo GetNearest(Vector3 position)`, `GetNearest(Vector3, NearestNodeConstraint)`. `NNInfo` has `node` (null if none matched) and `position` (the closest point on the node).
- `bool GetNearestBorder(Vector3 position, NearestNodeConstraint constraint, out GraphHitInfo hit)`
- `bool Linecast(Vector3 start, Vector3 end)` and `Linecast(start, end, out GraphHitInfo hit)`. They return **true if obstructed**.
- `void UpdateGraphs(Bounds bounds)`, `UpdateGraphs(GraphUpdateObject ob)`, plus `(…, float delay)` overloads. `void FlushGraphUpdates()` blocks, so don't use it in gameplay.
- `void Scan(NavGraph[] graphsToScan = null)` / `Scan(NavGraph)`: editor tools and prototypes only.
- `static void StartPath(Path path, bool pushToFront = false)`, `static void BlockUntilCalculated(Path path)` (blocking, avoid).
- `void AddWorkItem(System.Action callback)`: run code safely between pathfinding batches (needed when changing graph data directly).

## NearestNodeConstraint (new in 5.4, replaces NNConstraint)

Fields: `distanceMetric`, `area`, `maxDistanceSqr`, `walkable` (`WalkabilityConstraint`), `tags`, `graphMask`, `traversalProvider`. Presets: `NearestNodeConstraint.Walkable`, `NearestNodeConstraint.None`. It's a struct, so copy a preset and set `maxDistanceSqr` etc.

## Path types (`Pathfinders/`)

`ABPath.Construct(start, end, callback)`, `FleePath.Construct(start, avoid, searchLength, callback)`, `RandomPath.Construct(start, length, callback)`, `ConstantPath.Construct(start, maxGScore, callback)` (every node within a cost, for area queries), `MultiTargetPath.Construct(start, targets[], …)`. Feed to an agent with `agent.SetPath(path)`, after setting `autoRepath.mode = Never` or the agent will repath back to its old destination.

## PathUtilities (`Utilities/PathUtilities.cs`)

`IsPathPossible(GraphNode a, GraphNode b)` (same connected component, O(1)), `IsPathPossible(List<GraphNode>, int tagMask)`, `GetReachableNodes(seed, tagMask, filter)` (allocates a list), `BFS(seed, depth, …)`, `GetPointsAroundPointWorld(p, graph, previousPoints, radius, clearanceRadius)` (spread squad destinations), `FormationPacked(positions, destination, clearanceRadius, movementPlane)`.

## NavmeshCut (`Navmesh/NavmeshCut.cs`)

`type` (`MeshType.Box`, `Circle`, `CustomMesh`…), `circleRadius`, `circleResolution`, `height`, `mesh`, `meshScale`, `updateDistance` (re-cut after moving this far), `updateRotationDistance`, `useRotationAndScale`, `isDual`, `cutsAddedGeom`, `radiusExpansionMode` (default `ExpandByAgentRadius`). Works on recast/navmesh graphs only (tiled). For grid graphs use graph updates.

## Time

`AIMovementSystemGroup` (ECS system group) has `double CustomTimeScale` (default 1). It runs multiple substeps when `timeScale > 1` or fps is low.
