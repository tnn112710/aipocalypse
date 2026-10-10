using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio.Editor
{
    /// <summary>Independent resolution and full signed-distance interval for one prepared font tier.</summary>
    public readonly struct TextStudioFontAppearanceTierRequest
    {
        public float PixelsPerEm { get; }
        public float DistanceRangeEm { get; }

        public TextStudioFontAppearanceTierRequest(float pixelsPerEm, float distanceRangeEm)
        {
            if (!float.IsFinite(pixelsPerEm) || pixelsPerEm < 16 || pixelsPerEm > 1024)
                throw new ArgumentOutOfRangeException(nameof(pixelsPerEm), "Use 16 to 1,024 pixels per em.");
            if (!float.IsFinite(distanceRangeEm) || distanceRangeEm < .02f || distanceRangeEm > 4)
                throw new ArgumentOutOfRangeException(nameof(distanceRangeEm), "Use a full distance interval from 0.02 to 4 em.");
            PixelsPerEm = pixelsPerEm; DistanceRangeEm = distanceRangeEm;
        }

        /// <summary>Builds compact near-edge tiers and an optional wider-distance tier, without duplicate requests.</summary>
        public static TextStudioFontAppearanceTierRequest[] Adaptive(float pixelsPerEm, float safeDistanceEm)
        {
            TextStudioFontAppearanceTierRequest widest = new TextStudioFontAppearanceTierRequest(pixelsPerEm, 2 * safeDistanceEm);
            float lowDensity = Mathf.Min(96, pixelsPerEm), nearRange = Mathf.Min(.3f, widest.DistanceRangeEm);
            List<TextStudioFontAppearanceTierRequest> tiers = new List<TextStudioFontAppearanceTierRequest>(3)
            { new TextStudioFontAppearanceTierRequest(lowDensity, nearRange) };
            if (pixelsPerEm > lowDensity) tiers.Add(new TextStudioFontAppearanceTierRequest(pixelsPerEm, nearRange));
            if (widest.DistanceRangeEm > nearRange) tiers.Add(widest);
            return tiers.ToArray();
        }

        internal static TextStudioFontAppearanceTierRequest[] Snapshot(IReadOnlyList<TextStudioFontAppearanceTierRequest> tiers)
        {
            if (tiers == null) return Adaptive(160, .3f);
            if (tiers.Count == 0 || tiers.Count > 6) throw new ArgumentException("Prepare between one and six font field tiers.", nameof(tiers));
            TextStudioFontAppearanceTierRequest[] result = new TextStudioFontAppearanceTierRequest[tiers.Count];
            for (int index = 0; index < result.Length; index++)
            {
                TextStudioFontAppearanceTierRequest tier = tiers[index];
                result[index] = new TextStudioFontAppearanceTierRequest(tier.PixelsPerEm, tier.DistanceRangeEm);
                for (int previous = 0; previous < index; previous++)
                    if (result[previous].PixelsPerEm == tier.PixelsPerEm && result[previous].DistanceRangeEm == tier.DistanceRangeEm)
                        throw new ArgumentException("Each font field tier must have a distinct density or distance interval.", nameof(tiers));
            }
            return result;
        }
    }
}
