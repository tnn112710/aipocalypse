using Aipocalypse.Core.Combat;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.Debugging
{
    // Shooting-range target: logs each hit and heals back to full when emptied, so it never runs out.
    // Knockback tips it away from the hit and it springs back upright.
    public sealed class TargetDummy : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] float _maxHealth = 100f;
        [SerializeField, Tooltip("Tipped by knockback around its own pivot; empty = this transform")] Transform _body;
        [SerializeField, Min(0f), Tooltip("Degrees of tip per N·s of knockback")] float _tipPerForce = 6f;
        [SerializeField, Min(0f)] float _maxTip = 35f;
        [SerializeField, Min(0f)] float _stiffness = 120f;
        [SerializeField, Min(0f)] float _damping = 9f;

        [ShowInInspector, ReadOnly, HideInEditorMode] Health _health;

        Quaternion _rest;
        // Tip as a world-space axis * degrees, a damped spring back to zero.
        Vector3 _tip;
        Vector3 _tipVelocity;

        public bool IsDead => false;

        void Awake()
        {
            _health = new Health(_maxHealth);
            if (_body == null)
                _body = transform;
            _rest = _body.localRotation;
        }

        public void TakeDamage(in DamageInfo info)
        {
            _health.Damage(info.Amount);
            Debug.Log($"{name}: hit for {info.Amount} ({info.Type}), {_health.Current}/{_health.Max}", this);
            if (_health.IsDead)
                _health.Restore();

            Vector3 push = Vector3.ProjectOnPlane(info.Force, Vector3.up);
            // Kick speed for a peak of about push * _tipPerForce degrees (an undamped spring peaks at v / sqrt(k)).
            if (push.sqrMagnitude > 0.0001f)
                _tipVelocity += Vector3.Cross(Vector3.up, push.normalized) * (push.magnitude * _tipPerForce * Mathf.Sqrt(_stiffness));
        }

        void Update()
        {
            if (_tip == Vector3.zero && _tipVelocity == Vector3.zero)
                return;

            float deltaTime = Time.deltaTime;
            _tipVelocity += (-_tip * _stiffness - _tipVelocity * _damping) * deltaTime;
            _tip = Vector3.ClampMagnitude(_tip + _tipVelocity * deltaTime, _maxTip);
            if (_tip.sqrMagnitude < 0.0001f && _tipVelocity.sqrMagnitude < 0.0001f)
                _tip = _tipVelocity = Vector3.zero;

            Quaternion tip = Quaternion.AngleAxis(_tip.magnitude, _tip.sqrMagnitude > 0f ? _tip.normalized : Vector3.up);
            Quaternion parent = _body.parent != null ? _body.parent.rotation : Quaternion.identity;
            _body.localRotation = Quaternion.Inverse(parent) * tip * parent * _rest;
        }
    }
}
