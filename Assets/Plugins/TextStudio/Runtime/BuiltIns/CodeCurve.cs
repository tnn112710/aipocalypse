using System;
using UnityEngine;

namespace TextStudio.BuiltIns
{
    /// <summary>
    /// Code-defined curve. Pure math, no asset. Registered into the global registry.
    /// </summary>
    public sealed class CodeCurve : ITextStudioCurve, ITextStudioBuiltIn
    {
        readonly Func<float, float> _fn;

        public string Id { get; }
        public string Category => "Curves";
        public string DisplayName { get; }
        public string Description { get; }
        public CodeCurve(string id, string displayName, string description, Func<float, float> fn)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            _fn = fn;
        }

        public float Evaluate(float t) => _fn(Mathf.Clamp01(t));
    }
}
