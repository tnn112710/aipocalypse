using System;
using System.Collections.Generic;

namespace TextStudio.Editor
{
    internal readonly struct TextAnimatorMarkupToken
    {
        public readonly string Name;
        public readonly string RawParameter;
        public readonly string RawText;
        public readonly int Start;
        public readonly int End;
        public readonly bool IsClosing;
        public readonly bool IsBrace;
        public readonly bool IsDisappearanceBrace;

        public TextAnimatorMarkupToken(
            string name,
            string rawParameter,
            string rawText,
            int start,
            int end,
            bool isClosing,
            bool isBrace,
            bool isDisappearanceBrace)
        {
            Name = name ?? string.Empty;
            RawParameter = rawParameter ?? string.Empty;
            RawText = rawText ?? string.Empty;
            Start = Math.Max(0, start);
            End = Math.Max(Start, end);
            IsClosing = isClosing;
            IsBrace = isBrace;
            IsDisappearanceBrace = isDisappearanceBrace;
        }
    }

    internal static class TextAnimatorMarkupScanner
    {
        public static IReadOnlyList<TextAnimatorMarkupToken> Scan(string source)
        {
            List<TextAnimatorMarkupToken> tokens = new List<TextAnimatorMarkupToken>(16);
            if (string.IsNullOrEmpty(source))
                return tokens;

            int index = 0;
            while (index < source.Length)
            {
                char c = source[index];
                if (c == '<')
                {
                    int end = source.IndexOf('>', index + 1);
                    if (end > index && TryParseAngle(source, index, end, out TextAnimatorMarkupToken token))
                    {
                        tokens.Add(token);
                        index = end + 1;
                        continue;
                    }
                }
                else if (c == '{')
                {
                    int end = source.IndexOf('}', index + 1);
                    if (end > index && TryParseBrace(source, index, end, out TextAnimatorMarkupToken token))
                    {
                        tokens.Add(token);
                        index = end + 1;
                        continue;
                    }
                }

                index++;
            }

            return tokens;
        }

        static bool TryParseAngle(string source, int start, int close, out TextAnimatorMarkupToken token)
        {
            token = default;
            int cursor = start + 1;
            int innerEnd = close;
            while (cursor < innerEnd && char.IsWhiteSpace(source[cursor]))
                cursor++;
            while (innerEnd > cursor && char.IsWhiteSpace(source[innerEnd - 1]))
                innerEnd--;
            if (cursor >= innerEnd)
                return false;

            bool isClosing = source[cursor] == '/';
            if (isClosing)
            {
                cursor++;
                while (cursor < innerEnd && char.IsWhiteSpace(source[cursor]))
                    cursor++;
            }

            int nameStart = cursor;
            while (cursor < innerEnd &&
                   !char.IsWhiteSpace(source[cursor]) &&
                   source[cursor] != '=' &&
                   source[cursor] != '/')
            {
                cursor++;
            }

            if (cursor <= nameStart)
                return false;

            string name = source.Substring(nameStart, cursor - nameStart);
            if (!IsLikelyTagName(name))
                return false;

            string rawParameter = string.Empty;
            while (cursor < innerEnd && char.IsWhiteSpace(source[cursor]))
                cursor++;
            if (cursor < innerEnd && source[cursor] == '=')
                cursor++;
            while (cursor < innerEnd && char.IsWhiteSpace(source[cursor]))
                cursor++;
            if (cursor < innerEnd)
                rawParameter = source.Substring(cursor, innerEnd - cursor).Trim().TrimEnd('/');

            token = new TextAnimatorMarkupToken(
                name,
                rawParameter,
                source.Substring(start, close + 1 - start),
                start,
                close + 1,
                isClosing,
                false,
                false);
            return true;
        }

        static bool TryParseBrace(string source, int start, int close, out TextAnimatorMarkupToken token)
        {
            token = default;
            if ((start > 0 && source[start - 1] == '{') ||
                (start + 1 < source.Length && source[start + 1] == '{') ||
                (close + 1 < source.Length && source[close + 1] == '}') ||
                ContainsLineBreak(source, start, close))
            {
                return false;
            }

            int cursor = start + 1;
            int innerEnd = close;
            while (cursor < innerEnd && char.IsWhiteSpace(source[cursor]))
                cursor++;
            while (innerEnd > cursor && char.IsWhiteSpace(source[innerEnd - 1]))
                innerEnd--;
            if (cursor >= innerEnd)
                return false;

            bool isClosing = source[cursor] == '/';
            if (isClosing)
            {
                cursor++;
                while (cursor < innerEnd && char.IsWhiteSpace(source[cursor]))
                    cursor++;
            }

            bool isDisappearance = cursor < innerEnd && source[cursor] == '#';
            if (isDisappearance)
            {
                cursor++;
                while (cursor < innerEnd && char.IsWhiteSpace(source[cursor]))
                    cursor++;
            }

            int nameStart = cursor;
            while (cursor < innerEnd &&
                   !char.IsWhiteSpace(source[cursor]) &&
                   source[cursor] != '/' &&
                   source[cursor] != '#')
            {
                cursor++;
            }

            if (cursor <= nameStart)
                return false;

            string name = source.Substring(nameStart, cursor - nameStart);
            if (!IsLikelyTagName(name))
                return false;

            token = new TextAnimatorMarkupToken(
                name,
                string.Empty,
                source.Substring(start, close + 1 - start),
                start,
                close + 1,
                isClosing,
                true,
                isDisappearance);
            return true;
        }

        static bool IsLikelyTagName(string name)
        {
            if (string.IsNullOrEmpty(name) || !char.IsLetter(name[0]))
                return false;

            for (int i = 1; i < name.Length; i++)
            {
                char c = name[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                    return false;
            }

            return true;
        }

        static bool ContainsLineBreak(string source, int start, int close)
        {
            for (int i = start + 1; i < close; i++)
            {
                char c = source[i];
                if (c == '\r' || c == '\n')
                    return true;
            }

            return false;
        }
    }
}
