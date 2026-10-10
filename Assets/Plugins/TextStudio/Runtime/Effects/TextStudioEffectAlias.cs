using System;

namespace TextStudio
{
    /// <summary>Declares an additional case-insensitive lookup name for a code-defined effect.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class TextStudioEffectAliasAttribute : Attribute
    {
        public string Alias { get; }
        public string Note { get; }

        /// <summary>Declares an alias and optional migration or compatibility note.</summary>
        public TextStudioEffectAliasAttribute(string alias, string note = "")
        {
            Alias = alias ?? string.Empty;
            Note = note ?? string.Empty;
        }
    }
}
