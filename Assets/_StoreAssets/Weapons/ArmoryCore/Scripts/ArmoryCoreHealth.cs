using System;
using UnityEngine;
using UnityEngine.Events;

namespace ArmoryCore
{
    [Serializable]
    public sealed class ArmoryCoreHealthEvent : UnityEvent { }

    [DisallowMultipleComponent]
    public sealed class ArmoryCoreHealth : MonoBehaviour, IArmoryCoreBallisticTarget
    {
        [Min(0.01f)] public float maximumHealth = 100f;
        [SerializeField] private float currentHealth = 100f;
        public ArmoryCoreHealthEvent onDamaged = new();
        public ArmoryCoreHealthEvent onKilled = new();

        public float CurrentHealth => currentHealth;
        public bool IsAlive => currentHealth > 0f;

        private void Awake()
        {
            currentHealth = Mathf.Clamp(currentHealth, 0f, maximumHealth);
        }

        public void ReceiveBallisticHit(ArmoryCoreBallisticHit hit)
        {
            ApplyDamage(hit.Damage);
        }

        public void ApplyDamage(float amount)
        {
            if (!IsAlive || amount <= 0f)
                return;
            currentHealth = Mathf.Max(0f, currentHealth - amount);
            onDamaged.Invoke();
            if (!IsAlive)
                onKilled.Invoke();
        }

        public void Restore(float amount)
        {
            currentHealth = Mathf.Clamp(currentHealth + Mathf.Max(0f, amount), 0f, maximumHealth);
        }
    }
}
