using Aipocalypse.Core.Combat;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.Debugging
{
    // Shooting-range target: logs each hit and heals back to full when emptied, so it never runs out.
    public sealed class TargetDummy : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] float _maxHealth = 100f;

        [ShowInInspector, ReadOnly, HideInEditorMode] Health _health;

        public bool IsDead => false;

        void Awake() => _health = new Health(_maxHealth);

        public void TakeDamage(in DamageInfo info)
        {
            _health.Damage(info.Amount);
            Debug.Log($"{name}: hit for {info.Amount} ({info.Type}), {_health.Current}/{_health.Max}", this);
            if (_health.IsDead)
                _health.Restore();
        }
    }
}
