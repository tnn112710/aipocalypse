/// ---------------------------------------------
/// Movement Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.MovementPack.Runtime.Tasks
{
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.BehaviorDesigner.Runtime.Utility;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using System;
    using Unity.Entities;
    using UnityEngine;

    [Opsive.Shared.Utility.Description("Patrols around the specified waypoints.")]
    [NodeIcon("6f82a4685e473054986cedbfb24bf8bd", "ed44a06209d60e943819270678e69b89")]
    public class Patrol : MovementAgent
    {
        [Tooltip("The waypoints that the agent should move between.")]
        [SerializeField] protected SharedVariable<GameObject[]> m_Waypoints;
        [Tooltip("Should the agent start patrolling to the closest waypoint?")]
        [SerializeField] protected SharedVariable<bool> m_ClosestWaypointStart;
        [Tooltip("Should the agent patrol the waypoints randomly?")]
        [SerializeField] protected SharedVariable<bool> m_RandomPatrol;
        [Tooltip("Should the sequential patrol reverse direction at the route ends?")]
        [SerializeField] protected SharedVariable<bool> m_PingPongPatrol = false;
        [Tooltip("The length of time that the agent should pause when arriving at a waypoint.")]
        [SerializeField] protected SharedVariable<RangeFloat> m_WaypointPauseDuration;
        [Tooltip("The number of route cycles required before success. Zero patrols continuously.")]
        [SerializeField] protected SharedVariable<int> m_RequiredCycles = 0;
        [Tooltip("The number of invalid waypoints or paths that can be skipped consecutively.")]
        [SerializeField] protected SharedVariable<int> m_MaximumRecoveryAttempts = 3;

        private int m_Index;
        private int m_Direction = 1;
        private int m_CompletedCycles;
        private int m_VisitedCount;
        private int m_RecoveryAttempts;
        private float m_NextDestinationTime;
        private float m_PauseTime = -1;
        private bool m_RouteValid;

        // Callback when the agent arrives at a waypoint.
        public Action<GameObject> OnWaypointArrival { get; set; }

        /// <summary>
        /// The task has been initialized.
        /// </summary>
        public override void OnAwake()
        {
            base.OnAwake();

            m_Waypoints.OnValueChange += DetermineWaypointIndex;
        }

        /// <summary>
        /// The task has started.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();

            DetermineWaypointIndex();
            m_NextDestinationTime = -1;
            m_PauseTime = -1;
            m_Direction = 1;
            m_CompletedCycles = 0;
            m_VisitedCount = 0;
            m_RecoveryAttempts = 0;
            m_RouteValid = TrySetCurrentDestination();
        }

        /// <summary>
        /// Determines the waypoint index that the agent should move towards.
        /// </summary>
        public void DetermineWaypointIndex()
        {
            var waypoints = m_Waypoints.Value;
            if (waypoints == null || waypoints.Length == 0) {
                m_Index = 0;
                return;
            }
            if (m_RandomPatrol.Value) {
                m_Index = UnityEngine.Random.Range(0, waypoints.Length);
            } else if (m_ClosestWaypointStart.Value) {
                // Move towards the closest waypoint at the start.
                var distance = Mathf.Infinity;
                float localDistance;
                for (int i = 0; i < waypoints.Length; ++i) {
                    if (waypoints[i] == null) { continue; }
                    if ((localDistance = Vector3.SqrMagnitude(transform.position - waypoints[i].transform.position)) < distance) {
                        distance = localDistance;
                        m_Index = i;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the target destination.
        /// </summary>
        /// <returns>The target destination.</returns>
        private bool TrySetCurrentDestination()
        {
            var waypoints = m_Waypoints.Value;
            if (waypoints == null || waypoints.Length == 0) { return false; }
            var attempts = 0;
            while (attempts <= Mathf.Max(0, m_MaximumRecoveryAttempts.Value)) {
                if (m_Index >= 0 && m_Index < waypoints.Length && waypoints[m_Index] != null &&
                    SetDestination(waypoints[m_Index].transform.position)) {
                    return true;
                }
                ++attempts;
                ++m_RecoveryAttempts;
                if (m_RecoveryAttempts > Mathf.Max(0, m_MaximumRecoveryAttempts.Value) || AdvanceWaypoint()) { return false; }
            }
            return false;
        }

        /// <summary>
        ///  Patrol around the different waypoints specified in the waypoint array.
        /// </summary>
        /// <returns>Always returns a status of running. </returns>
        public override TaskStatus OnUpdate()
        {
            if (!m_RouteValid || m_Waypoints.Value == null || m_Waypoints.Value.Length == 0) {
                return TaskStatus.Failure;
            }

            if (HasMovementFailed()) {
                ++m_RecoveryAttempts;
                if (m_RecoveryAttempts > Mathf.Max(0, m_MaximumRecoveryAttempts.Value) || AdvanceWaypoint()) {
                    return m_RequiredCycles.Value > 0 && m_CompletedCycles >= m_RequiredCycles.Value ? TaskStatus.Success : TaskStatus.Failure;
                }
                return TrySetCurrentDestination() ? TaskStatus.Running : TaskStatus.Failure;
            }

            if (HasArrived()) {
                m_RecoveryAttempts = 0;
                if (m_NextDestinationTime == -1) {
                    m_NextDestinationTime = Time.time + m_WaypointPauseDuration.Value.RandomValue;

                    // Others may be interested when the agent arrives at a waypoint.
                    if (m_Waypoints.Value[m_Index] != null) { OnWaypointArrival?.Invoke(m_Waypoints.Value[m_Index]); }
                }
                // Wait the required duration before switching waypoints.
                if (m_NextDestinationTime <= Time.time) {
                    if (AdvanceWaypoint() && m_RequiredCycles.Value > 0) {
                        return TaskStatus.Success;
                    }
                    if (!TrySetCurrentDestination()) { return TaskStatus.Failure; }
                    m_NextDestinationTime = -1;
                }
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Advances to the next waypoint according to the configured route mode.
        /// </summary>
        /// <returns>True when the configured cycle count has completed.</returns>
        private bool AdvanceWaypoint()
        {
            var count = m_Waypoints.Value.Length;
            if (count <= 1) {
                ++m_CompletedCycles;
            } else if (m_RandomPatrol.Value) {
                var previousIndex = m_Index;
                m_Index = UnityEngine.Random.Range(0, count - 1);
                if (m_Index >= previousIndex) { ++m_Index; }
                ++m_VisitedCount;
                if (m_VisitedCount >= count) {
                    m_VisitedCount = 0;
                    ++m_CompletedCycles;
                }
            } else if (m_PingPongPatrol.Value) {
                m_Index += m_Direction;
                if (m_Index >= count) {
                    m_Direction = -1;
                    m_Index = count - 2;
                } else if (m_Index < 0) {
                    m_Direction = 1;
                    m_Index = 1;
                    ++m_CompletedCycles;
                }
            } else {
                ++m_Index;
                if (m_Index >= count) {
                    m_Index = 0;
                    ++m_CompletedCycles;
                }
            }
            return m_RequiredCycles.Value > 0 && m_CompletedCycles >= m_RequiredCycles.Value;
        }

        /// <summary>
        /// The behavior tree has been started.
        /// </summary>
        public override void OnBehaviorTreeStarted()
        {
            base.OnBehaviorTreeStarted();

            if (m_PauseTime == -1) {
                return;
            }

            if (m_NextDestinationTime != -1) {
                m_NextDestinationTime += Time.time - m_PauseTime;
            }
            m_PauseTime = -1;
        }

        /// <summary>
        /// The behavior tree has been stopped.
        /// </summary>
        /// <param name="paused">Was the behavior tree paused.</param>
        public override void OnBehaviorTreeStopped(bool paused)
        {
            base.OnBehaviorTreeStopped(paused);

            if (paused && m_PauseTime == -1) {
                m_PauseTime = Time.time;
            }
        }

        /// <summary>
        /// Returns the current task state.
        /// </summary>
        /// <param name="world">The DOTS world.</param>
        /// <param name="entity">The DOTS entity.</param>
        /// <returns>The current task state.</returns>
        public override object Save(World world, Entity entity)
        {
            var saveData = new object[7];
            saveData[0] = base.Save(world, entity);
            saveData[1] = m_Index;
            saveData[2] = Time.time - m_NextDestinationTime;
            saveData[3] = m_Direction;
            saveData[4] = m_CompletedCycles;
            saveData[5] = m_VisitedCount;
            saveData[6] = m_RecoveryAttempts;
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
            var saveDataArray = (object[])saveData;
            base.Load(saveDataArray[0], world, entity);
            m_Index = (int)saveDataArray[1];
            m_NextDestinationTime = Time.time - (float)saveDataArray[2];
            if (saveDataArray.Length > 3) {
                m_Direction = (int)saveDataArray[3];
                m_CompletedCycles = (int)saveDataArray[4];
                m_VisitedCount = (int)saveDataArray[5];
                m_RecoveryAttempts = (int)saveDataArray[6];
            }
        }

        /// <summary>
        /// Resets the task values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            m_Waypoints = new GameObject[0];
            m_ClosestWaypointStart = false;
            m_RandomPatrol = false;
            m_PingPongPatrol = false;
            m_WaypointPauseDuration = new RangeFloat();
            m_RequiredCycles = 0;
            m_MaximumRecoveryAttempts = 3;
        }

        /// <summary>
        /// The behavior tree has been destroyed.
        /// </summary>
        public override void OnDestroy()
        {
            base.OnDestroy();
            m_Waypoints.OnValueChange -= DetermineWaypointIndex;
        }
    }
}