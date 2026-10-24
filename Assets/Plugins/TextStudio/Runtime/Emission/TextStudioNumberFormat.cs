using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Shared numeric presentation used by persistent content and temporary emissions.
    /// Formatting occurs only when content changes.
    /// </summary>
    [Serializable]
    public struct TextStudioNumberFormat
    {
        const int PrecisionOptionCount = 13;
        const int PatternsPerGroupingMode =
            PrecisionOptionCount * PrecisionOptionCount;
        // Integers below this magnitude keep every digit under the double custom format's 15-digit precision.
        const double ExactIntegerLimit = 1e15;
        static readonly string[] s_patterns =
            new string[PatternsPerGroupingMode * 2];

        [SerializeField, HideInInspector] bool _initialized;
        [SerializeField, Tooltip("Insert culture-aware digit grouping separators.")]
        bool _useGrouping;
        [SerializeField, Min(0), Tooltip("Minimum number of digits shown after the decimal separator.")]
        int _minimumFractionDigits;
        [SerializeField, Min(0), Tooltip("Maximum number of digits shown after the decimal separator.")]
        int _maximumFractionDigits;
        [SerializeField, Tooltip("Prefix positive values with a plus sign.")]
        bool _showPositiveSign;
        [SerializeField, Tooltip("Abbreviate thousands and larger values with K, M, B, and T suffixes.")]
        bool _useCompactNotation;
        [SerializeField, Tooltip("Optional suffix appended after the formatted value.")]
        string _suffix;
        [SerializeField, Tooltip("Optional culture name such as en-US. Empty uses invariant formatting.")]
        string _cultureName;

        public static TextStudioNumberFormat Default
        {
            get
            {
                return new TextStudioNumberFormat
                {
                    _initialized = true,
                    _useGrouping = true,
                    _minimumFractionDigits = 0,
                    _maximumFractionDigits = 0,
                    _showPositiveSign = false,
                    _useCompactNotation = false,
                    _suffix = string.Empty,
                    _cultureName = string.Empty
                };
            }
        }

        public bool UseGrouping
        {
            get => Resolve()._useGrouping;
            set
            {
                EnsureInitialized();
                _useGrouping = value;
            }
        }

        public int MinimumFractionDigits
        {
            get => Resolve()._minimumFractionDigits;
            set
            {
                EnsureInitialized();
                _minimumFractionDigits = Mathf.Clamp(value, 0, 12);
                _maximumFractionDigits = Mathf.Max(_minimumFractionDigits, _maximumFractionDigits);
            }
        }

        public int MaximumFractionDigits
        {
            get => Resolve()._maximumFractionDigits;
            set
            {
                EnsureInitialized();
                _maximumFractionDigits = Mathf.Clamp(value, 0, 12);
                _minimumFractionDigits = Mathf.Min(_minimumFractionDigits, _maximumFractionDigits);
            }
        }

        public bool ShowPositiveSign
        {
            get => Resolve()._showPositiveSign;
            set
            {
                EnsureInitialized();
                _showPositiveSign = value;
            }
        }

        public bool UseCompactNotation
        {
            get => Resolve()._useCompactNotation;
            set
            {
                EnsureInitialized();
                _useCompactNotation = value;
            }
        }

        public string Suffix
        {
            get => Resolve()._suffix ?? string.Empty;
            set
            {
                EnsureInitialized();
                _suffix = value ?? string.Empty;
            }
        }

        public string CultureName
        {
            get => Resolve()._cultureName ?? string.Empty;
            set
            {
                EnsureInitialized();
                _cultureName = value ?? string.Empty;
            }
        }

        /// <summary>Formats a number with configured grouping, precision, sign, compact notation, suffix, and culture.</summary>
        public string Format(double value)
        {
            return Format(value, null);
        }

        internal string Format(double value, string existingMarkup)
        {
            return Format(value, existingMarkup, default, default);
        }

        internal string Format(double value, string existingMarkup,
            ReadOnlySpan<char> markupPrefix, ReadOnlySpan<char> markupSuffix)
        {
            TextStudioNumberFormat settings = Resolve();
            double displayValue = value;
            string compactSuffix = string.Empty;
            if (settings._useCompactNotation)
                ScaleCompactValue(ref displayValue, out compactSuffix);

            string sign = settings._showPositiveSign && displayValue > 0d ? "+" : string.Empty;
            string configuredSuffix = settings._suffix ?? string.Empty;
            if (CanWriteInvariantInteger(in settings, displayValue))
            {
                return WriteInvariantInteger(
                    displayValue,
                    settings._useGrouping,
                    sign,
                    compactSuffix,
                    configuredSuffix,
                    existingMarkup,
                    markupPrefix,
                    markupSuffix);
            }

            CultureInfo culture = ResolveCulture(settings._cultureName);
            string pattern = GetPattern(
                settings._useGrouping,
                settings._minimumFractionDigits,
                settings._maximumFractionDigits);
            if (sign.Length == 0 &&
                compactSuffix.Length == 0 &&
                configuredSuffix.Length == 0 &&
                existingMarkup == null && markupPrefix.IsEmpty && markupSuffix.IsEmpty)
            {
                return displayValue.ToString(pattern, culture);
            }
            return WriteFormattedNumber(displayValue, pattern, culture,
                sign, compactSuffix, configuredSuffix, existingMarkup, markupPrefix, markupSuffix);
        }

        /// <summary>Returns a copy with safe precision, suffix, and culture defaults.</summary>
        public TextStudioNumberFormat Normalized()
        {
            TextStudioNumberFormat settings = Resolve();
            settings._minimumFractionDigits = Mathf.Clamp(settings._minimumFractionDigits, 0, 12);
            settings._maximumFractionDigits = Mathf.Clamp(settings._maximumFractionDigits, 0, 12);
            if (settings._minimumFractionDigits > settings._maximumFractionDigits)
                settings._minimumFractionDigits = settings._maximumFractionDigits;
            settings._suffix ??= string.Empty;
            settings._cultureName ??= string.Empty;
            return settings;
        }

        void EnsureInitialized()
        {
            if (_initialized)
                return;
            this = Default;
        }

        TextStudioNumberFormat Resolve()
        {
            return _initialized ? this : Default;
        }

        static CultureInfo ResolveCulture(string cultureName)
        {
            if (string.IsNullOrWhiteSpace(cultureName))
                return CultureInfo.InvariantCulture;

            try
            {
                return CultureInfo.GetCultureInfo(cultureName);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.InvariantCulture;
            }
        }

        static string GetPattern(
            bool useGrouping,
            int minimumFractionDigits,
            int maximumFractionDigits)
        {
            int safeMaximum = Mathf.Clamp(
                maximumFractionDigits,
                0,
                PrecisionOptionCount - 1);
            int safeMinimum = Mathf.Clamp(
                minimumFractionDigits,
                0,
                safeMaximum);
            int index = (useGrouping ? PatternsPerGroupingMode : 0) +
                        safeMinimum * PrecisionOptionCount +
                        safeMaximum;
            string pattern = s_patterns[index];
            if (pattern != null)
                return pattern;

            pattern = BuildPattern(useGrouping, safeMinimum, safeMaximum);
            s_patterns[index] = pattern;
            return pattern;
        }

        static string BuildPattern(
            bool useGrouping,
            int minimumFractionDigits,
            int maximumFractionDigits)
        {
            StringBuilder builder = new StringBuilder(20);
            builder.Append(useGrouping ? "#,0" : "0");
            if (maximumFractionDigits <= 0)
                return builder.ToString();

            builder.Append('.');
            for (int i = 0; i < maximumFractionDigits; i++)
                builder.Append(i < minimumFractionDigits ? '0' : '#');
            return builder.ToString();
        }

        // Invariant whole numbers without required fraction digits print as plain grouped digits under every pattern,
        // which avoids parsing the custom format for each value. Other cultures and fractions keep the general path.
        static bool CanWriteInvariantInteger(
            in TextStudioNumberFormat settings,
            double displayValue)
        {
            if (!string.IsNullOrWhiteSpace(settings._cultureName))
                return false;
            int safeMaximum = Mathf.Clamp(
                settings._maximumFractionDigits,
                0,
                PrecisionOptionCount - 1);
            int safeMinimum = Mathf.Clamp(
                settings._minimumFractionDigits,
                0,
                safeMaximum);
            // Zero is excluded because runtimes disagree on printing negative zero.
            return safeMinimum == 0 &&
                   displayValue != 0d &&
                   displayValue > -ExactIntegerLimit &&
                   displayValue < ExactIntegerLimit &&
                   displayValue == Math.Floor(displayValue);
        }

        static string WriteInvariantInteger(
            double displayValue,
            bool useGrouping,
            string sign,
            string compactSuffix,
            string configuredSuffix,
            string existingMarkup,
            ReadOnlySpan<char> markupPrefix,
            ReadOnlySpan<char> markupSuffix)
        {
            bool negative = displayValue < 0d;
            long magnitude = (long)(negative ? -displayValue : displayValue);
            int digitCount = 1;
            for (long remaining = magnitude / 10L; remaining > 0L; remaining /= 10L)
                digitCount++;
            int separatorCount = useGrouping ? (digitCount - 1) / 3 : 0;
            int numberEnd = markupPrefix.Length + sign.Length + (negative ? 1 : 0) + digitCount + separatorCount;
            int suffixStart = numberEnd + compactSuffix.Length + configuredSuffix.Length;
            int length = suffixStart + markupSuffix.Length;
            char[] characters = TextStudioNumberFormatBuffer.Get(length);
            markupPrefix.CopyTo(characters.AsSpan());
            sign.CopyTo(0, characters, markupPrefix.Length, sign.Length);
            markupSuffix.CopyTo(characters.AsSpan(suffixStart));
            compactSuffix.CopyTo(0, characters, numberEnd, compactSuffix.Length);
            configuredSuffix.CopyTo(
                0,
                characters,
                numberEnd + compactSuffix.Length,
                configuredSuffix.Length);

            int position = numberEnd;
            int groupDigits = 0;
            do
            {
                if (useGrouping && groupDigits == 3)
                {
                    characters[--position] = ',';
                    groupDigits = 0;
                }
                characters[--position] = (char)('0' + (int)(magnitude % 10L));
                magnitude /= 10L;
                groupDigits++;
            }
            while (magnitude > 0L);
            if (negative)
                characters[--position] = '-';
            return CreateOrReuseText(characters, length, existingMarkup);
        }

        static string WriteFormattedNumber(double displayValue, string pattern, CultureInfo culture,
            string sign, string compactSuffix, string configuredSuffix, string existingMarkup,
            ReadOnlySpan<char> markupPrefix, ReadOnlySpan<char> markupSuffix)
        {
            int numberStart = markupPrefix.Length + sign.Length;
            int decorationLength = numberStart + compactSuffix.Length + configuredSuffix.Length + markupSuffix.Length;
            char[] characters = TextStudioNumberFormatBuffer.Get(decorationLength + 32);
            int numberLength;
            while (!displayValue.TryFormat(
                       characters.AsSpan(numberStart, characters.Length - decorationLength),
                       out numberLength, pattern.AsSpan(), culture))
            {
                characters = TextStudioNumberFormatBuffer.Get(characters.Length * 2);
            }

            int numberEnd = numberStart + numberLength;
            int suffixStart = numberEnd + compactSuffix.Length + configuredSuffix.Length;
            int length = suffixStart + markupSuffix.Length;
            markupPrefix.CopyTo(characters.AsSpan());
            sign.CopyTo(0, characters, markupPrefix.Length, sign.Length);
            markupSuffix.CopyTo(characters.AsSpan(suffixStart));
            compactSuffix.CopyTo(0, characters, numberEnd, compactSuffix.Length);
            configuredSuffix.CopyTo(0, characters,
                numberEnd + compactSuffix.Length, configuredSuffix.Length);
            return CreateOrReuseText(characters, length, existingMarkup);
        }

        static string CreateOrReuseText(char[] characters, int length, string existingMarkup)
        {
            if (existingMarkup != null && existingMarkup.Length == length)
            {
                int index = 0;
                while (index < length && existingMarkup[index] == characters[index])
                    index++;
                if (index == length)
                    return existingMarkup;
            }
            return new string(characters, 0, length);
        }

        static void ScaleCompactValue(ref double value, out string suffix)
        {
            double magnitude = Math.Abs(value);
            if (magnitude >= 1_000_000_000_000d)
            {
                value /= 1_000_000_000_000d;
                suffix = "T";
            }
            else if (magnitude >= 1_000_000_000d)
            {
                value /= 1_000_000_000d;
                suffix = "B";
            }
            else if (magnitude >= 1_000_000d)
            {
                value /= 1_000_000d;
                suffix = "M";
            }
            else if (magnitude >= 1_000d)
            {
                value /= 1_000d;
                suffix = "K";
            }
            else
            {
                suffix = string.Empty;
            }
        }
    }

#if UNITY_6000_7_OR_NEWER
    // Scratch only: every format writes each character it returns before reading it.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioNumberFormatBuffer
    {
        [ThreadStatic] static char[] s_characters;

        internal static char[] Get(int length)
        {
            char[] characters = s_characters;
            if (characters == null || characters.Length < length)
            {
                characters = new char[Mathf.NextPowerOfTwo(Mathf.Max(32, length))];
                s_characters = characters;
            }
            return characters;
        }
    }
}
