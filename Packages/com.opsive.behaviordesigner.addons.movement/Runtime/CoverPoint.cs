/// ---------------------------------------------
/// Movement Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
#if GRAPH_DESIGNER
namespace Opsive.BehaviorDesigner.AddOns.MovementPack.Runtime
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// An optional authored foot position and outward cover normal, still subject to geometry and route validation.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // Existing runtime initialization owns the static reset and its ordering.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    [AddComponentMenu("Opsive/Behavior Designer/Movement Pack/Cover Point")]
    public class CoverPoint : MonoBehaviour
    {
        [Tooltip("The physical geometry associated with this cover point.")]
        [SerializeField] private Collider m_Cover;
        [Tooltip("Higher values are preferred when using authored priority selection.")]
        [SerializeField] private float m_Priority;
        [Tooltip("Which stances are supported by this point.")]
        [SerializeField] private CoverStanceMode m_Stances = CoverStanceMode.Either;
        private static readonly List<CoverPoint> s_Points = new List<CoverPoint>();
        public Collider Cover { get => m_Cover; set => m_Cover = value; }
        public float Priority { get => m_Priority; set => m_Priority = value; }
        public CoverStanceMode Stances { get => m_Stances; set => m_Stances = value; }

        /// <summary>
        /// Registers this point while it is available.
        /// </summary>
        private void OnEnable() { if (!s_Points.Contains(this)) { s_Points.Add(this); } }
        /// <summary>
        /// Removes disabled points from subsequent searches.
        /// </summary>
        private void OnDisable() { s_Points.Remove(this); }
        /// <summary>
        /// Prunes stale registrations while retaining enabled points when scene reload is disabled.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            for (int i = s_Points.Count - 1; i >= 0; --i) { if (s_Points[i] == null || !s_Points[i].isActiveAndEnabled) { s_Points.RemoveAt(i); } }
        }

        /// <summary>
        /// Copies available nearby authored points into a reusable bounded buffer.
        /// </summary>
        /// <param name="origin">The search origin.</param>
        /// <param name="radius">The search radius.</param>
        /// <param name="buffer">The destination buffer.</param>
        /// <returns>The number of points copied.</returns>
        internal static int Collect(Vector3 origin, float radius, CoverPoint[] buffer)
        {
            var count = 0;
            foreach (var point in s_Points) {
                if (point == null || !point.isActiveAndEnabled || (point.transform.position - origin).sqrMagnitude > radius * radius) { continue; }
                if (count == buffer.Length) { break; }
                buffer[count++] = point;
            }
            return count;
        }

        /// <summary>
        /// Displays the authored foot position and normal in the Scene view.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            var previous = Gizmos.color; Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.2f); Gizmos.DrawRay(transform.position, transform.forward);
            Gizmos.color = previous;
        }
    }

    /// <summary>
    /// Coordinates optional time-limited cover claims between actors in this movement pack.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // Existing runtime initialization owns the static reset and its ordering.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class CoverReservations
    {
        /// <summary>
        /// A claim held by one active actor.
        /// </summary>
        private struct Claim
        {
            [Tooltip("The actor that owns the claim.")] public GameObject Owner;
            [Tooltip("The reserved foot position.")] public Vector3 Position;
            [Tooltip("The reserved radius.")] public float Radius;
            [Tooltip("The expiry on the scaled Unity clock.")] public float Expires;
            [Tooltip("The optional authored point.")] public CoverPoint Point;
        }
        private static readonly List<Claim> s_Claims = new List<Claim>();

        /// <summary>
        /// Clears claims when a new runtime session starts.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() { s_Claims.Clear(); }

        /// <summary>
        /// Checks other actors' unexpired claims without changing this actor's claim.
        /// </summary>
        /// <param name="owner">The actor requesting cover.</param>
        /// <param name="position">The candidate position.</param>
        /// <param name="radius">The candidate radius.</param>
        /// <param name="point">The optional authored point.</param>
        /// <returns>True when another actor owns overlapping cover.</returns>
        public static bool IsReserved(GameObject owner, Vector3 position, float radius, CoverPoint point = null)
        {
            for (int i = s_Claims.Count - 1; i >= 0; --i) {
                var claim = s_Claims[i];
                if (claim.Owner == null || !claim.Owner.activeInHierarchy || claim.Expires <= Time.time) { s_Claims.RemoveAt(i); continue; }
                if (claim.Owner == owner) { continue; }
                if ((point != null && claim.Point == point) || (position - claim.Position).sqrMagnitude < (radius + claim.Radius) * (radius + claim.Radius)) { return true; }
            }
            return false;
        }

        /// <summary>
        /// Atomically acquires or replaces this actor's lease after confirming no competing claim exists.
        /// </summary>
        /// <param name="owner">The claiming actor.</param>
        /// <param name="position">The chosen position.</param>
        /// <param name="radius">The required separation radius.</param>
        /// <param name="duration">The lease duration in scaled seconds.</param>
        /// <param name="point">The optional authored point.</param>
        /// <returns>True when the claim was acquired.</returns>
        public static bool TryReserve(GameObject owner, Vector3 position, float radius, float duration, CoverPoint point = null)
        {
            if (owner == null || !owner.activeInHierarchy || IsReserved(owner, position, radius, point)) { return false; }
            var claim = new Claim { Owner = owner, Position = position, Radius = Mathf.Max(0.01f, radius), Expires = Time.time + Mathf.Max(0.1f, duration), Point = point };
            for (int i = 0; i < s_Claims.Count; ++i) {
                if (s_Claims[i].Owner != owner) { continue; }
                s_Claims[i] = claim; return true;
            }
            s_Claims.Add(claim); return true;
        }

        /// <summary>
        /// Releases the actor's claim on cancellation or when cover is abandoned.
        /// </summary>
        /// <param name="owner">The claiming actor.</param>
        public static void Release(GameObject owner)
        {
            for (int i = s_Claims.Count - 1; i >= 0; --i) { if (s_Claims[i].Owner == owner) { s_Claims.RemoveAt(i); } }
        }
    }
}
#endif