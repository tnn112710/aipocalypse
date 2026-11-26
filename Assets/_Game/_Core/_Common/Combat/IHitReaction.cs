namespace MB13.Aipocalypse.Core.Combat
{
    // Something that takes no damage but moves when hit (a hanging sign): DamageService.DealHit calls it, on the hit
    // collider's parents, for every bullet, melee or projectile hit that found no IDamageable.
    public interface IHitReaction
    {
        void React(UnityEngine.Collider collider, in DamageInfo info);
    }
}
