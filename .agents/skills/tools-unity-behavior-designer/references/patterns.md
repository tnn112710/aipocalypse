# Tree recipes and task templates (aipocalypse)

Names in `PascalCase` without a path are **our** tasks; write them in `Aipocalypse.AI.Tasks`. Names with a path (e.g. `Time/Wait`) are built-ins. `[abort: X]` is the composite's `AbortType`.

## Generic building blocks

### Patrol → chase → search
```text
Selector
├─ Sequence [abort: LowerPriority]        "Engage"
│   ├─ CanSeeTarget (Target=Player)
│   ├─ SetLastKnownPosition
│   └─ MoveToTarget
├─ Sequence [abort: LowerPriority]        "Search"
│   ├─ HasValue (LastKnownPosition)
│   ├─ MoveToPosition (LastKnownPosition)
│   ├─ Time/Wait 2–4s (random)
│   └─ ClearVariable (LastKnownPosition)
└─ PatrolWaypoints                         fallback, never fails
```

### Attack with recovery
```text
Sequence [abort: Self]
├─ WithinRange (Target, AttackRange)
├─ FaceTarget
└─ Cooldown (recovery 1.2s)
    └─ PerformAttack          ← tells the bridge to run the attack; Running until the wind-up ends
```
`Cooldown` holds the branch after the child finishes. For "attack at most every N seconds, but keep moving between attacks", use `Utility/RateLimiter` or a timestamp variable checked by a conditional.

### Flee at low health
```text
Sequence [abort: LowerPriority]
├─ HealthBelow (0.25)      ← reads the Health component cached in OnAwake
└─ FleeFrom (Target, distance 15)
```
Put it to the left of combat so it preempts combat.

### React to a gameplay event (no polling)
Bridge: MessagePipe `DamageDealt` (filtered to self) → `EventHandler.ExecuteEvent<float>(tree, "Damaged", amount)`.
```text
Selector
├─ Sequence [abort: LowerPriority]
│   ├─ HasReceivedEvent "Damaged" (stores amount → LastDamage)
│   └─ TurnToAttacker → Alert nearby (bridge publishes)
└─ …
```
Use the `OnReceivedEvent` event node instead when the reaction is a separate one-off branch (e.g. "PhaseChanged" → swap subtree).

### Phases / moods (bosses, drone)
`SubtreeReferenceSelector` with N `Subtree` assets, indexed by an int variable `Phase`. On change: `tree.SetVariableValue("Phase", 2); tree.ReevaluateSubtreeReferences();`. The bridge does this when `Health` crosses thresholds or a `TimePhaseChanged` event arrives.

### Picking the best option (needs / attacks)
`UtilitySelector`. Wrap each child in `UtilityCurveEvaluator` (curve over a 0–1 input variable the bridge or a task updates) or `UtilityVariableEvaluator` (score read from a variable). Keep the inputs normalised to 0–1.

## Crowd enemy archetypes (from docs/gdd/CORE.md)

One Subtree asset per archetype, referenced by its `EnemyArchetypeDef`. Ticked by `EnemyManager` at the think rate, so `Running` tasks are re-entered every N frames, not every frame: write them against state (`Mover.Arrived`, `Attack.IsBusy`), never per-frame accumulation. Senses come from the `EntityRegistry` grid. Sketches to agree with the user before building.

### Rusher (also the base for swarm)
```text
Selector
├─ Sequence [abort: LowerPriority]  "Attack"
│   ├─ TargetInRange (AttackRange)
│   └─ PerformAttack
└─ ChaseTarget                         fallback: Mover.SetGoal(target), Running
```

### Ranged
```text
Selector
├─ Sequence [abort: LowerPriority]  "Too close"
│   ├─ TargetInRange (MinRange)
│   └─ BackOff
├─ Sequence [abort: LowerPriority]  "Shoot"
│   ├─ TargetInRange (AttackRange) → CanSeeTarget
│   └─ PerformAttack
└─ ChaseTarget (stop at AttackRange)
```

### Sneaker
```text
Selector
├─ Sequence [abort: LowerPriority]  "Ambush": TargetInRange(PounceRange) → PerformAttack
├─ Sequence [abort: Self]           "Stalk":  TargetLooking? (inverted) → ChaseTarget (cover-biased goal)
└─ HoldPosition / Hide
```

Tank and flyer reuse Rusher with different parts/data (flyer = different `Mover` setup). Prefer a new def over a new tree; a new tree over a new task; a new task over a new part.

## Unique agents in this game (from docs/gdd/CORE.md)

