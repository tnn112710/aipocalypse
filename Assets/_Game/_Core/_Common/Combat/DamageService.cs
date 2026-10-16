using Aipocalypse.Core.Events;
using MessagePipe;

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
    }
}
