using MB13.Aipocalypse.Core.Combat;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DefenseSystem
{
    // Spikes: every melee blow on the barrier hurts the attacker back.
    [CreateAssetMenu(fileName = "Thorns", menuName = "Aipocalypse/Defense/Thorns Effect")]
    public sealed class ThornsEffect : BarrierEffect
    {
        [Min(0f), Tooltip("Damage dealt back to the attacker per melee blow")] public float Damage = 10f;

        public override void OnStruck(in BarrierStrike strike)
        {
            GameObject attacker = strike.Info.Source;
            if (strike.Info.Type != DamageType.Melee || attacker == null || strike.Damage == null
                || !attacker.TryGetComponent(out IDamageable target))
                return;
            strike.Damage.Deal(target, new DamageInfo(Damage, DamageType.Generic, attacker.transform.position, default, strike.Barrier.gameObject));
        }
    }
}
