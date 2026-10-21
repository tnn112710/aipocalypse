using System;
using UnityEngine;

namespace Aipocalypse.Core.WorldTimeSystem
{
    // A span of hours that may wrap past midnight (On 18:30, Off 06:00), with fades inside both ends.
    [Serializable]
    public struct TimeWindow
    {
        [Range(0f, 24f)] public float On;
        [Range(0f, 24f)] public float Off;
        [Min(0f), Tooltip("Hours to fade in after On and out before Off")] public float Fade;

        public TimeWindow(float on, float off, float fade = 0f)
        {
            On = on;
            Off = off;
            Fade = fade;
        }

        public bool Contains(float hour) => Weight(hour) > 0f;

        // 0 outside, 1 fully inside, eased in between.
        public float Weight(float hour)
        {
            float length = Off - On;
            if (length <= 0f)
                length += 24f;
            float into = hour - On;
            if (into < 0f)
                into += 24f;
            if (into >= length)
                return 0f;
            if (Fade <= 0f)
                return 1f;
            float w = Mathf.Min(Mathf.Clamp01(into / Fade), Mathf.Clamp01((length - into) / Fade));
            return w * w * (3f - 2f * w);
        }
    }
}
