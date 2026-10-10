using System;
using System.Collections.Generic;
using System.Text;
using TextStudio.BuiltIns;
using UnityEngine;

namespace TextStudio
{
    public enum TextStudioExtensionTagShape
    {
        Point,
        Range
    }

    [Flags]
    public enum TextStudioExtensionTagDispatch
    {
        None = 0,
        Plan = 1 << 0,
        Reveal = 1 << 1,
        FlushOnSkip = 1 << 2
    }

    public enum TextStudioExtensionTagBoundary
    {
        Point,
        Range
    }

    /// <summary>Immutable schema and dispatch policy for a registered extension tag.</summary>
    public sealed class TextStudioExtensionTagDefinition
    {
        readonly string[] _aliases;
        readonly string[] _examples;
        readonly TextStudioTagParameterDefinition[] _parameters;

        public string Id { get; }
        public string OwnerId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public IReadOnlyList<string> Aliases => _aliases;
        public IReadOnlyList<string> Examples => _examples;
        public IReadOnlyList<TextStudioTagParameterDefinition> Parameters => _parameters;
        public TextStudioExtensionTagShape Shape { get; }
        public TextStudioExtensionTagDispatch Dispatch { get; }
        public bool AllowAliasOverride { get; }

        /// <summary>Creates a definition used for parsing, validation, discovery, and dispatch.</summary>
        public TextStudioExtensionTagDefinition(
            string id,
            string ownerId,
            string displayName = null,
            string description = null,
            TextStudioExtensionTagShape shape = TextStudioExtensionTagShape.Point,
            TextStudioExtensionTagDispatch dispatch =
                TextStudioExtensionTagDispatch.Plan |
                TextStudioExtensionTagDispatch.Reveal,
            string[] aliases = null,
            bool allowAliasOverride = false,
            string[] examples = null,
            TextStudioTagParameterDefinition[] parameters = null)
        {
            Id = id ?? string.Empty;
            OwnerId = ownerId ?? string.Empty;
            DisplayName = displayName ?? Id;
            Description = description ?? string.Empty;
            Shape = shape;
            Dispatch = dispatch;
            AllowAliasOverride = allowAliasOverride;
            _aliases = aliases ?? Array.Empty<string>();
            _examples = examples ?? Array.Empty<string>();
            _parameters = parameters ?? Array.Empty<TextStudioTagParameterDefinition>();
        }

        public bool DispatchesOnPlan => (Dispatch & TextStudioExtensionTagDispatch.Plan) != 0;
        public bool DispatchesOnReveal => (Dispatch & TextStudioExtensionTagDispatch.Reveal) != 0;
        public bool FlushesOnSkip => (Dispatch & TextStudioExtensionTagDispatch.FlushOnSkip) != 0;
    }

    public enum TextStudioTagParameterOperation
    {
        Set,
        Multiply
    }

    /// <summary>One parsed named parameter, flag, or multiplicative modifier from a tag.</summary>
    public readonly struct TextStudioNamedTagParameter
    {
        public readonly string Name;
        public readonly string Value;
        public readonly bool IsFlag;
        public readonly TextStudioTagParameterOperation Operation;

        /// <summary>Creates a flag or direct named assignment.</summary>
        public TextStudioNamedTagParameter(string name, string value, bool isFlag)
            : this(name, value, isFlag, TextStudioTagParameterOperation.Set)
        {
        }

        /// <summary>Creates a flag, direct assignment, or multiplicative assignment.</summary>
        public TextStudioNamedTagParameter(
            string name,
            string value,
            bool isFlag,
            TextStudioTagParameterOperation operation)
        {
            Name = name ?? string.Empty;
            Value = value ?? string.Empty;
            IsFlag = isFlag;
            Operation = isFlag ? TextStudioTagParameterOperation.Set : operation;
        }
    }

