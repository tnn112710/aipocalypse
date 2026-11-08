/// ---------------------------------------------
/// Movement Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
#if GRAPH_DESIGNER
namespace Opsive.BehaviorDesigner.AddOns.MovementPack.Runtime
{
    using UnityEngine;

    /// <summary>
    /// Coordinates incremental selection, bounded recovery, leases, and stable replacement without performing locomotion.
    /// </summary>
    public sealed class CoverController
    {
        /// <summary>
        /// The state of a cover query or maintenance update.
        /// </summary>
        public enum Status { Pending, Ready, Failed }
        /// <summary>
        /// Controls search work, retries, and replacement stability.
        /// </summary>
        public struct Options
        {
            [Tooltip("Candidate/stance pairs processed per update.")] public int Budget;
            [Tooltip("Maximum consecutive recovery attempts before failure.")] public int Retries;
            [Tooltip("The delay between recovery attempts.")] public float RetryDelay;
            [Tooltip("The interval between active cover validation checks.")] public float RecheckInterval;
            [Tooltip("The interval between optional better-cover searches.")] public float ReselectionInterval;
            [Tooltip("The minimum cost improvement required to replace valid cover.")] public float MinimumImprovement;
            [Tooltip("The duration of an optional cover lease.")] public float LeaseDuration;
            [Tooltip("Whether to maintain cover after arrival.")] public bool Maintain;
        }
        /// <summary>
        /// A validated selection that remains independent of an in-progress replacement search.
        /// </summary>
        public struct Selection
        {
            [Tooltip("The selected foot position.")] public Vector3 Position;
            [Tooltip("The outward normal.")] public Vector3 Normal;
            [Tooltip("The cover collider.")] public Collider Collider;
            [Tooltip("The optional authored cover point.")] public CoverPoint Point;
            [Tooltip("The required character stance.")] public CoverStance Stance;
            [Tooltip("The validated body height.")] public float Height;
            [Tooltip("The authored priority.")] public float Priority;
            [Tooltip("The authored point position when selected.")] public Vector3 AuthoredPosition;
        }
        private readonly CoverSearch m_Search = new CoverSearch();
        private readonly CoverSearch m_Validator = new CoverSearch();
        private CoverSearch.Settings m_Settings;
        private Options m_Options;
        private CoverSearch.SamplePosition m_Sample;
        private CoverSearch.PathDistance m_Path;
        private int m_MaximumColliders, m_MaximumOverlaps, m_Retries;
        private float m_NextRetry, m_LastCheck, m_LastSelection;
        private bool m_Waiting, m_Failed, m_CompareCurrent;
        public Selection Current { get; private set; }
        public bool HasCover { get; private set; }
        public bool Changed { get; private set; }
        public bool SearchPending => m_Search.IsSearching || m_Waiting;
        public CoverFailure Failure { get; private set; }
        public int LastStepCandidates => m_Search.LastStepCandidates;

        /// <summary>
        /// Begins a fresh cover request while reusing query buffers.
        /// </summary>
        /// <param name="settings">The actor and geometry settings.</param>
        /// <param name="options">The lifecycle settings.</param>
        /// <param name="maximumColliders">The geometry capacity.</param>
        /// <param name="maximumOverlaps">The overlap capacity.</param>
        /// <param name="sample">The navigation sampler.</param>
        /// <param name="path">The complete-route evaluator.</param>
        /// <param name="now">The active action clock.</param>
        public void Start(CoverSearch.Settings settings, Options options, int maximumColliders, int maximumOverlaps,
            CoverSearch.SamplePosition sample, CoverSearch.PathDistance path, float now)
        {
            m_Settings = CoverSearch.Normalize(settings); m_Options = options; m_Sample = sample; m_Path = path;
            m_MaximumColliders = maximumColliders; m_MaximumOverlaps = maximumOverlaps;
            Current = default; HasCover = false; Changed = false; Failure = CoverFailure.None;
            m_Retries = 0; m_Failed = false; m_Waiting = false; m_LastCheck = now; m_LastSelection = now;
            Begin(false);
        }

