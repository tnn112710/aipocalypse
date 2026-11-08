/// ---------------------------------------------
/// Shared Add-On for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.Shared.Runtime.Pathfinding
{
    using UnityEngine;

    /// <summary>
    /// Abstract class for pathfinding implementations shared by the Behavior Designer add-ons.
    /// </summary>
    public abstract class Pathfinder
    {
        /// <summary>
        /// Is the pathfinder initialized and ready to move?
        /// </summary>
        public virtual bool IsValid => true;
        /// <summary>
        /// Is the pathfinder waiting for a path calculation?
        /// </summary>
        public virtual bool IsPathPending => false;
        /// <summary>
        /// Is the current path invalid?
        /// </summary>
        public virtual bool IsPathInvalid => false;
        /// <summary>
        /// The velocity of the agent.
        /// </summary>
        public abstract Vector3 Velocity { get; }
        /// <summary>
        /// The remaining distance of the agent.
        /// </summary>
        public abstract float RemainingDistance { get; }
        /// <summary>
        /// The destination of the agent.
        /// </summary>
        public abstract Vector3 Destination { get; }
        /// <summary>
        /// The speed of the agent.
        /// </summary>
        public abstract float Speed { get; set; }

        /// <summary>
        /// Initializes the pathfinder.
        /// </summary>
        /// <param name="gameObject">The GameObject that the pathfinder belongs to.</param>
        public abstract void Initialize(GameObject gameObject);

        /// <summary>
        /// The task has started.
        /// </summary>
        public virtual void OnStart() { }

        /// <summary>
        /// Set a new pathfinding destination.
        /// </summary>
        /// <param name="destination">The destination to set.</param>
        /// <returns>True if the destination is valid.</returns>
        public abstract bool SetDesination(Vector3 destination);

        /// <summary>
        /// Sets a new pathfinding destination. This correctly-spelled wrapper preserves the original SetDesination API for compatibility.
        /// </summary>
        /// <param name="destination">The destination to set.</param>
        /// <returns>True if the destination is valid.</returns>
        public virtual bool SetDestination(Vector3 destination)
        {
            return SetDesination(destination);
        }

        /// <summary>
        /// Does the agent have a pathfinding path?
        /// </summary>
        /// <returns>True if the agent has a pathfinding path.</returns>
        public abstract bool HasPath();

        /// <summary>
        /// Returns true if the position is a valid pathfinding position.
        /// </summary>
        /// <param name="position">The position to sample. The position will be updated to the valid sampled position.</param>
        /// <returns>True if the position is a valid pathfinding position.</returns>
        public abstract bool SamplePosition(ref Vector3 position);

        /// <summary>
        /// Samples a formation slot on a surface the actor can reach. Providers can override the default nearest-surface sampling.
        /// </summary>
        /// <param name="position">The requested slot, updated to the reachable navigation position on success.</param>
        /// <returns>True if a suitable navigation position was found.</returns>
        public virtual bool SampleReachablePosition(ref Vector3 position)
        {
            return SamplePosition(ref position);
        }

        /// <summary>
        /// Measures a complete route without replacing the active destination. Providers must override this to support cover search.
        /// </summary>
        /// <param name="start">The current actor position.</param>
        /// <param name="destination">The desired destination.</param>
        /// <param name="distance">The complete route length.</param>
        /// <returns>True only when a complete route can be confirmed.</returns>
        public virtual bool TryGetPathDistance(Vector3 start, Vector3 destination, out float distance)
        {
            distance = 0f; return false;
        }

        /// <summary>
        /// Has the agent arrived at the destination?
        /// </summary>
        /// <returns>True if the agent has arrived at the destination.</returns>
        public abstract bool HasArrived();

        /// <summary>
        /// The agent should stop moving.
        /// </summary>
        public abstract void Stop();

        /// <summary>
        /// Resumes movement after the behavior tree has been paused.
        /// </summary>
        public virtual void Resume() { }

        /// <summary>
        /// The task has ended.
        /// </summary>
        public virtual void OnEnd() { }

        /// <summary>
        /// Resets the values back to their default.
        /// </summary>
        public virtual void Reset() { }
    }
}