using System.Collections.Generic;
using MB13.Aipocalypse.Core.Entities;
using MB13.Aipocalypse.Core.SpawnSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // How an enemy that breaks fences (a Fence weight) deals with barriers; from its MeleeSettings.
    public readonly struct BreachRules
    {
        // Nearest standing barriers compared; the one with the least health left is attacked. 0 = it never breaks fences.
        public readonly int Candidates;
        // A barrier it is hitting with this much health left or less is finished even when a way in is open.
        public readonly float FinishHealth;
        // With a way in open, metres its route through it may be longer than straight at the target before it breaks its own.
        public readonly float GapDetour;
        // Share of the enemies hunting while a way in is open that keep breaking (at least MinBreakers, never all of them).
        public readonly float BreakerShare;
        public readonly int MinBreakers;
        // A new breach is made at least this far (m) from an open way in.
        public readonly float GapSpacing;
        // Within this many metres of a way in it fits through, it always goes through (never one of the breakers).
        public readonly float GapNear;
        // Its body's width (m): ways in narrower than this don't let it through, so it widens one instead.
        public readonly float BodyWidth;

        public BreachRules(int candidates, float finishHealth, float gapDetour, float breakerShare, int minBreakers, float gapSpacing,
            float gapNear, float bodyWidth)
        {
            Candidates = candidates;
            FinishHealth = finishHealth;
            GapDetour = gapDetour;
            BreakerShare = breakerShare;
            MinBreakers = minBreakers;
            GapSpacing = gapSpacing;
            GapNear = gapNear;
            BodyWidth = bodyWidth;
        }
    }

    // Picks an enemy's target: the best kind weight per metre (EnemyDefinition.Targets), skipping what can't be targeted and
    // kinds the enemy doesn't want; one that hasn't noticed the player goes for its objective instead (the Farm). For a kind
    // that breaks fences:
    // - no path to the target (fence shut): the weakest of the nearest standing barriers;
    // - only ways in too narrow for its body: it widens the nearest one (the weaker barrier beside it);
    // - a way in it fits through: it goes through when it is within GapNear of it; further off, it breaks a new one (away from
    //   the open ones) when a standing barrier is between it and the target and the way in is GapDetour or more out of its way,
    //   or it is among the BreakerShare of such hunters for whom the way in is furthest. A barrier about to fall is finished first.
    public sealed class TargetSelector
    {
        public const int MaxBreachCandidates = 16;
        // Barrier ends closer than this (m) are joined: broken neighbours make one wider way in.
        const float JoinDistance = 0.25f;

        struct Hunter
        {
            public Vector3 Position;
            // At the fence, or walled off beyond GapNear of a way in it fits through: in the running for the breaker share.
            public bool Candidate;
        }

        // A run of joined barriers that don't block (broken, gate open): its two outer ends, width, and the standing barriers
        // beside it (null when none).
        struct Opening
        {
            public Vector3 A, B;
            public float Width;
            public IBreachable SideA, SideB;
        }

        readonly TargetRegistry _targets;
        readonly INavmeshQuery _navmesh;
        readonly IBreachable[] _nearest = new IBreachable[MaxBreachCandidates];
        readonly float[] _nearestDistance = new float[MaxBreachCandidates];
        // Everyone choosing through here (by holder), so the breaker share can be ranked; Forget on death and disable.
        readonly Dictionary<object, Hunter> _hunters = new();
        // The ways in, rebuilt when a barrier starts or stops blocking (BarrierVersion) or one registers or leaves.
        readonly List<Opening> _openings = new();
        readonly List<IBreachable> _run = new();
        readonly HashSet<IBreachable> _inRun = new();
        int _openingsVersion = -1;
        int _openingsBarriers = -1;

        // `navmesh` may be null (tests): every target then counts as reachable.
        public TargetSelector(TargetRegistry targets, INavmeshQuery navmesh)
        {
            _targets = targets;
            _navmesh = navmesh;
        }

        public int BarrierVersion => _targets.BarrierVersion;

        // Without breach rules: the best wanted target.
        public ITargetable Choose(Vector3 from, IReadOnlyDictionary<TargetKind, float> weights) => Choose(null, from, weights, null, default);

        // `hunter`: who asks (its slot in the breaker ranking; null = not ranked); `current`: what it hunts now; `playerKnown`:
        // false while it hasn't noticed the player, who is then never picked and `objective` (the Farm; may be null) stands in
        // for it when nothing else is wanted.
        public ITargetable Choose(object hunter, Vector3 from, IReadOnlyDictionary<TargetKind, float> weights, ITargetable current,
            in BreachRules rules, bool playerKnown = true, ITargetable objective = null)
        {
            ITargetable best = Best(from, weights, playerKnown);
            if (best == null && !playerKnown && objective != null && objective.IsTargetable)
                best = objective;
            if (best == null || _navmesh == null || rules.Candidates <= 0 || !weights.TryGetValue(TargetKind.Fence, out float breaks) || breaks <= 0f)
                return Record(hunter, from, best);

            Vector3 to = best.Transform.position;
            if (!_navmesh.IsReachable(from, to))
                return Record(hunter, from, ChooseBreach(from, rules.Candidates) ?? best);

            if (current is IBreachable hitting && hitting.IsTargetable && hitting.RemainingHealth <= rules.FinishHealth)
                return Record(hunter, from, hitting);
            if (!IsWalled(from, to, rules.BodyWidth))
                return Record(hunter, from, best);
            RefreshOpenings();
            bool fits = TryNearestOpening(from, rules.BodyWidth, out float gapDistance);
            if (!fits && _openings.Count > 0)
                return Record(hunter, from, (ITargetable)Widen(from, rules.Candidates) ?? best);
            if (fits && gapDistance <= rules.GapNear)
                return Record(hunter, from, best);

            float detour = Detour(from, to, rules.BodyWidth);
            bool breaker = detour >= rules.GapDetour || IsAmongFurthest(hunter, detour, to, rules);
            IBreachable breach = breaker ? ChooseBreach(from, rules.Candidates, rules.GapSpacing) : null;
            return Record(hunter, from, (ITargetable)breach ?? best, true);
        }

        public void Forget(object hunter)
        {
            if (hunter != null)
                _hunters.Remove(hunter);
        }

        // Of the `candidates` nearest standing barriers (none within `gapSpacing` m of an open way in), the one with the least
        // health left; the nearer on a tie.
        public IBreachable ChooseBreach(Vector3 from, int candidates, float gapSpacing = 0f)
        {
            int capacity = Mathf.Clamp(candidates, 1, MaxBreachCandidates);
            int count = 0;
            IReadOnlyList<IBreachable> barriers = _targets.Barriers;
            for (int i = 0; i < barriers.Count; i++)
            {
                IBreachable barrier = barriers[i];
                if (barrier == null || !barrier.IsTargetable || (gapSpacing > 0f && NearOpenWay(barrier.Transform.position, gapSpacing)))
                    continue;
                float distance = MeleeHit.SqrFlatDistance(from, barrier.Transform.position);
                if (count == capacity && distance >= _nearestDistance[count - 1])
                    continue;
                int at = count < capacity ? count++ : count - 1;
                while (at > 0 && _nearestDistance[at - 1] > distance)
                {
                    _nearest[at] = _nearest[at - 1];
                    _nearestDistance[at] = _nearestDistance[at - 1];
                    at--;
                }
                _nearest[at] = barrier;
                _nearestDistance[at] = distance;
            }

            IBreachable weakest = null;
            for (int i = 0; i < count; i++)
            {
                if (weakest == null || _nearest[i].RemainingHealth < weakest.RemainingHealth)
                    weakest = _nearest[i];
                _nearest[i] = null;
            }
            return weakest;
        }

        // Metres the walk through the nearest open way in at least `bodyWidth` wide (straight lines, through its middle) adds to
        // going straight at `to`; 0 with none open.
        public float Detour(Vector3 from, Vector3 to, float bodyWidth = 0f)
        {
            RefreshOpenings();
            float shortest = float.PositiveInfinity;
            for (int i = 0; i < _openings.Count; i++)
            {
                Opening way = _openings[i];
                if (way.Width < bodyWidth)
                    continue;
                Vector3 at = (way.A + way.B) * 0.5f;
                shortest = Mathf.Min(shortest, Flat(at - from) + Flat(to - at));
            }
            return float.IsPositiveInfinity(shortest) ? 0f : Mathf.Max(0f, shortest - Flat(to - from));
        }

        // A standing barrier, or a way in narrower than `bodyWidth`, crosses the straight line from `from` to `to` (on the ground
        // plane).
        public bool IsWalled(Vector3 from, Vector3 to, float bodyWidth = 0f)
        {
            IReadOnlyList<IBreachable> barriers = _targets.Barriers;
            for (int i = 0; i < barriers.Count; i++)
            {
                IBreachable barrier = barriers[i];
                if (barrier == null || !barrier.IsTargetable)
                    continue;
                Vector3 centre = barrier.Transform.position;
                Vector3 span = barrier.Span;
                if (Crosses(from, to, centre - span, centre + span))
                    return true;
            }
            RefreshOpenings();
            for (int i = 0; i < _openings.Count; i++)
                if (_openings[i].Width < bodyWidth && Crosses(from, to, _openings[i].A, _openings[i].B))
                    return true;
            return false;
        }

        ITargetable Best(Vector3 from, IReadOnlyDictionary<TargetKind, float> weights, bool playerKnown = true)
        {
            ITargetable best = null;
            float bestScore = 0f;
            for (int i = 0; i < _targets.Count; i++)
            {
                ITargetable target = _targets[i];
                if (target == null || !target.IsTargetable || weights == null || !weights.TryGetValue(target.Kind, out float weight) || weight <= 0f)
                    continue;
                if (!playerKnown && target.Kind == TargetKind.Player)
                    continue;
                float distance = Mathf.Max(1f, Mathf.Sqrt(MeleeHit.SqrFlatDistance(from, target.Transform.position)));
                float score = weight / distance;
                if (score <= bestScore)
                    continue;
                best = target;
                bestScore = score;
            }
            return best;
        }

        // Of the hunters walled off beyond GapNear (at the fence or still on their way), the BreakerShare (of all hunters; at
        // least MinBreakers, never every one) for whom the way in is furthest keep breaking, so a crowd opens more than one gap.
        // Ranked by where each stood at its last choice. Those within GapNear never get here.
        bool IsAmongFurthest(object hunter, float detour, Vector3 to, in BreachRules rules)
        {
            if (hunter == null)
                return false;
            int all = _hunters.ContainsKey(hunter) ? _hunters.Count : _hunters.Count + 1;
            int needed = Mathf.Min(Mathf.Max(Mathf.RoundToInt(all * rules.BreakerShare), rules.MinBreakers), all - 1);
            if (needed <= 0)
                return false;
            int further = 0;
            foreach (KeyValuePair<object, Hunter> other in _hunters)
                if (other.Value.Candidate && !ReferenceEquals(other.Key, hunter) && Detour(other.Value.Position, to, rules.BodyWidth) > detour)
                    further++;
            return further < needed;
        }

        // The nearest open way in at least `bodyWidth` wide, as the flat distance from `from` to it.
        bool TryNearestOpening(Vector3 from, float bodyWidth, out float distance)
        {
            distance = float.PositiveInfinity;
            for (int i = 0; i < _openings.Count; i++)
                if (_openings[i].Width >= bodyWidth)
                    distance = Mathf.Min(distance, DistanceToSegment(from, _openings[i].A, _openings[i].B));
            return !float.IsPositiveInfinity(distance);
        }

        // The weaker standing barrier beside the nearest open way in, so the way gets wider; with none standing beside it, the
        // usual breach.
        IBreachable Widen(Vector3 from, int candidates)
        {
            Opening nearest = default;
            float best = float.PositiveInfinity;
            for (int i = 0; i < _openings.Count; i++)
            {
                float distance = DistanceToSegment(from, _openings[i].A, _openings[i].B);
                if (distance >= best)
                    continue;
                best = distance;
                nearest = _openings[i];
            }
            IBreachable weaker = null;
            if (nearest.SideA != null && nearest.SideA.IsTargetable)
                weaker = nearest.SideA;
            if (nearest.SideB != null && nearest.SideB.IsTargetable && (weaker == null || nearest.SideB.RemainingHealth < weaker.RemainingHealth))
                weaker = nearest.SideB;
            return weaker ?? ChooseBreach(from, candidates);
        }

        // Groups the barriers that don't block into runs of joined ones: each run is one way in, as wide as its barriers.
        void RefreshOpenings()
        {
            IReadOnlyList<IBreachable> barriers = _targets.Barriers;
            if (_openingsVersion == _targets.BarrierVersion && _openingsBarriers == barriers.Count)
                return;
            _openingsVersion = _targets.BarrierVersion;
            _openingsBarriers = barriers.Count;
            _openings.Clear();
            _inRun.Clear();
            for (int i = 0; i < barriers.Count; i++)
            {
                IBreachable first = barriers[i];
                if (first == null || first.IsTargetable || !_inRun.Add(first))
                    continue;
                _run.Clear();
                _run.Add(first);
                var opening = new Opening();
                for (int k = 0; k < _run.Count; k++)
                {
                    IBreachable member = _run[k];
                    opening.Width += 2f * Flat(member.Span);
                    for (int j = 0; j < barriers.Count; j++)
                    {
                        IBreachable other = barriers[j];
                        if (other == null || ReferenceEquals(other, member) || !Joined(member, other))
                            continue;
                        if (!other.IsTargetable)
                        {
                            if (_inRun.Add(other))
                                _run.Add(other);
                        }
                        else if (opening.SideA == null)
                            opening.SideA = other;
                        else if (!ReferenceEquals(opening.SideA, other))
                            opening.SideB ??= other;
                    }
                }
                // Its outer ends: the two member ends furthest apart.
                float widest = -1f;
                for (int k = 0; k < _run.Count * 2; k++)
                    for (int m = k + 1; m < _run.Count * 2; m++)
                    {
                        Vector3 p = End(_run[k / 2], k % 2), q = End(_run[m / 2], m % 2);
                        float apart = Flat(p - q);
                        if (apart <= widest)
                            continue;
                        widest = apart;
                        opening.A = p;
                        opening.B = q;
                    }
                _openings.Add(opening);
            }
            _run.Clear();
        }

        static Vector3 End(IBreachable barrier, int side) => barrier.Transform.position + (side == 0 ? -barrier.Span : barrier.Span);

        static bool Joined(IBreachable a, IBreachable b)
        {
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                    if (Flat(End(a, i) - End(b, j)) < JoinDistance)
                        return true;
            return false;
        }

        static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector2 ab = new(b.x - a.x, b.z - a.z), ap = new(p.x - a.x, p.z - a.z);
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(ap, ab) / ab.sqrMagnitude) : 0f;
            return (ap - ab * t).magnitude;
        }

        bool NearOpenWay(Vector3 position, float spacing)
        {
            IReadOnlyList<IBreachable> barriers = _targets.Barriers;
            for (int i = 0; i < barriers.Count; i++)
            {
                IBreachable way = barriers[i];
                if (way != null && !way.IsTargetable && MeleeHit.SqrFlatDistance(position, way.Transform.position) < spacing * spacing)
                    return true;
            }
            return false;
        }

        // One that hunts nothing (unaware, wandering) isn't counted among the hunters.
        ITargetable Record(object hunter, Vector3 from, ITargetable choice, bool candidate = false)
        {
            if (hunter == null)
                return choice;
            if (choice == null)
                _hunters.Remove(hunter);
            else
                _hunters[hunter] = new Hunter { Position = from, Candidate = candidate || choice is IBreachable };
            return choice;
        }

        static float Flat(Vector3 v) => Mathf.Sqrt(v.x * v.x + v.z * v.z);

        // Segments ab and cd cross on the XZ plane.
        static bool Crosses(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            float d1 = Side(c, d, a), d2 = Side(c, d, b), d3 = Side(a, b, c), d4 = Side(a, b, d);
            return d1 * d2 < 0f && d3 * d4 < 0f;
        }

        static float Side(Vector3 p, Vector3 q, Vector3 r) => (q.x - p.x) * (r.z - p.z) - (q.z - p.z) * (r.x - p.x);
    }
}
