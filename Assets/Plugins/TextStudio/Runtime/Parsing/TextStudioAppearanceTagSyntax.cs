using System;

namespace TextStudio
{
    /// <summary>Shared appearance spelling rules for parsing, diagnostics, and authoring assistance.</summary>
    public static class TextStudioAppearanceTagSyntax
    {
        /// <summary>Checks for an identifier beginning with a letter or underscore and continuing with letters, digits, underscores, hyphens, periods, or colons.</summary>
        public static bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id) || !(char.IsLetter(id[0]) || id[0] == '_')) return false;
            for (int i = 1; i < id.Length; i++)
            {
                char c = id[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != '.' && c != ':') return false;
            }
            return true;
        }

        /// <summary>Checks case-insensitively whether a parameter name configures an ad-hoc fill, outline, shadow, or glow.</summary>
        public static bool IsAdHocParameter(string name)
        {
            switch (name?.ToLowerInvariant())
            {
                case "fill": case "outline": case "outlinewidth": case "shadow": case "shadowx":
                case "shadowy": case "shadowblur": case "glow": case "glowradius": return true;
                default: return false;
            }
        }

        /// <summary>Checks whether a tag name is already registered as an effect, action, extension tag, or tag expansion.</summary>
        public static bool HasLegacyNameCollision(string tagName)
        {
            return !string.IsNullOrEmpty(tagName) &&
                   (TextStudioRegistry.Effects.TryGetQuiet(tagName, out _) ||
                    TextStudioRegistry.Actions.TryGetQuiet(tagName, out _) ||
                    TextStudioRegistry.ExtensionTags.TryGet(tagName, out _) ||
                    TextStudioRegistry.TagExpansions.TryGet(tagName, out _));
        }

        /// <summary>Returns null when both supported spellings are occupied by existing registrations.</summary>
        public static string GetPreferredOpeningTag(string id)
        {
            if (!IsValidId(id)) return null;
            if (!HasLegacyNameCollision("$" + id)) return "<$" + id + ">";
            return !HasLegacyNameCollision("style") ? "<style ref=\"" + id + "\">" : null;
        }

        /// <summary>Recognizes shorthand or style appearance tags while rejecting legacy collisions and direct assignments, retaining malformed style references for diagnostics.</summary>
        public static bool TryClassify(string tagName, bool usesDirectAssignment,
            in TextStudioTagParameters parameters, out string id, out bool isAdHoc)
        {
            id = string.Empty;
            isAdHoc = false;
            if (string.IsNullOrEmpty(tagName) || usesDirectAssignment || HasLegacyNameCollision(tagName)) return false;
            if (tagName[0] == '$')
            {
                id = tagName.Substring(1);
                return IsValidId(id);
            }
            if (!string.Equals(tagName, "style", StringComparison.OrdinalIgnoreCase)) return false;
            if (parameters.TryGetNamedParameter("ref", out TextStudioNamedTagParameter reference))
            {
                id = reference.Value ?? string.Empty;
                // Keep malformed references as appearance occurrences so the resolver can explain them.
                return true;
            }
            for (int i = 0; i < parameters.NamedCount; i++)
            {
                if (!IsAdHocParameter(parameters.Named[i].Name)) continue;
                isAdHoc = true;
                return true;
            }
            return false;
        }

        internal static bool UsesDirectAssignment(string source, in TextToken token)
        {
            int cursor = token.nameEnd;
            while (cursor < token.sourceEnd && char.IsWhiteSpace(source[cursor])) cursor++;
            return cursor < token.sourceEnd && source[cursor] == '=';
        }

        internal static TextStudioTagParameters GetParameters(string source, in TextToken token)
        {
            return token.parameterStart > 0 && token.parameterEnd > token.parameterStart
                ? TextStudioTagParameterParser.Parse(source.Substring(token.parameterStart, token.parameterEnd - token.parameterStart))
                : TextStudioTagParameters.Empty;
        }
    }
}
