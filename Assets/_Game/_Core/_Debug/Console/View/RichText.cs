using System.Text;
using UnityEngine;

namespace MB13.Aipocalypse.Core.Debugging
{
    // TMP rich-text pieces written straight into a StringBuilder, without string allocations.
    public static class RichText
    {
        const string Hex = "0123456789ABCDEF";

        public static StringBuilder AppendColor(this StringBuilder text, Color color) => text.Append("<color=#").AppendHex(color).Append('>');

        public static StringBuilder AppendMark(this StringBuilder text, Color color) => text.Append("<mark=#").AppendHex(color).Append('>');

        // Text shown as typed: tags inside it are not parsed.
        public static StringBuilder AppendLiteral(this StringBuilder text, string value, int start, int count) =>
            count <= 0 ? text : text.Append("<noparse>").Append(value, start, count).Append("</noparse>");

        public static StringBuilder AppendLiteral(this StringBuilder text, string value) => text.AppendLiteral(value, 0, value?.Length ?? 0);

        static StringBuilder AppendHex(this StringBuilder text, Color color)
        {
            Color32 c = color;
            return text.AppendByte(c.r).AppendByte(c.g).AppendByte(c.b).AppendByte(c.a);
        }

        static StringBuilder AppendByte(this StringBuilder text, byte value) => text.Append(Hex[value >> 4]).Append(Hex[value & 15]);
    }
}
