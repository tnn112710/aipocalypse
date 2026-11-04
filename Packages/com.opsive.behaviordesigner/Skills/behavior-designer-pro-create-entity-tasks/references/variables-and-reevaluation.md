# Entity variables and conditional reevaluation

Read this when an Entity task must read changing Shared Variable values or participate in conditional aborts.

## Register values before constructing data

`Opsive.GraphDesigner.Runtime.Variables.ECSVariableRegistry.Register<T>` returns an index for a supported unmanaged `SharedVariable<T>`. Override the authoring task's `AddBufferElement(Unity.Entities.World, Unity.Entities.Entity, ECSVariableRegistry, UnityEngine.GameObject)`, register the fields, then call the base method. The base calls `GetBufferElement()`, so registering afterward supplies incorrect indices.

Store the returned index (or `Opsive.GraphDesigner.Runtime.Variables.ECSSharedVariableIndex<T>`) in the task buffer. Use `ECSSharedVariableIndex<T>.Get(DynamicBuffer<SharedVariableElement>)` and `.Set(buffer, value)` in runtime logic. Both types are in `Opsive.GraphDesigner.Runtime.Variables`. Read current values from the entity's variable buffer; copying `.Value` once into a constant config field does not implement live binding.

The installed `Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Time.Wait` is an example: its `AddBufferElement` registers Duration and other values before calling base, and its task system reads from `DynamicBuffer<SharedVariableElement>`. Mirror the installed pattern only for the fields your task needs. Check the registry's supported representations; `where T : unmanaged` alone is not a guarantee that an arbitrary struct is supported. Do not place a managed SharedVariable wrapper, string, GameObject, or Transform in an unmanaged task buffer.

Declare the variable buffer read-only when only observing it. A writer must declare write access and preserve dependencies; otherwise Unity's safety checks and execution ordering can be wrong. From gameplay code, prefer `BehaviorTree.SetVariableValue<T>` to update bound data through the managed/ECS synchronization path.

## Preserve both predicate paths

Generate the installed `ECSConditionalNode` template. It supplies:

1. `Opsive.BehaviorDesigner.Runtime.Tasks.IReevaluateResponder` on the authoring node, with `ReevaluateFlag` and `ReevaluateSystemType`.
2. A normal task system that checks executable branches and Queued/Running status.
3. A separate enableable reevaluation flag and system. That system queries its flag plus EvaluateFlag, checks `BranchComponent.CanExecute` and `TaskComponent.Reevaluate`, then writes the new predicate status.

Use the same side-effect-free predicate and current input data in both paths. Do not gate reevaluation solely on normal Queued status: previously visited conditions need reevaluation while another task is active. If the task needs explicit interruption behavior, inspect the installed interrupt responder contract and an existing task with equivalent cleanup before extending it.

## Verification recipe

Create a Graph Boolean `Alert`. A Selector chooses between a high-priority Sequence (the generated condition followed by Wait 30 seconds) and an idle Wait 30 seconds. Set the high-priority Sequence's Abort Type to Both. Bind the generated condition to Alert and compare against true.

With Alert false, idle runs. Change Alert through `SetVariableValue<bool>` to true, false, and true while the tree remains active. Confirm active-branch transitions in both directions and no stale values. Repeat with two trees holding different Alert values and two instances of the conditional in one tree. Compilation and one successful initial predicate evaluation do not establish reevaluation correctness.
