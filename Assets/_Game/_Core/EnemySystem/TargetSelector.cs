using System.Collections.Generic;
using Aipocalypse.Core.Entities;
using Aipocalypse.Core.SpawnSystem;
using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // Picks an enemy's target: the best kind weight per metre (EnemyDefinition.Targets), skipping what can't be targeted and
    // kinds the enemy doesn't want. When it has no path to that target and breaks fences (a Fence weight), it attacks what's
    // in the way instead: of the `breachCandidates` nearest standing barriers, the one with the least health left.
    public sealed class TargetSelector
    {
        public const int MaxBreachCandidates = 16;

        readonly TargetRegistry _targets;
        readonly INavmeshQuery _navmesh;
        readonly IBreachable[] _nearest = new IBreachable[MaxBreachCandidates];
        readonly float[] _nearestDistance = new float[MaxBreachCandidates];

        // `navmesh` may be null (tests): every target then counts as reachable.
        public TargetSelector(TargetRegistry targets, INavmeshQuery navmesh)
        {
            _targets = targets;
            _navmesh = navmesh;
        }

        public ITargetable Choose(Vector3 from, IReadOnlyDictionary<TargetKind, float> weights, int breachCandidates = 0)
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

            if (best == null || _navmesh == null || breachCandidates <= 0 || !weights.TryGetValue(TargetKind.Fence, out float breaks) || breaks <= 0f
                || _navmesh.IsReachable(from, best.Transform.position))
                return best;
            return ChooseBreach(from, breachCandidates) ?? best;
        }

        // Of the `candidates` nearest standing barriers, the one with the least health left; the nearer on a tie.
        public IBreachable ChooseBreach(Vector3 from, int candidates)
        {
            int capacity = Mathf.Clamp(candidates, 1, MaxBreachCandidates);
            int count = 0;
            IReadOnlyList<IBreachable> barriers = _targets.Barriers;
            for (int i = 0; i < barriers.Count; i++)
            {
                IBreachable barrier = barriers[i];
                if (barrier == null || !barrier.IsTargetable)
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
    }
}
