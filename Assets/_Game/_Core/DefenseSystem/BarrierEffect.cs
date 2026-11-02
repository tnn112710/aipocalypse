using Aipocalypse.Core.Combat;
using UnityEngine;

namespace Aipocalypse.Core.DefenseSystem
{
    // What a blow on a barrier does besides denting it (BarrierDefinition.OnStruck): a data asset per behaviour, shared by every
    // segment of a kind, so an upgrade tier is a new list, not new code.
    public abstract class BarrierEffect : ScriptableObject
    {
        public abstract void OnStruck(in BarrierStrike strike);
    }

    // A blow that landed on a barrier (after its damage scale).
    public readonly struct BarrierStrike
    {
        public readonly BarrierSegment Barrier;
        public readonly DamageInfo Info;
        public readonly float Applied;
        public readonly DamageService Damage;

        public BarrierStrike(BarrierSegment barrier, in DamageInfo info, float applied, DamageService damage)
        {
            Barrier = barrier;
            Info = info;
            Applied = applied;
            Damage = damage;
        }
    }
}
