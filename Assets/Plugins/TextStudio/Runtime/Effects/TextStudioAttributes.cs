using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Attaches display metadata to a code-defined effect so discovery tooling can render
    /// category grouping, tooltips, and markup examples.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class EffectInfoAttribute : Attribute
    {
        /// <summary>Category used to group effects in editor tooling.</summary>
        public string Category { get; }
        /// <summary>Short human-readable name.</summary>
        public string DisplayName { get; }
        /// <summary>One-line description surfaced as a tooltip.</summary>
        public string Description { get; }
        /// <summary>Suggested markup snippet (e.g. <c>&lt;wave&gt;text&lt;/wave&gt;</c>).</summary>
        public string MarkupSnippet { get; }

        /// <summary>Creates discoverable authoring metadata for a code-defined effect.</summary>
        public EffectInfoAttribute(string category, string displayName, string description = null, string markupSnippet = null)
        {
            Category = category;
            DisplayName = displayName;
            Description = description;
            MarkupSnippet = markupSnippet;
        }
    }

    /// <summary>
    /// Compact ranged float attribute used by Text Studio effect authoring. Renders as a slider
    /// with optional snap increment and unit suffix.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class TextStudioRangeAttribute : PropertyAttribute
    {
        public float Min { get; }
        public float Max { get; }
        public float Snap { get; }
        public string Suffix { get; }
        public bool AllowOverflow { get; }

        /// <summary>Declares an authoring range, optional snap interval, unit suffix, and overflow policy.</summary>
        public TextStudioRangeAttribute(
            float min,
            float max,
            float snap = 0f,
            string suffix = null,
            bool allowOverflow = false)
        {
            Min = min;
            Max = max;
            Snap = snap;
            Suffix = suffix;
            AllowOverflow = allowOverflow;
        }
    }

    /// <summary>
    /// Marks a field with a recommended default. The default property drawer renders a small
    /// reset button next to the field; clicking it writes the default back via reflection.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class TextStudioDefaultAttribute : PropertyAttribute
    {
        public object Value { get; }
        /// <summary>Declares the authoring default used when an effect parameter is omitted.</summary>
        public TextStudioDefaultAttribute(object value) { Value = value; }
    }

    /// <summary>
    /// Adds an alternate inline markup parameter name for an effect field.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
    public sealed class TextStudioParameterAliasAttribute : Attribute
    {
        public string Alias { get; }

        /// <summary>Declares an alternate case-insensitive markup name for an effect parameter.</summary>
        public TextStudioParameterAliasAttribute(string alias)
        {
            Alias = alias ?? string.Empty;
        }
    }
}