        /// <summary>
        /// Starts a search without discarding valid cover during optional reselection.
        /// </summary>
        /// <param name="compareCurrent">Whether a new selection must improve upon existing cover.</param>
        private void Begin(bool compareCurrent)
        {
            m_CompareCurrent = compareCurrent;
            m_Search.Begin(m_Settings, m_MaximumColliders, m_MaximumOverlaps, m_Sample, m_Path);
        }

        /// <summary>
        /// Validates current cover and advances a bounded amount of search work.
        /// </summary>
        /// <param name="settings">The refreshed threat and query settings.</param>
        /// <param name="now">The active action clock.</param>
        /// <returns>The query or maintenance state.</returns>
        public Status Tick(CoverSearch.Settings settings, float now)
        {
            Changed = false; m_Settings = CoverSearch.Normalize(settings);
            m_Validator.Configure(m_Settings, m_MaximumOverlaps);
            if (m_Failed) { return Status.Failed; }
            if (HasCover && m_Settings.UseReservations && !CoverReservations.TryReserve(m_Settings.Actor.gameObject, Current.Position, m_Settings.Radius, m_Options.LeaseDuration, Current.Point)) {
                Recover(CoverFailure.Reserved, now);
            }
            if (HasCover && now - m_LastCheck >= Mathf.Max(0.02f, m_Options.RecheckInterval)) {
                m_LastCheck = now;
                var failure = ValidateCurrent(Current.Position);
                if (failure == CoverFailure.None && !m_Path(Current.Position, out _)) { failure = CoverFailure.Unreachable; }
                if (failure != CoverFailure.None) { Recover(failure, now); }
            }
            if (m_Failed) { return Status.Failed; }
            if (m_Waiting) {
                if (now < m_NextRetry) { return Status.Pending; }
                m_Waiting = false; Begin(false);
            }
            if (!m_Search.IsSearching && HasCover && m_Options.Maintain && m_Options.ReselectionInterval > 0 && now - m_LastSelection >= m_Options.ReselectionInterval) {
                m_LastSelection = now; Begin(true);
            }
            if (m_Search.IsSearching && m_Search.Step(m_Options.Budget)) {
                if (!m_Search.HasSelection) {
                    if (m_CompareCurrent && HasCover) { return Status.Ready; }
                    Recover(m_Search.Failure, now); return m_Failed ? Status.Failed : Status.Pending;
                }
                var candidate = new Selection { Position = m_Search.Position, Normal = m_Search.Normal, Collider = m_Search.Collider,
                    Point = m_Search.Point, Stance = m_Search.RequiredStance, Height = m_Search.SelectedHeight, Priority = m_Search.Priority,
                    AuthoredPosition = m_Search.AuthoredPosition };
                var failure = m_Validator.ValidateHeight(candidate.Position, m_Settings.Threat, m_Settings.Directional, candidate.Height);
                if (candidate.Collider == null || !candidate.Collider.enabled || !candidate.Collider.gameObject.activeInHierarchy) { failure = CoverFailure.NoGeometry; }
                if (candidate.Point != null && (!candidate.Point.isActiveAndEnabled || candidate.Point.Cover != candidate.Collider ||
                    (candidate.Point.transform.position - candidate.AuthoredPosition).sqrMagnitude > 0.0001f)) { failure = CoverFailure.NoGeometry; }
                if (!m_Path(candidate.Position, out var candidateDistance)) { failure = CoverFailure.Unreachable; }
                if (failure != CoverFailure.None) {
                    if (m_CompareCurrent && HasCover) { return Status.Ready; }
                    Recover(failure, now); return m_Failed ? Status.Failed : Status.Pending;
                }
                if (m_CompareCurrent && HasCover && m_Path(Current.Position, out var distance)) {
                    var currentScore = CoverSearch.EvaluateScore(m_Settings, Current.Position, distance, Current.Priority);
                    var candidateScore = CoverSearch.EvaluateScore(m_Settings, candidate.Position, candidateDistance, candidate.Priority);
                    if (candidateScore >= currentScore - Mathf.Max(0.01f, m_Options.MinimumImprovement)) { return Status.Ready; }
                }
                if (m_Settings.UseReservations && !CoverReservations.TryReserve(m_Settings.Actor.gameObject, candidate.Position, m_Settings.Radius, m_Options.LeaseDuration, candidate.Point)) {
                    if (m_CompareCurrent && HasCover) { return Status.Ready; }
                    Recover(CoverFailure.Reserved, now); return m_Failed ? Status.Failed : Status.Pending;
                }
                Current = candidate; HasCover = true; Changed = true; Failure = CoverFailure.None; m_LastSelection = now;
            }
            return HasCover ? Status.Ready : Status.Pending;
        }

