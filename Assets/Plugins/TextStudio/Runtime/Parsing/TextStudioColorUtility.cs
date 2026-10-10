using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Parses Text Studio color syntax and formats normalized hexadecimal color values.</summary>
#if UNITY_6000_7_OR_NEWER
    // Named colors are immutable parsing metadata for the loaded code lifetime.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static partial class TextStudioColorUtility
    {
        static readonly Dictionary<string, Color32> s_namedColors = CreateNamedColors();

        /// <summary>Parses quoted or unquoted named colors and 3, 4, 6, or 8 digit HTML hexadecimal colors.</summary>
        public static bool TryParseColor(string raw, out Color32 value)
        {
            value = default;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string trimmed = Unquote(raw.Trim());
            if (trimmed.Length == 0)
                return false;

            if (TryParseNamedColor(trimmed, out value))
                return true;

            if (ColorUtility.TryParseHtmlString(trimmed, out Color color))
            {
                value = color;
                return true;
            }

            if (trimmed[0] != '#' &&
                IsHexCandidate(trimmed) &&
                ColorUtility.TryParseHtmlString("#" + trimmed, out color))
            {
                value = color;
                return true;
            }

            return false;
        }

        /// <summary>Parses a CSS-style named color while ignoring spaces, hyphens, underscores, and casing.</summary>
        public static bool TryParseNamedColor(string raw, out Color32 value)
        {
            value = default;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string key = NormalizeName(Unquote(raw.Trim()));
            return key.Length > 0 && s_namedColors.TryGetValue(key, out value);
        }

        /// <summary>Formats uppercase RGB hexadecimal text, adding alpha only when it is not fully opaque.</summary>
        public static string ToHex(Color32 color)
        {
            string rgb =
                "#" +
                color.r.ToString("X2", CultureInfo.InvariantCulture) +
                color.g.ToString("X2", CultureInfo.InvariantCulture) +
                color.b.ToString("X2", CultureInfo.InvariantCulture);
            if (color.a == 255)
                return rgb;

            return rgb + color.a.ToString("X2", CultureInfo.InvariantCulture);
        }

        internal static bool TryNormalizeToHex(string raw, out string hex)
        {
            hex = string.Empty;
            if (!TryParseColor(raw, out Color32 color))
                return false;

            hex = ToHex(color);
            return true;
        }

        static bool IsHexCandidate(string value)
        {
            if (value.Length != 3 &&
                value.Length != 4 &&
                value.Length != 6 &&
                value.Length != 8)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool hex =
                    (c >= '0' && c <= '9') ||
                    (c >= 'a' && c <= 'f') ||
                    (c >= 'A' && c <= 'F');
                if (!hex)
                    return false;
            }

            return true;
        }

        static string NormalizeName(string raw)
        {
            StringBuilder builder = new StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (char.IsWhiteSpace(c) || c == '-' || c == '_')
                    continue;

                builder.Append(char.ToLowerInvariant(c));
            }

            return builder.ToString();
        }

        static string Unquote(string value)
        {
            if (value.Length >= 2)
            {
                char first = value[0];
                char last = value[value.Length - 1];
                if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
                    return value.Substring(1, value.Length - 2);
            }

            return value;
        }

        static Dictionary<string, Color32> CreateNamedColors()
        {
            // Markup parsing hashes these keys: StringComparer.OrdinalIgnoreCase allocates an upper-case copy per lookup on Mono.
            Dictionary<string, Color32> colors = new Dictionary<string, Color32>(TextStudioIgnoreCaseComparer.Instance);
            Add(colors, "aliceblue", 0xF0, 0xF8, 0xFF);
            Add(colors, "antiquewhite", 0xFA, 0xEB, 0xD7);
            Add(colors, "aqua", 0x00, 0xFF, 0xFF);
            Add(colors, "aquamarine", 0x7F, 0xFF, 0xD4);
            Add(colors, "azure", 0xF0, 0xFF, 0xFF);
            Add(colors, "beige", 0xF5, 0xF5, 0xDC);
            Add(colors, "bisque", 0xFF, 0xE4, 0xC4);
            Add(colors, "black", 0x00, 0x00, 0x00);
            Add(colors, "blanchedalmond", 0xFF, 0xEB, 0xCD);
            Add(colors, "blue", 0x00, 0x00, 0xFF);
            Add(colors, "blueviolet", 0x8A, 0x2B, 0xE2);
            Add(colors, "brown", 0xA5, 0x2A, 0x2A);
            Add(colors, "burlywood", 0xDE, 0xB8, 0x87);
            Add(colors, "cadetblue", 0x5F, 0x9E, 0xA0);
            Add(colors, "chartreuse", 0x7F, 0xFF, 0x00);
            Add(colors, "chocolate", 0xD2, 0x69, 0x1E);
            Add(colors, "coral", 0xFF, 0x7F, 0x50);
            Add(colors, "cornflowerblue", 0x64, 0x95, 0xED);
            Add(colors, "cornsilk", 0xFF, 0xF8, 0xDC);
            Add(colors, "crimson", 0xDC, 0x14, 0x3C);
            Add(colors, "cyan", 0x00, 0xFF, 0xFF);
            Add(colors, "darkblue", 0x00, 0x00, 0x8B);
            Add(colors, "darkcyan", 0x00, 0x8B, 0x8B);
            Add(colors, "darkgoldenrod", 0xB8, 0x86, 0x0B);
            Add(colors, "darkgray", 0xA9, 0xA9, 0xA9);
            Add(colors, "darkgreen", 0x00, 0x64, 0x00);
            Add(colors, "darkgrey", 0xA9, 0xA9, 0xA9);
            Add(colors, "darkkhaki", 0xBD, 0xB7, 0x6B);
            Add(colors, "darkmagenta", 0x8B, 0x00, 0x8B);
            Add(colors, "darkolivegreen", 0x55, 0x6B, 0x2F);
            Add(colors, "darkorange", 0xFF, 0x8C, 0x00);
            Add(colors, "darkorchid", 0x99, 0x32, 0xCC);
            Add(colors, "darkred", 0x8B, 0x00, 0x00);
            Add(colors, "darksalmon", 0xE9, 0x96, 0x7A);
            Add(colors, "darkseagreen", 0x8F, 0xBC, 0x8F);
            Add(colors, "darkslateblue", 0x48, 0x3D, 0x8B);
            Add(colors, "darkslategray", 0x2F, 0x4F, 0x4F);
            Add(colors, "darkslategrey", 0x2F, 0x4F, 0x4F);
            Add(colors, "darkturquoise", 0x00, 0xCE, 0xD1);
            Add(colors, "darkviolet", 0x94, 0x00, 0xD3);
            Add(colors, "deeppink", 0xFF, 0x14, 0x93);
            Add(colors, "deepskyblue", 0x00, 0xBF, 0xFF);
            Add(colors, "dimgray", 0x69, 0x69, 0x69);
            Add(colors, "dimgrey", 0x69, 0x69, 0x69);
            Add(colors, "dodgerblue", 0x1E, 0x90, 0xFF);
            Add(colors, "firebrick", 0xB2, 0x22, 0x22);
            Add(colors, "floralwhite", 0xFF, 0xFA, 0xF0);
            Add(colors, "forestgreen", 0x22, 0x8B, 0x22);
            Add(colors, "fuchsia", 0xFF, 0x00, 0xFF);
            Add(colors, "gainsboro", 0xDC, 0xDC, 0xDC);
            Add(colors, "ghostwhite", 0xF8, 0xF8, 0xFF);
            Add(colors, "gold", 0xFF, 0xD7, 0x00);
            Add(colors, "goldenrod", 0xDA, 0xA5, 0x20);
            Add(colors, "gray", 0x80, 0x80, 0x80);
            Add(colors, "green", 0x00, 0x80, 0x00);
            Add(colors, "greenyellow", 0xAD, 0xFF, 0x2F);
            Add(colors, "grey", 0x80, 0x80, 0x80);
            Add(colors, "honeydew", 0xF0, 0xFF, 0xF0);
            Add(colors, "hotpink", 0xFF, 0x69, 0xB4);
            Add(colors, "indianred", 0xCD, 0x5C, 0x5C);
            Add(colors, "indigo", 0x4B, 0x00, 0x82);
            Add(colors, "ivory", 0xFF, 0xFF, 0xF0);
            Add(colors, "khaki", 0xF0, 0xE6, 0x8C);
            Add(colors, "lavender", 0xE6, 0xE6, 0xFA);
            Add(colors, "lavenderblush", 0xFF, 0xF0, 0xF5);
            Add(colors, "lawngreen", 0x7C, 0xFC, 0x00);
            Add(colors, "lemonchiffon", 0xFF, 0xFA, 0xCD);
            Add(colors, "lightblue", 0xAD, 0xD8, 0xE6);
            Add(colors, "lightcoral", 0xF0, 0x80, 0x80);
            Add(colors, "lightcyan", 0xE0, 0xFF, 0xFF);
            Add(colors, "lightgoldenrodyellow", 0xFA, 0xFA, 0xD2);
            Add(colors, "lightgray", 0xD3, 0xD3, 0xD3);
            Add(colors, "lightgreen", 0x90, 0xEE, 0x90);
            Add(colors, "lightgrey", 0xD3, 0xD3, 0xD3);
            Add(colors, "lightpink", 0xFF, 0xB6, 0xC1);
            Add(colors, "lightsalmon", 0xFF, 0xA0, 0x7A);
            Add(colors, "lightseagreen", 0x20, 0xB2, 0xAA);
            Add(colors, "lightskyblue", 0x87, 0xCE, 0xFA);
            Add(colors, "lightslategray", 0x77, 0x88, 0x99);
            Add(colors, "lightslategrey", 0x77, 0x88, 0x99);
            Add(colors, "lightsteelblue", 0xB0, 0xC4, 0xDE);
            Add(colors, "lightyellow", 0xFF, 0xFF, 0xE0);
            Add(colors, "lime", 0x00, 0xFF, 0x00);
            Add(colors, "limegreen", 0x32, 0xCD, 0x32);
            Add(colors, "linen", 0xFA, 0xF0, 0xE6);
            Add(colors, "magenta", 0xFF, 0x00, 0xFF);
            Add(colors, "maroon", 0x80, 0x00, 0x00);
            Add(colors, "mediumaquamarine", 0x66, 0xCD, 0xAA);
            Add(colors, "mediumblue", 0x00, 0x00, 0xCD);
            Add(colors, "mediumorchid", 0xBA, 0x55, 0xD3);
            Add(colors, "mediumpurple", 0x93, 0x70, 0xDB);
            Add(colors, "mediumseagreen", 0x3C, 0xB3, 0x71);
            Add(colors, "mediumslateblue", 0x7B, 0x68, 0xEE);
            Add(colors, "mediumspringgreen", 0x00, 0xFA, 0x9A);
            Add(colors, "mediumturquoise", 0x48, 0xD1, 0xCC);
            Add(colors, "mediumvioletred", 0xC7, 0x15, 0x85);
            Add(colors, "midnightblue", 0x19, 0x19, 0x70);
            Add(colors, "mintcream", 0xF5, 0xFF, 0xFA);
            Add(colors, "mistyrose", 0xFF, 0xE4, 0xE1);
            Add(colors, "moccasin", 0xFF, 0xE4, 0xB5);
            Add(colors, "navajowhite", 0xFF, 0xDE, 0xAD);
            Add(colors, "navy", 0x00, 0x00, 0x80);
            Add(colors, "oldlace", 0xFD, 0xF5, 0xE6);
            Add(colors, "olive", 0x80, 0x80, 0x00);
            Add(colors, "olivedrab", 0x6B, 0x8E, 0x23);
            Add(colors, "orange", 0xFF, 0xA5, 0x00);
            Add(colors, "orangered", 0xFF, 0x45, 0x00);
            Add(colors, "orchid", 0xDA, 0x70, 0xD6);
            Add(colors, "palegoldenrod", 0xEE, 0xE8, 0xAA);
            Add(colors, "palegreen", 0x98, 0xFB, 0x98);
            Add(colors, "paleturquoise", 0xAF, 0xEE, 0xEE);
            Add(colors, "palevioletred", 0xDB, 0x70, 0x93);
            Add(colors, "papayawhip", 0xFF, 0xEF, 0xD5);
            Add(colors, "peachpuff", 0xFF, 0xDA, 0xB9);
            Add(colors, "peru", 0xCD, 0x85, 0x3F);
            Add(colors, "pink", 0xFF, 0xC0, 0xCB);
            Add(colors, "plum", 0xDD, 0xA0, 0xDD);
            Add(colors, "powderblue", 0xB0, 0xE0, 0xE6);
            Add(colors, "purple", 0x80, 0x00, 0x80);
            Add(colors, "rebeccapurple", 0x66, 0x33, 0x99);
            Add(colors, "red", 0xFF, 0x00, 0x00);
            Add(colors, "rosybrown", 0xBC, 0x8F, 0x8F);
            Add(colors, "royalblue", 0x41, 0x69, 0xE1);
            Add(colors, "saddlebrown", 0x8B, 0x45, 0x13);
            Add(colors, "salmon", 0xFA, 0x80, 0x72);
            Add(colors, "sandybrown", 0xF4, 0xA4, 0x60);
            Add(colors, "seagreen", 0x2E, 0x8B, 0x57);
            Add(colors, "seashell", 0xFF, 0xF5, 0xEE);
            Add(colors, "sienna", 0xA0, 0x52, 0x2D);
            Add(colors, "silver", 0xC0, 0xC0, 0xC0);
            Add(colors, "skyblue", 0x87, 0xCE, 0xEB);
            Add(colors, "slateblue", 0x6A, 0x5A, 0xCD);
            Add(colors, "slategray", 0x70, 0x80, 0x90);
            Add(colors, "slategrey", 0x70, 0x80, 0x90);
            Add(colors, "snow", 0xFF, 0xFA, 0xFA);
            Add(colors, "springgreen", 0x00, 0xFF, 0x7F);
            Add(colors, "steelblue", 0x46, 0x82, 0xB4);
            Add(colors, "tan", 0xD2, 0xB4, 0x8C);
            Add(colors, "teal", 0x00, 0x80, 0x80);
            Add(colors, "thistle", 0xD8, 0xBF, 0xD8);
            Add(colors, "tomato", 0xFF, 0x63, 0x47);
            Add(colors, "transparent", 0x00, 0x00, 0x00, 0x00);
            Add(colors, "turquoise", 0x40, 0xE0, 0xD0);
            Add(colors, "violet", 0xEE, 0x82, 0xEE);
            Add(colors, "wheat", 0xF5, 0xDE, 0xB3);
            Add(colors, "white", 0xFF, 0xFF, 0xFF);
            Add(colors, "whitesmoke", 0xF5, 0xF5, 0xF5);
            Add(colors, "yellow", 0xFF, 0xFF, 0x00);
            Add(colors, "yellowgreen", 0x9A, 0xCD, 0x32);

            Add(colors, "clear", 0x00, 0x00, 0x00, 0x00);
            Add(colors, "light red", 0xFF, 0x7F, 0x7F);
            Add(colors, "dark red", 0x8B, 0x00, 0x00);
            Add(colors, "light orange", 0xFF, 0xD0, 0x8A);
            Add(colors, "light purple", 0xC8, 0xA2, 0xC8);
            Add(colors, "dark purple", 0x4B, 0x00, 0x82);
            Add(colors, "light brown", 0xC4, 0xA4, 0x84);
            Add(colors, "dark brown", 0x5C, 0x40, 0x33);
            Add(colors, "bright green", 0x00, 0xFF, 0x00);
            Add(colors, "bright blue", 0x00, 0x80, 0xFF);
            Add(colors, "bright red", 0xFF, 0x00, 0x00);
            return colors;
        }

        static void Add(Dictionary<string, Color32> colors, string name, byte r, byte g, byte b, byte a = 255)
        {
            colors[NormalizeName(name)] = new Color32(r, g, b, a);
        }
    }
}
