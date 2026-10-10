using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>One font's quality state. Promotion is immediate; camera-driven demotion needs headroom and sustained demand.</summary>
    internal sealed class TextStudioPreparedFieldSelection
    {
        internal const double DemotionDelay = 1;
        internal const float DemotionHeadroom = .85f;
        TextStudioFontAppearanceData _source;
        uint _revision;
        TextStudio2DSourceFieldRequest _baseRequest;
        int _pendingTier = -1;
        double _demoteAt;
        double _lastTime;
        internal int Tier { get; private set; } = -1;
        internal int PreviousTier { get; private set; } = -1;
        internal float RequiredPixelsPerEm { get; private set; }
        internal float RequiredSafeDistanceEm { get; private set; }

        internal void Update(TextStudioFontAppearanceData source, in TextStudio2DSourceFieldRequest request,
            float projectedPixelsPerEm, float pixelDistanceEm, double now)
        {
            bool changedSource = _source != source || _revision != (source != null ? source.Revision : 0);
            bool changedRequest = request.Quality != _baseRequest.Quality || request.MinimumPixelsPerEm != _baseRequest.MinimumPixelsPerEm ||
                request.MinimumSafeDistanceEm != _baseRequest.MinimumSafeDistanceEm;
            if (changedSource || changedRequest || now < _lastTime)
            { Tier = PreviousTier = _pendingTier = -1; }
            _source = source; _revision = source != null ? source.Revision : 0; _baseRequest = request; _lastTime = now;
            RequiredPixelsPerEm = Mathf.Max(request.MinimumPixelsPerEm, FiniteDemand(projectedPixelsPerEm));
            RequiredSafeDistanceEm = Mathf.Max(request.MinimumSafeDistanceEm, FiniteDemand(pixelDistanceEm));
            int desired = source != null && source.IsPreparedDataValid && request.Quality != TextStudio2DSourceFieldQuality.Native
                ? BestAvailableTier(source, RequiredPixelsPerEm, RequiredSafeDistanceEm) : -1;
            if (desired < 0) { Tier = PreviousTier = _pendingTier = -1; return; }
            if (Tier < 0) { Tier = desired; return; }
            if (desired == Tier) { _pendingTier = -1; return; }
            TextStudioPreparedFieldTier current = source.GetTier(Tier), candidate = source.GetTier(desired);
            if (candidate.PixelsPerEm > current.PixelsPerEm || candidate.SafeDistanceEm > current.SafeDistanceEm)
            { Switch(desired); return; }
            bool headroom = RequiredPixelsPerEm <= Mathf.Max(request.MinimumPixelsPerEm, candidate.PixelsPerEm * DemotionHeadroom) &&
                RequiredSafeDistanceEm <= Mathf.Max(request.MinimumSafeDistanceEm, candidate.SafeDistanceEm * DemotionHeadroom);
            if (!headroom) { _pendingTier = -1; return; }
            if (_pendingTier != desired) { _pendingTier = desired; _demoteAt = now + DemotionDelay; }
            if (now >= _demoteAt) Switch(desired);
        }

        /// <summary>True when an update with unchanged demand and this source cannot change the selection, so no clock-driven demotion is pending.</summary>
        internal bool IsSettledFor(TextStudioFontAppearanceData source) =>
            _pendingTier < 0 && _source == source && _revision == (source != null ? source.Revision : 0);

        void Switch(int tier) { PreviousTier = Tier; Tier = tier; _pendingTier = -1; }
        static float FiniteDemand(float value) => float.IsNaN(value) || value <= 0 ? 0 : Mathf.Min(value, 65536);

        internal static int BestAvailableTier(TextStudioFontAppearanceData data, float density, float extent)
        {
            int exact = data.SelectTier(density, extent);
            if (exact >= 0) return exact;
            int best = -1;
            bool bestExtent = false;
            for (int index = 0; index < data.TierCount; index++)
            {
                TextStudioPreparedFieldTier tier = data.GetTier(index);
                if (tier == null || tier.GlyphCount == 0 || tier.PageCount == 0) continue;
                bool meetsExtent = tier.SafeDistanceEm >= extent;
                if (best < 0) { best = index; bestExtent = meetsExtent; continue; }
                TextStudioPreparedFieldTier current = data.GetTier(best);
                bool better = meetsExtent && !bestExtent || meetsExtent == bestExtent &&
                    (meetsExtent ? tier.PixelsPerEm > current.PixelsPerEm || tier.PixelsPerEm == current.PixelsPerEm && tier.SafeDistanceEm < current.SafeDistanceEm
                        : tier.SafeDistanceEm > current.SafeDistanceEm || tier.SafeDistanceEm == current.SafeDistanceEm && tier.PixelsPerEm > current.PixelsPerEm);
                if (better) { best = index; bestExtent = meetsExtent; }
            }
            return best;
        }
    }
}
