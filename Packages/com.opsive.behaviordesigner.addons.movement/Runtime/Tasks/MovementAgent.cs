/// ---------------------------------------------
/// Movement Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.MovementPack.Runtime.Tasks
{
    using Opsive.BehaviorDesigner.AddOns.Shared.Runtime.Pathfinding;
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
    using Opsive.BehaviorDesigner.Runtime.Utility;
    using Opsive.GraphDesigner.Runtime.Variables;
#if OPSIVE_SHARED_V3
    using Opsive.Shared.Runtime.Utility;
    using Opsive.Shared.Runtime.Serialization;
    using Serialization = Opsive.Shared.Runtime.Serialization.Serialization;
    using MemberVisibility = Opsive.Shared.Runtime.Serialization.MemberVisibility;
#else
    using Opsive.Shared.Utility;
#endif
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// Navigation-capable base class for all Movement Pack tasks.
    /// </summary>
    [HideNameInTaskControl]
    [Category("Movement Pack")]
    public abstract class MovementAgent : Action
    {
#if UNITY_EDITOR
        private const string c_PathfinderTypeKey = "Opsive.BehaviorDesigner.AddOns.PathfinderType";
        public static string PathfinderTypeKey => c_PathfinderTypeKey;
#endif

        [Tooltip("Specifies the base pathfinding implementation that should be used.")]
        [SerializeField] [HideInInspector] protected Pathfinder m_Pathfinder;
        [Tooltip("Should the pathfinder be stopped when the task ends?")]
        public SharedVariable<bool> m_StopOnTaskEnd = true;
        [Tooltip("Should an invalid provider path or a rejected Seek destination cause the task to fail?")]
        [SerializeField] protected SharedVariable<bool> m_FailOnInvalidPath = true;
        [Tooltip("Should the task fail after a maximum duration?")]
        [SerializeField] protected SharedVariable<bool> m_UseTimeout = false;
        [Tooltip("The maximum task duration when timeout is enabled.")]
        [SerializeField] protected SharedVariable<float> m_Timeout = 10f;
        [Tooltip("Should a task fail when the agent stops making progress along an active path?")]
        [SerializeField] protected SharedVariable<bool> m_UseStuckDetection = true;
        [Tooltip("The duration without meaningful progress before the task is considered stuck.")]
        [SerializeField] protected SharedVariable<float> m_StuckDuration = 1.5f;
        [Tooltip("The world-space movement required to count as path progress.")]
        [SerializeField] protected SharedVariable<float> m_MinimumProgressDistance = 0.05f;
        [Tooltip("The delay after the initial destination request before an invalid path can fail the task.")]
        [SerializeField] protected SharedVariable<float> m_PathValidationGrace = 0.2f;

        public Pathfinder Pathfinder { get => m_Pathfinder; set => m_Pathfinder = value; }

        protected Vector3 Velocity { get => m_Pathfinder.Velocity; }
        protected float RemainingDistance { get => m_Pathfinder.RemainingDistance; }

        /// <summary>
        /// Whether the task changes locomotion rather than only querying navigation.
        /// </summary>
        protected virtual bool ControlsMovement => true;

        /// <summary>
        /// Returns active action time excluding pauses.
        /// </summary>
        /// <returns>The elapsed active time.</returns>
        protected float ActiveElapsedTime()
        {
            return Mathf.Max(0f, Time.time - m_StartTime - m_AccumulatedPauseTime - (m_PauseStartTime >= 0f ? Time.time - m_PauseStartTime : 0f));
        }

        private bool m_PathfinderValid;
        private bool m_HasRequestedDestination;
        private bool m_WasPaused;
        private Vector3 m_LastProgressPosition;
        private float m_StartTime;
        private float m_LastProgressTime;
        private float m_PathValidationStartTime;
        private float m_PauseStartTime = -1f;
        private float m_AccumulatedPauseTime;

        /// <summary>
        /// The task has been initialized.
        /// </summary>
        public override void OnAwake()
        {
            base.OnAwake();

            if (m_Pathfinder == null) {
                m_Pathfinder = new NavMeshAgentPathfinder();
                m_Pathfinder.Reset();
            }
            m_Pathfinder.Initialize(gameObject);
            m_PathfinderValid = m_Pathfinder.IsValid;
        }

        /// <summary>
        /// Starts the task.
        /// </summary>
        public override void OnStart()
        {
            m_PathfinderValid = m_Pathfinder != null && m_Pathfinder.IsValid;
            m_HasRequestedDestination = false;
            m_WasPaused = false;
            m_StartTime = Time.time;
            m_LastProgressTime = Time.time;
            m_LastProgressPosition = transform.position;
            m_PauseStartTime = -1f;
            m_AccumulatedPauseTime = 0f;
            if (ControlsMovement && m_PathfinderValid) { m_Pathfinder.OnStart(); }
        }

        /// <summary>
        /// Starts a fresh validation window when a recovery action abandons its previous route.
        /// </summary>
        protected void ResetMovementValidation()
        {
            m_HasRequestedDestination = false;
            m_LastProgressTime = Time.time;
            m_LastProgressPosition = transform.position;
        }

        /// <summary>
        /// Sets a new pathfinding destination.
        /// </summary>
        /// <param name="destination">The destination to set.</param>
        /// <returns>True when the request was accepted.</returns>
        protected virtual bool SetDestination(Vector3 destination)
        {
            if (!m_PathfinderValid || !m_Pathfinder.SetDestination(destination)) { return false; }
            if (!m_HasRequestedDestination) {
                m_HasRequestedDestination = true;
                m_PathValidationStartTime = Time.time;
            }
            return true;
        }

        /// <summary>
        /// Determines whether common provider, timeout, path, or progress validation has failed.
        /// </summary>
        /// <param name="monitorProgress">Should active-path progress be monitored for stuck movement?</param>
        /// <returns>True when the movement task should fail.</returns>
        protected bool HasMovementFailed(bool monitorProgress = true)
        {
            if (!m_PathfinderValid || !m_Pathfinder.IsValid) { return true; }
            var elapsedTime = Time.time - m_StartTime - m_AccumulatedPauseTime;
            if (m_PauseStartTime >= 0f) { elapsedTime -= Time.time - m_PauseStartTime; }
            if (m_UseTimeout.Value && elapsedTime >= Mathf.Max(0f, m_Timeout.Value)) { return true; }
            if (m_FailOnInvalidPath.Value && m_HasRequestedDestination &&
                Time.time - m_PathValidationStartTime >= Mathf.Max(0f, m_PathValidationGrace.Value) && m_Pathfinder.IsPathInvalid) {
                return true;
            }
            if (!monitorProgress || !m_UseStuckDetection.Value || m_Pathfinder.IsPathPending || !m_Pathfinder.HasPath()) { return false; }

            var minimumProgress = Mathf.Max(0f, m_MinimumProgressDistance.Value);
            if ((transform.position - m_LastProgressPosition).sqrMagnitude >= minimumProgress * minimumProgress) {
                m_LastProgressPosition = transform.position;
                m_LastProgressTime = Time.time;
                return false;
            }
            return Time.time - m_LastProgressTime >= Mathf.Max(0f, m_StuckDuration.Value);
        }

        /// <summary>
        /// Does the agent have a pathfinding path?
        /// </summary>
        /// <returns>True if the agent has a pathfinding path.</returns>
        protected bool HasPath()
        {
            return m_PathfinderValid && m_Pathfinder.HasPath();
        }

        /// <summary>
        /// Returns true if the position is a valid pathfinding position.
        /// </summary>
        /// <param name="position">The position to sample. The position will be updated to the valid sampled position.</param>
        /// <returns>True if the position is a valid pathfinding position.</returns>
        protected bool SamplePosition(ref Vector3 position)
        {
            return m_PathfinderValid && m_Pathfinder.SamplePosition(ref position);
        }

        /// <summary>
        /// Has the agent arrived at the destination?
        /// </summary>
        /// <returns>True if the agent has arrived at the destination.</returns>
        public bool HasArrived()
        {
            return m_PathfinderValid && m_Pathfinder.HasArrived();
        }

        /// <summary>
        /// The task has ended.
        /// </summary>
        public override void OnEnd()
        {
            if (!ControlsMovement || m_Pathfinder == null) { return; }
            if (m_StopOnTaskEnd.Value && m_PathfinderValid) {
                m_Pathfinder.Stop();
            }
            m_Pathfinder.OnEnd();
        }

        /// <summary>
        /// Specifies the type of reflection that should be used to save the task.
        /// </summary>
        /// <param name="index">The index of the sub-task. This is used for the task set allowing each contained task to have their own save type.</param>
        public override MemberVisibility GetSaveReflectionType(int index) { return MemberVisibility.None; }

        /// <summary>
        /// Returns the current task state.
        /// </summary>
        /// <param name="world">The DOTS world.</param>
        /// <param name="entity">The DOTS entity.</param>
        /// <returns>The current task state.</returns>
        public override object Save(World world, Entity entity)
        {
            if (!m_BehaviorTree.IsNodeActive(true, m_RuntimeIndex)) return null;

            var saveData = new MovementSaveData();
            if (ControlsMovement && m_PathfinderValid && m_Pathfinder.HasPath() && !m_Pathfinder.HasArrived()) {
                saveData.Destination = m_Pathfinder.Destination;
                saveData.HasDestination = true;
            } else {
                saveData.Destination = Vector3.zero;
                saveData.HasDestination = false;
            }
            saveData.ElapsedTime = Time.time - m_StartTime - m_AccumulatedPauseTime;

            return saveData;
        }

        /// <summary>
        /// Loads the previous task state.
        /// </summary>
        /// <param name="saveData">The previous task state.</param>
        /// <param name="world">The DOTS world.</param>
        /// <param name="entity">The DOTS entity.</param>
        public override void Load(object saveData, World world, Entity entity)
        {
            if (saveData == null) {
                return;
            }

            var movementSaveData = saveData as MovementSaveData;
            if (movementSaveData == null) { return; }
            m_StartTime = Time.time - Mathf.Max(0f, movementSaveData.ElapsedTime);
            if (ControlsMovement && movementSaveData.HasDestination) {
                SetDestination(movementSaveData.Destination);
            }
        }

        /// <summary>
        /// Resumes retained movement when the behavior tree resumes from a pause.
        /// </summary>
        public override void OnBehaviorTreeStarted()
        {
            base.OnBehaviorTreeStarted();
            if (!m_WasPaused) { return; }
            if (m_PauseStartTime >= 0f) {
                m_AccumulatedPauseTime += Time.time - m_PauseStartTime;
                m_PauseStartTime = -1f;
            }
            if (ControlsMovement && m_PathfinderValid) { m_Pathfinder.Resume(); }
            m_LastProgressPosition = transform.position;
            m_LastProgressTime = Time.time;
            m_WasPaused = false;
        }

        /// <summary>
        /// The behavior tree has been stopped.
        /// </summary>
        /// <param name="paused">Was the behavior tree paused.</param>
        public override void OnBehaviorTreeStopped(bool paused)
        {
            base.OnBehaviorTreeStopped(paused);

            if (paused && !m_WasPaused) {
                m_WasPaused = true;
                m_PauseStartTime = Time.time;
            }
            if (ControlsMovement && m_PathfinderValid && m_Pathfinder.HasPath()) {
                m_Pathfinder.Stop();
            }
        }

        /// <summary>
        /// The behavior tree has been destroyed.
        /// </summary>
        public override void OnDestroy()
        {
            base.OnDestroy();
            if (ControlsMovement && m_PathfinderValid) { m_Pathfinder.Stop(); }
        }

        /// <summary>
        /// Resets the task values back to their default.
        /// </summary>
        public override void Reset()
        {
#if UNITY_EDITOR
            if (m_Pathfinder == null) {
                var defaultPathfinderType = UnityEditor.EditorPrefs.GetString(c_PathfinderTypeKey);
                if (!string.IsNullOrEmpty(defaultPathfinderType)) {
                    var pathfinderType = TypeUtility.GetType(defaultPathfinderType);
                    if (pathfinderType != null) {
                        m_Pathfinder = System.Activator.CreateInstance(pathfinderType) as Pathfinder;
                    }
                }

                // The NavMeshAgentPathfinder will always be available.
                if (m_Pathfinder == null) {
                    m_Pathfinder = new NavMeshAgentPathfinder();
                }
            }
#endif
            if (m_Pathfinder == null) {
                m_Pathfinder = new NavMeshAgentPathfinder();
            }
            m_Pathfinder.Reset();
            m_StopOnTaskEnd = true;
            m_FailOnInvalidPath = true;
            m_UseTimeout = false;
            m_Timeout = 10f;
            m_UseStuckDetection = true;
            m_StuckDuration = 1.5f;
            m_MinimumProgressDistance = 0.05f;
            m_PathValidationGrace = 0.2f;
        }

        /// <summary>
        /// Data structure for saving movement state.
        /// </summary>
        private class MovementSaveData
        {
            [Tooltip("The current destination that the agent is moving towards.")]
            public Vector3 Destination;
            [Tooltip("Indicates if the agent has a valid destination to move towards.")]
            public bool HasDestination;
            [Tooltip("The active time elapsed when the task was saved.")]
            public float ElapsedTime;
        }
    }
}