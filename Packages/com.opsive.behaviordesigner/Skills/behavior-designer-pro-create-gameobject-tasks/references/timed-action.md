# Timed managed action

Use this example when a task must remain Running over multiple updates and reset on re-entry. Save it as `TimedFlagTask.cs` in a runtime scripts folder. Bind Active to a Graph Boolean and test it using a conditional-abort tree. Duration defaults to one second. Negative durations are treated as zero.

```csharp
/// <summary>Holds a shared flag true for a duration and clears it on termination.</summary>
public sealed class TimedFlagTask : Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Action
{
    [UnityEngine.Tooltip("How long the flag remains active in seconds.")]
    [UnityEngine.SerializeField] private Opsive.GraphDesigner.Runtime.Variables.SharedVariable<float> m_Duration = 1f;
    [UnityEngine.Tooltip("Bind to the graph flag that records active work.")]
    [UnityEngine.SerializeField] private Opsive.GraphDesigner.Runtime.Variables.SharedVariable<bool> m_Active = false;
    private float m_Deadline;

    /// <summary>Starts a fresh timed operation.</summary>
    public override void OnStart()
    {
        m_Deadline = UnityEngine.Time.time + UnityEngine.Mathf.Max(0f, m_Duration.Value);
        m_Active.Value = true;
    }

    /// <summary>Checks whether the duration has elapsed.</summary>
    /// <returns>Running until the deadline, then Success.</returns>
    public override Opsive.BehaviorDesigner.Runtime.Tasks.TaskStatus OnUpdate()
    {
        return UnityEngine.Time.time < m_Deadline
            ? Opsive.BehaviorDesigner.Runtime.Tasks.TaskStatus.Running
            : Opsive.BehaviorDesigner.Runtime.Tasks.TaskStatus.Success;
    }

    /// <summary>Clears the owned flag after completion or interruption.</summary>
    public override void OnEnd()
    {
        m_Active.Value = false;
    }
}
```

Verify Active becomes true during execution, false after completion, and false after a conditional abort. Re-entering starts a new deadline. This example uses Unity's scaled game time: a tree-only pause does not freeze that clock. If pause-excluded duration is required, implement the task's pause/resume behavior or use the built-in Wait task, which accounts for pauses. Do not interpret this example as a general pause-aware timer.
