using System.Collections.Generic;
using Aipocalypse.Core.Entities;
using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // Picks an enemy's target: the best kind weight per metre (EnemyDefinition.Targets), skipping what can't be targeted and
    // kinds the enemy doesn't want. Later: a target it has no path to gives way to what blocks the path (a closed fence).
    public sealed class TargetSelector
    {
        readonly TargetRegistry _targets;

        public TargetSelector(TargetRegistry targets) => _targets = targets;

        public ITargetable Choose(Vector3 from, IReadOnlyDictionary<TargetKind, float> weights)
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
    }
}
