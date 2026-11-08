using UnityEngine;

namespace MB13.Aipocalypse.Core.Combat
{
    // On a collider of something damageable (a head, later armor or weak spots): hits that land on this collider deal
    // DamageMultiplier × their damage, and count as critical if Critical is on. DamageService.DealHit applies it.
    [RequireComponent(typeof(Collider))]
    public sealed class Hitbox : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("Damage multiplier for hits on this collider (head: 2)")] float _damageMultiplier = 2f;
        [SerializeField, Tooltip("Hits here are critical (red damage number)")] bool _critical = true;

        public float DamageMultiplier => _damageMultiplier;
        public bool Critical => _critical;
    }
}
