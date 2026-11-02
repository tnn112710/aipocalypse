using System.Collections.Generic;
using Aipocalypse.Core.Entities;
using Aipocalypse.Core.SpawnSystem;
using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
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

        public BreachRules(int candidates, float finishHealth, float gapDetour, float breakerShare, int minBreakers, float gapSpacing)
        {
            Candidates = candidates;
            FinishHealth = finishHealth;
            GapDetour = gapDetour;
            BreakerShare = breakerShare;
            MinBreakers = minBreakers;
            GapSpacing = gapSpacing;
        }
    }

    // Picks an enemy's target: the best kind weight per metre (EnemyDefinition.Targets), skipping what can't be targeted and
    // kinds the enemy doesn't want. For a kind that breaks fences:
    // - no path to the target (fence shut): the weakest of the nearest standing barriers;
    // - a way in open (a broken segment, an open gate): it goes through, unless a standing barrier is between it and the target
    //   and the way in is GapDetour or more out of its way, or it is among the BreakerShare of hunters at the fence for whom the
    //   way in is furthest: then it breaks a new one, away from the open ones. A barrier about to fall is finished first.
    public sealed class TargetSelector
    {
        public const int MaxBreachCandidates = 16;

        struct Hunter
        {
            public Vector3 Position;
            public bool AtFence;
        }

        readonly TargetRegistry _targets;
        readonly INavmeshQuery _navmesh;
        readonly IBreachable[] _nearest = new IBreachable[MaxBreachCandidates];
        readonly float[] _nearestDistance = new float[MaxBreachCandidates];
        // Everyone choosing through here (by holder), so the breaker share can be ranked; Forget on death and disable.
        readonly Dictionary<object, Hunter> _hunters = new();

        // `navmesh` may be null (tests): every target then counts as reachable.
        public TargetSelector(TargetRegistry targets, INavmeshQuery navmesh)
        {
            _targets = targets;
            _navmesh = navmesh;
        }

        public int BarrierVersion => _targets.BarrierVersion;

        // Without breach rules: the best wanted target.
        public ITargetable Choose(Vector3 from, IReadOnlyDictionary<TargetKind, float> weights) => Choose(null, from, weights, null, default);

        // `hunter`: who asks (its slot in the breaker ranking; null = not ranked); `current`: what it hunts now.
        public ITargetable Choose(object hunter, Vector3 from, IReadOnlyDictionary<TargetKind, float> weights, ITargetable current, in BreachRules rules)
        {
            ITargetable best = Best(from, weights);
            if (best == null || _navmesh == null || rules.Candidates <= 0 || !weights.TryGetValue(TargetKind.Fence, out float breaks) || breaks <= 0f)
                return Record(hunter, from, best);

            Vector3 to = best.Transform.position;
            if (!_navmesh.IsReachable(from, to))
                return Record(hunter, from, ChooseBreach(from, rules.Candidates) ?? best);

            if (current is IBreachable hitting && hitting.IsTargetable && hitting.RemainingHealth <= rules.FinishHealth)
                return Record(hunter, from, hitting);
            if (!IsWalled(from, to))
                return Record(hunter, from, best);

            float detour = Detour(from, to);
            bool breaker = detour >= rules.GapDetour || (current is IBreachable && IsAmongFurthest(hunter, detour, to, rules));
            IBreachable breach = breaker ? ChooseBreach(from, rules.Candidates, rules.GapSpacing) : null;
            return Record(hunter, from, (ITargetable)breach ?? best);
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

        // Metres the walk through the nearest open way in (straight lines) adds to going straight at `to`; 0 with none open.
        public float Detour(Vector3 from, Vector3 to)
        {
            float shortest = float.PositiveInfinity;
            IReadOnlyList<IBreachable> barriers = _targets.Barriers;
            for (int i = 0; i < barriers.Count; i++)
            {
                IBreachable way = barriers[i];
                if (way == null || way.IsTargetable)
                    continue;
                Vector3 at = way.Transform.position;
                shortest = Mathf.Min(shortest, Flat(at - from) + Flat(to - at));
            }
            return float.IsPositiveInfinity(shortest) ? 0f : Mathf.Max(0f, shortest - Flat(to - from));
        }

        // A standing barrier crosses the straight line from `from` to `to` (on the ground plane).
        public bool IsWalled(Vector3 from, Vector3 to)
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
            return false;
        }

        ITargetable Best(Vector3 from, IReadOnlyDictionary<TargetKind, float> weights)
        {
            ITargetable best = null;
            float bestScore = 0f;
            for (int i = 0; i < _targets.Count; i++)
            {
                ITargetable target = _targets[i];
                if (target == null || !target.IsTargetable || weights == null || !weights.TryGetValue(target.Kind, out float weight) || weight <= 0f)
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

        // Of the hunters at the fence, the BreakerShare (of all hunters; at least MinBreakers, never every one) for whom the way
        // in is furthest keep breaking. Ranked by where each stood at its last choice.
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
                if (other.Value.AtFence && !ReferenceEquals(other.Key, hunter) && Detour(other.Value.Position, to) > detour)
                    further++;
            return further < needed;
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

        ITargetable Record(object hunter, Vector3 from, ITargetable choice)
        {
            if (hunter != null)
                _hunters[hunter] = new Hunter { Position = from, AtFence = choice is IBreachable };
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
