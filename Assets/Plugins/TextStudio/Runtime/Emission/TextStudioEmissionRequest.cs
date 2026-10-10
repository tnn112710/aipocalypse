using System;
using UnityEngine;

namespace TextStudio
{
    public enum TextStudioEmissionOutputSpace
    {
        World,
        ScreenProjected,
        ParentLocal
    }

    public enum TextStudioEmissionOverflowPolicy
    {
        RecycleOldest,
        RejectNew
    }

    /// <summary>
    /// One temporary text request. Use the emitter convenience methods for common calls.
    /// </summary>
    [Serializable]
    public struct TextStudioEmissionRequest
    {
        public TextStudioEmissionPreset Preset;
        public string Text;
        public double Value;
        public bool IsValue;
        public Transform Target;
        public Vector3 Position;
        public Vector3 Offset;
        public string GroupKey;

        /// <summary>Creates a numeric request positioned at a target transform.</summary>
        public static TextStudioEmissionRequest ForValue(
            TextStudioEmissionPreset preset,
            double value,
            Transform target)
        {
            return new TextStudioEmissionRequest
            {
                Preset = preset,
                Text = string.Empty,
                Value = value,
                IsValue = true,
                Target = target,
                Position = target != null ? target.position : Vector3.zero,
                Offset = Vector3.zero,
                GroupKey = string.Empty
            };
        }

        /// <summary>Creates a text request positioned at a target transform.</summary>
        public static TextStudioEmissionRequest ForText(
            TextStudioEmissionPreset preset,
            string text,
            Transform target)
        {
            return new TextStudioEmissionRequest
            {
                Preset = preset,
                Text = text ?? string.Empty,
                Value = 0d,
                IsValue = false,
                Target = target,
                Position = target != null ? target.position : Vector3.zero,
                Offset = Vector3.zero,
                GroupKey = string.Empty
            };
        }
    }
}
