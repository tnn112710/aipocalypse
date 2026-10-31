# Tree specs: Behavior Designer Pro trees as text

`scripts/TreeKit.cs` turns an indented text spec into a `Subtree` asset and any `Subtree` (or the tree on a prefab) back into the same text. Generated trees keep their spec in the repo (`AgentScripts/Enemies/<Name>.tree.txt`), so a behaviour is reviewed as a diff, not as a binary blob.

```bash
K=.agents/skills/aipocalypse-enemy-ai/scripts/TreeKit.cs
unity command script run --file $K --entry TreeKit.Tasks    --args '["Movement"]'     # what blocks exist, with fields
unity command script run --file $K --entry TreeKit.Check    --args '["AgentScripts/Enemies/Melee.tree.txt"]'
unity command script run --file $K --entry TreeKit.Build    --args '["AgentScripts/Enemies/Melee.tree.txt"]'
unity command script run --file $K --entry TreeKit.Describe --args '["Assets/_Game/Content/Enemies/_Shared/Wander.asset"]'
```

(Git Bash: add `--project-path "$(cygpath -w "$PWD")"`; `--result-only` prints just the text.)

## Format

```text
# Comment lines start with #.
tree Assets/_Game/Content/Enemies/_Shared/Thief.asset      the Subtree asset to write (created, or rebuilt in place keeping its GUID)
var GameObject Target                                       graph variables: var <Type> <Name> [= value] [internal]
var float Fear = 0.2

Repeater "Live"                                             <Task> [Field=Value …] [!disabled] ["Display name"] [// comment]
  Selector
    Sequence AbortType=LowerPriority "Flee"                 children are indented under their parent (any consistent indent)
      FloatComparison Float1={Fear} Operation=GreaterThan Float2=0.5
      Flee Target={Target} FleedDistance=15                 Movement Pack task, see below
    SubtreeReference Subtrees=[Assets/_Game/Content/Enemies/_Shared/Wander.asset]
OnReceivedEvent EventName="Bell" "Bell rang"                more top-level lines are event branches, one child each
  Cooldown Duration=1.5
    Idle                                                    (a GoHome task of ours would go here)
```

- **Root:** one top-level task becomes the `Start` branch. Other top-level lines must be event nodes (`OnReceivedEvent`, `OnBehaviorTreeStopping`…), each with exactly one child. `Start` can also be written explicitly.
- **Task names:** the class name (`ChaseTarget`, `Sequence`), or more of the namespace when two packages share it (`MovementPack.Runtime.Tasks.Seek`). Samples and demo assemblies are ignored. `TreeKit.Tasks` lists everything with its category and fields.
- **Fields:** the serialized field without `m_` / `_` (`AbortType`, `RepeatForever`, `Duration`). Values: `true`, numbers (`.` decimals), enum names (`A|B` for flags), `"strings"`, `(x,y,z)` vectors, asset paths for objects, `[a,b]` arrays, `new:TypeName` for a polymorphic field (`Pathfinder=new:EnemyMoverPathfinder`), `json:Type{…}` for other serializable structs. Unknown fields fail with the field list.
- **Shared variables:** `Field={Name}` binds to the graph variable `Name` (`{Name@GameObject}` for another scope); a literal sets the value. A field typed as the untyped `SharedVariable` (Iterator's `List`, OnReceivedEvent's `StoredValue1`) can only bind to a `var` declared in the spec.
- **Subtree references:** `SubtreeReference Subtrees=[path] Variables=[SubVar:{ParentVar}, Speed:3]` maps the referenced subtree's variables to the parent's (or to literals).
- **Stacked tasks:** a stackable `Action`/`Conditional` (most Movement Pack and many built-in tasks) only lives inside a `StackedAction`/`StackedConditional` node. Write it as a plain line and Build wraps it; several in one node: `StackedAction Tasks=[Seek(Target={Food}),Wait(Duration=2)]`.
- **Layout:** Build lays the tree out top-down (positions aren't part of the spec). Display names and `//` comments are kept and shown in the Behavior Designer window.

## Rules

- **A spec starts with its design answers** as `#` comments (SKILL.md §0 step 3: job, unaware, notices, then, warns, gives up), followed by how its branches work.
- **Generated trees are edited in their spec, never in the window**: Build overwrites window edits. A tree someone draws by hand stays hand-drawn; `Describe` gives its text for review and design discussions.
- `Check` before `Build`; `Build` returns the asset described back, so compare it with the spec. Commit the spec and the asset together.
- Renaming a task class breaks every asset that stores it (SKILL.md §3), but a spec survives: fix the name in the spec and rebuild.
- Not supported: `TaskDelegate` (the samples' reflected method calls; write a task), group boxes, per-node colours. `Describe` marks anything it can't express as `<unsupported:…>` or `<Type>`.
