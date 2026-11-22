using System;
using UnityEngine;

namespace MB13.Aipocalypse.Core.UI
{
    // One choice on the command wheel: what it says, whether it can be picked, whether it's the current setting.
    public readonly struct WheelSlice
    {
        public readonly string Label;
        public readonly Sprite Icon;
        public readonly bool Enabled;
        public readonly bool Current;
        public readonly Action Picked;

        public WheelSlice(string label, Sprite icon, bool enabled, bool current, Action picked)
        {
            Label = label;
            Icon = icon;
            Enabled = enabled;
            Current = current;
            Picked = picked;
        }
    }
}
