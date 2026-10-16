using System;
using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    // An additive, self-returning offset of the gun model: one kick, or a damped wobble.
    [Serializable]
    public struct GunJolt
    {
        [Tooltip("Local offset at the peak (m)")] public Vector3 Position;
        [Tooltip("Local rotation at the peak (degrees)")] public Vector3 Euler;
        [Min(0.01f)] public float Duration;
        [Min(0f), Tooltip("0 = one kick and return; more = that many damped back-and-forth swings")] public float Wobbles;

        public GunJolt(Vector3 position, Vector3 euler, float duration, float wobbles = 0f)
        {
            Position = position;
            Euler = euler;
            Duration = duration;
            Wobbles = wobbles;
        }

        public bool IsEmpty => Duration <= 0f || (Position == Vector3.zero && Euler == Vector3.zero);

        // Signed weight 0..1 at normalized time t.
        public float Weight(float t)
        {
            if (t <= 0f || t >= 1f)
                return 0f;
            if (Wobbles > 0f)
            {
                float decay = 1f - t;
                return Mathf.Sin(t * Mathf.PI * 2f * Wobbles) * decay * decay;
            }
            const float attack = 0.15f;
            if (t < attack)
            {
                float rise = 1f - t / attack;
                return 1f - rise * rise;
            }
            float fall = 1f - (t - attack) / (1f - attack);
            return fall * fall * fall;
        }
    }

    // Plays one GunJolt at a time; a new one replaces the running one.
    public sealed class GunJoltPlayer
    {
        GunJolt _jolt;
        float _time = -1f;

        public bool IsPlaying => _time >= 0f;

        public void Play(GunJolt jolt)
        {
            if (jolt.IsEmpty)
                return;
            _jolt = jolt;
            _time = 0f;
        }

        public void Stop() => _time = -1f;

        public void Tick(float deltaTime)
        {
            if (_time < 0f)
                return;
            _time += deltaTime;
            if (_time >= _jolt.Duration)
                _time = -1f;
        }

        public void Apply(ref Vector3 position, ref Quaternion rotation)
        {
            if (_time < 0f)
                return;
            float weight = _jolt.Weight(_time / _jolt.Duration);
            position += _jolt.Position * weight;
            rotation *= Quaternion.Euler(_jolt.Euler * weight);
        }
    }
}
