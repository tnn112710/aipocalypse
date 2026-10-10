using System;
using System.Collections.Generic;
using System.Text;

namespace TextStudio
{
    internal static class TextStudioTagExpansionProcessor
    {
        const int MaxExpansionDepth = 16;

        public static string Expand(string source, TextStudioTagExpansionRegistry registry)
        {
            source ??= string.Empty;
            if (source.Length == 0 || source.IndexOf('<') < 0 || registry == null || registry.Count == 0)
                return source;

            StringBuilder builder = null;
            List<string> expansionStack = null;
            bool changed = ExpandRange(source, registry, ref builder, ref expansionStack, 0);
            return changed ? builder.ToString() : source;
        }

        static bool ExpandRange(
            string source,
            TextStudioTagExpansionRegistry registry,
            ref StringBuilder builder,
            ref List<string> expansionStack,
            int depth)
        {
            bool changed = false;
            int cursor = 0;
            while (cursor < source.Length)
            {
                int tagStart = source.IndexOf('<', cursor);
                if (tagStart < 0)
                {
                    builder?.Append(source, cursor, source.Length - cursor);
                    break;
                }

                if (TextStudioTagExpansionUtility.IsEscaped(source, tagStart))
                {
                    builder?.Append(source, cursor, tagStart + 1 - cursor);
                    cursor = tagStart + 1;
                    continue;
                }

                int tagEnd = source.IndexOf('>', tagStart + 1);
                if (tagEnd < 0)
                {
                    builder?.Append(source, cursor, source.Length - cursor);
                    break;
                }

                if (!TextStudioTagExpansionUtility.TryParseTag(
                        source,
                        tagStart,
                        tagEnd,
                        out string name,
                        out bool isClosing,
                        out bool selfClosing,
                        out _))
                {
                    builder?.Append(source, cursor, tagEnd + 1 - cursor);
                    cursor = tagEnd + 1;
                    continue;
                }

                if ((!isClosing && string.Equals(name, "noparse", StringComparison.OrdinalIgnoreCase)) ||
                    TextStudioTagExpansionUtility.IsCommentStart(source, tagStart))
                {
                    bool comment = source[tagStart + 1] == '!';
                    string terminator = comment ? "-->" : "</noparse>";
                    int protectedEnd = source.IndexOf(terminator, comment ? tagStart + 4 : tagEnd + 1, StringComparison.OrdinalIgnoreCase);
                    int next = protectedEnd >= 0 ? protectedEnd + terminator.Length : source.Length;
                    builder?.Append(source, cursor, next - cursor);
                    cursor = next;
                    continue;
                }

                if (selfClosing ||
                    !registry.TryGet(name, out TextStudioTagExpansionEntry expansion) ||
                    Contains(expansionStack, expansion.TagId) ||
                    depth >= MaxExpansionDepth)
                {
                    builder?.Append(source, cursor, tagEnd + 1 - cursor);
                    cursor = tagEnd + 1;
                    continue;
                }

                if (builder == null)
                {
                    // Most native rich text does not use aliases. Preserve its original string
                    // without allocating output storage until the first actual expansion.
                    builder = new StringBuilder(source.Length + 32);
                    builder.Append(source, 0, tagStart);
                }
                else
                    builder.Append(source, cursor, tagStart - cursor);
                string markup = isClosing ? expansion.ClosingMarkup : expansion.OpeningMarkup;
                if (!string.IsNullOrEmpty(markup))
                {
                    expansionStack ??= new List<string>(4);
                    expansionStack.Add(expansion.TagId);
                    ExpandRange(markup, registry, ref builder, ref expansionStack, depth + 1);
                    expansionStack.RemoveAt(expansionStack.Count - 1);
                }

                cursor = tagEnd + 1;
                changed = true;
            }

            return changed;
        }

        static bool Contains(List<string> values, string value)
        {
            if (values == null) return false;
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
