namespace Aipocalypse.Core.Combat
{
    // Hurt it through DamageService.Deal, which also announces the hit; TakeDamage only applies the target's own rules.
    public interface IDamageable
    {
        bool IsDead { get; }
        DamageResult TakeDamage(in DamageInfo info);
    }
}
