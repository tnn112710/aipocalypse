using System;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EnvironmentSystem
{
    // The values a weather sets, blended from what is shown now to a new target over a duration. A change mid-blend starts
    // from the blended values, so nothing jumps.
    public sealed class WeatherBlend
    {
        public struct Mix
        {
            public float Strength;
            public float Speed;
            public float Gust;
            public float FogScale;
            public float Nature;
            public float Lightning;
            public float Rain;
        }

        readonly float[] _fromBeds;
        readonly float[] _toBeds;
        readonly float[] _beds;
        Mix _from;
        Mix _to;
        Mix _now;
        float _duration;
        float _progress = 1f;

        public WeatherBlend(int bedCount)
        {
            _fromBeds = new float[bedCount];
            _toBeds = new float[bedCount];
            _beds = new float[bedCount];
        }

        public ref readonly Mix Now => ref _now;
        // Bed volumes, one per bed the owner keeps; read only.
        public float[] Beds => _beds;
        public float Progress => _progress;

        // seconds 0 = cut.
        public void Start(in Mix target, float[] targetBeds, float seconds)
        {
            _from = _now;
            Array.Copy(_beds, _fromBeds, _beds.Length);
            _to = target;
            Array.Copy(targetBeds, _toBeds, _toBeds.Length);
            _duration = seconds;
            _progress = seconds > 0f ? 0f : 1f;
            Evaluate();
        }

        public void Advance(float deltaTime)
        {
            if (_progress >= 1f)
                return;
            _progress = Mathf.Min(1f, _progress + deltaTime / _duration);
            Evaluate();
        }

        void Evaluate()
        {
            float t = _progress;
            _now.Strength = Mathf.Lerp(_from.Strength, _to.Strength, t);
            _now.Speed = Mathf.Lerp(_from.Speed, _to.Speed, t);
            _now.Gust = Mathf.Lerp(_from.Gust, _to.Gust, t);
            _now.FogScale = Mathf.Lerp(_from.FogScale, _to.FogScale, t);
            _now.Nature = Mathf.Lerp(_from.Nature, _to.Nature, t);
            _now.Lightning = Mathf.Lerp(_from.Lightning, _to.Lightning, t);
            _now.Rain = Mathf.Lerp(_from.Rain, _to.Rain, t);
            for (int i = 0; i < _beds.Length; i++)
                _beds[i] = Mathf.Lerp(_fromBeds[i], _toBeds[i], t);
        }
    }
}
