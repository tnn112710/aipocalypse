using System;

namespace TextStudio
{
    /// <summary>Pure routing and eligibility rules shared by runtime audio playback and validation.</summary>
    public static class TextStudioAudioPlayerRules
    {
        /// <summary>Reports whether a visible character is eligible under whitespace and punctuation filtering.</summary>
        public static bool ShouldPlayForCharacter(char character, bool ignoreWhitespaceAndPunctuation, string ignoredPunctuation)
        {
            if (!ignoreWhitespaceAndPunctuation)
                return true;

            if (char.IsWhiteSpace(character))
                return false;

            string punctuation = ignoredPunctuation ?? string.Empty;
            return punctuation.IndexOf(character) < 0;
        }

        /// <summary>Resolves the canonical marker route from an action ID and positional parameters.</summary>
        public static string GetMarkerRouteId(string markerId, string[] parameters)
        {
            if (string.Equals(markerId, TextStudioBuiltInActionIds.Event, StringComparison.OrdinalIgnoreCase) &&
                parameters != null &&
                parameters.Length > 0 &&
                !string.IsNullOrEmpty(parameters[0]))
                return parameters[0];

            return markerId ?? string.Empty;
        }

        /// <summary>Compares a configured route with an authored marker using ordinal case-insensitive matching.</summary>
        public static bool MarkerRouteMatches(string expectedRouteId, string markerId, string[] parameters)
        {
            return !string.IsNullOrEmpty(expectedRouteId) &&
                   string.Equals(expectedRouteId, GetMarkerRouteId(markerId, parameters), StringComparison.OrdinalIgnoreCase);
        }
    }
}
