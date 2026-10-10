using System;
using System.Globalization;
using UnityEngine;

namespace TextStudio
{
    /// <summary>A half-open range of logical text units, resolved relative to one scope.</summary>
    [Serializable]
    public struct TextStudioTextRange : IEquatable<TextStudioTextRange>
    {
        [SerializeField] int _start;
        [SerializeField] int _end;
        [SerializeField] bool _startFromEnd;
        [SerializeField] bool _endFromEnd;
        [SerializeField] bool _hasStart;
        [SerializeField] bool _hasEnd;

        public bool HasStart => _hasStart;
        public bool HasEnd => _hasEnd;
        public int Start => _start;
        public int End => _end;
        public bool StartFromEnd => _startFromEnd;
        public bool EndFromEnd => _endFromEnd;
        public bool IsValid => _start >= 0 && _end >= 0 &&
                               (!_startFromEnd || _hasStart) && (!_endFromEnd || _hasEnd);
        public static TextStudioTextRange All => default;

        /// <summary>Creates a half-open range with explicit nonnegative endpoints that may be measured from the end of a scope.</summary>
        public TextStudioTextRange(int start, int end, bool startFromEnd = false, bool endFromEnd = false)
        {
            if (start < 0) throw new ArgumentOutOfRangeException(nameof(start));
            if (end < 0) throw new ArgumentOutOfRangeException(nameof(end));
            _start = start;
            _end = end;
            _startFromEnd = startFromEnd;
            _endFromEnd = endFromEnd;
            _hasStart = true;
            _hasEnd = true;
        }

        /// <summary>Parses a start..end range with optional endpoints and ^ offsets from the end, returning a diagnostic for invalid syntax.</summary>
        public static bool TryParse(string text, out TextStudioTextRange range, out string diagnostic)
        {
            range = default;
            diagnostic = string.Empty;
            if (text == null)
            {
                diagnostic = "A range is required. Use .. for the whole phrase.";
                return false;
            }
            string source = text.Trim();
            int separator = source.IndexOf("..", StringComparison.Ordinal);
            if (separator < 0 || source.IndexOf("..", separator + 2, StringComparison.Ordinal) >= 0)
            {
                diagnostic = "Use one .. between the start and exclusive end, for example 0..5.";
                return false;
            }
            string startText = source.Substring(0, separator).Trim();
            string endText = source.Substring(separator + 2).Trim();
            if (!TryEndpoint(startText, out range._start, out range._startFromEnd, out range._hasStart) ||
                !TryEndpoint(endText, out range._end, out range._endFromEnd, out range._hasEnd))
            {
                range = default;
                diagnostic = "Range endpoints must be non-negative whole numbers, optionally prefixed with ^.";
                return false;
            }
            return true;
        }

        /// <summary>Resolves endpoints against a nonnegative scope length, clamps them to the scope, and collapses reversed ranges to empty.</summary>
        public void Resolve(int scopeLength, out int start, out int end)
        {
            if (scopeLength < 0) throw new ArgumentOutOfRangeException(nameof(scopeLength));
            start = _hasStart ? ResolveEndpoint(_start, _startFromEnd, scopeLength) : 0;
            end = _hasEnd ? ResolveEndpoint(_end, _endFromEnd, scopeLength) : scopeLength;
            if (end < start) end = start;
        }

        public override string ToString()
        {
            return (_hasStart ? FormatEndpoint(_start, _startFromEnd) : string.Empty) + ".." +
                   (_hasEnd ? FormatEndpoint(_end, _endFromEnd) : string.Empty);
        }

        public bool Equals(TextStudioTextRange other)
        {
            return _start == other._start && _end == other._end &&
                   _startFromEnd == other._startFromEnd && _endFromEnd == other._endFromEnd &&
                   _hasStart == other._hasStart && _hasEnd == other._hasEnd;
        }

        public override bool Equals(object obj) => obj is TextStudioTextRange other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(_start, _end, _startFromEnd, _endFromEnd, _hasStart, _hasEnd);

        static bool TryEndpoint(string text, out int value, out bool fromEnd, out bool present)
        {
            value = 0;
            fromEnd = false;
            present = text.Length != 0;
            if (!present) return true;
            fromEnd = text[0] == '^';
            int first = fromEnd ? 1 : 0;
            if (first == text.Length) return false;
            for (int index = first; index < text.Length; index++)
                if (text[index] < '0' || text[index] > '9') return false;
            return int.TryParse(text.Substring(first), NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        static int ResolveEndpoint(int value, bool fromEnd, int length)
        {
            long resolved = fromEnd ? (long)length - value : value;
            return (int)Math.Max(0L, Math.Min(length, resolved));
        }

        static string FormatEndpoint(int value, bool fromEnd)
        {
            return (fromEnd ? "^" : string.Empty) + value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