        /// <summary>
        /// Checks the selected geometry and a supplied arrival position at the selected stance.
        /// </summary>
        /// <param name="position">The actual or planned foot position.</param>
        /// <returns>The failed validation or None.</returns>
        public CoverFailure ValidateCurrent(Vector3 position)
        {
            if (!HasCover || Current.Collider == null || !Current.Collider.enabled || !Current.Collider.gameObject.activeInHierarchy ||
                (Current.Point != null && (!Current.Point.isActiveAndEnabled || Current.Point.Cover != Current.Collider ||
                (Current.Point.transform.position - Current.AuthoredPosition).sqrMagnitude > 0.0001f))) { return CoverFailure.NoGeometry; }
            return m_Validator.ValidateHeight(position, m_Settings.Threat, m_Settings.Directional, Current.Height);
        }

        /// <summary>
        /// Abandons invalid cover and schedules a bounded retry, releasing its lease immediately.
        /// </summary>
        /// <param name="reason">The reason cover was lost.</param>
        /// <param name="now">The active action clock.</param>
        /// <returns>True while recovery remains possible.</returns>
        public bool Recover(CoverFailure reason, float now)
        {
            if (m_Settings.UseReservations && m_Settings.Actor != null) { CoverReservations.Release(m_Settings.Actor.gameObject); }
            HasCover = false; Current = default; Changed = false; Failure = reason;
            if (m_Waiting) { return !m_Failed; }
            if (m_Retries++ >= Mathf.Max(0, m_Options.Retries)) { m_Failed = true; return false; }
            m_Waiting = true; m_NextRetry = now + Mathf.Max(0.02f, m_Options.RetryDelay);
            return true;
        }

        /// <summary>
        /// Resets consecutive recovery accounting only after sustained valid arrival.
        /// </summary>
        public void MarkStable() { m_Retries = 0; }

        /// <summary>
        /// Ends the request and optionally leaves a short lease for the next graph action.
        /// </summary>
        /// <param name="successful">Whether the request finished successfully.</param>
        public void End(bool successful)
        {
            if (!successful && m_Settings.UseReservations && m_Settings.Actor != null) { CoverReservations.Release(m_Settings.Actor.gameObject); }
            if (!successful) { HasCover = false; Current = default; }
        }

        /// <summary>
        /// Draws the active search diagnostics and highlights the retained selection during replacement searches.
        /// </summary>
        public void DrawGizmos()
        {
            m_Search.DrawGizmos();
            if (m_Settings.Debug && HasCover) {
                var previous = Gizmos.color; Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(Current.Position, m_Settings.Radius); Gizmos.DrawRay(Current.Position, Current.Normal);
#if UNITY_EDITOR
                UnityEditor.Handles.Label(Current.Position, Current.Stance.ToString());
#endif
                Gizmos.color = previous;
            }
        }
    }
}
#endif