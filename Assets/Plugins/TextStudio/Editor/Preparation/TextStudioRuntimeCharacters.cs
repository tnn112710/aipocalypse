using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TextStudio.Editor
{
    /// <summary>Character sets for text that changes while playing, such as scores, timers or player names.</summary>
    [Flags]
    internal enum TextStudioRuntimeCharacterSet
    {
        None = 0,
        Digits = 1 << 0,
        UppercaseLatin = 1 << 1,
        LowercaseLatin = 1 << 2,
        Punctuation = 1 << 3,
        Latin1Supplement = 1 << 4,
        LatinExtendedA = 1 << 5,
        Greek = 1 << 6,
        Cyrillic = 1 << 7
    }

    /// <summary>Optional coverage that preparation adds to every text's known text. Unlike authored text, characters a font
    /// does not contain are skipped rather than reported, so choosing a set never blocks preparation.</summary>
#if UNITY_6000_7_OR_NEWER
    // Character-set definitions are immutable editor metadata.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class TextStudioRuntimeCharacters
    {
        internal readonly struct Entry
        {
            internal readonly TextStudioRuntimeCharacterSet Set;
            internal readonly string Label, Name, Tooltip;
            internal readonly bool Common;

            internal Entry(TextStudioRuntimeCharacterSet set, string label, string name, string tooltip, bool common)
            {
                Set = set;
                Label = label;
                Name = name;
                Tooltip = tooltip;
                Common = common;
            }
        }

        internal static readonly Entry[] Entries =
        {
            new Entry(TextStudioRuntimeCharacterSet.Digits, "Digits 0–9", "digits", "Scores, timers, counters and prices.", true),
            new Entry(TextStudioRuntimeCharacterSet.UppercaseLatin, "A–Z", "A–Z", "Uppercase Latin letters for names, codes and titles.", true),
            new Entry(TextStudioRuntimeCharacterSet.LowercaseLatin, "a–z", "a–z", "Lowercase Latin letters for names and messages.", true),
            new Entry(TextStudioRuntimeCharacterSet.Punctuation, "Punctuation", "punctuation", "Basic punctuation and symbols such as ! ? : % + - and quotes.", true),
            new Entry(TextStudioRuntimeCharacterSet.Latin1Supplement, "Latin-1 (é, ü, ß…)", "Latin-1", "Accented letters and symbols of Western European languages.", false),
            new Entry(TextStudioRuntimeCharacterSet.LatinExtendedA, "Latin Extended-A (ą, č, ő…)", "Latin Extended-A", "Letters of Central and Eastern European languages.", false),
            new Entry(TextStudioRuntimeCharacterSet.Greek, "Greek", "Greek", "Greek letters.", false),
            new Entry(TextStudioRuntimeCharacterSet.Cyrillic, "Cyrillic", "Cyrillic", "Cyrillic letters.", false)
        };

        internal static string Characters(TextStudioRuntimeCharacterSet set)
        {
            switch (set)
            {
                case TextStudioRuntimeCharacterSet.Digits: return "0123456789";
                case TextStudioRuntimeCharacterSet.UppercaseLatin: return "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                case TextStudioRuntimeCharacterSet.LowercaseLatin: return "abcdefghijklmnopqrstuvwxyz";
                case TextStudioRuntimeCharacterSet.Punctuation: return "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";
                // Block ranges match Text Studio 3D project coverage; unassigned code points are skipped like any missing glyph.
                case TextStudioRuntimeCharacterSet.Latin1Supplement: return Range(0x00A0, 0x00FF);
                case TextStudioRuntimeCharacterSet.LatinExtendedA: return Range(0x0100, 0x017F);
                case TextStudioRuntimeCharacterSet.Greek: return Range(0x0370, 0x03FF);
                case TextStudioRuntimeCharacterSet.Cyrillic: return Range(0x0400, 0x04FF);
                default: return string.Empty;
            }
        }

        /// <summary>One plain-character sample per selected set. Samples are not markup and must not be parsed as tags.</summary>
        internal static IEnumerable<string> Samples(TextStudioRuntimeCharacterSet sets)
        {
            foreach (Entry entry in Entries)
                if ((sets & entry.Set) != 0) yield return Characters(entry.Set);
        }

        /// <summary>Short product wording such as "digits, A–Z and your phrases", or empty when nothing is added.</summary>
        internal static string Describe(TextStudioRuntimeCharacterSet sets, string phrases)
        {
            List<string> parts = Entries.Where(entry => (sets & entry.Set) != 0).Select(entry => entry.Name).ToList();
            if (!string.IsNullOrWhiteSpace(phrases)) parts.Add("your phrases");
            if (parts.Count <= 1) return parts.Count == 0 ? string.Empty : parts[0];
            return string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[parts.Count - 1];
        }

        static string Range(int first, int last)
        {
            StringBuilder characters = new StringBuilder(last - first + 1);
            for (int codepoint = first; codepoint <= last; codepoint++) characters.Append((char)codepoint);
            return characters.ToString();
        }
    }
}
