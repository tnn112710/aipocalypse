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
Prefer this shape: the task drives a tested component, and the component holds the rules and numbers. Then the same `Attack` part serves code-driven crowd enemies too.

### Manual ticking from a manager (only if trees ever run on many agents)
```csharp
// Set each tree's Update Mode to Manual. Stagger so each tree thinks every N frames.
public class TreeTicker : ITickable
{
    readonly List<BehaviorTree> _trees = new(64);
    static readonly ProfilerMarker Marker = new("TreeTicker.Tick");
    int _frame;
    const int Stride = 4;

    public void Add(BehaviorTree tree) => _trees.Add(tree);
    public void Remove(BehaviorTree tree) => _trees.Remove(tree);

    public void Tick()
    {
        using var _ = Marker.Auto();
        var phase = _frame++ % Stride;
        for (int i = phase; i < _trees.Count; i += Stride) _trees[i].Tick();
    }
}
```
Register it in the system's installer (`builder.RegisterEntryPoint<TreeTicker>().AsSelf()`). Measure in `Proto_Stress` before adopting it for crowd enemies.
