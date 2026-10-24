using System.Text;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Selects the source markup dialect accepted before Text Studio compiles canonical tags.
    /// </summary>
    public enum TextStudioAuthoringProfile
    {
        [InspectorName("Compatibility (All Providers)")]
        Compatibility = 0,
        [InspectorName("Native (Text Studio Only)")]
        Native = 1,
        [InspectorName("Text Animator")]
        TextAnimator = 2,
        [InspectorName("TMPEffects")]
        TMPEffects = 3,
        [InspectorName("Custom Delimiters")]
        Custom = 4
    }

    /// <summary>
    /// Configures project-wide or per-host authoring syntax without adding provider dependencies.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TextStudioMarkupSyntax",
        menuName = "Text Studio/Text Authoring/Markup Syntax",
        order = 103)]
    public sealed class TextStudioMarkupSyntax : ScriptableObject
    {
        [SerializeField] TextStudioAuthoringProfile _profile = TextStudioAuthoringProfile.Compatibility;
        [SerializeField] string _openingDelimiter = "[";
        [SerializeField] string _closingDelimiter = "]";
        [SerializeField] bool _acceptNativeAngleTags = true;

        [System.NonSerialized] int _revision;
        readonly StringBuilder _builder = new StringBuilder(256);

        public TextStudioAuthoringProfile Profile => _profile;
        public string OpeningDelimiter => ResolveOpeningDelimiter();
        public string ClosingDelimiter => ResolveClosingDelimiter();
        public bool AcceptNativeAngleTags => _profile != TextStudioAuthoringProfile.Custom || _acceptNativeAngleTags;
        internal bool AcceptTextAnimatorBraceTags =>
            _profile == TextStudioAuthoringProfile.Compatibility ||
            _profile == TextStudioAuthoringProfile.TextAnimator;
        internal bool AcceptTMPEffectsTags =>
            _profile == TextStudioAuthoringProfile.Compatibility ||
            _profile == TextStudioAuthoringProfile.TMPEffects;
        internal int Revision => _revision;
        // Normalize returns its input unchanged for every profile but Custom.
        internal bool NormalizesIdentically => _profile != TextStudioAuthoringProfile.Custom;

        internal static TextStudioAuthoringProfile ResolveProfile(TextStudioMarkupSyntax syntax)
        {
            return syntax != null ? syntax.Profile : TextStudioAuthoringProfile.Compatibility;
        }

        /// <summary>Converts configured custom delimiters to canonical angle tags while honoring escaping and native-tag policy.</summary>
        public string Normalize(string markup)
        {
            string source = markup ?? string.Empty;
            if (_profile != TextStudioAuthoringProfile.Custom || source.Length == 0)
                return source;

            char opening = ResolveOpeningDelimiter()[0];
            char closing = ResolveClosingDelimiter()[0];
            if (opening == '<' && closing == '>')
                return source;

            _builder.Clear();
            _builder.EnsureCapacity(source.Length);
            bool insideCustomTag = false;
            bool insideEscapedCustomTag = false;

            for (int i = 0; i < source.Length; i++)
            {
                char value = source[i];
                bool escaped = i > 0 && source[i - 1] == '\\' && (i < 2 || source[i - 2] != '\\');
                if (!insideCustomTag && !insideEscapedCustomTag && value == opening && IsPlausibleTagStart(source, i + 1, closing))
                {
                    _builder.Append('<');
                    insideCustomTag = !escaped;
                    insideEscapedCustomTag = escaped;
                }
                else if (insideCustomTag && value == closing)
                {
                    _builder.Append('>');
                    insideCustomTag = false;
                }
                else if (insideEscapedCustomTag && value == closing)
                {
                    _builder.Append('>');
                    insideEscapedCustomTag = false;
                }
                else if (!_acceptNativeAngleTags && (value == '<' || value == '>'))
                {
                    _builder.Append('\\');
                    _builder.Append(value);
                }
                else
                {
                    _builder.Append(value);
                }
            }

            return _builder.ToString();
        }

        /// <summary>Sets the authoring dialect and single-character custom delimiters, then invalidates dependent plans.</summary>
        public void Configure(
            TextStudioAuthoringProfile profile,
            string openingDelimiter = "[",
            string closingDelimiter = "]",
            bool acceptNativeAngleTags = true)
        {
            _profile = profile;
            _openingDelimiter = SanitizeDelimiter(openingDelimiter, '[');
            _closingDelimiter = SanitizeDelimiter(closingDelimiter, ']');
            _acceptNativeAngleTags = acceptNativeAngleTags;
            _revision++;
        }

        void OnValidate()
        {
            _openingDelimiter = SanitizeDelimiter(_openingDelimiter, '[');
            _closingDelimiter = SanitizeDelimiter(_closingDelimiter, ']');
            if (_openingDelimiter == _closingDelimiter)
                _closingDelimiter = _openingDelimiter == "]" ? ")" : "]";
            _revision++;
        }

        string ResolveOpeningDelimiter()
        {
            return SanitizeDelimiter(_openingDelimiter, '[');
        }

        string ResolveClosingDelimiter()
        {
            return SanitizeDelimiter(_closingDelimiter, ']');
        }

        static string SanitizeDelimiter(string value, char fallback)
        {
            return string.IsNullOrEmpty(value) ? fallback.ToString() : value[0].ToString();
        }

        static bool IsPlausibleTagStart(string source, int start, char closing)
        {
            if (start >= source.Length)
                return false;

            if (source[start] == '/')
            {
                start++;
                if (start < source.Length && source[start] == closing) return true;
            }
            if (start >= source.Length) return false;
            if (source[start] == '$') start++;
            else if (source[start] == '#')
            {
                int end = source.IndexOf(closing, start + 1);
                return end > start && BuiltIns.BuiltInStyles.IsNativeColorShorthand(source.Substring(start, end - start));
            }
            if (start >= source.Length || !(char.IsLetter(source[start]) || source[start] == '_')) return false;

            for (int i = start + 1; i < source.Length; i++)
            {
                if (source[i] == closing)
                    return true;
                if (source[i] == '\n' || source[i] == '\r')
                    return false;
            }
            return false;
        }
    }
}
