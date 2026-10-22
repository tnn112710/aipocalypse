using Aipocalypse.Core.Events;
using MessagePipe;
using UnityEngine;

namespace Aipocalypse.Core.Combat
{
    // The one way game code hurts something: applies the hit and publishes DamageDealt with what landed, so hit feedback,
    // kill credit and (later) damage rules hook in at one place.
    public sealed class DamageService
    {
        readonly IPublisher<DamageDealt> _dealt;

        public DamageService(IPublisher<DamageDealt> dealt) => _dealt = dealt;

        public DamageResult Deal(IDamageable target, in DamageInfo info)
        {
            if (target == null || target.IsDead || info.Amount <= 0f)
                return default;
            DamageResult result = target.TakeDamage(info);
            if (result.Applied > 0f)
                _dealt.Publish(new DamageDealt(target, info, result));
            return result;
        }

        // A physics hit (bullet, swing): damages the IDamageable the collider belongs to and pushes a loose rigidbody by
        // info.Force. Returns the damageable, or null when the hit was on scenery.
        public IDamageable DealHit(in RaycastHit hit, in DamageInfo info)
        {
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            Deal(target, info);

            Rigidbody body = hit.rigidbody;
            if (body != null && !body.isKinematic && info.Force != Vector3.zero)
                body.AddForceAtPosition(info.Force, hit.point, ForceMode.Impulse);
            return target;
        }
    }
}
