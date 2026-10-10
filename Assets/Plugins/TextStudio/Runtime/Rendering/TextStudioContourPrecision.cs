using UnityEngine;

namespace TextStudio
{
    /// <summary>Power-of-two curve precision tiers bound screen error and avoid new geometry for every camera movement.</summary>
    internal sealed class TextStudioContourPrecision
    {
        internal const float MaximumPixelError = .2f;
        internal float Tolerance => MaximumPixelError / _density;
        internal int Density => _density;
        /// <summary>True when no clock-driven demotion is pending.</summary>
        internal bool IsSettled => _belowSince < 0;
        int _density = 512;
        double _belowSince = -1;
        double _lastTime;

        internal void Update(float projectedDensity, double now)
        {
            if (now < _lastTime) _belowSince = -1;
            _lastTime = now;
            float demand = float.IsPositiveInfinity(projectedDensity) ? 65536 :
                float.IsFinite(projectedDensity) && projectedDensity > 0 ? projectedDensity : 512;
            int next = Mathf.NextPowerOfTwo(Mathf.Clamp(Mathf.CeilToInt(demand), 64, 65536));
            if (next > _density) { _density = next; _belowSince = -1; return; }
            if (next == _density || demand > next * .85f) { _belowSince = -1; return; }
            if (_belowSince < 0) _belowSince = now;
            if (now - _belowSince >= 1) { _density = next; _belowSince = -1; }
        }
    }
}
