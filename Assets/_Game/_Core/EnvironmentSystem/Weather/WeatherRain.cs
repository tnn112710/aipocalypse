using System;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.EnvironmentSystem
{
    // The rain the player sees (on the root of Environment/Weather/WeatherRain.prefab, built from Unluck Stylized Weather): layers
    // kept above the camera and fake splashes on the ground around it. No particle collides with anything: a splash emitter
    // sits on the ground under the camera (one raycast a frame). The weather sets the amount and the wind drift; the
    // particles are unlit, so their authored colours darken with the director's daylight. Everything is off while dry.
    public sealed class WeatherRain : MonoBehaviour
    {
        [Serializable]
        public struct Layer
        {
            public ParticleSystem System;
            [Min(0f), Tooltip("Particles per second at full rain (amount 1)")] public float MaxRate;
            [Range(0f, 1f), Tooltip("Rain amount where this layer starts; it reaches MaxRate at amount 1")] public float From;
            [Tooltip("Placed on the ground under the camera (splashes) instead of riding with the camera; no wind drift")]
            public bool OnGround;
        }

        [SerializeField, Tooltip("Children: falling layers (their local offset is kept above the camera) and splashes")]
        Layer[] _layers = Array.Empty<Layer>();
        [SerializeField, Tooltip("What splashes sit on")] LayerMask _ground = 1 | 1 << 4;
        [SerializeField, Tooltip("Colour multiplier at night (the authored colours are the daylight look)")]
        Color _night = new(0.35f, 0.4f, 0.5f, 0.75f);

        EnvironmentDirector _director;
        Transform _camera;
        readonly RaycastHit[] _hits = new RaycastHit[4];
        ParticleSystem.MinMaxGradient[] _authored = Array.Empty<ParticleSystem.MinMaxGradient>();
        float _amount;
        bool _on;
        float _tintedDaylight = -1f;

        [Inject]
        void Construct(IObjectResolver resolver) => resolver.TryResolve(out _director);

        void Awake()
        {
            _authored = new ParticleSystem.MinMaxGradient[_layers.Length];
            for (int i = 0; i < _layers.Length; i++)
            {
                if (_layers[i].System == null)
                    continue;
                _authored[i] = _layers[i].System.main.startColor;
                ParticleSystem.EmissionModule emission = _layers[i].System.emission;
                emission.rateOverTimeMultiplier = 0f;
            }
            SetVisible(false);
        }

        // amount 0–1; drift = sideways velocity of the falling drops (m/s, world).
        public void Set(float amount, Vector3 drift)
        {
            _amount = amount;
            if (amount > 0f && !_on)
            {
                Follow();
                SetVisible(true);
            }
            bool drifts = drift.sqrMagnitude > 0.0001f;
            foreach (Layer layer in _layers)
            {
                if (layer.System == null)
                    continue;
                ParticleSystem.EmissionModule emission = layer.System.emission;
                emission.rateOverTimeMultiplier = layer.MaxRate * Mathf.Clamp01((amount - layer.From) / Mathf.Max(0.001f, 1f - layer.From));
                if (layer.OnGround)
                    continue;
                ParticleSystem.VelocityOverLifetimeModule velocity = layer.System.velocityOverLifetime;
                velocity.enabled = drifts;
                if (!drifts)
                    continue;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = drift.x;
                velocity.y = 0f;
                velocity.z = drift.z;
            }
        }

        void LateUpdate()
        {
            if (!_on)
                return;
            // Done raining and the last drops are gone.
            if (_amount <= 0f && Alive() == 0)
            {
                SetVisible(false);
                return;
            }
            Follow();
            Tint();
        }

        void SetVisible(bool on)
        {
            _on = on;
            foreach (Layer layer in _layers)
            {
                if (layer.System == null)
                    continue;
                layer.System.gameObject.SetActive(on);
                if (on)
                    layer.System.Play(true);
            }
            _tintedDaylight = -1f;
        }

        int Alive()
        {
            int count = 0;
            foreach (Layer layer in _layers)
                if (layer.System != null)
                    count += layer.System.particleCount;
            return count;
        }

        void Follow()
        {
            if (_camera == null)
            {
                Camera main = ViewCamera.Current;
                if (main == null)
                    return;
                _camera = main.transform;
            }
            // This root rides with the camera; the falling layers keep their offset under it.
            Vector3 at = _camera.position;
            transform.position = at;
            if (!Ground(at, out Vector3 ground))
                return;
            foreach (Layer layer in _layers)
                if (layer.OnGround && layer.System != null)
                    layer.System.transform.position = ground + Vector3.up * 0.05f;
        }

        // The nearest ground below, skipping the player's (and anything's) CharacterController capsule the ray starts in.
        bool Ground(Vector3 from, out Vector3 ground)
        {
            ground = default;
            int count = Physics.RaycastNonAlloc(from, Vector3.down, _hits, 50f, _ground, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].collider is CharacterController || _hits[i].distance >= nearest)
                    continue;
                nearest = _hits[i].distance;
                ground = _hits[i].point;
            }
            return nearest < float.MaxValue;
        }

        void Tint()
        {
            float daylight = _director != null ? _director.Daylight : 1f;
            if (Mathf.Abs(daylight - _tintedDaylight) < 0.02f)
                return;
            _tintedDaylight = daylight;
            Color tint = Color.Lerp(_night, Color.white, daylight);
            for (int i = 0; i < _layers.Length; i++)
            {
                if (_layers[i].System == null)
                    continue;
                ParticleSystem.MinMaxGradient authored = _authored[i];
                ParticleSystem.MainModule main = _layers[i].System.main;
                main.startColor = authored.mode == ParticleSystemGradientMode.TwoColors
                    ? new ParticleSystem.MinMaxGradient(authored.colorMin * tint, authored.colorMax * tint)
                    : new ParticleSystem.MinMaxGradient(authored.color * tint);
            }
        }
    }
}
