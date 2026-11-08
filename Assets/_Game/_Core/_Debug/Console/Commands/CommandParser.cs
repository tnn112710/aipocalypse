using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace MB13.Aipocalypse.Core.Debugging
{
    // Text rules of the console: statements, tokens, argument values and how results print.
    public static class CommandParser
    {
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        // Splits a line at each ';' outside quotes; blank statements are skipped.
        public static void SplitStatements(string line, List<string> statements)
        {
            statements.Clear();
            if (string.IsNullOrEmpty(line))
                return;
            int start = 0;
            bool quoted = false;
            for (int i = 0; i <= line.Length; i++)
            {
                if (i < line.Length)
                {
                    char c = line[i];
                    if (quoted && c == '\\')
                    {
                        i++;
                        continue;
                    }
                    if (c == '"')
                        quoted = !quoted;
                    if (c != ';' || quoted)
                        continue;
                }
                string statement = line.Substring(start, i - start).Trim();
                if (statement.Length > 0)
                    statements.Add(statement);
                start = i + 1;
            }
        }

        // Where the last statement of a line starts (after its last unquoted ';').
        public static int LastStatementStart(string line)
        {
            int start = 0;
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted && c == '\\')
                    i++;
                else if (c == '"')
                    quoted = !quoted;
                else if (c == ';' && !quoted)
                    start = i + 1;
            }
            return start;
        }

        // Words split by whitespace; "double quotes" keep spaces in one token, with \" and \\ inside them.
        public static void Tokenize(string statement, List<string> tokens)
        {
            tokens.Clear();
            if (string.IsNullOrEmpty(statement))
                return;
            var token = new StringBuilder();
            bool quoted = false;
            bool hasToken = false;
            for (int i = 0; i < statement.Length; i++)
            {
                char c = statement[i];
                if (quoted)
                {
                    if (c == '\\' && i + 1 < statement.Length)
                        token.Append(statement[++i]);
                    else if (c == '"')
                        quoted = false;
                    else
                        token.Append(c);
                }
                else if (c == '"')
                {
                    quoted = true;
                    hasToken = true;
                }
                else if (char.IsWhiteSpace(c))
                {
                    if (hasToken)
                        tokens.Add(token.ToString());
                    token.Clear();
                    hasToken = false;
                }
                else
                {
                    token.Append(c);
                    hasToken = true;
                }
            }
            if (hasToken)
                tokens.Add(token.ToString());
        }

        public static bool IsSupported(Type type) =>
            type == typeof(string) || type == typeof(bool) || type == typeof(int) || type == typeof(long) || type == typeof(float) ||
            type == typeof(double) || type.IsEnum || type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Color);

        public static bool TryParse(string text, Type type, out object value)
        {
            value = null;
            if (text == null)
                return false;
            if (type == typeof(string))
            {
                value = text;
                return true;
            }
            if (type == typeof(bool))
                return TryParseBool(text, out value);
            if (type == typeof(int))
                return Box(int.TryParse(text, NumberStyles.Integer, Invariant, out int i), i, out value);
            if (type == typeof(long))
                return Box(long.TryParse(text, NumberStyles.Integer, Invariant, out long l), l, out value);
            if (type == typeof(float))
                return Box(float.TryParse(text, NumberStyles.Float, Invariant, out float f), f, out value);
            if (type == typeof(double))
                return Box(double.TryParse(text, NumberStyles.Float, Invariant, out double d), d, out value);
            if (type.IsEnum)
                return TryParseEnum(text, type, out value);
            if (type == typeof(Vector2) || type == typeof(Vector3))
                return TryParseVector(text, type == typeof(Vector2) ? 2 : 3, out value);
            if (type == typeof(Color))
                return Box(ColorUtility.TryParseHtmlString(text, out Color color), color, out value);
            return false;
        }

        // Values a typed argument may complete to (enum names, on / off), or none.
        public static void Choices(Type type, List<string> choices)
        {
            choices.Clear();
            if (type == typeof(bool))
            {
                choices.Add("on");
                choices.Add("off");
            }
            else if (type.IsEnum)
            {
                choices.AddRange(Enum.GetNames(type));
            }
        }

        public static string Format(object value)
        {
            switch (value)
            {
                case null:
                    return "null";
                case string text:
                    return text;
                case bool flag:
                    return flag ? "on" : "off";
                case float f:
                    return f.ToString("0.###", Invariant);
                case double d:
                    return d.ToString("0.###", Invariant);
                case Vector2 v:
                    return string.Format(Invariant, "({0:0.##}, {1:0.##})", v.x, v.y);
                case Vector3 v:
                    return string.Format(Invariant, "({0:0.##}, {1:0.##}, {2:0.##})", v.x, v.y, v.z);
                case Color color:
                    return "#" + ColorUtility.ToHtmlStringRGBA(color);
                case IEnumerable items:
                    var lines = new StringBuilder();
                    foreach (object item in items)
                    {
                        if (lines.Length > 0)
                            lines.Append('\n');
                        lines.Append(Format(item));
                    }
                    return lines.ToString();
                default:
                    return value.ToString();
            }
        }

        public static string TypeName(Type type) =>
            type == typeof(string) ? "text" :
            type == typeof(bool) ? "on|off" :
            type == typeof(int) || type == typeof(long) ? "int" :
            type == typeof(float) || type == typeof(double) ? "number" :
            type == typeof(Vector2) ? "x,y" :
            type == typeof(Vector3) ? "x,y,z" :
            type == typeof(Color) ? "#rrggbb" :
            type.Name;

        static bool Box<T>(bool parsed, T parsedValue, out object value)
        {
            value = parsed ? parsedValue : null;
            return parsed;
        }

        static bool TryParseBool(string text, out object value)
        {
            switch (text.ToLowerInvariant())
            {
                case "1":
                case "on":
                case "true":
                case "yes":
                    value = true;
                    return true;
                case "0":
                case "off":
                case "false":
                case "no":
                    value = false;
                    return true;
                default:
                    value = null;
                    return false;
            }
        }

        // A name, ignoring case, or the start of exactly one name ("du" → Dusk).
        static bool TryParseEnum(string text, Type type, out object value)
        {
            value = null;
            string match = null;
            foreach (string name in Enum.GetNames(type))
            {
                if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase))
                {
                    match = name;
                    break;
                }
                if (!name.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (match != null)
                    return false;
                match = name;
            }
            if (match == null)
                return false;
            value = Enum.Parse(type, match);
            return true;
        }

        // "1,2,3" or "(1, 2, 3)".
        static bool TryParseVector(string text, int size, out object value)
        {
            value = null;
            string[] parts = text.Trim('(', ')', ' ').Split(',');
            if (parts.Length != size)
                return false;
            var v = new float[size];
            for (int i = 0; i < size; i++)
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, Invariant, out v[i]))
                    return false;
            value = size == 2 ? new Vector2(v[0], v[1]) : new Vector3(v[0], v[1], v[2]);
            return true;
        }
    }
}
