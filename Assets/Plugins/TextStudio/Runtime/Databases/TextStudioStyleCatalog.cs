using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Style-specific identity validation without changing other catalog ownership contracts.</summary>
    internal static class TextStudioStyleCatalog
    {
        internal static void Register<T>(TextStudioDatabase<T> database, T style, string id, bool replaceExisting) where T : class
        {
            if (style == null) throw new ArgumentNullException(nameof(style));
            if (!TextStudioAppearanceTagSyntax.IsValidId(id))
                throw new ArgumentException("Assign a valid Tag ID before registering a style.", nameof(style));
            if (database.TryGetQuiet(id, out T current))
            {
                if (ReferenceEquals(current, style)) return;
                if (!replaceExisting)
                    throw new InvalidOperationException("Style ID '" + id + "' is already registered. Use replaceExisting: true to install a runtime override explicitly.");
            }
            database.Register(id, style, !replaceExisting);
        }

        internal static Dictionary<string, T> BuildProjectEntries<T>(IReadOnlyList<T> entries, Func<T, string> getId, string label) where T : UnityEngine.Object
        {
            Dictionary<string, T> result = new Dictionary<string, T>(TextStudioIgnoreCaseComparer.Instance);
            HashSet<string> ambiguous = new HashSet<string>(TextStudioIgnoreCaseComparer.Instance);
            if (entries == null) return result;
            for (int i = 0; i < entries.Count; i++)
            {
                T entry = entries[i];
                string id = entry != null ? getId(entry) : string.Empty;
                if (entry == null || !TextStudioAppearanceTagSyntax.IsValidId(id))
                {
                    Debug.LogWarning("[TextStudio] " + label + ": Catalog entry " + (i + 1) + " has a missing asset or invalid Tag ID and is unavailable to tags.");
                    continue;
                }
                if (ambiguous.Contains(id)) continue;
                if (result.ContainsKey(id))
                {
                    result.Remove(id);
                    ambiguous.Add(id);
                    Debug.LogWarning("[TextStudio] " + label + ": Duplicate Tag ID '" + id + "'. Neither catalog entry is available until the conflict is resolved.");
                    continue;
                }
                result.Add(id, entry);
            }
            return result;
        }

        internal static string FindError<T>(IReadOnlyList<T> entries, Func<T, string> getId, string label) where T : UnityEngine.Object
        {
            if (entries == null) return null;
            HashSet<string> ids = new HashSet<string>(TextStudioIgnoreCaseComparer.Instance);
            for (int i = 0; i < entries.Count; i++)
            {
                T entry = entries[i];
                if (entry == null) return label + " contains a missing style at entry " + (i + 1) + ". Remove or replace it.";
                string id = getId(entry);
                if (!TextStudioAppearanceTagSyntax.IsValidId(id)) return label + " contains an invalid Tag ID on '" + entry.name + "'. Assign a valid ID in the style inspector.";
                if (!ids.Add(id)) return label + " contains duplicate Tag ID '" + id + "'. Keep one registered style for each ID.";
            }
            return null;
        }
    }
}