These are sketches to discuss with the user. CORE.md is canon. Never design from IDEAS.md unless asked.

### Guide drone (welcomes, explains, scans portals; secretly the AI Lord's eyes)
```text
Selector
├─ OnReceivedEvent "ScanPortal" → Sequence: FlyTo(Portal) → PlayScan → ReportScan (bridge publishes result)
├─ Sequence [abort: LowerPriority]  "Comment"
│   ├─ HasReceivedEvent "Quip" (stores QuipId)
│   └─ FacePlayer → SayLine(QuipId)
└─ HoverNearPlayer (offset, bob)         fallback
```
The quips subscribe to gameplay events in the bridge, which picks a line and sends "Quip". The tree only decides *when* the drone can talk.

### Dog (companion)
```text
Selector
├─ Sequence [abort: LowerPriority] "Defend": EnemyNearPlayer(r=10) → MoveToTarget → Cooldown(0.8) → Bite
├─ Sequence [abort: LowerPriority] "Fetch":  HasReceivedEvent "Fetch" → MoveToPosition → PickUp → ReturnToPlayer
└─ FollowPlayer (keep 2–4 m, idle animations when close)
```

### Cat (raises sanity and luck; hisses to warn of sneaky enemies)
```text
Selector
├─ Sequence [abort: LowerPriority] "Warn": SneakerNearby(r=12) → FaceToward(Sneaker) → Hiss (bridge publishes warning)
└─ RandomSelector: Wander / SleepAtSpot / SitNearPlayer   (each wrapped in Time/Wait to linger)
```
`SneakerNearby` queries the EntityRegistry spatial grid. Never use `Physics.OverlapSphere` on everything.

## Task templates

### Cheap sense conditional (abort-safe)
```csharp
[Category("Aipocalypse/Senses")]
[Description("Success while the target is within range and line of sight.")]
public class CanSeeTarget : Conditional
{
    [SerializeField] SharedVariable<GameObject> _target;
    [SerializeField] SharedVariable<float> _range = 20f;
    [SerializeField] LayerMask _occluders;
    [SerializeField] float _eyeHeight = 1.6f;

    public override TaskStatus OnUpdate()
    {
        var target = _target.Value;
        if (target == null) return TaskStatus.Failure;

        var eye = m_Transform.position + Vector3.up * _eyeHeight;
        var to = target.transform.position + Vector3.up * _eyeHeight - eye;
        var range = _range.Value;
        if (to.sqrMagnitude > range * range) return TaskStatus.Failure;   // cheap test first

        return Physics.Raycast(eye, to, to.magnitude, _occluders) ? TaskStatus.Failure : TaskStatus.Success;
    }
}
```

### Action that delegates to a component (keeps logic out of the graph)
```csharp
[Category("Aipocalypse/Combat")]
public class PerformAttack : Action
{
    [SerializeField] SharedVariable<GameObject> _target;
    Attack _attack;   // our Attack part (EnemySystem)

    public override void OnAwake() => _attack = GetComponent<Attack>();
    public override void OnStart() => _attack.Begin(_target.Value);
    public override TaskStatus OnUpdate() => _attack.IsBusy ? TaskStatus.Running : (_attack.LastHit ? TaskStatus.Success : TaskStatus.Failure);
    public override void OnEnd() => _attack.CancelIfBusy();   // runs on abort too
}
```
Always this shape: the task drives a tested component, and the component holds the rules and numbers. That keeps crowd trees cheap and keeps the last-resort escape hatch (a code think step for one archetype) cheap too.

### Crowd tree ticking in `EnemyManager`
```csharp
// Crowd trees are Update Mode = Manual. Each enemy ticks every Stride frames, staggered by index; far enemies skip more.
public sealed class EnemyManager : ITickable
{
    static readonly ProfilerMarker s_Think = new("Aipocalypse.EnemyManager.Think");
    readonly List<EnemyBrain> _active = new(512);    // dense; swap-remove on despawn
    int _frame;
    const int Stride = 4;

    public void Tick()
    {
        var phase = _frame++ % Stride;
        using (s_Think.Auto())
        {
            for (int i = phase; i < _active.Count; i += Stride)
            {
                var brain = _active[i];
                if (brain.IsFar && (_frame / Stride) % 2 != 0) continue;    // distance LOD: half rate
                brain.Tree.Tick();
            }
        }
    }
}
```
No separate ticker class: `EnemyManager` stays the one central tick for enemies (ARCHITECTURE.md). `IsFar` is updated by the senses pass, not computed here. Unique agents with Manual trees get ticked by their own system the same way.
