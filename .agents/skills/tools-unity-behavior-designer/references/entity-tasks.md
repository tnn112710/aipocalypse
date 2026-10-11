# Entity (ECS/Burst) tasks

**Default: don't write these.** The project keeps its own code as MonoBehaviours (ARCHITECTURE.md: "without ECS in our own code"), and GameObject tasks are Opsive's recommended default. Write an Entity task only if both of these hold:
1. A development player build profile shows that GameObject *task execution* is the bottleneck. If traversal is the cost, the task type won't change it.
2. The user agrees to break the "no ECS in our code" rule for that behaviour. Record the decision in ARCHITECTURE.md.

Entity tasks can't touch MonoBehaviours or managed objects. They read ECS component data and the tree's shared-variable buffer only. A tree may mix Entity and GameObject tasks. Pure ECS tasks show a green border in the editor, which is not an error.

## Anatomy (taken from built-in `Time/Wait` and the `HasTarget` sample)

An Entity task is made of four parts:

1. **Authoring class**: `ECSActionTask<TSystem, TBufferElement, TFlag>` (or `ECSConditionalTask` / `ECSCompositeTask` / `ECSDecoratorTask`). It holds the inspector fields and registers its shared variables.
2. **Buffer element**: `struct X : IBufferElementData` with `ushort Index` (the task's runtime index) plus the data or variable indices it needs. Add `[InternalBufferCapacity(1)]`.
3. **Flag**: `struct XFlag : IComponentData, IEnableableComponent {}`. It is enabled while the task is active.
4. **System**: `[DisableAutoCreation] partial struct XTaskSystem : ISystem`. BD creates it in its own group. It queries `DynamicBuffer<TaskComponent>`, the buffer element and the flag together with `EvaluateFlag`. It moves `TaskComponent.Status` through **Queued → Running → Success/Failure** and writes the element back.

```csharp
public class MoveForward : ECSActionTask<MoveForwardTaskSystem, MoveForwardComponent, MoveForwardFlag>
{
    [SerializeField] SharedVariable<float> _duration = 1f;
    ECSSharedVariableIndex<float> _durationIndex;

    public override int AddBufferElement(World world, Entity entity, ECSVariableRegistry registry, GameObject gameObject)
    {
        _durationIndex = new ECSSharedVariableIndex<float>(registry.Register(_duration));
        return base.AddBufferElement(world, entity, registry, gameObject);
    }

    public override MoveForwardComponent GetBufferElement() => new()
    {
        Index = RuntimeIndex,
        DurationVariableIndex = _durationIndex.Index,
    };
}

[InternalBufferCapacity(1)]
public struct MoveForwardComponent : IBufferElementData
{
    public ushort Index;
    public int DurationVariableIndex;
    public double StartTime;
}

public struct MoveForwardFlag : IComponentData, IEnableableComponent { }

[DisableAutoCreation]
public partial struct MoveForwardTaskSystem : ISystem
{
    EntityQuery _query;

    void OnCreate(ref SystemState state) =>
        _query = SystemAPI.QueryBuilder().WithAllRW<TaskComponent>().WithAllRW<MoveForwardComponent>()
            .WithAll<MoveForwardFlag, EvaluateFlag, SharedVariableElement>().Build();

    // UnityEngine.Time.time keeps ECS and managed tasks on the same frame boundary (as in the built-in Wait).
    void OnUpdate(ref SystemState state) =>
        state.Dependency = new Job { Now = UnityEngine.Time.time }.ScheduleParallel(_query, state.Dependency);

    [BurstCompile]
    partial struct Job : IJobEntity
    {
        public double Now;

        void Execute(ref DynamicBuffer<TaskComponent> tasks, ref DynamicBuffer<MoveForwardComponent> elems, DynamicBuffer<SharedVariableElement> vars)
        {
            for (int i = 0; i < elems.Length; ++i)
            {
                var e = elems[i];
                var task = tasks[e.Index];
                if (task.Status == TaskStatus.Queued)
                {
                    task.Status = TaskStatus.Running;
                    e.StartTime = Now;
                    elems[i] = e;
                }
                if (task.Status == TaskStatus.Running && e.StartTime + vars.Get<float>(e.DurationVariableIndex) <= Now)
                    task.Status = TaskStatus.Success;
                tasks[e.Index] = task;
            }
        }
    }
}
```

## Conditionals with aborts

Implement `IReevaluateResponder` (`ReevaluateFlag`, `ReevaluateSystemType`) and add a second system. That system checks `taskComponent.Reevaluate` and the branch's `CanExecute`, and writes `Status` only when it changes. Copy the pattern from `Samples~/Scripts/EntitiesScene/Tasks/HasTarget.cs`. Systems that need structural `EntityManager` access, as `HasTarget` does, run on the main thread with `foreach (… in SystemAPI.Query<…>())` instead of a job.

## Extras

- Implement `IPausableTask` for pause/resume (shift stored times) and `ISavableTask` for save/load. See built-in `Wait`.
- Implement `ICloneable` if the task must work inside pooled subtrees. Clone shared variables with `?.Clone()`.
- Asmdef references: `Unity.Entities`, `Unity.Burst`, `Unity.Collections`, `Unity.Mathematics`. The Opsive runtime DLLs are auto-referenced.
- Docs: https://opsive.com/support/documentation/behavior-designer-pro/new-tasks/entity-task/ and `…/entity-baking/`. Samples: `Samples~/Scripts/EntitiesScene/**`.
