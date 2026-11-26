using MB13.Aipocalypse.Core.Events;
using MessagePipe;
using Unity.Profiling;
using UnityEngine;

namespace MB13.Aipocalypse.Core.Combat
{
    // The one way game code hurts something: applies the hit and publishes DamageDealt with what landed, so hit feedback,
    // kill credit and (later) damage rules hook in at one place.
    public sealed class DamageService
    {
        static readonly ProfilerMarker DealMarker = new("Aipocalypse.DamageService.Deal");
        static readonly ProfilerMarker PublishMarker = new("Aipocalypse.DamageService.Publish");

        readonly IPublisher<DamageDealt> _dealt;

        public DamageService(IPublisher<DamageDealt> dealt) => _dealt = dealt;

        public DamageResult Deal(IDamageable target, in DamageInfo info)
        {
            if (target == null || target.IsDead || info.Amount <= 0f)
                return default;
            using (DealMarker.Auto())
            {
                DamageResult result = target.TakeDamage(info);
                if (result.Applied > 0f)
                {
                    using (PublishMarker.Auto())
                        _dealt.Publish(new DamageDealt(target, info, result));
                }
                return result;
            }
        }

        // A physics hit (bullet, swing): damages the IDamageable the collider belongs to (scaled by its Hitbox, if any: a head
        // doubles and crits), or lets scenery with an IHitReaction move, and pushes a loose rigidbody by info.Force. Returns
        // the damageable, or null on scenery.
        public IDamageable DealHit(in RaycastHit hit, in DamageInfo info)
        {
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null && hit.collider.TryGetComponent(out Hitbox hitbox))
                Deal(target, info.OnHitbox(hitbox.DamageMultiplier, hitbox.Critical));
            else
                Deal(target, info);

            if (target == null)
                hit.collider.GetComponentInParent<IHitReaction>()?.React(hit.collider, info);

            Rigidbody body = hit.rigidbody;
            if (body != null && !body.isKinematic && info.Force != Vector3.zero)
                body.AddForceAtPosition(info.Force, hit.point, ForceMode.Impulse);
            return target;
        }
    }
}