    /// <summary>
    /// Allocation-free read view over positional and named parameters parsed from one tag.
    /// Lookup is case-insensitive and later named values take precedence.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // Empty is immutable value metadata shared by all tag parsing paths.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public readonly partial struct TextStudioTagParameters
    {
        public static readonly TextStudioTagParameters Empty = new TextStudioTagParameters(
            string.Empty,
            Array.Empty<string>(),
            Array.Empty<TextStudioNamedTagParameter>());

        readonly string _raw;
        readonly string[] _positional;
        readonly TextStudioNamedTagParameter[] _named;

        public string Raw => _raw ?? string.Empty;
        public IReadOnlyList<string> Positional => _positional ?? Array.Empty<string>();
        public IReadOnlyList<TextStudioNamedTagParameter> Named => _named ?? Array.Empty<TextStudioNamedTagParameter>();
        public int PositionalCount => _positional != null ? _positional.Length : 0;
        public int NamedCount => _named != null ? _named.Length : 0;

        /// <summary>Creates a read-only parameter view over caller-supplied arrays that must not be mutated afterward.</summary>
        public TextStudioTagParameters(string raw, string[] positional, TextStudioNamedTagParameter[] named)
        {
            _raw = raw ?? string.Empty;
            _positional = positional ?? Array.Empty<string>();
            _named = named ?? Array.Empty<TextStudioNamedTagParameter>();
        }

        /// <summary>Gets a positional string or a fallback when the index is absent.</summary>
        public string GetString(int index, string defaultValue = "")
        {
            if (_positional == null || index < 0 || index >= _positional.Length)
                return defaultValue;
            return _positional[index] ?? defaultValue;
        }

        /// <summary>Gets the last named string assignment or a fallback when it is absent.</summary>
        public string GetString(string name, string defaultValue = "")
        {
            if (TryGetString(name, out string value))
                return value;
            return defaultValue;
        }

        /// <summary>Tries to read the last named direct string assignment.</summary>
        public bool TryGetString(string name, out string value)
        {
            value = string.Empty;
            if (!TryGetNamedParameter(name, out TextStudioNamedTagParameter parameter) ||
                parameter.IsFlag ||
                parameter.Operation != TextStudioTagParameterOperation.Set)
            {
                return false;
            }

            value = parameter.Value;
            return true;
        }

        /// <summary>Tries to read the last parsed parameter with a case-insensitive name.</summary>
        public bool TryGetNamedParameter(string name, out TextStudioNamedTagParameter parameter)
        {
            parameter = default;
            if (string.IsNullOrEmpty(name) || _named == null)
                return false;

            for (int i = _named.Length - 1; i >= 0; i--)
            {
                if (!string.Equals(_named[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    continue;

                parameter = _named[i];
                return true;
            }

            return false;
        }

        /// <summary>Reports whether the last parameter with the requested name is a flag.</summary>
        public bool HasFlag(string name)
        {
            if (string.IsNullOrEmpty(name) || _named == null)
                return false;

            for (int i = _named.Length - 1; i >= 0; i--)
            {
                if (!string.Equals(_named[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    continue;

                return _named[i].IsFlag;
            }

            return false;
        }

        /// <summary>Parses a positional value as an invariant finite or non-finite floating-point value.</summary>
        public bool TryGetFloat(int index, out float value)
        {
            return float.TryParse(
                GetString(index),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);
        }

        /// <summary>Parses a named direct assignment as an invariant floating-point value.</summary>
        public bool TryGetFloat(string name, out float value)
        {
            value = 0f;
            return TryGetString(name, out string raw) &&
                   float.TryParse(
                       raw,
                       System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture,
                       out value);
        }

        /// <summary>Resolves the last named assignment or multiplier against a baseline value.</summary>
        /// <returns><see langword="false"/> for missing, malformed, NaN, or infinite results.</returns>
        public bool TryResolveFloat(
            float baseline,
            string name,
            out float value,
            params string[] aliases)
        {
            value = baseline;
            if (!TryGetLastNamedParameter(name, aliases, out TextStudioNamedTagParameter parameter) ||
                parameter.IsFlag ||
                !float.TryParse(
                    parameter.Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float operand) ||
                float.IsNaN(operand) ||
                float.IsInfinity(operand))
            {
                return false;
            }

            float resolved = parameter.Operation == TextStudioTagParameterOperation.Multiply
                ? baseline * operand
                : operand;
            if (float.IsNaN(resolved) || float.IsInfinity(resolved))
                return false;

            value = resolved;
            return true;
        }

        bool TryGetLastNamedParameter(
            string name,
            string[] aliases,
            out TextStudioNamedTagParameter parameter)
        {
            parameter = default;
            if (string.IsNullOrEmpty(name) || _named == null)
                return false;

            for (int i = _named.Length - 1; i >= 0; i--)
            {
                TextStudioNamedTagParameter candidate = _named[i];
                if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    parameter = candidate;
                    return true;
                }

                for (int aliasIndex = 0; aliases != null && aliasIndex < aliases.Length; aliasIndex++)
                {
                    if (!string.Equals(candidate.Name, aliases[aliasIndex], StringComparison.OrdinalIgnoreCase))
                        continue;

                    parameter = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Parses a named direct assignment as an invariant integer.</summary>
        public bool TryGetInt(string name, out int value)
        {
            value = 0;
            return TryGetString(name, out string raw) &&
                   int.TryParse(
                       raw,
                       System.Globalization.NumberStyles.Integer,
                       System.Globalization.CultureInfo.InvariantCulture,
                       out value);
        }

        /// <summary>Parses a positional color using Text Studio color syntax.</summary>
        public bool TryGetColor(int index, out Color32 value)
        {
            return TextStudioColorUtility.TryParseColor(GetString(index), out value);
        }

        /// <summary>Parses a named color, including multi-token color syntax.</summary>
        public bool TryGetColor(string name, out Color32 value)
        {
            value = default;
            if (string.IsNullOrEmpty(name) || _named == null)
                return false;

            for (int i = _named.Length - 1; i >= 0; i--)
            {
                if (!string.Equals(_named[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (_named[i].IsFlag ||
                    _named[i].Operation != TextStudioTagParameterOperation.Set)
                {
                    return false;
                }

                if (TextStudioColorUtility.TryParseColor(_named[i].Value, out value))
                    return true;

                return TryGetColorFromNamedSequence(i, out value);
            }

            return false;
        }

        internal bool TryGetColorParameterString(int index, out string value)
        {
            value = GetString(index);
            return !string.IsNullOrEmpty(value);
        }

        internal bool TryGetColorParameterString(string name, out string value)
        {
            value = string.Empty;
            if (string.IsNullOrEmpty(name) || _named == null)
                return false;

            for (int i = _named.Length - 1; i >= 0; i--)
            {
                if (!string.Equals(_named[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (_named[i].IsFlag ||
                    _named[i].Operation != TextStudioTagParameterOperation.Set)
                {
                    return false;
                }

                value = _named[i].Value;
                if (TextStudioColorUtility.TryParseColor(value, out _))
                    return true;

                string rawValue = value;
                if (TryGetColorStringFromNamedSequence(i, out value))
                    return true;

                value = rawValue;
                return !string.IsNullOrEmpty(value);
            }

            return false;
        }

        internal bool IsColorContinuationFlag(int namedIndex, TextStudioTagParameterDefinition previousDefinition)
        {
            if (_named == null ||
                namedIndex <= 0 ||
                namedIndex >= _named.Length ||
                !_named[namedIndex].IsFlag ||
                previousDefinition == null ||
                previousDefinition.Kind != TextStudioTagParameterKind.Color)
                return false;

            int start = namedIndex - 1;
            while (start >= 0 && _named[start].IsFlag)
                start--;

            return start >= 0 &&
                   string.Equals(_named[start].Name, previousDefinition.Name, StringComparison.OrdinalIgnoreCase) &&
                   TryGetColorStringFromNamedSequence(start, namedIndex, out _);
        }

        bool TryGetColorFromNamedSequence(int namedIndex, out Color32 value)
        {
            value = default;
            return TryGetColorStringFromNamedSequence(namedIndex, out string raw) &&
                   TextStudioColorUtility.TryParseColor(raw, out value);
        }

        bool TryGetColorStringFromNamedSequence(int namedIndex, out string value)
        {
            return TryGetColorStringFromNamedSequence(namedIndex, -1, out value);
        }

        bool TryGetColorStringFromNamedSequence(int namedIndex, int requiredFlagIndex, out string value)
        {
            value = string.Empty;
            if (_named == null ||
                namedIndex < 0 ||
                namedIndex >= _named.Length ||
                _named[namedIndex].IsFlag ||
                string.IsNullOrEmpty(_named[namedIndex].Value))
                return false;

            StringBuilder builder = null;
            for (int i = namedIndex + 1; i < _named.Length && _named[i].IsFlag; i++)
            {
                if (builder == null)
                    builder = new StringBuilder(_named[namedIndex].Value);

                builder.Append(' ');
                builder.Append(_named[i].Name);

                string candidate = builder.ToString();
                if (TextStudioColorUtility.TryParseColor(candidate, out _) &&
                    (requiredFlagIndex < 0 || i == requiredFlagIndex))
                {
                    value = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Parses a flag or named boolean using true/false, 1/0, yes/no, and on/off syntax.</summary>
        public bool TryGetBool(string name, out bool value)
        {
            value = false;
            if (HasFlag(name))
            {
                value = true;
                return true;
            }

            if (!TryGetString(name, out string raw))
                return false;

            if (bool.TryParse(raw, out value))
                return true;

            if (string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            if (string.Equals(raw, "0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(raw, "no", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(raw, "off", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }

            return false;
        }
    }

    /// <summary>Immutable occurrence of a registered extension tag mapped to source and visible-glyph ranges.</summary>
    public readonly struct TextStudioExtensionTag
    {
        public readonly TextStudioExtensionTagDefinition Definition;
        public readonly string TagId;
        public readonly string AuthoredName;
        public readonly int SourceStart;
        public readonly int SourceEnd;
        public readonly int GlyphStart;
        public readonly int GlyphCount;
        public readonly TextStudioExtensionTagBoundary Boundary;
        public readonly TextStudioTagParameters Parameters;

        /// <summary>Creates a dispatched extension-tag occurrence.</summary>
        public TextStudioExtensionTag(
            TextStudioExtensionTagDefinition definition,
            string authoredName,
            int sourceStart,
            int sourceEnd,
            int glyphStart,
            int glyphCount,
            TextStudioExtensionTagBoundary boundary,
            TextStudioTagParameters parameters)
        {
            Definition = definition;
            TagId = definition != null ? definition.Id : string.Empty;
            AuthoredName = authoredName ?? string.Empty;
            SourceStart = sourceStart;
            SourceEnd = sourceEnd;
            GlyphStart = glyphStart;
            GlyphCount = glyphCount;
            Boundary = boundary;
            Parameters = parameters;
        }

        public int SourceLength => SourceEnd - SourceStart;
        public bool DispatchesOnPlan => Definition != null && Definition.DispatchesOnPlan;
        public bool DispatchesOnReveal => Definition != null && Definition.DispatchesOnReveal;
        public bool FlushesOnSkip => Definition != null && Definition.FlushesOnSkip;
    }

    /// <summary>Receives compiled extension tags and reveal-time tag events for one host.</summary>
    public interface ITextStudioExtensionTagProcessor
    {
        /// <summary>Notifies the processor after the compiled plan or its extension-tag set changes.</summary>
        /// <param name="plan">Transient compiled-plan view that must not be retained.</param>
        void OnExtensionTagsChanged(TextStudioPlanView plan);

        /// <summary>Notifies the processor when reveal reaches a tag configured for reveal dispatch.</summary>
        /// <param name="tag">Tag occurrence valid only for the duration of the call.</param>
        void OnExtensionTagReached(in TextStudioExtensionTag tag);
    }

    internal sealed class TextStudioExtensionTagRegistry
    {
        // Markup parsing hashes these keys: StringComparer.OrdinalIgnoreCase allocates an upper-case copy per lookup on Mono.
        readonly Dictionary<string, TextStudioExtensionTagDefinition> _entries =
            new Dictionary<string, TextStudioExtensionTagDefinition>(TextStudioIgnoreCaseComparer.Instance);

        uint _revision;
        public int Count => _entries.Count;
        internal uint Revision => _revision;

        public bool TryGet(string tagName, out TextStudioExtensionTagDefinition definition)
        {
            if (!TextStudioTagUtility.IsExtensionTagCandidate(tagName))
            {
                definition = null;
                return false;
            }

            return _entries.TryGetValue(tagName, out definition);
        }

        public bool Register(TextStudioExtensionTagDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.Id))
            {
                Debug.LogWarning("[TextStudio] Extension Tags: Skipping null or empty definition.");
                return false;
            }

            bool registered = RegisterName(definition.Id, definition, false);
            if (!registered)
                return false;

            IReadOnlyList<string> aliases = definition.Aliases;
            for (int i = 0; i < aliases.Count; i++)
                registered |= RegisterName(aliases[i], definition, definition.AllowAliasOverride);
            return registered;
        }

        public bool Unregister(string tagName)
        {
            if (string.IsNullOrEmpty(tagName))
                return false;
            if (!_entries.Remove(tagName)) return false;
            unchecked { _revision++; }
            return true;
        }

        public void UnregisterOwner(string ownerId)
        {
            if (string.IsNullOrEmpty(ownerId))
                return;

            List<string> remove = new List<string>();
            foreach (KeyValuePair<string, TextStudioExtensionTagDefinition> pair in _entries)
            {
                if (pair.Value != null &&
                    string.Equals(pair.Value.OwnerId, ownerId, StringComparison.OrdinalIgnoreCase))
                    remove.Add(pair.Key);
            }

            for (int i = 0; i < remove.Count; i++)
                _entries.Remove(remove[i]);
            if (remove.Count > 0) unchecked { _revision++; }
        }

        public void Reset()
        {
            if (_entries.Count == 0) return;
            _entries.Clear();
            unchecked { _revision++; }
        }

        public IEnumerable<TextStudioExtensionTagDefinition> GetAllDefinitions()
        {
            HashSet<string> seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, TextStudioExtensionTagDefinition> pair in _entries)
            {
                TextStudioExtensionTagDefinition definition = pair.Value;
                if (definition == null || string.IsNullOrEmpty(definition.Id) || !seenIds.Add(definition.Id))
                    continue;

                yield return definition;
            }
        }

        bool RegisterName(string tagName, TextStudioExtensionTagDefinition definition, bool allowOverride)
        {
            if (string.IsNullOrWhiteSpace(tagName))
                return false;

            tagName = tagName.Trim();
            if (!TextStudioTagUtility.IsExtensionTagCandidate(tagName))
            {
                Debug.LogWarning("[TextStudio] Extension Tags: '" + tagName + "' is reserved by Text Studio or TMP.");
                return false;
            }

            if (_entries.TryGetValue(tagName, out TextStudioExtensionTagDefinition existing) && !allowOverride)
            {
                Debug.LogWarning("[TextStudio] Extension Tags: Duplicate tag '" + tagName + "' from '" +
                    definition.OwnerId + "' conflicts with '" + existing.OwnerId + "'.");
                return false;
            }

            if (!_entries.TryGetValue(tagName, out TextStudioExtensionTagDefinition previous) || !ReferenceEquals(previous, definition))
            {
                _entries[tagName] = definition;
                unchecked { _revision++; }
            }
            return true;
        }
    }

    internal static class TextStudioTagUtility
    {
        public static bool IsReservedTag(string tagName, bool hasParameter)
        {
            if (string.IsNullOrEmpty(tagName))
                return true;
            TextStudioRegistry.EnsureInitialized();
            if (BuiltInStyles.IsNativeRichTextFormatting(tagName, hasParameter))
                return true;
            if (BuiltInStyles.IsCustomDecorationTag(tagName))
                return true;
            if (BuiltInCharacterSubstitutions.IsCharacterSubstitutionTag(tagName))
                return true;
            if (BuiltInStyles.TryResolve(tagName, out _))
                return true;
            if (BuiltInActions.IsWaitTag(tagName) ||
                BuiltInActions.IsMarkerTag(tagName) ||
                BuiltInActions.TryGetCanonicalActionId(tagName, out _))
                return true;
            if (TextStudioRegistry.Effects.TryGetQuiet(tagName, out _))
                return true;
            if (TextStudioRegistry.Actions.TryGetQuiet(tagName, out _))
                return true;
            return false;
        }

        public static bool IsExtensionTagCandidate(string tagName)
        {
            return !IsReservedTag(tagName, hasParameter: false) &&
                   !IsReservedTag(tagName, hasParameter: true);
        }

        public static bool TryGetExtensionTag(
            string tagName,
            bool hasParameter,
            out TextStudioExtensionTagDefinition definition)
        {
            definition = null;
            if (IsReservedTag(tagName, hasParameter))
                return false;
            return TextStudioRegistry.ExtensionTags.TryGet(tagName, out definition);
        }
    }
}
