using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Stable serialized identity shared by Text Studio appearance-stack items.</summary>
    [Serializable]
    public struct TextStudioStackItemId : IEquatable<TextStudioStackItemId>
    {
        [SerializeField] string _value;

        /// <summary>Canonical namespaced identifier.</summary>
        public string Value => _value ?? string.Empty;

        /// <summary>Whether the identifier is canonical and safe to persist.</summary>
        public bool IsValid => IsCanonical(Value);

        TextStudioStackItemId(string value)
        {
            _value = value;
        }

        /// <summary>Creates a validated identifier or throws when the value is malformed.</summary>
        public static TextStudioStackItemId Create(string value)
        {
            if (!TryCreate(value, out TextStudioStackItemId id))
                throw new ArgumentException("Stack item IDs must be lowercase namespaced identifiers.", nameof(value));
            return id;
        }

        /// <summary>Creates a new authoring identity in the supplied namespace.</summary>
        public static TextStudioStackItemId CreateUnique(string itemNamespace)
        {
            string normalizedNamespace = NormalizeNamespace(itemNamespace);
            return new TextStudioStackItemId(
                normalizedNamespace + "." + Guid.NewGuid().ToString("N"));
        }

        /// <summary>Attempts to create a canonical identifier without throwing.</summary>
        public static bool TryCreate(string value, out TextStudioStackItemId id)
        {
            string normalized = value != null ? value.Trim() : string.Empty;
            if (!IsCanonical(normalized))
            {
                id = default;
                return false;
            }

            id = new TextStudioStackItemId(normalized);
            return true;
        }

        /// <summary>Returns true when a string follows the stable namespaced ID grammar.</summary>
        public static bool IsCanonical(string value)
        {
            if (string.IsNullOrEmpty(value) ||
                value.Length < 3 ||
                value.Length > 96 ||
                value[0] == '.' ||
                value[value.Length - 1] == '.' ||
                value.IndexOf('.') <= 0)
            {
                return false;
            }

            bool previousDot = false;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                bool valid = character >= 'a' && character <= 'z' ||
                             character >= '0' && character <= '9' ||
                             character == '.' ||
                             character == '_' ||
                             character == '-';
                if (!valid || character == '.' && previousDot)
                    return false;
                previousDot = character == '.';
            }
            return true;
        }

        public bool Equals(TextStudioStackItemId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudioStackItemId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        /// <summary>Reports whether both values identify the same persistent stack item.</summary>
        public static bool operator ==(TextStudioStackItemId left, TextStudioStackItemId right)
        {
            return left.Equals(right);
        }

        /// <summary>Reports whether the values identify different persistent stack items.</summary>
        public static bool operator !=(TextStudioStackItemId left, TextStudioStackItemId right)
        {
            return !left.Equals(right);
        }

        static string NormalizeNamespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "item";

            string normalized = value.Trim().ToLowerInvariant();
            char[] characters = normalized.ToCharArray();
            for (int i = 0; i < characters.Length; i++)
            {
                char character = characters[i];
                bool valid = character >= 'a' && character <= 'z' ||
                             character >= '0' && character <= '9' ||
                             character == '_' ||
                             character == '-';
                if (!valid)
                    characters[i] = '-';
            }
            normalized = new string(characters).Trim('-', '_');
            if (string.IsNullOrEmpty(normalized))
                return "item";
            const int MaximumUniqueNamespaceLength = 63;
            return normalized.Length <= MaximumUniqueNamespaceLength
                ? normalized
                : normalized.Substring(0, MaximumUniqueNamespaceLength);
        }
    }

    /// <summary>Minimal shared authoring contract for stack rows.</summary>
    public interface ITextStudioStackItem
    {
        TextStudioStackItemId ItemId { get; }
        string DisplayName { get; }
        bool IsEnabled { get; }
        bool IsSolo { get; }
    }

    /// <summary>Read-only shared contract implemented by persistent stack assets.</summary>
    public interface ITextStudioStackAsset
    {
        int SchemaVersion { get; }
        int ItemCount { get; }
        /// <summary>Returns the persistent identifier of the stack item at the specified index.</summary>
        TextStudioStackItemId GetItemId(int index);
    }

    /// <summary>Classifies appearance-stack validation and compilation feedback as information, warnings, or errors.</summary>
    public enum TextStudioStackDiagnosticSeverity : byte
    {
        Information = 0,
        Warning = 1,
        Error = 2
    }

    /// <summary>One deterministic validation or compilation diagnostic.</summary>
    public readonly struct TextStudioStackDiagnostic
    {
        public TextStudioStackDiagnosticSeverity Severity { get; }
        public string Code { get; }
        public int ItemIndex { get; }
        public TextStudioStackItemId ItemId { get; }
        public string Message { get; }

        /// <summary>Creates a stack diagnostic identifying its severity, code, item location, and message.</summary>
        public TextStudioStackDiagnostic(
            TextStudioStackDiagnosticSeverity severity,
            string code,
            int itemIndex,
            TextStudioStackItemId itemId,
            string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            ItemIndex = itemIndex;
            ItemId = itemId;
            Message = message ?? string.Empty;
        }
    }

    /// <summary>Independent deterministic identities for style compilation domains.</summary>
    public readonly struct TextStudioStackFingerprint : IEquatable<TextStudioStackFingerprint>
    {
        public ulong Topology { get; }
        public ulong Parameters { get; }
        public ulong RenderState { get; }
        public ulong Bounds { get; }
        public ulong Complete { get; }

        /// <summary>Creates a fingerprint with independent topology, parameter, render-state, bounds, and complete hashes.</summary>
        public TextStudioStackFingerprint(
            ulong topology,
            ulong parameters,
            ulong renderState,
            ulong bounds,
            ulong complete)
        {
            Topology = topology;
            Parameters = parameters;
            RenderState = renderState;
            Bounds = bounds;
            Complete = complete;
        }

        public bool Equals(TextStudioStackFingerprint other)
        {
            return Topology == other.Topology &&
                   Parameters == other.Parameters &&
                   RenderState == other.RenderState &&
                   Bounds == other.Bounds &&
                   Complete == other.Complete;
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudioStackFingerprint other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Topology.GetHashCode();
                hash = hash * 397 ^ Parameters.GetHashCode();
                hash = hash * 397 ^ RenderState.GetHashCode();
                hash = hash * 397 ^ Bounds.GetHashCode();
                return hash * 397 ^ Complete.GetHashCode();
            }
        }

        /// <summary>Reports whether every fingerprint domain has the same hash in both values.</summary>
        public static bool operator ==(
            TextStudioStackFingerprint left,
            TextStudioStackFingerprint right)
        {
            return left.Equals(right);
        }

        /// <summary>Reports whether at least one fingerprint domain differs between the values.</summary>
        public static bool operator !=(
            TextStudioStackFingerprint left,
            TextStudioStackFingerprint right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>Classifies which compiled stack domains changed.</summary>
    [Flags]
    public enum TextStudioStackChange : byte
    {
        None = 0,
        Topology = 1 << 0,
        Parameters = 1 << 1,
        RenderState = 1 << 2,
        Bounds = 1 << 3,
        All = Topology | Parameters | RenderState | Bounds
    }

    /// <summary>Compares stack fingerprint domains to distinguish topology, parameter, render-state, and bounds changes.</summary>
    public static class TextStudioStackFingerprintUtility
    {
        /// <summary>Classifies domain changes without treating a parameter edit as topology.</summary>
        public static TextStudioStackChange Classify(
            in TextStudioStackFingerprint previous,
            in TextStudioStackFingerprint current)
        {
            TextStudioStackChange change = TextStudioStackChange.None;
            if (previous.Topology != current.Topology)
                change |= TextStudioStackChange.Topology;
            if (previous.Parameters != current.Parameters)
                change |= TextStudioStackChange.Parameters;
            if (previous.RenderState != current.RenderState)
                change |= TextStudioStackChange.RenderState;
            if (previous.Bounds != current.Bounds)
                change |= TextStudioStackChange.Bounds;
            return change;
        }
    }

    internal struct TextStudioDeterministicHash64
    {
        const ulong Offset = 14695981039346656037UL;
        const ulong Prime = 1099511628211UL;
        ulong _value;
        bool _initialized;

        internal ulong Value => _initialized ? _value : Offset;

        internal void Add(bool value) => Add(value ? 1u : 0u);
        internal void Add(byte value) => Add((uint)value);
        internal void Add(int value) => Add(unchecked((uint)value));
        internal void Add(ulong value)
        {
            Add(unchecked((uint)value));
            Add(unchecked((uint)(value >> 32)));
        }
        internal void Add(uint value)
        {
            EnsureInitialized();
            _value = (_value ^ (byte)value) * Prime;
            _value = (_value ^ (byte)(value >> 8)) * Prime;
            _value = (_value ^ (byte)(value >> 16)) * Prime;
            _value = (_value ^ (byte)(value >> 24)) * Prime;
        }

        internal void Add(float value)
        {
            Add(BitConverter.SingleToInt32Bits(value));
        }

        internal void Add(Color value)
        {
            Add(value.r);
            Add(value.g);
            Add(value.b);
            Add(value.a);
        }

        internal void Add(Vector2 value)
        {
            Add(value.x);
            Add(value.y);
        }

        internal void Add(Vector3 value)
        {
            Add(value.x);
            Add(value.y);
            Add(value.z);
        }

        internal void Add(Vector4 value)
        {
            Add(value.x);
            Add(value.y);
            Add(value.z);
            Add(value.w);
        }

        internal void Add(string value)
        {
            EnsureInitialized();
            string resolved = value ?? string.Empty;
            Add(resolved.Length);
            for (int i = 0; i < resolved.Length; i++)
            {
                char character = resolved[i];
                _value = (_value ^ (byte)character) * Prime;
                _value = (_value ^ (byte)(character >> 8)) * Prime;
            }
        }

        void EnsureInitialized()
        {
            if (_initialized)
                return;
            _value = Offset;
            _initialized = true;
        }
    }
}
